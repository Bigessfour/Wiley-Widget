using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Input;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Syncfusion.Windows.Forms.Chart;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Abstractions.Models;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.WinForms.Exporters;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Forms
{
    internal static class Resources
    {
        public const string FormTitle = "Municipal Accounts";
        public const string LoadAccountsButton = "Load Accounts";
        public const string ApplyFiltersButton = "Apply Filters";
        public const string LoadingText = "Loading...";
        public const string AccountNumberHeader = "Account Number";
        public const string AccountNameHeader = "Account Name";
        public const string DescriptionHeader = "Description";
        public const string TypeHeader = "Type";
        public const string FundHeader = "Fund";
        public const string BalanceHeader = "Balance";
        public const string BudgetAmountHeader = "Budget Amount";
        public const string DepartmentHeader = "Department";
        public const string ActiveHeader = "Active";
        public const string HasParentHeader = "Has Parent";
        public const string ErrorTitle = "Error";
        public const string LoadErrorMessage = "Error loading accounts: {0}";
        public const string NewAccountMenu = "New Account";
        public const string EditAccountMenu = "Edit Account";
        public const string DeleteAccountMenu = "Delete Account";
        public const string ViewDetailsMenu = "View Details";
        public const string ExportMenu = "Export Selected";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class AccountsForm : Form
    {
        private readonly AccountsViewModel _viewModel;
        private readonly ILogger<AccountsForm> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly FormStateManager _stateManager;
        private SfDataGrid? _dataGrid;  // Use Syncfusion SfDataGrid for high-performance grid
        private Panel? _detailPanel;
        private Panel? _validationPanel;
        private Label? _validationLabel;
        private TreeView? _accountTree;
        private Label? _detailAccountNumber;
        private Label? _detailAccountName;
        private Label? _detailBalance;
        private Label? _detailBudget;
        private Label? _detailVariance;
        private ComboBox? _fundCombo;
        private ComboBox? _typeCombo;
        private TextBox? _searchBox;
        private SplitContainer? _mainSplit;
        private Label? _emptyStateLabel;
        private SplitContainer? _leftSplit;
        private ChartControl? _varianceChart;
        private Button? _toggleDetailButton;

        // Cancellation token source for async operations
        private CancellationTokenSource? _cts;
        private System.Windows.Forms.Timer? _searchTimer;
        private bool _isSelectingFromTree = false;

        public AccountsForm(AccountsViewModel viewModel, IServiceProvider serviceProvider, ILogger<AccountsForm> logger)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateManager = new FormStateManager(logger);

            try
            {
                ApplySyncfusionTheme();
                SetupDataGrid();

                // Restore window state if available
                var savedState = _stateManager.LoadFormState("AccountsForm");
                if (savedState != null)
                {
                    _stateManager.ApplyFormState(this, savedState);
                    // Restore splitter positions if saved
                    if (savedState.MainSplitterDistance.HasValue && _mainSplit != null)
                    {
                        _mainSplit.SplitterDistance = savedState.MainSplitterDistance.Value;
                    }
                    if (savedState.LeftSplitterDistance.HasValue && _leftSplit != null)
                    {
                        _leftSplit.SplitterDistance = savedState.LeftSplitterDistance.Value;
                    }
                }

                _logger.LogInformation("AccountsForm initialized successfully");

                // Initialize cancellation token source
                _viewModel.HierarchicalAccounts.CollectionChanged += (s, e) => PopulateTreeFromHierarchy();
                _viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(_viewModel.ErrorMessage) && !string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
                    {
                        ShowValidationMessage(_viewModel.ErrorMessage);
                    }
                };
                _cts = new CancellationTokenSource();

                FormClosing += (s, e) =>
                {
                    // Save window state before closing
                    try
                    {
                        var mainSplitterDist = _mainSplit?.SplitterDistance;
                        var leftSplitterDist = _leftSplit?.SplitterDistance;
                        _stateManager.SaveFormState(this, "AccountsForm", mainSplitterDist, leftSplitterDist);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to save form state on close");
                    }
                    Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                };

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                LoadData();
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AccountsForm");
                if (Application.MessageLoop)
                {
                    MessageBox.Show($"Unable to open accounts view: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                throw;
            }
        }

        private void ApplySyncfusionTheme()
        {
            try
            {
                // Prefer Syncfusion SkinManager when available
                if (Syncfusion.Windows.Forms.SkinManager.ContainsSkinManager)
                {
                    try { Syncfusion.Windows.Forms.SkinManager.SetVisualStyle(this, "Office2019DarkGray"); } catch { }
                }
                else
                {
                    // Apply Office 2019 Dark Gray theme colors manually as a fallback
                    BackColor = Color.FromArgb(45, 45, 48);
                    ForeColor = Color.White;

                    // Update toolbar colors
                    var toolStrip = Controls.OfType<ToolStrip>().FirstOrDefault();
                    if (toolStrip != null)
                    {
                        toolStrip.BackColor = Color.FromArgb(45, 45, 48);
                    }

                    // Update status strip
                    var statusStrip = Controls.OfType<StatusStrip>().FirstOrDefault();
                    if (statusStrip != null)
                    {
                        statusStrip.BackColor = Color.FromArgb(45, 45, 48);
                    }
                }

                _logger.LogInformation("Applied Office2019DarkGray theme to AccountsForm");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply theme to AccountsForm");
            }
        }

        private void AccountsForm_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.F5)
                {
                    // Refresh
                    _ = Utilities.AsyncEventHelper.ExecuteAsync(async ct => await LoadData(), _cts, this, _logger, "Refreshing accounts");
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.N)
                {
                    CreateNewAccount();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelectedAccount();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    ShowAccountDetails();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling keypress in AccountsForm");
            }
        }

        private System.Windows.Forms.Timer? _validationHideTimer;

        private void ShowValidationMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                if (_validationPanel != null) _validationPanel.Visible = false;
                return;
            }

            if (_validationLabel != null && _validationPanel != null)
            {
                _validationLabel.Text = message;
                _validationPanel.Visible = true;

                // Auto-hide after 6 seconds
                try
                {
                    _validationHideTimer ??= new System.Windows.Forms.Timer { Interval = 6000 };
                    _validationHideTimer.Tick -= ValidationHideTimer_Tick;
                    _validationHideTimer.Tick += ValidationHideTimer_Tick;
                    _validationHideTimer.Stop();
                    _validationHideTimer.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to start validation hide timer");
                }
            }

            // Also show a light toast for user attention
            ShowToast(message, 3500);
        }

        private void ValidationHideTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                _validationHideTimer?.Stop();
                if (_validationPanel != null)
                    _validationPanel.Visible = false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error hiding validation panel");
            }
        }

        private void ShowToast(string message, int timeoutMs = 3000)
        {
            try
            {
                var toast = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    TopMost = true,
                    Opacity = 0.95,
                    Width = 420,
                    Height = 64
                };

                var lbl = new Label
                {
                    Text = message,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9F),
                    Padding = new Padding(12)
                };

                if (!Syncfusion.Windows.Forms.SkinManager.ContainsSkinManager)
                {
                    toast.BackColor = Color.FromArgb(40, 40, 40);
                    lbl.ForeColor = Color.White;
                }
                toast.Controls.Add(lbl);

                // Position at top-right of parent
                var screenPoint = this.PointToScreen(new Point(this.ClientSize.Width - toast.Width - 16, 16));
                toast.Location = screenPoint;

                toast.Load += (s, e) =>
                {
                    var t = new System.Windows.Forms.Timer { Interval = timeoutMs };
                    t.Tick += (ts, te) =>
                    {
                        t.Stop();
                        t.Dispose();
                        try { toast.Close(); toast.Dispose(); } catch { }
                    };
                    t.Start();
                };

                toast.Show(this);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to show toast");
            }
        }

        private void CreateNewAccount()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var accountService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Services.Abstractions.IAccountService>(scope.ServiceProvider);
                var deptRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IDepartmentRepository>(scope.ServiceProvider);
                var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scope.ServiceProvider);
                var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<WileyWidget.WinForms.Dialogs.AccountEditDialog>>(scope.ServiceProvider);

                var dialog = new WileyWidget.WinForms.Dialogs.AccountEditDialog(
                    accountService, deptRepo, scopeFactory, logger);

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Refresh grid after a new account was created
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    LoadData();
#pragma warning restore CS4014
                }
            }
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Forms.Form.set_Text")]
        private void InitializeComponent()
        {
            SuspendLayout();

            Text = Resources.FormTitle;
            // Use 85% of primary screen or 1400x900 minimum, whichever is larger
            var primaryScreen = Screen.PrimaryScreen;
            var screenWidth = primaryScreen?.Bounds.Width ?? 1600;
            var screenHeight = primaryScreen?.Bounds.Height ?? 1200;
            var width = Math.Max(1400, (int)(screenWidth * 0.85));
            var height = Math.Max(900, (int)(screenHeight * 0.75));
            Size = new Size(width, height);
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            KeyDown += AccountsForm_KeyDown;

            // === Enhanced Toolbar ===
            var toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(5, 0, 5, 0)
            };

            var loadButton = new ToolStripButton(Resources.LoadAccountsButton, null, async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct => await LoadData(),
                    _cts,
                    this,
                    _logger,
                    "Loading accounts");
            })
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                BackColor = Color.FromArgb(66, 133, 244),
                ForeColor = Color.White
            };

            var filterButton = new ToolStripButton(Resources.ApplyFiltersButton, null, async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct => await _viewModel.FilterAccountsCommand.ExecuteAsync(ct),
                    _cts,
                    this,
                    _logger,
                    "Applying account filters");
            })
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };

            // Fund filter
            var fundLabel = new ToolStripLabel("  Fund: ");
            _fundCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            _fundCombo.Items.AddRange(new object[] { "(all)", "General Fund", "Water Fund", "Sewer Fund", "Capital Projects", "Debt Service" });
            _fundCombo.SelectedIndex = 0;
            _fundCombo.AccessibleName = "Fund Filter";
            _fundCombo.AccessibleDescription = "Filter accounts by fund type";
            var fundHost = new ToolStripControlHost(_fundCombo);

            // Account Type filter
            var typeLabel = new ToolStripLabel("  Type: ");
            _typeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            _typeCombo.Items.AddRange(new object[] { "(all)", "Asset", "Liability", "Revenue", "Expense", "Equity" });
            _typeCombo.SelectedIndex = 0;
            _typeCombo.AccessibleName = "Account Type Filter";
            _typeCombo.AccessibleDescription = "Filter accounts by type (Asset, Liability, Revenue, Expense, Equity)";
            var typeHost = new ToolStripControlHost(_typeCombo);

            // Search box
            var searchLabel = new ToolStripLabel("  Search: ");
            _searchBox = new TextBox { Width = 180 };
            _searchBox.PlaceholderText = "Account name or number...";
            _searchBox.AccessibleName = "Account Search";
            _searchBox.AccessibleDescription = "Search accounts by name or number";
            var searchHost = new ToolStripControlHost(_searchBox);
            // Debounced search
            _searchTimer = new System.Windows.Forms.Timer { Interval = 400 };
            // Use an async void handler so we can await the async execution helper and avoid CS4014
            _searchTimer.Tick += async (s, e) =>
            {
                _searchTimer!.Stop();
                try
                {
                    _viewModel.SearchText = _searchBox?.Text;
                    await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.FilterAccountsCommand.ExecuteAsync(ct), _cts, this, _logger, "Applying search");
                    // Refresh grid styling for search highlights
                    _dataGrid?.Invalidate();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing search");
                }
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _searchTimer!.Stop();
                _searchTimer.Start();
            };

            // Export button — invoke the same export flow as the grid context menu
            var exportButton = new ToolStripButton("Export to Excel", null, (s, e) =>
            {
                try
                {
                    _logger.LogInformation("Export to Excel toolbar button clicked");
                    ExportSelectedAccounts();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Export toolbar button failed");
                    if (Application.MessageLoop)
                    {
                        MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            });

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                loadButton,
                new ToolStripSeparator(),
                fundLabel, fundHost,
                typeLabel, typeHost,
                searchLabel, searchHost,
                new ToolStripSeparator(),
                filterButton,
                new ToolStripSeparator(),
                exportButton
            });

            // Enable overflow and set minimum sizes to prevent clipping
            toolStrip.CanOverflow = true;
            _fundCombo.MinimumSize = new Size(150, 0);
            _typeCombo.MinimumSize = new Size(120, 0);
            _searchBox.MinimumSize = new Size(180, 0);

            // Wire filter selections to ViewModel
            _fundCombo.SelectedIndexChanged += (s, e) =>
            {
                try
                {
                    if (_fundCombo.SelectedItem is string str && str != "(all)")
                    {
                        // Normalize label to enum name (remove spaces)
                        var candidate = str.Replace(" ", string.Empty);
                        if (Enum.TryParse<MunicipalFundType>(candidate, ignoreCase: true, out var mf))
                        {
                            _viewModel.SelectedFund = mf;
                        }
                        else
                        {
                            _viewModel.SelectedFund = null;
                        }
                    }
                    else
                    {
                        _viewModel.SelectedFund = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to map fund selection to ViewModel");
                }
            };

            _typeCombo.SelectedIndexChanged += (s, e) =>
            {
                try
                {
                    if (_typeCombo.SelectedItem is string ts && ts != "(all)")
                    {
                        if (Enum.TryParse<AccountType>(ts, ignoreCase: true, out var at))
                        {
                            _viewModel.SelectedAccountType = at;
                        }
                        else
                        {
                            _viewModel.SelectedAccountType = null;
                        }
                    }
                    else
                    {
                        _viewModel.SelectedAccountType = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to map account type selection to ViewModel");
                }
            };

            // === Status Strip ===
            var statusStrip = new StatusStrip { BackColor = Color.FromArgb(248, 249, 250) };
            var statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var recordCountLabel = new ToolStripStatusLabel { Alignment = ToolStripItemAlignment.Right };
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, recordCountLabel });

            // Bind status
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.IsLoading))
                {
                    statusLabel.Text = _viewModel.IsLoading ? Resources.LoadingText : "Ready";
                    recordCountLabel.Text = _viewModel.IsLoading ? "" : $"{_viewModel.ActiveAccountCount} accounts | Total Balance: {_viewModel.TotalBalance:C}";
                }
            };

            Controls.AddRange(new Control[] { toolStrip, statusStrip });

            ResumeLayout(false);
            PerformLayout();
        }

        private void SetupDataGrid()
        {
            // === Main Split Container: (Tree+Grid) | Details ===
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,  // Changed from Horizontal to Vertical for proper Tree | Grid | Details layout
                SplitterDistance = 800, // Initial default; will be recalculated in OnLoad
                BackColor = Color.FromArgb(45, 45, 48)
            };
            _mainSplit = mainSplit;

            // Left panel: Tree and Grid vertically split
            _leftSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 250, // Initial default; will be recalculated in OnLoad
                BackColor = Color.FromArgb(45, 45, 48)
            };
            mainSplit.Panel1.Controls.Add(_leftSplit);

            // === LEFT PANEL: Tree with Header ===
            var treeContainerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 250, 252)
            };

            // Tree section header (bold, visible)
            var treeHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(240, 240, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };
            var treeHeaderLabel = new Label
            {
                Text = "Account Hierarchy",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = true
            };
            treeHeaderPanel.Controls.Add(treeHeaderLabel);
            treeContainerPanel.Controls.Add(treeHeaderPanel);

            // Tree panel on left
            var treePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(8)
            };

            _accountTree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowLines = true,
                ShowRootLines = true,
                Visible = true
            };
            _accountTree.AfterSelect += (s, e) =>
            {
                _isSelectingFromTree = true;
                // When a tree node is selected, try to select the corresponding row in the grid
                if (e.Node?.Tag is MunicipalAccountDisplay disp && _dataGrid != null)
                {
                    // Find first matching item in the bound BindingSource and select it
                    if (_dataGrid.DataSource is BindingSource bs)
                    {
                        for (int i = 0; i < bs.Count; i++)
                        {
                            if (bs[i] is MunicipalAccountDisplay r && r.Id == disp.Id)
                            {
                                _dataGrid.SelectedIndex = i;
                                _dataGrid.Focus();
                                break;
                            }
                        }
                    }
                }
                _isSelectingFromTree = false;
            };
            treePanel.Controls.Add(_accountTree);
            treeContainerPanel.Controls.Add(treePanel);
            _leftSplit.Panel1.Controls.Add(treeContainerPanel);

            // === MIDDLE PANEL: Grid with Header ===
            var gridContainerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Grid section header (bold, visible)
            var gridHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(240, 240, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };
            var gridHeaderLabel = new Label
            {
                Text = "Account List",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = true
            };
            gridHeaderPanel.Controls.Add(gridHeaderLabel);
            gridContainerPanel.Controls.Add(gridHeaderPanel);

            // Grid panel on right of left split
            var gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            _dataGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AllowEditing = false,
                SelectionUnit = SelectionUnit.Row,
                AutoGenerateColumns = false,
                ShowGroupDropArea = false,
                ShowRowHeader = false,
                BackColor = Color.White,
                RowHeight = 30,
                HeaderRowHeight = 28,
                AllowSorting = true,
                AllowFiltering = false,
                AccessibleName = "Municipal Accounts Grid",
                AccessibleDescription = "Data grid displaying all municipal accounts with balance and budget information. Use arrow keys to navigate, Enter to view details.",
                AccessibleRole = AccessibleRole.Table,
                Visible = true
            };

            // Selection changed event for detail panel
            _dataGrid.SelectionChanged += DataGrid_SelectionChanged;

            // Empty state label
            _emptyStateLabel = new Label
            {
                Text = "No accounts found.\n\nClick 'Load Accounts' to refresh or check your filters.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                Visible = false
            };
            gridPanel.Controls.Add(_emptyStateLabel);
            gridPanel.Controls.Add(_dataGrid);
            gridContainerPanel.Controls.Add(gridPanel);
            _leftSplit.Panel2.Controls.Add(gridContainerPanel);

            // === Context Menu ===
            var contextMenu = new ContextMenuStrip();
            var viewDetailsItem = new ToolStripMenuItem(Resources.ViewDetailsMenu, null, (s, e) =>
            {
                _logger.LogInformation("View account details menu item clicked");
                ShowAccountDetails();
            });
            var editItem = new ToolStripMenuItem(Resources.EditAccountMenu, null, (s, e) =>
            {
                _logger.LogInformation("Edit account menu item clicked");
                EditSelectedAccount();
            });
            var newItem = new ToolStripMenuItem(Resources.NewAccountMenu, null, (s, e) =>
            {
                _logger.LogInformation("Create new account menu item clicked");
                CreateNewAccount();
            });
            var deleteItem = new ToolStripMenuItem(Resources.DeleteAccountMenu, null, (s, e) =>
            {
                _logger.LogInformation("Delete account menu item clicked");
                DeleteSelectedAccount();
            });
            var exportItem = new ToolStripMenuItem(Resources.ExportMenu, null, (s, e) =>
            {
                _logger.LogInformation("Export accounts menu item clicked");
                ExportSelectedAccounts();
            });

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                viewDetailsItem,
                editItem,
                new ToolStripSeparator(),
                newItem,
                deleteItem,
                new ToolStripSeparator(),
                exportItem
            });
            _dataGrid.ContextMenuStrip = contextMenu;

            // Double-click to view details
            _dataGrid.CellDoubleClick += (s, e) => ShowAccountDetails();

            // Configure SfDataGrid columns (bind to AccountsViewModel.MunicipalAccountDisplay)
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = Resources.AccountNumberHeader, Width = 120 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = Resources.AccountNameHeader, Width = 200 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Description", HeaderText = Resources.DescriptionHeader, Width = 250 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Type", HeaderText = Resources.TypeHeader, Width = 100 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Fund", HeaderText = Resources.FundHeader, Width = 100 });
            _dataGrid.Columns.Add(new GridNumericColumn { MappingName = "Balance", HeaderText = Resources.BalanceHeader, Format = "C2", Width = 120 });
            _dataGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetAmount", HeaderText = Resources.BudgetAmountHeader, Format = "C2", Width = 120 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Department", HeaderText = Resources.DepartmentHeader, Width = 150 });
            _dataGrid.Columns.Add(new GridCheckBoxColumn { MappingName = "IsActive", HeaderText = Resources.ActiveHeader, Width = 80 });
            _dataGrid.Columns.Add(new GridCheckBoxColumn { MappingName = "HasParent", HeaderText = Resources.HasParentHeader, Width = 100 });

            // Enable column resizing for better responsiveness
            _dataGrid.AllowResizingColumns = true;

            // Additional properties for complete SfDataGrid implementation
            _dataGrid.AllowGrouping = false;
            // Theme managed globally by SfSkinManager, do not hardcode
            _dataGrid.TabIndex = 0;
            _dataGrid.RightToLeft = RightToLeft.No;

            // Add row highlighting for search matches
            _dataGrid.QueryRowStyle += DataGrid_QueryRowStyle;

            // === Account Detail Panel ===
            _detailPanel = new Panel
            {
                Dock = DockStyle.Fill,  // Changed from Right to Fill for proper right-panel filling in vertical layout
                // Removed Width = 350; allow dynamic sizing
                BackColor = Color.White,
                Padding = new Padding(8),
                BorderStyle = BorderStyle.None
            };

            // Add left border effect
            var borderPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 1,
                BackColor = Color.FromArgb(220, 220, 225)
            };
            _detailPanel.Controls.Add(borderPanel);

            // Detail header with collapse/expand toggle - PROMINENT AND VISIBLE
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(240, 240, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 8, 8, 8)
            };

            var headerLabel = new Label
            {
                Text = "Account Details",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Visible = true
            };

            _toggleDetailButton = new Button
            {
                Text = "«",
                Width = 32,
                Height = 32,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(66, 133, 244),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Visible = true
            };
            _toggleDetailButton.FlatAppearance.BorderSize = 0;
            _toggleDetailButton.Click += (s, e) =>
            {
                try
                {
                    if (_mainSplit != null)
                    {
                        _mainSplit.Panel2Collapsed = !_mainSplit.Panel2Collapsed;
                        _toggleDetailButton.Text = _mainSplit.Panel2Collapsed ? "»" : "«";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Toggle detail panel failed");
                }
            };

            headerPanel.Controls.Add(headerLabel);
            headerPanel.Controls.Add(_toggleDetailButton);
            _detailPanel.Controls.Add(headerPanel);

            // Inline validation panel (non-blocking) shown in the details area
            _validationPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(255, 249, 230),
                Visible = false,
                Padding = new Padding(8)
            };
            _validationLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(94, 53, 177),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                AutoEllipsis = true
            };
            _validationPanel.Controls.Add(_validationLabel);
            _detailPanel.Controls.Add(_validationPanel);

            // Detail content panel with TableLayout
            var detailContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(4, 8, 4, 4),
                AutoScroll = true,
                AutoSize = false
            };
            detailContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            detailContent.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Account Number
            detailContent.Controls.Add(CreateDetailLabel("Account #:"), 0, 0);
            _detailAccountNumber = CreateDetailValue("-");
            detailContent.Controls.Add(_detailAccountNumber, 1, 0);

            // Account Name
            detailContent.Controls.Add(CreateDetailLabel("Name:"), 0, 1);
            _detailAccountName = CreateDetailValue("-");
            detailContent.Controls.Add(_detailAccountName, 1, 1);

            // Balance
            detailContent.Controls.Add(CreateDetailLabel("Balance:"), 0, 2);
            _detailBalance = CreateDetailValue("-");
            _detailBalance.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            detailContent.Controls.Add(_detailBalance, 1, 2);

            // Budget
            detailContent.Controls.Add(CreateDetailLabel("Budget:"), 0, 3);
            _detailBudget = CreateDetailValue("-");
            detailContent.Controls.Add(_detailBudget, 1, 3);

            // Variance
            detailContent.Controls.Add(CreateDetailLabel("Variance:"), 0, 4);
            _detailVariance = CreateDetailValue("-");
            detailContent.Controls.Add(_detailVariance, 1, 4);

            // Fund
            var detailFundLabel = CreateDetailLabel("Fund:");
            detailContent.Controls.Add(detailFundLabel, 0, 5);
            var detailFundValue = CreateDetailValue("-");
            detailFundValue.Name = "detailFund";
            detailContent.Controls.Add(detailFundValue, 1, 5);

            // Department
            var detailDeptLabel = CreateDetailLabel("Department:");
            detailContent.Controls.Add(detailDeptLabel, 0, 6);
            var detailDeptValue = CreateDetailValue("-");
            detailDeptValue.Name = "detailDept";
            detailContent.Controls.Add(detailDeptValue, 1, 6);

            // Status
            var detailStatusLabel = CreateDetailLabel("Status:");
            detailContent.Controls.Add(detailStatusLabel, 0, 7);
            var detailStatusValue = CreateDetailValue("-");
            detailStatusValue.Name = "detailStatus";
            detailContent.Controls.Add(detailStatusValue, 1, 7);

            _detailPanel.Controls.Add(detailContent);

            // Small variance/summary chart (Balance vs Budget) using Syncfusion ChartControl
            _varianceChart = new ChartControl
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Color.White,
                ShowLegend = true,
                LegendsPlacement = ChartPlacement.Outside
            };

            var s = new ChartSeries("Values", ChartSeriesType.Pie);
            s.Points.Add(new ChartPoint(1, 0.0));
            s.Points.Add(new ChartPoint(2, 0.0));
            _varianceChart.Series.Add(s);
            _detailPanel.Controls.Add(_varianceChart);

            // Action buttons panel at bottom of detail panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var editButton = new Button
            {
                Text = "Edit",
                Width = 80,
                Height = 32,
                BackColor = Color.FromArgb(66, 133, 244),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            editButton.FlatAppearance.BorderSize = 0;
            editButton.Click += (s, e) => EditSelectedAccount();

            var viewButton = new Button
            {
                Text = "View",
                Width = 80,
                Height = 32,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            viewButton.FlatAppearance.BorderSize = 0;
            viewButton.Click += (s, e) => ShowAccountDetails();

            buttonPanel.Controls.AddRange(new Control[] { editButton, viewButton });
            _detailPanel.Controls.Add(buttonPanel);

            // Add controls to form: Tree | Grid | Details
            mainSplit.Panel2.Controls.Add(_detailPanel);
            Controls.Add(mainSplit);
        }

        private static Label CreateDetailLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 5)
            };
        }

        private static Label CreateDetailValue(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 5)
            };
        }

        private void DataGrid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dataGrid == null || _detailPanel == null) return;

            // SfDataGrid provides SelectedItems - first item is our bound object
            try
            {
                var sel = _dataGrid.SelectedItems;
                if (sel != null && sel.Count > 0)
                {
                    UpdateDetailPanelFromObject(sel[0]);

                    // Bidirectional sync to TreeView
                    if (!_isSelectingFromTree && sel[0] is MunicipalAccountDisplay disp)
                    {
                        var treeNode = FindTreeNodeByAccount(disp);
                        if (_accountTree != null && treeNode != null)
                        {
                            _accountTree.SelectedNode = treeNode;
                            treeNode.Expand();
                        }
                    }
                }
            }
            catch
            {
                // Fallback for other grid types (not expected here)
            }
        }

        private void UpdateDetailPanelFromObject(object rowOrItem)
        {
            // Handle MunicipalAccountDisplay objects (the ViewModel's display models)
            if (rowOrItem is MunicipalAccountDisplay disp)
            {
                _detailAccountNumber!.Text = disp.AccountNumber ?? "-";
                _detailAccountName!.Text = disp.Name ?? "-";
                if (_detailBalance != null)
                {
                    _detailBalance.Text = disp.Balance.ToString("C2", CultureInfo.CurrentCulture);
                    _detailBalance.ForeColor = disp.Balance >= 0 ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                }

                if (_detailBudget != null) _detailBudget.Text = disp.BudgetAmount.ToString("C2", CultureInfo.CurrentCulture);

                if (_detailVariance != null)
                {
                    var variance = disp.Balance - disp.BudgetAmount;
                    _detailVariance.Text = variance.ToString("C2", CultureInfo.CurrentCulture);
                    _detailVariance.ForeColor = variance >= 0 ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                }

                var detailContent = _detailPanel?.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
                if (detailContent != null)
                {
                    var fundLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailFund");
                    if (fundLabel != null) fundLabel.Text = disp.Fund ?? "-";

                    var deptLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailDept");
                    if (deptLabel != null) deptLabel.Text = disp.Department ?? "-";

                    var statusLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailStatus");
                    if (statusLabel != null)
                    {
                        statusLabel.Text = disp.IsActive ? "Active" : "Inactive";
                        statusLabel.ForeColor = disp.IsActive ? Color.FromArgb(40, 167, 69) : Color.FromArgb(108, 117, 125);
                    }
                }

                    // Update variance pie (Syncfusion ChartControl)
                    if (_varianceChart != null)
                    {
                        try
                        {
                            _varianceChart.Series.Clear();
                            var series = new ChartSeries("Values", ChartSeriesType.Pie);
                            series.Points.Add(new ChartPoint(0, (double)disp.BudgetAmount));
                            series.Points.Add(new ChartPoint(1, (double)disp.Balance));
                            _varianceChart.Series.Add(series);
                            var variance = disp.Balance - disp.BudgetAmount;
                            _varianceChart.Titles.Clear();
                            _varianceChart.Titles.Add(new ChartTitle { Text = $"Variance: {variance.ToString("C2", CultureInfo.CurrentCulture)}" });
                            _varianceChart.ShowLegend = true;
                            _varianceChart.LegendsPlacement = ChartPlacement.Outside;
                            try { _varianceChart.Refresh(); } catch { }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed updating variance chart");
                        }
                    }
                return;
            }

            // This view is SfDataGrid-only. Legacy DataGridView fallbacks removed.
            // If the bound item isn't a MunicipalAccountDisplay, we intentionally do not attempt
            // to reflect arbitrary grid row types into the details panel.
        }

        private void DataGrid_QueryRowStyle(object? sender, QueryRowStyleEventArgs e)
        {
            if (e.RowData is MunicipalAccountDisplay account && !string.IsNullOrWhiteSpace(_viewModel.SearchText))
            {
                var searchTerm = _viewModel.SearchText.Trim();
                var name = account.Name ?? string.Empty;
                var accountNumber = account.AccountNumber ?? string.Empty;

                if (name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    accountNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    // Highlight search matches with light yellow background
                    e.Style.BackColor = Color.FromArgb(255, 255, 224); // Light yellow
                    e.Style.Font.Bold = true;
                }
            }
        }

        private async void DeleteSelectedAccount()
        {
            if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
            {
                var item = _dataGrid.SelectedItems[0];

                if (item is MunicipalAccountDisplay disp)
                {
                    _logger.LogInformation("Delete confirmation requested for account {AccountNumber}", disp.AccountNumber);

                    var confirmed = WileyWidget.WinForms.Dialogs.DeleteConfirmationDialog.Show(
                        this,
                        "Delete Account",
                        "Are you sure you want to delete this account?",
                        $"{disp.AccountNumber} - {disp.Name}",
                        null);

                    if (confirmed)
                    {
                        try
                        {
                            _logger.LogInformation("Deleting account {Id}", disp.Id);
                            var success = await _viewModel.DeleteAccountAsync(disp.Id);

                            if (success)
                            {
                                _logger.LogInformation("Account {AccountNumber} deleted successfully", disp.AccountNumber);
                                // Refresh UI and tree
                                await LoadData();
                            }
                            else
                            {
                                _logger.LogWarning("Failed to delete account {AccountNumber}", disp.AccountNumber);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting account {AccountNumber}", disp.AccountNumber);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Account deletion canceled by user");
                    }
                }
            }
        }

        private void PopulateTreeFromHierarchy()
        {
            if (_accountTree == null) return;

            _accountTree.BeginUpdate();
            try
            {
                _accountTree.Nodes.Clear();
                foreach (var root in _viewModel.HierarchicalAccounts)
                {
                    var rootNode = CreateTreeNode(root);
                    _accountTree.Nodes.Add(rootNode);
                }
                _accountTree.ExpandAll();
            }
            finally
            {
                _accountTree.EndUpdate();
            }
        }

        private TreeNode CreateTreeNode(AccountsViewModel.HierarchicalAccountNode node)
        {
            var label = node.Account != null ? $"{node.Account.AccountNumber} - {node.Account.Name}" : "(Unknown)";
            var tn = new TreeNode(label) { Tag = node.Account };
            foreach (var child in node.Children)
            {
                tn.Nodes.Add(CreateTreeNode(child));
            }
            return tn;
        }

        private TreeNode? FindTreeNodeByAccount(MunicipalAccountDisplay account)
        {
            if (_accountTree == null) return null;
            return FindTreeNodeRecursive(_accountTree.Nodes, account);
        }

        private TreeNode? FindTreeNodeRecursive(TreeNodeCollection nodes, MunicipalAccountDisplay account)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is MunicipalAccountDisplay disp && disp.Id == account.Id)
                {
                    return node;
                }
                var found = FindTreeNodeRecursive(node.Nodes, account);
                if (found != null) return found;
            }
            return null;
        }

        private void ShowAccountDetails()
        {
            if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
            {
                var item = _dataGrid.SelectedItems[0];
                if (item is MunicipalAccountDisplay disp)
                {
                    _logger.LogWarning("Full account details view coming soon for account {AccountNumber} ({Name})", disp.AccountNumber, disp.Name);
                    if (Application.MessageLoop)
                    {
                        MessageBox.Show($"{disp.AccountNumber} - {disp.Name}\n\nBalance: {disp.Balance:C}\nBudget: {disp.BudgetAmount:C}", "Account Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    _logger.LogDebug("Selected item is not a MunicipalAccountDisplay; cannot show typed details");
                }
            }
        }

        private async void EditSelectedAccount()
        {
            if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
            {
                var item = _dataGrid.SelectedItems[0];

                if (item is MunicipalAccountDisplay disp)
                {
                    _logger.LogInformation("Opening edit dialog for account {AccountNumber}", disp.AccountNumber);

                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var context = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Data.AppDbContext>(scope.ServiceProvider);
                        var account = await context.MunicipalAccounts
                            .FirstOrDefaultAsync(a => a.Id == disp.Id, _cts?.Token ?? CancellationToken.None);

                        if (account == null)
                        {
                            _logger.LogWarning("Account {Id} not found for editing", disp.Id);
                            return;
                        }

                        var accountService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Services.Abstractions.IAccountService>(scope.ServiceProvider);
                        var deptRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IDepartmentRepository>(scope.ServiceProvider);
                        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scope.ServiceProvider);
                        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<WileyWidget.WinForms.Dialogs.AccountEditDialog>>(scope.ServiceProvider);

                        var dialog = new WileyWidget.WinForms.Dialogs.AccountEditDialog(
                            accountService, deptRepo, scopeFactory, logger, account);

                        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.EditedAccount != null)
                        {
                            _logger.LogInformation("Account {AccountNumber} edited successfully", dialog.EditedAccount.AccountNumber?.Value);
                            await LoadData();  // Refresh grid
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to edit account {AccountNumber}", disp.AccountNumber);
                    }
                }
            }
        }

        private async void ExportSelectedAccounts()
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|PDF Files (*.pdf)|*.pdf|CSV Files (*.csv)|*.csv",
                Title = "Export Accounts",
                FileName = $"Accounts_Export_{DateTime.Now:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var path = saveDialog.FileName;
                    // Prefer CSV export for now
                    var accountsToExport = new List<MunicipalAccountDisplay>();
                    if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
                    {
                        foreach (var it in _dataGrid.SelectedItems)
                        {
                            if (it is MunicipalAccountDisplay d) accountsToExport.Add(d);
                        }
                    }
                    if (accountsToExport.Count == 0)
                    {
                        // export all
                        accountsToExport.AddRange(_viewModel.Accounts);
                    }
                    // Use typed exporter (ClosedXML) for XLSX; fallback to CSV
                    var exporter = new AccountsExporter(_logger);
                    var didXlsx = false;
                    var didPdf = false;
                    if (saveDialog.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        // Async XLSX export with progress and cancellation
                        var progressDialogX = new WileyWidget.WinForms.Forms.ProgressDialog("Exporting Accounts", "Preparing XLSX export...");
                        using var localCtsX = new CancellationTokenSource();
                        using var linkedCtsX = CancellationTokenSource.CreateLinkedTokenSource(localCtsX.Token, _cts?.Token ?? CancellationToken.None);
                        var progressX = new Progress<int>(p =>
                        {
                            try
                            {
                                if (p >= 92)
                                {
                                    progressDialogX.SetStatus("Saving file...");
                                    progressDialogX.SetIndeterminate(true);
                                }
                                else
                                {
                                    progressDialogX.SetIndeterminate(false);
                                    progressDialogX.SetStatus($"Writing rows... {p}%");
                                    progressDialogX.SetProgress(p);
                                }
                            }
                            catch { }
                        });

                        try
                        {
                            this.Enabled = false;
                            progressDialogX.Show(this);

                            var watchTaskX = Task.Run(async () =>
                            {
                                try
                                {
                                    while (!progressDialogX.IsDisposed && !progressDialogX.IsCancelled)
                                    {
                                        await Task.Delay(200).ConfigureAwait(false);
                                    }
                                    if (progressDialogX.IsCancelled)
                                    {
                                        try { localCtsX.Cancel(); } catch { }
                                    }
                                }
                                catch { }
                            });

                            // Write to a temporary file first, then atomically move into place
                            var directory = Path.GetDirectoryName(path) ?? Path.GetTempPath();
                            var tempPath = Path.Combine(directory, Path.GetFileName(path) + $".tmp-{Guid.NewGuid():N}");
                                using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                                {
                                    await exporter.ExportToXlsxAsync(fs, accountsToExport, linkedCtsX.Token, progressX).ConfigureAwait(false);
                                    await fs.FlushAsync().ConfigureAwait(false);
                                }

                                // Attempt atomic replace, fall back to move
                                try
                                {
                                    if (File.Exists(path))
                                    {
                                        File.Replace(tempPath, path, null);
                                    }
                                    else
                                    {
                                        File.Move(tempPath, path);
                                    }
                                }
                                catch (Exception)
                                {
                                    // Best-effort fallback
                                    File.Move(tempPath, path, true);
                                }

                                didXlsx = true;
                            _logger.LogInformation("Exported {Count} accounts to {Path}", accountsToExport.Count, path);
                            if (Application.MessageLoop)
                            {
                                MessageBox.Show(this, $"Exported {accountsToExport.Count} accounts to:\n{path}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }

                            try { await Task.WhenAny(watchTaskX).ConfigureAwait(false); } catch { }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogInformation("XLSX export canceled by user");
                            didXlsx = false;
                            if (Application.MessageLoop)
                            {
                                MessageBox.Show(this, "Export canceled.", "Export Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Typed XLSX export failed; falling back to CSV");
                            didXlsx = false;
                        }
                        finally
                        {
                            try
                            {
                                if (progressDialogX != null && !progressDialogX.IsDisposed)
                                {
                                    progressDialogX.Close();
                                    progressDialogX.Dispose();
                                }
                            }
                            catch { }
                            this.Enabled = true;
                        }
                    }

                    if (saveDialog.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        // Async PDF export with progress and cancellation
                        var progressDialogPdf = new WileyWidget.WinForms.Forms.ProgressDialog("Exporting Accounts", "Preparing PDF export...");
                        using var localCtsPdf = new CancellationTokenSource();
                        using var linkedCtsPdf = CancellationTokenSource.CreateLinkedTokenSource(localCtsPdf.Token, _cts?.Token ?? CancellationToken.None);
                        var progressPdf = new Progress<int>(p =>
                        {
                            try
                            {
                                if (p >= 92)
                                {
                                    progressDialogPdf.SetStatus("Saving file...");
                                    progressDialogPdf.SetIndeterminate(true);
                                }
                                else
                                {
                                    progressDialogPdf.SetIndeterminate(false);
                                    progressDialogPdf.SetStatus($"Writing rows... {p}%");
                                    progressDialogPdf.SetProgress(p);
                                }
                            }
                            catch { }
                        });

                        try
                        {
                            this.Enabled = false;
                            progressDialogPdf.Show(this);

                            var watchTaskPdf = Task.Run(async () =>
                            {
                                try
                                {
                                    while (!progressDialogPdf.IsDisposed && !progressDialogPdf.IsCancelled)
                                    {
                                        await Task.Delay(200).ConfigureAwait(false);
                                    }
                                    if (progressDialogPdf.IsCancelled)
                                    {
                                        try { localCtsPdf.Cancel(); } catch { }
                                    }
                                }
                                catch { }
                            });

                            // Write to a temporary file first, then atomically move into place
                            var directory = Path.GetDirectoryName(path) ?? Path.GetTempPath();
                            var tempPath = Path.Combine(directory, Path.GetFileName(path) + $".tmp-{Guid.NewGuid():N}");
                                using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                                {
                                    await exporter.ExportToPdfAsync(fs, accountsToExport, linkedCtsPdf.Token, progressPdf).ConfigureAwait(false);
                                    await fs.FlushAsync().ConfigureAwait(false);
                                }

                                // Attempt atomic replace, fall back to move
                                try
                                {
                                    if (File.Exists(path))
                                    {
                                        File.Replace(tempPath, path, null);
                                    }
                                    else
                                    {
                                        File.Move(tempPath, path);
                                    }
                                }
                                catch (Exception)
                                {
                                    // Best-effort fallback
                                    File.Move(tempPath, path, true);
                                }

                                didPdf = true;
                            _logger.LogInformation("Exported {Count} accounts to {Path}", accountsToExport.Count, path);
                            if (Application.MessageLoop)
                            {
                                MessageBox.Show(this, $"Exported {accountsToExport.Count} accounts to:\n{path}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }

                            try { await Task.WhenAny(watchTaskPdf).ConfigureAwait(false); } catch { }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogInformation("PDF export canceled by user");
                            didPdf = false;
                            if (Application.MessageLoop)
                            {
                                MessageBox.Show(this, "Export canceled.", "Export Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "PDF export failed; falling back to CSV");
                            didPdf = false;
                        }
                        finally
                        {
                            try
                            {
                                if (progressDialogPdf != null && !progressDialogPdf.IsDisposed)
                                {
                                    progressDialogPdf.Close();
                                    progressDialogPdf.Dispose();
                                }
                            }
                            catch { }
                            this.Enabled = true;
                        }
                    }

                    if (!didXlsx && !didPdf)
                    {
                        var csvPath = saveDialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                            ? saveDialog.FileName
                            : Path.ChangeExtension(saveDialog.FileName, ".csv");

                        // Async CSV export with progress and cancellation
                        var progressDialog = new WileyWidget.WinForms.Forms.ProgressDialog("Exporting Accounts", "Preparing export...");

                        // Create a linked cancellation token source so we can cancel from dialog or form-level CTS
                        using var localCts = new CancellationTokenSource();
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token, _cts?.Token ?? CancellationToken.None);

                        var progress = new Progress<int>(p =>
                        {
                            try
                            {
                                if (p >= 92)
                                {
                                    progressDialog.SetStatus("Saving file...");
                                    progressDialog.SetIndeterminate(true);
                                }
                                else
                                {
                                    progressDialog.SetIndeterminate(false);
                                    progressDialog.SetStatus($"Writing rows... {p}%");
                                    progressDialog.SetProgress(p);
                                }
                            }
                            catch { }
                        });

                        try
                        {
                            // Disable the UI while exporting
                            this.Enabled = false;

                            // Show progress dialog modelessly (parented)
                            progressDialog.Show(this);

                            // Watch for user cancel clicks and cancel the local token when requested
                            var watchTask = Task.Run(async () =>
                            {
                                try
                                {
                                    while (!progressDialog.IsDisposed && !progressDialog.IsCancelled)
                                    {
                                        await Task.Delay(200).ConfigureAwait(false);
                                    }
                                    if (progressDialog.IsCancelled)
                                    {
                                        try { localCts.Cancel(); } catch { }
                                    }
                                }
                                catch { }
                            });

                            // Write CSV to a temp file first, then move into place atomically
                            var csvDir = Path.GetDirectoryName(csvPath) ?? Path.GetTempPath();
                            var csvTemp = Path.Combine(csvDir, Path.GetFileName(csvPath) + $".tmp-{Guid.NewGuid():N}");
                            try
                            {
                                using (var fs = new FileStream(csvTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                                {
                                    await CsvHelperExporter.ExportToCsvAsync(accountsToExport, fs, linkedCts.Token, progress).ConfigureAwait(false);
                                    await fs.FlushAsync().ConfigureAwait(false);
                                }

                                try
                                {
                                    if (File.Exists(csvPath))
                                    {
                                        File.Replace(csvTemp, csvPath, null);
                                    }
                                    else
                                    {
                                        File.Move(csvTemp, csvPath);
                                    }
                                }
                                catch (Exception)
                                {
                                    File.Move(csvTemp, csvPath, true);
                                }

                                _logger.LogInformation("Exported {Count} accounts to {Path}", accountsToExport.Count, csvPath);
                                if (Application.MessageLoop)
                                {
                                    MessageBox.Show(this, $"Exported {accountsToExport.Count} accounts to:\n{csvPath}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            finally
                            {
                                try { if (File.Exists(csvTemp)) File.Delete(csvTemp); } catch { }
                            }

                            // Ensure watcher finishes
                            try { await Task.WhenAny(watchTask).ConfigureAwait(false); } catch { }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogInformation("CSV export canceled by user");
                            if (Application.MessageLoop)
                            {
                                MessageBox.Show(this, "Export canceled.", "Export Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed exporting accounts to CSV");
                            if (Application.MessageLoop)
                            {
                                MessageBox.Show(this, $"Failed to export accounts: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        finally
                        {
                            try
                            {
                                if (progressDialog != null && !progressDialog.IsDisposed)
                                {
                                    progressDialog.Close();
                                    progressDialog.Dispose();
                                }
                            }
                            catch { }
                            // Restore UI
                            this.Enabled = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed exporting accounts");
                    if (Application.MessageLoop)
                    {
                        MessageBox.Show(this, $"Failed to export accounts: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }



        private async Task LoadData()
        {
            try
            {
                await _viewModel.LoadAccountsCommand.ExecuteAsync(CancellationToken.None);
                _dataGrid!.DataSource = new BindingSource { DataSource = _viewModel.Accounts };
                // Populate tree view from the ViewModel hierarchical collection (if available)
                PopulateTreeFromHierarchy();

                // Handle empty state
                if (_emptyStateLabel != null)
                {
                    _emptyStateLabel.Visible = _viewModel.Accounts.Count == 0;
                    _dataGrid.Visible = _viewModel.Accounts.Count > 0;
                }

                // Ensure at least the first row is selected for better UX
                try
                {
                    if (_dataGrid.SelectedIndex < 0 && _dataGrid.DataSource is BindingSource bs && bs.Count > 0)
                    {
                        _dataGrid.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Unable to auto-select first row after load");
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug(oce, "Account loading was canceled (likely due to form close or app shutdown)");
                // Don't show a dialog on cancellation — this is expected behavior during shutdown or quick navigation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading accounts in AccountsForm");
                if (Application.MessageLoop)
                {
                    MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.LoadErrorMessage, ex.Message), Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dataGrid?.Dispose();
                _detailPanel?.Dispose();
                _detailAccountNumber?.Dispose();
                _detailAccountName?.Dispose();
                _detailBalance?.Dispose();
                _detailBudget?.Dispose();
                _detailVariance?.Dispose();
                _fundCombo?.Dispose();
                _typeCombo?.Dispose();
                _searchBox?.Dispose();
                _searchTimer?.Stop();
                _searchTimer?.Dispose();
                _validationPanel?.Dispose();
                _validationLabel?.Dispose();
                _emptyStateLabel?.Dispose();
                _leftSplit?.Dispose();

                // Cancel and dispose async operations
                Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
            }
            base.Dispose(disposing);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Recalculate splitter distances now that the form is fully loaded and sized
            RecalculateSplitterDistances();
        }

        private void RecalculateSplitterDistances()
        {
            // Improve responsiveness: adjust splitter distances based on form size
            if (_mainSplit != null && Width > 0)
            {
                // Main split: left (tree+grid) takes 70%, right (details) takes 30%
                int mainSplitterDistance = (int)(Width * 0.7);
                if (mainSplitterDistance > 0 && mainSplitterDistance < Width - 100)
                {
                    _mainSplit.SplitterDistance = mainSplitterDistance;
                }

                // Left split: tree takes 30% of left panel
                if (_leftSplit != null && _mainSplit.Panel1.Width > 0)
                {
                    int leftSplitterDistance = (int)(_mainSplit.Panel1.Width * 0.3);
                    if (leftSplitterDistance > 0 && leftSplitterDistance < _mainSplit.Panel1.Width - 50)
                    {
                        _leftSplit.SplitterDistance = leftSplitterDistance;
                    }
                }
            }

            // Set PanelMinSize to prevent collapse
            if (_mainSplit != null)
            {
                _mainSplit.Panel1MinSize = 400;  // Min width for tree/grid
                _mainSplit.Panel2MinSize = 300;  // Min width for details
            }
            if (_leftSplit != null)
            {
                _leftSplit.Panel1MinSize = 200;  // Min width for tree
                _leftSplit.Panel2MinSize = 400;  // Min width for grid
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateSplitterDistances();
        }
    }
}
