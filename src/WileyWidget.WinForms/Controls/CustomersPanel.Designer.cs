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
    partial class CustomersPanel
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
            
            // SUSPEND LAYOUT - Speeds up initialization with many controls
            this.SuspendLayout();
            
            try
            {
                // ===== DPI CONSTANTS =====
                var standardPadding = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(16f);
                var controlSpacing = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f);
                var rowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(40f);
                
                // ===== CORE PANEL PROPERTIES =====
                this.Name = "CustomersPanel";
                this.Dock = System.Windows.Forms.DockStyle.Fill;
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
                this.Size = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(900f));
                this.MinimumSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
                this.Padding = new System.Windows.Forms.Padding(standardPadding);
                this.AccessibleName = "Customers Management";
                this.AccessibleDescription = "Panel for viewing and managing utility customers with search, filtering, and bulk operations";

                // ===== TOOLTIP INITIALIZATION (MUST BE FIRST) =====
                this._toolTip = new System.Windows.Forms.ToolTip(this.components);
                this._toolTip.AutoPopDelay = 5000;
                this._toolTip.InitialDelay = 500;
                this._toolTip.ReshowDelay = 200;
                this._toolTip.ShowAlways = true;

                // ===== ERROR PROVIDER =====
                this._errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
                this._errorProvider.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;

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

                // Row 1: Summary Panel
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f)));

                // Row 2: Toolbar
                this._mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130f)));

                // Row 3: Data Grid
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
                    Title = "Customers Management",
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                    AccessibleName = "Panel header",
                    AccessibleDescription = "Panel title and action buttons"
                };
                this._mainLayout.Controls.Add(this._panelHeader, 0, 0);
                SfSkinManager.SetVisualStyle(this._panelHeader, ThemeColors.DefaultTheme);

                // ===== SUMMARY PANEL =====
                this._summaryPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Customer summary metrics",
                    AccessibleDescription = "Shows total customers, active count, and total balance"
                };

                var summaryLayout = new System.Windows.Forms.FlowLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true
                };

                // Summary label helper function
                var createSummaryLabel = new System.Func<string, System.Windows.Forms.Label>((text) =>
                {
                    return new System.Windows.Forms.Label
                    {
                        Text = text,
                        Font = new System.Drawing.Font("Segoe UI", 
                            Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f), 
                            System.Drawing.FontStyle.Bold),
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                        AutoSize = false,
                        Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(180f),
                        Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                        Margin = new System.Windows.Forms.Padding(controlSpacing)
                    };
                });

                this._totalCustomersLabel = createSummaryLabel("Total: 0");
                this._activeCustomersLabel = createSummaryLabel("Active: 0");
                this._balanceSummaryLabel = createSummaryLabel("Balance: $0.00");

                summaryLayout.Controls.Add(this._totalCustomersLabel);
                summaryLayout.Controls.Add(this._activeCustomersLabel);
                summaryLayout.Controls.Add(this._balanceSummaryLabel);

                this._summaryPanel.Controls.Add(summaryLayout);
                this._mainLayout.Controls.Add(this._summaryPanel, 0, 1);
                SfSkinManager.SetVisualStyle(this._summaryPanel, ThemeColors.DefaultTheme);

                // ===== TOOLBAR PANEL =====
                this._toolbarPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Toolbar",
                    AccessibleDescription = "Customer management toolbar with search, filters, and action buttons"
                };

                var toolbarFlow = new System.Windows.Forms.FlowLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                    WrapContents = true,
                    AutoSize = true,
                    AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
                };

                // Search box
                this._searchTextBox = new TextBoxExt
                {
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(220f),
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    PlaceholderText = "Search by name or account...",
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    TabIndex = 1,
                    AccessibleName = "Search customers",
                    AccessibleDescription = "Enter name, account number, or address to search"
                };
                this._toolTip.SetToolTip(this._searchTextBox, "Search by customer name, account number, or service address");
                toolbarFlow.Controls.Add(this._searchTextBox);

                // Action buttons
                var buttonSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(95f),
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

                this._addCustomerButton = createButton("&Add", 2, "Add new customer");
                this._editCustomerButton = createButton("&Edit", 3, "Edit selected customer");
                this._deleteCustomerButton = createButton("&Delete", 4, "Delete selected customer");
                this._refreshButton = createButton("Re&fresh", 5, "Reload customer data");
                this._exportButton = createButton("E&xport", 6, "Export to Excel or PDF");

                toolbarFlow.Controls.Add(this._addCustomerButton);
                toolbarFlow.Controls.Add(this._editCustomerButton);
                toolbarFlow.Controls.Add(this._deleteCustomerButton);
                toolbarFlow.Controls.Add(this._refreshButton);
                toolbarFlow.Controls.Add(this._exportButton);

                // Filter section
                var filterLabel = new System.Windows.Forms.Label
                {
                    Text = "Filter:",
                    AutoSize = true,
                    Margin = new System.Windows.Forms.Padding(controlSpacing, 5, controlSpacing / 2, 0)
                };
                toolbarFlow.Controls.Add(filterLabel);

                this._filterTypeComboBox = new SfComboBox
                {
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                    TabIndex = 7,
                    AccessibleName = "Customer type filter",
                    AccessibleDescription = "Filter customers by type: Residential, Commercial, Industrial, or All"
                };
                this._toolTip.SetToolTip(this._filterTypeComboBox, "Filter by customer type");
                toolbarFlow.Controls.Add(this._filterTypeComboBox);

                this._filterLocationComboBox = new SfComboBox
                {
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(140f),
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                    TabIndex = 8,
                    AccessibleName = "Service location filter",
                    AccessibleDescription = "Filter by service location: Inside City Limits or Outside City Limits"
                };
                this._toolTip.SetToolTip(this._filterLocationComboBox, "Filter by service location");
                toolbarFlow.Controls.Add(this._filterLocationComboBox);

                this._showActiveOnlyCheckBox = new CheckBoxAdv
                {
                    Text = "Active Only",
                    AutoSize = true,
                    Checked = true,
                    TabIndex = 9,
                    Margin = new System.Windows.Forms.Padding(controlSpacing, 5, 0, 0),
                    AccessibleName = "Show active customers only",
                    AccessibleDescription = "Show only active customer accounts"
                };
                this._toolTip.SetToolTip(this._showActiveOnlyCheckBox, "Show only active customers");
                toolbarFlow.Controls.Add(this._showActiveOnlyCheckBox);

                this._toolbarPanel.Controls.Add(toolbarFlow);
                this._mainLayout.Controls.Add(this._toolbarPanel, 0, 2);
                SfSkinManager.SetVisualStyle(this._toolbarPanel, ThemeColors.DefaultTheme);

                // ===== DATA GRID =====
                this._customersGrid = new SfDataGrid
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowEditing = false,
                    AllowFiltering = true,
                    AllowSorting = true,
                    AllowResizingColumns = true,
                    AllowDraggingColumns = true,
                    AllowMovingColumns = true,
                    SelectionMode = GridSelectionMode.Single,
                    NavigationMode = NavigationMode.Row,
                    ShowRowHeader = true,
                    ShowBorder = true,
                    GridLinesVisibility = GridLinesVisibility.Horizontal,
                    ExcelLikeCurrentCell = true,
                    RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28f),
                    HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(35f),
                    TabIndex = 10,
                    AccessibleName = "Customers data grid",
                    AccessibleDescription = "Table of utility customers with account number, name, type, address, city, location, phone, and balance columns"
                };

                // Grid columns
                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "AccountNumber",
                    HeaderText = "Account #",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f),
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "DisplayName",
                    HeaderText = "Customer Name",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250f),
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "CustomerTypeDescription",
                    HeaderText = "Type",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "ServiceAddress",
                    HeaderText = "Service Address",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(280f),
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "ServiceCity",
                    HeaderText = "City",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
                    AllowSorting = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "ServiceLocationDescription",
                    HeaderText = "Location",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(150f),
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "PhoneNumber",
                    HeaderText = "Phone",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130f),
                    AllowSorting = true
                });

                this._customersGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "CurrentBalance",
                    HeaderText = "Balance",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
                    Format = "C2",
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._customersGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "StatusDescription",
                    HeaderText = "Status",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
                    AllowSorting = true,
                    AllowFiltering = true
                });

                this._mainLayout.Controls.Add(this._customersGrid, 0, 3);
                SfSkinManager.SetVisualStyle(this._customersGrid, ThemeColors.DefaultTheme);

                // ===== STATUS BAR =====
                this._statusStrip = new System.Windows.Forms.StatusStrip
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    AccessibleName = "Status bar",
                    AccessibleDescription = "Shows current operation status and record count"
                };

                this._statusLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "Ready",
                    Spring = true,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                this._countLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "0 customers",
                    BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left,
                    BorderStyle = System.Windows.Forms.Border3DStyle.Etched
                };

                this._balanceLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "Total Balance: $0.00",
                    BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left,
                    BorderStyle = System.Windows.Forms.Border3DStyle.Etched
                };

                this._statusStrip.Items.Add(this._statusLabel);
                this._statusStrip.Items.Add(this._countLabel);
                this._statusStrip.Items.Add(this._balanceLabel);
                this._mainLayout.Controls.Add(this._statusStrip, 0, 4);

                // ===== OVERLAYS =====
                this._loadingOverlay = new LoadingOverlay
                {
                    Message = "Loading customers...",
                    Visible = false,
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AccessibleName = "Loading overlay",
                    AccessibleDescription = "Shows while loading customer data"
                };
                this.Controls.Add(this._loadingOverlay);
                this._loadingOverlay.BringToFront();

                this._noDataOverlay = new NoDataOverlay
                {
                    Message = "No customers found. Click 'Add' to create one.",
                    Visible = false,
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AccessibleName = "No data overlay",
                    AccessibleDescription = "Shows when no customers match the current filter"
                };
                this.Controls.Add(this._noDataOverlay);
                this._noDataOverlay.BringToFront();

                // ===== THEME APPLICATION =====
                Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, ThemeColors.DefaultTheme);
            }
            finally
            {
                // RESUME LAYOUT - Applies all pending layout changes at once
                this.ResumeLayout(false);
                this.PerformLayout();
            }
        }

        #endregion

        // ===== FIELD DECLARATIONS =====
        
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.ErrorProvider _errorProvider;
        
        // Layout
        private System.Windows.Forms.TableLayoutPanel _mainLayout;
        
        // Header & overlays
        private PanelHeader _panelHeader;
        private LoadingOverlay _loadingOverlay;
        private NoDataOverlay _noDataOverlay;
        
        // Summary section
        private GradientPanelExt _summaryPanel;
        private System.Windows.Forms.Label _totalCustomersLabel;
        private System.Windows.Forms.Label _activeCustomersLabel;
        private System.Windows.Forms.Label _balanceSummaryLabel;
        
        // Toolbar section
        private GradientPanelExt _toolbarPanel;
        private TextBoxExt _searchTextBox;
        private Syncfusion.WinForms.Controls.SfButton _addCustomerButton;
        private Syncfusion.WinForms.Controls.SfButton _editCustomerButton;
        private Syncfusion.WinForms.Controls.SfButton _deleteCustomerButton;
        private Syncfusion.WinForms.Controls.SfButton _refreshButton;
        private Syncfusion.WinForms.Controls.SfButton _exportButton;
        private SfComboBox _filterTypeComboBox;
        private SfComboBox _filterLocationComboBox;
        private CheckBoxAdv _showActiveOnlyCheckBox;
        
        // Data grid
        private SfDataGrid _customersGrid;
        
        // Status bar
        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabel;
        private System.Windows.Forms.ToolStripStatusLabel _countLabel;
        private System.Windows.Forms.ToolStripStatusLabel _balanceLabel;
    }
}
