using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.Themes;
using WileyWidget.Models;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Exporters;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Form for managing utility customers with full Syncfusion UI controls.
    /// Provides CRUD operations, search, and data binding using MVVM pattern.
    /// </summary>
    public partial class CustomersForm : Form
    {
        private readonly CustomersViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CustomersForm> _logger;

        // Syncfusion UI controls for data display and editing
        private SfDataGrid? _dataGrid;
        private BindingSource? _bindingSource;
        private Panel? _detailPanel;
        private Panel? _validationPanel;
        private Label? _validationLabel;

        // Syncfusion input controls for customer details
        private TextBoxExt? _accountNumberBox;
        private TextBoxExt? _firstNameBox;
        private TextBoxExt? _lastNameBox;
        private TextBoxExt? _companyBox;
        private ComboBoxAdv? _customerTypeCombo;
        private TextBoxExt? _serviceAddressBox;
        private TextBoxExt? _serviceCityBox;
        private TextBoxExt? _serviceStateBox;
        private TextBoxExt? _serviceZipBox;
        private ComboBoxAdv? _serviceLocationCombo;
        private TextBoxExt? _mailingAddressBox;
        private TextBoxExt? _mailingCityBox;
        private TextBoxExt? _mailingStateBox;
        private TextBoxExt? _mailingZipBox;
        private TextBoxExt? _phoneBox;
        private TextBoxExt? _emailBox;
        private TextBoxExt? _meterNumberBox;
        private SfNumericTextBox? _balanceBox;
        private ComboBoxAdv? _statusCombo;
        private SfDateTimeEdit? _accountOpenDatePicker;
        private TextBoxExt? _notesBox;
        private StatusStrip? _statusStrip;
        private TabControlAdv? _detailTabs;

        // Syncfusion action buttons
        private SfButton? _saveButton;
        private SfButton? _deleteButton;
        private SfButton? _newButton;
        private SfButton? _refreshButton;

        // ToolStrip with Syncfusion theming
        private ToolStrip? _toolStrip;

        private CancellationTokenSource? _cts;
        private System.Windows.Forms.Timer? _validationHideTimer;
        private bool _isDirty = false;

        public CustomersForm(CustomersViewModel viewModel, IServiceProvider serviceProvider, ILogger<CustomersForm> logger)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(this);

                _cts = new CancellationTokenSource();

                // Wire ViewModel property changes for UI updates
                _viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(_viewModel.ErrorMessage) && !string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
                    {
                        ShowValidationMessage(_viewModel.ErrorMessage);
                    }
                };

                _logger.LogInformation("CustomersForm initialized successfully");

#pragma warning disable CS4014
                LoadData();
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize CustomersForm");
                throw;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Configure form properties
            Text = "Customer Management";
            Size = new Size(1400, 800);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1000, 600);
            Name = "CustomersForm";
            BackColor = WileyWidget.WinForms.Themes.ThemeColors.Background;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;

            // Get icons from theme icon service for toolbar and buttons
            Image? loadIcon = GetIcon("load");
            Image? refreshIcon = GetIcon("refresh");
            Image? newIcon = GetIcon("add");
            Image? saveIcon = GetIcon("save");
            Image? deleteIcon = GetIcon("delete");
            Image? searchIcon = GetIcon("search");
            Image? exportIcon = GetIcon("export");
            Image? clearIcon = GetIcon("clear");

            // Initialize toolbar with Syncfusion-themed ToolStrip
            _toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(24, 24),
                Padding = new Padding(4),
                BackColor = WileyWidget.WinForms.Themes.ThemeColors.Background
            };
            var loadBtn = new ToolStripButton("Load", loadIcon, async (s, e) => await LoadData())
            {
                ToolTipText = "Load all customers (F5)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            var refreshBtn = new ToolStripButton("Refresh", refreshIcon, async (s, e) => await RefreshData())
            {
                ToolTipText = "Refresh customer list (Ctrl+R)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            var newBtn = new ToolStripButton("New", newIcon, (s, e) => _ = _viewModel.AddCustomerCommand.ExecuteAsync(default))
            {
                ToolTipText = "Add new customer (Ctrl+N)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            var saveBtn = new ToolStripButton("Save", saveIcon, async (s, e) => await SaveCurrentCustomer())
            {
                ToolTipText = "Save current customer (Ctrl+S)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Enabled = false
            };
            var deleteBtn = new ToolStripButton("Delete", deleteIcon, async (s, e) => await DeleteSelectedCustomer())
            {
                ToolTipText = "Delete selected customer (Delete)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Enabled = false
            };
            var searchBox = new ToolStripTextBox
            {
                Width = 250,
                ToolTipText = "Search customers..."
            };
            searchBox.TextBox.PlaceholderText = "Search customers...";
            var searchBtn = new ToolStripButton("Search", searchIcon, async (s, e) => await PerformSearch(searchBox.Text))
            {
                ToolTipText = "Search customers (Ctrl+F)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            var clearBtn = new ToolStripButton("Clear", clearIcon, async (s, e) => await ClearSearchAndFilters())
            {
                ToolTipText = "Clear search and filters (Esc)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            var exportBtn = new ToolStripButton("Export", exportIcon, async (s, e) => await ExportCustomers())
            {
                ToolTipText = "Export customers to CSV (Ctrl+E)",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };

            // Set proper spacing for toolbar buttons
            loadBtn.Margin = new Padding(2);
            refreshBtn.Margin = new Padding(2);
            refreshBtn.Name = "refreshBtn";
            newBtn.Margin = new Padding(2);
            saveBtn.Margin = new Padding(2);
            saveBtn.Name = "saveBtn";
            deleteBtn.Margin = new Padding(2);
            deleteBtn.Name = "deleteBtn";
            searchBox.Margin = new Padding(2);
            searchBtn.Margin = new Padding(2);
            clearBtn.Margin = new Padding(2);
            exportBtn.Margin = new Padding(2);

            _toolStrip.Items.AddRange(new ToolStripItem[] { loadBtn, refreshBtn, new ToolStripSeparator(), newBtn, saveBtn, deleteBtn, new ToolStripSeparator(), searchBox, searchBtn, clearBtn, new ToolStripSeparator(), exportBtn });

            // Initialize validation panel
            _validationPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(40, WileyWidget.WinForms.Themes.ThemeColors.Warning),
                Visible = false,
                Padding = new Padding(10)
            };
            _validationLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Warning,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _validationPanel.Controls.Add(_validationLabel);

            // Initialize data binding source
            _bindingSource = new BindingSource();

            // Configure Syncfusion DataGrid for customer list display
            _dataGrid = new SfDataGrid
            {
                Name = "Customers_DataGrid",
                AccessibleName = "Customers_DataGrid",
                Dock = DockStyle.Left,
                Width = 750,
                AutoGenerateColumns = false,
                AllowSorting = true,
                AllowFiltering = true,
                AllowEditing = false,
                AllowResizingColumns = true,
                SelectionMode = GridSelectionMode.Single,
                RowHeight = 36,
                Font = new Font("Segoe UI", 9F)
            };

            // Apply theme to the data grid
            WileyWidget.WinForms.Themes.ThemeColors.ApplySfDataGridTheme(_dataGrid);
            SfSkinManager.SetVisualStyle(_dataGrid, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            // Define columns with proper formatting
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountNumber", HeaderText = "Account #", Width = 110 });
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "DisplayName", HeaderText = "Name/Company", Width = 220 });
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "ServiceAddress", HeaderText = "Address", Width = 150 });
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "ServiceCity", HeaderText = "City", Width = 100 });
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "ServiceState", HeaderText = "State", Width = 60 });
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "PhoneNumber", HeaderText = "Phone", Width = 120 });
            var balanceColumn = new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "CurrentBalance", HeaderText = "Balance", Width = 100, Format = "C2" };
            _dataGrid.Columns.Add(balanceColumn);
            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "StatusDescription", HeaderText = "Status", Width = 90 });

            // Add conditional formatting for balance
            _dataGrid.DrawCell += (s, e) =>
            {
                if (e.Column.MappingName == "CurrentBalance" && e.DataRow != null)
                {
                    var customer = e.DataRow.RowData as UtilityCustomer;
                    if (customer != null && customer.CurrentBalance > 0)
                    {
                        e.Style.TextColor = Color.Red;
                        e.Style.Font.Bold = true;
                    }
                }
            };

            // Handle selection changes to update detail view and toolbar
            _dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            _dataGrid.SelectionChanged += (s, e) => UpdateButtonStates();

            // Initialize detail panel with tabbed interface for editing customer information
            _detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            // Create tabbed interface for better organization
            _detailTabs = new TabControlAdv
            {
                Dock = DockStyle.Fill,
                TabStyle = typeof(TabRenderer3D)
            };
            SfSkinManager.SetVisualStyle(_detailTabs, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            // Tab 1: Basic Information
            var basicTab = new TabPageAdv("Basic Info");
            var basicLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 7, AutoSize = true };
            basicLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            basicLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            void AddRow(TableLayoutPanel layout, string labelText, Control control, ref int currentRow)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                var lbl = new Label
                {
                    Text = labelText,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                };
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(0, 3, 0, 3);
                layout.Controls.Add(lbl, 0, currentRow);
                layout.Controls.Add(control, 1, currentRow);
                currentRow++;
            }

            // Initialize all input controls with modern styling
            _accountNumberBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_accountNumberBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _firstNameBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_firstNameBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _lastNameBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_lastNameBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _companyBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_companyBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _phoneBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_phoneBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _emailBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_emailBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _meterNumberBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_meterNumberBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            _customerTypeCombo = new ComboBoxAdv { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_customerTypeCombo, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _statusCombo = new ComboBoxAdv { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_statusCombo, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _serviceLocationCombo = new ComboBoxAdv { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_serviceLocationCombo, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            _balanceBox = new SfNumericTextBox { Font = new Font("Segoe UI", 9F), FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency };
            SfSkinManager.SetVisualStyle(_balanceBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _accountOpenDatePicker = new SfDateTimeEdit { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_accountOpenDatePicker, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            AddRow(basicLayout, "Account #:", _accountNumberBox, ref row);
            AddRow(basicLayout, "First Name:", _firstNameBox, ref row);
            AddRow(basicLayout, "Last Name:", _lastNameBox, ref row);
            AddRow(basicLayout, "Company:", _companyBox, ref row);
            AddRow(basicLayout, "Phone:", _phoneBox, ref row);
            AddRow(basicLayout, "Email:", _emailBox, ref row);
            AddRow(basicLayout, "Open Date:", _accountOpenDatePicker, ref row);

            basicTab.Controls.Add(basicLayout);

            // Tab 2: Service Address
            var serviceTab = new TabPageAdv("Service Address");
            var serviceLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 6, AutoSize = true };
            serviceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            serviceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            row = 0;
            _serviceAddressBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_serviceAddressBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _serviceCityBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_serviceCityBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _serviceStateBox = new TextBoxExt { Font = new Font("Segoe UI", 9F), MaxLength = 2 };
            SfSkinManager.SetVisualStyle(_serviceStateBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _serviceZipBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_serviceZipBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            AddRow(serviceLayout, "Address:", _serviceAddressBox, ref row);
            AddRow(serviceLayout, "City:", _serviceCityBox, ref row);
            AddRow(serviceLayout, "State:", _serviceStateBox, ref row);
            AddRow(serviceLayout, "ZIP Code:", _serviceZipBox, ref row);
            AddRow(serviceLayout, "Location:", _serviceLocationCombo, ref row);
            AddRow(serviceLayout, "Meter #:", _meterNumberBox, ref row);

            serviceTab.Controls.Add(serviceLayout);

            // Tab 3: Mailing Address
            var mailingTab = new TabPageAdv("Mailing Address");
            var mailingLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 4, AutoSize = true };
            mailingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            mailingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            row = 0;
            _mailingAddressBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_mailingAddressBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _mailingCityBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_mailingCityBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _mailingStateBox = new TextBoxExt { Font = new Font("Segoe UI", 9F), MaxLength = 2 };
            SfSkinManager.SetVisualStyle(_mailingStateBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _mailingZipBox = new TextBoxExt { Font = new Font("Segoe UI", 9F) };
            SfSkinManager.SetVisualStyle(_mailingZipBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            AddRow(mailingLayout, "Address:", _mailingAddressBox, ref row);
            AddRow(mailingLayout, "City:", _mailingCityBox, ref row);
            AddRow(mailingLayout, "State:", _mailingStateBox, ref row);
            AddRow(mailingLayout, "ZIP Code:", _mailingZipBox, ref row);

            mailingTab.Controls.Add(mailingLayout);

            // Tab 4: Account Details
            var accountTab = new TabPageAdv("Account Details");
            var accountLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 4, AutoSize = true };
            accountLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            accountLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            row = 0;
            _notesBox = new TextBoxExt { Multiline = true, Height = 120, Font = new Font("Segoe UI", 9F), ScrollBars = ScrollBars.Vertical };
            SfSkinManager.SetVisualStyle(_notesBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            AddRow(accountLayout, "Balance:", _balanceBox, ref row);
            AddRow(accountLayout, "Status:", _statusCombo, ref row);
            AddRow(accountLayout, "Type:", _customerTypeCombo, ref row);
            AddRow(accountLayout, "Notes:", _notesBox, ref row);

            accountTab.Controls.Add(accountLayout);

            _detailTabs.TabPages.AddRange(new TabPageAdv[] { basicTab, serviceTab, mailingTab, accountTab });

            // Initialize action buttons with icons and modern styling
            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
                BackColor = WileyWidget.WinForms.Themes.ThemeColors.Background
            };
            _newButton = new SfButton
            {
                Text = "New",
                Image = newIcon,
                Size = new Size(100, 36),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.White,
                BackColor = WileyWidget.WinForms.Themes.ThemeColors.Success
            };
            SfSkinManager.SetVisualStyle(_newButton, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _newButton.Click += async (s, e) => await AddNewCustomer();

            _saveButton = new SfButton
            {
                Text = "Save",
                Image = saveIcon,
                Size = new Size(100, 36),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = WileyWidget.WinForms.Themes.ThemeColors.PrimaryAccent
            };
            SfSkinManager.SetVisualStyle(_saveButton, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _saveButton.Click += async (s, e) => await SaveCurrentCustomer();

            _deleteButton = new SfButton
            {
                Text = "Delete",
                Image = deleteIcon,
                Size = new Size(100, 36),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.White,
                BackColor = WileyWidget.WinForms.Themes.ThemeColors.Error
            };
            SfSkinManager.SetVisualStyle(_deleteButton, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _deleteButton.Click += async (s, e) => await DeleteSelectedCustomer();

            _refreshButton = new SfButton
            {
                Text = "Refresh",
                Image = refreshIcon,
                Size = new Size(100, 36),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.White,
                BackColor = ThemeManager.Colors.TextPrimary
            };
            SfSkinManager.SetVisualStyle(_refreshButton, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _refreshButton.Click += async (s, e) => await RefreshData();

            buttonBar.Controls.Add(_newButton);
            buttonBar.Controls.Add(_saveButton);
            buttonBar.Controls.Add(_deleteButton);
            buttonBar.Controls.Add(_refreshButton);

            _detailPanel.Controls.Add(buttonBar);
            _detailPanel.Controls.Add(_detailTabs);

            // Add status strip
            _statusStrip = new StatusStrip
            {
                BackColor = WileyWidget.WinForms.Themes.ThemeColors.Background,
                Font = new Font("Segoe UI", 9F)
            };
            var statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var recordCountLabel = new ToolStripStatusLabel("Records: 0") { Name = "recordCount" };
            _statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, recordCountLabel });

            // Add controls to form in correct z-order
            Controls.Add(_detailPanel);
            Controls.Add(_dataGrid);
            Controls.Add(_validationPanel);
            Controls.Add(_toolStrip);
            Controls.Add(_statusStrip);

            // Add keyboard shortcuts
            this.KeyDown += async (sender, e) =>
            {
                if (e.Control && e.KeyCode == Keys.N)
                {
                    e.Handled = true;
                    await AddNewCustomer();
                }
                else if (e.Control && e.KeyCode == Keys.S && _saveButton?.Enabled == true)
                {
                    e.Handled = true;
                    await SaveCurrentCustomer();
                }
                else if (e.KeyCode == Keys.Delete && _deleteButton?.Enabled == true)
                {
                    e.Handled = true;
                    await DeleteSelectedCustomer();
                }
                else if (e.KeyCode == Keys.F5)
                {
                    e.Handled = true;
                    await LoadData();
                }
                else if (e.Control && e.KeyCode == Keys.R)
                {
                    e.Handled = true;
                    await RefreshData();
                }
                else if (e.Control && e.KeyCode == Keys.E)
                {
                    e.Handled = true;
                    await ExportCustomers();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    await ClearSearchAndFilters();
                }
                else if (e.Control && e.KeyCode == Keys.F)
                {
                    e.Handled = true;
                    // Focus search box
                    foreach (ToolStripItem item in _toolStrip.Items)
                    {
                        if (item is ToolStripTextBox searchBox)
                        {
                            searchBox.Focus();
                            break;
                        }
                    }
                }
            };

            // Compose simple bindings - data source will be set after loading data

            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>
        /// Retrieves an icon from the theme icon service by name.
        /// </summary>
        /// <param name="iconName">The name of the icon to retrieve.</param>
        /// <returns>The icon image, or null if not found.</returns>
        private Image? GetIcon(string iconName)
        {
            try
            {
                var themeIconServiceType = Type.GetType("WileyWidget.WinForms.Services.IThemeIconService, WileyWidget.WinForms");
                if (themeIconServiceType != null)
                {
                    var iconService = _serviceProvider.GetService(themeIconServiceType);
                    if (iconService != null)
                    {
                        var getIconMethod = themeIconServiceType.GetMethod("GetIcon");
                        if (getIconMethod != null)
                        {
                            return (Image?)getIconMethod.Invoke(iconService, new object?[] { iconName, null, 16 });
                        }
                    }
                }
            }
            catch
            {
                // Fallback to no icon
            }
            return null;
        }

        // Theme applied globally via ThemeColors.ApplyTheme(this) in constructor

        /// <summary>
        /// Displays a validation message to the user in the validation panel.
        /// </summary>
        /// <param name="message">The message to display.</param>
        private void ShowValidationMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                if (_validationPanel != null) _validationPanel.Visible = false;
                return;
            }

            if (_validationLabel != null && _validationPanel != null)
            {
                _validationLabel.Text = $"Warning: {message}";
                _validationPanel.Visible = true;

                // Auto-hide after 6 seconds
                _validationHideTimer ??= new System.Windows.Forms.Timer { Interval = 6000 };
                _validationHideTimer.Tick -= ValidationHideTimer_Tick;
                _validationHideTimer.Tick += ValidationHideTimer_Tick;
                _validationHideTimer.Stop();
                _validationHideTimer.Start();
            }
        }

        private void ValidationHideTimer_Tick(object? sender, EventArgs e)
        {
            if (_validationPanel != null) _validationPanel.Visible = false;
            _validationHideTimer?.Stop();
        }

        /// <summary>
        /// Loads customer data asynchronously and sets up data bindings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task LoadData()
        {
            try
            {
                _logger.LogInformation("Loading customer data");
                await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.LoadCustomersAsync(ct), _cts, this, _logger, "Loading customers");

                // Wire data source
                if (_bindingSource != null)
                {
                    _bindingSource.DataSource = _viewModel.Customers;
                    _dataGrid!.DataSource = _bindingSource;

                    // Setup detail bindings
                    BindDetailControls();

                    // Update status bar
                    UpdateStatusBar();
                }
                _logger.LogInformation("Customer data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load customer data");
                ShowValidationMessage("Failed to load customer data");
            }
        }

        private void UpdateStatusBar()
        {
            if (_statusStrip?.Items["recordCount"] is ToolStripStatusLabel recordLabel)
            {
                var selectedCount = _dataGrid?.SelectedItems?.Count ?? 0;
                var totalCount = _viewModel.Customers.Count;
                recordLabel.Text = selectedCount > 0
                    ? $"Records: {totalCount} | Selected: {selectedCount}"
                    : $"Records: {totalCount}";
            }
        }

        /// <summary>
        /// Updates button states based on current selection and dirty state.
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasSelection = _dataGrid?.SelectedItem != null;

            if (_toolStrip?.Items["saveBtn"] is ToolStripButton saveBtn)
                saveBtn.Enabled = hasSelection;
            if (_toolStrip?.Items["deleteBtn"] is ToolStripButton delBtn)
                delBtn.Enabled = hasSelection;

            if (_saveButton != null) _saveButton.Enabled = hasSelection;
            if (_deleteButton != null) _deleteButton.Enabled = hasSelection;

            UpdateStatusBar();
        }

        /// <summary>
        /// Sets up data bindings for the detail input controls to the current customer.
        /// </summary>
        private void BindDetailControls()
        {
            if (_bindingSource == null) return;

            _logger.LogDebug("Setting up detail control bindings");

            // Clear existing bindings
            _accountNumberBox?.DataBindings.Clear();
            _firstNameBox?.DataBindings.Clear();
            _lastNameBox?.DataBindings.Clear();
            _companyBox?.DataBindings.Clear();
            _phoneBox?.DataBindings.Clear();
            _emailBox?.DataBindings.Clear();
            _meterNumberBox?.DataBindings.Clear();
            _serviceAddressBox?.DataBindings.Clear();
            _serviceCityBox?.DataBindings.Clear();
            _serviceStateBox?.DataBindings.Clear();
            _serviceZipBox?.DataBindings.Clear();
            _mailingAddressBox?.DataBindings.Clear();
            _mailingCityBox?.DataBindings.Clear();
            _mailingStateBox?.DataBindings.Clear();
            _mailingZipBox?.DataBindings.Clear();
            _notesBox?.DataBindings.Clear();
            _balanceBox?.DataBindings.Clear();
            _accountOpenDatePicker?.DataBindings.Clear();
            _statusCombo?.DataBindings.Clear();
            _customerTypeCombo?.DataBindings.Clear();
            _serviceLocationCombo?.DataBindings.Clear();

            // Bind text controls to customer properties and track changes
            System.Action markDirty = () => { _isDirty = true; UpdateButtonStates(); };

            if (_accountNumberBox != null)
            {
                _accountNumberBox.DataBindings.Add("Text", _bindingSource, "AccountNumber", true, DataSourceUpdateMode.OnPropertyChanged);
                _accountNumberBox.TextChanged += (s, e) => markDirty();
            }
            if (_firstNameBox != null)
            {
                _firstNameBox.DataBindings.Add("Text", _bindingSource, "FirstName", true, DataSourceUpdateMode.OnPropertyChanged);
                _firstNameBox.TextChanged += (s, e) => markDirty();
            }
            if (_lastNameBox != null)
            {
                _lastNameBox.DataBindings.Add("Text", _bindingSource, "LastName", true, DataSourceUpdateMode.OnPropertyChanged);
                _lastNameBox.TextChanged += (s, e) => markDirty();
            }
            if (_companyBox != null)
            {
                _companyBox.DataBindings.Add("Text", _bindingSource, "CompanyName", true, DataSourceUpdateMode.OnPropertyChanged);
                _companyBox.TextChanged += (s, e) => markDirty();
            }
            if (_phoneBox != null)
            {
                _phoneBox.DataBindings.Add("Text", _bindingSource, "PhoneNumber", true, DataSourceUpdateMode.OnPropertyChanged);
                _phoneBox.TextChanged += (s, e) => markDirty();
            }
            if (_emailBox != null)
            {
                _emailBox.DataBindings.Add("Text", _bindingSource, "EmailAddress", true, DataSourceUpdateMode.OnPropertyChanged);
                _emailBox.TextChanged += (s, e) => markDirty();
            }
            if (_meterNumberBox != null)
            {
                _meterNumberBox.DataBindings.Add("Text", _bindingSource, "MeterNumber", true, DataSourceUpdateMode.OnPropertyChanged);
                _meterNumberBox.TextChanged += (s, e) => markDirty();
            }

            // Service address bindings
            if (_serviceAddressBox != null) _serviceAddressBox.DataBindings.Add("Text", _bindingSource, "ServiceAddress", true, DataSourceUpdateMode.OnPropertyChanged);
            if (_serviceCityBox != null) _serviceCityBox.DataBindings.Add("Text", _bindingSource, "ServiceCity", true, DataSourceUpdateMode.OnPropertyChanged);
            if (_serviceStateBox != null) _serviceStateBox.DataBindings.Add("Text", _bindingSource, "ServiceState", true, DataSourceUpdateMode.OnPropertyChanged);
            if (_serviceZipBox != null) _serviceZipBox.DataBindings.Add("Text", _bindingSource, "ServiceZipCode", true, DataSourceUpdateMode.OnPropertyChanged);

            // Mailing address bindings
            if (_mailingAddressBox != null) _mailingAddressBox.DataBindings.Add("Text", _bindingSource, "MailingAddress", true, DataSourceUpdateMode.OnPropertyChanged);
            if (_mailingCityBox != null) _mailingCityBox.DataBindings.Add("Text", _bindingSource, "MailingCity", true, DataSourceUpdateMode.OnPropertyChanged);
            if (_mailingStateBox != null) _mailingStateBox.DataBindings.Add("Text", _bindingSource, "MailingState", true, DataSourceUpdateMode.OnPropertyChanged);
            if (_mailingZipBox != null) _mailingZipBox.DataBindings.Add("Text", _bindingSource, "MailingZipCode", true, DataSourceUpdateMode.OnPropertyChanged);

            // Notes binding
            if (_notesBox != null) _notesBox.DataBindings.Add("Text", _bindingSource, "Notes", true, DataSourceUpdateMode.OnPropertyChanged);

            // Bind numeric control to balance
            if (_balanceBox != null) _balanceBox.DataBindings.Add("Value", _bindingSource, "CurrentBalance", true, DataSourceUpdateMode.OnPropertyChanged);

            // Bind date picker to account open date
            if (_accountOpenDatePicker != null) _accountOpenDatePicker.DataBindings.Add("Value", _bindingSource, "AccountOpenDate", true, DataSourceUpdateMode.OnPropertyChanged);

            // Bind combo boxes with enum values
            if (_statusCombo != null)
            {
                _statusCombo.Items.Clear();
                foreach (var s in Enum.GetValues(typeof(CustomerStatus))) _statusCombo.Items.Add(s);
                _statusCombo.DataBindings.Add("SelectedItem", _bindingSource, "Status", true, DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_customerTypeCombo != null)
            {
                _customerTypeCombo.Items.Clear();
                foreach (var t in Enum.GetValues(typeof(CustomerType))) _customerTypeCombo.Items.Add(t);
                _customerTypeCombo.DataBindings.Add("SelectedItem", _bindingSource, "CustomerType", true, DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_serviceLocationCombo != null)
            {
                _serviceLocationCombo.Items.Clear();
                foreach (var l in Enum.GetValues(typeof(ServiceLocation))) _serviceLocationCombo.Items.Add(l);
                _serviceLocationCombo.DataBindings.Add("SelectedItem", _bindingSource, "ServiceLocation", true, DataSourceUpdateMode.OnPropertyChanged);
            }

            _logger.LogDebug("Detail control bindings set up");
        }

        /// <summary>
        /// Handles selection changes in the data grid to update the selected customer in the ViewModel.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void DataGrid_SelectionChanged(object? sender, EventArgs e)
        {
            try
            {
                if (_bindingSource != null && _bindingSource.Current is UtilityCustomer uc)
                {
                    _viewModel.SelectedCustomer = uc;
                    _logger.LogDebug("Selected customer changed to {Account}", uc.AccountNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error handling selection change in customers grid");
            }
        }

        /// <summary>
        /// Adds a new customer to the collection.
        /// </summary>
        private async Task AddNewCustomer()
        {
            try
            {
                await _viewModel.AddCustomerCommand.ExecuteAsync(default);
                UpdateButtonStates();
                ShowValidationMessage("New customer added. Fill in details and click Save.");
                _isDirty = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add new customer");
                ShowValidationMessage("Failed to add new customer");
            }
        }

        /// <summary>
        /// Refreshes the customer data from the database.
        /// </summary>
        private async Task RefreshData()
        {
            try
            {
                if (_isDirty)
                {
                    var result = MessageBox.Show(this,
                        "You have unsaved changes. Refreshing will lose these changes. Continue?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No) return;
                }

                await LoadData();
                _isDirty = false;
                ShowValidationMessage("Data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh data");
                ShowValidationMessage("Failed to refresh data");
            }
        }

        /// <summary>
        /// Performs a search with the given search text.
        /// </summary>
        private async Task PerformSearch(string searchText)
        {
            try
            {
                _viewModel.SearchText = searchText;
                await _viewModel.SearchCommand.ExecuteAsync(default);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search failed");
                ShowValidationMessage("Search operation failed");
            }
        }

        /// <summary>
        /// Clears the search text and reloads all customers.
        /// </summary>
        private async Task ClearSearchAndFilters()
        {
            try
            {
                _viewModel.SearchText = string.Empty;

                // Clear search box in toolbar
                if (_toolStrip?.Items != null)
                {
                    foreach (ToolStripItem item in _toolStrip.Items)
                    {
                        if (item is ToolStripTextBox searchBox)
                        {
                            searchBox.Text = string.Empty;
                            break;
                        }
                    }
                }

                await LoadData();
                ShowValidationMessage("Search and filters cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear search");
                ShowValidationMessage("Failed to clear search");
            }
        }

        /// <summary>
        /// Exports customers to CSV file.
        /// </summary>
        private async Task ExportCustomers()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"Customers_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "Export Customers"
                };

                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    await Task.Run(() =>
                    {
                        var csv = new StringBuilder();

                        // Header
                        csv.AppendLine("Account Number,Name/Company,Service Address,City,State,ZIP,Phone,Email,Balance,Status,Type,Location,Meter Number,Account Open Date,Notes");

                        // Data rows
                        foreach (var customer in _viewModel.Customers)
                        {
                            string line = string.Format(CultureInfo.InvariantCulture,
                                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",{8:F2},\"{9}\",\"{10}\",\"{11}\",\"{12}\",{13:yyyy-MM-dd},\"{14}\"",
                                customer.AccountNumber,
                                customer.DisplayName,
                                customer.ServiceAddress,
                                customer.ServiceCity,
                                customer.ServiceState,
                                customer.ServiceZipCode,
                                customer.PhoneNumber,
                                customer.EmailAddress,
                                customer.CurrentBalance,
                                customer.StatusDescription,
                                customer.CustomerType,
                                customer.ServiceLocation,
                                customer.MeterNumber,
                                customer.AccountOpenDate,
                                customer.Notes?.Replace("\"", "\"\"", StringComparison.Ordinal));
                            csv.AppendLine(line);
                        }

                        File.WriteAllText(saveDialog.FileName, csv.ToString());
                    });

                    _logger.LogInformation("Exported {Count} customers to {File}", _viewModel.Customers.Count, saveDialog.FileName);
                    MessageBox.Show(this, $"Successfully exported {_viewModel.Customers.Count} customers to:\n{saveDialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export failed");
                MessageBox.Show(this, $"Failed to export customers: {ex.Message}",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Saves the currently selected customer asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SaveCurrentCustomer()
        {
            try
            {
                _logger.LogInformation("Saving current customer");
                if (_bindingSource?.Current is UtilityCustomer uc)
                {
                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(uc.AccountNumber))
                    {
                        ShowValidationMessage("Account Number is required");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(uc.FirstName) && string.IsNullOrWhiteSpace(uc.LastName) && string.IsNullOrWhiteSpace(uc.CompanyName))
                    {
                        ShowValidationMessage("Customer must have either a name or company name");
                        return;
                    }

                    var success = await _viewModel.SaveCustomerAsync(uc);
                    if (success)
                    {
                        _isDirty = false;
                        UpdateStatusBar();
                        ShowValidationMessage("Customer saved successfully");
                        _logger.LogInformation("Customer saved successfully");
                    }
                    else
                    {
                        ShowValidationMessage("Failed to save customer - check logs for details");
                    }
                }
                else
                {
                    _logger.LogWarning("No customer selected for saving");
                    ShowValidationMessage("No customer selected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save failed");
                ShowValidationMessage("Failed to save customer: " + ex.Message);
            }
        }

        /// <summary>
        /// Deletes the currently selected customer after confirmation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task DeleteSelectedCustomer()
        {
            try
            {
                _logger.LogInformation("Attempting to delete selected customer");
                if (_bindingSource?.Current is UtilityCustomer uc)
                {
                    var id = uc.Id;
                    if (id == 0)
                    {
                        // Not persisted yet - just remove from list
                        _viewModel.Customers.Remove(uc);
                        UpdateStatusBar();
                        _logger.LogInformation("Removed unsaved customer from list");
                        return;
                    }

                    var dr = MessageBox.Show(this, $"Delete customer '{uc.DisplayName}'?\n\nThis action cannot be undone.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Yes)
                    {
                        await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.DeleteCustomerAsync(id), _cts, this, _logger, "Deleting customer");
                        UpdateStatusBar();
                        MessageBox.Show(this, "Customer deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _logger.LogInformation("Customer deleted successfully");
                    }
                    else
                    {
                        _logger.LogInformation("Customer deletion cancelled by user");
                    }
                }
                else
                {
                    _logger.LogWarning("No customer selected for deletion");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed");
                ShowValidationMessage("Failed to delete customer");
            }
        }

        /// <summary>
        /// Handles form closing to clean up resources.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            // Check for unsaved changes
            if (_isDirty)
            {
                var result = MessageBox.Show(this,
                    "You have unsaved changes. Do you want to close without saving?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            _logger.LogInformation("CustomersForm closing, cleaning up resources");
            _validationHideTimer?.Dispose();
            Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
            base.OnFormClosing(e);
        }
    }
}
