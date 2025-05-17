using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class TernaryOperatorTests : TestBase
{
    public TernaryOperatorTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task SimpleTernaryOperatorTest()
    {
        // Assert
        var expected = $"{(true ? "TRUE" : "FALSE")}";
        // Act
        var result = await DollarSign.EvalAsync("{(true ? \"TRUE\" : \"FALSE\")}");
        result.Should().Be(expected);
    }
}