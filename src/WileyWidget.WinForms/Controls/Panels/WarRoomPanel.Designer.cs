using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Utilities;

namespace WileyWidget.WinForms.Controls.Panels
{
    partial class WarRoomPanel
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        // Minimal designer for WarRoomPanel — only core controls
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._topPanel = new Panel();
            this._panelHeader = new PanelHeader();
            this._scenarioInput = new TextBox();
            this._btnRunScenario = new SfButton();
            this._btnExportForecast = new SfButton();
            this._contentPanel = new Panel();
            this._resultsPanel = new Panel();

            // ... keep only these lines ...
            this._topPanel.SuspendLayout();
            this._contentPanel.SuspendLayout();
            this.SuspendLayout();

            // Simple clean layout
            this._topPanel.Dock = DockStyle.Top;
            this._topPanel.Height = LayoutTokens.Dp(140);
            this._topPanel.Padding = new Padding(LayoutTokens.PanelPadding);

            this._scenarioInput.Dock = DockStyle.Top;
            this._scenarioInput.Height = LayoutTokens.Dp(LayoutTokens.StandardControlHeight);
            this._scenarioInput.Margin = new Padding(LayoutTokens.ContentMargin);

            this._btnRunScenario.Dock = DockStyle.Right;
            this._btnExportForecast.Dock = DockStyle.Right;

            this._topPanel.Controls.AddRange(new Control[] { _panelHeader, _scenarioInput, _btnRunScenario, _btnExportForecast });
            this.Controls.Add(_topPanel);
            this.Controls.Add(_contentPanel);

            this.Name = "WarRoomPanel";
            this.Size = new System.Drawing.Size(1000, 700);
            this.MinimumSize = new System.Drawing.Size(600, 420);

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
