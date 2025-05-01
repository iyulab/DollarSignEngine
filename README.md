# DollarSignEngine

Dynamically evaluate and interpolate C# expressions at runtime with ease, leveraging a powerful script execution engine.

## Introduction

The DollarSignEngine is a robust C# library designed to simplify the process of dynamically evaluating and interpolating expressions at runtime. Ideal for applications requiring on-the-fly script execution, it offers developers the flexibility to inject variables and execute complex C# expressions seamlessly.

## Guiding Principles

1. **Core Purpose**: Enable runtime evaluation of C# string interpolation ($"{}") exactly as it works in compile-time C#.

2. **Extension Rules**:
   - All features natively supported in C# string interpolation must be maintained
   - Avoid extending functionality beyond standard C# interpolation syntax
   - Only add features essential for runtime evaluation

3. **Contributor Guidelines**: This library's sole purpose is runtime evaluation of C# interpolation strings. Additional features should only be considered if they directly support this core purpose.

## Features

- **Dynamic Expression Evaluation:** Evaluate C# expressions dynamically at runtime, with support for interpolation and complex logic.
- **Flexible Parameter Injection:** Easily pass parameters into expressions using dictionaries, anonymous objects, or regular C# objects.
- **Support for Complex Types:** Effortlessly handle complex data types, including custom objects, collections, and more.
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

// With parameters using dictionary (for backward compatibility)
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
