using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class CollectionsTests : TestBase
{
    public CollectionsTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task ArrayInterpolation()
    {
        var numbers = new[] { 10, 20, 30, 40, 50 };

        var expected = $"First: {numbers[0]}, Last: {numbers[^1]}, Length: {numbers.Length}";
        var actual = await DollarSign.EvalAsync("First: {numbers[0]}, Last: {numbers[^1]}, Length: {numbers.Length}", new { numbers });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ListInterpolation()
    {
        var fruits = new List<string> { "Apple", "Banana", "Cherry" };

        var expected = $"Fruits: {string.Join(", ", fruits)}, Count: {fruits.Count}";
        var actual = await DollarSign.EvalAsync("Fruits: {string.Join(\", \", fruits)}, Count: {fruits.Count}", new { fruits });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DictionaryAccessWithIndexer()
    {
        var settings = new Dictionary<string, string>
        {
            { "Theme", "Dark" },
            { "FontSize", "12pt" },
            { "Language", "English" }
        };

        var expected = $"Theme: {settings["Theme"]}, Font: {settings["FontSize"]}";
        var actual = await DollarSign.EvalAsync("Theme: {settings[\"Theme\"]}, Font: {settings[\"FontSize\"]}", new { settings });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LinqMethodsOnCollections()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        var expected = $"Sum: {numbers.Sum()}, Average: {numbers.Average():F1}, Odd Count: {numbers.Count(n => n % 2 == 1)}";
        var actual = await DollarSign.EvalAsync("Sum: {numbers.Sum()}, Average: {numbers.Average():F1}, Odd Count: {numbers.Count(n => n % 2 == 1)}", new { numbers });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CollectionFiltering()
    {
        var users = new[]
        {
            new { Name = "Alice", Age = 25 },
            new { Name = "Bob", Age = 30 },
            new { Name = "Charlie", Age = 22 }
        };

        var expected = $"Adults: {string.Join(", ", users.Where(u => u.Age >= 25).Select(u => u.Name))}";
        var actual = await DollarSign.EvalAsync("Adults: {string.Join(\", \", users.Where(u => u.Age >= 25).Select(u => u.Name))}", new { users });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task NestedCollections()
    {
        var departments = new Dictionary<string, List<string>>
        {
            { "Engineering", new List<string> { "Alice", "Bob" } },
            { "Marketing", new List<string> { "Charlie", "David" } }
        };

        var expected = $"Engineering team size: {departments["Engineering"].Count}";
        var actual = await DollarSign.EvalAsync("Engineering team size: {departments[\"Engineering\"].Count}", new { departments });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CollectionOrdering()
    {
        var scores = new[] { 85, 92, 78, 95, 88 };

        var expected = $"Top 3 scores: {string.Join(", ", scores.OrderByDescending(s => s).Take(3))}";
        var actual = await DollarSign.EvalAsync("Top 3 scores: {string.Join(\", \", scores.OrderByDescending(s => s).Take(3))}", new { scores });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task EnumerableTransformation()
    {
        var words = new[] { "hello", "world" };

        var expected = $"Transformed: {string.Join(" ", words.Select(w => w.ToUpper()))}";
        var actual = await DollarSign.EvalAsync("Transformed: {string.Join(\" \", words.Select(w => w.ToUpper()))}", new { words });

        Assert.Equal(expected, actual);
    }
}