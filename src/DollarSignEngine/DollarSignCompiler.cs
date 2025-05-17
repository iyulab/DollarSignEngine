using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;

namespace DollarSignEngine;

/// <summary>
/// Compiles string interpolation expressions using Roslyn
/// </summary>
internal class DollarSignCompiler
{
    private readonly Dictionary<string, CompiledExpression> _cache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Compiles a string interpolation expression
    /// </summary>
    internal async Task<CompiledExpression> CompileExpressionAsync(
        string expression,
        DollarSignOptions options)
    {
        // Try to get from cache if enabled
        string cacheKey = expression;
        if (options.UseCache && TryGetFromCache(cacheKey, out var cached))
            return cached;

        try
        {
            // Generate source code
            string sourceCode = GenerateSourceCode(expression);

            // For debugging
            Console.WriteLine($"Generated source code for expression '{expression}':");
            Console.WriteLine(sourceCode);

            // Compile the source code
            Assembly assembly;
            try
            {
                assembly = await CompileSourceCodeAsync(sourceCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compilation error: {ex.Message}");
                throw new CompilationException($"Failed to compile expression: {ex.Message}", sourceCode);
            }

            // Create compiled expression wrapper
            var compiledExpression = new CompiledExpression(assembly);

            // Add to cache if option enabled
            if (options.UseCache)
                AddToCache(cacheKey, compiledExpression);

            return compiledExpression;
        }
        catch (CompilationException)
        {
            // Already a CompilationException, just rethrow
            throw;
        }
        catch (DollarSignEngineException)
        {
            // Already a DollarSignEngineException, just rethrow
            throw;
        }
        catch (Exception ex)
        {
            // Wrap all other exceptions
            throw new CompilationException($"Failed to compile expression: {ex.Message}", expression);
        }
    }

    /// <summary>
    /// Generate source code for evaluating an expression
    /// </summary>
    private string GenerateSourceCode(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return GenerateEmptyStringCode();

        // Parse the expression into segments using a carefully designed algorithm
        var (segments, variables) = ParseExpression(expression);

        // Generate the code
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Globalization;");
        code.AppendLine("using System.Text;");
        code.AppendLine();
        code.AppendLine("namespace DynamicInterpolation");
        code.AppendLine("{");
        code.AppendLine("    public static class Evaluator");
        code.AppendLine("    {");
        code.AppendLine("        public delegate object ResolverDelegate(string name);");
        code.AppendLine();
        code.AppendLine("        public static string Evaluate(ResolverDelegate resolver)");
        code.AppendLine("        {");
        code.AppendLine("            try");
        code.AppendLine("            {");

        // Declare variables (only once per unique variable name)
        int varIndex = 0;
        var varMap = new Dictionary<string, string>();
        foreach (var variable in variables)
        {
            string safeVarName = $"var{varIndex++}";
            varMap[variable] = safeVarName;
            code.AppendLine($"                object _{safeVarName} = resolver(\"{variable}\");");
        }

        // StringBuilder를 사용하여 결과 구성
        code.AppendLine("                var result = new StringBuilder();");

        foreach (var segment in segments)
        {
            if (segment.IsText)
            {
                if (!string.IsNullOrEmpty(segment.Content))
                {
                    // 텍스트 세그먼트에서 특수 문자를 C# 문자열 리터럴에서 사용 가능한 이스케이프 시퀀스로 변환
                    string escapedText = segment.Content
                        .Replace("\\", "\\\\")    // 백슬래시 먼저 이스케이프
                        .Replace("\"", "\\\"")    // 따옴표 이스케이프
                        .Replace("\n", "\\n")     // 줄바꿈을 \n으로 이스케이프
                        .Replace("\r", "\\r")     // 캐리지 리턴을 \r로 이스케이프
                        .Replace("\t", "\\t");    // 탭을 \t로 이스케이프

                    code.AppendLine($"                result.Append(\"{escapedText}\");");
                }
            }
            else if (segment.IsVariable)
            {
                string safeVarName = varMap[segment.Content];

                if (string.IsNullOrEmpty(segment.AlignmentSpecifier) && string.IsNullOrEmpty(segment.FormatSpecifier))
                {
                    // 형식 지정자가 없는 경우 - ToString() 사용
                    code.AppendLine($"                result.Append(_{safeVarName} == null ? \"\" : _{safeVarName}.ToString());");
                }
                else
                {
                    // 형식 지정자나 정렬 지정자가 있는 경우
                    string formatString = "{0";

                    // 정렬 지정자 추가
                    if (!string.IsNullOrEmpty(segment.AlignmentSpecifier))
                    {
                        formatString += segment.AlignmentSpecifier;
                    }

                    // 형식 지정자 추가
                    if (!string.IsNullOrEmpty(segment.FormatSpecifier))
                    {
                        formatString += segment.FormatSpecifier;
                    }

                    formatString += "}";

                    // string.Format 사용
                    code.AppendLine($"                result.Append(string.Format(CultureInfo.CurrentCulture, \"{formatString}\", _{safeVarName} ?? \"\"));");
                }
            }
        }

        code.AppendLine("                return result.ToString();");
        code.AppendLine("            }");
        code.AppendLine("            catch (Exception ex)");
        code.AppendLine("            {");
        code.AppendLine("                Console.WriteLine($\"Error in string interpolation: {ex.Message}\");");
        code.AppendLine("                Console.WriteLine(ex.StackTrace);");
        code.AppendLine("                return string.Empty;");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    private (List<ExpressionSegment> Segments, HashSet<string> Variables) ParseExpression(string expression)
    {
        var segments = new List<ExpressionSegment>();
        var variables = new HashSet<string>();

        int i = 0;
        StringBuilder textBuffer = new StringBuilder();

        // Helper to add a text segment
        void AddTextSegment()
        {
            if (textBuffer.Length > 0)
            {
                segments.Add(new ExpressionSegment
                {
                    IsText = true,
                    IsVariable = false,
                    Content = textBuffer.ToString()
                });
                textBuffer.Clear();
            }
        }

        while (i < expression.Length)
        {
            // Handle special cases first

            // 1. Escaped opening brace: {{
            if (i + 1 < expression.Length && expression[i] == '{' && expression[i + 1] == '{')
            {
                // Add the single { to the output directly
                textBuffer.Append('{');
                i += 2; // Skip both braces
                continue;
            }

            // 2. Escaped closing brace: }}
            if (i + 1 < expression.Length && expression[i] == '}' && expression[i + 1] == '}')
            {
                // Add the single } to the output directly
                textBuffer.Append('}');
                i += 2; // Skip both braces
                continue;
            }

            // 3. Variable expression: {variable}
            if (expression[i] == '{')
            {
                // Add any accumulated text
                AddTextSegment();

                int start = i + 1; // Skip the opening brace
                i = start; // Start searching from here

                // Find the matching closing brace, handling nested expressions correctly
                int braceCount = 1;
                bool inStringLiteral = false;
                bool foundClosing = false;

                while (i < expression.Length) // Loop to find matching '}'
                {
                    char c = expression[i];

                    // Handle string literals (to ignore braces inside string literals)
                    if (c == '"' && (i == 0 || expression[i - 1] != '\\'))
                    {
                        inStringLiteral = !inStringLiteral;
                        i++;
                        continue;
                    }

                    if (!inStringLiteral)
                    {
                        if (c == '{')
                        {
                            braceCount++;
                        }
                        else if (c == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                // Found the matching closing brace
                                foundClosing = true;
                                break;
                            }
                        }
                    }
                    i++;
                }

                if (foundClosing)
                {
                    // Extract the variable content (excluding the braces)
                    string varContent = expression.Substring(start, i - start);

                    // Parse the variable name, alignment, and format specifier
                    string varName = varContent;
                    string alignmentSpecifier = string.Empty;
                    string formatSpecifier = string.Empty;

                    // Simple case - no format or alignment specifiers
                    if (!varContent.Contains(',') && !varContent.Contains(':'))
                    {
                        varName = varContent.Trim();
                    }
                    else
                    {
                        // Parse for alignment and format specifiers
                        bool inVarString = false;
                        int commaPos = -1;
                        int colonPos = -1;

                        // Find positions of comma and colon outside of string literals
                        for (int j = 0; j < varContent.Length; j++)
                        {
                            char c = varContent[j];

                            // Track string literals
                            if (c == '"' && (j == 0 || varContent[j - 1] != '\\'))
                            {
                                inVarString = !inVarString;
                                continue;
                            }

                            if (!inVarString)
                            {
                                // First comma outside a string literal is alignment specifier
                                if (c == ',' && commaPos == -1)
                                {
                                    commaPos = j;
                                }
                                // First colon outside a string literal is format specifier
                                else if (c == ':' && colonPos == -1)
                                {
                                    colonPos = j;
                                }
                            }
                        }

                        // Extract the parts based on what we found
                        if (colonPos >= 0)
                        {
                            if (commaPos >= 0 && commaPos < colonPos)
                            {
                                // We have both alignment and format: {name,align:format}
                                varName = varContent.Substring(0, commaPos).Trim();
                                alignmentSpecifier = varContent.Substring(commaPos, colonPos - commaPos);
                                formatSpecifier = varContent.Substring(colonPos);
                            }
                            else
                            {
                                // We have only format: {name:format}
                                varName = varContent.Substring(0, colonPos).Trim();
                                formatSpecifier = varContent.Substring(colonPos);
                            }
                        }
                        else if (commaPos >= 0)
                        {
                            // We have only alignment: {name,align}
                            varName = varContent.Substring(0, commaPos).Trim();
                            alignmentSpecifier = varContent.Substring(commaPos);
                        }
                    }

                    // Add the variable to our list
                    variables.Add(varName);

                    // Add a variable segment
                    segments.Add(new ExpressionSegment
                    {
                        IsText = false,
                        IsVariable = true,
                        Content = varName,
                        AlignmentSpecifier = alignmentSpecifier,
                        FormatSpecifier = formatSpecifier
                    });

                    i++; // Skip the closing brace for the outer loop
                }
                else
                {
                    // No matching closing brace, treat the opening brace as text
                    textBuffer.Append('{');
                    i = start; // Reset position to just after the opening brace
                }
            }
            else
            {
                // Regular text character - 특수 문자도 그대로 보존
                textBuffer.Append(expression[i]);
                i++;
            }
        }

        // Add any remaining text
        AddTextSegment();

        return (segments, variables);
    }

    // Helper method to parse variable parts (name, alignment, format)
    private string ParseVariableParts(string varContent, out string alignmentSpecifier, out string formatSpecifier)
    {
        alignmentSpecifier = string.Empty;
        formatSpecifier = string.Empty;

        // Simple case - no specifiers
        if (!varContent.Contains(',') && !varContent.Contains(':'))
        {
            return varContent.Trim();
        }

        // Handle more complex cases with string analysis
        string varName = varContent;

        // Track string literals to avoid parsing specifiers inside them
        bool inStringLiteral = false;
        int commaPos = -1;
        int colonPos = -1;

        // Find positions of comma and colon outside of string literals
        for (int i = 0; i < varContent.Length; i++)
        {
            char c = varContent[i];

            // Track string literals
            if (c == '"' && (i == 0 || varContent[i - 1] != '\\'))
            {
                inStringLiteral = !inStringLiteral;
                continue;
            }

            if (!inStringLiteral)
            {
                // First comma outside a string literal is alignment specifier
                if (c == ',' && commaPos == -1)
                {
                    commaPos = i;
                }
                // First colon outside a string literal is format specifier
                else if (c == ':' && colonPos == -1)
                {
                    colonPos = i;
                }
            }
        }

        // Extract the parts based on what we found
        if (colonPos >= 0)
        {
            if (commaPos >= 0 && commaPos < colonPos)
            {
                // We have both alignment and format: {name,align:format}
                varName = varContent.Substring(0, commaPos).Trim();
                alignmentSpecifier = varContent.Substring(commaPos, colonPos - commaPos);
                formatSpecifier = varContent.Substring(colonPos);
            }
            else
            {
                // We have only format: {name:format}
                varName = varContent.Substring(0, colonPos).Trim();
                formatSpecifier = varContent.Substring(colonPos);
            }
        }
        else if (commaPos >= 0)
        {
            // We have only alignment: {name,align}
            varName = varContent.Substring(0, commaPos).Trim();
            alignmentSpecifier = varContent.Substring(commaPos);
        }

        return varName;
    }

    /// <summary>
    /// Represents a segment of an interpolated string expression
    /// </summary>
    private class ExpressionSegment
    {
        public bool IsText { get; set; }
        public bool IsVariable { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AlignmentSpecifier { get; set; } = string.Empty;
        public string FormatSpecifier { get; set; } = string.Empty;
    }

    /// <summary>
    /// Generates code for an empty string result
    /// </summary>
    private string GenerateEmptyStringCode()
    {
        return @"
using System;

namespace DynamicInterpolation
{
    public static class Evaluator
    {
        public delegate object ResolverDelegate(string name);

        public static string Evaluate(ResolverDelegate resolver)
        {
            return string.Empty;
        }
    }
}";
    }

    /// <summary>
    /// Compiles source code into an assembly
    /// </summary>
    private async Task<Assembly> CompileSourceCodeAsync(string sourceCode)
    {
        return await Task.Run(() => {
            // Parse the source code
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // Set compilation options
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release);

            // Get required references
            var references = GetRequiredReferences();

            // Create unique assembly name
            string assemblyName = $"DynamicAssembly_{Guid.NewGuid():N}";

            // Create the compilation
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                options);

            // Emit to memory stream
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            // Handle compilation errors
            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine,
                    result.Diagnostics
                        .Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"{d.Id}: {d.GetMessage()}"));

                throw new Exception($"Compilation errors:{Environment.NewLine}{errors}");
            }

            // Load the assembly
            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        });
    }

    /// <summary>
    /// Gets required references for compilation
    /// </summary>
    private List<MetadataReference> GetRequiredReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core .NET types
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FormattableString).Assembly.Location),
            
            // System assemblies
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            
            // Collections and LINQ
            MetadataReference.CreateFromFile(typeof(System.Collections.IEnumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            
            // Text and Globalization
            MetadataReference.CreateFromFile(typeof(StringBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Globalization.CultureInfo).Assembly.Location)
        };

        // Try to add Microsoft.CSharp for dynamic features
        try
        {
            var csharpAssembly = Assembly.Load("Microsoft.CSharp");
            if (csharpAssembly != null)
            {
                references.Add(MetadataReference.CreateFromFile(csharpAssembly.Location));
            }
        }
        catch
        {
            // Ignore if not available
        }

        return references;
    }

    /// <summary>
    /// Tries to get a compiled expression from cache
    /// </summary>
    private bool TryGetFromCache(string key, out CompiledExpression expression)
    {
        lock (_cacheLock)
        {
            return _cache.TryGetValue(key, out expression!);
        }
    }

    /// <summary>
    /// Adds a compiled expression to the cache
    /// </summary>
    private void AddToCache(string key, CompiledExpression expression)
    {
        lock (_cacheLock)
        {
            _cache[key] = expression;
        }
    }

    /// <summary>
    /// Clears the compilation cache
    /// </summary>
    internal void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }
}