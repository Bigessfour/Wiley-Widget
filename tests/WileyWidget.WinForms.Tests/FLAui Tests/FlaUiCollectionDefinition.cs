using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    /// <summary>
    /// Declares the single xUnit collection that serialises all FlaUI tests.
    /// Without this, each test CLASS would be its own collection and xUnit would
    /// launch them in parallel — spawning multiple WileyWidget.WinForms.exe instances
    /// simultaneously, which causes every WaitForMainWindow call to time out.
    /// </summary>
    [CollectionDefinition("FlaUI Tests")]
    public class FlaUiCollectionDefinition { }
}
