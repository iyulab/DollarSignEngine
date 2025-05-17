using System.Reflection;

namespace DollarSignEngine;

/// <summary>
/// Represents a compiled string interpolation expression
/// </summary>
internal class CompiledExpression
{
    private readonly MethodInfo _evaluateMethod;
    private readonly object[] _methodParameters;
    private readonly Delegate _resolverDelegate;
    private ResolveVariableDelegate? _currentResolver;
    private bool _throwOnError;

    /// <summary>
    /// Creates a new compiled expression from an assembly
    /// </summary>
    internal CompiledExpression(Assembly assembly)
    {
        try
        {
            // Get the compiled evaluator type
            var evaluatorType = assembly.GetType("DynamicInterpolation.Evaluator")
                ?? throw new Exception("Failed to find evaluator type in compiled assembly");

            // Get the resolver delegate type
            var resolverDelegateType = evaluatorType.GetNestedType("ResolverDelegate")
                ?? throw new Exception("Failed to find resolver delegate type");

            // Get the evaluate method
            _evaluateMethod = evaluatorType.GetMethod("Evaluate")
                ?? throw new Exception("Failed to find Evaluate method");

            // Create resolver delegate
            _resolverDelegate = Delegate.CreateDelegate(
                resolverDelegateType,
                this,
                GetType().GetMethod(nameof(ResolverCallback), BindingFlags.Instance | BindingFlags.NonPublic)!);

            // Create parameter array for method invocation
            _methodParameters = new object[] { _resolverDelegate };
        }
        catch (Exception ex)
        {
            throw new DollarSignEngineException("Failed to initialize compiled expression", ex);
        }
    }

    /// <summary>
    /// Callback method invoked by the compiled code
    /// </summary>
    private object? ResolverCallback(string name)
    {
        if (_currentResolver == null)
            return string.Empty;

        try
        {
            var value = _currentResolver(name);
            return value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in resolver callback: {ex.Message}");

            if (_throwOnError)
            {
                if (ex is DollarSignEngineException)
                    throw;
                else
                    throw new DollarSignEngineException($"Error resolving variable '{name}'", ex);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Executes the compiled expression with the given variable resolver
    /// </summary>
    internal string Execute(ResolveVariableDelegate resolver, DollarSignOptions options)
    {
        _throwOnError = options.ThrowOnError;
        _currentResolver = resolver;

        try
        {
            // Invoke the compiled method
            var result = _evaluateMethod.Invoke(null, _methodParameters);

            // Convert result to string
            return result?.ToString() ?? string.Empty;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Print detailed exception info for debugging
            Console.WriteLine($"ERROR in Execute: {ex.InnerException.Message}");
            Console.WriteLine(ex.InnerException.StackTrace);

            if (options.ThrowOnError)
            {
                if (ex.InnerException is DollarSignEngineException innerDsEx)
                    throw innerDsEx;
                else
                    throw new DollarSignEngineException("Error executing expression", ex.InnerException);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            // Print detailed exception info for debugging
            Console.WriteLine($"ERROR in Execute: {ex.Message}");
            Console.WriteLine(ex.StackTrace);

            if (options.ThrowOnError)
            {
                if (ex is DollarSignEngineException)
                    throw;
                else
                    throw new DollarSignEngineException("Error executing expression", ex);
            }

            return string.Empty;
        }
        finally
        {
            _currentResolver = null;
            _throwOnError = false;
        }
    }
}