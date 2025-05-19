using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;
using System.Text;
using System.Text.RegularExpressions;

namespace DollarSignEngine.Internals;

internal static class TemplateEscaper
{
    private static string OPEN = "@@OPEN@@";
    private static string CLOSE = "@@CLOSE@@";

    public static string EscapeBlocks(string template) // 새로운 로직으로 대체
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        // 1단계: "{{" 를 "@@OPEN@@" 으로 정방향 치환
        System.Text.StringBuilder pass1Builder = new System.Text.StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            if (i <= template.Length - 2 && template[i] == '{' && template[i + 1] == '{')
            {
                pass1Builder.Append(OPEN);
                i += 2; // "{{" 두 글자 건너뛰기
            }
            else
            {
                pass1Builder.Append(template[i]);
                i++;
            }
        }
        string intermediateResult = pass1Builder.ToString();

        // 2단계: "}}" 를 "@@CLOSE@@" 으로 역방향 치환
        // 역방향 탐색 및 치환은 StringBuilder.Insert(0, ...)를 사용하여 구현
        System.Text.StringBuilder finalBuilder = new System.Text.StringBuilder();
        int j = intermediateResult.Length - 1;
        while (j >= 0)
        {
            // 현재 위치 j와 그 앞의 j-1 위치를 확인하여 "}}" 패턴을 찾음
            if (j > 0 && intermediateResult[j - 1] == '}' && intermediateResult[j] == '}')
            {
                finalBuilder.Insert(0, CLOSE); // 결과의 맨 앞에 CLOSE 추가
                j -= 2; // "}}" 두 글자 건너뛰기 (인덱스를 2만큼 앞으로 이동)
            }
            else
            {
                finalBuilder.Insert(0, intermediateResult[j]); // 결과의 맨 앞에 현재 문자 추가
                j--;
            }
        }
        return finalBuilder.ToString();
    }

    public static string UnescapeBlocks(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var result = new System.Text.StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            if (i <= template.Length - OPEN.Length && template.Substring(i, OPEN.Length) == OPEN)
            {
                result.Append("{");
                i += OPEN.Length; // Skip @@OPEN@@
            }
            else if (i <= template.Length - CLOSE.Length && template.Substring(i, CLOSE.Length) == CLOSE)
            {
                result.Append("}");
                i += CLOSE.Length; // Skip @@CLOSE@@
            }
            else
            {
                result.Append(template[i]);
                i++;
            }
        }
        return result.ToString();
    }
}

internal class ExpressionEvaluator
{
    private readonly Dictionary<string, ScriptRunner<object>> _scriptCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    private readonly Dictionary<Type, TypeAccessor> _typeAccessors = new();
    private readonly object _typeAccessorLock = new();

    private static readonly Regex SimpleIdentifierRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex SimplePropertyPathRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);

    public async Task<string> EvaluateAsync(string template, object? context, DollarSignOptions options)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        // 1. Preprocess: replace {{...}} escape blocks with unique placeholders
        var work = TemplateEscaper.EscapeBlocks(template);

        // 2. Main processing on tokenized string
        var sb = new StringBuilder();
        int i = 0, n = work.Length;
        while (i < n)
        {
            char currentChar = work[i];

            // Handle literal '}}' -> '}'
            if (currentChar == '}' && i + 1 < n && work[i + 1] == '}')
            {
                sb.Append('}'); i += 2; continue;
            }

            bool isDollarExpr = options.SupportDollarSignSyntax && currentChar == '$' && i + 1 < n && work[i + 1] == '{';
            bool isRegularExpr = !options.SupportDollarSignSyntax && currentChar == '{';

            if (isDollarExpr || isRegularExpr)
            {
                int offset = isDollarExpr ? 2 : 1;
                int exprStart = i + offset;
                int exprEnd = FindMatchingBrace(work, exprStart, '{', '}');
                if (exprEnd < 0)
                {
                    sb.Append(work.Substring(i, offset)); i += offset; continue;
                }

                // Extract expression content
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
                    if (string.IsNullOrWhiteSpace(finalExpr)) value = string.Empty;
                    else if (options.VariableResolver != null && SimpleIdentifierRegex.IsMatch(finalExpr) && !finalExpr.Contains('.'))
                        value = options.VariableResolver(finalExpr);
                    else if (SimplePropertyPathRegex.IsMatch(finalExpr) && context is not ScriptHost && context is not DictionaryWrapper && finalExpr.Contains('.'))
                        value = ResolveNestedProperty(context, finalExpr);
                    else
                        value = await EvaluateScriptAsync(finalExpr, context, options);
                }
                catch (Exception ex)
                {
                    bool handled = false;
                    if (options.ErrorHandler != null)
                    {
                        sb.Append(options.ErrorHandler(finalExpr, ex)); handled = true;
                    }
                    else if (options.ThrowOnError)
                    {
                        if (ex is DollarSignEngineException dse && dse.InnerException is CompilationException) throw;
                        if (ex is DollarSignEngineException) throw;
                        throw new DollarSignEngineException($"Error evaluating expression: '{finalExpr}'", ex);
                    }
                    else if (options.TreatUndefinedVariablesInSimpleExpressionsAsEmpty && SimpleIdentifierRegex.IsMatch(finalExpr) && !finalExpr.Contains('.') &&
                             (ex is KeyNotFoundException || ex.InnerException is KeyNotFoundException || ex is RuntimeBinderException ||
                              (ex is DollarSignEngineException de && de.InnerException is RuntimeBinderException)))
                    {
                        handled = true;
                    }
                }

                sb.Append(ApplyFormat(value, align, format, finalExpr, options));
                i = exprEnd + 1;
                continue;
            }

            // Literal '{' when dollar syntax is enabled
            if (options.SupportDollarSignSyntax && currentChar == '{')
            {
                sb.Append('{'); i++; continue;
            }

            // Default literal char
            sb.Append(currentChar);
            i++;
        }

        // 3. Postprocess: restore escape blocks
        string result = TemplateEscaper.UnescapeBlocks(sb.ToString());
        return result;
    }

    private int FindMatchingBrace(string text, int startIndex, char openBrace, char closeBrace)
    {
        int depth = 1;
        for (int k = startIndex; k < text.Length; k++)
        {
            // Basic string literal skipping (does not handle escaped quotes within strings perfectly)
            if (text[k] == '"' || text[k] == '\'')
            {
                char quote = text[k];
                k++;
                while (k < text.Length && (text[k] != quote || text[k - 1] == '\\'))
                {
                    k++;
                }
                if (k >= text.Length) return -1; // Unterminated string
                continue;
            }

            if (text[k] == openBrace) depth++;
            else if (text[k] == closeBrace)
            {
                depth--;
                if (depth == 0) return k;
            }
        }
        return -1;
    }

    private static int FindFirstUnnestedChar(string s, char ch)
    {
        int parenDepth = 0;
        int quoteDepth = 0;
        char currentQuoteChar = '\0';

        for (int k = 0; k < s.Length; k++)
        {
            if (quoteDepth > 0) // Inside a string literal
            {
                if (s[k] == '\\' && k + 1 < s.Length) // Handle escape sequence
                {
                    k++; // Skip next char
                }
                else if (s[k] == currentQuoteChar)
                {
                    quoteDepth = 0; // End of string literal
                }
            }
            else // Not inside a string literal
            {
                if (s[k] == '"' || s[k] == '\'')
                {
                    quoteDepth = 1;
                    currentQuoteChar = s[k];
                }
                else if (s[k] == '(' || s[k] == '[' || s[k] == '{') parenDepth++;
                else if (s[k] == ')' || s[k] == ']' || s[k] == '}') parenDepth--;
                else if (parenDepth == 0 && s[k] == ch) // Target char found at nesting level 0
                    return k;
            }
        }
        return -1;
    }

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
            // This Regex.Replace is for unescaping {{...}} if they appear in the *result* of an expression.
            // This is distinct from the template's {{...}} syntax handled by the main EvaluateAsync loop's recursive call.
            if (string.IsNullOrEmpty(format))
            {
                // Example: If script returns "Result: {{Value}}", this makes it "Result: {Value}"
                stringValue = Regex.Replace(sVal, @"\{\{(.*?)\}\}", m => $"{{{m.Groups[1].Value}}}");
            }
            else
            {
                stringValue = sVal;
            }
        }

        if (value == null && !string.IsNullOrEmpty(alignment) && stringValue == null)
        { // stringValue would be string.Empty if value was null
            return string.Format(culture, $"{{0,{alignment}}}", string.Empty);
        }
        // If value was null, stringValue is now string.Empty.
        // If value was string, stringValue is now the (potentially unescaped) string.
        if (stringValue == string.Empty && string.IsNullOrEmpty(alignment)) return string.Empty;


        if (stringValue != null && string.IsNullOrEmpty(alignment) && string.IsNullOrEmpty(format))
        {
            return stringValue;
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
            Logger.Debug($"[ExpressionEvaluator.ApplyFormat] FormatException for value '{valueToFormat}', format '{format}'. Error: {ex.Message}");
            if (options.ThrowOnError) throw;
            return valueToFormat?.ToString() ?? string.Empty;
        }
    }

    private async Task<object?> EvaluateScriptAsync(string expression, object? context, DollarSignOptions options)
    {
        ScriptRunner<object>? runner;
        string processedExpression = expression;
        Type globalsTypeForScript;
        object effectiveGlobals = context ?? new DollarSign.NoParametersContext();

        // This logging is crucial
        Logger.Debug($"[EvaluateScriptAsync START] Original Expr: \"{expression}\", Processed Expr: \"{processedExpression}\", Context: {context?.GetType().Name}");

        if (options.GlobalVariableTypes != null && options.GlobalVariableTypes.Any() && context is ScriptHost scriptHostContext)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(expression, new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script));
            var rewriter = new CastingDictionaryAccessRewriter(options.GlobalVariableTypes);
            var newRoot = rewriter.Visit(syntaxTree.GetRoot());
            processedExpression = newRoot.ToFullString();
            globalsTypeForScript = typeof(ScriptHost);
            effectiveGlobals = scriptHostContext;
            Logger.Debug($"[EvaluateScriptAsync] Rewritten expression for ScriptHost: '{processedExpression}'");
        }
        else if (context is DictionaryWrapper dw)
        {
            globalsTypeForScript = typeof(DictionaryWrapper);
            effectiveGlobals = dw;
            Logger.Debug($"[EvaluateScriptAsync] Using DictionaryWrapper. Expression: '{processedExpression}'");
        }
        else
        {
            globalsTypeForScript = effectiveGlobals.GetType();
            Logger.Debug($"[EvaluateScriptAsync] Using POCO/NoParams ({globalsTypeForScript.Name}). Expression: '{processedExpression}'");
        }

        string cacheKey = options.UseCache ? $"{globalsTypeForScript.FullName}_{processedExpression}" : Guid.NewGuid().ToString();

        if (options.UseCache)
        {
            lock (_cacheLock) { _scriptCache.TryGetValue(cacheKey, out runner); }
        }
        else
        {
            runner = null;
        }

        if (runner == null)
        {
            var scriptOptions = ScriptOptions.Default
                .AddReferences(GetReferences(effectiveGlobals, options.GlobalVariableTypes?.Values.Select(t => t.Assembly).ToArray()))
                .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Math",
                            "System.Globalization", "System.Dynamic", "DollarSignEngine");

            Logger.Debug($"[EvaluateScriptAsync] Compiling expression: '{processedExpression}' with globalsType: '{globalsTypeForScript.FullName}'");

            var script = CSharpScript.Create<object>(processedExpression, scriptOptions, globalsType: globalsTypeForScript);

            try
            {
                runner = script.CreateDelegate();
            }
            catch (CompilationErrorException ex)
            {
                string errorDetails = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
                Logger.Debug($"[EvaluateScriptAsync] CompilationErrorException: {errorDetails} for expression: {processedExpression}");
                throw new CompilationException($"Error compiling C# script: {processedExpression} (Original: {expression}) | Errors: {errorDetails}", errorDetails, ex);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[EvaluateScriptAsync] Exception during script delegate creation: {ex.Message} for expression: {processedExpression}");
                throw new DollarSignEngineException($"Unexpected error creating script delegate for: {processedExpression} (Original: {expression})", ex);
            }

            if (options.UseCache && runner != null)
            {
                lock (_cacheLock) { _scriptCache[cacheKey] = runner; }
            }
        }

        if (runner == null)
        {
            Logger.Debug($"[EvaluateScriptAsync] Runner is null after compilation/cache attempt for key: {cacheKey}");
            throw new DollarSignEngineException($"Failed to create or retrieve a script runner for expression: {processedExpression}");
        }

        try
        {
            var result = await runner(effectiveGlobals, CancellationToken.None);
            Logger.Debug($"[EvaluateScriptAsync END] Original Expr: \"{expression}\", Processed Expr: \"{processedExpression}\", Result: \"{result}\"");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[EvaluateScriptAsync] Runtime error executing script '{processedExpression}': {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}");
            throw new DollarSignEngineException($"Runtime error executing script: '{processedExpression}' (Original: {expression})", ex);
        }
    }

    private object? ResolveNestedProperty(object? source, string path)
    {
        if (source == null || string.IsNullOrEmpty(path)) return null;
        object? current = source;
        string[] parts = path.Split('.');

        foreach (var part in parts)
        {
            if (current == null) return null;

            if (current is DictionaryWrapper dw) { current = dw.TryGetValue(part); continue; }
            if (current is IDictionary<string, object> genericDict)
            {
                if (genericDict.TryGetValue(part, out var dictValue) ||
                    (genericDict.Keys.FirstOrDefault(k => string.Equals(k, part, StringComparison.OrdinalIgnoreCase)) is string key && genericDict.TryGetValue(key, out dictValue)))
                { current = dictValue; }
                else { return null; }
                continue;
            }
            if (current is IDictionary<string, object?> genericDictNullable)
            {
                if (genericDictNullable.TryGetValue(part, out var dictValue) ||
                    (genericDictNullable.Keys.FirstOrDefault(k => string.Equals(k, part, StringComparison.OrdinalIgnoreCase)) is string key && genericDictNullable.TryGetValue(key, out dictValue)))
                { current = dictValue; }
                else { return null; }
                continue;
            }
            if (current is IDictionary nonGenericDict)
            {
                object? tempKey = part;
                if (!nonGenericDict.Contains(tempKey))
                {
                    tempKey = nonGenericDict.Keys.Cast<object>().FirstOrDefault(k => k is string ks && string.Equals(ks, part, StringComparison.OrdinalIgnoreCase));
                }
                if (tempKey != null && nonGenericDict.Contains(tempKey)) { current = nonGenericDict[tempKey]; } else { return null; }
                continue;
            }

            var accessor = GetTypeAccessor(current.GetType());
            if (!accessor.TryGetPropertyValue(current, part, out current)) return null;
        }
        return current;
    }

    private IEnumerable<MetadataReference> GetReferences(object? contextObject, IEnumerable<Assembly>? additionalAssembliesFromTypes = null)
    {
        var assemblies = new HashSet<Assembly>();
        void AddValidAssembly(Assembly? asm)
        {
            if (asm != null && !asm.IsDynamic && !string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
            {
                assemblies.Add(asm);
            }
        }

        AddValidAssembly(typeof(object).Assembly);
        AddValidAssembly(typeof(System.Linq.Enumerable).Assembly);
        AddValidAssembly(typeof(System.Collections.Generic.List<>).Assembly);
        AddValidAssembly(typeof(System.Text.RegularExpressions.Regex).Assembly);
        AddValidAssembly(typeof(Uri).Assembly);
        AddValidAssembly(typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly);
        AddValidAssembly(typeof(System.Dynamic.IDynamicMetaObjectProvider).Assembly);

        AddValidAssembly(typeof(ExpressionEvaluator).Assembly);
        AddValidAssembly(typeof(DollarSign).Assembly);

        if (contextObject != null) AddValidAssembly(contextObject.GetType().Assembly);

        if (additionalAssembliesFromTypes != null)
        {
            foreach (var asm in additionalAssembliesFromTypes) AddValidAssembly(asm);
        }

        if (contextObject != null)
        {
            Type contextType = contextObject.GetType();
            if (contextType.IsGenericType)
            {
                foreach (Type argType in contextType.GetGenericArguments())
                {
                    AddValidAssembly(argType.Assembly);
                }
            }
        }

        return assemblies
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Distinct() // Using Distinct after Select to avoid issues with MetadataReference equality
            .ToList();
    }

    public void ClearCache()
    {
        lock (_cacheLock) { _scriptCache.Clear(); Logger.Debug("[ExpressionEvaluator.ClearCache] Script cache cleared."); }
        lock (_typeAccessorLock) { _typeAccessors.Clear(); Logger.Debug("[ExpressionEvaluator.ClearCache] Type accessor cache cleared."); }
    }

    #region TypeAccessor Inner Class
    private class TypeAccessor
    {
        private readonly Dictionary<string, Func<object, object?>> _getters = new(StringComparer.OrdinalIgnoreCase);
        public void AddGetter(string name, Func<object, object?> getter) => _getters[name] = getter;
        public bool TryGetPropertyValue(object instance, string propName, out object? value)
        {
            if (_getters.TryGetValue(propName, out var getter))
            {
                try { value = getter(instance); return true; }
                catch (Exception ex) { Logger.Debug($"[TypeAccessor.TryGetPropertyValue] Property: {propName}, Instance: {instance.GetType().Name}, Error: {ex.Message}"); value = null; return false; }
            }
            value = null; return false;
        }
    }
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
    private TypeAccessor CreateTypeAccessor(Type type)
    {
        var accessor = new TypeAccessor();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        foreach (var p in props) { if (p.CanRead && p.GetIndexParameters().Length == 0) { accessor.AddGetter(p.Name, inst => p.GetValue(inst)); } }
        return accessor;
    }
    #endregion
}
