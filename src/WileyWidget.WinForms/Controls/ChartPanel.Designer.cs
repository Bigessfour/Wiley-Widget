using Syncfusion.WinForms.Core;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class ChartPanel
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
            this.Name = "ChartPanel";
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Size = new System.Drawing.Size(1000, 700);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = false;
            this.Padding = new System.Windows.Forms.Padding(0);
            this.AccessibleName = "Budget Analytics";
            this.AccessibleDescription = "Panel for visualizing budget data with charts and analytics";
        }

        #endregion

        // Field declarations
        private ChartControl _chartControl;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private Syncfusion.WinForms.ListView.SfComboBox _comboDepartmentFilter;
        private Syncfusion.WinForms.Controls.SfButton _btnRefresh;
        private Syncfusion.WinForms.Controls.SfButton _btnExportPng;
        private Syncfusion.WinForms.Controls.SfButton _btnExportPdf;
        private GradientPanelExt _topPanel;
        private GradientPanelExt _summaryPanel;
        private System.Windows.Forms.Label _lblTotalBudget;
        private System.Windows.Forms.Label _lblTotalActual;
        private System.Windows.Forms.Label _lblTotalVariance;
        private System.Windows.Forms.Label _lblVariancePercent;
        private System.Windows.Forms.ErrorProvider _errorProvider;
    }
}
