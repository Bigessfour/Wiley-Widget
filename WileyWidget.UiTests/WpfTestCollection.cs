using Xunit;

namespace WileyWidget.UiTests;

/// <summary>
/// WPF Test Collection that ensures STA threading for WPF UI tests.
/// This collection definition ensures all WPF UI-related tests run on STA threads.
/// </summary>
[CollectionDefinition("WPF Test Collection")]
public class WpfTestCollection : ICollectionFixture<WpfTestFixture>
{
}

/// <summary>
/// WPF Test Fixture that sets up STA threading for WPF UI tests.
/// This fixture ensures proper WPF initialization and cleanup.
/// </summary>
public class WpfTestFixture : IDisposable
{
    public WpfTestFixture()
    {
        // WPF initialization can be done here if needed
        // Note: The xunit.runner.wpf package handles STA threading automatically
    }

    public void Dispose()
    {
        // WPF cleanup can be done here if needed
    }
}
