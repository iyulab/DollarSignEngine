using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class VariableResolverTests : TestBase
{
    public VariableResolverTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task BasicVariableResolver()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "username")
                    return "Alice";
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {username}!", null, options);

        // Assert
        Assert.Equal("Hello, Alice!", result);
    }

    [Fact]
    public async Task VariableResolverWithParameter()
    {
        // Arrange
        var parameters = new { role = "Admin" };
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "greeting")
                    return "Welcome";
                if (expression == "role" && parameter != null)
                    return ((dynamic)parameter).role;
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("{greeting}, {role}!", parameters, options);

        // Assert
        Assert.Equal("Welcome, Admin!", result);
    }

    [Fact]
    public async Task VariableResolverFallbackToScriptEvaluation()
    {
        // Arrange
        var parameters = new { name = "Bob" };
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                // Only resolve specific variables, let others fall back
                if (expression == "greeting")
                    return "Hi";
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("{greeting}, {name}!", parameters, options);

        // Assert
        Assert.Equal("Hi, Bob!", result);
    }

    [Fact]
    public async Task VariableResolverWithComplexExpression()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "currentDate")
                    return DateTime.Now.ToString("yyyy-MM-dd");
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("Date: {currentDate}", null, options);
        var expected = $"Date: {DateTime.Now:yyyy-MM-dd}";

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task VariableResolverWithCollection()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "numbers")
                    return new List<int> { 1, 2, 3, 4, 5 };
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("Sum: {numbers.Sum()}", null, options);

        // Assert
        Assert.Equal("Sum: 15", result);
    }

    [Fact]
    public async Task VariableResolverThrowsWhenStrictMode()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                throw new Exception("Resolver failed");
            },
            StrictParameterAccess = true,
            PreferCallbackResolution = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<DollarSignEngineException>(() =>
            DollarSign.EvalAsync("Value: {test}", null, options));
    }

    [Fact]
    public async Task VariableResolverWithFormatSpecifier()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "price")
                    return 123.456;
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("Price: {price:C2}", null, options);

        // Assert
        Assert.Equal("Price: $123.46", result);
    }

    [Fact]
    public async Task VariableResolverWithDollarSignSyntax()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "user")
                    return "Charlie";
                return null;
            },
            SupportDollarSignSyntax = true,
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("User: ${user}, Raw: {user}", null, options);

        // Assert
        Assert.Equal("User: Charlie, Raw: {user}", result);
    }

    [Fact]
    public async Task VariableResolverWithoutPreferCallbackResolution()
    {
        // Arrange
        var parameters = new { name = "David" };
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                // This should be ignored since PreferCallbackResolution is false
                if (expression == "name")
                    return "Ignored";
                return null;
            },
            PreferCallbackResolution = false
        };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}!", parameters, options);

        // Assert
        Assert.Equal("Hello, David!", result);
    }

    [Fact]
    public async Task VariableResolverWithNullParameter()
    {
        // Arrange
        var options = new DollarSignOption
        {
            VariableResolver = (expression, parameter) =>
            {
                if (expression == "value")
                    return "TestValue";
                return null;
            },
            PreferCallbackResolution = true
        };

        // Act
        var result = await DollarSign.EvalAsync("Value: {value}", null, options);

        // Assert
        Assert.Equal("Value: TestValue", result);
    }
}