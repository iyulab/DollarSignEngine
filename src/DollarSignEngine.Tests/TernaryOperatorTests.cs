using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class TernaryOperatorTests : TestBase
{
    public TernaryOperatorTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task BasicTernaryOperator()
    {
        // Arrange
        var data = new { IsActive = true, Name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Status: {IsActive ? \"Active\" : \"Inactive\"}", data);

        // Assert
        Assert.Equal("Status: Active", result);
    }

    [Fact]
    public async Task TernaryOperatorWithFalseCondition()
    {
        // Arrange
        var data = new { IsActive = false, Name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Status: {IsActive ? \"Active\" : \"Inactive\"}", data);

        // Assert
        Assert.Equal("Status: Inactive", result);
    }

    [Fact]
    public async Task TernaryOperatorWithExpressionCondition()
    {
        // Arrange
        var data = new { Age = 25 };

        // Act
        var result = await DollarSign.EvalAsync("Category: {Age >= 18 ? \"Adult\" : \"Minor\"}", data);

        // Assert
        Assert.Equal("Category: Adult", result);
    }

    [Fact]
    public async Task TernaryOperatorWithNestedExpressions()
    {
        // Arrange
        var data = new { Score = 85 };

        // Act
        var result = await DollarSign.EvalAsync("Grade: {Score >= 90 ? \"A\" : (Score >= 80 ? \"B\" : \"C\")}", data);

        // Assert
        Assert.Equal("Grade: B", result);
    }

    [Fact]
    public async Task TernaryOperatorWithMethodCall()
    {
        // Arrange
        var data = new TernaryTestData { Value = 10 };

        // Act
        var result = await DollarSign.EvalAsync("Result: {Value > 5 ? GetHigh() : GetLow()}", data);

        // Assert
        Assert.Equal("Result: high value", result);
    }

    [Fact]
    public async Task TernaryOperatorWithFormatSpecifier()
    {
        // Arrange
        var data = new { IsPremium = true, RegularPrice = 100.00, DiscountPrice = 80.00 };

        // Act
        var result = await DollarSign.EvalAsync("Price: {(IsPremium ? DiscountPrice : RegularPrice):C2}", data);

        // Assert
        Assert.Equal("Price: $80.00", result);
    }

    [Fact]
    public async Task TernaryOperatorWithStringConcatenation()
    {
        // Arrange
        var data = new { IsVIP = true, Name = "Alice" };

        // Act
        var result = await DollarSign.EvalAsync("Welcome {IsVIP ? \"VIP \" + Name : Name}!", data);

        // Assert
        Assert.Equal("Welcome VIP Alice!", result);
    }

    [Fact]
    public async Task TernaryOperatorWithMultipleConditions()
    {
        // Arrange
        var data = new { Temperature = 22 };

        // Act
        var result = await DollarSign.EvalAsync(
            "Weather: {Temperature < 0 ? \"Freezing\" : (Temperature < 10 ? \"Cold\" : (Temperature < 20 ? \"Cool\" : (Temperature < 30 ? \"Warm\" : \"Hot\")))}",
            data);

        // Assert
        Assert.Equal("Weather: Warm", result);
    }

    [Fact]
    public async Task TernaryOperatorWithNullValues()
    {
        // Arrange
        var data = new { UserName = (string)null };

        // Act
        var result = await DollarSign.EvalAsync("Name: {UserName != null ? UserName : \"Guest\"}", data);

        // Assert
        Assert.Equal("Name: Guest", result);
    }

    [Fact]
    public async Task TernaryOperatorWithVariableResolver()
    {
        // Arrange
        var options = new DollarSignOptions
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "hasPermission")
                    return true;
                return null;
            }
        };

        // Act
        var result = await DollarSign.EvalAsync("Access: {hasPermission ? \"Granted\" : \"Denied\"}", null, options);

        // Assert
        Assert.Equal("Access: Granted", result);
    }
}

public class TernaryTestData
{
    public int Value { get; set; }

    public string GetHigh()
    {
        return "high value";
    }

    public string GetLow()
    {
        return "low value";
    }
}