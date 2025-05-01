using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class TestOutputHelperExtensions
{
    private static ITestOutputHelper? _testOutputHelper;

    public static void SetOutputHelper(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public static void WriteLine(string message)
    {
        _testOutputHelper?.WriteLine(message);
    }
}
