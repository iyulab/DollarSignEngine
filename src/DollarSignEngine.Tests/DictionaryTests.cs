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

    [Fact]
    public void ShouldResolveDictionaryWithListValues()
    {
        // Dictionary에 List 값을 넣는 간단한 테스트
        var data = new Dictionary<string, object>
        {
            { "Numbers", new List<int> { 10, 20, 30 } },
            { "Names", new List<string> { "Alice", "Bob", "Charlie" } }
        };

        var template = "${Numbers[1]} - ${Names[2]}";
        var options = DollarSignOptions.Default
            .WithDollarSignSyntax()
            .WithGlobalData(data);

        // Act
        var result = DollarSign.Eval(template, null, options);
        var expected = "20 - Charlie";

        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldResolveNestedDictionaryWithList()
    {
        // 하나의 Dictionary 내에 또 다른 Dictionary가 있고, 그 안에 List가 있는 경우
        var data = new Dictionary<string, object>
        {
            {
                "User", new Dictionary<string, object>
                {
                    { "Name", "John" },
                    { "Hobbies", new List<string> { "Reading", "Gaming", "Coding" } }
                }
            }
        };

        var template = "${User.Name} likes ${User.Hobbies[0]} and ${User.Hobbies[2]}";
        var options = DollarSignOptions.Default
            .WithDollarSignSyntax()
            .WithGlobalData(data);

        // Act
        var result = DollarSign.Eval(template, null, options);
        var expected = "John likes Reading and Coding";

        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldResolveListOfDictionaries()
    {
        var products = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "Name", "Laptop" }, { "Price", 1200 } },
            new Dictionary<string, object> { { "Name", "Phone" }, { "Price", 800 } }
        };

        // Dictionary 내에 Dictionary 목록이 있는 경우
        var data = new Dictionary<string, object>
        {
            {
                "Products", products
            }
        };

        var template = "1. ${Products[0][\"Name\"]}: $${Products[0][\"Price\"]}, 2. ${Products[1][\"Name\"]}: $${Products[1][\"Price\"]}";
        var options = DollarSignOptions.Default
            .WithDollarSignSyntax()
            .WithGlobalData(data);

        // Act
        var result = DollarSign.Eval(template, null, options);
        var expected = $"1. {products[0]["Name"]}: ${products[0]["Price"]}, 2. {products[1]["Name"]}: ${products[1]["Price"]}";

        result.Should().Be(expected);
    }
}