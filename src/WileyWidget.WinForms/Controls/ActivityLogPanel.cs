using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
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
        private Button? _btnClearLog;
        private CheckBox? _chkAutoRefresh;

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
            BackColor = System.Drawing.Color.White;

            // Main vertical split for header + grid
            var mainSplit = new SplitContainer
            {
                Name = "MainSplit",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
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

            _panelHeader = new PanelHeader
            {
                Text = "Recent Activity (Navigation Hub Activity Log)"
            };
            headerPanel.Controls.Add(_panelHeader);

            // Control buttons
            _chkAutoRefresh = new CheckBox
            {
                Text = "Auto-refresh",
                AutoSize = true,
                Checked = true,
                Dock = DockStyle.Right,
                Padding = new Padding(8)
            };
            _chkAutoRefresh.CheckedChanged += (s, e) =>
            {
                if (_autoRefreshTimer != null)
                {
                    _autoRefreshTimer.Enabled = _chkAutoRefresh.Checked;
                }
            };
            headerPanel.Controls.Add(_chkAutoRefresh);

            _btnClearLog = new Button
            {
                Text = "Clear",
                AutoSize = true,
                Dock = DockStyle.Right,
                Padding = new Padding(8)
            };
            _btnClearLog.Click += (s, e) => ClearActivityLog();
            headerPanel.Controls.Add(_btnClearLog);

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

            // Configure columns
            _activityGrid.Columns.Add(new GridTextColumn
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
            _autoRefreshTimer.Tick += async (s, e) =>
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
            };
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
                SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");
                if (_activityGrid != null)
                {
                    SfSkinManager.SetVisualStyle(_activityGrid, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to apply theme");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer?.Dispose();
                _activityGrid?.Dispose();
                _panelHeader?.Dispose();
                _btnClearLog?.Dispose();
                _chkAutoRefresh?.Dispose();
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
