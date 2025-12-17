using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.McpServer.Helpers;

/// <summary>
/// Factory for creating mock objects needed for testing.
/// </summary>
public static class MockFactory
{
    /// <summary>
    /// Creates a mock MainForm for isolated form testing.
    /// </summary>
    public static MockMainForm CreateMockMainForm(bool enableMdi = false)
    {
        var mockMainForm = new MockMainForm();

        if (enableMdi)
        {
            mockMainForm.EnableMdiMode();
        }

        return mockMainForm;
    }
}

/// <summary>
/// Mock MainForm for testing child forms in isolation.
/// </summary>
public class MockMainForm : System.Windows.Forms.Form
{
    public bool UseMdiMode { get; private set; }
    public bool UseTabbedMdi { get; private set; }

    public MockMainForm()
    {
        UseMdiMode = false;
        UseTabbedMdi = false;
        IsMdiContainer = false;
    }

    public void EnableMdiMode()
    {
        UseMdiMode = true;
        IsMdiContainer = true;
    }

    public void RegisterAsDockingMDIChild(System.Windows.Forms.Control control)
    {
        // No-op for tests
    }
}
