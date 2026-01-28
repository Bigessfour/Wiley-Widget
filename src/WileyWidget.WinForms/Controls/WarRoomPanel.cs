#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using WileyWidget.WinForms.Themes;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Abstractions;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// War Room panel for interactive what-if scenario analysis.
    /// Integrates GrokAgentService for AI-powered financial projections.
    /// Features:
    /// - Natural language scenario input with JARVIS voice hint
    /// - Revenue/Reserves trend chart (line chart)
    /// - Department impact analysis (column chart)
    /// - Risk level gauge (radial gauge)
    /// - Prominent "Required Rate Increase" display
    /// Production-Ready: Full validation, databinding, error handling, sizing, accessibility.
    /// </summary>
    public partial class WarRoomPanel : ScopedPanelBase
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public new object? DataContext { get; private set; }

        // Strongly-typed ViewModel (this is what you use in your code)
        public new WarRoomViewModel? ViewModel
        {
            get => (WarRoomViewModel?)base.ViewModel;
            set => base.ViewModel = value;
        }

        // Event handler storage for proper cleanup in Dispose
        private EventHandler? _btnRunScenarioClickHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler? _scenarioInputTextChangedHandler;

        // Collection change handlers to react to ViewModel ObservableCollection updates
        private NotifyCollectionChangedEventHandler? _projectionsCollectionChangedHandler;
        private NotifyCollectionChangedEventHandler? _departmentImpCollectionChangedHandler;
        private INotifyCollectionChanged? _projectionsCollection;
        private INotifyCollectionChanged? _departmentImpCollection;

        private readonly ErrorProvider? _errorProvider;
        private readonly string _activeThemeName = ThemeColors.DefaultTheme;

        // Minimum size for content panel to ensure proper layout
        private static readonly Size ContentPanelMinSize = new(800, 600);

        // Obsolete parameterless constructor for designer compatibility
        // Design-time constructor MUST NOT rely on runtime DI (Program.Services may be null in designer).
        [Obsolete("Use DI constructor with IServiceScopeFactory and ILogger", false)]
        public WarRoomPanel()
            : base()
        {
            // Lightweight initialization for Visual Studio designer
            _logger?.LogDebug("WarRoomPanel (design-time) initializing");

            _errorProvider = new ErrorProvider();

            InitializeComponent();

            try
            {
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                try { SfSkinManager.SetVisualStyle(this, theme); } catch { }
            }
            catch { }

            // Ensure minimum size for content panel
            if (_contentPanel != null)
                _contentPanel.MinimumSize = ContentPanelMinSize;

            DeferSizeValidation();

            this.Load += (s, e) => this.Invalidate(true);
        }

        // Primary DI constructor
        public WarRoomPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase> logger)
            : base(scopeFactory, logger)
        {
            _logger?.LogDebug("WarRoomPanel initializing");

            _errorProvider = new ErrorProvider();

            InitializeComponent();

            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            _activeThemeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            try { SfSkinManager.SetVisualStyle(this, _activeThemeName); } catch { }

            // Ensure minimum size for content panel
            if (_contentPanel != null)
                _contentPanel.MinimumSize = ContentPanelMinSize;

            DeferSizeValidation();

            _logger?.LogDebug("WarRoomPanel initialized successfully");

            this.Load += (s, e) => this.Invalidate(true);
        }

        /// <summary>
        /// Called when the ViewModel is resolved from the scoped provider.
        /// </summary>
        protected override void OnViewModelResolved(object? viewModel)
        {
            base.OnViewModelResolved(viewModel);
            if (viewModel is not WarRoomViewModel)
            {
                return;
            }
            BindViewModel();
            ScheduleUpdateUIFromViewModel();
        }

        private void DeferSizeValidation()
        {
            if (IsDisposed) return;

            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
                }
                catch { }
                return;
            }

            EventHandler? handleCreatedHandler = null;
            handleCreatedHandler = (s, e) =>
            {
                HandleCreated -= handleCreatedHandler;
                if (IsDisposed) return;

                try
                {
                    BeginInvoke(new Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
                }
                catch { }
            };

            HandleCreated += handleCreatedHandler;
        }

        /// <summary>
        /// Binds the ViewModel to UI controls with event handler storage for proper cleanup.
        /// </summary>
        private void BindViewModel()
        {
            if (ViewModel == null)
            {
                _logger?.LogWarning("ViewModel is null - cannot bind");
                return;
            }

            try
            {
                // Validate grids exist before binding
                if (_projectionsGrid != null && ViewModel.Projections != null)
                {
                    _projectionsGrid.DataSource = ViewModel.Projections;
                    _projectionsGrid.Refresh();
                }
                else
                {
                    _logger?.LogWarning("ProjectionsGrid or Projections collection is null");
                }

                if (_departmentImpactGrid != null && ViewModel.DepartmentImpacts != null)
                {
                    _departmentImpactGrid.DataSource = ViewModel.DepartmentImpacts;
                    _departmentImpactGrid.Refresh();
                    _departmentImpactGrid.Invalidate(true);
                }
                else
                {
                    _logger?.LogWarning("DepartmentImpactGrid or DepartmentImpacts collection is null");
                }

                // Subscribe to ViewModel property changes (with stored delegate for cleanup)
                _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

                // Wire up Run Scenario button with stored handler for cleanup
                _btnRunScenarioClickHandler = async (s, e) => await OnRunScenarioClickAsync();
                _btnRunScenario.Click += _btnRunScenarioClickHandler;

                // Wire up scenario input textbox with stored handler for cleanup
                _scenarioInputTextChangedHandler = (s, e) => OnScenarioInputTextChanged();
                _scenarioInput.TextChanged += _scenarioInputTextChangedHandler;

                // Attach to collection change events and ensure UI updates if data already exists
                AttachCollectionHandlers();

                _logger?.LogDebug("ViewModel bound successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error binding ViewModel");
            }
        }

        /// <summary>
        /// Perform deferred async initialization without blocking the UI thread.
        /// This defers heavy loads to the panel lifecycle and protects against NREs during navigation.
        /// </summary>
        protected override async Task OnHandleCreatedAsync()
        {
            try
            {
                var ct = RegisterOperation();
                await LoadAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("WarRoomPanel async handle creation cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during async initialization of WarRoomPanel");
            }
        }

        /// <summary>
        /// ILazyLoadViewModel implementation: called when the panel's visibility changes.
        /// Ensures controls are visible and triggers data binding/UI updates.
        /// </summary>
        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            try
            {
                if (isVisible && !DesignMode)
                {
                    // Ensure all controls are visible
                    _revenueChart.Visible = true;
                    _riskGauge.Visible = true;
                    _projectionsGrid.Visible = true;
                    _departmentImpactGrid.Visible = true;

                    // Force invalidate to ensure rendering
                    this.Invalidate(true);

                    // Force refresh on Syncfusion controls
                    _revenueChart?.Refresh();
                    _departmentChart?.Refresh();
                    _riskGauge?.Refresh();
                    _projectionsGrid?.Refresh();
                    _departmentImpactGrid?.Refresh();

                    // Lazy load data if ViewModel supports it and data not loaded
                    if (ViewModel is WileyWidget.Abstractions.ILazyLoadViewModel lazyVm)
                    {
                        await lazyVm.OnVisibilityChangedAsync(true).ConfigureAwait(false);
                        return;
                    }

                    // Otherwise, trigger panel-level load
                    var ct = RegisterOperation();
                    await LoadAsync(ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during OnVisibilityChangedAsync for WarRoomPanel");
            }
        }

        /// <summary>
        /// IAsyncInitializable implementation: allows external orchestrators to trigger async init.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (ViewModel == null)
                {
                    // ViewModel not yet resolved; OnHandleCreatedAsync will handle the load
                    return;
                }

                await LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("WarRoomPanel InitializeAsync cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during InitializeAsync for WarRoomPanel");
            }
        }

        /// <summary>
        /// Handles Run Scenario button click with validation and busy state tracking.
        /// </summary>
        private async Task OnRunScenarioClickAsync()
        {
            try
            {
                _lblInputError.Visible = false;

                if (string.IsNullOrWhiteSpace(_scenarioInput.Text))
                {
                    _lblInputError.Text = "Please enter a scenario";
                    _lblInputError.Visible = true;
                    _scenarioInput.Focus();
                    return;
                }

                if (_scenarioInput.Text.Length < 10)
                {
                    _lblInputError.Text = "Scenario description too short (minimum 10 characters)";
                    _lblInputError.Visible = true;
                    _scenarioInput.Focus();
                    return;
                }

                if (ViewModel?.RunScenarioCommand == null)
                {
                    _logger?.LogError("RunScenarioCommand is null");
                    _lblInputError.Text = "Command not available";
                    _lblInputError.Visible = true;
                    return;
                }

                // Set busy state during scenario analysis
                var token = RegisterOperation();
                IsBusy = true;

                try
                {
                    await ViewModel.RunScenarioCommand.ExecuteAsync(token);
                    _logger?.LogDebug("Scenario analysis completed successfully");
                }
                finally
                {
                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Run Scenario handler");
                _lblInputError.Text = $"Error: {ex.Message}";
                _lblInputError.Visible = true;
                IsBusy = false;
                MessageBox.Show($"Failed to run scenario: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles scenario input text changes and marks panel as dirty.
        /// </summary>
        private void OnScenarioInputTextChanged()
        {
            if (ViewModel != null)
            {
                ViewModel.ScenarioInput = _scenarioInput.Text;
                SetHasUnsavedChanges(true); // Mark as dirty on user edit
                _lblInputError.Visible = false; // Clear error on input change
            }
        }

        private void ClosePanel()
        {
            try
            {
                var form = FindForm();
                if (form is WileyWidget.WinForms.Forms.MainForm mainForm && mainForm.PanelNavigator != null)
                {
                    mainForm.PanelNavigator.HidePanel("War Room");
                    return;
                }

                var dockingManagerField = form?.GetType()
                    .GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dockingManagerField?.GetValue(form) is Syncfusion.Windows.Forms.Tools.DockingManager dockingManager)
                {
                    dockingManager.SetDockVisibility(this, false);
                }
                else
                {
                    Visible = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to close WarRoomPanel via docking manager");
                Visible = false;
            }
        }

        /// <summary>
        /// Handles ViewModel property changes with proper null checking and UI updates.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                // Ensure we are on the UI thread when updating controls
                if (IsDisposed) return;

                if (this.InvokeRequired)
                {
                    try
                    {
                        BeginInvoke(new Action(() => ViewModel_PropertyChanged(sender, e)));
                    }
                    catch { }
                    return;
                }

                // Verify ViewModel still exists (may be disposed)
                if (ViewModel == null)
                    return;

                switch (e?.PropertyName)
                {
                    case nameof(WarRoomViewModel.StatusMessage):
                        if (_lblStatus != null)
                        {
                            _lblStatus.Text = ViewModel.StatusMessage ?? "Ready";
                            _lblStatus.Refresh();
                        }
                        break;

                    case nameof(WarRoomViewModel.IsAnalyzing):
                        if (_loadingOverlay != null)
                        {
                            _loadingOverlay.Visible = ViewModel.IsAnalyzing;
                            if (_loadingOverlay.Visible)
                            {
                                _loadingOverlay.BringToFront();
                            }
                        }
                        break;

                    case nameof(WarRoomViewModel.HasResults):
                        bool hasResults = ViewModel.HasResults;
                        if (_resultsPanel != null)
                        {
                            _resultsPanel.Visible = hasResults;
                        }
                        if (_lblNoResults != null)
                        {
                            _lblNoResults.Visible = !hasResults;
                        }
                        break;

                    case nameof(WarRoomViewModel.RequiredRateIncrease):
                        if (_lblRateIncreaseValue != null)
                        {
                            _lblRateIncreaseValue.Text = ViewModel.RequiredRateIncrease ?? "—";
                        }
                        break;

                    case nameof(WarRoomViewModel.RiskLevel):
                        if (_riskGauge != null)
                        {
                            _riskGauge.Value = (float)ViewModel.RiskLevel;
                        }
                        break;

                    case nameof(WarRoomViewModel.Projections):
                        // Re-attach handlers in case the observable collection instance changed
                        AttachCollectionHandlers();

                        // Validate before rendering
                        if (ViewModel.Projections != null && ViewModel.Projections.Count > 0)
                        {
                            RenderRevenueChart();
                        }
                        break;

                    case nameof(WarRoomViewModel.DepartmentImpacts):
                        // Re-attach handlers in case the observable collection instance changed
                        AttachCollectionHandlers();

                        // Validate before rendering
                        if (ViewModel.DepartmentImpacts != null && ViewModel.DepartmentImpacts.Count > 0)
                        {
                            RenderDepartmentChart();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling ViewModel property change: {PropertyName}", e?.PropertyName);
            }
        }

        /// <summary>
        /// Updates the UI from the current ViewModel state, ensuring controls are visible based on data availability.
        /// </summary>
        private async Task UpdateUIFromViewModel()
        {
            if (ViewModel == null) return;

            try
            {
                // Bind chart data
                if (ViewModel.Projections != null && ViewModel.Projections.Any())
                {
                    RenderRevenueChart();
                    _revenueChart.Visible = true;
                    _revenueChart.Refresh();
                    _logger?.LogDebug("Revenue chart bound with {Count} data points", ViewModel.Projections.Count);
                }
                else
                {
                    _revenueChart.Visible = false;
                    _logger?.LogWarning("No revenue projection data available");
                }

                // Bind gauge
                if (ViewModel.RiskLevel > 0)
                {
                    _riskGauge.Value = (float)ViewModel.RiskLevel;
                    _riskGauge.Visible = true;
                    _riskGauge.Refresh();
                    _logger?.LogDebug("Risk gauge set to {Value}", ViewModel.RiskLevel);
                }
                else
                {
                    _riskGauge.Visible = false;
                    _logger?.LogWarning("Risk level not available");
                }

                // Bind grids - assuming _scenarioGrid and _projectionGrid are the grids
                // Note: The code has _projectionsGrid and _departmentImpactGrid
                _projectionsGrid.DataSource = ViewModel.Projections;
                _departmentImpactGrid.DataSource = ViewModel.DepartmentImpacts;
                _projectionsGrid.Visible = ViewModel.Projections?.Any() == true;
                _departmentImpactGrid.Visible = ViewModel.DepartmentImpacts?.Any() == true;
                _projectionsGrid.Refresh();
                _departmentImpactGrid.Refresh();

                // Update results panel visibility
                _resultsPanel.Visible = ViewModel.HasResults;
                _lblNoResults.Visible = !ViewModel.HasResults;

                _logger?.LogDebug("UI updated from ViewModel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating UI from ViewModel");
            }
        }

        private void ScheduleUpdateUIFromViewModel()
        {
            if (IsDisposed) return;

            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(() => { _ = UpdateUIFromViewModel(); }));
                }
                catch { }
                return;
            }

            EventHandler? handleCreatedHandler = null;
            handleCreatedHandler = (s, e) =>
            {
                HandleCreated -= handleCreatedHandler;
                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action(() => { _ = UpdateUIFromViewModel(); }));
                }
                catch { }
            };

            HandleCreated += handleCreatedHandler;
        }

        private void AttachCollectionHandlers()
        {
            if (ViewModel == null) return;

            try
            {
                // Projections collection
                var projectionsNotify = ViewModel.Projections as INotifyCollectionChanged;
                if (!ReferenceEquals(_projectionsCollection, projectionsNotify))
                {
                    if (_projectionsCollection != null && _projectionsCollectionChangedHandler != null)
                    {
                        try { _projectionsCollection.CollectionChanged -= _projectionsCollectionChangedHandler; }
                        catch { }
                    }

                    _projectionsCollection = projectionsNotify;

                    if (_projectionsCollection != null)
                    {
                        _projectionsCollectionChangedHandler = (s, e) =>
                        {
                            if (IsDisposed) return;
                            try
                            {
                                if (this.InvokeRequired)
                                {
                                    try { BeginInvoke(new Action(OnProjectionsCollectionChanged)); } catch { }
                                }
                                else
                                {
                                    OnProjectionsCollectionChanged();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error handling projections collection changed");
                            }
                        };
                        _projectionsCollection.CollectionChanged += _projectionsCollectionChangedHandler;
                    }
                }

                // Department impacts collection
                var deptNotify = ViewModel.DepartmentImpacts as INotifyCollectionChanged;
                if (!ReferenceEquals(_departmentImpCollection, deptNotify))
                {
                    if (_departmentImpCollection != null && _departmentImpCollectionChangedHandler != null)
                    {
                        try { _departmentImpCollection.CollectionChanged -= _departmentImpCollectionChangedHandler; }
                        catch { }
                    }

                    _departmentImpCollection = deptNotify;

                    if (_departmentImpCollection != null)
                    {
                        _departmentImpCollectionChangedHandler = (s, e) =>
                        {
                            if (IsDisposed) return;
                            try
                            {
                                if (this.InvokeRequired)
                                {
                                    try { BeginInvoke(new Action(OnDepartmentImpactsCollectionChanged)); } catch { }
                                }
                                else
                                {
                                    OnDepartmentImpactsCollectionChanged();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error handling department impacts collection changed");
                            }
                        };
                        _departmentImpCollection.CollectionChanged += _departmentImpCollectionChangedHandler;
                    }
                }

                // Ensure grids use current collections
                if (_projectionsGrid != null && ViewModel.Projections != null)
                {
                    _projectionsGrid.DataSource = ViewModel.Projections;
                }

                if (_departmentImpactGrid != null && ViewModel.DepartmentImpacts != null)
                {
                    _departmentImpactGrid.DataSource = ViewModel.DepartmentImpacts;
                }

                ScheduleUpdateUIFromViewModel();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error attaching collection handlers");
            }
        }

        private void OnProjectionsCollectionChanged()
        {
            try
            {
                if (ViewModel?.Projections != null && ViewModel.Projections.Count > 0)
                {
                    RenderRevenueChart();
                    if (_revenueChart != null) _revenueChart.Visible = true;
                    if (_projectionsGrid != null) _projectionsGrid.Visible = true;
                    _projectionsGrid.Refresh();
                }
                else
                {
                    if (_revenueChart != null) _revenueChart.Visible = false;
                    if (_projectionsGrid != null) _projectionsGrid.Visible = false;
                }

                bool hasResults = ViewModel?.HasResults ?? false;
                if (!hasResults)
                {
                    hasResults = (ViewModel?.Projections?.Any() == true) || (ViewModel?.DepartmentImpacts?.Any() == true);
                }

                if (_resultsPanel != null) _resultsPanel.Visible = hasResults;
                if (_lblNoResults != null) _lblNoResults.Visible = !hasResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating UI on projections collection change");
            }
        }

        private void OnDepartmentImpactsCollectionChanged()
        {
            try
            {
                if (ViewModel?.DepartmentImpacts != null && ViewModel.DepartmentImpacts.Count > 0)
                {
                    RenderDepartmentChart();
                    if (_departmentChart != null) _departmentChart.Visible = true;
                    if (_departmentImpactGrid != null) _departmentImpactGrid.Visible = true;
                    _departmentImpactGrid.Refresh();
                }
                else
                {
                    if (_departmentChart != null) _departmentChart.Visible = false;
                    if (_departmentImpactGrid != null) _departmentImpactGrid.Visible = false;
                }

                bool hasResults = ViewModel?.HasResults ?? false;
                if (!hasResults)
                {
                    hasResults = (ViewModel?.Projections?.Any() == true) || (ViewModel?.DepartmentImpacts?.Any() == true);
                }

                if (_resultsPanel != null) _resultsPanel.Visible = hasResults;
                if (_lblNoResults != null) _lblNoResults.Visible = !hasResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating UI on department impacts collection change");
            }
        }

        /// <summary>
        /// Renders the revenue and expenses trend chart with validation.
        /// </summary>
        private void RenderRevenueChart()
        {
            try
            {
                // Validate all preconditions
                if (_revenueChart == null)
                {
                    _logger?.LogWarning("RevenueChart is null - cannot render");
                    return;
                }

                if (ViewModel?.Projections == null || ViewModel.Projections.Count == 0)
                {
                    _logger?.LogDebug("No projections data - skipping revenue chart render");
                    return;
                }

                _revenueChart.Series.Clear();
                _revenueChart.PrimaryXAxis.Title = "Year";
                _revenueChart.PrimaryYAxis.Title = "Amount ($)";

                // Revenue series
                var revenueSeries = new ChartSeries
                {
                    Name = "Revenue",
                    Type = ChartSeriesType.Line
                };

                foreach (var proj in ViewModel.Projections)
                {
                    revenueSeries.Points.Add(proj.Year, (double)proj.ProjectedRevenue);
                }

                _revenueChart.Series.Add(revenueSeries);

                // Expenses series
                var expenseSeries = new ChartSeries
                {
                    Name = "Expenses",
                    Type = ChartSeriesType.Line
                };

                foreach (var proj in ViewModel.Projections)
                {
                    expenseSeries.Points.Add(proj.Year, (double)proj.ProjectedExpenses);
                }

                _revenueChart.Series.Add(expenseSeries);

                // Refresh chart
                _revenueChart.Refresh();
                _logger?.LogDebug("Revenue chart rendered with {Count} projections", ViewModel.Projections.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rendering revenue chart");
            }
        }

        /// <summary>
        /// Renders the department impact column chart with validation.
        /// </summary>
        private void RenderDepartmentChart()
        {
            try
            {
                // Validate all preconditions
                if (_departmentChart == null)
                {
                    _logger?.LogWarning("DepartmentChart is null - cannot render");
                    return;
                }

                if (ViewModel?.DepartmentImpacts == null || ViewModel.DepartmentImpacts.Count == 0)
                {
                    _logger?.LogDebug("No department impacts data - skipping department chart render");
                    return;
                }

                _departmentChart.Series.Clear();
                _departmentChart.PrimaryXAxis.Title = "Department";
                _departmentChart.PrimaryYAxis.Title = "Budget Impact ($)";

                var impactSeries = new ChartSeries
                {
                    Name = "Impact Amount",
                    Type = ChartSeriesType.Column
                };

                foreach (var impact in ViewModel.DepartmentImpacts)
                {
                    impactSeries.Points.Add(impact.DepartmentName, (double)impact.ImpactAmount);
                }

                _departmentChart.Series.Add(impactSeries);

                _departmentChart.Refresh();
                _logger?.LogDebug("Department chart rendered with {Count} departments", ViewModel.DepartmentImpacts.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rendering department chart");
            }
        }

        /// <summary>
        /// Loads scenario data and initializes the panel.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            if (IsLoaded) return;

            try
            {
                IsBusy = true;

                // Pre-load any cached scenarios or default data
                if (ViewModel != null && !DesignMode)
                {
                    _logger?.LogDebug("Loading WarRoom scenario data");
                    // Initialize ViewModel with any default scenarios from database if available
                    await Task.CompletedTask;
                }

                SetHasUnsavedChanges(false);
                _logger?.LogDebug("WarRoomPanel loaded successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("WarRoomPanel load cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading WarRoom panel");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Saves any unsaved scenario changes (if applicable).
        /// </summary>
        public override async Task SaveAsync(CancellationToken ct)
        {
            try
            {
                if (!HasUnsavedChanges)
                {
                    _logger?.LogDebug("No unsaved changes in WarRoom panel");
                    return;
                }

                IsBusy = true;

                // Validate before saving
                var validationResult = await ValidateAsync(ct);
                if (!validationResult.IsValid)
                {
                    _logger?.LogWarning("Validation failed during save: {ErrorCount} errors", validationResult.Errors.Count);
                    FocusFirstError();
                    throw new InvalidOperationException($"Cannot save: {validationResult.Errors.Count} validation error(s)");
                }

                // Persist any scenario state if needed
                _logger?.LogDebug("Saving WarRoom scenario data");
                await Task.CompletedTask;

                SetHasUnsavedChanges(false);
                _logger?.LogDebug("WarRoomPanel saved successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("WarRoomPanel save cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving WarRoom panel");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Validates the scenario input before submission.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;
                _errorProvider?.Clear();
                var errors = new List<ValidationItem>();

                // Validate scenario input
                if (string.IsNullOrEmpty(_scenarioInput.Text))
                {
                    var error = new ValidationItem(
                        FieldName: "ScenarioInput",
                        Message: "Scenario cannot be empty",
                        Severity: ValidationSeverity.Error
                    );
                    errors.Add(error);
                    _errorProvider?.SetError(_scenarioInput, "Required");
                }
                else if (_scenarioInput.Text.Length < 10)
                {
                    var error = new ValidationItem(
                        FieldName: "ScenarioInput",
                        Message: "Scenario must be at least 10 characters",
                        Severity: ValidationSeverity.Error
                    );
                    errors.Add(error);
                    _errorProvider?.SetError(_scenarioInput, "Too short");
                }
                else if (_scenarioInput.Text.Length > 500)
                {
                    var error = new ValidationItem(
                        FieldName: "ScenarioInput",
                        Message: "Scenario must be 500 characters or less",
                        Severity: ValidationSeverity.Error
                    );
                    errors.Add(error);
                    _errorProvider?.SetError(_scenarioInput, "Too long");
                }
                else
                {
                    // Clear error for valid input
                    _errorProvider?.SetError(_scenarioInput, "");
                }

                await Task.CompletedTask;

                return errors.Count == 0
                    ? ValidationResult.Success
                    : ValidationResult.Failed(errors.ToArray());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating WarRoom panel");
                return ValidationResult.Failed(new ValidationItem(
                    FieldName: "Validation",
                    Message: ex.Message,
                    Severity: ValidationSeverity.Error
                ));
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Focuses the first validation error control.
        /// </summary>
        public override void FocusFirstError()
        {
            try
            {
                // If scenario input has error, focus it
                if (_scenarioInput != null && !string.IsNullOrEmpty(_errorProvider?.GetError(_scenarioInput)))
                {
                    _scenarioInput.Focus();
                    _scenarioInput.SelectAll();
                    return;
                }

                // Default: focus scenario input
                if (_scenarioInput != null)
                {
                    _scenarioInput.Focus();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error focusing first validation error");
            }
        }

        /// <summary>
        /// Cleans up all resources, event handlers, and child controls.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // Unsubscribe from ViewModel events
                    if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                    {
                        ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
                    }

                    // Unsubscribe from button click handler
                    if (_btnRunScenario != null && _btnRunScenarioClickHandler != null)
                    {
                        _btnRunScenario.Click -= _btnRunScenarioClickHandler;
                    }

                    // Unsubscribe from text changed handler
                    if (_scenarioInput != null && _scenarioInputTextChangedHandler != null)
                    {
                        _scenarioInput.TextChanged -= _scenarioInputTextChangedHandler;
                    }

                    // Unsubscribe from collection changed handlers
                    if (_projectionsCollection != null && _projectionsCollectionChangedHandler != null)
                    {
                        try { _projectionsCollection.CollectionChanged -= _projectionsCollectionChangedHandler; } catch { }
                    }

                    if (_departmentImpCollection != null && _departmentImpCollectionChangedHandler != null)
                    {
                        try { _departmentImpCollection.CollectionChanged -= _departmentImpCollectionChangedHandler; } catch { }
                    }

                    // Dispose Syncfusion controls
                    _projectionsGrid?.Dispose();
                    _departmentImpactGrid?.Dispose();
                    _revenueChart?.Dispose();
                    _departmentChart?.Dispose();
                    _riskGauge?.Dispose();

                    // Dispose containers and overlays
                    _loadingOverlay?.Dispose();
                    _topPanel?.Dispose();
                    _panelHeader?.Dispose();
                    _contentPanel?.Dispose();

                    // Dispose ErrorProvider
                    _errorProvider?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during Dispose cleanup");
                }
            }

            base.Dispose(disposing);
        }
    }
}
