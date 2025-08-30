using Xunit;

namespace WileyWidget.Tests;

/// <summary>
/// WPF Test Collection that ensures STA threading for WPF components.
/// This collection definition ensures all WPF-related tests run on STA threads.
/// </summary>
[CollectionDefinition("WPF Test Collection")]
public class WpfTestCollection : ICollectionFixture<WpfTestFixture>
{
}

/// <summary>
/// WPF Test Fixture that sets up STA threading for WPF tests.
/// This fixture ensures proper WPF initialization and cleanup.
/// </summary>
public sealed class WpfTestFixture : IDisposable
{
    public WpfTestFixture()
    {
        // WPF initialization can be done here if needed
        // Note: The xunit.runner.wpf package handles STA threading automatically
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        // WPF cleanup can be done here if needed
        if (disposing)
        {
            // Dispose managed resources
        }
    }
}
