# DollarSignEngine

Dynamically evaluate and interpolate C# expressions at runtime with ease, leveraging a powerful script execution engine.

## Introduction

The DollarSignEngine is a robust C# library designed to simplify the process of dynamically evaluating and interpolating expressions at runtime. Ideal for applications requiring on-the-fly script execution, it offers developers the flexibility to inject variables and execute complex C# expressions seamlessly.

## Features

- **Dynamic Expression Evaluation:** Evaluate C# expressions dynamically at runtime, with support for interpolation and complex logic.
- **Parameter Injection:** Easily pass parameters into expressions for dynamic evaluation.
- **Support for Complex Types:** Effortlessly handle complex data types, including custom objects, collections, and more.
- **Comprehensive Error Handling:** Provides detailed exceptions for compilation and runtime errors to ease debugging.

## Installation

The library is available on NuGet. You can install it using the following command:

```bash
dotnet add package DollarSignEngine
```

## Usage
Below are some examples of how to use the DollarSignEngine to evaluate expressions dynamically.

**Evaluating Simple Expressions**
```csharp
var result = await DollarSign.EvalAsync("1 + 1");
Console.WriteLine(result); // Outputs: 2
```

**Interpolating Strings with Parameters**
```csharp
var parameters = new Dictionary<string, object>
{
    { "name", "John" }
};
var result = await DollarSign.EvalAsync("Hello, {name}", parameters);
Console.WriteLine(result); // Outputs: Hello, John
```

**Handling Complex Data Types**
```csharp
var person = new { FirstName = "Jane", LastName = "Doe" };
var parameters = new Dictionary<string, object>
{
    { "person", person }
};
var result = await DollarSign.EvalAsync("Person: {person.FirstName} {person.LastName}", parameters);
Console.WriteLine(result); // Outputs: Person: Jane Doe
```

More usage can be found in the test code.
[Show Tests](src/DollarSignEngine.Tests/DollarSignEngineTests.cs)

## Contributing
Contributions are welcome! If you have an idea for improvement or have found a bug, please feel free to create an issue or submit a pull request.