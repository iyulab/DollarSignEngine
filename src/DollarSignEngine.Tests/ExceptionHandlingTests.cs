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
    public async Task Should_Throw_CompilationException_For_Invalid_Expression()
    {
        // Arrange
        var expression = "{nonExistentVariable + 5}"; // Invalid expression with math operation
        var variables = new { };
        var options = new DollarSignOptions { ThrowOnError = true };

        // Act & Assert
        await Assert.ThrowsAsync<CompilationException>(async () =>
            await DollarSign.EvalAsync(expression, variables, options));
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
    public async Task Should_Throw_DollarSignEngineException_For_Missing_Variable()
    {
        // Arrange
        string expression = "Hello {missingVariable}!";
        var variables = new { existingVariable = "world" }; // missingVariable is not provided
        var options = new DollarSignOptions
        {
            ThrowOnError = true,
            VariableResolver = name =>
            {
                if (name == "existingVariable")
                    return "world";

                throw new DollarSignEngineException($"Variable '{name}' not found");
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<DollarSignEngineException>(async () =>
            await DollarSign.EvalAsync(expression, variables, options));
    }

    [Fact]
    public async Task Should_Throw_DollarSignEngineException_With_Inner_Exception()
    {
        // Arrange
        string expression = "Hello {throwingVariable}!";
        var options = new DollarSignOptions
        {
            ThrowOnError = true,
            VariableResolver = name =>
            {
                if (name == "throwingVariable")
                    throw new InvalidOperationException("Something went wrong");

                return null;
            }
        };

        // Act
        var exception = await Assert.ThrowsAsync<DollarSignEngineException>(async () =>
            await DollarSign.EvalAsync(expression, new { }, options));

        // Assert
        exception.InnerException.Should().NotBeNull();
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("Error executing expression");
    }

    [Fact]
    public async Task Should_Catch_And_Wrap_Various_Exceptions_In_DollarSignEngineException()
    {
        // Arrange
        string expression = "Hello {errorVariable}!";
        var options = new DollarSignOptions
        {
            ThrowOnError = true,
            VariableResolver = name =>
            {
                throw new System.IO.FileNotFoundException("Simulated file not found");
            }
        };

        // Act
        var exception = await Assert.ThrowsAsync<DollarSignEngineException>(async () =>
            await DollarSign.EvalAsync(expression, new { }, options));

        // Assert
        exception.InnerException.Should().BeOfType<System.IO.FileNotFoundException>();
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
    public async Task Should_Throw_When_ExpressionHas_Syntax_Errors()
    {
        // Arrange
        string expression = "{unclosed"; // Missing closing brace
        var options = new DollarSignOptions { ThrowOnError = true };

        // Act & Assert
        await Assert.ThrowsAsync<CompilationException>(async () =>
            await DollarSign.EvalAsync(expression, new { }, options));
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