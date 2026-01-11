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
    partial class AccountsPanel
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
            
            // Initialize ToolTip first
            _toolTip = new System.Windows.Forms.ToolTip(this.components);
            _toolTip.AutoPopDelay = 10000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 100;
            _toolTip.ShowAlways = true;

            // Panel properties
            this.Name = "AccountsPanel";
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Size = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1200f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f));
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.AccessibleName = "Municipal Accounts";
            this.AccessibleDescription = "Panel for managing municipal accounts with filtering, sorting, and CRUD operations";
        }

        #endregion

        // Field declarations
        private System.Windows.Forms.ToolTip _toolTip;
        private SfDataGrid gridAccounts;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private Syncfusion.WinForms.ListView.SfComboBox comboFund;
        private Syncfusion.WinForms.ListView.SfComboBox comboAccountType;
        private Syncfusion.WinForms.Controls.SfButton btnRefresh;
        private Syncfusion.WinForms.Controls.SfButton btnAdd;
        private Syncfusion.WinForms.Controls.SfButton btnEdit;
        private Syncfusion.WinForms.Controls.SfButton btnDelete;
        private Syncfusion.WinForms.Controls.SfButton btnExportExcel;
        private Syncfusion.WinForms.Controls.SfButton btnExportPdf;
        private GradientPanelExt topPanel;
        private GradientPanelExt summaryPanel;
        private System.Windows.Forms.Label lblTotalBalance;
        private System.Windows.Forms.Label lblAccountCount;
        private System.Windows.Forms.ErrorProvider errorProvider;
        private System.Windows.Forms.BindingSource accountsBindingSource;
    }
}
