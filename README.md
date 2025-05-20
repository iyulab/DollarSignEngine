# DollarSignEngine

Dynamically evaluate and interpolate C# expressions at runtime with ease, leveraging the power of the Roslyn compiler.

## Introduction

The DollarSignEngine is a robust C# library designed to simplify the process of dynamically evaluating and interpolating expressions at runtime. Ideal for applications requiring on-the-fly evaluation of string templates, it offers developers the flexibility to inject variables and execute complex C# expressions with the same syntax as C# string interpolation.

## Guiding Principles

1. **Core Purpose**: Enable runtime evaluation of C# string interpolation (`$"{}"`) exactly as it works in compile-time C#.

2. **Extension Rules**:
   - All features natively supported in C# string interpolation must be maintained
   - Avoid extending functionality beyond standard C# interpolation syntax
   - Only add features essential for runtime evaluation

## Features

- **Dynamic Expression Evaluation:** Evaluate C# expressions dynamically at runtime, with support for interpolation and complex logic.
- **Flexible Parameter Injection:** Easily pass parameters into expressions using dictionaries, anonymous objects, or regular C# objects.
- **Method & LINQ Support:** Call methods on objects and use LINQ expressions within interpolated strings.
- **Format Specifiers & Alignment:** Full support for C# format specifiers and alignment in interpolated expressions.
- **Custom Variable Resolution:** Provide custom variable resolvers for advanced use cases.
- **Multiple Syntax Options:** Support for both standard C# interpolation `{expression}` and dollar-sign `${expression}` syntax.
- **Comprehensive Error Handling:** Provides detailed exceptions for compilation and runtime errors to ease debugging.
- **Expression Caching:** Compiled expressions are cached for improved performance with repeated evaluations.

## Installation

The library is available on NuGet. You can install it using the following command:

```bash
dotnet add package DollarSignEngine
```

## Usage

Below are some examples of how to use the DollarSignEngine to evaluate expressions dynamically.

### Basic Interpolation

```csharp
// Simple interpolation with current date
var result = await DollarSign.EvalAsync("Today is {DateTime.Now:yyyy-MM-dd}");
Console.WriteLine(result); // Outputs: Today is 2023-05-01 (based on current date)

// With parameters using anonymous object
var name = "John";
var result = await DollarSign.EvalAsync("Hello, {name}!", new { name });
Console.WriteLine(result); // Outputs: Hello, John!

// With parameters using dictionary
var parameters = new Dictionary<string, object?> { { "name", "John" } };
var result = await DollarSign.EvalAsync("Hello, {name}!", parameters);
Console.WriteLine(result); // Outputs: Hello, John!
```

### Using Custom Objects

```csharp
// Using a class directly
public class User
{
    public string Username { get; set; } = string.Empty;
    public int Age { get; set; }
}

var user = new User { Username = "Alice", Age = 30 };
var result = await DollarSign.EvalAsync("User: {Username}, Age: {Age}", user);
Console.WriteLine(result); // Outputs: User: Alice, Age: 30

// Using anonymous types with nested properties
var person = new { 
    Name = "Jane", 
    Address = new { City = "New York", Country = "USA" } 
};
var result = await DollarSign.EvalAsync("Person: {Name} from {Address.City}", person);
Console.WriteLine(result); // Outputs: Person: Jane from New York

// Calling a method on a custom object
public class Greeter
{
    public string Name { get; set; } = string.Empty;

    public string Hello()
    {
        return $"hello, {Name}";
    }
}

var greeter = new Greeter { Name = "Bob" };
var options = new DollarSignOptions { SupportDollarSignSyntax = true };
var result = await DollarSign.EvalAsync("Greeting: ${Hello()}", greeter, options);
Console.WriteLine(result); // Outputs: Greeting: hello, Bob

// Without dollar sign syntax, the expression is treated as literal
var standardResult = await DollarSign.EvalAsync("Greeting: ${Hello()}", greeter);
Console.WriteLine(standardResult); // Outputs: Greeting: ${Hello()}
```

### Method Calls and LINQ Expressions

```csharp
// Calling methods on strings
var text = "hello world";
var result = await DollarSign.EvalAsync("Uppercase: {text.ToUpper()}", new { text });
Console.WriteLine(result); // Outputs: Uppercase: HELLO WORLD

// Using LINQ with collections
var numbers = new List<int> { 1, 2, 3, 4, 5 };
var result = await DollarSign.EvalAsync("Even numbers: {numbers.Where(n => n % 2 == 0).Count()}", new { numbers });
Console.WriteLine(result); // Outputs: Even numbers: 2

// Chaining method calls
var data = "  sample text  ";
var result = await DollarSign.EvalAsync("Processed: {data.Trim().ToUpper().Replace('A', 'X')}", new { data });
Console.WriteLine(result); // Outputs: Processed: SXMPLE TEXT

// Using strongly-typed custom objects for method calls
public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}

var calc = new Calculator();
var result = await DollarSign.EvalAsync("Sum: {calc.Add(5, 3)}, Product: {calc.Multiply(5, 3)}", new { calc });
Console.WriteLine(result); // Outputs: Sum: 8, Product: 15
```

> **Important Note**: For method calls and LINQ expressions to work properly, you need to pass strongly-typed objects rather than anonymous types. This is because the engine needs access to the type information at runtime to properly compile and execute the methods.

### Format Specifiers and Alignment

```csharp
// Using format specifiers
var price = 123.456;
var result = await DollarSign.EvalAsync("Price: {price:C2}", new { price });
Console.WriteLine(result); // Outputs: Price: $123.46

// Using alignment
var number = 42;
var result = await DollarSign.EvalAsync("Left aligned: {number,-10} | Right aligned: {number,10}", new { number });
Console.WriteLine(result); // Outputs: Left aligned: 42         | Right aligned:         42

// Combined alignment and format
var percentage = 0.8654;
var result = await DollarSign.EvalAsync("Progress: {percentage,8:P1}", new { percentage });
Console.WriteLine(result); // Outputs: Progress:    86.5%
```

### Conditional Logic

```csharp
// Simple ternary operation
var age = 20;
var result = await DollarSign.EvalAsync("You are {(age >= 18 ? \"adult\" : \"minor\")}.", new { age });
Console.WriteLine(result); // Outputs: You are adult.

// Nested ternary operations
var score = 85;
var result = await DollarSign.EvalAsync("Grade: {(score >= 90 ? \"A\" : score >= 80 ? \"B\" : \"C\")}.", new { score });
Console.WriteLine(result); // Outputs: Grade: B.

// Complex condition with formatting
var price = 123.456;
var discount = true;
var result = await DollarSign.EvalAsync("Final price: {(discount ? price * 0.9 : price):C2}", 
    new { price, discount });
Console.WriteLine(result); // Outputs: Final price: $111.11
```

### Working with Collections

```csharp
var numbers = new List<int> { 1, 2, 3, 4, 5 };
var result = await DollarSign.EvalAsync("Total sum: {numbers.Sum()}. Items count: {numbers.Count}", new { numbers });
Console.WriteLine(result); // Outputs: Total sum: 15. Items count: 5

var settings = new Dictionary<string, string> { { "Theme", "Dark" }, { "FontSize", "12" } };
var result = await DollarSign.EvalAsync("Theme: {settings[\"Theme\"]}, Font Size: {settings[\"FontSize\"]}", new { settings });
Console.WriteLine(result); // Outputs: Theme: Dark, Font Size: 12
```

### Dollar Sign Syntax

```csharp
// When working with text that contains literal curly braces (like JSON),
// enable dollar sign syntax to specify which parts should be interpolated
var options = new DollarSignOptions { SupportDollarSignSyntax = true };

var user = new { name = "Alice", age = 30 };

// With dollar sign syntax enabled, only ${...} is interpolated
var jsonTemplate = "{ \"user\": { \"name\": \"{name}\", \"age\": ${age} } }";
var result = await DollarSign.EvalAsync(jsonTemplate, user, options);
Console.WriteLine(result); 
// Outputs: { "user": { "name": "{name}", "age": 30 } }

// In standard mode (default), all {...} expressions are interpolated
var standardResult = await DollarSign.EvalAsync("{ \"user\": { \"name\": \"{name}\", \"age\": {age} } }", user);
Console.WriteLine(standardResult); 
// Outputs: { "user": { "name": "Alice", "age": 30 } }
```

## Method Calls and LINQ: Requirements and Limitations

When using method calls and LINQ expressions within interpolated strings, there are a few important considerations:

### Requirements

1. **Strongly-Typed Objects**: For method calls to work properly, pass strongly-typed objects rather than anonymous types whenever possible. The engine needs access to type information at runtime.

   ```csharp
   // This works well - using a concrete class
   var calculator = new Calculator();
   var result = await DollarSign.EvalAsync("Result: {calculator.Add(5, 3)}", new { calculator });
   
   // May have limitations with anonymous types
   var data = new { calc = (Func<int, int, int>)((a, b) => a + b) };
   ```

2. **Required References**: The engine automatically adds references to common assemblies like System.Linq, but custom assemblies might need to be explicitly included via reflection.

3. **Type Compatibility**: Parameters in method calls must be compatible with expected parameter types. The engine attempts to convert types when possible.

### Limitations

1. **Anonymous Method Limitations**: Methods defined inline as lambda expressions in anonymous objects may have restricted functionality compared to methods in concrete classes.

2. **Extension Method Support**: Extension methods (like many LINQ methods) require the appropriate namespace to be in scope. The engine includes common namespaces by default.

3. **Performance Considerations**: Complex method calls and LINQ expressions require more compilation resources than simple property access.

## Configuration Options

The `DollarSignOptions` class provides several options to customize the behavior of the expression evaluation:

```csharp
var options = new DollarSignOptions
{
    // Whether to cache compiled expressions. Defaults to true.
    UseCache = true,
    
    // Whether to throw exceptions on errors during evaluation. Defaults to false.
    ThrowOnError = false,
    
    // Custom variable resolver function.
    VariableResolver = variableName => /* Return value for variable */,
    
    // Custom error handler function.
    ErrorHandler = (expression, exception) => /* Return replacement text for errors */,
    
    // The culture to use for formatting. If null, the current culture is used.
    CultureInfo = new CultureInfo("en-US"),
    
    // Whether to support dollar sign syntax in templates.
    // When enabled, {expression} is treated as literal text and ${expression} is evaluated.
    // Defaults to false.
    SupportDollarSignSyntax = true
};
```

You can also use the static helper methods to create options:

```csharp
// Default options
var options = DollarSignOptions.Default;

// Options with a specific variable resolver
var resolverOptions = DollarSignOptions.WithResolver(name => /* Resolve variable */);

// Options with error handling
var errorOptions = DollarSignOptions.WithErrorHandler((expr, ex) => /* Handle error */);

// Options that throw exceptions on errors
var throwingOptions = DollarSignOptions.Throwing();

// Options with dollar sign syntax support
var dollarOptions = DollarSignOptions.WithDollarSignSyntax();

// Options with a specific culture
var cultureOptions = DollarSignOptions.WithCulture(new CultureInfo("fr-FR"));
```

## Error Handling

The library provides exceptions for handling errors during expression evaluation:

```csharp
try
{
    var result = await DollarSign.EvalAsync("{nonExistentVariable + 1}", null, 
        new DollarSignOptions { ThrowOnError = true });
}
catch (DollarSignEngineException ex)
{
    Console.WriteLine($"General error: {ex.Message}");
    // Handle the error...
}
catch (CompilationException ex)
{
    Console.WriteLine($"Compilation error: {ex.Message}");
    Console.WriteLine($"Error details: {ex.ErrorDetails}");
    // Handle compilation error...
}
```

## Synchronous API

The library also provides synchronous versions of the evaluation methods:

```csharp
// Using object variables
var result = DollarSign.Eval("Hello, {name}!", new { name = "World" });

// Using dictionary variables
var parameters = new Dictionary<string, object?> { { "value", 42 } };
var result = DollarSign.Eval("The answer is {value}.", parameters);
```

## Performance Considerations

- Compiled expressions are cached to improve performance for repeated calls with `UseCache = true` (default)
- Use the `ClearCache()` method to free memory if needed:
  ```csharp
  DollarSign.ClearCache();
  ```
- For templates that are evaluated many times with different parameters, consider reusing the same template string
- Method calls and LINQ queries are more resource-intensive to compile than simple property access