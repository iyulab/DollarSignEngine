using Xunit;
using DollarSignEngine;
using System.Diagnostics;

namespace DollarSignEngine.Tests.Performance;

/// <summary>
/// Quick performance tests for CI/CD pipeline.
/// </summary>
public class QuickPerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task Simple_Expression_Should_Execute_Quickly()
    {
        // Arrange
        var expression = "Hello, {name}!";
        var variables = new { name = "World" };
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            await DollarSign.EvalAsync(expression, variables);
        }
        stopwatch.Stop();

        // Assert - Should complete 1000 evaluations in under 1 second
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Performance regression: took {stopwatch.ElapsedMilliseconds}ms for 1000 evaluations");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Cache_Should_Improve_Performance()
    {
        // Arrange
        var expression = "Hello, {name}! Today is {DateTime.Now:yyyy-MM-dd}";
        var variables = new { name = "Performance Test" };
        var options = new DollarSignOptions { UseCache = true };

        // Warm up
        await DollarSign.EvalAsync(expression, variables, options);

        // Act - Measure cached performance
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            await DollarSign.EvalAsync(expression, variables, options);
        }
        stopwatch.Stop();

        // Assert - Cached evaluations should be very fast
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Cache performance issue: took {stopwatch.ElapsedMilliseconds}ms for 100 cached evaluations");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void GetMetrics_Should_Show_Cache_Effectiveness()
    {
        // Arrange
        DollarSign.ClearCache(); // Start fresh
        var expression = "Performance test: {value}";
        var variables = new { value = 42 };

        // Act - Execute same expression multiple times
        for (int i = 0; i < 10; i++)
        {
            DollarSign.Eval(expression, variables);
        }

        var (totalEvaluations, cacheHits, hitRate) = DollarSign.GetMetrics();

        // Assert - Should show cache effectiveness
        Assert.True(totalEvaluations >= 10, "Should record all evaluations");
        Assert.True(cacheHits > 0, "Should have some cache hits");
        Assert.True(hitRate > 0, "Should have positive hit rate");
    }
}