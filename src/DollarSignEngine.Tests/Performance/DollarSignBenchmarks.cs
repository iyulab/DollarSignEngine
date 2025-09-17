using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DollarSignEngine;

namespace DollarSignEngine.Tests.Performance;

/// <summary>
/// Performance benchmarks for DollarSignEngine using BenchmarkDotNet.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[HtmlExporter]
[MarkdownExporter]
public class DollarSignBenchmarks
{
    private DollarSignOptions _defaultOptions = null!;
    private DollarSignOptions _cachedOptions = null!;
    private DollarSignOptions _noCacheOptions = null!;
    private object _testData = null!;
    private Dictionary<string, object?> _testDataDict = null!;

    [GlobalSetup]
    public void Setup()
    {
        _defaultOptions = DollarSignOptions.Default;
        _cachedOptions = new DollarSignOptions { UseCache = true, CacheSize = 1000 };
        _noCacheOptions = new DollarSignOptions { UseCache = false };

        _testData = new
        {
            Name = "John Doe",
            Age = 30,
            Score = 95.5,
            IsActive = true,
            JoinDate = new DateTime(2020, 1, 15),
            Tags = new[] { "premium", "verified", "active" }
        };

        _testDataDict = new Dictionary<string, object?>
        {
            ["Name"] = "John Doe",
            ["Age"] = 30,
            ["Score"] = 95.5,
            ["IsActive"] = true,
            ["JoinDate"] = new DateTime(2020, 1, 15),
            ["Tags"] = new[] { "premium", "verified", "active" }
        };
    }

    [Benchmark(Baseline = true)]
    public string SimpleInterpolation()
    {
        return DollarSign.Eval("Hello, {Name}! You are {Age} years old.", _testData);
    }

    [Benchmark]
    public string SimpleInterpolationWithCache()
    {
        return DollarSign.Eval("Hello, {Name}! You are {Age} years old.", _testData, _cachedOptions);
    }

    [Benchmark]
    public string SimpleInterpolationWithoutCache()
    {
        return DollarSign.Eval("Hello, {Name}! You are {Age} years old.", _testData, _noCacheOptions);
    }

    [Benchmark]
    public string ComplexInterpolation()
    {
        return DollarSign.Eval(
            "User: {Name} ({Age}), Score: {Score:F2}, Status: {(IsActive ? \"Active\" : \"Inactive\")}, Member since: {JoinDate:yyyy-MM-dd}",
            _testData);
    }

    [Benchmark]
    public string FormatSpecifiers()
    {
        return DollarSign.Eval("Score: {Score:C2}, Date: {JoinDate:yyyy-MM-dd HH:mm}, Percentage: {Score:P1}", _testData);
    }

    [Benchmark]
    public string ConditionalLogic()
    {
        return DollarSign.Eval("Status: {(Score >= 90 ? \"Excellent\" : Score >= 70 ? \"Good\" : \"Needs Improvement\")}", _testData);
    }

    [Benchmark]
    public string ArrayAccess()
    {
        return DollarSign.Eval("Primary tag: {Tags[0]}, Secondary tag: {Tags[1]}", _testData);
    }

    [Benchmark]
    public string DictionaryBasedEvaluation()
    {
        return DollarSign.Eval("Hello, {Name}! You are {Age} years old.", _testDataDict);
    }

    [Benchmark]
    public string MethodCalls()
    {
        var data = new { Text = "  Hello World  ", Numbers = new List<int> { 1, 2, 3, 4, 5 } };
        return DollarSign.Eval("Trimmed: '{Text.Trim()}', Count: {Numbers.Count}, Sum: {Numbers.Sum()}", data);
    }

    [Benchmark]
    public async Task<string> AsyncEvaluation()
    {
        return await DollarSign.EvalAsync("Hello, {Name}! You are {Age} years old.", _testData);
    }

    [Benchmark]
    public async Task<Dictionary<string, string>> ParallelEvaluation()
    {
        var templates = new Dictionary<string, string>
        {
            ["greeting"] = "Hello, {Name}!",
            ["age"] = "Age: {Age}",
            ["score"] = "Score: {Score:F2}",
            ["status"] = "Status: {(IsActive ? \"Active\" : \"Inactive\")}",
            ["date"] = "Joined: {JoinDate:yyyy-MM-dd}"
        };

        return await DollarSign.EvalManyAsync(templates, _testData);
    }

    [Benchmark]
    public string RepeatedSameTemplate()
    {
        var template = "Hello, {Name}! You are {Age} years old.";
        var results = new string[100];

        for (int i = 0; i < 100; i++)
        {
            results[i] = DollarSign.Eval(template, _testData);
        }

        return string.Join(", ", results.Take(3)); // Return first 3 to avoid huge strings
    }

    [Benchmark]
    public string SecurityValidation()
    {
        var strictOptions = DollarSignOptions.Default.WithStrictSecurity();
        return DollarSign.Eval("Hello, {Name}! Score: {Score}", _testData, strictOptions);
    }

    [Benchmark]
    public string LargeTemplateProcessing()
    {
        var largeTemplate = string.Join(" ", Enumerable.Repeat("Hello {Name}, your score is {Score:F2}.", 50));
        return DollarSign.Eval(largeTemplate, _testData);
    }

    [Benchmark]
    public string ErrorHandling()
    {
        var options = new DollarSignOptions { ThrowOnError = false };
        return DollarSign.Eval("Hello {UndefinedVariable}!", _testData, options);
    }
}