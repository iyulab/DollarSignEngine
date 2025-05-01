using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class BasicInterpolationTests : TestBase
{
    public BasicInterpolationTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task AddCalc()
    {
        var expected = $"1 + 2 = {1 + 2}";
        // Runtime
        var actual = await DollarSign.EvalAsync("1 + 2 = {1 + 2}");
        Assert.Equal(expected.ToString(), actual);
    }

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
    public async Task MultipleInterpolations()
    {
        var firstName = "Jane";
        var lastName = "Doe";
        var age = 30;

        var expected = $"Name: {firstName} {lastName}, Age: {age}";
        var actual = await DollarSign.EvalAsync("Name: {firstName} {lastName}, Age: {age}", new { firstName, lastName, age });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task NestedObjectInterpolation()
    {
        var person = new { Name = "Alice", Address = new { City = "New York", Country = "USA" } };

        var expected = $"Person: {person.Name} from {person.Address.City}, {person.Address.Country}";
        var actual = await DollarSign.EvalAsync("Person: {Name} from {Address.City}, {Address.Country}", person);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DictionaryInterpolation()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "username", "admin" },
            { "role", "Administrator" }
        };

        var expected = $"User: {parameters["username"]}, Role: {parameters["role"]}";
        var actual = await DollarSign.EvalAsync("User: {username}, Role: {role}", parameters);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task StaticMethodInterpolation()
    {
        var expected = $"Current date: {DateTime.Now.Date:yyyy-MM-dd}";
        var actual = await DollarSign.EvalAsync("Current date: {DateTime.Now.Date:yyyy-MM-dd}");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task NullParameterHandling()
    {
        var name = (string?)null;

        var expected = $"Hello, {name}!";
        var actual = await DollarSign.EvalAsync("Hello, {name}!", new { name });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task EscapedBracesHandling()
    {
        var name = "John";

        var expected = $"{{Hello}}, {name}!";
        var actual = await DollarSign.EvalAsync("{{Hello}}, {name}!", new { name });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task StandardInterpolation()
    {
        // Standard interpolation with no options
        var name = "John";
        var expected = $"Hello, {name}!";
        var actual = await DollarSign.EvalAsync("Hello, {name}!", new { name });
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DollarSignSyntaxSupport()
    {
        var name = "John";
        var option = new DollarSignOption { SupportDollarSignSyntax = true };

        var expected = $"Hello, {name}!";
        var actual = await DollarSign.EvalAsync("Hello, ${name}!", new { name }, option);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task MixedSyntaxWithDollarSignEnabled()
    {
        // Testing both syntaxes with dollar sign option enabled
        // Only ${name} should be evaluated, {name} should remain as-is
        var name = "John";
        var option = new DollarSignOption { SupportDollarSignSyntax = true };
        var expected = "Name: {name}, Value: John";
        var actual = await DollarSign.EvalAsync("Name: {name}, Value: ${name}", new { name }, option);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task MixedSyntaxWithoutDollarSignOption()
    {
        // Testing both syntaxes without dollar sign option
        // Only {name} should be evaluated, ${name} should remain as-is
        var name = "John";
        var expected = "Name: John, Value: ${name}";
        var actual = await DollarSign.EvalAsync("Name: {name}, Value: ${name}", new { name });
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task JsonTemplateWithDollarSign()
    {
        // Testing with JSON template using dollar sign
        var user = new { name = "John", age = 30 };
        var option = new DollarSignOption { SupportDollarSignSyntax = true };

        var template = "{ \"user\": { \"name\": \"{name}\", \"age\": ${age} } }";
        var expected = "{ \"user\": { \"name\": \"{name}\", \"age\": 30 } }";

        var actual = await DollarSign.EvalAsync(template, user, option);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task JsonTemplateWithStandardMode()
    {
        // Testing with JSON template using standard mode
        var user = new { name = "John", age = 30 };

        var template = "{ \"user\": { \"name\": \"{name}\", \"age\": {age} } }";
        var expected = "{ \"user\": { \"name\": \"John\", \"age\": 30 } }";

        var actual = await DollarSign.EvalAsync(template, user);
        Assert.Equal(expected, actual);
    }
}