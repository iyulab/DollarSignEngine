using System.Text.RegularExpressions;

namespace DollarSignEngine.Internals;

/// <summary>
/// Provides security validation for expressions to prevent dangerous code execution.
/// </summary>
public static class SecurityValidator
{
    // Dangerous namespaces and types that should be blocked
    private static readonly HashSet<string> DangerousKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // File system access
        "System.IO", "File.", "Directory.", "FileStream", "StreamReader", "StreamWriter",
        "Path.", "DriveInfo", "FileInfo", "DirectoryInfo",
        
        // Process and system access
        "Process.", "ProcessStartInfo", "System.Diagnostics.Process",
        "Registry", "RegistryKey", "Microsoft.Win32",
        
        // Reflection and dynamic loading
        "Assembly.", "Type.GetType", "Activator.CreateInstance", "AppDomain",
        "System.Reflection", "MethodInfo", "PropertyInfo", "FieldInfo",
        
        // Threading and parallel execution
        "Thread.", "Task.Run", "Task.Factory", "Parallel.", "ThreadPool",
        "System.Threading", "CancellationToken", "Mutex", "Semaphore",
        
        // Environment and system information
        "Environment.", "System.Environment", "GC.", "Marshal.",
        "RuntimeHelpers", "Unsafe.",
        
        // Network access
        "WebClient", "HttpClient", "WebRequest", "Socket", "TcpClient",
        "System.Net", "NetworkStream",
        
        // Database access
        "SqlConnection", "SqlCommand", "OleDbConnection", "System.Data",
        
        // Code compilation and execution
        "CSharpCodeProvider", "CodeDomProvider", "Microsoft.CodeAnalysis",
        "Roslyn", "ScriptEngine"
    };

    // Dangerous method patterns
    private static readonly Regex[] DangerousPatterns =
    {
        new(@"\busing\s+[^;]+;", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(while|for)\s*\(\s*true\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bgoto\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b__[a-zA-Z]", RegexOptions.Compiled), // Dunder methods
        new(@"sizeof\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"stackalloc\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"fixed\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"unsafe\s*\{", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>
    /// Validates if an expression is safe to execute.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>True if the expression is considered safe, false otherwise.</returns>
    public static bool IsSafeExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        // Check for dangerous keywords
        foreach (var keyword in DangerousKeywords)
        {
            if (expression.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"[SecurityValidator] Blocked expression containing dangerous keyword: {keyword}");
                return false;
            }
        }

        // Check for dangerous patterns
        foreach (var pattern in DangerousPatterns)
        {
            if (pattern.IsMatch(expression))
            {
                Logger.Warning($"[SecurityValidator] Blocked expression matching dangerous pattern: {pattern}");
                return false;
            }
        }

        // Check for excessive nesting (potential stack overflow)
        if (CountNestedBraces(expression) > 20)
        {
            Logger.Warning("[SecurityValidator] Blocked expression with excessive nesting");
            return false;
        }

        // Check expression length (prevent extremely long expressions)
        if (expression.Length > 10000)
        {
            Logger.Warning("[SecurityValidator] Blocked expression exceeding maximum length");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates expression with custom options for different security levels.
    /// </summary>
    public static bool IsSafeExpression(string expression, SecurityLevel level)
    {
        if (!IsSafeExpression(expression))
            return false;

        switch (level)
        {
            case SecurityLevel.Strict:
                return IsStrictModeCompliant(expression);
            case SecurityLevel.Moderate:
                return IsModerateModeCompliant(expression);
            case SecurityLevel.Permissive:
            default:
                return true;
        }
    }

    private static bool IsStrictModeCompliant(string expression)
    {
        // In strict mode, only allow basic arithmetic, property access, and simple method calls
        var allowedPatterns = new[]
        {
            @"^[\w\s\.\+\-\*\/\(\)\[\]\""\',\?:]+$", // Basic operations only
        };

        return allowedPatterns.Any(pattern => Regex.IsMatch(expression, pattern));
    }

    private static bool IsModerateModeCompliant(string expression)
    {
        // In moderate mode, allow LINQ but block complex reflection
        var blockedInModerate = new[]
        {
            "GetType()", "typeof(", "nameof(", "default("
        };

        return !blockedInModerate.Any(blocked =>
            expression.Contains(blocked, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountNestedBraces(string expression)
    {
        int maxDepth = 0;
        int currentDepth = 0;

        foreach (char c in expression)
        {
            switch (c)
            {
                case '(' or '[' or '{':
                    currentDepth++;
                    maxDepth = Math.Max(maxDepth, currentDepth);
                    break;
                case ')' or ']' or '}':
                    currentDepth = Math.Max(0, currentDepth - 1);
                    break;
            }
        }

        return maxDepth;
    }
}

/// <summary>
/// Security levels for expression validation.
/// </summary>
public enum SecurityLevel
{
    /// <summary>
    /// Most restrictive - only basic operations allowed.
    /// </summary>
    Strict,

    /// <summary>
    /// Moderate restrictions - allows LINQ but blocks reflection.
    /// </summary>
    Moderate,

    /// <summary>
    /// Least restrictive - standard security checks only.
    /// </summary>
    Permissive
}