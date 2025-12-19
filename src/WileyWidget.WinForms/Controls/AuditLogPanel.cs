using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Audit Log viewer panel displaying audit entries with filtering and export capabilities.
/// Inherits from ScopedPanelBase to ensure proper DI lifetime management for scoped dependencies.
/// </summary>
public partial class AuditLogPanel : ScopedPanelBase<AuditLogViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private Panel? _filterPanel;
    private SfDataGrid? _auditGrid;
    private Syncfusion.WinForms.Controls.SfButton? _btnRefresh;
    private Syncfusion.WinForms.Controls.SfButton? _btnExportCsv;
    private CheckBox? _chkAutoRefresh;
    private DateTimePicker? _dtpStartDate;
    private DateTimePicker? _dtpEndDate;
    private Syncfusion.WinForms.ListView.SfComboBox? _cmbActionType;
    private Syncfusion.WinForms.ListView.SfComboBox? _cmbUser;

    // Event handlers for cleanup
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _entriesCollectionChangedHandler;
    private EventHandler<AppTheme>? _themeChangedHandler;
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _btnRefreshClickHandler;
    private EventHandler? _btnExportCsvClickHandler;
    private EventHandler? _chkAutoRefreshCheckedChangedHandler;
    private EventHandler? _dtpStartDateValueChangedHandler;
    private EventHandler? _dtpEndDateValueChangedHandler;
    private EventHandler? _cmbActionTypeSelectedIndexChangedHandler;
    private EventHandler? _cmbUserSelectedIndexChangedHandler;

    /// <summary>
    /// Initializes a new instance with required DI dependencies.
    /// </summary>
    public AuditLogPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AuditLogViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
        SetupUI();
        SubscribeToThemeChanges();
    }

    private void InitializeComponent()
    {
        Name = "AuditLogPanel";
        Size = new Size(1200, 800);
        Dock = DockStyle.Fill;
        AccessibleName = "Audit Log Panel";
        AccessibleDescription = "Displays audit log entries with filtering and export capabilities";

        try
        {
            AutoScaleMode = AutoScaleMode.Dpi;
        }
        catch
        {
            // Fall back if DPI scaling not supported
        }
    }

    private void SetupUI()
    {
        SuspendLayout();

        // Panel header with title and actions
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Audit Log",
            AccessibleName = "Audit Log header"
        };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Filter panel
        _filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(8),
            AccessibleName = "Audit log filters"
        };

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            AutoSize = true
        };
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Start Date label
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // Start Date picker
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // End Date label
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // End Date picker
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Action Type label
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Action Type combo

        // Row 1: Date filters
        filterTable.Controls.Add(new Label { Text = "Start Date:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _dtpStartDate = new DateTimePicker
        {
            Width = 140,
            Format = DateTimePickerFormat.Short,
            AccessibleName = "Start date filter",
            AccessibleDescription = "Filter audit entries from this date"
        };
        _dtpStartDateValueChangedHandler = (s, e) => ApplyFilters();
        _dtpStartDate.ValueChanged += _dtpStartDateValueChangedHandler;
        filterTable.Controls.Add(_dtpStartDate, 1, 0);

        filterTable.Controls.Add(new Label { Text = "End Date:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
        _dtpEndDate = new DateTimePicker
        {
            Width = 140,
            Format = DateTimePickerFormat.Short,
            AccessibleName = "End date filter",
            AccessibleDescription = "Filter audit entries until this date"
        };
        _dtpEndDateValueChangedHandler = (s, e) => ApplyFilters();
        _dtpEndDate.ValueChanged += _dtpEndDateValueChangedHandler;
        filterTable.Controls.Add(_dtpEndDate, 3, 0);

        // Row 2: Action Type and User filters
        filterTable.Controls.Add(new Label { Text = "Action Type:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        _cmbActionType = new Syncfusion.WinForms.ListView.SfComboBox
        {
            Width = 140,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AllowDropDownResize = false,
            MaxDropDownItems = 10,
            AllowNull = true,
            Watermark = "All Actions",
            AccessibleName = "Action type filter",
            AccessibleDescription = "Filter audit entries by action type"
        };
        _cmbActionTypeSelectedIndexChangedHandler = (s, e) => ApplyFilters();
        _cmbActionType.SelectedIndexChanged += _cmbActionTypeSelectedIndexChangedHandler;
        filterTable.Controls.Add(_cmbActionType, 1, 1);

        filterTable.Controls.Add(new Label { Text = "User:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 2, 1);
        _cmbUser = new Syncfusion.WinForms.ListView.SfComboBox
        {
            Width = 140,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AllowDropDownResize = false,
            MaxDropDownItems = 10,
            AllowNull = true,
            Watermark = "All Users",
            AccessibleName = "User filter",
            AccessibleDescription = "Filter audit entries by user"
        };
        _cmbUserSelectedIndexChangedHandler = (s, e) => ApplyFilters();
        _cmbUser.SelectedIndexChanged += _cmbUserSelectedIndexChangedHandler;
        filterTable.Controls.Add(_cmbUser, 3, 1);

        // Buttons
        _btnRefresh = new Syncfusion.WinForms.Controls.SfButton
        {
            Text = "&Refresh",
            Width = 100,
            Height = 32,
            AccessibleName = "Refresh audit log",
            AccessibleDescription = "Reload audit entries from database"
        };
        _btnRefreshClickHandler = async (s, e) => await RefreshDataAsync();
        _btnRefresh.Click += _btnRefreshClickHandler;
        filterTable.Controls.Add(_btnRefresh, 4, 0);

        _chkAutoRefresh = new CheckBox
        {
            Text = "Auto-refresh",
            AutoSize = true,
            AccessibleName = "Auto-refresh toggle",
            AccessibleDescription = "Automatically refresh audit log every 30 seconds"
        };
        _chkAutoRefreshCheckedChangedHandler = (s, e) => ToggleAutoRefresh();
        _chkAutoRefresh.CheckedChanged += _chkAutoRefreshCheckedChangedHandler;
        filterTable.Controls.Add(_chkAutoRefresh, 4, 1);

        _btnExportCsv = new Syncfusion.WinForms.Controls.SfButton
        {
            Text = "&Export CSV",
            Width = 100,
            Height = 32,
            AccessibleName = "Export to CSV",
            AccessibleDescription = "Export filtered audit entries to CSV file"
        };
        _btnExportCsvClickHandler = (s, e) => ExportToCsv();
        _btnExportCsv.Click += _btnExportCsvClickHandler;
        filterTable.Controls.Add(_btnExportCsv, 5, 0);

        _filterPanel.Controls.Add(filterTable);
        Controls.Add(_filterPanel);

        // Audit grid
        _auditGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowFiltering = true,
            AllowSorting = true,
            AllowGrouping = false,
            ShowRowHeader = false,
            SelectionMode = GridSelectionMode.Single,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f),
            HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f),
            AllowResizingColumns = true,
            AllowTriStateSorting = true,
            AccessibleName = "Audit log entries grid",
            AccessibleDescription = "Grid displaying audit log entries with timestamp, user, action, and details"
        };

        ConfigureGridColumns();
        Controls.Add(_auditGrid);

        // Loading and no-data overlays
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading audit entries...",
            AccessibleName = "Loading overlay"
        };
        Controls.Add(_loadingOverlay);

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No audit entries found",
            AccessibleName = "No data overlay"
        };
        Controls.Add(_noDataOverlay);

        ResumeLayout(false);
    }

    private void ConfigureGridColumns()
    {
        if (_auditGrid == null) return;

        _auditGrid.Columns.Clear();

        _auditGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = nameof(AuditEntry.Timestamp),
            HeaderText = "Timestamp",
            MinimumWidth = 150,
            Format = "yyyy-MM-dd HH:mm:ss",
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.User),
            HeaderText = "User",
            MinimumWidth = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.Action),
            HeaderText = "Action",
            MinimumWidth = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.EntityType),
            HeaderText = "Entity Type",
            MinimumWidth = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.EntityId),
            HeaderText = "Entity ID",
            MinimumWidth = 100,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.Changes),
            HeaderText = "Details",
            MinimumWidth = 200,
            AllowSorting = false,
            AllowFiltering = true
        });
    }

    /// <summary>
    /// Called after ViewModel is resolved from scoped service provider.
    /// Binds ViewModel data and initiates data load.
    /// </summary>
    protected override void OnViewModelResolved(AuditLogViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);

        // Subscribe to ViewModel property changes
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Subscribe to Entries collection changes
        _entriesCollectionChangedHandler = (s, e) => UpdateGridData();
        viewModel.Entries.CollectionChanged += _entriesCollectionChangedHandler;

        // Initialize filters
        InitializeFilters();

        // Initial UI update
        UpdateUI();

        // Load data asynchronously
        _ = LoadDataSafeAsync();
    }

    private void InitializeFilters()
    {
        if (ViewModel == null) return;

        // Set default date range (last 30 days)
        var endDate = DateTime.Now;
        var startDate = endDate.AddDays(-30);

        _dtpStartDate!.Value = startDate;
        _dtpEndDate!.Value = endDate;

        // Populate action types (common audit actions)
        var actionTypes = new List<string> { "All", "CREATE", "UPDATE", "DELETE", "LOGIN", "LOGOUT" };
        _cmbActionType!.DataSource = actionTypes;

        // Populate users (would come from ViewModel in real implementation)
        var users = new List<string> { "All", "admin", "user1", "user2" }; // Placeholder
        _cmbUser!.DataSource = users;
    }

    private async Task LoadDataSafeAsync()
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadEntriesAsync();
            }
        }
        catch (Exception ex)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowError(ex)));
            }
            else
            {
                ShowError(ex);
            }
        }
    }

    private void ShowError(Exception ex)
    {
        MessageBox.Show(
            $"Failed to load audit entries: {ex.Message}",
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed || ViewModel == null) return;

        // Thread-safe UI updates
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsLoading):
                    if (_loadingOverlay != null)
                        _loadingOverlay.Visible = ViewModel.IsLoading;
                    break;

                case nameof(ViewModel.Entries):
                    UpdateGridData();
                    UpdateNoDataOverlay();
                    break;

                case nameof(ViewModel.ErrorMessage):
                    if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                    {
                        MessageBox.Show(
                            ViewModel.ErrorMessage,
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    break;
            }
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed during update
        }
    }

    private void UpdateUI()
    {
        if (IsDisposed || ViewModel == null) return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateUI));
            return;
        }

        try
        {
            UpdateGridData();
            UpdateNoDataOverlay();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed
        }
    }

    private void UpdateGridData()
    {
        if (_auditGrid == null || ViewModel == null) return;

        try
        {
            _auditGrid.SuspendLayout();

            // Create snapshot to avoid collection modification issues
            var snapshot = ViewModel.Entries.ToList();
            _auditGrid.DataSource = snapshot;

            _auditGrid.ResumeLayout();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: UpdateGridData failed: {ex.Message}");
        }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        try
        {
            _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.Entries.Any();
        }
        catch
        {
            // Ignore
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadEntriesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to refresh audit entries: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ApplyFilters()
    {
        if (ViewModel == null) return;

        try
        {
            ViewModel.StartDate = _dtpStartDate?.Value ?? DateTime.MinValue;
            ViewModel.EndDate = _dtpEndDate?.Value ?? DateTime.MaxValue;
            ViewModel.SelectedActionType = _cmbActionType?.SelectedItem?.ToString() == "All" ? null : _cmbActionType?.SelectedItem?.ToString();
            ViewModel.SelectedUser = _cmbUser?.SelectedItem?.ToString() == "All" ? null : _cmbUser?.SelectedItem?.ToString();

            // Trigger reload with filters
            _ = ViewModel.LoadEntriesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: ApplyFilters failed: {ex.Message}");
        }
    }

    private void ToggleAutoRefresh()
    {
        // Implementation for auto-refresh timer would go here
        // For now, just a placeholder
        if (_chkAutoRefresh?.Checked == true)
        {
            // Start timer
        }
        else
        {
            // Stop timer
        }
    }

    private void ExportToCsv()
    {
        try
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv",
                DefaultExt = "csv",
                FileName = $"audit_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            // Export logic would go here
            // For now, just show success message
            MessageBox.Show($"Audit log exported to {sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClosePanel()
    {
        try
        {
            var parentForm = FindForm();
            if (parentForm == null) return;

            // Try to find ClosePanel method on parent form
            var closePanelMethod = parentForm.GetType().GetMethod(
                "ClosePanel",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            closePanelMethod?.Invoke(parentForm, new object[] { Name });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: ClosePanel failed: {ex.Message}");
        }
    }

    private void SubscribeToThemeChanges()
    {
        _themeChangedHandler = (s, theme) =>
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyTheme()));
            }
            else
            {
                ApplyTheme();
            }
        };

        ThemeManager.ThemeChanged += _themeChangedHandler;
    }

    private void ApplyTheme()
    {
        try
        {
            // Theme is applied automatically by SkinManager cascade from parent form
            // No manual color assignments needed
            ThemeManager.ApplyThemeToControl(this);
        }
        catch
        {
            // Ignore theme application failures
        }
    }

    /// <summary>
    /// Disposes resources using SafeDispose pattern to prevent disposal errors.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events
            try
            {
                if (_themeChangedHandler != null)
                    ThemeManager.ThemeChanged -= _themeChangedHandler;
            }
            catch { }

            try
            {
                if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                    ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }
            catch { }

            try
            {
                if (ViewModel != null && _entriesCollectionChangedHandler != null)
                    ViewModel.Entries.CollectionChanged -= _entriesCollectionChangedHandler;
            }
            catch { }

            try
            {
                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshHandler != null)
                        _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                    if (_panelHeaderCloseHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                }
            }
            catch { }

            // Unsubscribe control events
            try { if (_btnRefresh != null && _btnRefreshClickHandler != null) _btnRefresh.Click -= _btnRefreshClickHandler; } catch { }
            try { if (_btnExportCsv != null && _btnExportCsvClickHandler != null) _btnExportCsv.Click -= _btnExportCsvClickHandler; } catch { }
            try { if (_chkAutoRefresh != null && _chkAutoRefreshCheckedChangedHandler != null) _chkAutoRefresh.CheckedChanged -= _chkAutoRefreshCheckedChangedHandler; } catch { }
            try { if (_dtpStartDate != null && _dtpStartDateValueChangedHandler != null) _dtpStartDate.ValueChanged -= _dtpStartDateValueChangedHandler; } catch { }
            try { if (_dtpEndDate != null && _dtpEndDateValueChangedHandler != null) _dtpEndDate.ValueChanged -= _dtpEndDateValueChangedHandler; } catch { }
            try { if (_cmbActionType != null && _cmbActionTypeSelectedIndexChangedHandler != null) _cmbActionType.SelectedIndexChanged -= _cmbActionTypeSelectedIndexChangedHandler; } catch { }
            try { if (_cmbUser != null && _cmbUserSelectedIndexChangedHandler != null) _cmbUser.SelectedIndexChanged -= _cmbUserSelectedIndexChangedHandler; } catch { }

            // Dispose controls using SafeDispose pattern
            try { _auditGrid?.SafeClearDataSource(); } catch { }
            try { _auditGrid?.SafeDispose(); } catch { }
            try { _panelHeader?.Dispose(); } catch { }
            try { _loadingOverlay?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _filterPanel?.Dispose(); } catch { }
            try { _btnRefresh?.Dispose(); } catch { }
            try { _btnExportCsv?.Dispose(); } catch { }
            try { _chkAutoRefresh?.Dispose(); } catch { }
            try { _dtpStartDate?.Dispose(); } catch { }
            try { _dtpEndDate?.Dispose(); } catch { }
            try { _cmbActionType?.SafeClearDataSource(); } catch { }
            try { _cmbActionType?.SafeDispose(); } catch { }
            try { _cmbUser?.SafeClearDataSource(); } catch { }
            try { _cmbUser?.SafeDispose(); } catch { }
        }

        base.Dispose(disposing);
    }
}
