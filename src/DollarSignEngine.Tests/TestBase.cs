using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class TestBase : IDisposable
{
    private readonly TextWriter _originalConsoleOut;
    protected readonly ITestOutputHelper _output;

    public TestBase(ITestOutputHelper output)
    {
        _output = output;
        TestOutputHelperExtensions.SetOutputHelper(output);

        // Redirect console output to test output
        _originalConsoleOut = Console.Out;
        Console.SetOut(new TestOutputTextWriter(output));
    }

    public void Dispose()
    {
        Console.SetOut(_originalConsoleOut);
    }

    private class TestOutputTextWriter : TextWriter
    {
        private readonly ITestOutputHelper _output;

        public TestOutputTextWriter(ITestOutputHelper output)
        {
            _output = output;
        }

        public override void WriteLine(string? value)
        {
            _output.WriteLine(value);
        }

        public override void Write(string? value)
        {
            _output.WriteLine(value);
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}