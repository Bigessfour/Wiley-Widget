#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Data;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Helpers;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utilities;
using GridComboBoxColumn = Syncfusion.WinForms.DataGrid.GridComboBoxColumn;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for displaying and managing payments (check register)
/// </summary>
public partial class PaymentsPanel : ScopedPanelBase<PaymentsViewModel>
{
    private const string StatusSymbolHeaderText = "Flag";

    private SfDataGrid _paymentsGrid = null!;
    private GridComboBoxColumn _budgetAccountColumn = null!;
    private SfButton _btnAdd = null!;
    private SfButton _btnEdit = null!;
    private SfButton _btnDelete = null!;
    private SfButton _btnRefresh = null!;
    private SfButton _btnReconcile = null!;
    private SfButton _btnExport = null!;
    private TextBoxExt _txtSearch = null!;
    private PanelHeader _panelHeader = null!;
    private Label _statusLabel = null!;
    private ToolTip _toolTip = null!;

    public PaymentsPanel(IServiceScopeFactory scopeFactory, ILogger<PaymentsPanel> logger)
        : base(scopeFactory, logger)
    {
        SafeSuspendAndLayout(InitializeControls);

        if (ViewModel != null)
        {
            BindViewModel();
        }
    }

    protected override void OnViewModelResolved(PaymentsViewModel? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel == null)
        {
            return;
        }

        if (_paymentsGrid != null)
        {
            BindViewModel();
        }
    }

    public override Task LoadAsync(CancellationToken ct = default) => LoadDataAsync(ct);

    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SetBusyState(isBusy: true, statusMessage: "Loading payments…");

            if (ViewModel == null)
            {
                Logger?.LogError("PaymentsPanel: ViewModel is null");
                SetStatusMessage("Unable to load payments.");
                return;
            }

            EnsureGridBinding();
            EnsureBudgetAccountColumnDataSource();

            // Load payments
            await ViewModel.LoadPaymentsCommand.ExecuteAsync(null);

            EnsureGridBinding(forceRebindWhenOutOfSync: true);
            EnsureBudgetAccountColumnDataSource();

            // CRITICAL FIX: Force grid refresh after loading data
            // The grid may not auto-refresh even though Payments is ObservableCollection
            if (_paymentsGrid?.View != null)
            {
                if (string.IsNullOrWhiteSpace(_txtSearch?.Text))
                {
                    _paymentsGrid.View.Filter = null;
                    _paymentsGrid.View.RefreshFilter();
                }
                else
                {
                    ApplySearchFilter();
                }

                _paymentsGrid.View.Refresh();
                Logger?.LogDebug("PaymentsPanel: Grid refreshed after loading {Count} payments", ViewModel.Payments.Count);
            }

            var loadedCount = ViewModel.Payments.Count;
            SetStatusMessage(loadedCount == 1 ? "Loaded 1 payment." : $"Loaded {loadedCount} payments.");
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: LoadDataAsync failed");
            SetStatusMessage("Unable to load payments. See logs for details.");
            IsLoaded = false;
        }
        finally
        {
            SetBusyState(isBusy: false);
        }
    }

    private void BindViewModel()
    {
        EnsureGridBinding(forceRebindWhenOutOfSync: true);
        EnsureBudgetAccountColumnDataSource();
        UpdateButtonStates();
    }

    private void InitializeControls()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(this, themeName);

        Name = "PaymentsPanel";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Size = ScaleLogicalToDevice(new Size(1180, 760));
        MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
        Dock = DockStyle.Fill;

        var headerRowHeight = LayoutTokens.GetScaled(LayoutTokens.HeaderMinimumHeight);
        var statusRowHeight = LayoutTokens.GetScaled(36);
        var compactPadding = LayoutTokens.PanelPaddingCompact;

        var mainLayout = new TableLayoutPanel();
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.RowCount = 4;
        mainLayout.ColumnCount = 1;
        mainLayout.Padding = new System.Windows.Forms.Padding(
            LayoutTokens.GetScaled(compactPadding.Left),
            LayoutTokens.GetScaled(compactPadding.Top),
            LayoutTokens.GetScaled(compactPadding.Right),
            LayoutTokens.GetScaled(compactPadding.Bottom));
        mainLayout.Margin = System.Windows.Forms.Padding.Empty;
        mainLayout.AutoSize = false;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var toolbarHeight = LayoutTokens.GetScaled(92);
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, headerRowHeight)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, toolbarHeight)); // Toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, statusRowHeight)); // Status

        _panelHeader = ControlFactory.CreatePanelHeader(header =>
        {
            header.Title = "Payments";
            header.Dock = DockStyle.Fill;
            header.ShowPinButton = false;
            header.ShowHelpButton = false;
            header.ShowCloseButton = false;
            header.ShowRefreshButton = true;
        });
        _panelHeader.RefreshClicked += PanelHeader_RefreshClicked;
        mainLayout.Controls.Add(_panelHeader, 0, 0);

        _toolTip = ControlFactory.CreateToolTip(toolTip =>
        {
            toolTip.AutoPopDelay = 8000;
            toolTip.InitialDelay = 250;
            toolTip.ReshowDelay = 100;
            toolTip.ShowAlways = true;
        });

        // Toolbar
        var toolbar = CreateToolbar();
        mainLayout.Controls.Add(toolbar, 0, 1);

        // Grid
        _paymentsGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowEditing = true;
            grid.AllowDeleting = false;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AllowFiltering = true;
            grid.SelectionMode = Syncfusion.WinForms.DataGrid.Enums.GridSelectionMode.Single;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.Fill;
            grid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightExtraTall);
            grid.AccessibleName = "Payments Grid";
        }).PreventStringRelationalFilters(_logger, "Status", "CheckNumber", "Payee", "Description");

        // Status icon column
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Status),
            HeaderText = StatusSymbolHeaderText,
            MinimumWidth = 56,
            Width = 64,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowEditing = false,
            AllowSorting = false,
            AllowFiltering = false
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.CheckNumber),
            HeaderText = "Check #",
            MinimumWidth = 88,
            Width = 96,
            AllowEditing = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells
        });
        _paymentsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = nameof(Payment.PaymentDate),
            HeaderText = "Date",
            Format = "d",
            MinimumWidth = 96,
            Width = 104,
            AllowEditing = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Payee),
            HeaderText = "Payee",
            MinimumWidth = 180,
            Width = 208,
            AllowEditing = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill
        });
        _paymentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(Payment.Amount),
            HeaderText = "Amount",
            Format = "C2",
            MinimumWidth = 96,
            Width = 104,
            AllowEditing = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Description),
            HeaderText = "Description",
            MinimumWidth = 180,
            Width = 200,
            AllowEditing = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill
        });
        _budgetAccountColumn = new GridComboBoxColumn
        {
            MappingName = nameof(Payment.MunicipalAccountId),
            HeaderText = "Budget Account",
            MinimumWidth = 176,
            Width = 196,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            AllowEditing = true,
            AllowSorting = true,
            AllowFiltering = true,
            DataSource = ViewModel?.BudgetAccountOptions,
            DisplayMember = nameof(PaymentBudgetAccountOption.Display),
            ValueMember = nameof(PaymentBudgetAccountOption.AccountId)
        };
        _paymentsGrid.Columns.Add(_budgetAccountColumn);
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.BudgetPostingDisplay),
            HeaderText = "Budget Posting",
            MinimumWidth = 144,
            Width = 168,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            AllowEditing = false,
            AllowSorting = true,
            AllowFiltering = true
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Status),
            HeaderText = "Status",
            MinimumWidth = 88,
            Width = 96,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowEditing = false,
            AllowSorting = true,
            AllowFiltering = true
        });
        _paymentsGrid.Columns.Add(new GridCheckBoxColumn
        {
            MappingName = nameof(Payment.IsCleared),
            HeaderText = "Cleared",
            MinimumWidth = 80,
            Width = 88,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowEditing = false // Read-only indicator, synced with Status field
        });

        // Apply custom cell styling for color-coding
        _paymentsGrid.QueryCellStyle += Grid_QueryCellStyle;
        _paymentsGrid.CurrentCellEndEdit += PaymentsGrid_CurrentCellEndEdit;
        _paymentsGrid.ToolTipOpening += PaymentsGrid_ToolTipOpening;

        // Enable grouping and show group area for advanced users
        _paymentsGrid.AllowGrouping = true;
        _paymentsGrid.ShowGroupDropArea = false;

        // Header and row sizing for readability
        _paymentsGrid.HeaderRowHeight = LayoutTokens.GetScaled(LayoutTokens.GridHeaderRowHeightComfortable);
        _paymentsGrid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightExtraTall);

        // Add summary row for total cleared payments
        var summaryRow = new GridTableSummaryRow
        {
            ShowSummaryInRow = true,
            Title = "Total Cleared: {ClearedAmount} ({Count} payments)",
            Position = VerticalPosition.Bottom
        };

        // Custom summary for cleared payments only
        var clearedSummary = new GridSummaryColumn
        {
            Name = "ClearedAmount",
            MappingName = nameof(Payment.Amount),
            SummaryType = SummaryType.Custom,
            Format = "{Sum:C2}"
        };
        clearedSummary.CustomAggregate = new ClearedPaymentAggregate();
        summaryRow.SummaryColumns.Add(clearedSummary);

        summaryRow.SummaryColumns.Add(new GridSummaryColumn
        {
            Name = "Count",
            MappingName = nameof(Payment.Id),
            SummaryType = SummaryType.CountAggregate,
            Format = "{Count}"
        });
        _paymentsGrid.TableSummaryRows.Add(summaryRow);

        _paymentsGrid.SelectionChanged += (s, e) => UpdateButtonStates();
        _paymentsGrid.CellClick += Grid_CellClick;
        _paymentsGrid.KeyDown += Grid_KeyDown;  // Keyboard shortcuts

        EnsureGridBinding();

        mainLayout.Controls.Add(_paymentsGrid, 0, 2);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = "Ready",
            Padding = new Padding(LayoutTokens.GetScaled(6), 0, 0, 0),
            AccessibleName = "Payments Status"
        };
        mainLayout.Controls.Add(_statusLabel, 0, 3);

        Controls.Add(mainLayout);
        ApplyProfessionalPanelLayout();

        SetStatusMessage("Ready");
    }

    private void EnsureGridBinding(bool forceRebindWhenOutOfSync = false)
    {
        if (ViewModel == null || _paymentsGrid == null)
        {
            return;
        }

        EnsureBudgetAccountColumnDataSource();

        if (!ReferenceEquals(_paymentsGrid.DataSource, ViewModel.Payments))
        {
            _paymentsGrid.DataSource = ViewModel.Payments;
            Logger?.LogDebug("PaymentsPanel: Bound grid DataSource to Payments collection ({Count} items)", ViewModel.Payments.Count);
            return;
        }

        if (!forceRebindWhenOutOfSync || _paymentsGrid.View == null)
        {
            return;
        }

        var hasSearchFilter = !string.IsNullOrWhiteSpace(_txtSearch?.Text);
        var visibleRecords = _paymentsGrid.View.Records?.Count ?? 0;

        if (!hasSearchFilter && ViewModel.Payments.Count > 0 && visibleRecords == 0)
        {
            _paymentsGrid.DataSource = null;
            _paymentsGrid.DataSource = ViewModel.Payments;
            _paymentsGrid.View.Refresh();
            Logger?.LogWarning("PaymentsPanel: Rebound DataSource because grid records were out of sync with loaded payments ({Count})", ViewModel.Payments.Count);
        }
    }

    private void EnsureBudgetAccountColumnDataSource()
    {
        if (ViewModel == null || _budgetAccountColumn == null)
        {
            return;
        }

        if (!ReferenceEquals(_budgetAccountColumn.DataSource, ViewModel.BudgetAccountOptions))
        {
            _budgetAccountColumn.DataSource = ViewModel.BudgetAccountOptions;
        }
    }

    private Control CreateToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = false
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        var actionRowHeight = LayoutTokens.GetScaled(48);
        var searchRowHeight = LayoutTokens.GetScaled(44);
        toolbar.MinimumSize = new Size(0, actionRowHeight + searchRowHeight);
        toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, actionRowHeight));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, searchRowHeight));

        var actionStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, LayoutTokens.GetScaled(8), LayoutTokens.GetScaled(12), LayoutTokens.GetScaled(8)),
            Margin = new Padding(0),
            WrapContents = false,
            AutoScroll = false,
            AutoSize = false,
            MinimumSize = new Size(0, actionRowHeight)
        };

        var searchStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, LayoutTokens.GetScaled(8)),
            Margin = new Padding(0),
            MinimumSize = new Size(0, searchRowHeight),
        };
        searchStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        searchStrip.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var searchLabel = new Label
        {
            Text = "&Quick Filter:",
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0, LayoutTokens.GetScaled(10), LayoutTokens.GetScaled(8), 0)
        };

        var toolbarButtonHeight = LayoutTokens.GetScaled(LayoutTokens.ToolbarButtonHeight);
        var toolbarButtonSpacing = LayoutTokens.GetScaled(8);

        // Helper to load icon from resources
        System.Drawing.Image? LoadIcon(string iconName)
        {
            try
            {
                var type = typeof(PaymentsPanel);
                var stream =
                    type.Assembly.GetManifestResourceStream(
                        $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}flatflat.png") ??
                    type.Assembly.GetManifestResourceStream(
                        $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}.png") ??
                    type.Assembly.GetManifestResourceStream(
                        $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}flat.png");

                if (stream != null)
                    return System.Drawing.Image.FromStream(stream);
            }
            catch { }
            return null;
        }

        // Add Payment button - green icon
        _btnAdd = ControlFactory.CreateSfButton("&Add Payment", button =>
        {
            button.Width = LayoutTokens.GetScaled(152);
            button.Height = toolbarButtonHeight;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.AccessibleName = "Add Payment";
            button.Margin = new Padding(0, 0, toolbarButtonSpacing, 0);
        });
        _btnAdd.Image = LoadIcon("New32");
        _btnAdd.Click += BtnAdd_Click;
        _toolTip.SetToolTip(_btnAdd, "Create a new payment (Ctrl+N)");
        actionStrip.Controls.Add(_btnAdd);

        // Edit button
        _btnEdit = ControlFactory.CreateSfButton("&Edit", button =>
        {
            button.Width = LayoutTokens.GetScaled(116);
            button.Height = toolbarButtonHeight;
            button.Enabled = false;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.AccessibleName = "Edit Payment";
            button.Margin = new Padding(0, 0, toolbarButtonSpacing, 0);
        });
        _btnEdit.Image = LoadIcon("Edit32");
        _btnEdit.Click += BtnEdit_Click;
        _toolTip.SetToolTip(_btnEdit, "Edit selected payment (Enter or F2)");
        actionStrip.Controls.Add(_btnEdit);

        // Delete button
        _btnDelete = ControlFactory.CreateSfButton("&Delete", button =>
        {
            button.Width = LayoutTokens.GetScaled(116);
            button.Height = toolbarButtonHeight;
            button.Enabled = false;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.AccessibleName = "Delete Payment";
            button.Margin = new Padding(0, 0, toolbarButtonSpacing, 0);
        });
        _btnDelete.Image = LoadIcon("Delete32");
        _btnDelete.Click += BtnDelete_Click;
        _toolTip.SetToolTip(_btnDelete, "Delete selected payment (Delete)");
        actionStrip.Controls.Add(_btnDelete);

        // Refresh button
        _btnRefresh = ControlFactory.CreateSfButton("&Refresh", button =>
        {
            button.Width = LayoutTokens.GetScaled(124);
            button.Height = toolbarButtonHeight;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.AccessibleName = "Refresh Payments";
            button.Margin = new Padding(0, 0, toolbarButtonSpacing, 0);
        });
        _btnRefresh.Image = LoadIcon("Refresh32");
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();
        _toolTip.SetToolTip(_btnRefresh, "Reload payments from the data source (F5)");
        actionStrip.Controls.Add(_btnRefresh);

        _btnReconcile = ControlFactory.CreateSfButton("&Reconcile Budget", button =>
        {
            button.Width = LayoutTokens.GetScaled(188);
            button.Height = toolbarButtonHeight;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.AccessibleName = "Reconcile Payment Budget Mapping";
            button.Margin = new Padding(0, 0, toolbarButtonSpacing, 0);
        });
        _btnReconcile.Image = LoadIcon("Refresh32");
        _btnReconcile.Click += BtnReconcile_Click;
        _toolTip.SetToolTip(_btnReconcile, "Link payments to budget lines by the selected account number and refresh budget actuals.");
        actionStrip.Controls.Add(_btnReconcile);

        // Export to Excel button
        _btnExport = ControlFactory.CreateSfButton("E&xport to Excel", button =>
        {
            button.Width = LayoutTokens.GetScaled(168);
            button.Height = toolbarButtonHeight;
            button.Enabled = false;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.AccessibleName = "Export Payments";
            button.Margin = new Padding(0, 0, toolbarButtonSpacing, 0);
        });
        _btnExport.Image = LoadIcon("Excel32");
        _btnExport.Click += BtnExport_Click;
        _toolTip.SetToolTip(_btnExport, "Export current grid data to Excel (Ctrl+E)");
        actionStrip.Controls.Add(_btnExport);

        searchStrip.Controls.Add(searchLabel, 0, 0);

        _txtSearch = ControlFactory.CreateTextBoxExt(textBox =>
        {
            textBox.Dock = DockStyle.Fill;
            textBox.MinimumSize = LayoutTokens.GetScaled(new Size(300, LayoutTokens.StandardControlHeightLarge));
            textBox.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
            textBox.PlaceholderText = "Filter payee, check #, account, posting, status, or description";
            textBox.Padding = LayoutTokens.GetScaled(LayoutTokens.ToolbarPadding);
            textBox.AccessibleName = "Search Payments";
            textBox.Margin = new Padding(0);
        });
        _txtSearch.TextChanged += (s, e) => ApplySearchFilter();
        _toolTip.SetToolTip(_txtSearch, "Filter by payee, check number, account, posting, status, or description (Ctrl+F)");
        searchStrip.Controls.Add(_txtSearch, 1, 0);

        toolbar.Controls.Add(actionStrip, 0, 0);
        toolbar.Controls.Add(searchStrip, 0, 1);

        return toolbar;
    }

    private async void PanelHeader_RefreshClicked(object? sender, EventArgs e)
    {
        await LoadDataAsync();
    }

    private async void BtnReconcile_Click(object? sender, EventArgs e)
    {
        try
        {
            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentsPanel: ServiceProvider is null");
                return;
            }

            SetBusyState(isBusy: true, statusMessage: "Reconciling payment budget mappings…");

            var repository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPaymentRepository>(ServiceProvider);
            var result = await repository.ReconcileBudgetMappingsAsync(CancellationToken.None);

            await LoadDataAsync();

            var message = result.NeedsAttentionCount == 0
                ? result.Summary
                : result.Summary + $"\n\nNeeds review: {result.NeedsAttentionCount} payment(s). Open any payment in Edit to change its budget account assignment.";

            var semanticKind = result.NeedsAttentionCount == 0
                ? SyncfusionControlFactory.MessageSemanticKind.Success
                : SyncfusionControlFactory.MessageSemanticKind.Warning;

            ShowMessageDialog(message, "Budget Reconciliation", semanticKind);
            SetStatusMessage(result.Summary);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Error reconciling budget mappings");
            SetBusyState(isBusy: false);
            SetStatusMessage("Unable to reconcile payment budget mappings. See logs for details.");
            ShowMessageDialog("Unable to reconcile payment budget mappings.", "Budget Reconciliation", SyncfusionControlFactory.MessageSemanticKind.Error, details: ex.Message);
        }
    }

    private async void BtnAdd_Click(object? sender, EventArgs e)
    {
        try
        {
            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentsPanel: ServiceProvider is null");
                return;
            }

            var editPanel = ActivatorUtilities.CreateInstance<PaymentEditPanel>(ServiceProvider);
            var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;

            using var dialog = new Form
            {
                Text = "Add Payment",
            };
            PaymentEditPanel.ConfigureHostedDialog(dialog);
            SfSkinManager.SetVisualStyle(dialog, themeName);

            editPanel.Dock = DockStyle.Fill;
            dialog.Controls.Add(editPanel);

            dialog.Shown += async (s, args) => await editPanel.LoadDataAsync();

            var dialogResult = dialog.ShowDialog();

            if (editPanel.HasSavedPayments)
            {
                Logger?.LogInformation("PaymentsPanel: Add payment dialog saved one or more payments, reloading data");
                await LoadDataAsync();
            }
            else if (dialogResult != DialogResult.OK)
            {
                Logger?.LogDebug("PaymentsPanel: Add payment dialog cancelled");
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Error opening add dialog");
            ShowMessageDialog("Unable to open the add payment dialog.", "Open Dialog Error", SyncfusionControlFactory.MessageSemanticKind.Error, details: ex.Message);
        }
    }

    private async void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (ViewModel?.SelectedPayment == null) return;

        try
        {
            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentsPanel: ServiceProvider is null");
                return;
            }

            var editPanel = ActivatorUtilities.CreateInstance<PaymentEditPanel>(ServiceProvider);
            editPanel.SetExistingPayment(ViewModel.SelectedPayment);
            var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;

            using var dialog = new Form
            {
                Text = "Edit Payment",
            };
            PaymentEditPanel.ConfigureHostedDialog(dialog);
            SfSkinManager.SetVisualStyle(dialog, themeName);

            editPanel.Dock = DockStyle.Fill;
            dialog.Controls.Add(editPanel);

            dialog.Shown += async (s, args) => await editPanel.LoadDataAsync();

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Logger?.LogInformation("PaymentsPanel: Payment edited successfully, reloading data");
                await LoadDataAsync();
            }
            else
            {
                Logger?.LogDebug("PaymentsPanel: Edit payment dialog cancelled");
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Error opening edit dialog");
            ShowMessageDialog("Unable to open the edit payment dialog.", "Open Dialog Error", SyncfusionControlFactory.MessageSemanticKind.Error, details: ex.Message);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (ViewModel?.SelectedPayment == null) return;

        var result = ShowMessageDialog(
            message: $"Delete payment {ViewModel.SelectedPayment.CheckNumber} to {ViewModel.SelectedPayment.Payee}?\n\nThis action cannot be undone.",
            title: "Delete Payment",
            semanticKind: SyncfusionControlFactory.MessageSemanticKind.Warning,
            buttons: MessageBoxButtons.YesNo,
            defaultButton: MessageBoxDefaultButton.Button2);

        if (result == DialogResult.Yes)
        {
            await ViewModel.DeletePaymentCommand.ExecuteAsync(null);

            _ = ControlFactory.ShowSemanticMessageBox(
                this,
                "Payment deleted successfully.",
                "Delete Successful",
                SyncfusionControlFactory.MessageSemanticKind.Success,
                MessageBoxButtons.OK,
                playNotificationSound: true);
        }
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _paymentsGrid.SelectedItem != null;
        var canActOnSelection = hasSelection && !IsBusy;

        _btnEdit.Enabled = canActOnSelection;
        _btnDelete.Enabled = canActOnSelection;
        if (_btnReconcile != null)
        {
            var hasRows = (_paymentsGrid?.View?.Records?.Count ?? 0) > 0 || (ViewModel?.Payments.Count ?? 0) > 0;
            _btnReconcile.Enabled = hasRows && !IsBusy;
        }
        UpdateExportButtonState();

        if (ViewModel != null && _paymentsGrid.SelectedItem is Payment payment)
        {
            ViewModel.SelectedPayment = payment;
        }
    }

    private void UpdateExportButtonState()
    {
        if (_btnExport == null)
        {
            return;
        }

        var view = _paymentsGrid?.View;
        var hasRows = view?.Records?.Count > 0;
        _btnExport.Enabled = view != null && hasRows && !IsBusy;
    }

    private void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.N when e.Control:
                BtnAdd_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
            case Keys.Delete:
                if (_btnDelete.Enabled)
                    BtnDelete_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
            case Keys.Enter:
                if (_btnEdit.Enabled)
                    BtnEdit_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
            case Keys.F when e.Control:
                _txtSearch.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
            case Keys.F5:
                _btnRefresh.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
        }
    }

    private async void Grid_CellClick(object? sender, CellClickEventArgs e)
    {
        if (e.DataColumn == null || e.DataRow?.RowData is not Payment payment)
        {
            return;
        }

        if (!string.Equals(e.DataColumn.GridColumn?.MappingName, nameof(Payment.IsCleared), StringComparison.Ordinal))
        {
            return;
        }

        await TogglePaymentClearedAsync(payment);
    }

    private async void PaymentsGrid_CurrentCellEndEdit(object? sender, CurrentCellEndEditEventArgs e)
    {
        if (ViewModel == null || _paymentsGrid == null)
        {
            return;
        }

        var currentCell = _paymentsGrid.CurrentCell;
        if (currentCell == null || currentCell.RowIndex < 0)
        {
            return;
        }

        if (!string.Equals(ResolveCurrentCellMappingName(currentCell.ColumnIndex), nameof(Payment.MunicipalAccountId), StringComparison.Ordinal))
        {
            return;
        }

        var selectedRow = _paymentsGrid.View?.Records.ElementAtOrDefault(currentCell.RowIndex);
        if (selectedRow?.Data is not Payment payment)
        {
            return;
        }

        try
        {
            await ViewModel.UpdatePaymentBudgetAccountAsync(payment, payment.MunicipalAccountId, CancellationToken.None);
            _paymentsGrid.View?.Refresh();
            SetStatusMessage(ViewModel.StatusMessage);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Failed to update budget account mapping for payment {PaymentId}", payment.Id);
            SetStatusMessage("Unable to update budget account mapping. Reloading payments.");
            ShowMessageDialog("Unable to update the budget account mapping.", "Payments", SyncfusionControlFactory.MessageSemanticKind.Error);
            await LoadDataAsync();
        }
    }

    private string ResolveCurrentCellMappingName(int columnIndex)
    {
        if (_paymentsGrid == null)
        {
            return string.Empty;
        }

        foreach (var candidateIndex in new[] { columnIndex, columnIndex - 1 })
        {
            if (candidateIndex >= 0 && candidateIndex < _paymentsGrid.Columns.Count)
            {
                return _paymentsGrid.Columns[candidateIndex].MappingName ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private async Task TogglePaymentClearedAsync(Payment payment)
    {
        if (ServiceProvider == null)
        {
            Logger?.LogError("PaymentsPanel: ServiceProvider is null while toggling cleared state");
            return;
        }

        if (IsVoidedStatus(payment.Status))
        {
            SetStatusMessage($"Check {payment.CheckNumber} is void and cannot be marked cleared.");
            _paymentsGrid?.View?.Refresh();
            return;
        }

        var originalIsCleared = payment.IsCleared;
        var originalStatus = payment.Status;

        payment.IsCleared = !payment.IsCleared;
        if (payment.IsCleared)
        {
            payment.Status = "Cleared";
        }
        else if (string.Equals(payment.Status, "Cleared", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = "Pending";
        }

        try
        {
            var repository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPaymentRepository>(ServiceProvider);
            await repository.UpdateAsync(payment, CancellationToken.None);

            _paymentsGrid?.View?.Refresh();
            SetStatusMessage(payment.IsCleared
                ? $"Marked check {payment.CheckNumber} as cleared."
                : $"Marked check {payment.CheckNumber} as pending.");
        }
        catch (Exception ex)
        {
            payment.IsCleared = originalIsCleared;
            payment.Status = originalStatus;
            _paymentsGrid?.View?.Refresh();

            Logger?.LogError(ex, "PaymentsPanel: Failed to toggle cleared state for payment {PaymentId}", payment.Id);
            SetStatusMessage("Unable to update cleared status. See logs for details.");
            ShowMessageDialog("Unable to update cleared status.", "Payments", SyncfusionControlFactory.MessageSemanticKind.Error);
        }
    }

    private void PaymentsGrid_ToolTipOpening(object? sender, ToolTipOpeningEventArgs e)
    {
        if (_paymentsGrid == null || e?.Column == null)
        {
            return;
        }

        var headerRowIndex = DataGridIndexResolver.GetHeaderIndex(_paymentsGrid.TableControl);
        if (e.RowIndex == headerRowIndex && e.Record == null)
        {
            if (!TryGetPaymentsColumnTooltip(e.Column.MappingName, e.Column.HeaderText, out var columnTooltip))
            {
                return;
            }

            var headerTooltipInfo = new Syncfusion.WinForms.Controls.ToolTipInfo();
            headerTooltipInfo.Items.Add(new Syncfusion.WinForms.Controls.ToolTipItem { Text = columnTooltip });
            e.ToolTipInfo = headerTooltipInfo;
            return;
        }

        if (e.Record == null)
        {
            return;
        }

        if (!TryBuildPaymentCellTooltip(e.Record, e.Column.MappingName, e.DisplayText, out var cellTooltip))
        {
            return;
        }

        var cellTooltipInfo = new Syncfusion.WinForms.Controls.ToolTipInfo();
        cellTooltipInfo.Items.Add(new Syncfusion.WinForms.Controls.ToolTipItem { Text = cellTooltip });
        e.ToolTipInfo = cellTooltipInfo;
    }

    private static bool TryGetPaymentsColumnTooltip(string? mappingName, string? headerText, out string tooltipText)
    {
        tooltipText = mappingName switch
        {
            nameof(Payment.Status) when string.Equals(headerText, StatusSymbolHeaderText, StringComparison.Ordinal) => "Quick visual state marker for each payment.",
            nameof(Payment.CheckNumber) => "Printed or assigned check number.",
            nameof(Payment.PaymentDate) => "Effective payment date recorded for the check.",
            nameof(Payment.Payee) => "Vendor or payee receiving the payment.",
            nameof(Payment.Amount) => "Payment amount in local currency.",
            nameof(Payment.Description) => "Purpose or summary for the payment.",
            nameof(Payment.BudgetAccountDisplay) => "Budget account linked to the payment.",
            nameof(Payment.BudgetPostingDisplay) => "Shows whether the payment is posted, missing an account, or needs reconciliation.",
            nameof(Payment.Status) => "Current payment status such as Pending, Cleared, Void, or Cancelled.",
            nameof(Payment.IsCleared) => "Whether the check has cleared the bank.",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(tooltipText);
    }

    private static bool TryBuildPaymentCellTooltip(object record, string? mappingName, string? displayText, out string tooltipText)
    {
        tooltipText = string.Empty;
        if (string.IsNullOrWhiteSpace(mappingName))
        {
            return false;
        }

        var rowData = ResolveRecordData(record);
        if (rowData is not Payment payment)
        {
            return false;
        }

        var resolvedValue = ResolveMappedValue(rowData, mappingName);
        var cleanDisplayText = string.IsNullOrWhiteSpace(displayText) ? "(blank)" : displayText.Trim();

        tooltipText = mappingName switch
        {
            nameof(Payment.Status) when string.Equals(cleanDisplayText, GetStatusSymbolGlyph(NormalizePaymentStatus(payment)), StringComparison.Ordinal) => $"Flag: {payment.Status}",
            nameof(Payment.CheckNumber) => $"Check Number: {payment.CheckNumber}",
            nameof(Payment.PaymentDate) => $"Payment Date: {payment.PaymentDate:MM/dd/yyyy}",
            nameof(Payment.Payee) => $"Payee: {payment.Payee}",
            nameof(Payment.Amount) => $"Amount: {payment.Amount:C2}",
            nameof(Payment.Description) => $"Description: {payment.Description}",
            nameof(Payment.BudgetAccountDisplay) => $"Budget Account: {resolvedValue?.ToString() ?? "(blank)"}",
            nameof(Payment.BudgetPostingDisplay) => $"Budget Posting: {payment.BudgetPostingDisplay}\nBudget Line: {(string.IsNullOrWhiteSpace(payment.BudgetLineDisplay) ? "(not linked)" : payment.BudgetLineDisplay)}",
            nameof(Payment.Status) => $"Status: {payment.Status}",
            nameof(Payment.IsCleared) => payment.IsCleared ? "Cleared: Yes" : "Cleared: No",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(tooltipText);
    }

    private static object? ResolveRecordData(object? record)
    {
        if (record == null)
        {
            return null;
        }

        var dataProperty = record.GetType().GetProperty("Data");
        return dataProperty?.GetValue(record) ?? record;
    }

    private static object? ResolveMappedValue(object? source, string mappingName)
    {
        if (source == null)
        {
            return null;
        }

        object? current = source;
        foreach (var segment in mappingName.Split('.'))
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(segment);
            current = property?.GetValue(current);
        }

        return current;
    }

    private void Grid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
    {
        // Header styling - professional appearance with better contrast
        if (e.RowIndex == -1)
        {
            e.Style.Font.Bold = true;
            e.Style.Font.Size = 11F;
            // Removed manual BackColor to respect SfSkinManager theme cascade.
            // Header background should be provided by the active Syncfusion theme.
            return;
        }

        // Row alternation for better readability
        if (e.RowIndex >= 0 && e.RowIndex % 2 == 0)
        {
            // Removed manual alternating row BackColor to respect SfSkinManager theme cascade.
            // Rely on theme or grid style for alternating rows instead of hard-coded colors.
        }

        // Get the payment object from the row
        var payment = e.DataRow?.RowData as Payment;
        if (payment == null) return;

        // Status icon column
        if (IsStatusSymbolColumn(e))
        {
            e.Style.Font.Size = 13F;
            e.Style.Font.Bold = true;

            var normalizedStatus = NormalizePaymentStatus(payment);
            e.Style.TextColor = GetStatusSymbolColor(normalizedStatus);
            e.DisplayText = GetStatusSymbolGlyph(normalizedStatus);

            return;
        }

        // Status text column remains human-readable
        if (e.Column.MappingName == nameof(Payment.Status))
        {
            e.Style.Font.Size = 10F;
            e.Style.Font.Bold = false;
        }

        if (e.Column.MappingName == nameof(Payment.BudgetPostingDisplay))
        {
            e.Style.Font.Size = 10F;
            e.Style.TextColor = GetBudgetPostingColor(payment.BudgetPostingStatus);
        }

        // Amount column - emphasize currency for scanability
        if (e.Column.MappingName == nameof(Payment.Amount))
        {
            e.Style.Font.Size = 10F;
            e.Style.Font.Bold = true;
        }

        // Date column styling
        if (e.Column.MappingName == nameof(Payment.PaymentDate))
        {
            e.Style.Font.Size = 10F;
            e.DisplayText = payment.PaymentDate.ToString("MM/dd/yyyy");
        }

        // Payee column - readable without emphasis
        if (e.Column.MappingName == nameof(Payment.Payee))
        {
            e.Style.Font.Size = 10F;
        }
    }

    private static bool IsStatusSymbolColumn(QueryCellStyleEventArgs e)
    {
        if (!string.Equals(e.Column.MappingName, nameof(Payment.Status), StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(e.Column.HeaderText, StatusSymbolHeaderText, StringComparison.Ordinal);
    }

    private static System.Drawing.Color GetStatusSymbolColor(string? normalizedStatus) => normalizedStatus switch
    {
        "cleared" or "posted" or "paid" or "complete" or "completed" => System.Drawing.Color.Green,
        "pending" => System.Drawing.Color.Orange,
        "processing" or "in progress" or "in-progress" or "scheduled" or "on hold" or "on-hold" => System.Drawing.Color.Orange,
        "void" or "voided" or "rejected" or "failed" or "error" => System.Drawing.Color.Red,
        "cancelled" or "canceled" => System.Drawing.Color.Orange,
        _ => System.Drawing.Color.DimGray
    };

    private static string GetStatusSymbolGlyph(string? normalizedStatus) => normalizedStatus switch
    {
        "cleared" or "posted" or "paid" or "complete" or "completed" => "✓",
        "pending" => "⏱",
        "processing" or "in progress" or "in-progress" or "scheduled" => "⏱",
        "on hold" or "on-hold" => "⏸",
        "void" or "voided" or "rejected" or "failed" or "error" => "✕",
        "cancelled" or "canceled" => "⊘",
        _ => "•"
    };

    private static string NormalizePaymentStatus(Payment payment)
    {
        if (payment.IsCleared)
        {
            return "cleared";
        }

        return payment.Status?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static System.Drawing.Color GetBudgetPostingColor(string? postingStatus)
    {
        return postingStatus?.Trim().ToLowerInvariant() switch
        {
            "posted" => System.Drawing.Color.Green,
            "ready to link" => System.Drawing.Color.Orange,
            "needs account" => System.Drawing.Color.Red,
            "multiple budget lines" => System.Drawing.Color.Orange,
            "conflicting budget account" => System.Drawing.Color.Red,
            "no budget line" => System.Drawing.Color.Orange,
            _ => System.Drawing.Color.DimGray
        };
    }

    private static bool IsVoidedStatus(string? status)
    {
        return string.Equals(status?.Trim(), "Void", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status?.Trim(), "Voided", StringComparison.OrdinalIgnoreCase);
    }

    private async void BtnExport_Click(object? sender, EventArgs e)
    {
        try
        {
            SetStatusMessage("Preparing export…");

            var view = _paymentsGrid.View;
            if (view == null)
            {
                ShowMessageDialog("Grid is still loading. Please try again once data is ready.", "Export", SyncfusionControlFactory.MessageSemanticKind.Warning);
                SetStatusMessage("Export is not available while the grid is loading.");
                return;
            }

            if (view.Records?.Count == 0)
            {
                ShowMessageDialog("No data to export.", "Export", SyncfusionControlFactory.MessageSemanticKind.Warning);
                SetStatusMessage("No payments available to export.");
                return;
            }

            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(PaymentsPanel)}.Excel",
                dialogTitle: "Export Payments to Excel",
                filter: "Excel Files (*.xlsx)|*.xlsx",
                defaultExtension: "xlsx",
                defaultFileName: $"Payments_{DateTime.Now:yyyyMMdd}.xlsx",
                exportAction: async (filePath, cancellationToken) =>
                {
                    SetBusyState(isBusy: true, statusMessage: "Exporting payments…");
                    try
                    {
                        await ExportService.ExportGridToExcelAsync(_paymentsGrid, filePath, cancellationToken);
                    }
                    finally
                    {
                        SetBusyState(isBusy: false);
                    }
                },
                statusCallback: SetStatusMessage,
                logger: Logger,
                cancellationToken: CancellationToken.None);

            if (result.IsSkipped)
            {
                ShowMessageDialog(result.ErrorMessage ?? "An export is already in progress.", "Export", SyncfusionControlFactory.MessageSemanticKind.Warning);
                return;
            }

            if (result.IsCancelled)
            {
                SetStatusMessage("Export cancelled.");
                return;
            }

            if (!result.IsSuccess)
            {
                SetStatusMessage("Export failed. See logs for details.");
                ShowMessageDialog(result.ErrorMessage ?? "Export failed.", "Export Error", SyncfusionControlFactory.MessageSemanticKind.Error);
                return;
            }

            SetStatusMessage(view.Records?.Count == 1 ? "Exported 1 payment." : $"Exported {view.Records?.Count ?? 0} payments.");
            ShowMessageDialog("Export completed successfully.", "Export Complete", SyncfusionControlFactory.MessageSemanticKind.Success);

            if (ShowMessageDialog("Would you like to open the exported file?", "Open File", SyncfusionControlFactory.MessageSemanticKind.Success, MessageBoxButtons.YesNo, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Error exporting to Excel");
            SetBusyState(isBusy: false);
            SetStatusMessage("Export failed. See logs for details.");
            ShowMessageDialog("Unable to export payments.", "Export Error", SyncfusionControlFactory.MessageSemanticKind.Error, details: ex.Message);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        try
        {
            if (keyData == (Keys.Control | Keys.N))
            {
                BtnAdd_Click(this, EventArgs.Empty);
                return true;
            }

            if ((keyData == Keys.F2 || keyData == Keys.Enter) && _btnEdit?.Enabled == true)
            {
                BtnEdit_Click(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.Delete && _btnDelete?.Enabled == true)
            {
                BtnDelete_Click(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F5)
            {
                _btnRefresh?.PerformClick();
                return true;
            }

            if (keyData == (Keys.Control | Keys.E))
            {
                BtnExport_Click(this, EventArgs.Empty);
                return true;
            }

            if (keyData == (Keys.Control | Keys.F))
            {
                _txtSearch?.Focus();
                return true;
            }

            if (keyData == Keys.Escape)
            {
                _paymentsGrid?.SelectedIndex = -1;
                _paymentsGrid?.Focus();
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Error processing keyboard shortcut");
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// Applies real-time search filter to the grid using View.Filter
    /// Searches across Payee, CheckNumber, and Description fields
    /// </summary>
    private void ApplySearchFilter()
    {
        EnsureGridBinding();

        if (_paymentsGrid?.View == null)
        {
            return;
        }

        var searchText = _txtSearch.Text?.Trim();

        if (string.IsNullOrEmpty(searchText))
        {
            _paymentsGrid.View.Filter = null;
            _paymentsGrid.View.RefreshFilter();
            var totalCount = ViewModel?.Payments.Count ?? 0;
            SetStatusMessage(totalCount == 1 ? "Showing all 1 payment." : $"Showing all {totalCount} payments.");
            return;
        }

        _paymentsGrid.View.Filter = (obj) =>
        {
            if (obj is Payment payment)
            {
                return (payment.Payee?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (payment.CheckNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (payment.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                      (payment.BudgetAccountDisplay?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                      (payment.BudgetPostingDisplay?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                      (payment.BudgetLineDisplay?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (payment.MunicipalAccount?.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (payment.Status?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
            }
            return false;
        };
        _paymentsGrid.View.RefreshFilter();

        var filteredCount = _paymentsGrid.View.Records?.Count ?? 0;
        SetStatusMessage(filteredCount == 1 ? "Showing 1 matching payment." : $"Showing {filteredCount} matching payments.");
    }

    private void SetBusyState(bool isBusy, string? statusMessage = null)
    {
        IsBusy = isBusy;

        if (_panelHeader != null)
        {
            _panelHeader.IsLoading = isBusy;
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            SetStatusMessage(statusMessage);
        }

        if (_btnAdd != null)
        {
            _btnAdd.Enabled = !isBusy;
        }

        if (_btnRefresh != null)
        {
            _btnRefresh.Enabled = !isBusy;
        }

        UpdateButtonStates();
        UpdateExportButtonState();
    }

    private DialogResult ShowMessageDialog(
        string message,
        string title,
        SyncfusionControlFactory.MessageSemanticKind semanticKind,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1,
        string? details = null)
    {
        return ControlFactory.ShowSemanticMessageBox(this, message, title, semanticKind, buttons, defaultButton, details: details);
    }

    private void SetStatusMessage(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_panelHeader != null)
            {
                _panelHeader.RefreshClicked -= PanelHeader_RefreshClicked;
            }

            if (_paymentsGrid != null)
            {
                _paymentsGrid.QueryCellStyle -= Grid_QueryCellStyle;
                _paymentsGrid.CellClick -= Grid_CellClick;
                _paymentsGrid.ToolTipOpening -= PaymentsGrid_ToolTipOpening;
                _paymentsGrid.Dispose();
            }
            _btnAdd?.Dispose();
            _btnEdit?.Dispose();
            _btnDelete?.Dispose();
            _btnRefresh?.Dispose();
            _btnReconcile?.Dispose();
            _btnExport?.Dispose();
            _toolTip?.Dispose();
            _panelHeader?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Custom summary aggregate for calculating total of cleared payments only
/// </summary>
internal sealed class ClearedPaymentAggregate : ISummaryAggregate
{
    public double Sum { get; set; }

    public Action<System.Collections.IEnumerable, string, PropertyDescriptor> CalculateAggregateFunc()
    {
        return (items, property, pd) =>
        {
            var payments = items.Cast<Payment>().Where(p => p.IsCleared);
            decimal total = payments.Sum(p => p.Amount);
            Sum = (double)total;
        };
    }
}
