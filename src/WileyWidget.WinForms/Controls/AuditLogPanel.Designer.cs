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
    partial class AuditLogPanel
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
            this.Name = "AuditLogPanel";
            this.Size = new System.Drawing.Size(1200, 800);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AccessibleName = "Audit Log Panel";
            this.AccessibleDescription = "View and analyze audit log entries with filtering and export";
        }

        #endregion

        // Field declarations
        private SfDataGrid _auditGrid;
        private ChartControl _chartControl;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private LoadingOverlay _chartLoadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private GradientPanelExt _filterPanel;
        private GradientPanelExt _chartHostPanel;
        private Syncfusion.WinForms.Controls.SfButton _btnRefresh;
        private Syncfusion.WinForms.Controls.SfButton _btnExportCsv;
        private Syncfusion.WinForms.Controls.SfButton _btnUpdateChart;
        private CheckBoxAdv _chkAutoRefresh;
        private SfDateTimeEdit _dtpStartDate;
        private SfDateTimeEdit _dtpEndDate;
        private Syncfusion.WinForms.ListView.SfComboBox _cmbActionType;
        private Syncfusion.WinForms.ListView.SfComboBox _cmbUser;
        private Syncfusion.WinForms.ListView.SfComboBox _cmbChartGroupBy;
        private System.Windows.Forms.Label _lblChartSummary;
        private System.Windows.Forms.SplitContainer _mainSplit;
        private System.Windows.Forms.StatusStrip _statusStrip;
    }
}
