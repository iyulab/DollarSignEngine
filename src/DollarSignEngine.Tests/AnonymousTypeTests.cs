using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class AnonymousTypeTests : TestBase
{
    public AnonymousTypeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task Should_Access_Anonymous_Type_Properties_In_Dictionary_Array()
    {
        // Arrange
        var parameters = new Dictionary<string, object>()
        {
            {
                "Items",
                new object[]
                {
                    new { Id = 1, Name = "Item1" },
                    new { Id = 2, Name = "Item2" }
                }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync("${Items[0].Id}. ${Items[0].Name}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Expected: '1. Item1'");
        _output.WriteLine($"Actual: '{result}'");

        var expected = "1. Item1";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Access_Anonymous_Type_Properties_In_Typed_Object_Array()
    {
        // Arrange
        var parameters = new
        {
            Items = new object[]
            {
                new { Id = 1, Name = "Item1" },
                new { Id = 2, Name = "Item2" }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync("${Items[0].Id}. ${Items[0].Name}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Expected: '1. Item1'");
        _output.WriteLine($"Actual: '{result}'");

        var expected = "1. Item1";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Access_Anonymous_Type_Properties_Directly()
    {
        // Arrange
        var item = new { Id = 1, Name = "Item1" };
        var parameters = new { item };

        // Act
        var result = await DollarSign.EvalAsync("${item.Id}. ${item.Name}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Expected: '1. Item1'");
        _output.WriteLine($"Actual: '{result}'");

        var expected = "1. Item1";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Access_Anonymous_Type_Properties_In_Strongly_Typed_Array()
    {
        // Arrange
        var items = new[] { new { Id = 1, Name = "Item1" } };
        var parameters = new { Items = items };

        // Act
        var result = await DollarSign.EvalAsync("${Items[0].Id}. ${Items[0].Name}", parameters,
            new DollarSignOptions()
            {
                SupportDollarSignSyntax = true
            });

        // Assert
        _output.WriteLine($"Expected: '1. Item1'");
        _output.WriteLine($"Actual: '{result}'");

        var expected = "1. Item1";
        result.Should().Be(expected);
    }
}