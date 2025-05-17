using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class TernaryOperatorTests : TestBase
{
    public TernaryOperatorTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task TEST()
    {
        // Assert
        var expected = $"{(true ? "TRUE" : "FALSE")}";
        // Act
        var result = await DollarSign.EvalAsync("{(true ? \"TRUE\" : \"FALSE\")}");
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithFalseCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var expected = $"{(false ? "TRUE" : "FALSE")}";

        // Act
        var result = await DollarSign.EvalAsync("{(false ? \"TRUE\" : \"FALSE\")}");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithVariable_ShouldEvaluateCorrectly()
    {
        // Arrange
        var condition = true;
        var expected = $"{(condition ? "YES" : "NO")}";

        // Act
        var result = await DollarSign.EvalAsync("{(condition ? \"YES\" : \"NO\")}", new { condition });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithVariableInBothBranches_ShouldEvaluateCorrectly()
    {
        // Arrange
        var trueValue = "CORRECT";
        var falseValue = "INCORRECT";
        var condition = true;
        var expected = $"{(condition ? trueValue : falseValue)}";

        // Act
        var result = await DollarSign.EvalAsync("{(condition ? trueValue : falseValue)}",
            new { condition, trueValue, falseValue });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task NestedTernaryOperators_ShouldEvaluateCorrectly()
    {
        // Arrange
        var expected = $"{(true ? (false ? "A" : "B") : "C")}";

        // Act
        var result = await DollarSign.EvalAsync("{(true ? (false ? \"A\" : \"B\") : \"C\")}");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithComparisonCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var a = 5;
        var b = 10;
        var expected = $"{(a < b ? "Less" : "Greater or Equal")}";

        // Act
        var result = await DollarSign.EvalAsync("{(a < b ? \"Less\" : \"Greater or Equal\")}",
            new { a, b });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithComplexCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var a = 5;
        var b = 10;
        var c = 15;
        var expected = $"{((a + b > c) ? "Sum is greater" : "Sum is not greater")}";

        // Act
        var result = await DollarSign.EvalAsync("{((a + b > c) ? \"Sum is greater\" : \"Sum is not greater\")}",
            new { a, b, c });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithMethodCallInCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var text = "test";
        var expected = $"{(text.Length > 3 ? "Long" : "Short")}";

        // Act
        var result = await DollarSign.EvalAsync("{(text.Length > 3 ? \"Long\" : \"Short\")}",
            new { text });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithStringEscapesInBranches_ShouldEvaluateCorrectly()
    {
        // Arrange
        var expected = $"{(true ? "Line1\nLine2" : "Line3\nLine4")}";

        // Act
        var result = await DollarSign.EvalAsync("{(true ? \"Line1\\nLine2\" : \"Line3\\nLine4\")}");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task MixedInterpolationWithTernary_ShouldEvaluateCorrectly()
    {
        // Arrange
        var name = "User";
        var isAdmin = true;
        var expected = $"Hello {name}, you are {(isAdmin ? "an admin" : "a regular user")}";

        // Act
        var result = await DollarSign.EvalAsync("Hello {name}, you are {(isAdmin ? \"an admin\" : \"a regular user\")}",
            new { name, isAdmin });

        // Assert
        result.Should().Be(expected);
    }
}