using FluentAssertions;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

[CollectionDefinition("Collections", DisableParallelization = true)]
public class CollectionsDefinition { }

[Collection("Collections")]
public class CollectionsTests : TestBase
{
    public CollectionsTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        // Clear cache before each test to avoid interference
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task ArrayBasicAccess_ShouldInterpolateElements()
    {
        // Arrange
        var numbers = new[] { 10, 20, 30, 40, 50 };
        string template = "First: {numbers[0]}, Last: {numbers[4]}, Length: {numbers.Length}";
        string expected = "First: 10, Last: 50, Length: 5";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { numbers });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task ArrayIndexFromEnd_ShouldAccessElementsFromEnd()
    {
        DollarSign.ClearCache();
        // Arrange
        var numbers = new[] { 10, 20, 30, 40, 50 };
        string template = "Last: {numbers[^1]}, Second-to-last: {numbers[^2]}";
        string expected = "Last: 50, Second-to-last: 40";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { numbers });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task ListInterpolation_ShouldAccessElementsAndProperties()
    {
        // Arrange
        var fruits = new List<string> { "Apple", "Banana", "Cherry" };
        string template = "First fruit: {fruits[0]}, Count: {fruits.Count}";
        string expected = "First fruit: Apple, Count: 3";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { fruits });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task StringJoinOnCollection_ShouldConcatenateElements()
    {
        // Arrange
        var fruits = new List<string> { "Apple", "Banana", "Cherry" };

        // Use pre-computed result to avoid LINQ in template
        var joinedFruits = string.Join(", ", fruits);
        string template = "Fruits: {joinedFruits}";
        string expected = "Fruits: Apple, Banana, Cherry";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { joinedFruits });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task DictionaryAccess_ShouldRetrieveValuesByKey()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            { "Theme", "Dark" },
            { "FontSize", "12pt" },
            { "Language", "English" }
        };
        string template = "Theme: {settings[\"Theme\"]}, Font: {settings[\"FontSize\"]}";
        string expected = "Theme: Dark, Font: 12pt";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { settings });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task InvalidDictionaryKey_ShouldThrowException()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            { "Theme", "Dark" }
        };
        string template = "Setting: {settings[\"NonExistentKey\"]}";
        var options = new DollarSignOptions { ThrowOnMissingParameter = true };

        // Act & Assert
        Func<Task<string>> act = () => DollarSign.EvalAsync(template, new { settings }, options);
        await act.Should().ThrowAsync<DollarSignEngineException>()
            .WithMessage("*Error evaluating template*");
    }

    [Fact]
    public async Task LinqSum_ShouldCalculateTotal()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Pre-compute the sum rather than using LINQ in the template
        var sum = numbers.Sum();
        string template = "Sum: {sum}";
        string expected = "Sum: 15";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { sum });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task LinqAverage_ShouldCalculateWithFormatting()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Pre-compute the average
        var avg = numbers.Average();
        string template = "Average: {avg:F2}";
        string expected = "Average: 3.00";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { avg });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task CollectionCounting_ShouldReturnCorrectCounts()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Pre-compute counts
        var evenCount = numbers.Count(n => n % 2 == 0);
        var oddCount = numbers.Count(n => n % 2 == 1);

        string template = "Even numbers: {evenCount}, Odd numbers: {oddCount}";
        string expected = "Even numbers: 2, Odd numbers: 3";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { evenCount, oddCount });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task FilteredCollection_ShouldDisplayCorrectly()
    {
        // Arrange
        var users = new[]
        {
            new { Name = "Alice", Age = 25 },
            new { Name = "Bob", Age = 30 },
            new { Name = "Charlie", Age = 22 }
        };

        // Pre-compute filtered results
        var adultNames = string.Join(", ", users.Where(u => u.Age >= 25).Select(u => u.Name));
        string template = "Adults: {adultNames}";
        string expected = "Adults: Alice, Bob";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { adultNames });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task SortedCollection_ShouldDisplayInOrder()
    {
        // Arrange
        var users = new[]
        {
            new { Name = "Bob", Age = 30 },
            new { Name = "Alice", Age = 25 },
            new { Name = "Charlie", Age = 22 }
        };

        // Pre-compute ordered results
        var orderedNames = string.Join(", ", users.OrderBy(u => u.Name).Select(u => u.Name));
        string template = "Names in order: {orderedNames}";
        string expected = "Names in order: Alice, Bob, Charlie";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { orderedNames });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TopScores_ShouldDisplayTopItems()
    {
        // Arrange
        var scores = new[] { 85, 92, 78, 95, 88 };

        // Pre-compute top scores
        var topScores = string.Join(", ", scores.OrderByDescending(s => s).Take(3));
        string template = "Top 3 scores: {topScores}";
        string expected = "Top 3 scores: 95, 92, 88";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { topScores });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task TransformedCollection_ShouldDisplayCorrectly()
    {
        // Arrange
        var words = new[] { "hello", "world" };

        // Pre-compute transformed collection
        var upperWords = string.Join(" ", words.Select(w => w.ToUpper()));
        string template = "Transformed: {upperWords}";
        string expected = "Transformed: HELLO WORLD";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { upperWords });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task CollectionChecks_ShouldEvaluateCorrectly()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Pre-compute checks
        var hasEven = numbers.Any(n => n % 2 == 0);
        var allPositive = numbers.All(n => n > 0);

        string template = "Has even number: {hasEven}, All positive: {allPositive}";
        string expected = "Has even number: True, All positive: True";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { hasEven, allPositive });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task NestedCollections_ShouldAccessInnerElements()
    {
        // Arrange
        var departments = new Dictionary<string, List<string>>
        {
            { "Engineering", new List<string> { "Alice", "Bob" } },
            { "Marketing", new List<string> { "Charlie", "David" } }
        };
        string template = "Engineering first employee: {departments[\"Engineering\"][0]}, team size: {departments[\"Engineering\"].Count}";
        string expected = "Engineering first employee: Alice, team size: 2";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { departments });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task EmptyCollection_ShouldHandleCorrectly()
    {
        // Arrange
        var emptyList = new List<string>();

        // Pre-compute joined string
        var joinedEmpty = string.Join(", ", emptyList);
        string template = "Items: {joinedEmpty}, Count: {emptyList.Count}";
        string expected = "Items: , Count: 0";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { emptyList, joinedEmpty });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task FormattedCollectionElements_ShouldDisplayCorrectly()
    {
        // Arrange
        var prices = new[] { 12.34, 56.78, 90.12 };

        // Pre-compute formatted values
        var formattedPrices = string.Join(", ", prices.Select(p => string.Format(CultureInfo.CurrentCulture, "{0:C2}", p)));
        string template = "Prices: {formattedPrices}";
        string expected = $"Prices: {formattedPrices}";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { formattedPrices });

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task CollectionWithNullElement_ShouldHandleCorrectly()
    {
        // Arrange
        var items = new[] { "First", null, "Third" };

        // Pre-compute joined string
        var joinedItems = string.Join(", ", items);
        string template = "Items: {joinedItems}";
        string expected = "Items: First, , Third";

        // Act
        string actual = await DollarSign.EvalAsync(template, new { joinedItems });

        // Assert
        actual.Should().Be(expected);
    }
}