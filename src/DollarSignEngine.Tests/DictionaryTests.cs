using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class DictionaryTests : TestBase
{
    public DictionaryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ShouldResolveArrayOfAnonymousTypesInGlobalData()
    {
        var items = new[]
        {
            new { Id = 1, Name = "Item1" },
            new { Id = 2, Name = "Item2" },
            new { Id = 3, Name = "Item3" }
        };
        var data = new Dictionary<string, object>
        {
            {
                "Items",
                items
            }
        };
        var template = "${Items[0].Id}. ${Items[0].Name} ";

        var options = DollarSignOptions.Default
            .WithDollarSignSyntax()
            .WithGlobalData(data)
            .WithErrorHandler((expr, ex) =>
            {
                Console.WriteLine($"Expression error: '{expr}' - {ex.Message}");
                return string.Empty;  // Return empty on error
            });

        // Act
        var result = DollarSign.Eval(template, null, options);

        var expected = $"{items[0].Id}. {items[0].Name} ";
        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldResolveListTypesInGlobalData()
    {
        var items = new List<object>
        {
            new { Id = 1, Name = "Item1" },
            new { Id = 2, Name = "Item2" },
            new { Id = 3, Name = "Item3" }
        };
        var data = new Dictionary<string, object>
        {
            {
                "Items",
                items
            }
        };
        var template = "${Items[0].Id}. ${Items[0].Name} ";

        var options = DollarSignOptions.Default
            .WithDollarSignSyntax()
            .WithGlobalData(data)
            .WithErrorHandler((expr, ex) =>
            {
                Console.WriteLine($"Expression error: '{expr}' - {ex.Message}");
                return string.Empty;  // Return empty on error
            });

        // Act
        var result = DollarSign.Eval(template, null, options);

        var expected = $"1. Item1 ";
        result.Should().Be(expected);
    }
}