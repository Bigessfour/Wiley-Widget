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
    private SfDataGrid _paymentsGrid = null!;
    private SfButton _btnAdd = null!;
    private SfButton _btnEdit = null!;
    private SfButton _btnDelete = null!;
    private SfButton _btnRefresh = null!;
    private SfButton _btnExport = null!;
    private TextBox _txtSearch = null!;

    public PaymentsPanel(IServiceScopeFactory scopeFactory, ILogger<PaymentsPanel> logger)
        : base(scopeFactory, logger)
    {
        InitializeControls();

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
            IsBusy = true;

            if (ViewModel == null)
            {
                Logger?.LogError("PaymentsPanel: ViewModel is null");
                return;
            }

            // Load payments
            await ViewModel.LoadPaymentsCommand.ExecuteAsync(null);

            // CRITICAL FIX: Force grid refresh after loading data
            // The grid may not auto-refresh even though Payments is ObservableCollection
            if (_paymentsGrid?.View != null)
            {
                _paymentsGrid.View.Refresh();
                Logger?.LogDebug("PaymentsPanel: Grid refreshed after loading {Count} payments", ViewModel.Payments.Count);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: LoadDataAsync failed");
        }
        finally
        {
            IsBusy = false;
            UpdateExportButtonState();
        }
    }

    private void InitializeControls()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(this, themeName);

        Name = "PaymentsPanel";
        Size = new System.Drawing.Size(1000, 600);
        Dock = DockStyle.Fill;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10)
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid

        // Toolbar
        var toolbar = CreateToolbar(themeName);
        mainLayout.Controls.Add(toolbar, 0, 0);

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
            RowHeight = 40  // Increased for better readability
        }.PreventStringRelationalFilters(_logger, "Status", "CheckNumber", "Payee", "Description");

        // Status icon column
        _paymentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(Payment.Status),
            HeaderText = "●",
            MinimumWidth = 40,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            AllowSorting = false
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
        _paymentsGrid.KeyDown += Grid_KeyDown;  // Keyboard shortcuts

        mainLayout.Controls.Add(_paymentsGrid, 0, 1);

        Controls.Add(mainLayout);
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
            Text = "Add Payment",
            Width = 130,
            Height = 38,
            ThemeName = themeName,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        _btnAdd.Image = LoadIcon("New32");
        _btnAdd.Click += BtnAdd_Click;
        toolbar.Controls.Add(_btnAdd);

        // Edit button
        _btnEdit = new SfButton
        {
            Text = "Edit",
            Width = 100,
            Height = 38,
            ThemeName = themeName,
            Enabled = false,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        _btnEdit.Image = LoadIcon("Edit32");
        _btnEdit.Click += BtnEdit_Click;
        toolbar.Controls.Add(_btnEdit);

        // Delete button
        _btnDelete = new SfButton
        {
            Text = "Delete",
            Width = 100,
            Height = 38,
            ThemeName = themeName,
            Enabled = false,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        _btnDelete.Image = LoadIcon("Delete32");
        _btnDelete.Click += BtnDelete_Click;
        toolbar.Controls.Add(_btnDelete);

        // Refresh button
        _btnRefresh = new SfButton
        {
            Text = "Refresh",
            Width = 110,
            Height = 38,
            ThemeName = themeName,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        _btnRefresh.Image = LoadIcon("Refresh32");
        _btnRefresh.Click += async (s, e) => await ViewModel!.RefreshCommand.ExecuteAsync(null);
        toolbar.Controls.Add(_btnRefresh);

        // Export to Excel button
        _btnExport = new SfButton
        {
            Text = "Export to Excel",
            Width = 140,
            Height = 38,
            ThemeName = themeName,
            Enabled = false,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        _btnExport.Image = LoadIcon("Excel32");
        _btnExport.Click += BtnExport_Click;
        toolbar.Controls.Add(_btnExport);

        // Separator
        toolbar.Controls.Add(new Label
        {
            Text = "  |",
            AutoSize = true,
            Padding = new Padding(5, 0, 10, 0),
            ForeColor = System.Drawing.Color.LightGray
        });

        // Search section
        toolbar.Controls.Add(new Label
        {
            Text = "Search:",
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 8, 0)
        });

        _txtSearch = new TextBox
        {
            Width = 220,
            Height = 30,
            PlaceholderText = "Type to filter...",
            Padding = new Padding(4)
        };
        _txtSearch.TextChanged += (s, e) => ApplySearchFilter();
        toolbar.Controls.Add(_txtSearch);

        return toolbar;
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
                MaximizeBox = false
            };

            editPanel.Dock = DockStyle.Fill;
            dialog.Controls.Add(editPanel);

            dialog.Shown += async (s, args) => await editPanel.LoadDataAsync();

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Logger?.LogInformation("PaymentsPanel: Payment saved successfully, reloading data");
                await LoadDataAsync();
            }
            else
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
                MaximizeBox = false
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
            $"Are you sure you want to delete payment {ViewModel.SelectedPayment.CheckNumber} to {ViewModel.SelectedPayment.Payee}?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

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
                break;
            case Keys.Delete:
                if (_btnDelete.Enabled)
                    BtnDelete_Click(sender, e);
                e.Handled = true;
                break;
            case Keys.Enter:
                if (_btnEdit.Enabled)
                    BtnEdit_Click(sender, e);
                e.Handled = true;
                break;
            case Keys.F when e.Control:
                _txtSearch.Focus();
                e.Handled = true;
                break;
            case Keys.F5:
                _btnRefresh.PerformClick();
                e.Handled = true;
                break;
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

        // Status column - sophisticated color-coded indicators with icons
        if (e.Column.MappingName == nameof(Payment.Status))
        {
            e.Style.Font.Size = 13F;
            e.Style.Font.Bold = true;

            var statusColor = payment.Status?.ToLowerInvariant() switch
            {
                "cleared" => System.Drawing.Color.FromArgb(40, 167, 69),       // Green ✓
                "pending" => System.Drawing.Color.FromArgb(255, 193, 7),       // Orange ⏱
                "void" => System.Drawing.Color.FromArgb(220, 53, 69),          // Red ✕
                "cancelled" => System.Drawing.Color.FromArgb(108, 117, 125),   // Gray ⊘
                _ => System.Drawing.Color.FromArgb(0, 123, 255)                // Blue ?
            };

            e.Style.TextColor = statusColor;
            // Use Unicode symbols for better visual distinction
            e.DisplayText = payment.Status?.ToLowerInvariant() switch
            {
                "cleared" => "✓",
                "pending" => "⏱",
                "void" => "✕",
                "cancelled" => "⊘",
                _ => "?"
            };
        }

        // Amount column - bold, color-coded based on payment status
        if (e.Column.MappingName == nameof(Payment.Amount))
        {
            e.Style.Font.Size = 10F;
            e.Style.Font.Bold = true;
            e.DisplayText = $"${payment.Amount:F2}";

            if (payment.IsCleared)
            {
                e.Style.TextColor = System.Drawing.Color.FromArgb(40, 167, 69); // Green for cleared
            }
            else if (payment.Amount > 10000)
            {
                e.Style.TextColor = System.Drawing.Color.FromArgb(220, 53, 69); // Red for large amounts
            }
            else
            {
                e.Style.TextColor = System.Drawing.Color.FromArgb(0, 0, 0); // Black for normal amounts
            }
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

    private async void BtnExport_Click(object? sender, EventArgs e)
    {
        try
        {
            var view = _paymentsGrid.View;
            if (view == null)
            {
                MessageBox.Show("Grid is still loading. Please try again once data is ready.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (view.Records?.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Payments_{DateTime.Now:yyyyMMdd}.xlsx",
                Title = "Export Payments to Excel"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var previousCursor = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    await ExportService.ExportGridToExcelAsync(_paymentsGrid, saveDialog.FileName);
                }
                finally
                {
                    Cursor.Current = previousCursor;
                }

                MessageBox.Show("Export completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (MessageBox.Show("Would you like to open the exported file?", "Open File", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentsPanel: Error exporting to Excel");
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
        var searchText = _txtSearch.Text?.Trim();

        if (string.IsNullOrEmpty(searchText))
        {
            _paymentsGrid.View.Filter = null;
            _paymentsGrid.View.RefreshFilter();
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_paymentsGrid != null)
            {
                _paymentsGrid.QueryCellStyle -= Grid_QueryCellStyle;
                _paymentsGrid.Dispose();
            }
            _btnAdd?.Dispose();
            _btnEdit?.Dispose();
            _btnDelete?.Dispose();
            _btnRefresh?.Dispose();
            _btnExport?.Dispose();
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
