using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests.Integration.TestUtilities;

/// <summary>
/// Simple shared test panel used by multiple integration tests.
/// Keeps the test helper in a single place to avoid duplicate private types.
/// </summary>
internal sealed class TestPanel : UserControl
{
    public TestPanel()
    {
        Name = "TestPanel";
    }
}
