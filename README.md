# DollarSignEngine

Dynamically evaluate and interpolate C# expressions at runtime with ease, leveraging a powerful script execution engine.

## Introduction

The DollarSignEngine is a robust C# library designed to simplify the process of dynamically evaluating and interpolating expressions at runtime. Ideal for applications requiring on-the-fly script execution, it offers developers the flexibility to inject variables and execute complex C# expressions seamlessly.

## Guiding Principles

1. **Core Purpose**: Enable runtime evaluation of C# string interpolation (`$"{}"`) exactly as it works in compile-time C#.

2. **Extension Rules**:
   - All features natively supported in C# string interpolation must be maintained
   - Avoid extending functionality beyond standard C# interpolation syntax
   - Only add features essential for runtime evaluation

3. **Contributor Guidelines**: This library's sole purpose is runtime evaluation of C# interpolation strings. Additional features should only be considered if they directly support this core purpose.

## Features

- **Dynamic Expression Evaluation:** Evaluate C# expressions dynamically at runtime, with support for interpolation and complex logic.
- **Flexible Parameter Injection:** Easily pass parameters into expressions using dictionaries, anonymous objects, or regular C# objects.
- **Support for Complex Types:** Effortlessly handle complex data types, including custom objects, collections, and more.
- **Method Invocation:** Call methods on parameter objects within expressions, such as `{obj.Method()}` or `${obj.Method()}`, consistent with C# interpolation syntax. *(Added)*
- **Format Specifiers & Alignment:** Full support for C# format specifiers and alignment in interpolated expressions.
- **Custom Variable Resolution:** Provide custom variable resolvers for advanced use cases.
- **Multiple Syntax Options:** Support for both standard C# interpolation `{expression}` and dollar-sign `${expression}` syntax.
- **Comprehensive Error Handling:** Provides detailed exceptions for compilation and runtime errors to ease debugging.

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
var options = new DollarSignOption { SupportDollarSignSyntax = true };
var result = await DollarSign.EvalAsync("Greeting: ${Hello()}", greeter, options);
Console.WriteLine(result); // Outputs: Greeting: hello, Bob

// Without dollar sign syntax, the expression is treated as literal
var standardResult = await DollarSign.EvalAsync("Greeting: ${Hello()}", greeter);
Console.WriteLine(standardResult); // Outputs: Greeting: ${Hello()}
```

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
var age = 20;
var result = await DollarSign.EvalAsync("You are {(age >= 18 ? \"adult\" : \"minor\")}.", new { age });
Console.WriteLine(result); // Outputs: You are adult.

var score = 85;
var result = await DollarSign.EvalAsync("Grade: {(score >= 90 ? \"A\" : score >= 80 ? \"B\" : \"C\")}.", new { score });
Console.WriteLine(result); // Outputs: Grade: B.
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
var options = new DollarSignOption { SupportDollarSignSyntax = true };

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

// Example with method invocation
var greeter = new Greeter { Name = "Bob" };
var methodResult = await DollarSign.EvalAsync("Greeting: ${greeter.Hello()}", new { greeter }, options);
Console.WriteLine(methodResult); // Outputs: Greeting: hello, Bob
```

### Custom Variable Resolution

```csharp
// Create a custom variable resolver
var options = new DollarSignOption
{
    VariableResolver = (expression, parameter) =>
    {
        // Custom logic to resolve variables
        if (expression == "currentUser")
            return "Admin";
        
        // Return null to fall back to standard resolution
        return null;
    }
};

var result = await DollarSign.EvalAsync("Current user: {currentUser}", null, options);
Console.WriteLine(result); // Outputs: Current user: Admin
```

## Configuration Options

The `DollarSignOption` class provides several options to customize the behavior of the expression evaluation:

```csharp
var options = new DollarSignOption
{
    // Whether to throw an exception when a parameter is missing
    ThrowOnMissingParameter = false,
    
    // Whether to enable debug logging
    EnableDebugLogging = false,
    
    // Additional namespaces to import in the script
    AdditionalNamespaces = new List<string> { "System.Text.Json" },
    
    // Additional assemblies to reference in the script
    AdditionalAssemblies = new List<string> { "System.Text.Json.dll" },
    
    // Whether to use strict mode for parameter access
    StrictParameterAccess = false,
    
    // The culture to use for formatting operations
    FormattingCulture = new CultureInfo("en-US"),
    
    // Whether to support dollar sign prefixed variables (${name})
    SupportDollarSignSyntax = true,
    
    // A callback for custom variable resolution
    VariableResolver = (expression, parameter) => { /* ... */ },
    
    // Whether to prefer callback-based resolution over script evaluation
    PreferCallbackResolution = true
};
```

## Error Handling

The library provides the `DollarSignEngineException` for handling errors during expression evaluation:

```csharp
try
{
    var result = await DollarSign.EvalAsync("Value: {nonExistentVariable}", null, 
        new DollarSignOption { ThrowOnMissingParameter = true });
}
catch (DollarSignEngineException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    // Handle the error...
}
```