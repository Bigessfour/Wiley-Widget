#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for displaying and managing payments (check register)
/// </summary>
public partial class PaymentsPanel : ScopedPanelBase<PaymentsViewModel>
{
    private const string StatusSymbolHeaderText = "●";

    private SfDataGrid _paymentsGrid = null!;
    private SfButton _btnAdd = null!;
    private SfButton _btnEdit = null!;
    private SfButton _btnDelete = null!;
    private SfButton _btnRefresh = null!;
    private SfButton _btnExport = null!;
    private TextBox _txtSearch = null!;
    private PanelHeader _panelHeader = null!;
    private Label _statusLabel = null!;
    private ToolTip _toolTip = null!;

    public PaymentsPanel(IServiceScopeFactory scopeFactory, ILogger<PaymentsPanel> logger)
        : base(scopeFactory, logger)
    {
        SafeSuspendAndLayout(InitializeControls);

        // CRITICAL FIX: Load data when panel is loaded
        Load += PaymentsPanel_Load;
    }

    private async void PaymentsPanel_Load(object? sender, EventArgs e)
    {
        Logger?.LogDebug("PaymentsPanel: Load event fired, loading payment data");
        await LoadDataAsync();
    }

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

            // Load payments
            await ViewModel.LoadPaymentsCommand.ExecuteAsync(null);

            EnsureGridBinding(forceRebindWhenOutOfSync: true);

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
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: LoadDataAsync failed");
            SetStatusMessage("Unable to load payments. See logs for details.");
        }
        finally
        {
            SetBusyState(isBusy: false);
        }
    }

    private void InitializeControls()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(this, themeName);

        Name = "PaymentsPanel";
        Size = new System.Drawing.Size(1000, 600);
        MinimumSize = new System.Drawing.Size(1024, 720);
        Dock = DockStyle.Fill;

        var panelPadding = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f);
        var headerRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(56f);
        var toolbarRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(60f);
        var statusRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(30f);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(panelPadding)
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, headerRowHeight)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, toolbarRowHeight)); // Toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, statusRowHeight)); // Status

        _panelHeader = new PanelHeader
        {
            Title = "Payments",
            Dock = DockStyle.Fill,
            ShowPinButton = false,
            ShowHelpButton = false,
            ShowCloseButton = false,
            ShowRefreshButton = true
        };
        _panelHeader.RefreshClicked += PanelHeader_RefreshClicked;
        mainLayout.Controls.Add(_panelHeader, 0, 0);

        _toolTip = new ToolTip
        {
            AutoPopDelay = 8000,
            InitialDelay = 250,
            ReshowDelay = 100,
            ShowAlways = true
        };

        // Toolbar
        var toolbar = CreateToolbar(themeName);
        mainLayout.Controls.Add(toolbar, 0, 1);

        // Grid
        _paymentsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            AllowDeleting = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = true,
            SelectionMode = Syncfusion.WinForms.DataGrid.Enums.GridSelectionMode.Single,
            ThemeName = themeName,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            RowHeight = 40,  // Increased for better readability
            AccessibleName = "Payments Grid"
        }.PreventStringRelationalFilters(_logger, "Status", "CheckNumber", "Payee", "Description");

        // Status icon column
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Status),
            HeaderText = StatusSymbolHeaderText,
            MinimumWidth = 40,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowSorting = false,
            AllowFiltering = false
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.CheckNumber),
            HeaderText = "Check #",
            MinimumWidth = 90,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells
        });
        _paymentsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = nameof(Payment.PaymentDate),
            HeaderText = "Date",
            Format = "d",
            MinimumWidth = 90,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Payee),
            HeaderText = "Payee",
            MinimumWidth = 160,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill
        });
        _paymentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(Payment.Amount),
            HeaderText = "Amount",
            Format = "C2",
            MinimumWidth = 110,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Description),
            HeaderText = "Description",
            MinimumWidth = 220,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "MunicipalAccount.Name",
            HeaderText = "Account",
            MinimumWidth = 150,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            AllowSorting = true,
            AllowFiltering = true
        });
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Status),
            HeaderText = "Status",
            MinimumWidth = 100,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowSorting = true,
            AllowFiltering = true
        });
        _paymentsGrid.Columns.Add(new GridCheckBoxColumn
        {
            MappingName = nameof(Payment.IsCleared),
            HeaderText = "Cleared",
            MinimumWidth = 70,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowEditing = false // Read-only indicator, synced with Status field
        });

        // Apply custom cell styling for color-coding
        _paymentsGrid.QueryCellStyle += Grid_QueryCellStyle;

        // Enable grouping and show group area for advanced users
        _paymentsGrid.AllowGrouping = true;
        _paymentsGrid.ShowGroupDropArea = true;

        // Header and row sizing for readability
        _paymentsGrid.HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(36f);
        _paymentsGrid.RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(40f);

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
            Padding = new Padding(4, 0, 0, 0),
            AccessibleName = "Payments Status"
        };
        mainLayout.Controls.Add(_statusLabel, 0, 3);

        Controls.Add(mainLayout);

        SetStatusMessage("Ready");
    }

    private void EnsureGridBinding(bool forceRebindWhenOutOfSync = false)
    {
        if (ViewModel == null || _paymentsGrid == null)
        {
            return;
        }

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

    private Panel CreateToolbar(string themeName)
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10, 8, 10, 8),
            WrapContents = false,
            AutoScroll = true
        };

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
        _btnAdd = new SfButton
        {
            Text = "&Add Payment",
            Width = 130,
            Height = 38,
            ThemeName = themeName,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AccessibleName = "Add Payment"
        };
        _btnAdd.Image = LoadIcon("New32");
        _btnAdd.Click += BtnAdd_Click;
        _toolTip.SetToolTip(_btnAdd, "Create a new payment (Ctrl+N)");
        toolbar.Controls.Add(_btnAdd);

        // Edit button
        _btnEdit = new SfButton
        {
            Text = "&Edit",
            Width = 100,
            Height = 38,
            ThemeName = themeName,
            Enabled = false,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AccessibleName = "Edit Payment"
        };
        _btnEdit.Image = LoadIcon("Edit32");
        _btnEdit.Click += BtnEdit_Click;
        _toolTip.SetToolTip(_btnEdit, "Edit selected payment (Enter or F2)");
        toolbar.Controls.Add(_btnEdit);

        // Delete button
        _btnDelete = new SfButton
        {
            Text = "&Delete",
            Width = 100,
            Height = 38,
            ThemeName = themeName,
            Enabled = false,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AccessibleName = "Delete Payment"
        };
        _btnDelete.Image = LoadIcon("Delete32");
        _btnDelete.Click += BtnDelete_Click;
        _toolTip.SetToolTip(_btnDelete, "Delete selected payment (Delete)");
        toolbar.Controls.Add(_btnDelete);

        // Refresh button
        _btnRefresh = new SfButton
        {
            Text = "&Refresh",
            Width = 110,
            Height = 38,
            ThemeName = themeName,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AccessibleName = "Refresh Payments"
        };
        _btnRefresh.Image = LoadIcon("Refresh32");
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();
        _toolTip.SetToolTip(_btnRefresh, "Reload payments from the data source (F5)");
        toolbar.Controls.Add(_btnRefresh);

        // Export to Excel button
        _btnExport = new SfButton
        {
            Text = "E&xport to Excel",
            Width = 140,
            Height = 38,
            ThemeName = themeName,
            Enabled = false,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AccessibleName = "Export Payments"
        };
        _btnExport.Image = LoadIcon("Excel32");
        _btnExport.Click += BtnExport_Click;
        _toolTip.SetToolTip(_btnExport, "Export current grid data to Excel (Ctrl+E)");
        toolbar.Controls.Add(_btnExport);

        // Separator
        toolbar.Controls.Add(new Label
        {
            Text = "  |",
            AutoSize = true,
            Padding = new Padding(5, 0, 10, 0)
        });

        // Search section
        toolbar.Controls.Add(new Label
        {
            Text = "&Search:",
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 8, 0)
        });

        _txtSearch = new TextBox
        {
            Width = 260,
            Height = 30,
            PlaceholderText = "Type to filter...",
            Padding = new Padding(4),
            AccessibleName = "Search Payments"
        };
        _txtSearch.TextChanged += (s, e) => ApplySearchFilter();
        _toolTip.SetToolTip(_txtSearch, "Filter by payee, check number, or description (Ctrl+F)");
        toolbar.Controls.Add(_txtSearch);

        return toolbar;
    }

    private async void PanelHeader_RefreshClicked(object? sender, EventArgs e)
    {
        await LoadDataAsync();
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

            var dialog = new Form
            {
                Text = "Add Payment",
                Width = 900,
                Height = 950,
                FormBorderStyle = FormBorderStyle.Sizable,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi
            };

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
            MessageBox.Show($"Error opening add dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            var dialog = new Form
            {
                Text = "Edit Payment",
                Width = 900,
                Height = 950,
                FormBorderStyle = FormBorderStyle.Sizable,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi
            };

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
            MessageBox.Show($"Error opening edit dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (ViewModel?.SelectedPayment == null) return;

        var result = MessageBox.Show(
            $"Delete payment {ViewModel.SelectedPayment.CheckNumber} to {ViewModel.SelectedPayment.Payee}?\n\nThis action cannot be undone.",
            "Delete Payment",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result == DialogResult.Yes)
        {
            await ViewModel.DeletePaymentCommand.ExecuteAsync(null);
        }
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _paymentsGrid.SelectedItem != null;
        _btnEdit.Enabled = hasSelection;
        _btnDelete.Enabled = hasSelection;
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
            MessageBox.Show("Unable to update cleared status.", "Payments", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
                MessageBox.Show("Grid is still loading. Please try again once data is ready.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatusMessage("Export is not available while the grid is loading.");
                return;
            }

            if (view.Records?.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(result.ErrorMessage ?? "Export failed.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetStatusMessage(view.Records?.Count == 1 ? "Exported 1 payment." : $"Exported {view.Records?.Count ?? 0} payments.");
            MessageBox.Show("Export completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (MessageBox.Show("Would you like to open the exported file?", "Open File", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
            MessageBox.Show($"Error exporting to Excel: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                _ = ViewModel?.RefreshCommand.ExecuteAsync(null);
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
                       (payment.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
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

        UpdateExportButtonState();
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
                _paymentsGrid.Dispose();
            }
            _btnAdd?.Dispose();
            _btnEdit?.Dispose();
            _btnDelete?.Dispose();
            _btnRefresh?.Dispose();
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
