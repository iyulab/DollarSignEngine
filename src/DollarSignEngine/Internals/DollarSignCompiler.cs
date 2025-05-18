using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace DollarSignEngine.Internals;

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
            string sourceCode = GenerateSourceCode(expression);

            // Compile the source code
            Assembly assembly;
            try
            {
                assembly = await CompileSourceCodeAsync(sourceCode);
            }
            catch (Exception ex)
            {
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
            throw;
        }
        catch (DollarSignEngineException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompilationException($"Failed to compile expression: {ex.Message}", expression);
        }
    }

    /// <summary>
    /// Generates C# source code for the provided interpolation expression
    /// </summary>
    private string GenerateSourceCode(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return CodeGenerators.GenerateEmptyStringCode();

        // For strings with newlines or tabs, use specialized parser
        if (expression.Contains('\n') || expression.Contains('\r') || expression.Contains('\t'))
        {
            return CodeGenerators.GenerateCodeForStringWithSpecialChars(expression);
        }

        // Regular approach for strings without special characters
        string wrappedExpression = $"$\"{expression}\"";

        // Use Roslyn to parse the interpolated string expression
        var tree = CSharpSyntaxTree.ParseText(wrappedExpression);
        var root = tree.GetRoot();

        // Find all interpolated string expressions in the parsed code
        var interpolatedString = root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>().FirstOrDefault();
        if (interpolatedString == null)
        {
            // If not an interpolated string, return simple string code
            return CodeGenerators.GenerateSimpleStringCode(expression);
        }

        // Extract all identifiers from the interpolation expressions
        var variablePaths = new HashSet<string>();
        var formatSpecifiers = new Dictionary<string, string>(); // Map of variable path to format specifier

        foreach (var interpolation in interpolatedString.DescendantNodes().OfType<InterpolationSyntax>())
        {
            // Extract all identifiers from this interpolation
            InterpolationParser.ExtractVariables(interpolation.Expression, variablePaths);

            // Also extract format specifiers if present
            if (interpolation.FormatClause != null)
            {
                string format = interpolation.FormatClause.ToString().TrimStart(':');

                // Associate format with variable if it's a simple variable
                if (interpolation.Expression is IdentifierNameSyntax identifier)
                {
                    formatSpecifiers[identifier.Identifier.Text] = format;
                }
                else if (interpolation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    string path = InterpolationParser.ExtractPropertyPath(memberAccess);
                    if (!string.IsNullOrEmpty(path))
                    {
                        formatSpecifiers[path] = format;
                    }
                }
            }
        }

        return CodeGenerators.GenerateEvaluatorCode(interpolatedString, variablePaths);
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
            var references = CompilationReferences.GetRequiredReferences();

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