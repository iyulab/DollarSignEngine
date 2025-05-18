using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class InterpolationTests : TestBase
{
    public InterpolationTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task SumTest()
    {
        // Act
        var result = await DollarSign.EvalAsync("1 + 2 = {1 + 2}");

        // Assert - Compare with C# interpolation
        var expected = $"1 + 2 = {1 + 2}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Interpolate_Simple_Variable()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}!", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Hello, {parameters.name}!";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Interpolate_Multiple_Variables()
    {
        // Arrange
        var parameters = new { firstName = "John", lastName = "Doe" };

        // Act
        var result = await DollarSign.EvalAsync("Name: {firstName} {lastName}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Name: {parameters.firstName} {parameters.lastName}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Empty_Expression()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Null_Variables()
    {
        // Arrange
        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}!", null);

        // Assert - Compare with actual C# behavior for null interpolation
        string? nullValue = null;
        var expected = $"Hello, {nullValue}!";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Escaped_Braces()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {{name}}, your code is: {{code}}", parameters);

        // Assert - Compare with C# interpolation of escaped braces
        var expected = $"Hello, {{name}}, your code is: {{code}}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Escaped_And_Interpolated_Braces_Mixed()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}, your code is: {{code-{name}}}", parameters);

        // Assert - Compare with C# interpolation mixed with escaped braces
        var expected = $"Hello, {parameters.name}, your code is: {{code-{parameters.name}}}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Complex_Objects()
    {
        // Arrange
        var person = new
        {
            Name = "John",
            Address = new
            {
                Street = "123 Main St",
                City = "Anytown",
                ZipCode = "12345"
            }
        };

        // Act
        var result = await DollarSign.EvalAsync("Name: {Name}, Address: {Address.Street}, {Address.City} {Address.ZipCode}", person);

        // Assert - Compare with C# interpolation of complex objects
        var expected = $"Name: {person.Name}, Address: {person.Address.Street}, {person.Address.City} {person.Address.ZipCode}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Dictionary_Values()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["name"] = "John",
            ["age"] = 30
        };

        // Act
        var result = await DollarSign.EvalAsync("Name: {name}, Age: {age}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Name: {parameters["name"]}, Age: {parameters["age"]}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Format_Specifiers()
    {
        // Arrange
        var parameters = new
        {
            price = 123.456,
            date = new DateTime(2023, 10, 15)
        };

        // Act
        var result = await DollarSign.EvalAsync("Price: {price:C2}, Date: {date:yyyy-MM-dd}", parameters);

        // Assert - Compare with C# interpolation with format specifiers
        var expected = $"Price: {parameters.price:C2}, Date: {parameters.date:yyyy-MM-dd}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Use_Cache_For_Repeated_Expressions()
    {
        // Arrange
        var parameters1 = new { name = "John" };
        var parameters2 = new { name = "Jane" };
        var options = new DollarSignOptions { UseCache = true };

        // Act - First call compiles, second should use cache
        var result1 = await DollarSign.EvalAsync("Hello, {name}!", parameters1, options);
        var result2 = await DollarSign.EvalAsync("Hello, {name}!", parameters2, options);

        // Assert - Compare with C# interpolation
        var expected1 = $"Hello, {parameters1.name}!";
        var expected2 = $"Hello, {parameters2.name}!";
        result1.Should().Be(expected1);
        result2.Should().Be(expected2);
    }

    [Fact]
    public void Should_Handle_Synchronous_Evaluation()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = DollarSign.Eval("Hello, {name}!", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Hello, {parameters.name}!";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_String_With_No_Interpolation()
    {
        // Arrange
        var parameters = new { name = "John" };
        var plainText = "This is just plain text with no interpolation.";

        // Act
        var result = await DollarSign.EvalAsync(plainText, parameters);

        // Assert - Compare with C# string
        var expected = plainText; // No interpolation, should be same as input
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Special_Characters_In_String()
    {
        // Arrange
        var parameters = new { name = "John", symbol = "@#$%" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}! Your symbol is {symbol}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Hello, {parameters.name}! Your symbol is {parameters.symbol}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Newlines_And_Tabs_In_String()
    {
        // Arrange
        var parameters = new { name = "John" };
        var template = "Hello,\n{name}!\tWelcome.";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Hello,\n{parameters.name}!\tWelcome.";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Quotes_In_String()
    {
        // Arrange
        var parameters = new { message = "Don't \"worry\"" };

        // Act
        var result = await DollarSign.EvalAsync("Message: {message}", parameters);

        // Assert - Compare with C# interpolation
        var expected = $"Message: {parameters.message}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Missing_Variables_With_Empty_String()
    {
        // Arrange
        var parameters = new { foo = "bar" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}!", parameters);

        // Assert - Compare with C# behavior for missing variables
        string? missingValue = null;
        var expected = $"Hello, {missingValue}!";
        result.Should().Be(expected);
    }
}