using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

[CollectionDefinition("TernaryOperator", DisableParallelization = true)]
public class TernaryOperatorDefinition { }

[Collection("TernaryOperator")]
public class TernaryOperatorTests : TestBase
{
    public TernaryOperatorTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        // Clear cache before each test to avoid interference
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task TernaryWithTrueCondition_ShouldReturnFirstExpression()
    {
        // Arrange
        var data = new { IsActive = true };
        string template = "Status: {IsActive ? \"Active\" : \"Inactive\"}";
        string expected = "Status: Active";

        // Act
        var options = new DollarSignOptions { EnableDebugLogging = true };
        string actual = await DollarSign.EvalAsync(template, data, options);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithFalseCondition_ShouldReturnSecondExpression()
    {
        // Arrange
        var data = new { IsActive = false };
        string template = "Status: {IsActive ? \"Active\" : \"Inactive\"}";
        string expected = "Status: Inactive";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithComparisonCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var data = new { Age = 25 };
        string template = "Category: {Age >= 18 ? \"Adult\" : \"Minor\"}";
        string expected = "Category: Adult";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithLogicalOperators_ShouldEvaluateCorrectly()
    {
        // Arrange
        var data = new { Age = 20, HasLicense = true };
        string template = "Can Drive: {Age >= 18 && HasLicense ? \"Yes\" : \"No\"}";
        string expected = "Can Drive: Yes";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task NestedTernaryOperators_ShouldEvaluateCorrectly()
    {
        // Arrange
        var data = new { Score = 85 };
        string template = "Grade: {Score >= 90 ? \"A\" : (Score >= 80 ? \"B\" : \"C\")}";
        string expected = "Grade: B";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MultipleNestedTernaries_ShouldEvaluateCorrectly()
    {
        // Arrange
        var data = new { Temperature = 22 };
        string template = "Weather: {Temperature < 0 ? \"Freezing\" : " +
                         "(Temperature < 10 ? \"Cold\" : " +
                         "(Temperature < 20 ? \"Cool\" : " +
                         "(Temperature < 30 ? \"Warm\" : \"Hot\")))}";
        string expected = "Weather: Warm";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithMethodCall_ShouldInvokeMethod()
    {
        // Arrange
        var data = new TernaryTestData { Value = 10 };
        string template = "Result: {Value > 5 ? GetHigh() : GetLow()}";
        string expected = "Result: high value";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithAlternateMethodCall_ShouldInvokeCorrectMethod()
    {
        // Arrange
        var data = new TernaryTestData { Value = 3 };
        string template = "Result: {Value > 5 ? GetHigh() : GetLow()}";
        string expected = "Result: low value";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithFormatSpecifier_ShouldFormatResult()
    {
        // Arrange
        var data = new { IsPremium = true, RegularPrice = 100.00, DiscountPrice = 80.00 };
        string template = "Price: {(IsPremium ? DiscountPrice : RegularPrice):C2}";

        // Use CultureInfo to get the proper currency format
        string formattedPrice = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:C2}", 80.00);
        string expected = $"Price: {formattedPrice}";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithStringConcatenation_ShouldCombineStrings()
    {
        // Arrange
        var data = new { IsVIP = true, Name = "Alice" };
        string template = "Welcome {IsVIP ? \"VIP \" + Name : Name}!";
        string expected = "Welcome VIP Alice!";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithNullValues_ShouldHandleNullChecks()
    {
        // Arrange
        var data = new { UserName = (string)null };
        string template = "Name: {UserName != null ? UserName : \"Guest\"}";
        string expected = "Name: Guest";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithEmptyString_ShouldTreatAsValid()
    {
        // Arrange
        var data = new { Message = "" };
        string template = "Result: {Message == \"\" ? \"Empty\" : Message}";
        string expected = "Result: Empty";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithBooleanResult_ShouldReturnBooleanString()
    {
        // Arrange
        var data = new { X = 10, Y = 5 };
        string template = "Comparison: {X > Y ? true : false}";
        string expected = "Comparison: True";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithNumericResult_ShouldReturnNumber()
    {
        // Arrange
        var data = new { HasDiscount = true, RegularPrice = 100, DiscountValue = 20 };
        string template = "Final Price: {HasDiscount ? RegularPrice - DiscountValue : RegularPrice}";
        string expected = "Final Price: 80";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithVariableResolver_ShouldUseResolvedValue()
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
        string template = "Access: {hasPermission ? \"Granted\" : \"Denied\"}";
        string expected = "Access: Granted";

        // Act
        string actual = await DollarSign.EvalAsync(template, null, options);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MultipleTernariesInSameTemplate_ShouldEvaluateIndependently()
    {
        // Arrange
        var data = new { Age = 25, Income = 40000 };
        string template = "Age Category: {Age >= 18 ? \"Adult\" : \"Minor\"}, " +
                          "Income Level: {Income < 30000 ? \"Low\" : (Income < 60000 ? \"Medium\" : \"High\")}";
        string expected = "Age Category: Adult, Income Level: Medium";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TernaryWithParentheses_ShouldRespectPrecedence()
    {
        // Arrange
        var data = new { A = 5, B = 10, C = 15 };
        string template = "Result: {(A < B) && (B < C) ? \"Valid\" : \"Invalid\"}";
        string expected = "Result: Valid";

        // Act
        string actual = await DollarSign.EvalAsync(template, data);

        // Assert
        actual.Should().Be(expected);
    }
}

/// <summary>
/// Test class with methods for method invocation tests
/// </summary>
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