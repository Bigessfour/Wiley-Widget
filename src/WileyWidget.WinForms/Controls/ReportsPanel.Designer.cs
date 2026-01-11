using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Controls;
using FastReport;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    partial class ReportsPanel
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
            this.Name = "ReportsPanel";
            this.AccessibleName = "Reports Panel";
            this.Size = new System.Drawing.Size(1400, 900);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AccessibleDescription = "View, generate, and export reports with FastReport";
        }

        #endregion

        // Field declarations
        private SfDataGrid _parametersGrid;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private GradientPanelExt _reportViewerContainer;
        private GradientPanelExt _toolbarPanel;
        private GradientPanelExt _parametersPanel;
        private Syncfusion.WinForms.ListView.SfComboBox _reportSelector;
        private Syncfusion.WinForms.Controls.SfButton _loadReportButton;
        private Syncfusion.WinForms.Controls.SfButton _exportPdfButton;
        private Syncfusion.WinForms.Controls.SfButton _exportExcelButton;
        private Syncfusion.WinForms.Controls.SfButton _printButton;
        private Syncfusion.WinForms.Controls.SfButton _parametersButton;
        private Syncfusion.WinForms.Controls.SfButton _applyParametersButton;
        private Syncfusion.WinForms.Controls.SfButton _closeParametersButton;
        private System.Windows.Forms.SplitContainer _mainSplitContainer;
        private System.Windows.Forms.SplitContainer _parametersSplitContainer;
        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabel;
    }
}
