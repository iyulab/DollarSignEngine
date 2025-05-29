using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;
using System.Text;
using System.Text.RegularExpressions;

namespace DollarSignEngine.Internals;

/// <summary>
/// Core expression evaluation engine with enhanced security and performance.
/// </summary>
internal class ExpressionEvaluator : IDisposable
{
    private readonly LruCache<string, ScriptRunner<object>> _scriptCache;
    private readonly int _timeoutMs;
    private volatile bool _disposed;

    private long _totalEvaluations;
    private long _cacheHits;

    private static readonly Regex SimpleIdentifierRegex =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex SimplePropertyPathRegex =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);

    public ExpressionEvaluator(int cacheSize = 1000, TimeSpan? cacheTtl = null, int timeoutMs = 5000)
    {
        _scriptCache = new LruCache<string, ScriptRunner<object>>(
            cacheSize,
            cacheTtl ?? TimeSpan.FromHours(1));
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Gets performance metrics for monitoring.
    /// </summary>
    public (long TotalEvaluations, long CacheHits, double HitRate) GetMetrics()
    {
        var total = Interlocked.Read(ref _totalEvaluations);
        var hits = Interlocked.Read(ref _cacheHits);
        return (total, hits, total == 0 ? 0.0 : (double)hits / total);
    }

    /// <summary>
    /// Evaluates template string, replacing expressions with computed values.
    /// </summary>
    public async Task<string> EvaluateAsync(string template, object? context, DollarSignOptions options)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        ThrowIfDisposed();
        Interlocked.Increment(ref _totalEvaluations);

        // Prepare evaluation context
        var evaluationContext = PrepareEvaluationContext(context, options);

        var work = TemplateEscaper.EscapeBlocks(template);
        var sb = new StringBuilder(template.Length * 2);
        int i = 0, n = work.Length;

        while (i < n)
        {
            char currentChar = work[i];

            if (currentChar == '}' && i + 1 < n && work[i + 1] == '}')
            {
                sb.Append('}');
                i += 2;
                continue;
            }

            bool isDollarExpr = options.SupportDollarSignSyntax && currentChar == '$' && i + 1 < n && work[i + 1] == '{';
            bool isRegularExpr = !options.SupportDollarSignSyntax && currentChar == '{';

            if (isDollarExpr || isRegularExpr)
            {
                int offset = isDollarExpr ? 2 : 1;
                int exprStart = i + offset;
                int exprEnd = FindMatchingBrace(work.AsSpan(), exprStart, '{', '}');

                if (exprEnd < 0)
                {
                    sb.Append(work.AsSpan(i, offset));
                    i += offset;
                    continue;
                }

                string content = work.Substring(exprStart, exprEnd - exprStart);
                string expr = content;
                string? format = null;
                string? align = null;

                int fmtPos = FindFirstUnnestedChar(expr, ':');
                if (fmtPos >= 0)
                {
                    format = expr[(fmtPos + 1)..].Trim();
                    expr = expr[..fmtPos].Trim();
                }

                int alignPos = FindFirstUnnestedChar(expr, ',');
                if (alignPos >= 0 && (fmtPos < 0 || alignPos < fmtPos))
                {
                    align = expr[(alignPos + 1)..].Trim();
                    expr = expr[..alignPos].Trim();
                }

                string finalExpr = expr.Trim();
                object? value = null;

                try
                {
                    if (!SecurityValidator.IsSafeExpression(finalExpr, options.SecurityLevel))
                    {
                        throw new DollarSignEngineException($"Expression blocked by security policy: {finalExpr}");
                    }

                    if (options.VariableResolver != null)
                        value = options.VariableResolver(finalExpr);

                    if (value == null)
                    {
                        if (string.IsNullOrWhiteSpace(finalExpr))
                        {
                            value = string.Empty;
                        }
                        else if (SimplePropertyPathRegex.IsMatch(finalExpr) &&
                                !(evaluationContext.Context is ScriptHost) &&
                                !(evaluationContext.Context is DictionaryWrapper) &&
                                finalExpr.Contains('.'))
                        {
                            value = PropertyResolver.ResolveNestedProperty(evaluationContext.Context, finalExpr);
                        }
                        else
                        {
                            value = await EvaluateScriptAsync(finalExpr, evaluationContext, options);
                        }
                    }
                }
                catch (Exception ex)
                {
                    bool handled = false;

                    if (options.ErrorHandler != null)
                    {
                        handled = true;
                    }
                    else if (options.ThrowOnError)
                    {
                        if (ex is DollarSignEngineException dse && dse.InnerException is CompilationException) throw;
                        if (ex is DollarSignEngineException) throw;
                        throw new DollarSignEngineException($"Error evaluating expression: '{finalExpr}'", ex);
                    }
                    else if (options.TreatUndefinedVariablesInSimpleExpressionsAsEmpty &&
                              SimpleIdentifierRegex.IsMatch(finalExpr) &&
                              !finalExpr.Contains('.'))
                    {
                        handled = true;
                        value = null;
                    }
                    else if (IsVariableNotFoundError(ex))
                    {
                        handled = true;
                        value = null;
                    }

                    if (!handled)
                    {
                        sb.Append($"[ERROR: {ex.Message}]");
                        i = exprEnd + 1;
                        continue;
                    }
                }

                sb.Append(ApplyFormat(value, align, format, finalExpr, options));
                i = exprEnd + 1;
                continue;
            }

            if (options.SupportDollarSignSyntax && currentChar == '{')
            {
                sb.Append('{');
                i++;
                continue;
            }

            sb.Append(currentChar);
            i++;
        }

        string result = TemplateEscaper.UnescapeBlocks(sb.ToString());
        return result;
    }

    /// <summary>
    /// Prepares evaluation context from variables and options.
    /// </summary>
    private EvaluationContext PrepareEvaluationContext(object? variables, DollarSignOptions options)
    {
        var localVariables = variables ?? new DollarSign.NoParametersContext();
        var globalData = options.GlobalData;

        var localDict = ConvertToDictionary(localVariables);
        var globalDict = ConvertToDictionary(globalData);

        var mergedDict = MergeDictionaries(globalDict, localDict);

        var globalVariableTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in mergedDict)
        {
            if (kvp.Value != null)
            {
                globalVariableTypes[kvp.Key] = kvp.Value.GetType();
            }
            else
            {
                globalVariableTypes[kvp.Key] = typeof(object);
            }
        }

        var scriptHost = new ScriptHost(mergedDict);
        return new EvaluationContext(scriptHost, globalVariableTypes);
    }

    /// <summary>
    /// Converts object to dictionary with string keys.
    /// </summary>
    private IDictionary<string, object?> ConvertToDictionary(object? obj)
    {
        if (obj == null || obj is DollarSign.NoParametersContext)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (obj is IDictionary<string, object?> dictNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        if (obj is IDictionary<string, object> dictNonNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNonNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        if (obj is ExpandoObject expandoObj)
        {
            var dynamicDict = (IDictionary<string, object?>)expandoObj;
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dynamicDict)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        if (obj is IDictionary dictionary && obj.GetType().IsGenericType)
        {
            Type genericTypeDef = obj.GetType().GetGenericTypeDefinition();
            if (genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IDictionary<,>))
            {
                var keyType = obj.GetType().GetGenericArguments()[0];

                if (keyType == typeof(string) || keyType.IsPrimitive || keyType == typeof(Guid))
                {
                    var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        string key = entry.Key?.ToString() ?? string.Empty;
                        newDict[key] = ConvertToEvalFriendlyObject(entry.Value);
                    }
                    return newDict;
                }
            }
        }

        if (obj is IDictionary nonGenericDict)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in nonGenericDict)
            {
                string key = entry.Key?.ToString() ?? string.Empty;
                newDict[key] = ConvertToEvalFriendlyObject(entry.Value);
            }
            return newDict;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Handle anonymous types specially - convert to dictionary
        if (DataPreparationHelper.IsAnonymousType(obj.GetType()))
        {
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    var propValue = property.GetValue(obj);
                    result[property.Name] = ConvertToEvalFriendlyObject(propValue);
                }
            }
            return result;
        }

        // For regular objects, extract properties but keep the original object intact
        // This preserves LINQ and other functionality
        if (obj != null && !(obj is DollarSign.NoParametersContext))
        {
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    var propValue = property.GetValue(obj);
                    // Only convert the property values, not the root object
                    result[property.Name] = propValue;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Converts object to format suitable for evaluation with proper anonymous type handling.
    /// </summary>
    private object? ConvertToEvalFriendlyObject(object? obj)
    {
        if (obj == null) return null;
        Type type = obj.GetType();

        // Handle anonymous types by converting to ExpandoObject
        if (DataPreparationHelper.IsAnonymousType(type))
        {
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    dict[property.Name] = ConvertToEvalFriendlyObject(property.GetValue(obj));
                }
            }
            return expando;
        }

        // Handle arrays - only convert if they contain anonymous types
        if (type.IsArray)
        {
            var array = (Array)obj;
            Type? elementType = type.GetElementType();

            if (elementType == null) return obj;

            // Only convert arrays containing anonymous types
            if (DataPreparationHelper.IsAnonymousType(elementType))
            {
                var convertedItems = new List<object?>(array.Length);
                foreach (var item in array)
                {
                    convertedItems.Add(ConvertToEvalFriendlyObject(item));
                }
                return convertedItems.ToArray();
            }

            // For arrays of object type that might contain anonymous types, check each element
            if (elementType == typeof(object))
            {
                bool hasAnonymousTypes = false;
                foreach (var item in array)
                {
                    if (item != null && DataPreparationHelper.IsAnonymousType(item.GetType()))
                    {
                        hasAnonymousTypes = true;
                        break;
                    }
                }

                if (hasAnonymousTypes)
                {
                    var convertedItems = new List<object?>(array.Length);
                    foreach (var item in array)
                    {
                        convertedItems.Add(ConvertToEvalFriendlyObject(item));
                    }
                    return convertedItems.ToArray();
                }
            }

            // For all other arrays, return as-is to preserve LINQ functionality
            return obj;
        }

        // Handle collections - only convert if they contain anonymous types
        if (obj is IList list && type.IsGenericType)
        {
            Type listType = type.GetGenericTypeDefinition();
            if (listType == typeof(List<>) || listType == typeof(IList<>))
            {
                Type elementType = type.GetGenericArguments()[0];

                // Only convert if element type is anonymous or object (which might contain anonymous)
                if (DataPreparationHelper.IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    var newList = new List<object?>(list.Count);
                    foreach (var item in list)
                    {
                        newList.Add(ConvertToEvalFriendlyObject(item));
                    }
                    return newList;
                }
            }
        }

        // Handle dictionaries - always convert for consistency
        if (obj is IDictionary<string, object?> dictNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        if (obj is IDictionary<string, object> dictNonNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNonNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        // For all other types (including regular classes), return as-is
        // This preserves LINQ functionality and other object behaviors
        return obj;
    }

    /// <summary>
    /// Merges dictionaries with local taking precedence.
    /// </summary>
    private IDictionary<string, object?> MergeDictionaries(
        IDictionary<string, object?> globalDict,
        IDictionary<string, object?> localDict)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in globalDict)
        {
            result[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in localDict)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// Checks if exception indicates variable not found.
    /// </summary>
    private static bool IsVariableNotFoundError(Exception ex)
    {
        return ex is KeyNotFoundException ||
               ex.InnerException is KeyNotFoundException ||
               ex is RuntimeBinderException ||
               (ex is DollarSignEngineException de && de.InnerException is RuntimeBinderException) ||
               ex.Message.Contains("does not exist in the current context") ||
               ex.Message.Contains("not found") ||
               ex.Message.Contains("undefined");
    }

    /// <summary>
    /// Finds matching brace using Span for performance.
    /// </summary>
    private static int FindMatchingBrace(ReadOnlySpan<char> text, int startIndex, char openBrace, char closeBrace)
    {
        int depth = 1;
        for (int k = startIndex; k < text.Length; k++)
        {
            char currentChar = text[k];

            if (currentChar == '"' || currentChar == '\'')
            {
                char quote = currentChar;
                k++;
                while (k < text.Length)
                {
                    if (text[k] == quote && (k == 0 || text[k - 1] != '\\'))
                        break;
                    if (text[k] == '\\' && k + 1 < text.Length)
                        k++;
                    k++;
                }
                if (k >= text.Length) return -1;
                continue;
            }

            if (currentChar == openBrace) depth++;
            else if (currentChar == closeBrace)
            {
                depth--;
                if (depth == 0) return k;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds first occurrence of character not nested within parentheses or quotes.
    /// </summary>
    private static int FindFirstUnnestedChar(string s, char ch)
    {
        int parenDepth = 0;
        int quoteDepth = 0;
        char currentQuoteChar = '\0';

        for (int k = 0; k < s.Length; k++)
        {
            if (quoteDepth > 0)
            {
                if (s[k] == '\\' && k + 1 < s.Length)
                {
                    k++;
                }
                else if (s[k] == currentQuoteChar)
                {
                    quoteDepth = 0;
                }
            }
            else
            {
                if (s[k] == '"' || s[k] == '\'')
                {
                    quoteDepth = 1;
                    currentQuoteChar = s[k];
                }
                else if (s[k] == '(' || s[k] == '[' || s[k] == '{') parenDepth++;
                else if (s[k] == ')' || s[k] == ']' || s[k] == '}') parenDepth--;
                else if (parenDepth == 0 && s[k] == ch)
                    return k;
            }
        }
        return -1;
    }

    /// <summary>
    /// Applies formatting and alignment to value.
    /// </summary>
    private string ApplyFormat(object? value, string? alignment, string? format, string originalExpression, DollarSignOptions options)
    {
        bool isComplexExpression = originalExpression.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '.');
        CultureInfo culture = options.CultureInfo ??
                             (isComplexExpression ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);

        string? stringValue = null;

        if (value == null)
        {
            stringValue = string.Empty;
        }
        else if (value is string sVal)
        {
            if (string.IsNullOrEmpty(format))
            {
                stringValue = Regex.Replace(sVal, @"\{\{(.*?)\}\}", m => $"{{{m.Groups[1].Value}}}");
            }
            else
            {
                stringValue = sVal;
            }
        }

        if (value == null && !string.IsNullOrEmpty(alignment) && stringValue == null)
        {
            return string.Format(culture, $"{{0,{alignment}}}", string.Empty);
        }

        if (stringValue != null)
        {
            if (string.IsNullOrEmpty(alignment) && string.IsNullOrEmpty(format))
            {
                return stringValue;
            }
        }

        object valueToFormat = stringValue ?? value!;

        string pattern = "{0";
        if (!string.IsNullOrEmpty(alignment)) pattern += "," + alignment;
        if (!string.IsNullOrEmpty(format)) pattern += ":" + format;
        pattern += "}";

        try
        {
            if (valueToFormat is IFormattable formattableValue)
            {
                string formatted = formattableValue.ToString(format, culture);
                if (!string.IsNullOrEmpty(alignment))
                {
                    return string.Format(culture, $"{{0,{alignment}}}", formatted);
                }
                return formatted;
            }

            return string.Format(culture, pattern, valueToFormat);
        }
        catch (FormatException ex)
        {
            Logger.Warning($"Format error for value '{valueToFormat}', format '{format}': {ex.Message}");
            if (options.ThrowOnError) throw;
            return valueToFormat?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Evaluates C# script expression against context object.
    /// </summary>
    private async Task<object?> EvaluateScriptAsync(string expression, EvaluationContext evaluationContext, DollarSignOptions options)
    {
        ScriptRunner<object>? runner;
        string processedExpression = expression;
        Type globalsTypeForScript = typeof(ScriptHost);
        object effectiveGlobals = evaluationContext.Context;

        Logger.Debug($"[EvaluateScriptAsync START] Original Expr: \"{expression}\"");

        // Apply syntax rewriting for complex property access
        if (evaluationContext.GlobalVariableTypes.Any())
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(expression, new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script));
            var rewriter = new CastingDictionaryAccessRewriter(evaluationContext.GlobalVariableTypes);
            var newRoot = rewriter.Visit(syntaxTree.GetRoot());
            processedExpression = newRoot.ToFullString();
            Logger.Debug($"[EvaluateScriptAsync] Rewritten expression: '{processedExpression}'");
        }

        string cacheKey = options.UseCache
            ? $"{globalsTypeForScript.FullName}_{processedExpression}"
            : Guid.NewGuid().ToString();

        if (options.UseCache)
        {
            if (_scriptCache.TryGetValue(cacheKey, out runner))
            {
                Logger.Debug($"[EvaluateScriptAsync] Cache hit for expression: '{processedExpression}'");
                Interlocked.Increment(ref _cacheHits);
            }
            else
            {
                runner = null;
            }
        }
        else
        {
            runner = null;
        }

        if (runner == null)
        {
            var scriptOptions = ScriptOptions.Default
                .AddReferences(AssemblyReferenceHelper.GetReferences(effectiveGlobals, evaluationContext.GlobalVariableTypes.Values.Select(t => t.Assembly).ToArray()))
                .AddImports(
                    "System",
                    "System.Linq",
                    "System.Collections",
                    "System.Collections.Generic",
                    "System.Math",
                    "System.Globalization",
                    "System.Dynamic",
                    "DollarSignEngine"
                );

            Logger.Debug($"[EvaluateScriptAsync] Compiling expression: '{processedExpression}' with globalsType: '{globalsTypeForScript.FullName}'");

            try
            {
                var script = CSharpScript.Create<object>(processedExpression, scriptOptions, globalsType: globalsTypeForScript);
                runner = script.CreateDelegate();

                if (options.UseCache && runner != null)
                {
                    _scriptCache.GetOrAdd(cacheKey, _ => runner);
                }
            }
            catch (CompilationErrorException ex)
            {
                string errorDetails = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
                Logger.Error($"Compilation error: {errorDetails} for expression: {processedExpression}");
                throw new CompilationException($"Error compiling C# script: {processedExpression} (Original: {expression}) | Errors: {errorDetails}", errorDetails, ex);
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error during script compilation: {ex.Message} for expression: {processedExpression}");
                throw new DollarSignEngineException($"Unexpected error creating script delegate for: {processedExpression} (Original: {expression})", ex);
            }
        }

        if (runner == null)
        {
            Logger.Error($"Script runner is null after compilation/cache attempt for key: {cacheKey}");
            throw new DollarSignEngineException($"Failed to create or retrieve script runner for expression: {processedExpression}");
        }

        try
        {
            using var cts = new CancellationTokenSource(_timeoutMs);
            var result = await runner(effectiveGlobals, cts.Token);
            Logger.Debug($"[EvaluateScriptAsync END] Result: \"{result}\"");
            return result;
        }
        catch (OperationCanceledException)
        {
            throw new DollarSignEngineException($"Script execution timed out after {_timeoutMs}ms: {processedExpression}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Runtime error executing script '{processedExpression}': {ex.Message}");
            throw new DollarSignEngineException($"Runtime error executing script: '{processedExpression}' (Original: {expression})", ex);
        }
    }

    /// <summary>
    /// Clears all internal caches.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();
        _scriptCache.Clear();
        TypeAccessorFactory.ClearCache();
        TypeNameHelper.ClearCaches();

        Interlocked.Exchange(ref _totalEvaluations, 0);
        Interlocked.Exchange(ref _cacheHits, 0);

        Logger.Info("[ExpressionEvaluator.ClearCache] All caches cleared.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ExpressionEvaluator));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _scriptCache?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warning($"[ExpressionEvaluator] Error during disposal: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Evaluation context containing script host and variable types.
/// </summary>
internal class EvaluationContext
{
    public ScriptHost Context { get; }
    public IDictionary<string, Type> GlobalVariableTypes { get; }

    public EvaluationContext(ScriptHost context, IDictionary<string, Type> globalVariableTypes)
    {
        Context = context;
        GlobalVariableTypes = globalVariableTypes;
    }
}