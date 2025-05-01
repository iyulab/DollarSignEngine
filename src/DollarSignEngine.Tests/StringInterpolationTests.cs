using System.Dynamic;
using System.Globalization;

namespace DollarSignEngine.Tests;

/// <summary>
/// Core tests to verify DollarSign runtime interpolation matches compile-time string interpolation behavior.
/// Each test directly compares the result of C# compiler's string interpolation with DollarSign's runtime interpolation.
/// </summary>
public class StringInterpolationTests
{
    // Basic interpolation tests

    [Fact]
    public async Task BasicStringInterpolation()
    {
        var name = "John";

        // Compile-time
        var expected = $"Hello, {name}!";

        // Runtime
        var actual = await DollarSign.EvalAsync("Hello, {name}!", new { name });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task MultipleParametersInterpolation()
    {
        var firstName = "John";
        var lastName = "Doe";

        // Compile-time
        var expected = $"Name: {firstName} {lastName}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Name: {firstName} {lastName}", new { firstName, lastName });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task NumberFormatInterpolation()
    {
        var amount = 1234.56;

        // Compile-time
        var expected = $"Amount: {amount:C}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Amount: {amount:C}", new { amount });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DateTimeFormatInterpolation()
    {
        var date = new DateTime(2023, 5, 15);

        // Compile-time
        var expected = $"Date: {date:yyyy-MM-dd}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Date: {date:yyyy-MM-dd}", new { date });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task EscapedBraceInterpolation()
    {
        var value = 42;

        // Compile-time
        var expected = $"Value: {value} {{escaped}}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Value: {value} {{escaped}}", new { value });

        Assert.Equal(expected, actual);
    }

    // Complex type and expression tests

    [Fact]
    public async Task NestedPropertyInterpolation()
    {
        var person = new { Name = "Alice", Address = new { City = "New York", Country = "USA" } };

        // Compile-time
        var expected = $"Person: {person.Name} from {person.Address.City}, {person.Address.Country}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Person: {person.Name} from {person.Address.City}, {person.Address.Country}", new { person });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ConditionalExpressionInterpolation()
    {
        var age = 20;

        // Compile-time
        var expected = $"Status: {(age >= 18 ? "Adult" : "Minor")}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Status: {(age >= 18 ? \"Adult\" : \"Minor\")}", new { age });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ComplexConditionalExpressionInterpolation()
    {
        var score = 85;

        // Compile-time
        var expected = $"Grade: {(score >= 90 ? "A" : score >= 80 ? "B" : score >= 70 ? "C" : "D")}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Grade: {(score >= 90 ? \"A\" : score >= 80 ? \"B\" : score >= 70 ? \"C\" : \"D\")}", new { score });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task MathExpressionInterpolation()
    {
        var x = 10;
        var y = 5;

        // Compile-time
        var expected = $"Sum: {x + y}, Product: {x * y}, Division: {x / y}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Sum: {x + y}, Product: {x * y}, Division: {x / y}", new { x, y });

        Assert.Equal(expected, actual);
    }

    // Collection tests

    [Fact]
    public async Task ArrayInterpolation()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Compile-time
        var expected = $"First: {numbers[0]}, Last: {numbers[^1]}, Length: {numbers.Length}";

        // Runtime
        var actual = await DollarSign.EvalAsync("First: {numbers[0]}, Last: {numbers[^1]}, Length: {numbers.Length}", new { numbers });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ListInterpolation()
    {
        var items = new List<int> { 10, 20, 30 };

        // Compile-time
        var expected = $"List count: {items.Count}";

        // Runtime
        var actual = await DollarSign.EvalAsync("List count: {items.Count}", new { items });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LinqExpressionInterpolation()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Compile-time
        var expected = $"Sum: {numbers.Sum()}, Even Count: {numbers.Count(n => n % 2 == 0)}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Sum: {numbers.Sum()}, Even Count: {numbers.Count(n => n % 2 == 0)}", new { numbers });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DictionaryInterpolation()
    {
        var dict = new Dictionary<string, string> { { "Key1", "Value1" }, { "Key2", "Value2" } };

        // Compile-time
        var expected = $"Value: {dict["Key1"]}, Contains Key2: {dict.ContainsKey("Key2")}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Value: {dict[\"Key1\"]}, Contains Key2: {dict.ContainsKey(\"Key2\")}", new { dict });

        Assert.Equal(expected, actual);
    }

    // Object tests

    [Fact]
    public async Task CustomClassInterpolation()
    {
        var user = new User { Name = "Bob", Age = 30 };

        // Compile-time
        var expected = $"User: {user.Name}, Age: {user.Age}";

        // Runtime
        var actual = await DollarSign.EvalAsync("User: {user.Name}, Age: {user.Age}", new { user });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DirectObjectInterpolation()
    {
        var user = new User { Name = "Bob", Age = 30 };

        // Compile-time
        var expected = $"User: {user.Name}, Age: {user.Age}";

        // Runtime - passing the object directly
        var actual = await DollarSign.EvalAsync("User: {Name}, Age: {Age}", user);

        Assert.Equal(expected, actual);
    }

    // Various parameter passing styles

    [Fact]
    public async Task DictionaryParameterInterpolation()
    {
        var name = "John";
        var age = 30;

        // Compile-time
        var expected = $"Name: {name}, Age: {age}";

        // Runtime with dictionary
        var parameters = new Dictionary<string, object?> { { "name", name }, { "age", age } };
        var actual = await DollarSign.EvalAsync("Name: {name}, Age: {age}", parameters);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task AnonymousTypeParameterInterpolation()
    {
        var name = "John";
        var age = 30;

        // Compile-time
        var expected = $"Name: {name}, Age: {age}";

        // Runtime with anonymous type
        var actual = await DollarSign.EvalAsync("Name: {name}, Age: {age}", new { name, age });

        Assert.Equal(expected, actual);
    }

    // Advanced formatting

    [Fact]
    public async Task AlignmentAndFormatInterpolation()
    {
        var value = 42;

        // Compile-time
        var expected = $"Value: {value,10:D5}";

        // Runtime
        var actual = await DollarSign.EvalAsync("Value: {value,10:D5}", new { value });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CultureSpecificFormatInterpolation()
    {
        var amount = 1234.56;
        var currentCulture = CultureInfo.CurrentCulture;

        try
        {
            // Set specific culture for test
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            // Compile-time
            var expected = $"Amount: {amount:C}";

            // Runtime
            var actual = await DollarSign.EvalAsync("Amount: {amount:C}", new { amount });

            Assert.Equal(expected, actual);
        }
        finally
        {
            // Restore original culture
            CultureInfo.CurrentCulture = currentCulture;
        }
    }

    // Helper class for tests
    private class User
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}