using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Docked panel displaying Department Summary with key metrics and drill-down grid.
    /// Inherits from ScopedPanelBase to ensure proper DI lifetime management for scoped dependencies.
    /// </summary>
    public partial class DepartmentSummaryPanel : ScopedPanelBase<DepartmentSummaryViewModel>
    {
        // UI Controls
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private LegacyGradientPanel? _summaryPanel;
        private SfDataGrid? _metricsGrid;
        private TableLayoutPanel? _summaryCardsPanel;

        // Summary metric labels
        private Label? _lblTotalBudgetValue;
        private Label? _lblTotalActualValue;
        private Label? _lblVarianceValue;
        private Label? _lblOverBudgetCountValue;
        private Label? _lblUnderBudgetCountValue;

        // Status and validation
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;
        private ErrorProvider? _errorProvider;

        // Event handlers for cleanup
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _metricsCollectionChangedHandler;
        private EventHandler? _themeChangedHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;

        /// <summary>
        /// Initializes a new instance with required DI dependencies.
        /// </summary>
        public DepartmentSummaryPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<DepartmentSummaryViewModel>> logger)
            : base(scopeFactory, logger)
        {
            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Size = new Size(540, 400);
            MinimumSize = new Size(420, 360);

            // Apply theme via SfSkinManager (single source of truth)
            try { var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme; Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme); } catch { }
            SetupUI();
            SubscribeToThemeChanges();
        }



        private void SetupUI()
        {
            SuspendLayout();

            // Create root TableLayoutPanel
            var rootTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                AutoSize = true,
                Padding = new Padding(8),
                AccessibleName = "Department Summary Layout"
            };

            // Configure rows
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: Header
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: Summary panel
            rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Row 3: Grid (fills remaining space)

            // Row 1: Panel header
            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Fill,
                Title = "Department Summary",
                AccessibleName = "Department Summary header"
            };
            _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            rootTable.Controls.Add(_panelHeader, 0, 0);

            // Row 2: Summary panel with cards
            _summaryPanel = new LegacyGradientPanel
            {
                Dock = DockStyle.Fill,
                Height = 120,
                Padding = new Padding(8),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                AccessibleName = "Summary metrics panel"
            };
            var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(_summaryPanel, theme);

            _summaryCardsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                AutoSize = true,
                AccessibleName = "Summary cards"
            };

            // Configure equal column widths
            for (int i = 0; i < 5; i++)
            {
                _summaryCardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            }
            _summaryCardsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Create summary cards
            _lblTotalBudgetValue = CreateSummaryCard(_summaryCardsPanel, "Total Budget", "$0", 0, "Total budgeted amount");
            _lblTotalActualValue = CreateSummaryCard(_summaryCardsPanel, "Total Actual", "$0", 1, "Total actual spending");
            _lblVarianceValue = CreateSummaryCard(_summaryCardsPanel, "Variance", "$0", 2, "Difference between budget and actual");
            _lblOverBudgetCountValue = CreateSummaryCard(_summaryCardsPanel, "Over Budget", "0", 3, "Number of departments over budget");
            _lblUnderBudgetCountValue = CreateSummaryCard(_summaryCardsPanel, "Under Budget", "0", 4, "Number of departments under budget");

            _summaryPanel.Controls.Add(_summaryCardsPanel);
            rootTable.Controls.Add(_summaryPanel, 0, 1);

            // Row 3: Data grid for detailed metrics
            _metricsGrid = new SfDataGrid
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
                AccessibleName = "Department metrics grid",
                AccessibleDescription = "Grid displaying budget metrics for each department"
            };

            ConfigureGridColumns();
            rootTable.Controls.Add(_metricsGrid, 0, 2);

            // Add root table to panel
            Controls.Add(rootTable);

            // Status strip (bottom of panel)
            _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel { Text = "Ready" };
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);

            // Error provider
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            // Loading overlay
            _loadingOverlay = new LoadingOverlay
            {
                Message = "Loading department data...",
                Dock = DockStyle.Fill,
                AccessibleName = "Loading overlay"
            };
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();

            // No-data overlay
            _noDataOverlay = new NoDataOverlay
            {
                Message = "No department data available",
                Dock = DockStyle.Fill,
                AccessibleName = "No data overlay"
            };
            Controls.Add(_noDataOverlay);
            _noDataOverlay.BringToFront();

            ResumeLayout(false);
            this.PerformLayout();
            this.Refresh();

            Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
        }

        private Label CreateSummaryCard(TableLayoutPanel parent, string title, string value, int columnIndex, string description)
        {
            var cardPanel = new LegacyGradientPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Padding = new Padding(8),
                AccessibleName = $"{title} card",
                AccessibleDescription = description,
                BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(cardPanel, theme);

            var lblTitle = new GradientLabel
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = false,
                BackColor = Color.Transparent,
                AccessibleName = $"{title} label"
            };
            cardPanel.Controls.Add(lblTitle);

            var lblValue = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                AutoSize = false,
                AccessibleName = $"{title} value"
            };
            cardPanel.Controls.Add(lblValue);

            parent.Controls.Add(cardPanel, columnIndex, 0);
            return lblValue;
        }

        private void ConfigureGridColumns()
        {
            if (_metricsGrid == null) return;

            _metricsGrid.Columns.Clear();

            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = nameof(DepartmentMetric.DepartmentName),
                HeaderText = "Department",
                MinimumWidth = 150,
                AllowSorting = true,
                AllowFiltering = true
            });

            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = nameof(DepartmentMetric.BudgetedAmount),
                HeaderText = "Budgeted",
                MinimumWidth = 120,
                Format = "C2",
                AllowSorting = true
            });

            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = nameof(DepartmentMetric.ActualAmount),
                HeaderText = "Actual",
                MinimumWidth = 120,
                Format = "C2",
                AllowSorting = true
            });

            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = nameof(DepartmentMetric.Variance),
                HeaderText = "Variance",
                MinimumWidth = 120,
                Format = "C2",
                AllowSorting = true
            });

            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = nameof(DepartmentMetric.VariancePercent),
                HeaderText = "Variance %",
                MinimumWidth = 100,
                Format = "N2",
                AllowSorting = true
            });

            _metricsGrid.Columns.Add(new GridCheckBoxColumn
            {
                MappingName = nameof(DepartmentMetric.IsOverBudget),
                HeaderText = "Over Budget",
                MinimumWidth = 100,
                AllowSorting = true,
                AllowFiltering = true
            });
        }

        /// <summary>
        /// Called after ViewModel is resolved from scoped service provider.
        /// Binds ViewModel data and initiates data load.
        /// </summary>
        protected override void OnViewModelResolved(object? viewModel)
        {
            base.OnViewModelResolved(viewModel);
            if (viewModel is not DepartmentSummaryViewModel typedViewModel)
            {
                return;
            }

            // Subscribe to ViewModel property changes
            _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
            typedViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

            // Subscribe to Metrics collection changes
            _metricsCollectionChangedHandler = (s, e) => UpdateGridData();
            typedViewModel.Metrics.CollectionChanged += _metricsCollectionChangedHandler;

            // Initial UI update
            UpdateUI();

            // Defer sizing validation until layout is complete
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

            // Load data asynchronously
            _ = LoadDataSafeAsync();
        }

        private async Task LoadDataSafeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                UpdateStatus("Loading department data...");
                if (ViewModel != null)
                {
                    await ViewModel.LoadDataAsync();
                }
                UpdateStatus("Department data loaded");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Load failed: {ex.Message}");
                if (InvokeRequired)
                {
                    BeginInvoke(new System.Action(() => ShowError(ex)));
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
                $"Failed to load department data: {ex.Message}",
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
                BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
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

                    case nameof(ViewModel.TotalBudget):
                    case nameof(ViewModel.TotalActual):
                    case nameof(ViewModel.Variance):
                    case nameof(ViewModel.VariancePercent):
                    case nameof(ViewModel.DepartmentsOverBudget):
                    case nameof(ViewModel.DepartmentsUnderBudget):
                    case nameof(ViewModel.LastUpdated):
                        UpdateSummaryCards();
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
                BeginInvoke(new System.Action(UpdateUI));
                return;
            }

            try
            {
                UpdateSummaryCards();
                UpdateGridData();
                UpdateNoDataOverlay();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if disposed
            }
        }

        private void UpdateSummaryCards()
        {
            if (ViewModel == null) return;

            try
            {
                if (_lblTotalBudgetValue != null)
                    _lblTotalBudgetValue.Text = ViewModel.TotalBudget.ToString("C0", CultureInfo.CurrentCulture);

                if (_lblTotalActualValue != null)
                    _lblTotalActualValue.Text = ViewModel.TotalActual.ToString("C0", CultureInfo.CurrentCulture);

                if (_lblVarianceValue != null)
                {
                    _lblVarianceValue.Text = ViewModel.Variance.ToString("C0", CultureInfo.CurrentCulture);
                    // Semantic status color: green for under budget, red for over budget
                    _lblVarianceValue.ForeColor = ViewModel.Variance >= 0 ? Color.Green : Color.Red;
                }

                if (_lblOverBudgetCountValue != null)
                {
                    _lblOverBudgetCountValue.Text = ViewModel.DepartmentsOverBudget.ToString(CultureInfo.CurrentCulture);
                    _lblOverBudgetCountValue.ForeColor = Color.Red; // Semantic status
                }

                if (_lblUnderBudgetCountValue != null)
                {
                    _lblUnderBudgetCountValue.Text = ViewModel.DepartmentsUnderBudget.ToString(CultureInfo.CurrentCulture);
                    _lblUnderBudgetCountValue.ForeColor = Color.Green; // Semantic status
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash on UI update failure
                Console.WriteLine($"DepartmentSummaryPanel: UpdateSummaryCards failed: {ex.Message}");
            }
        }

        private void UpdateGridData()
        {
            if (_metricsGrid == null || ViewModel == null) return;

            try
            {
                _metricsGrid.SuspendLayout();

                // Create snapshot to avoid collection modification issues
                var snapshot = ViewModel.Metrics.ToList();
                _metricsGrid.DataSource = snapshot;

                _metricsGrid.ResumeLayout();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DepartmentSummaryPanel: UpdateGridData failed: {ex.Message}");
            }
        }

        private void UpdateNoDataOverlay()
        {
            if (_noDataOverlay == null || ViewModel == null) return;

            try
            {
                _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.Metrics.Any();
            }
            catch
            {
                // Ignore
            }
        }

        private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                UpdateStatus("Refreshing department data...");
                if (ViewModel != null)
                {
                    await ViewModel.LoadDataAsync();
                }
                UpdateStatus("Department data refreshed");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Refresh failed: {ex.Message}");
                MessageBox.Show(
                    $"Failed to refresh data: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SubscribeToThemeChanges()
        {
            _themeChangedHandler = (s, theme) =>
            {
                if (IsDisposed) return;

                if (InvokeRequired)
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(new System.Action(() => ApplyTheme()));
                    }
                }
                else
                {
                    ApplyTheme();
                }
            };

            // Theme subscription removed - handled by SfSkinManager
        }

        private void ApplyTheme()
        {
            // Theme is applied automatically by SfSkinManager cascade from parent form
            // No manual color assignments needed
        }

        /// <summary>
        /// Loads department summary data asynchronously.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;
                UpdateStatus("Loading department summary data...");
                await LoadDataSafeAsync(ct);
                SetHasUnsavedChanges(false);
                UpdateStatus("Department summary loaded successfully");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Load cancelled");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Load failed: {ex.Message}");
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Saves department summary data (read-only panel, no-op).
        /// </summary>
        public override async Task SaveAsync(CancellationToken ct)
        {
            // Read-only panel, no save operation
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates department summary state.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            var errors = new List<ValidationItem>();
            // Read-only panel, always valid
            return ValidationResult.Success;
        }

        /// <summary>
        /// Focuses the first error (no errors in read-only panel).
        /// </summary>
        public override void FocusFirstError()
        {
            // No editable fields to focus
        }

        /// <summary>
        /// Updates the status strip with a message.
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (_statusLabel != null && !IsDisposed)
            {
                if (InvokeRequired)
                    BeginInvoke(() => _statusLabel.Text = message);
                else
                    _statusLabel.Text = message;
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
                // No theme change handlers needed - SfSkinManager cascade handles all theming.

                try
                {
                    if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                        ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
                }
                catch { }

                try
                {
                    if (ViewModel != null && _metricsCollectionChangedHandler != null)
                        ViewModel.Metrics.CollectionChanged -= _metricsCollectionChangedHandler;
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

                // Dispose controls using SafeDispose pattern
                try { _metricsGrid?.SafeClearDataSource(); } catch { }
                try { _metricsGrid?.SafeDispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }
                try { _summaryCardsPanel?.Dispose(); } catch { }
                try { _summaryPanel?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }
    }

}
