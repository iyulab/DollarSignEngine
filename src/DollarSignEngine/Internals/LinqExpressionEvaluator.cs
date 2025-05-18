using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DollarSignEngine.Internals
{
    /// <summary>
    /// Evaluates expressions that contain LINQ operations using Roslyn scripting
    /// </summary>
    internal class LinqExpressionEvaluator
    {
        // Main pattern to extract expressions in curly braces
        private static readonly Regex ExpressionPattern = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

        // Pattern to handle format specifiers
        private static readonly Regex FormatSpecifierPattern = new(@"^(.*?)(?::([A-Za-z0-9]+))?$", RegexOptions.Compiled);

        /// <summary>
        /// Evaluates a string interpolation expression using Roslyn scripting for LINQ support
        /// </summary>
        internal async Task<string> EvaluateAsync(
            string template,
            object? parameters,
            DollarSignOptions options)
        {
            Logger.Debug($"[LinqExpressionEvaluator] Starting template evaluation: '{template}'");

            if (string.IsNullOrEmpty(template))
                return string.Empty;

            // Use regex to find all expressions in curly braces
            var matches = ExpressionPattern.Matches(template);
            if (matches.Count == 0)
            {
                Logger.Debug("[LinqExpressionEvaluator] No expressions found in template");
                return template;
            }

            var result = new StringBuilder(template);

            // Process expressions in reverse order to avoid index shifting
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                string expression = match.Groups[1].Value;

                Logger.Debug($"[LinqExpressionEvaluator] Processing expression: '{expression}'");

                // Check for format specifier
                var formatMatch = FormatSpecifierPattern.Match(expression);
                string baseExpression = expression;
                string? formatSpecifier = null;

                if (formatMatch.Success)
                {
                    baseExpression = formatMatch.Groups[1].Value;
                    formatSpecifier = formatMatch.Groups[2].Success ? formatMatch.Groups[2].Value : null;

                    if (!string.IsNullOrEmpty(formatSpecifier))
                    {
                        Logger.Debug($"[LinqExpressionEvaluator] Extracted base expression: '{baseExpression}'");
                        Logger.Debug($"[LinqExpressionEvaluator] Extracted format specifier: '{formatSpecifier}'");
                    }
                }

                // Evaluate the base expression
                string evaluatedValue = string.Empty;
                try
                {
                    // Evaluate the base expression without format specifier
                    var scriptResult = await EvaluateScriptAsync(baseExpression, parameters, options);

                    // Apply format specifier if present
                    if (!string.IsNullOrEmpty(formatSpecifier) && scriptResult != null)
                    {
                        try
                        {
                            // Use direct formatting with InvariantCulture
                            evaluatedValue = string.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                $"{{0:{formatSpecifier}}}",
                                scriptResult);

                            Logger.Debug($"[LinqExpressionEvaluator] Formatted with '{formatSpecifier}': '{evaluatedValue}'");
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"[LinqExpressionEvaluator] Format error: {ex.Message}");
                            evaluatedValue = scriptResult.ToString() ?? string.Empty;
                        }
                    }
                    else
                    {
                        evaluatedValue = scriptResult?.ToString() ?? string.Empty;
                    }

                    Logger.Debug($"[LinqExpressionEvaluator] Evaluated to: '{evaluatedValue}'");
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[LinqExpressionEvaluator] Evaluation error: {ex.Message}");

                    if (options.ThrowOnError)
                        throw;

                    evaluatedValue = options.ErrorHandler?.Invoke(expression, ex) ?? string.Empty;
                }

                // Replace the expression with its evaluated value
                result.Remove(match.Index, match.Length);
                result.Insert(match.Index, evaluatedValue);
            }

            Logger.Debug($"[LinqExpressionEvaluator] Final result: '{result}'");
            return result.ToString();
        }

        /// <summary>
        /// Evaluates a string interpolation expression using a dictionary as parameters
        /// </summary>
        internal async Task<string> EvaluateAsync(
            string template,
            IDictionary<string, object?> parameters,
            DollarSignOptions options)
        {
            // Convert dictionary to DictionaryWrapper for script access
            var wrapper = new DictionaryWrapper(parameters);
            return await EvaluateAsync(template, wrapper, options);
        }

        /// <summary>
        /// Evaluates a C# script expression and returns the resulting object
        /// </summary>
        private async Task<object?> EvaluateScriptAsync(
            string expression,
            object? globals,
            DollarSignOptions options)
        {
            if (string.IsNullOrEmpty(expression))
                return string.Empty;

            Logger.Debug($"[LinqExpressionEvaluator] Evaluating script: '{expression}'");

            if (globals != null && globals.GetType().Name.StartsWith("<>f__AnonymousType"))
            {
                throw new DollarSignEngineException(
                    "Anonymous types are not supported in LINQ expressions. Use public classes instead.");
            }

            try
            {
                // Collect references needed for script compilation
                var references = new List<MetadataReference>();
                Type? globalsType = globals?.GetType();

                // Add core references
                AddCoreReferences(references);

                // Add reference for globals type if available
                if (globalsType != null && !globalsType.Assembly.IsDynamic &&
                    !string.IsNullOrEmpty(globalsType.Assembly.Location))
                {
                    var location = globalsType.Assembly.Location;
                    if (File.Exists(location) && !references.Any(r => r.Display == location))
                    {
                        references.Add(MetadataReference.CreateFromFile(location));
                        Logger.Debug($"[LinqExpressionEvaluator] Added globals assembly reference: {location}");
                    }
                }

                // Create script options with necessary imports
                var scriptOptions = ScriptOptions.Default
                    .AddReferences(references)
                    .AddImports(
                        "System",
                        "System.Linq",
                        "System.Collections.Generic",
                        "System.Text",
                        "System.Globalization"
                    );

                // Create and run the script
                var script = CSharpScript.Create<object>(
                    expression,
                    scriptOptions,
                    globalsType: globalsType);

                var result = await script.RunAsync(globals);

                if (result.Exception != null)
                {
                    Logger.Debug($"[LinqExpressionEvaluator] Script exception: {result.Exception.Message}");

                    if (options.ThrowOnError)
                        throw new DollarSignEngineException($"Error in expression '{expression}'", result.Exception);

                    return options.ErrorHandler?.Invoke(expression, result.Exception);
                }

                Logger.Debug($"[LinqExpressionEvaluator] Raw script result: {result.ReturnValue}");
                return result.ReturnValue;
            }
            catch (CompilationErrorException ex)
            {
                Logger.Debug("[LinqExpressionEvaluator] Compilation error:");
                foreach (var diagnostic in ex.Diagnostics)
                {
                    Logger.Debug($"  {diagnostic.GetMessage()}");
                }

                if (options.ThrowOnError)
                    throw new CompilationException($"Compilation error in expression '{expression}'",
                        string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.GetMessage())));

                return options.ErrorHandler?.Invoke(expression, ex);
            }
            catch (Exception ex) when (!(ex is DollarSignEngineException))
            {
                Logger.Debug($"[LinqExpressionEvaluator] General error: {ex.GetType().Name}: {ex.Message}");

                if (options.ThrowOnError)
                    throw new DollarSignEngineException($"Error evaluating expression '{expression}'", ex);

                return options.ErrorHandler?.Invoke(expression, ex);
            }
        }

        /// <summary>
        /// Adds core references required for script compilation
        /// </summary>
        private void AddCoreReferences(List<MetadataReference> references)
        {
            // Add core .NET runtime assemblies
            TryAddReferenceByType(references, typeof(object));        // System.Private.CoreLib
            TryAddReferenceByType(references, typeof(Console));       // System.Console
            TryAddReferenceByType(references, typeof(Uri));           // System.Private.Uri
            TryAddReferenceByType(references, typeof(Enumerable));    // System.Linq
            TryAddReferenceByType(references, typeof(List<>));        // System.Collections.Generic
            TryAddReferenceByType(references, typeof(File));          // System.IO.FileSystem
            TryAddReferenceByType(references, typeof(System.Globalization.CultureInfo)); // Globalization

            // Try to add additional assemblies that might be needed
            TryAddAssemblyByName("System.Runtime.dll");
            TryAddAssemblyByName("System.Collections.dll");
            TryAddAssemblyByName("System.Globalization.dll");

            // Try to access from TRUSTED_PLATFORM_ASSEMBLIES
            TryAddTrustedPlatformAssemblies(references);
        }

        /// <summary>
        /// Attempts to add a reference based on a type
        /// </summary>
        private void TryAddReferenceByType(List<MetadataReference> references, Type type)
        {
            try
            {
                if (!type.Assembly.IsDynamic && !string.IsNullOrEmpty(type.Assembly.Location))
                {
                    string location = type.Assembly.Location;
                    if (File.Exists(location) && !references.Any(r => r.Display == location))
                    {
                        references.Add(MetadataReference.CreateFromFile(location));
                        Logger.Debug($"[LinqExpressionEvaluator] Added reference for type {type.FullName}: {location}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore errors trying to add references
                Logger.Debug($"[LinqExpressionEvaluator] Error adding reference for type {type.FullName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to add a reference by assembly name
        /// </summary>
        private void TryAddAssemblyByName(string assemblyName)
        {
            try
            {
                var assembly = Assembly.Load(assemblyName.Replace(".dll", ""));
                if (assembly != null && !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                {
                    Logger.Debug($"[LinqExpressionEvaluator] Successfully loaded assembly: {assemblyName}");
                }
            }
            catch (Exception ex)
            {
                // Ignore errors trying to load assemblies
                Logger.Debug($"[LinqExpressionEvaluator] Error loading assembly {assemblyName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to add references from TRUSTED_PLATFORM_ASSEMBLIES AppContext data
        /// </summary>
        private void TryAddTrustedPlatformAssemblies(List<MetadataReference> references)
        {
            try
            {
                var trustedAssembliesPathsRaw = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
                if (!string.IsNullOrEmpty(trustedAssembliesPathsRaw))
                {
                    var trustedAssembliesPaths = trustedAssembliesPathsRaw.Split(Path.PathSeparator);
                    int initialCount = references.Count;

                    foreach (var path in trustedAssembliesPaths)
                    {
                        if (!string.IsNullOrEmpty(path) && File.Exists(path) &&
                            !references.Any(r => r.Display == path) &&
                            (path.Contains("System.Linq") ||
                             path.Contains("System.Core") ||
                             path.Contains("System.Globalization") ||
                             path.Contains("System.Collections")))
                        {
                            try
                            {
                                references.Add(MetadataReference.CreateFromFile(path));
                            }
                            catch (Exception)
                            {
                                // Ignore errors
                            }
                        }
                    }

                    Logger.Debug($"[LinqExpressionEvaluator] Added {references.Count - initialCount} references from trusted platform assemblies");
                }
            }
            catch (Exception ex)
            {
                // Ignore errors in adding trusted platform assemblies
                Logger.Debug($"[LinqExpressionEvaluator] Error adding trusted platform assemblies: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Wrapper for dictionary objects to make them accessible in scripts
    /// </summary>
    internal class DictionaryWrapper
    {
        private readonly IDictionary<string, object?> _dictionary;

        public DictionaryWrapper(IDictionary<string, object?> dictionary)
        {
            _dictionary = dictionary;
        }

        public object? this[string key]
        {
            get => _dictionary.TryGetValue(key, out var value) ? value : null;
        }

        // Dynamic property access via C# dynamic binding
        public object? TryGetValue(string key)
        {
            return _dictionary.TryGetValue(key, out var value) ? value : null;
        }
    }
}