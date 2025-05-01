using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class MethodInvocationTests : TestBase
{
    public MethodInvocationTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task MethodInvocationWithDollarSignSyntax()
    {
        // Arrange
        var data = new MyClass { Name = "John" };

        // Test with SupportDollarSignSyntax enabled
        var optionsWithDollarSign = new DollarSignOption
        {
            SupportDollarSignSyntax = true,
            EnableDebugLogging = true // Enable for debugging
        };
        var expectedWithDollarSign = "hello, John";
        var actualWithDollarSign = await DollarSign.EvalAsync("${Hello()}", data, optionsWithDollarSign);

        // Test with SupportDollarSignSyntax disabled
        var optionsWithoutDollarSign = new DollarSignOption
        {
            SupportDollarSignSyntax = false,
            EnableDebugLogging = true
        };
        var expectedWithoutDollarSign = "${Hello()}";
        var actualWithoutDollarSign = await DollarSign.EvalAsync("${Hello()}", data, optionsWithoutDollarSign);

        // Assert
        Assert.Equal(expectedWithDollarSign, actualWithDollarSign);
        Assert.Equal(expectedWithoutDollarSign, actualWithoutDollarSign);
    }

    [Fact]
    public async Task MethodInvocationWithNonExistentMethod()
    {
        // Arrange
        var data = new MyClass { Name = "John" };
        var options = new DollarSignOption
        {
            SupportDollarSignSyntax = true,
            ThrowOnMissingParameter = true,
            EnableDebugLogging = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<DollarSignEngineException>(() =>
            DollarSign.EvalAsync("${NonExistentMethod()}", data, options));
    }
}

public class MyClass
{
    public string Name { get; set; } = string.Empty;

    public string Hello()
    {
        return $"hello, {Name}";
    }
}