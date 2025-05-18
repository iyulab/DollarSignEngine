using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis;
using System.Collections;
using System.Dynamic;

namespace DollarSignEngine.Internals;

internal class ExpressionEvaluator
{
    private readonly Dictionary<string, ScriptRunner<object>> _scriptCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    private readonly Dictionary<Type, TypeAccessor> _typeAccessors = new();
    private readonly object _typeAccessorLock = new();

    // Regular expressions for pattern matching
    private static readonly Regex SimpleIdentifierRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex SimplePropertyPathRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);

    public async Task<string> EvaluateAsync(string template, object? context, DollarSignOptions options)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        var sb = new StringBuilder();
        int i = 0, n = template.Length;

        while (i < n)
        {
            // Handle dollar sign syntax if enabled
            if (options.SupportDollarSignSyntax && i + 1 < n && template[i] == '$' && template[i + 1] == '{')
            {
                int start = i + 2; // Skip ${ prefix
                int end = template.IndexOf('}', start);
                if (end < 0) // No closing brace
                {
                    sb.Append("${"); i += 2; continue;
                }

                string expressionContent = template.Substring(start, end - start);

                // Process dollar sign expression
                string expressionPart = expressionContent;
                string? formatPart = null;
                string? alignmentPart = null;

                // Parse format and alignment specifiers
                int formatSpecifierPos = FindFirstUnnestedChar(expressionPart, ':');
                if (formatSpecifierPos >= 0)
                {
                    formatPart = expressionPart.Substring(formatSpecifierPos + 1).Trim();
                    expressionPart = expressionPart.Substring(0, formatSpecifierPos);
                }

                int alignmentSpecifierPos = FindFirstUnnestedChar(expressionPart, ',');
                if (alignmentSpecifierPos >= 0)
                {
                    alignmentPart = expressionPart.Substring(alignmentSpecifierPos + 1).Trim();
                    expressionPart = expressionPart.Substring(0, alignmentSpecifierPos);
                }

                string finalExpressionToEvaluate = expressionPart.Trim();
                object? value = null;

                try
                {
                    // Try to evaluate the expression using the most efficient approach
                    if (options.VariableResolver != null && SimpleIdentifierRegex.IsMatch(finalExpressionToEvaluate))
                    {
                        // Path 1: Custom Variable Resolver (simple identifiers)
                        value = options.VariableResolver(finalExpressionToEvaluate);
                    }
                    else if (SimplePropertyPathRegex.IsMatch(finalExpressionToEvaluate) && finalExpressionToEvaluate.Contains("."))
                    {
                        // Path 2: Simple Property Path (e.g., obj.Prop1.NestedProp)
                        value = ResolveNestedProperty(context, finalExpressionToEvaluate);
                    }
                    else
                    {
                        // Path 3: Complex C# Script Evaluation
                        value = await EvaluateScriptAsync(finalExpressionToEvaluate, context, options);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[ExpressionEvaluator.EvaluateAsync] Exception evaluating expression '{finalExpressionToEvaluate}': {ex.GetType().Name} - {ex.Message}");
                    if (options.ErrorHandler != null)
                    {
                        sb.Append(options.ErrorHandler(finalExpressionToEvaluate, ex));
                    }
                    else if (options.ThrowOnError)
                    {
                        if (ex is DollarSignEngineException)
                            throw;
                        throw new DollarSignEngineException($"Error evaluating expression part: '{finalExpressionToEvaluate}'", ex);
                    }
                }

                sb.Append(ApplyFormat(value, alignmentPart, formatPart, finalExpressionToEvaluate, options));
                i = end + 1;
                continue;
            }

            // Handle escape sequences: {{ -> { , }} -> }
            if (i + 1 < n && template[i] == '{' && template[i + 1] == '{')
            {
                sb.Append('{'); i += 2; continue;
            }
            if (i + 1 < n && template[i] == '}' && template[i + 1] == '}')
            {
                sb.Append('}'); i += 2; continue;
            }

            // When dollar sign syntax is enabled, treat regular braces as literal
            if (options.SupportDollarSignSyntax && template[i] == '{')
            {
                // Find the matching closing brace
                int start = i;
                int end = template.IndexOf('}', start + 1);
                if (end < 0) // No closing brace
                {
                    sb.Append('{'); i++; continue;
                }

                // Include the entire {expression} as literal text
                sb.Append(template.Substring(start, end - start + 1));
                i = end + 1;
                continue;
            }

            // Regular expression evaluation when dollar sign syntax is disabled
            if (!options.SupportDollarSignSyntax && template[i] == '{') // Expression start
            {
                // Original expression evaluation logic
                int start = i + 1;
                int end = template.IndexOf('}', start);
                if (end < 0) // No closing brace
                {
                    sb.Append('{'); i++; continue;
                }

                string innerExpressionContent = template.Substring(start, end - start);

                string expressionPart = innerExpressionContent;
                string? formatPart = null;
                string? alignmentPart = null;

                // Parse format and alignment specifiers
                int formatSpecifierPos = FindFirstUnnestedChar(expressionPart, ':');
                if (formatSpecifierPos >= 0)
                {
                    formatPart = expressionPart.Substring(formatSpecifierPos + 1).Trim();
                    expressionPart = expressionPart.Substring(0, formatSpecifierPos);
                }

                int alignmentSpecifierPos = FindFirstUnnestedChar(expressionPart, ',');
                if (alignmentSpecifierPos >= 0)
                {
                    alignmentPart = expressionPart.Substring(alignmentSpecifierPos + 1).Trim();
                    expressionPart = expressionPart.Substring(0, alignmentSpecifierPos);
                }

                string finalExpressionToEvaluate = expressionPart.Trim();
                object? value = null;

                try
                {
                    // Try to evaluate the expression using the most efficient approach
                    if (options.VariableResolver != null && SimpleIdentifierRegex.IsMatch(finalExpressionToEvaluate))
                    {
                        // Path 1: Custom Variable Resolver (simple identifiers)
                        value = options.VariableResolver(finalExpressionToEvaluate);
                    }
                    else if (SimplePropertyPathRegex.IsMatch(finalExpressionToEvaluate) && finalExpressionToEvaluate.Contains("."))
                    {
                        // Path 2: Simple Property Path (e.g., obj.Prop1.NestedProp)
                        value = ResolveNestedProperty(context, finalExpressionToEvaluate);
                    }
                    else
                    {
                        // Path 3: Complex C# Script Evaluation
                        value = await EvaluateScriptAsync(finalExpressionToEvaluate, context, options);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[ExpressionEvaluator.EvaluateAsync] Exception evaluating expression '{finalExpressionToEvaluate}': {ex.GetType().Name} - {ex.Message}");
                    if (options.ErrorHandler != null)
                    {
                        sb.Append(options.ErrorHandler(finalExpressionToEvaluate, ex));
                    }
                    else if (options.ThrowOnError)
                    {
                        if (ex is DollarSignEngineException)
                            throw;
                        throw new DollarSignEngineException($"Error evaluating expression part: '{finalExpressionToEvaluate}'", ex);
                    }
                }

                sb.Append(ApplyFormat(value, alignmentPart, formatPart, finalExpressionToEvaluate, options));
                i = end + 1;
            }
            else
            {
                sb.Append(template[i]); i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Finds the first occurrence of a character in a string that is not nested in parentheses.
    /// </summary>
    private static int FindFirstUnnestedChar(string s, char ch)
    {
        int depth = 0;
        for (int k = 0; k < s.Length; k++)
        {
            if (s[k] == '(') depth++;
            else if (s[k] == ')') depth--;
            else if (depth == 0 && s[k] == ch)
                return k;
        }
        return -1; // Not found
    }

    /// <summary>
    /// Applies format and alignment to a value.
    /// </summary>
    private string ApplyFormat(object? value, string? alignment, string? format, string originalExpression, DollarSignOptions options)
    {
        bool isComplexExpression = originalExpression.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '.');

        // Use the culture from options if provided, otherwise use appropriate default
        CultureInfo culture = options.CultureInfo ??
                            (isComplexExpression ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);

        if (value == null)
        {
            if (!string.IsNullOrEmpty(alignment))
                return string.Format(culture, $"{{0,{alignment}}}", string.Empty);
            return string.Empty;
        }

        if (string.IsNullOrEmpty(alignment) && string.IsNullOrEmpty(format))
            return value.ToString() ?? string.Empty;

        string pattern = "{0";
        if (!string.IsNullOrEmpty(alignment)) pattern += "," + alignment;
        if (!string.IsNullOrEmpty(format)) pattern += ":" + format;
        pattern += "}";

        try
        {
            if (value is IFormattable formattableValue)
            {
                return formattableValue.ToString(format, culture);
            }
            return string.Format(culture, pattern, value);
        }
        catch (FormatException ex)
        {
            Logger.Debug($"[ExpressionEvaluator.ApplyFormat] FormatException for value '{value}', format '{format}'. Error: {ex.Message}");
            return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Evaluates a C# script expression.
    /// </summary>
    private async Task<object?> EvaluateScriptAsync(string expression, object? context, DollarSignOptions options)
    {
        ScriptRunner<object>? runner = null;
        string cacheKey = expression + "|" + (context?.GetType().FullName ?? "<null_context_type>");

        if (options.UseCache)
        {
            lock (_cacheLock)
            {
                _scriptCache.TryGetValue(cacheKey, out runner);
            }
        }

        if (runner == null)
        {
            var scriptOptions = ScriptOptions.Default
                .AddReferences(GetReferences(context))
                .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Math",
                            "System.Globalization", "System.Dynamic");

            Type? globalsTypeToUse = context is DictionaryWrapper ?
                typeof(DictionaryWrapper) : context?.GetType();

            Logger.Debug($"[ExpressionEvaluator.EvaluateScriptAsync] Compiling expression: '{expression}' with globalsType: '{globalsTypeToUse?.FullName ?? "null"}'");

            var script = CSharpScript.Create<object>(expression, scriptOptions, globalsType: globalsTypeToUse);

            try
            {
                runner = script.CreateDelegate();
            }
            catch (CompilationErrorException ex)
            {
                string errorDetails = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
                Logger.Debug($"[ExpressionEvaluator.EvaluateScriptAsync] CompilationErrorException: {errorDetails}");
                throw new CompilationException($"Error compiling C# script: {expression}", errorDetails, ex);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ExpressionEvaluator.EvaluateScriptAsync] Exception: {ex.Message}");
                throw new DollarSignEngineException($"Unexpected error creating script delegate for: {expression}", ex);
            }

            if (options.UseCache && runner != null)
            {
                lock (_cacheLock)
                {
                    _scriptCache[cacheKey] = runner;
                }
            }
        }

        if (runner == null)
        {
            Logger.Debug($"[ExpressionEvaluator.EvaluateScriptAsync] Runner is null after compilation attempt");
            return null;
        }

        try
        {
            return await runner(context, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.Debug($"[ExpressionEvaluator.EvaluateScriptAsync] Runtime error: {ex.Message}");
            throw new DollarSignEngineException($"Runtime error executing script: '{expression}'", ex);
        }
    }

    /// <summary>
    /// Resolves a nested property path on an object.
    /// </summary>
    private object? ResolveNestedProperty(object? source, string path)
    {
        if (source == null || string.IsNullOrEmpty(path))
            return null;

        object? current = source;
        string[] parts = path.Split('.');

        foreach (var part in parts)
        {
            if (current == null) return null;

            // Handle different types of containers
            if (current is DictionaryWrapper dw)
            {
                current = dw.TryGetValue(part);
                continue;
            }

            if (current is IDictionary<string, object> genericDict)
            {
                if (genericDict.TryGetValue(part, out var dictValue))
                {
                    current = dictValue;
                }
                else
                {
                    var insensitiveKey = genericDict.Keys.FirstOrDefault(k =>
                        string.Equals(k, part, StringComparison.OrdinalIgnoreCase));

                    if (insensitiveKey != null && genericDict.TryGetValue(insensitiveKey, out dictValue))
                    {
                        current = dictValue;
                    }
                    else return null;
                }
                continue;
            }

            if (current is IDictionary nonGenericDict)
            {
                object? tempKey = part;

                if (!nonGenericDict.Contains(tempKey))
                {
                    foreach (var key in nonGenericDict.Keys)
                    {
                        if (key is string keyStr &&
                            string.Equals(keyStr, part, StringComparison.OrdinalIgnoreCase))
                        {
                            tempKey = key;
                            break;
                        }
                    }
                }

                if (tempKey != null && nonGenericDict.Contains(tempKey))
                {
                    current = nonGenericDict[tempKey];
                }
                else return null;

                continue;
            }

            // Use type accessor for general objects
            var accessor = GetTypeAccessor(current.GetType());
            if (!accessor.TryGetPropertyValue(current, part, out current))
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Gets reference assemblies for script compilation.
    /// </summary>
    private IEnumerable<MetadataReference> GetReferences(object? context)
    {
        var assemblies = new HashSet<Assembly>();

        // Add essential assemblies
        AddAssembly(assemblies, typeof(object).Assembly);
        AddAssembly(assemblies, typeof(Enumerable).Assembly);
        AddAssembly(assemblies, typeof(List<>).Assembly);
        AddAssembly(assemblies, typeof(Regex).Assembly);
        AddAssembly(assemblies, typeof(Uri).Assembly);
        AddAssembly(assemblies, typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly);
        AddAssembly(assemblies, typeof(IDynamicMetaObjectProvider).Assembly);

        // Add context's assembly if available
        if (context != null)
        {
            AddAssembly(assemblies, context.GetType().Assembly);
        }

        // Ensure DollarSignEngine assembly is always added
        AddAssembly(assemblies, typeof(ExpressionEvaluator).Assembly);

        return assemblies
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();
    }

    private void AddAssembly(HashSet<Assembly> assemblies, Assembly? assembly)
    {
        if (assembly != null)
        {
            try
            {
                if (!string.IsNullOrEmpty(assembly.Location))
                {
                    assemblies.Add(assembly);
                }
            }
            catch (NotSupportedException)
            {
                Logger.Debug($"[ExpressionEvaluator.AddAssembly] Assembly '{assembly.FullName}' skipped - invalid location.");
            }
        }
    }

    /// <summary>
    /// Clears the script and type accessor caches.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _scriptCache.Clear();
            Logger.Debug("[ExpressionEvaluator.ClearCache] Script cache cleared.");
        }

        lock (_typeAccessorLock)
        {
            _typeAccessors.Clear();
            Logger.Debug("[ExpressionEvaluator.ClearCache] Type accessor cache cleared.");
        }
    }

    #region TypeAccessor Inner Class

    /// <summary>
    /// Helper class for fast property access.
    /// </summary>
    private class TypeAccessor
    {
        private readonly Dictionary<string, Func<object, object?>> _getters = new(StringComparer.OrdinalIgnoreCase);

        public void AddGetter(string name, Func<object, object?> getter) => _getters[name] = getter;

        public bool TryGetPropertyValue(object instance, string propName, out object? value)
        {
            if (_getters.TryGetValue(propName, out var getter))
            {
                try
                {
                    value = getter(instance);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[TypeAccessor.TryGetPropertyValue] Error: {ex.Message}");
                    value = null;
                    return false;
                }
            }

            value = null;
            return false;
        }
    }

    /// <summary>
    /// Gets a TypeAccessor for the specified type.
    /// </summary>
    private TypeAccessor GetTypeAccessor(Type type)
    {
        lock (_typeAccessorLock)
        {
            if (!_typeAccessors.TryGetValue(type, out var accessor))
            {
                accessor = CreateTypeAccessor(type);
                _typeAccessors[type] = accessor;
            }
            return accessor;
        }
    }

    /// <summary>
    /// Creates a TypeAccessor for the specified type.
    /// </summary>
    private TypeAccessor CreateTypeAccessor(Type type)
    {
        var accessor = new TypeAccessor();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        foreach (var p in props)
        {
            if (p.CanRead)
            {
                accessor.AddGetter(p.Name, inst => p.GetValue(inst));
            }
        }

        return accessor;
    }

    #endregion
}