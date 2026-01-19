using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using CheckBoxAdv = Syncfusion.Windows.Forms.Tools.CheckBoxAdv;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.Models;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Activity Log panel for displaying recent navigation and application events.
    /// Replaces left dock navigation buttons, providing activity audit trail.
    /// Uses SfDataGrid to show recent actions in a user-friendly interface.
    /// </summary>
    public partial class ActivityLogPanel : ScopedPanelBase<ActivityLogViewModel>
    {
        private SfDataGrid? _activityGrid;
        private PanelHeader? _panelHeader;
        private System.Windows.Forms.Timer? _autoRefreshTimer;
        private SfButton? _btnClearLog;
        private CheckBoxAdv? _chkAutoRefresh;

        /// <summary>
        /// Initializes a new instance with required DI dependencies.
        /// </summary>
        public ActivityLogPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<ActivityLogViewModel>> logger)
            : base(scopeFactory, logger)
        {
            InitializeComponent();
            ApplyTheme();
            SetupUI();
            InitializeAutoRefresh();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            Name = "ActivityLogPanel";
            Dock = DockStyle.Fill;

            // Main horizontal split for header (top) + grid (bottom)
            var mainSplit = new SplitContainer
            {
                Name = "MainSplit",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 60
            };

            // Header panel (top section)
            var headerPanel = new Panel
            {
                Name = "HeaderPanel",
                Dock = DockStyle.Fill,
                Height = 60,
                Padding = new Padding(8)
            };

            // Control buttons - add right-docked controls first so the title fills remaining space
            _btnClearLog = new SfButton
            {
                Text = "Clear",
                AutoSize = true,
                Dock = DockStyle.Right,
                Padding = new Padding(8)
            };
            _btnClearLog.Click += OnClearLogClicked;
            _btnClearLog.AccessibleName = "Clear Activity Log";
            _btnClearLog.AccessibleDescription = "Clears all activity log entries";
            _btnClearLog.TabIndex = 2;
            headerPanel.Controls.Add(_btnClearLog);

            _chkAutoRefresh = new CheckBoxAdv
            {
                Text = "Auto-refresh",
                AutoSize = true,
                Checked = true,
                Dock = DockStyle.Right,
                Padding = new Padding(8)
            };
            _chkAutoRefresh.CheckedChanged += OnAutoRefreshCheckedChanged;
            _chkAutoRefresh.AccessibleName = "Auto Refresh";
            _chkAutoRefresh.AccessibleDescription = "Toggle auto refresh for activity log";
            _chkAutoRefresh.TabIndex = 3;
            headerPanel.Controls.Add(_chkAutoRefresh);

            _panelHeader = new PanelHeader
            {
                Title = "Recent Activity (Navigation Hub Activity Log)",
                Dock = DockStyle.Fill
            };
            _panelHeader.AccessibleName = "Activity Log Header";
            _panelHeader.AccessibleDescription = "Header for the activity log panel";
            _panelHeader.TabIndex = 0;
            headerPanel.Controls.Add(_panelHeader);

            mainSplit.Panel1.Controls.Add(headerPanel);

            // Data grid (bottom section)
            _activityGrid = new SfDataGrid
            {
                Name = "ActivityGrid",
                Dock = DockStyle.Fill,
                AllowResizingColumns = true,
                AllowSorting = true,
                AllowFiltering = true,
                ShowBusyIndicator = false
            };
            _activityGrid.AccessibleName = "Activity Log Grid";
            _activityGrid.AccessibleDescription = "Grid listing recent activity entries";
            _activityGrid.TabIndex = 1;

            // Configure columns - use DateTime column for timestamps
            _activityGrid.Columns.Add(new GridDateTimeColumn
            {
                MappingName = "Timestamp",
                HeaderText = "Time",
                Format = "g",
                AllowSorting = true,
                Width = 150
            });

            _activityGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Activity",
                HeaderText = "Activity",
                AllowSorting = true,
                Width = 150
            });

            _activityGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Details",
                HeaderText = "Details",
                AllowSorting = false,
                Width = 300
            });

            _activityGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Status",
                HeaderText = "Status",
                AllowSorting = true,
                Width = 80
            });

            mainSplit.Panel2.Controls.Add(_activityGrid);

            Controls.Add(mainSplit);

            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            try
            {
                if (_activityGrid != null && ViewModel != null)
                {
                    _activityGrid.DataSource = ViewModel.ActivityEntries;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to setup activity grid");
            }
        }

        private void InitializeAutoRefresh()
        {
            _autoRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000 // Refresh every 5 seconds
            };
            _autoRefreshTimer.Tick += OnAutoRefreshTick;
            _autoRefreshTimer.Start();
        }

        private void ClearActivityLog()
        {
            if (MessageBox.Show(
                "Clear all activity log entries?",
                "Clear Activity Log",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    ViewModel?.ClearActivityLog();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to clear activity log");
                    MessageBox.Show($"Failed to clear log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyTheme()
        {
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrEmpty(theme))
                {
                    SfSkinManager.SetVisualStyle(this, theme);
                    if (_activityGrid != null)
                    {
                        // Prefer theme cascade; set ThemeName on grid as a best-effort to ensure consistent visuals
                        _activityGrid.ThemeName = theme;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to apply theme");
            }
        }

        private void OnClearLogClicked(object? sender, EventArgs e) => ClearActivityLog();

        private void OnAutoRefreshCheckedChanged(object? sender, EventArgs e)
        {
            if (_autoRefreshTimer != null && _chkAutoRefresh != null)
            {
                _autoRefreshTimer.Enabled = _chkAutoRefresh.Checked;
            }
        }

        private async void OnAutoRefreshTick(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel != null && _chkAutoRefresh?.Checked == true)
                {
                    await ViewModel.RefreshActivityEntriesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Auto-refresh failed");
            }
        }

        /// <summary>
        /// Validate the activity log. Since it's append-only, validation passes if ViewModel is ready.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            // Activity log is append-only and read-only from the UI.
            // Valid if the ViewModel is loaded.
            if (ViewModel == null)
            {
                return ValidationResult.Failed(new ValidationItem("ViewModel", "Activity log ViewModel not loaded", ValidationSeverity.Error));
            }

            return await Task.FromResult(ValidationResult.Success);
        }

        /// <summary>
        /// Save is a no-op for the read-only Activity Log panel.
        /// </summary>
        public override Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Load activity entries from the ViewModel asynchronously.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            if (ViewModel == null)
            {
                return;
            }

            try
            {
                var ct_op = RegisterOperation();
                IsBusy = true;
                await ViewModel.RefreshActivityEntriesAsync();
                SetHasUnsavedChanges(false);
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("ActivityLogPanel load cancelled");
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_autoRefreshTimer != null)
                {
                    try { _autoRefreshTimer.Tick -= OnAutoRefreshTick; } catch { }
                    try { _autoRefreshTimer.Stop(); } catch { }
                    try { _autoRefreshTimer.Dispose(); } catch { }
                    _autoRefreshTimer = null;
                }

                if (_chkAutoRefresh != null)
                {
                    try { _chkAutoRefresh.CheckedChanged -= OnAutoRefreshCheckedChanged; } catch { }
                    try { _chkAutoRefresh.Dispose(); } catch { }
                    _chkAutoRefresh = null;
                }

                if (_btnClearLog != null)
                {
                    try { _btnClearLog.Click -= OnClearLogClicked; } catch { }
                    try { _btnClearLog.Dispose(); } catch { }
                    _btnClearLog = null;
                }

                try { _activityGrid?.Dispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// ViewModel for ActivityLogPanel.
    /// Manages activity entry collection and refresh operations.
    /// </summary>
    public class ActivityLogViewModel
    {
        private readonly ILogger<ActivityLogViewModel>? _logger;

        /// <summary>
        /// Observable collection of activity entries.
        /// </summary>
        public ObservableCollection<ActivityLog> ActivityEntries { get; } = new();

        public ActivityLogViewModel(ILogger<ActivityLogViewModel>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Refresh activity entries from data source.
        /// </summary>
        public async System.Threading.Tasks.Task RefreshActivityEntriesAsync()
        {
            // TODO: Load from database via repository
            // For now, this is a placeholder for integration
            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Add a new activity entry.
        /// </summary>
        public void AddActivity(ActivityLog activity)
        {
            if (activity != null)
            {
                ActivityEntries.Insert(0, activity); // Add to top of list
                // Keep only last 500 entries
                while (ActivityEntries.Count > 500)
                {
                    ActivityEntries.RemoveAt(ActivityEntries.Count - 1);
                }
            }
        }

        /// <summary>
        /// Clear all activity log entries.
        /// </summary>
        public void ClearActivityLog()
        {
            ActivityEntries.Clear();
        }
    }
}
