using FluentAssertions;
using Xunit.Abstractions;
using System.Globalization;

namespace DollarSignEngine.Tests;

public class OptionsTests : TestBase
{
    public OptionsTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task DollarSignSyntaxShouldApplyVariables()
    {
        // Arrange
        var parameters = new { name = "John" };
        var options = new DollarSignOptions { SupportDollarSignSyntax = true };

        // Act
        var result = await DollarSign.EvalAsync("{name}: ${name}!", parameters, options);

        // Assert
        result.Should().Be($"{{name}}: {parameters.name}!");
    }

    [Fact]
    public async Task CultureInfoShouldAffectFormatting()
    {
        // Arrange
        var parameters = new
        {
            number = 1234.56,
            date = new DateTime(2023, 4, 15)
        };

        // Create cultures
        var usCulture = new CultureInfo("en-US");
        var frCulture = new CultureInfo("fr-FR");
        var deCulture = new CultureInfo("de-DE");

        // Act - Apply different cultures
        var usOptions = DollarSignOptions.Default.WithCulture(usCulture);
        var frOptions = DollarSignOptions.Default.WithCulture(frCulture);
        var deOptions = DollarSignOptions.Default.WithCulture(deCulture);

        var usResult = await DollarSign.EvalAsync("{number:C} on {date:d}", parameters, usOptions);
        var frResult = await DollarSign.EvalAsync("{number:C} on {date:d}", parameters, frOptions);
        var deResult = await DollarSign.EvalAsync("{number:C} on {date:d}", parameters, deOptions);

        // Assert - Generate expected values using the same cultures
        string usExpected = string.Format(usCulture, "{0:C} on {1:d}", parameters.number, parameters.date);
        string frExpected = string.Format(frCulture, "{0:C} on {1:d}", parameters.number, parameters.date);
        string deExpected = string.Format(deCulture, "{0:C} on {1:d}", parameters.number, parameters.date);

        usResult.Should().Be(usExpected);
        frResult.Should().Be(frExpected);
        deResult.Should().Be(deExpected);
    }

    [Fact]
    public async Task DefaultCultureShouldBeCurrentCulture()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Set a specific culture for testing
            var testCulture = new CultureInfo("es-ES");
            CultureInfo.CurrentCulture = testCulture;

            var parameters = new
            {
                number = 1234.56,
                date = new DateTime(2023, 4, 15)
            };

            // Act - Using default options (should use CurrentCulture)
            var defaultResult = await DollarSign.EvalAsync("{number:C} on {date:d}", parameters);

            // Act - Explicitly setting the same culture
            var explicitOptions = DollarSignOptions.Default.WithCulture(testCulture);
            var explicitResult = await DollarSign.EvalAsync("{number:C} on {date:d}", parameters, explicitOptions);

            // Generate expected value using the test culture
            string expected = string.Format(testCulture, "{0:C} on {1:d}", parameters.number, parameters.date);

            // Assert
            defaultResult.Should().Be(expected);
            explicitResult.Should().Be(expected);
        }
        finally
        {
            // Restore the original culture
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task CultureShouldAffectNumericFormatting()
    {
        // Arrange
        var parameters = new { value = 12345.6789 };

        // Create cultures
        var usCulture = new CultureInfo("en-US");
        var frCulture = new CultureInfo("fr-FR");
        var jpCulture = new CultureInfo("ja-JP");

        // Act with different cultures
        var usOptions = DollarSignOptions.Default.WithCulture(usCulture);
        var frOptions = DollarSignOptions.Default.WithCulture(frCulture);
        var jpOptions = DollarSignOptions.Default.WithCulture(jpCulture);

        var usNumberResult = await DollarSign.EvalAsync("{value:N2}", parameters, usOptions);
        var frNumberResult = await DollarSign.EvalAsync("{value:N2}", parameters, frOptions);
        var jpNumberResult = await DollarSign.EvalAsync("{value:N2}", parameters, jpOptions);

        // Assert - using the same cultures to generate expected values
        string usExpected = string.Format(usCulture, "{0:N2}", parameters.value);
        string frExpected = string.Format(frCulture, "{0:N2}", parameters.value);
        string jpExpected = string.Format(jpCulture, "{0:N2}", parameters.value);

        usNumberResult.Should().Be(usExpected);
        frNumberResult.Should().Be(frExpected);
        jpNumberResult.Should().Be(jpExpected);
    }

    [Fact]
    public async Task CultureShouldAffectCurrencyFormatting()
    {
        // Arrange
        var parameters = new { price = 42.5 };

        // Create cultures
        var usCulture = new CultureInfo("en-US");
        var gbCulture = new CultureInfo("en-GB");
        var jpCulture = new CultureInfo("ja-JP");
        var krCulture = new CultureInfo("ko-KR");

        // Act with different cultures
        var usOptions = DollarSignOptions.Default.WithCulture(usCulture);
        var gbOptions = DollarSignOptions.Default.WithCulture(gbCulture);
        var jpOptions = DollarSignOptions.Default.WithCulture(jpCulture);
        var krOptions = DollarSignOptions.Default.WithCulture(krCulture);

        var usCurrencyResult = await DollarSign.EvalAsync("{price:C}", parameters, usOptions);
        var gbCurrencyResult = await DollarSign.EvalAsync("{price:C}", parameters, gbOptions);
        var jpCurrencyResult = await DollarSign.EvalAsync("{price:C}", parameters, jpOptions);
        var krCurrencyResult = await DollarSign.EvalAsync("{price:C}", parameters, krOptions);

        // Assert - using the same cultures to generate expected values
        string usExpected = string.Format(usCulture, "{0:C}", parameters.price);
        string gbExpected = string.Format(gbCulture, "{0:C}", parameters.price);
        string jpExpected = string.Format(jpCulture, "{0:C}", parameters.price);
        string krExpected = string.Format(krCulture, "{0:C}", parameters.price);

        usCurrencyResult.Should().Be(usExpected);
        gbCurrencyResult.Should().Be(gbExpected);
        jpCurrencyResult.Should().Be(jpExpected);
        krCurrencyResult.Should().Be(krExpected);
    }
}