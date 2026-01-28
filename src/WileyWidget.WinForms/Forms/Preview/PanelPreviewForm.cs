using System;
using System.Windows.Forms;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Controls;  // adjust namespace

namespace WileyWidget.WinForms.Forms.Preview
{
    public partial class PanelPreviewForm : Form
    {
        private UserControl? _currentPanel;

        public PanelPreviewForm()
        {
            this.Text = "Panel Preview - WarRoomPanel";  // change for other panels
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Force a theme (super useful for polish)
            // SfSkinManager.SetVisualStyle(this, "Office2019Colorful");  // or Office2019Dark

            // Option 1: Simple (no DI) — great for visual tweaks
            // _currentPanel = new WarRoomPanel();  // ← swap with BudgetPanel, etc.

            // Option 2: With DI (if you expose Program.Services)
            var sp = Program.Services ?? throw new InvalidOperationException("Services not available");
            _currentPanel = sp.GetRequiredService<WarRoomPanel>();

            _currentPanel.Dock = DockStyle.Fill;
            this.Controls.Add(_currentPanel);

            // Optional: fake data / init for preview
            if (_currentPanel is WarRoomPanel wp && wp.ViewModel != null)
            {
                // Call a sample method if you add one
                // wp.ViewModel?.LoadSampleData();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _currentPanel?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
