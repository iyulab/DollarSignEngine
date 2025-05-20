using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class MethodTests : TestBase
{
    public MethodTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task Should_Handle_Simple_Method_Call()
    {
        // Arrange
        var parameters = new { text = "hello world" };

        // Act
        var result = await DollarSign.EvalAsync("Uppercase: {text.ToUpper()}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Uppercase: {parameters.text.ToUpper()}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_With_Parameters()
    {
        // Arrange
        var parameters = new { text = "hello world" };

        // Act
        var result = await DollarSign.EvalAsync("Substring: {text.Substring(0, 5)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Substring: {parameters.text.Substring(0, 5)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Chained_Method_Calls()
    {
        // Arrange
        var parameters = new { text = "  hello world  " };

        // Act
        var result = await DollarSign.EvalAsync("Transformed: {text.Trim().ToUpper()}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Transformed: {parameters.text.Trim().ToUpper()}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_With_Complex_Objects()
    {
        // Arrange
        var parameters = new
        {
            person = new
            {
                Name = "John Doe",
                BirthDate = new DateTime(1990, 1, 15)
            }
        };

        // Act
        var result = await DollarSign.EvalAsync("Name: {person.Name.ToUpper()}, Birth Year: {person.BirthDate.Year}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Name: {parameters.person.Name.ToUpper()}, Birth Year: {parameters.person.BirthDate.Year}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_With_Multiple_Parameters()
    {
        // Arrange
        var parameters = new
        {
            text = "hello world",
            start = 6,
            length = 5
        };

        // Act
        var result = await DollarSign.EvalAsync("Substring: {text.Substring(start, length)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Substring: {parameters.text.Substring(parameters.start, parameters.length)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Call_With_Format_Specifier()
    {
        // Arrange
        var parameters = new
        {
            date = new DateTime(2023, 10, 15)
        };

        // Act
        var result = await DollarSign.EvalAsync("Day of week: {date.DayOfWeek}, Short date: {date.ToShortDateString():yyyy-MM-dd}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Day of week: {parameters.date.DayOfWeek}, Short date: {parameters.date.ToShortDateString():yyyy-MM-dd}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Call_With_Null_Object()
    {
        // Arrange
        var parameters = new
        {
            nullObject = (string)null
        };

        // Act
        var result = await DollarSign.EvalAsync("Null check: {nullObject?.Length}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Null check: {parameters.nullObject?.Length}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Static_Method_Calls()
    {
        // Arrange
        var parameters = new
        {
            value = 123.456
        };

        // Act
        var result = await DollarSign.EvalAsync("Rounded: {Math.Round(value, 2)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Rounded: {Math.Round(parameters.value, 2)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_With_String_Interpolation()
    {
        // Arrange
        var parameters = new
        {
            firstName = "John",
            lastName = "Doe"
        };

        // Act 
        var result = await DollarSign.EvalAsync("Full name: {$\"{firstName} {lastName}\".ToUpper()}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Full name: {$"{parameters.firstName} {parameters.lastName}".ToUpper()}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_With_Boolean_Return()
    {
        // Arrange
        var parameters = new
        {
            text = "Hello World",
            searchTerm = "World"
        };

        // Act
        var result = await DollarSign.EvalAsync("Contains 'World': {text.Contains(searchTerm)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Contains 'World': {parameters.text.Contains(parameters.searchTerm)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_Within_Conditional_Expressions()
    {
        // Arrange
        var parameters = new
        {
            text = "Hello World"
        };

        // Act
        var result = await DollarSign.EvalAsync("Result: {(text.Contains(\"World\") ? text.ToUpper() : text.ToLower())}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Result: {(parameters.text.Contains("World") ? parameters.text.ToUpper() : parameters.text.ToLower())}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_On_Passed_Function_Objects()
    {
        // Arrange - Create a class with methods to test passing functions
        var calculator = new Calculator();
        var parameters = new
        {
            calc = calculator,
            x = 10,
            y = 5
        };

        // Act
        var result = await DollarSign.EvalAsync("Sum: {calc.Add(x, y)}, Product: {calc.Multiply(x, y)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Sum: {parameters.calc.Add(parameters.x, parameters.y)}, Product: {parameters.calc.Multiply(parameters.x, parameters.y)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Method_Calls_With_Function_Parameters()
    {
        // Arrange
        var stringProcessor = new StringProcessor();
        var parameters = new
        {
            processor = stringProcessor,
            transform = new Func<string, string>(s => s.ToUpper()),
            text = "hello world"
        };

        // Act
        var result = await DollarSign.EvalAsync("Processed: {processor.Process(text, transform)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Processed: {parameters.processor.Process(parameters.text, parameters.transform)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Object_With_Method_Returning_Functions()
    {
        // Arrange
        var functionProvider = new FunctionProvider();
        var parameters = new
        {
            provider = functionProvider,
            input = 5
        };

        // Act
        var result = await DollarSign.EvalAsync("Doubled: {provider.GetDoubler()(input)}, Squared: {provider.GetSquarer()(input)}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Doubled: {parameters.provider.GetDoubler()(parameters.input)}, Squared: {parameters.provider.GetSquarer()(parameters.input)}";
        result.Should().Be(expected);
    }
}

// Helper classes for testing function calls
public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
    public int Multiply(int a, int b) => a * b;
    public double Divide(int a, int b) => (double)a / b;
}

public class StringProcessor
{
    public string Process(string input, Func<string, string> transformation)
    {
        return transformation(input);
    }
}

public class FunctionProvider
{
    public Func<int, int> GetDoubler()
    {
        return x => x * 2;
    }

    public Func<int, int> GetSquarer()
    {
        return x => x * x;
    }
}