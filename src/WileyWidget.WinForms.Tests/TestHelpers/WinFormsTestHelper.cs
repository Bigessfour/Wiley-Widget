using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests.TestHelpers
{
    // Small helpers for creating a hidden form or control parent for headless UI testing
    public static class WinFormsTestHelper
    {
        public static Form CreateHiddenForm()
        {
            var f = new Form();
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new System.Drawing.Point(-2000, -2000);
            // Do not call Show(); tests will add controls and not display the form
            return f;
        }
    }
}
