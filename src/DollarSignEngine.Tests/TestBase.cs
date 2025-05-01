using Xunit.Abstractions;

public class TestBase : IDisposable
{
    protected readonly ITestOutputHelper _output;
    private readonly TextWriter _originalConsoleOut;

    public TestBase(ITestOutputHelper output)
    {
        _output = output;
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