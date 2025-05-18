using DollarSignEngine.Internals;
using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class ExceptionHandlingTests : TestBase
{
    public ExceptionHandlingTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task Should_Return_Empty_String_When_ThrowOnError_Is_False()
    {
        // Arrange
        var expression = "{nonExistentVariable + 5}"; // Invalid expression
        var variables = new { };
        var options = new DollarSignOptions { ThrowOnError = false }; // Will not throw

        // Act
        var result = await DollarSign.EvalAsync(expression, variables, options);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Should_Include_Source_Code_In_CompilationException()
    {
        // Arrange
        string sourceCode = "invalid C# code";

        // Act
        var exception = new CompilationException("Compilation failed", sourceCode);

        // Assert
        exception.SourceCode.Should().Be(sourceCode);
        exception.Message.Should().Be("Compilation failed");
    }

    [Fact]
    public void Should_Create_DollarSignEngineException_With_Message_And_Inner_Exception()
    {
        // Arrange
        string message = "Test exception message";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new DollarSignEngineException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void Should_Create_DollarSignEngineException_With_Message_Only()
    {
        // Arrange
        string message = "Test exception message";

        // Act
        var exception = new DollarSignEngineException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public async Task Should_Handle_Null_Expression_Gracefully()
    {
        // Arrange
        string? expression = null;

        // Act
        var result = await DollarSign.EvalAsync(expression!, new { });

        // Assert
        result.Should().BeEmpty();
    }
}