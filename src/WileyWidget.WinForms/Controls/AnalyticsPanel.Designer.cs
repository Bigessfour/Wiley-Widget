using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    partial class AnalyticsPanel
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            
            // Panel properties
            this.Name = "AnalyticsPanel";
            this.AccessibleName = "Budget Analytics";
            this.Size = new System.Drawing.Size(1400, 900);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.AccessibleDescription = "Advanced analytics with scenario modeling and forecasting";
        }

        #endregion

        // Field declarations
        private SfDataGrid _metricsGrid;
        private SfDataGrid _variancesGrid;
        private ChartControl _trendsChart;
        private ChartControl _forecastChart;
        private SfListView _insightsListBox;
        private SfListView _recommendationsListBox;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private Syncfusion.WinForms.Controls.SfButton _performAnalysisButton;
        private Syncfusion.WinForms.Controls.SfButton _runScenarioButton;
        private Syncfusion.WinForms.Controls.SfButton _generateForecastButton;
        private Syncfusion.WinForms.Controls.SfButton _refreshButton;
        private GradientPanelExt _scenarioPanel;
        private GradientPanelExt _resultsPanel;
        private GradientPanelExt _chartsPanel;
        private GradientPanelExt _buttonPanel;
        private System.Windows.Forms.SplitContainer _mainSplitContainer;
        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ErrorProvider _errorProvider;
    }
}
