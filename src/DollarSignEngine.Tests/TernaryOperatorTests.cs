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
    public async Task SimpleTernaryOperatorTest()
    {
        // Arrange
        var condition = true;

        // Assert
        var expected = $"{(condition ? "TRUE" : "FALSE")}";

        // Act
        var result = await DollarSign.EvalAsync("{(true ? \"TRUE\" : \"FALSE\")}");

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryOperatorWithVariableTest()
    {
        // Arrange
        var isEnabled = true;
        var variables = new Dictionary<string, object?>
        {
            { "isEnabled", isEnabled }
        };

        // Assert
        var expected = $"{(isEnabled ? "Feature is enabled" : "Feature is disabled")}";

        // Act
        var result = await DollarSign.EvalAsync("{(isEnabled ? \"Feature is enabled\" : \"Feature is disabled\")}", variables);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryOperatorWithFormatSpecifierTest()
    {
        // Arrange
        var amount = 1234.56;
        var condition = true;
        var variables = new Dictionary<string, object?>
        {
            { "amount", amount }
        };

        // Assert
        var expected = $"{(condition ? amount : 0):C}"; // Should be formatted as currency

        // Act
        var result = await DollarSign.EvalAsync("{(true ? amount : 0):C}", variables);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithStringConcatenationTest()
    {
        // Arrange
        var userName = "Alice";
        var isAdmin = true;
        var variables = new Dictionary<string, object?>
        {
            { "userName", userName },
            { "isAdmin", isAdmin }
        };

        // Assert
        var expected = $"{userName + (isAdmin ? " (Admin)" : "")}";

        // Act
        var result = await DollarSign.EvalAsync("{userName + (isAdmin ? \" (Admin)\" : \"\")}", variables);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task MultipleTernaryOperatorsInOneStringTest()
    {
        // Arrange
        var isLoggedIn = true;
        var hasPermission = false;
        var variables = new Dictionary<string, object?>
        {
            { "isLoggedIn", isLoggedIn },
            { "hasPermission", hasPermission }
        };

        // Assert
        var expected = $"User is {(isLoggedIn ? "logged in" : "logged out")} and {(hasPermission ? "has" : "does not have")} permission";

        // Act
        var result = await DollarSign.EvalAsync("User is {(isLoggedIn ? \"logged in\" : \"logged out\")} and {(hasPermission ? \"has\" : \"does not have\")} permission", variables);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithNumbersTest()
    {
        // Arrange
        var useHighValue = true;
        var variables = new Dictionary<string, object?>
        {
            { "useHighValue", useHighValue }
        };

        // Assert
        var expected = $"The value is {(useHighValue ? 100 : 50)}";

        // Act
        var result = await DollarSign.EvalAsync("The value is {(useHighValue ? 100 : 50)}", variables);

        result.Should().Be(expected);
    }
}