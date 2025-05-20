using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;
using System.Text;
using System.Text.RegularExpressions;

namespace DollarSignEngine.Internals;

/// <summary>
/// Core expression evaluation engine. 
/// </summary>
internal class ExpressionEvaluator
{
    // Script compilation and execution cache
    private readonly LruCache<string, ScriptRunner<object>> _scriptCache =
        new(1000, StringComparer.OrdinalIgnoreCase);

    // Regex patterns for parsing expressions
    private static readonly Regex SimpleIdentifierRegex =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex SimplePropertyPathRegex =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);

    /// <summary>
    /// Evaluates a template string, replacing expressions with their computed values.
    /// </summary>
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
                int exprEnd = FindMatchingBrace(work, exprStart, '{', '}');

                if (exprEnd < 0)
                {
                    sb.Append(work.Substring(i, offset));
                    i += offset;
                    continue;
                }

                // Extract and parse expression content
                string content = work.Substring(exprStart, exprEnd - exprStart);
                string expr = content;
                string? format = null;
                string? align = null;

                // Extract formatting and alignment info
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
                    // Try to resolve using custom resolver first
                    if (options.VariableResolver != null)
                        value = options.VariableResolver(finalExpr);

                    if (value == null)
                    {
                        if (string.IsNullOrWhiteSpace(finalExpr))
                        {
                            value = string.Empty;
                        }
                        // For dot-notation properties, use faster property resolver
                        else if (SimplePropertyPathRegex.IsMatch(finalExpr) &&
                                context is not ScriptHost &&
                                context is not DictionaryWrapper &&
                                finalExpr.Contains('.'))
                        {
                            value = PropertyResolver.ResolveNestedProperty(context, finalExpr);
                        }
                        else
                        {
                            // Full script evaluation for complex expressions
                            value = await EvaluateScriptAsync(finalExpr, context, options);
                        }
                    }
                }
                catch (Exception ex)
                {
                    bool handled = false;

                    if (options.ErrorHandler != null)
                    {
                        sb.Append(options.ErrorHandler(finalExpr, ex));
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
                              !finalExpr.Contains('.') &&
                              (ex is KeyNotFoundException ||
                               ex.InnerException is KeyNotFoundException ||
                               ex is RuntimeBinderException ||
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
                sb.Append('{');
                i++;
                continue;
            }

            // Default literal char
            sb.Append(currentChar);
            i++;
        }

        // 3. Postprocess: restore escape blocks
        string result = TemplateEscaper.UnescapeBlocks(sb.ToString());
        return result;
    }

    /// <summary>
    /// Finds a matching closing brace for a given opening brace position.
    /// </summary>
    private int FindMatchingBrace(string text, int startIndex, char openBrace, char closeBrace)
    {
        int depth = 1;
        for (int k = startIndex; k < text.Length; k++)
        {
            // Skip string literals
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
        return -1; // No matching brace found
    }

    /// <summary>
    /// Finds the first occurrence of a character that is not nested within parentheses or quotes.
    /// </summary>
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
        return -1; // Not found
    }

    /// <summary>
    /// Applies formatting and alignment to a value.
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
            // Unescape {{...}} if they appear in the result of an expression
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
            // Apply alignment to empty string
            return string.Format(culture, $"{{0,{alignment}}}", string.Empty);
        }

        // Use existing string value
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
            // Use IFormattable if available for best formatting support
            if (valueToFormat is IFormattable formattableValue)
            {
                string formatted = formattableValue.ToString(format, culture);
                if (!string.IsNullOrEmpty(alignment))
                {
                    return string.Format(culture, $"{{0,{alignment}}}", formatted);
                }
                return formatted;
            }

            // Fall back to standard string.Format
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
    /// Evaluates a C# script expression against a context object.
    /// </summary>
    private async Task<object?> EvaluateScriptAsync(string expression, object? context, DollarSignOptions options)
    {
        ScriptRunner<object>? runner;
        string processedExpression = expression;
        Type globalsTypeForScript;
        object effectiveGlobals = context ?? new DollarSign.NoParametersContext();

        Logger.Debug($"[EvaluateScriptAsync START] Original Expr: \"{expression}\", Context: {context?.GetType().Name}");

        // Process scripts with global variable types for better type safety
        if (options.GlobalVariableTypes != null && options.GlobalVariableTypes.Any() && context is ScriptHost scriptHostContext)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(expression, new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script));
            var rewriter = new CastingDictionaryAccessRewriter(options.GlobalVariableTypes);
            var newRoot = rewriter.Visit(syntaxTree.GetRoot());
            processedExpression = newRoot.ToFullString();
            globalsTypeForScript = typeof(ScriptHost);
            effectiveGlobals = scriptHostContext;
            Logger.Debug($"[EvaluateScriptAsync] Rewritten expression: '{processedExpression}'");
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

        // Generate cache key
        string cacheKey = options.UseCache
            ? $"{globalsTypeForScript.FullName}_{processedExpression}"
            : Guid.NewGuid().ToString();

        // Check cache first if enabled
        if (options.UseCache)
        {
            if (_scriptCache.TryGetValue(cacheKey, out runner))
            {
                Logger.Debug($"[EvaluateScriptAsync] Cache hit for expression: '{processedExpression}'");
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

        // Compile script if not in cache
        if (runner == null)
        {
            var scriptOptions = ScriptOptions.Default
                .AddReferences(AssemblyReferenceHelper.GetReferences(effectiveGlobals, options.GlobalVariableTypes?.Values.Select(t => t.Assembly).ToArray()))
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

                // Cache the compiled script if caching is enabled
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
            throw new DollarSignEngineException($"Failed to create or retrieve a script runner for expression: {processedExpression}");
        }

        try
        {
            // Execute the script
            var result = await runner(effectiveGlobals, CancellationToken.None);
            Logger.Debug($"[EvaluateScriptAsync END] Result: \"{result}\"");
            return result;
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
        _scriptCache.Clear();
        TypeAccessorFactory.ClearCache();
        TypeNameHelper.ClearCaches();
        Logger.Info("[ExpressionEvaluator.ClearCache] All caches cleared.");
    }
}