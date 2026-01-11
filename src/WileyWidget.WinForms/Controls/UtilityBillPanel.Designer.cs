using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Drawing;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class UtilityBillPanel
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
            
            // SUSPEND LAYOUT
            this.SuspendLayout();
            
            try
            {
                // ===== DPI CONSTANTS =====
                var standardPadding = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(16f);
                var controlSpacing = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f);
                
                // ===== CORE PANEL PROPERTIES =====
                this.Name = "UtilityBillPanel";
                this.Dock = System.Windows.Forms.DockStyle.Fill;
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
                this.Size = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(900f));
                this.MinimumSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
                this.Padding = new System.Windows.Forms.Padding(standardPadding);
                this.AccessibleName = "Utility Bill Management";
                this.AccessibleDescription = "Panel for managing utility bills, customers, and payment tracking with financial summaries";

                // ===== TOOLTIP INITIALIZATION =====
                this._toolTip = new System.Windows.Forms.ToolTip(this.components);
                this._toolTip.AutoPopDelay = 5000;
                this._toolTip.InitialDelay = 500;
                this._toolTip.ReshowDelay = 200;
                this._toolTip.ShowAlways = true;

                // ===== MAIN LAYOUT TABLE =====
                this._mainLayout = new System.Windows.Forms.TableLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    RowCount = 5,
                    ColumnCount = 1,
                    AutoSize = false,
                    Padding = System.Windows.Forms.Padding.Empty
                };

                // Row 0: Panel Header
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f)));

                // Row 1: Summary Panel (KPI metrics)
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f)));

                // Row 2: Action Buttons
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(45f)));

                // Row 3: Dual Grids (split)
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Percent, 100f));

                // Row 4: Status Bar
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f)));

                this.Controls.Add(this._mainLayout);

                // ===== PANEL HEADER =====
                this._panelHeader = new PanelHeader
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Title = "Utility Bill Management",
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                    AccessibleName = "Panel header",
                    AccessibleDescription = "Panel title with refresh and close actions"
                };
                this._mainLayout.Controls.Add(this._panelHeader, 0, 0);
                SfSkinManager.SetVisualStyle(this._panelHeader, ThemeColors.DefaultTheme);

                // ===== SUMMARY PANEL (KPI METRICS) =====
                this._summaryPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Bill summary metrics",
                    AccessibleDescription = "Shows total outstanding, overdue count, revenue, and bills this month"
                };

                var summaryTable = new System.Windows.Forms.TableLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 1,
                    CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single,
                    Padding = System.Windows.Forms.Padding.Empty
                };

                for (int i = 0; i < 4; i++)
                    summaryTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                        System.Windows.Forms.SizeType.Percent, 25f));

                this._totalOutstandingLabel = new System.Windows.Forms.Label
                {
                    Text = "Total Outstanding: $0.00",
                    Font = new System.Drawing.Font("Segoe UI", 
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    AutoSize = false
                };

                this._overdueCountLabel = new System.Windows.Forms.Label
                {
                    Text = "Overdue Bills: 0",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    AutoSize = false
                };

                this._totalRevenueLabel = new System.Windows.Forms.Label
                {
                    Text = "Total Revenue: $0.00",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    AutoSize = false
                };

                this._billsThisMonthLabel = new System.Windows.Forms.Label
                {
                    Text = "Bills This Month: 0",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    AutoSize = false
                };

                summaryTable.Controls.Add(this._totalOutstandingLabel, 0, 0);
                summaryTable.Controls.Add(this._overdueCountLabel, 1, 0);
                summaryTable.Controls.Add(this._totalRevenueLabel, 2, 0);
                summaryTable.Controls.Add(this._billsThisMonthLabel, 3, 0);

                this._summaryPanel.Controls.Add(summaryTable);
                this._mainLayout.Controls.Add(this._summaryPanel, 0, 1);
                SfSkinManager.SetVisualStyle(this._summaryPanel, ThemeColors.DefaultTheme);

                // ===== ACTION BUTTONS PANEL =====
                this._buttonPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Action buttons",
                    AccessibleDescription = "Buttons for bill management operations"
                };

                var buttonFlow = new System.Windows.Forms.FlowLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true,
                    AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
                };

                var buttonSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32f));

                var createButton = new System.Func<string, int, string, Syncfusion.WinForms.Controls.SfButton>((text, tabIdx, tooltip) =>
                {
                    var btn = new Syncfusion.WinForms.Controls.SfButton
                    {
                        Text = text,
                        Size = buttonSize,
                        TabIndex = tabIdx,
                        AutoSize = false,
                        Margin = new System.Windows.Forms.Padding(controlSpacing / 2, 0, 0, 0),
                        AccessibleName = text.Replace("&", "")
                    };
                    this._toolTip.SetToolTip(btn, tooltip);
                    return btn;
                });

                this._createBillButton = createButton("&Create", 1, "Create a new utility bill");
                this._saveBillButton = createButton("&Save", 2, "Save bill changes");
                this._deleteBillButton = createButton("&Delete", 3, "Delete selected bill");
                this._markPaidButton = createButton("Mark &Paid", 4, "Record payment for bill");
                this._generateReportButton = createButton("&Report", 5, "Generate bill report");
                this._exportExcelButton = createButton("E&xport", 6, "Export to Excel");
                this._refreshButton = createButton("Re&fresh", 7, "Reload bill data");

                buttonFlow.Controls.Add(this._createBillButton);
                buttonFlow.Controls.Add(this._saveBillButton);
                buttonFlow.Controls.Add(this._deleteBillButton);
                buttonFlow.Controls.Add(this._markPaidButton);
                buttonFlow.Controls.Add(this._generateReportButton);
                buttonFlow.Controls.Add(this._exportExcelButton);
                buttonFlow.Controls.Add(this._refreshButton);

                this._buttonPanel.Controls.Add(buttonFlow);
                this._mainLayout.Controls.Add(this._buttonPanel, 0, 2);
                SfSkinManager.SetVisualStyle(this._buttonPanel, ThemeColors.DefaultTheme);

                // ===== DUAL GRID SPLIT CONTAINER =====
                this._mainSplitContainer = new System.Windows.Forms.SplitContainer
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Orientation = System.Windows.Forms.Orientation.Horizontal,
                    TabStop = false,
                    SplitterWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(4f)
                };

                // ===== BILLS GRID (Top) =====
                var billsPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Bills grid panel",
                    AccessibleDescription = "Grid showing all utility bills"
                };
                SfSkinManager.SetVisualStyle(billsPanel, ThemeColors.DefaultTheme);

                this._billsGrid = new SfDataGrid
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowEditing = false,
                    AllowGrouping = true,
                    AllowFiltering = true,
                    AllowSorting = true,
                    AllowResizingColumns = true,
                    ShowRowHeader = true,
                    ShowBorder = true,
                    GridLinesVisibility = GridLinesVisibility.Horizontal,
                    SelectionMode = GridSelectionMode.Single,
                    NavigationMode = NavigationMode.Row,
                    RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28f),
                    HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(35f),
                    TabIndex = 8,
                    AccessibleName = "Bills grid",
                    AccessibleDescription = "Table showing utility bills with bill number, customer, dates, amounts, and status"
                };

                // Bills grid columns
                this._billsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "BillNumber",
                    HeaderText = "Bill Number",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f)
                });

                this._billsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Customer.DisplayName",
                    HeaderText = "Customer Name",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(180f),
                    AllowFiltering = true
                });

                this._billsGrid.Columns.Add(new GridDateTimeColumn
                {
                    MappingName = "BillDate",
                    HeaderText = "Bill Date",
                    Format = "MM/dd/yyyy",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f)
                });

                this._billsGrid.Columns.Add(new GridDateTimeColumn
                {
                    MappingName = "DueDate",
                    HeaderText = "Due Date",
                    Format = "MM/dd/yyyy",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f)
                });

                this._billsGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "TotalAmount",
                    HeaderText = "Total Amount",
                    Format = "C2",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f)
                });

                this._billsGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "AmountDue",
                    HeaderText = "Amount Due",
                    Format = "C2",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f)
                });

                this._billsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "StatusDescription",
                    HeaderText = "Status",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
                    AllowFiltering = true
                });

                this._billsGrid.Columns.Add(new GridCheckBoxColumn
                {
                    MappingName = "IsOverdue",
                    HeaderText = "Overdue",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f)
                });

                billsPanel.Controls.Add(this._billsGrid);
                this._mainSplitContainer.Panel1.Controls.Add(billsPanel);
                SfSkinManager.SetVisualStyle(this._billsGrid, ThemeColors.DefaultTheme);

                // ===== CUSTOMERS GRID (Bottom) =====
                var customersPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Customers grid panel",
                    AccessibleDescription = "Grid showing all utility customers"
                };
                SfSkinManager.SetVisualStyle(customersPanel, ThemeColors.DefaultTheme);

                this._customersGrid = new SfDataGrid
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowEditing = false,
                    AllowResizingColumns = true,
                    AllowSorting = true,
                    AllowFiltering = true,
                    ShowRowHeader = false,
                    ShowBorder = true,
                    GridLinesVisibility = GridLinesVisibility.Horizontal,
                    SelectionMode = GridSelectionMode.Single,
                    NavigationMode = NavigationMode.Row,
                    RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28f),
                    HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(35f),
                    TabIndex = 9,
                    AccessibleName = "Customers grid",
                    AccessibleDescription = "Table showing utility customers with account number, name, address, phone, and status"
                };

                // Customers grid columns
                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "AccountNumber",
                    HeaderText = "Account Number",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "DisplayName",
                    HeaderText = "Customer Name",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(200f),
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "ServiceAddress",
                    HeaderText = "Service Address",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250f)
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "PhoneNumber",
                    HeaderText = "Phone",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f)
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "StatusDescription",
                    HeaderText = "Status",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
                    AllowFiltering = true
                });

                customersPanel.Controls.Add(this._customersGrid);
                this._mainSplitContainer.Panel2.Controls.Add(customersPanel);
                SfSkinManager.SetVisualStyle(this._customersGrid, ThemeColors.DefaultTheme);

                this._mainLayout.Controls.Add(this._mainSplitContainer, 0, 3);

                // ===== STATUS BAR =====
                this._statusStrip = new System.Windows.Forms.StatusStrip
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    AccessibleName = "Status bar",
                    AccessibleDescription = "Shows current operation status"
                };

                this._statusLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "Ready",
                    Spring = true,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                this._statusStrip.Items.Add(this._statusLabel);
                this._mainLayout.Controls.Add(this._statusStrip, 0, 4);

                // ===== OVERLAYS =====
                this._loadingOverlay = new LoadingOverlay
                {
                    Message = "Loading utility bill data...",
                    Visible = false,
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AccessibleName = "Loading overlay",
                    AccessibleDescription = "Shows while loading bill data"
                };
                this.Controls.Add(this._loadingOverlay);
                this._loadingOverlay.BringToFront();

                this._noDataOverlay = new NoDataOverlay
                {
                    Message = "No utility bills found",
                    Visible = false,
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AccessibleName = "No data overlay",
                    AccessibleDescription = "Shows when no bills match the current filter"
                };
                this.Controls.Add(this._noDataOverlay);
                this._noDataOverlay.BringToFront();

                // ===== THEME APPLICATION =====
                Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, ThemeColors.DefaultTheme);
            }
            finally
            {
                this.ResumeLayout(false);
                this.PerformLayout();
            }
        }

        #endregion

        // ===== FIELD DECLARATIONS =====
        
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolTip _toolTip;
        
        // Layout
        private System.Windows.Forms.TableLayoutPanel _mainLayout;
        private System.Windows.Forms.SplitContainer _mainSplitContainer;
        
        // Header
        private PanelHeader _panelHeader;
        
        // Summary section
        private GradientPanelExt _summaryPanel;
        private System.Windows.Forms.Label _totalOutstandingLabel;
        private System.Windows.Forms.Label _overdueCountLabel;
        private System.Windows.Forms.Label _totalRevenueLabel;
        private System.Windows.Forms.Label _billsThisMonthLabel;
        
        // Action buttons
        private GradientPanelExt _buttonPanel;
        private Syncfusion.WinForms.Controls.SfButton _createBillButton;
        private Syncfusion.WinForms.Controls.SfButton _saveBillButton;
        private Syncfusion.WinForms.Controls.SfButton _deleteBillButton;
        private Syncfusion.WinForms.Controls.SfButton _markPaidButton;
        private Syncfusion.WinForms.Controls.SfButton _generateReportButton;
        private Syncfusion.WinForms.Controls.SfButton _exportExcelButton;
        private Syncfusion.WinForms.Controls.SfButton _refreshButton;
        
        // Data grids
        private SfDataGrid _billsGrid;
        private SfDataGrid _customersGrid;
        
        // Status bar
        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabel;
        
        // Overlays
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
    }
}
