using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    partial class QuickBooksPanel
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
            this.Name = "QuickBooksPanel";
            this.Size = new System.Drawing.Size(1400, 900);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(750f));
            this.Padding = new System.Windows.Forms.Padding(8);
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AccessibleName = "QuickBooks Integration";
            this.AccessibleDescription = "QuickBooks Online integration with sync history and connection management";
        }

        #endregion

        // Field declarations
        private SfDataGrid _syncHistoryGrid;
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        private GradientPanelExt _connectionPanel;
        private GradientPanelExt _operationsPanel;
        private GradientPanelExt _summaryPanel;
        private GradientPanelExt _historyPanel;
        private System.Windows.Forms.Label _connectionStatusLabel;
        private System.Windows.Forms.Label _companyNameLabel;
        private System.Windows.Forms.Label _lastSyncLabel;
        private Syncfusion.WinForms.Controls.SfButton _connectButton;
        private Syncfusion.WinForms.Controls.SfButton _disconnectButton;
        private Syncfusion.WinForms.Controls.SfButton _testConnectionButton;
        private Syncfusion.WinForms.Controls.SfButton _syncDataButton;
        private Syncfusion.WinForms.Controls.SfButton _importAccountsButton;
        private Syncfusion.WinForms.Controls.SfButton _refreshHistoryButton;
        private Syncfusion.WinForms.Controls.SfButton _clearHistoryButton;
        private Syncfusion.WinForms.Controls.SfButton _exportHistoryButton;
        private ProgressBarAdv _syncProgressBar;
        private System.Windows.Forms.SplitContainer _mainSplitContainer;
    }
}
