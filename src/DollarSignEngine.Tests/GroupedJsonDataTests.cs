using FluentAssertions;
using System.Text.Json;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class GroupedJsonDataTests : TestBase
{
    public GroupedJsonDataTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    private List<Dictionary<string, JsonElement>> GetTestJsonData()
    {
        var json = @"[
            { ""Category"": ""Electronics"", ""Name"": ""Phone"", ""Price"": 500 },
            { ""Category"": ""Electronics"", ""Name"": ""Laptop"", ""Price"": 1000 },
            { ""Category"": ""Books"", ""Name"": ""Fiction Novel"", ""Price"": 15 },
            { ""Category"": ""Books"", ""Name"": ""Science Book"", ""Price"": 25 },
            { ""Category"": ""Clothing"", ""Name"": ""T-Shirt"", ""Price"": 20 },
            { ""Category"": ""Electronics"", ""Name"": ""Tablet"", ""Price"": 300 }
        ]";

        return JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
    }

    private object[] GetGroupedProducts()
    {
        var items = GetTestJsonData();
        return items
            .GroupBy(item => item.ContainsKey("Category") ? item["Category"].ToString() : "null")
            .Select(p => new
            {
                Key = p.Key,
                Items = p.ToArray()
            })
            .ToArray(); // Convert to array to avoid LINQ iterator issues
    }

    [Fact]
    public async Task Should_Access_Grouped_Json_Data_First_Item()
    {
        // Arrange
        var groupedProducts = GetGroupedProducts();
        var parameters = new { Products = groupedProducts };

        // Act
        var result = await DollarSign.EvalAsync("${Products[0].Key}: ${Products[0].Items[0][\"Name\"]}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Electronics: Phone");
    }

    [Fact]
    public async Task Should_Access_Grouped_Json_Data_Multiple_Items()
    {
        // Arrange
        var groupedProducts = GetGroupedProducts();
        var parameters = new { Products = groupedProducts };

        // Act
        var result = await DollarSign.EvalAsync("Category: ${Products[0].Key}, Items: ${Products[0].Items[0][\"Name\"]} and ${Products[0].Items[1][\"Name\"]}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Category: Electronics, Items: Phone and Laptop");
    }

    [Fact]
    public async Task Should_Access_Different_Groups_In_Grouped_Data()
    {
        // Arrange
        var groupedProducts = GetGroupedProducts();
        var parameters = new { Products = groupedProducts };

        // Act
        var result = await DollarSign.EvalAsync("${Products[0].Key} vs ${Products[1].Key}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Electronics vs Books");
    }

    [Fact]
    public async Task Should_Access_Json_Element_Properties_With_Brackets()
    {
        // Arrange
        var groupedProducts = GetGroupedProducts();
        var parameters = new { Products = groupedProducts };

        // Act
        var result = await DollarSign.EvalAsync("${Products[0].Items[0][\"Name\"]} costs ${Products[0].Items[0][\"Price\"]}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Phone costs 500");
    }

    [Fact]
    public async Task Should_Handle_Missing_Json_Properties_Gracefully()
    {
        // Arrange
        var items = new List<Dictionary<string, JsonElement>>
        {
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(@"{ ""Name"": ""Item1"" }"),
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(@"{ ""Category"": ""Test"", ""Name"": ""Item2"" }")
        };

        var groupedItems = items
            .GroupBy(item => item.ContainsKey("Category") ? item["Category"].ToString() : "NoCategory")
            .Select(p => new { Key = p.Key, Items = p.ToArray() })
            .ToArray();

        var parameters = new { Products = groupedItems };

        // Act
        var result = await DollarSign.EvalAsync("${Products[0].Key}: ${Products[0].Items[0][\"Name\"]}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("NoCategory: Item1");
    }

    [Fact]
    public async Task Should_Work_With_Regular_Curly_Brace_Syntax()
    {
        // Arrange
        var groupedProducts = GetGroupedProducts();
        var parameters = new { Products = groupedProducts };

        // Act
        var result = await DollarSign.EvalAsync("Category: {Products[0].Key}", parameters);

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Category: Electronics");
    }

    [Fact]
    public async Task Should_Access_Array_Length_Property()
    {
        // Arrange
        var groupedProducts = GetGroupedProducts();
        var parameters = new { Products = groupedProducts };

        // Act
        var result = await DollarSign.EvalAsync("${Products[0].Key} has ${Products[0].Items.Length} items", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Electronics has 3 items");
    }

    [Fact]
    public async Task Should_Work_With_Traditional_Anonymous_Type_Groups()
    {
        // Arrange - Using traditional grouping with concrete types
        var products = new[]
        {
            new { Category = "Electronics", Name = "Phone", Price = 500 },
            new { Category = "Electronics", Name = "Laptop", Price = 1000 },
            new { Category = "Books", Name = "Novel", Price = 15 }
        };

        var grouped = products
            .GroupBy(p => p.Category)
            .Select(g => new { Key = g.Key, Items = g.ToArray() })
            .ToArray();

        var parameters = new { Products = grouped };

        // Act
        var result = await DollarSign.EvalAsync("${Products[0].Key}: ${Products[0].Items[0].Name}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Result: '{result}'");
        result.Should().Be("Electronics: Phone");
    }
}