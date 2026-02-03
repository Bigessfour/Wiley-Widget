namespace WileyWidget.WinForms.Controls
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
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._topPanel = new WileyWidget.WinForms.Controls.GradientPanelExt();
            this._panelHeader = new WileyWidget.WinForms.Controls.PanelHeader();
            this._scenarioInput = new System.Windows.Forms.TextBox();
            this._btnRunScenario = new Syncfusion.WinForms.Controls.SfButton();
            this._lblInputError = new System.Windows.Forms.Label();
            this._lblVoiceHint = new System.Windows.Forms.Label();
            this._lblStatus = new System.Windows.Forms.Label();
            this._contentPanel = new System.Windows.Forms.Panel();
            this._resultsPanel = new System.Windows.Forms.Panel();
            this._lblNoResults = new System.Windows.Forms.Label();
            this._loadingOverlay = new WileyWidget.WinForms.Controls.LoadingOverlay();
            this._lblRateIncreaseHeadline = new System.Windows.Forms.Label();
            this._lblRateIncreaseValue = new System.Windows.Forms.Label();
            this._riskGauge = new Syncfusion.Windows.Forms.Gauge.RadialGauge();
            this._revenueChart = new Syncfusion.Windows.Forms.Chart.ChartControl();
            this._departmentChart = new Syncfusion.Windows.Forms.Chart.ChartControl();
            this._projectionsGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid();
            this._departmentImpactGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid();
            this._topPanel.SuspendLayout();
            this._contentPanel.SuspendLayout();
            this._resultsPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // _topPanel
            //
            this._topPanel.Controls.Add(this._panelHeader);
            this._topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._topPanel.MinimumSize = new System.Drawing.Size(0, 160);
            this._topPanel.Name = "WarRoomTopPanel";
            this._topPanel.Size = new System.Drawing.Size(800, 160);
            this._topPanel.TabIndex = 0;
            //
            // _panelHeader
            //
            this._panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this._panelHeader.Name = "WarRoomPanelHeader";
            this._panelHeader.Size = new System.Drawing.Size(800, 40);
            this._panelHeader.TabIndex = 0;
            //
            // _scenarioInput
            //
            this._scenarioInput.Location = new System.Drawing.Point(10, 50);
            this._scenarioInput.Multiline = false;
            this._scenarioInput.Name = "ScenarioInput";
            this._scenarioInput.Size = new System.Drawing.Size(300, 23);
            this._scenarioInput.TabIndex = 1;
            this._scenarioInput.Text = "Raise water rates 12% and inflation is 4% for 5 years";
            //
            // _btnRunScenario
            //
            this._btnRunScenario.Location = new System.Drawing.Point(320, 50);
            this._btnRunScenario.Name = "RunScenarioButton";
            this._btnRunScenario.Size = new System.Drawing.Size(120, 28);
            this._btnRunScenario.TabIndex = 2;
            this._btnRunScenario.Text = "Run Scenario";
            //
            // _lblInputError
            //
            this._lblInputError.AutoSize = true;
            this._lblInputError.ForeColor = System.Drawing.Color.Red;
            this._lblInputError.Location = new System.Drawing.Point(10, 80);
            this._lblInputError.Name = "InputErrorLabel";
            this._lblInputError.Size = new System.Drawing.Size(0, 15);
            this._lblInputError.TabIndex = 3;
            this._lblInputError.Visible = false;
            //
            // _lblVoiceHint
            //
            this._lblVoiceHint.AutoSize = true;
            this._lblVoiceHint.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            this._lblVoiceHint.Location = new System.Drawing.Point(10, 100);
            this._lblVoiceHint.Name = "VoiceHint";
            this._lblVoiceHint.Size = new System.Drawing.Size(400, 15);
            this._lblVoiceHint.TabIndex = 4;
            this._lblVoiceHint.Text = "💬 Or ask JARVIS aloud using voice input (if available in your installation)";
            //
            // _lblStatus
            //
            this._lblStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblStatus.Location = new System.Drawing.Point(450, 50);
            this._lblStatus.Name = "StatusLabel";
            this._lblStatus.Size = new System.Drawing.Size(340, 100);
            this._lblStatus.TabIndex = 5;
            this._lblStatus.Text = "Ready";
            this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // _contentPanel
            //
            this._contentPanel.Controls.Add(this._resultsPanel);
            this._contentPanel.Controls.Add(this._lblNoResults);
            this._contentPanel.Controls.Add(this._loadingOverlay);
            this._contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contentPanel.Name = "ContentPanel";
            this._contentPanel.Size = new System.Drawing.Size(800, 440);
            this._contentPanel.TabIndex = 1;
            //
            // _resultsPanel
            //
            this._resultsPanel.AutoScroll = true;
            this._resultsPanel.Controls.Add(this._lblRateIncreaseHeadline);
            this._resultsPanel.Controls.Add(this._lblRateIncreaseValue);
            this._resultsPanel.Controls.Add(this._riskGauge);
            this._resultsPanel.Controls.Add(this._revenueChart);
            this._resultsPanel.Controls.Add(this._departmentChart);
            this._resultsPanel.Controls.Add(this._projectionsGrid);
            this._resultsPanel.Controls.Add(this._departmentImpactGrid);
            this._resultsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._resultsPanel.Name = "ResultsPanel";
            this._resultsPanel.Size = new System.Drawing.Size(800, 440);
            this._resultsPanel.TabIndex = 0;
            this._resultsPanel.Visible = false;
            //
            // _lblNoResults
            //
            this._lblNoResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblNoResults.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Italic);
            this._lblNoResults.Location = new System.Drawing.Point(0, 0);
            this._lblNoResults.Name = "NoResultsLabel";
            this._lblNoResults.Size = new System.Drawing.Size(800, 440);
            this._lblNoResults.TabIndex = 1;
            this._lblNoResults.Text = "Run a scenario to see results...";
            this._lblNoResults.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // _loadingOverlay
            //
            this._loadingOverlay.Dock = System.Windows.Forms.DockStyle.Fill;
            this._loadingOverlay.Name = "WarRoomLoadingOverlay";
            this._loadingOverlay.Size = new System.Drawing.Size(800, 440);
            this._loadingOverlay.TabIndex = 2;
            this._loadingOverlay.Visible = false;
            //
            // _lblRateIncreaseHeadline
            //
            this._lblRateIncreaseHeadline.AutoSize = true;
            this._lblRateIncreaseHeadline.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._lblRateIncreaseHeadline.Location = new System.Drawing.Point(10, 10);
            this._lblRateIncreaseHeadline.Name = "RateIncreaseLabel";
            this._lblRateIncreaseHeadline.Size = new System.Drawing.Size(150, 19);
            this._lblRateIncreaseHeadline.TabIndex = 0;
            this._lblRateIncreaseHeadline.Text = "Required Rate Increase:";
            //
            // _lblRateIncreaseValue
            //
            this._lblRateIncreaseValue.Font = new System.Drawing.Font("Segoe UI", 48F, System.Drawing.FontStyle.Bold);
            this._lblRateIncreaseValue.Location = new System.Drawing.Point(10, 35);
            this._lblRateIncreaseValue.Name = "RateIncreaseValue";
            this._lblRateIncreaseValue.Size = new System.Drawing.Size(300, 80);
            this._lblRateIncreaseValue.TabIndex = 1;
            this._lblRateIncreaseValue.Text = "—";
            this._lblRateIncreaseValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _riskGauge
            //
            this._riskGauge.Location = new System.Drawing.Point(400, 10);
            this._riskGauge.MaximumValue = 100F;
            this._riskGauge.MinimumValue = 0F;
            this._riskGauge.Name = "RiskGauge";
            this._riskGauge.Size = new System.Drawing.Size(200, 200);
            this._riskGauge.TabIndex = 2;
            this._riskGauge.Value = 0F;
            this._riskGauge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            //
            // _revenueChart
            //
            this._revenueChart.Location = new System.Drawing.Point(10, 130);
            this._revenueChart.Name = "RevenueChart";
            this._revenueChart.Size = new System.Drawing.Size(380, 150);
            this._revenueChart.TabIndex = 3;
            this._revenueChart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            //
            // _departmentChart
            //
            this._departmentChart.Location = new System.Drawing.Point(400, 130);
            this._departmentChart.Name = "DepartmentChart";
            this._departmentChart.Size = new System.Drawing.Size(380, 150);
            this._departmentChart.TabIndex = 4;
            this._departmentChart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            //
            // _projectionsGrid
            //
            this._projectionsGrid.AllowEditing = false;
            this._projectionsGrid.AllowFiltering = true;
            this._projectionsGrid.AllowSorting = true;
            this._projectionsGrid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
            this._projectionsGrid.Location = new System.Drawing.Point(10, 290);
            this._projectionsGrid.Name = "ProjectionsGrid";
            this._projectionsGrid.RowHeight = 24;
            this._projectionsGrid.ShowRowHeader = false;
            this._projectionsGrid.Size = new System.Drawing.Size(380, 140);
            this._projectionsGrid.TabIndex = 5;
            this._projectionsGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            //
            // _departmentImpactGrid
            //
            this._departmentImpactGrid.AllowEditing = false;
            this._departmentImpactGrid.AllowFiltering = true;
            this._departmentImpactGrid.AllowSorting = true;
            this._departmentImpactGrid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
            this._departmentImpactGrid.Location = new System.Drawing.Point(400, 290);
            this._departmentImpactGrid.Name = "DepartmentImpactGrid";
            this._departmentImpactGrid.RowHeight = 24;
            this._departmentImpactGrid.ShowRowHeader = false;
            this._departmentImpactGrid.Size = new System.Drawing.Size(380, 140);
            this._departmentImpactGrid.TabIndex = 6;
            this._departmentImpactGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            //
            // WarRoomPanel
            //
            this.Controls.Add(this._contentPanel);
            this.Controls.Add(this._topPanel);
            this.Name = "WarRoomPanel";
            this.Size = new System.Drawing.Size(800, 600);
            this._topPanel.ResumeLayout(false);
            this._contentPanel.ResumeLayout(false);
            this._resultsPanel.ResumeLayout(false);
            this._resultsPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        private GradientPanelExt _topPanel;
        private PanelHeader _panelHeader;
        private TextBox _scenarioInput;
        private Syncfusion.WinForms.Controls.SfButton _btnRunScenario;
        private Label _lblInputError;
        private Label _lblVoiceHint;
        private Label _lblStatus;
        private Panel _contentPanel;
        private Panel _resultsPanel;
        private Label _lblNoResults;
        private LoadingOverlay _loadingOverlay;
        private Label _lblRateIncreaseHeadline;
        private Label _lblRateIncreaseValue;
        private Syncfusion.Windows.Forms.Gauge.RadialGauge _riskGauge;
        private Syncfusion.Windows.Forms.Chart.ChartControl _revenueChart;
        private Syncfusion.Windows.Forms.Chart.ChartControl _departmentChart;
        private Syncfusion.WinForms.DataGrid.SfDataGrid _projectionsGrid;
        private Syncfusion.WinForms.DataGrid.SfDataGrid _departmentImpactGrid;
    }
}
