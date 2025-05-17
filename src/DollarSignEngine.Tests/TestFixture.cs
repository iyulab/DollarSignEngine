namespace DollarSignEngine.Tests;

public class TestFixture : IDisposable
{
    public TestFixture()
    {
        DollarSign.ClearCache();
    }

    public void Dispose()
    {
        DollarSign.ClearCache();
    }
}
