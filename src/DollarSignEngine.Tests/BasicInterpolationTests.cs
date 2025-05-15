using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

[CollectionDefinition("BasicInterpolation", DisableParallelization = true)]
public class BasicInterpolationDefinition { }

[Collection("BasicInterpolation")]
public class BasicInterpolationTests : TestBase
{
    public BasicInterpolationTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        // Clear cache before each test to avoid interference between tests
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task SimpleExpressionEvaluation_ShouldCalculate()
    {
        // Arrange
        string template = "1 + 2 = {1 + 2}";
        string expected = "1 + 2 = 3";

        // Act
        string actual = await DollarSign.EvalAsync(template);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task BasicStringInterpolation_ShouldReplaceVariables()
    {
        // Arrange
        var name = "John";
        string template = "Hello, {name}!";
        string expected = "Hello, John!";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { name });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MultipleInterpolations_ShouldReplaceAllVariables()
    {
        // Arrange
        var firstName = "Jane";
        var lastName = "Doe";
        var age = 30;
        string template = "Name: {firstName} {lastName}, Age: {age}";
        string expected = "Name: Jane Doe, Age: 30";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { firstName, lastName, age });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task NestedObjectInterpolation_ShouldAccessProperties()
    {
        // Arrange
        var person = new { Name = "Alice", Address = new { City = "New York", Country = "USA" } };
        string template = "Person: {Name} from {Address.City}, {Address.Country}";
        string expected = "Person: Alice from New York, USA";

        // Act
        string actual = await DollarSign.EvalAsync(template, person);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task DictionaryInterpolation_ShouldRetrieveValues()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            { "username", "admin" },
            { "role", "Administrator" }
        };
        string template = "User: {username}, Role: {role}";
        string expected = "User: admin, Role: Administrator";

        // Act
        string actual = await DollarSign.EvalAsync(template, parameters);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task FormattingInInterpolation_ShouldApplyFormat()
    {
        // Arrange
        var price = 1234.5678;
        var date = new DateTime(2025, 5, 14);
        string template = "Price: {price:C2}, Date: {date:yyyy-MM-dd}";
        string expected = $"Price: {price:C2}, Date: {date:yyyy-MM-dd}";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { price, date });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task AlignmentInInterpolation_ShouldPadCorrectly()
    {
        // Arrange
        var value = 42;
        string template = "Left aligned: {value,-10} | Right aligned: {value,10}";

        // Convert test expectation to use exact padding for clarity
        string expected = $"Left aligned: {value.ToString().PadRight(10)} | Right aligned: {value.ToString().PadLeft(10)}";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { value });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task CombinedFormatAndAlignment_ShouldWorkTogether()
    {
        // Arrange
        var percentage = 0.8654;
        string template = "Progress: {percentage,8:P1}";

        // Calculate expected output explicitly
        string formattedValue = string.Format("{0:P1}", percentage);
        string expected = $"Progress: {formattedValue.PadLeft(8)}";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { percentage });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task NullParameterHandling_ShouldRenderEmpty()
    {
        // Arrange
        DollarSign.ClearCache(); // Ensure cache is cleared before this specific test
        string? name = null;
        string template = "Hello, {name}!";
        string expected = "Hello, !";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { name });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task EscapedBracesHandling_ShouldPreserveBraces()
    {
        // Arrange
        var name = "John";
        string template = "{{Hello}}, {name}!";
        string expected = "{Hello}, John!";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { name });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task DoubleBracesInValues_ShouldBeHandledCorrectly()
    {
        // Arrange
        var template = "Template: {{name}} will not be interpolated";
        var expected = "Template: {name} will not be interpolated";

        // Act
        var actual = await DollarSign.EvalAsync(template);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task DollarSignSyntaxSupport_ShouldInterpolateWithDollarSign()
    {
        DollarSign.ClearCache();
        // Arrange
        var name = "John";
        var options = new DollarSignOptions { SupportDollarSignSyntax = true };
        string template = "Hello, ${name}!";
        string expected = "Hello, John!";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { name }, options);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MixedSyntaxWithDollarSignEnabled_ShouldOnlyProcessDollarSyntax()
    {
        // Arrange
        var name = "John";
        var options = new DollarSignOptions { SupportDollarSignSyntax = true };
        string template = "Name: {name}, Value: ${name}";
        string expected = "Name: {name}, Value: John";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { name }, options);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MixedSyntaxWithoutDollarSignOption_ShouldOnlyProcessStandardSyntax()
    {
        // Arrange
        var name = "John";
        string template = "Name: {name}, Value: ${name}";
        string expected = "Name: John, Value: ${name}";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { name });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task JsonTemplateWithDollarSign_ShouldPreserveJsonStructure()
    {
        // Arrange
        var user = new { name = "John", age = 30 };
        var options = new DollarSignOptions { SupportDollarSignSyntax = true };
        string template = "{ \"user\": { \"name\": \"{name}\", \"age\": ${age} } }";
        string expected = "{ \"user\": { \"name\": \"{name}\", \"age\": 30 } }";

        // Act
        string actual = await DollarSign.EvalAsync(template, user, options);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task SpecialCharactersInStrings_ShouldBeHandledCorrectly()
    {
        // Arrange
        var text = "Hello, \"World\"! It's a nice day.";
        string template = "Text: {text}";
        string expected = "Text: Hello, \"World\"! It's a nice day.";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { text });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MissingParameters_ShouldReturnEmpty_WhenNotThrowing()
    {
        // Arrange
        string template = "Hello, {name}!";
        string expected = "Hello, !";
        var options = new DollarSignOptions { ThrowOnMissingParameter = false, EnableCaching = false };

        // Act
        string actual = await DollarSign.EvalAsync(template, null, options);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task MissingParameters_ShouldThrowException_WhenOptionEnabled()
    {
        // Arrange
        string template = "Hello, {name}!";
        var options = new DollarSignOptions { ThrowOnMissingParameter = true };

        // Act & Assert
        Func<Task<string>> act = () => DollarSign.EvalAsync(template, null, options);
        await act.Should().ThrowAsync<DollarSignEngineException>()
            .WithMessage("*Error evaluating template*");
    }

    [Fact]
    public async Task ComplexNestedExpressions_ShouldEvaluateCorrectly()
    {
        // Arrange
        var values = new { a = 10, b = 5 };
        string template = "Result: {a + b * 2}";
        string expected = "Result: 20";  // 10 + (5 * 2) = 20

        // Act
        string actual = await DollarSign.EvalAsync(template, values);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task EmptyTemplate_ShouldReturnEmptyString()
    {
        // Arrange
        string template = "";
        string expected = "";

        // Act
        string actual = await DollarSign.EvalAsync(template);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TemplateWithNoInterpolation_ShouldReturnOriginalString()
    {
        // Arrange
        string template = "Hello, world!";
        string expected = "Hello, world!";

        // Act
        string actual = await DollarSign.EvalAsync(template);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task DisablingCaching_ShouldEvaluateExpressionsEachTime()
    {
        // Arrange
        string template = "Value: {value}";
        var options = new DollarSignOptions { EnableCaching = false };

        // First call with one value
        string firstActual = await DollarSign.EvalAsync(template, new { value = "First" }, options);

        // Second call with different value
        string secondActual = await DollarSign.EvalAsync(template, new { value = "Second" }, options);

        // Assert
        firstActual.Should().Be("Value: First");
        secondActual.Should().Be("Value: Second");
    }
}