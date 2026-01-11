using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class BudgetPanel
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
            this.Name = "BudgetPanel";
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Size = new System.Drawing.Size(1400, 900);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.AccessibleName = "Budget Management";
            this.AccessibleDescription = "Panel for managing municipal budgets with analysis and scenario modeling";
        }

        #endregion

        // Field declarations
        private System.Windows.Forms.ToolTip _toolTip;
        private SfDataGrid _budgetGrid;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private Syncfusion.WinForms.ListView.SfComboBox _comboFund;
        private Syncfusion.WinForms.ListView.SfComboBox _comboAccountType;
        private Syncfusion.WinForms.Controls.SfButton _btnRefresh;
        private Syncfusion.WinForms.Controls.SfButton _btnAdd;
        private Syncfusion.WinForms.Controls.SfButton _btnEdit;
        private Syncfusion.WinForms.Controls.SfButton _btnDelete;
        private Syncfusion.WinForms.Controls.SfButton _btnExportExcel;
        private Syncfusion.WinForms.Controls.SfButton _btnExportPdf;
        private GradientPanelExt _topPanel;
        private GradientPanelExt _summaryPanel;
        private System.Windows.Forms.Label _lblTotalBalance;
        private System.Windows.Forms.Label _lblAccountCount;
        private System.Windows.Forms.ErrorProvider _errorProvider;
        private System.Windows.Forms.BindingSource _budgetBindingSource;
    }
}
