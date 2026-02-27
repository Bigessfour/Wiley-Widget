using Syncfusion.WinForms.Controls;
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
            var actionsRow = new TableLayoutPanel();

            this._topPanel.SuspendLayout();
            this._contentPanel.SuspendLayout();
            this.SuspendLayout();

            this._topPanel.Dock = DockStyle.Top;
            this._topPanel.Height = LayoutTokens.Dp(128);
            this._topPanel.Padding = new Padding(LayoutTokens.PanelPadding, LayoutTokens.PanelPadding, LayoutTokens.PanelPadding, 0);

            this._panelHeader.Dock = DockStyle.Top;
            this._panelHeader.Title = "War Room";

            actionsRow.Dock = DockStyle.Top;
            actionsRow.Height = LayoutTokens.Dp(48);
            actionsRow.ColumnCount = 3;
            actionsRow.RowCount = 1;
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this._scenarioInput.Dock = DockStyle.Fill;
            this._scenarioInput.Height = LayoutTokens.Dp(LayoutTokens.StandardControlHeight);
            this._scenarioInput.Margin = new Padding(0, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin);

            this._btnRunScenario.Text = "Run Scenario";
            this._btnRunScenario.AutoSize = true;
            this._btnRunScenario.Margin = new Padding(0, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin);

            this._btnExportForecast.Text = "Export Forecast";
            this._btnExportForecast.AutoSize = true;
            this._btnExportForecast.Margin = new Padding(0, LayoutTokens.ContentMargin, 0, LayoutTokens.ContentMargin);

            actionsRow.Controls.Add(this._scenarioInput, 0, 0);
            actionsRow.Controls.Add(this._btnRunScenario, 1, 0);
            actionsRow.Controls.Add(this._btnExportForecast, 2, 0);

            this._topPanel.Controls.Add(actionsRow);
            this._topPanel.Controls.Add(this._panelHeader);

            this._contentPanel.Dock = DockStyle.Fill;
            this._contentPanel.Padding = new Padding(LayoutTokens.PanelPadding);

            this._resultsPanel.Dock = DockStyle.Fill;
            this._contentPanel.Controls.Add(this._resultsPanel);

            this.Controls.Add(this._contentPanel);
            this.Controls.Add(this._topPanel);

            this.Name = "WarRoomPanel";
            this.Size = new System.Drawing.Size(1100, 760);
            this.MinimumSize = new System.Drawing.Size(900, 650);

            this._topPanel.ResumeLayout(false);
            this._contentPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
