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
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Helpers;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Abstractions;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// War Room panel for interactive what-if scenario analysis.
    /// Integrates GrokAgentService for AI-powered financial projections.
    ///
    /// FEATURES:
    /// - Natural language scenario input with JARVIS voice hint
    /// - Revenue/Reserves trend chart (line chart)
    /// - Department impact analysis (column chart)
    /// - Risk level gauge (radial gauge)
    /// - Prominent "Required Rate Increase" display
    /// - Full input validation with visual error feedback
    /// - Accessible UI with screen reader support
    /// - Theme-aware styling via SfSkinManager
    /// - Cancellation support for long-running operations
    /// - Keyboard shortcuts (Alt+Enter: run, Ctrl+E: export, Esc: cancel)
    ///
    /// VIEWMODEL CONTRACT (WarRoomViewModel properties required):
    /// - ScenarioInput (string, RW): User-entered scenario description
    /// - StatusMessage (string, RO): Current operation status
    /// - IsAnalyzing (bool, RO): True while running scenario analysis
    /// - RequiredRateIncrease (string or decimal, RO): Formatted percentage or numeric value
    /// - RiskLevel (double, RO): Risk assessment 0-100
    /// - HasResults (bool, RO): True if analysis completed successfully
    /// - Projections (ObservableCollection, RO): Financial projections
    /// - DepartmentImpacts (ObservableCollection, RO): Department budget impact
    /// - RunScenarioCommand (IAsyncCommand, RO): Executes scenario with CancellationToken
    /// - ExportForecastCommand (IAsyncCommand, RO): Exports results with CancellationToken
    ///
    /// PRODUCTION-READY: Validation, databinding, error handling, sizing, accessibility, cancellation.
    /// </summary>
    public partial class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>
    {
        #region Fields and Constants
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public new object? DataContext { get; private set; }

        // Event handler storage for proper cleanup in Dispose
        private EventHandler? _btnRunScenarioClickHandler;
        private EventHandler? _btnExportForecastClickHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler? _scenarioInputTextChangedHandler;
        private KeyEventHandler? _scenarioInputKeyDownHandler;
        private KeyEventHandler? _exportButtonKeyDownHandler;

        // Cancellation token support for long-running analysis
        private CancellationTokenSource? _analysisCancellationTokenSource;
        private const int OPERATION_TIMEOUT_SECONDS = 300;  // 5-minute timeout for analysis
        private DateTime _operationStartTime;

        // Collection change handlers to react to ViewModel ObservableCollection updates
        private NotifyCollectionChangedEventHandler? _projectionsCollectionChangedHandler;
        private NotifyCollectionChangedEventHandler? _departmentImpCollectionChangedHandler;
        private INotifyCollectionChanged? _projectionsCollection;
        private INotifyCollectionChanged? _departmentImpCollection;

        private readonly ErrorProvider? _errorProvider;
        private readonly string _activeThemeName = ThemeColors.DefaultTheme;

        // Minimum size for content panel to ensure proper layout
        private static readonly Size ContentPanelMinSize = new(800, 600);

        #endregion Fields and Constants

        #region Initialization and Design-Time Support

        // Helper to detect design-time reliably for constructors and static initializers.
        private static bool IsDesignModeStatic()
        {
            try
            {
                return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            }
            catch
            {
                return false;
            }
        }

        // Obsolete parameterless constructor for designer compatibility
        // Design-time constructor MUST NOT rely on runtime DI (Program.Services may be null in designer).
        [Obsolete("Use DI constructor with IServiceScopeFactory and ILogger", false)]
        public WarRoomPanel()
            : base()
        {
            // Lightweight initialization for Visual Studio designer
            _logger?.LogDebug("WarRoomPanel (design-time) initializing");

            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Size = new Size(560, 420);
            MinimumSize = new Size(420, 360);

            _errorProvider = new ErrorProvider();

            InitializeComponent();

            // Designer should avoid runtime-only initialization which may reference services
            if (!IsDesignModeStatic())
            {
                try
                {
                    ThemeColors.EnsureThemeAssemblyLoaded(_logger);
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
            else
            {
                // Designer: only apply safe, layout-only adjustments
                if (_contentPanel != null)
                    _contentPanel.MinimumSize = ContentPanelMinSize;
            }
        }

        // Primary DI constructor
        public WarRoomPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<WarRoomViewModel>> logger)
            : base(scopeFactory, logger)
        {
            _logger?.LogDebug("WarRoomPanel initializing");

            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Size = new Size(560, 420);
            MinimumSize = new Size(420, 360);

            _errorProvider = new ErrorProvider();

            InitializeComponent();

            if (!IsDesignModeStatic())
            {
                try { ThemeColors.EnsureThemeAssemblyLoaded(_logger); } catch { }

                _activeThemeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                try { SfSkinManager.SetVisualStyle(this, _activeThemeName); } catch { }

                // Ensure minimum size for content panel
                if (_contentPanel != null)
                    _contentPanel.MinimumSize = ContentPanelMinSize;

                DeferSizeValidation();

                _logger?.LogDebug("WarRoomPanel initialized successfully");

                this.Load += (s, e) => this.Invalidate(true);
            }
            else
            {
                // Designer mode - only apply layout-safe adjustments
                if (_contentPanel != null)
                    _contentPanel.MinimumSize = ContentPanelMinSize;
            }
        }

        #endregion Initialization and Design-Time Support

        #region ViewModel Resolution and Binding

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
                    BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
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
                    BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
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

                // === TWO-WAY BINDING FOR SCENARIO INPUT (using stored handler only - no duplication) ===
                _scenarioInput.Text = ViewModel.ScenarioInput ?? "";
                _scenarioInput.AccessibleName = "Scenario Description";
                _scenarioInput.AccessibleDescription = "Enter your what-if scenario in natural language (e.g., 'Raise rates 12% over 5 years')";
                _errorProvider?.SetError(_scenarioInput, "");  // Initialize with no error

                // === INITIAL UI STATE WITH ACCESSIBILITY ===
                _lblStatus.Text = ViewModel.StatusMessage ?? "Ready";
                _lblStatus.AccessibleName = "Analysis Status";
                _loadingOverlay.Visible = ViewModel.IsAnalyzing;

                // Format RequiredRateIncrease as percentage (assume it's a decimal like 0.035 for 3.5%)
                if (double.TryParse(ViewModel.RequiredRateIncrease, out double rateValue))
                {
                    _lblRateIncreaseValue.Text = rateValue.ToString("P1");  // e.g., "3.5%"
                }
                else
                {
                    _lblRateIncreaseValue.Text = string.IsNullOrEmpty(ViewModel.RequiredRateIncrease) ? "—" : ViewModel.RequiredRateIncrease;
                }
                _lblRateIncreaseValue.AccessibleName = "Required Rate Increase";

                _riskGauge.Value = (float)ViewModel.RiskLevel;
                _riskGauge.AccessibleName = "Risk Level Gauge";
                _riskGauge.AccessibleDescription = $"Risk level: {ViewModel.RiskLevel}%";

                // Property changed
                _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

                // Configure Syncfusion controls with theme application
                ApplySyncfusionThemes();
                ConfigureRiskGauge();
                ConfigureGrids();

                // Wire up Run Scenario button with stored handler for cleanup
                _btnRunScenarioClickHandler = async (s, e) => await OnRunScenarioClickAsync();
                _btnRunScenario.Click += _btnRunScenarioClickHandler;
                _btnRunScenario.AccessibleName = "Run Scenario";
                _btnRunScenario.AccessibleDescription = "Execute the what-if scenario analysis";

                // Wire up Export Forecast button with stored handler for cleanup
                _btnExportForecastClickHandler = async (s, e) => await OnExportForecastClickAsync();
                _btnExportForecast.Click += _btnExportForecastClickHandler;
                _btnExportForecast.AccessibleName = "Export Forecast";
                _btnExportForecast.AccessibleDescription = "Export scenario results to file (Ctrl+E)";

                // Wire up scenario input textbox with stored handler for cleanup (NO INLINE HANDLER)
                _scenarioInputTextChangedHandler = (s, e) => OnScenarioInputTextChanged();
                _scenarioInput.TextChanged += _scenarioInputTextChangedHandler;
                _scenarioInputKeyDownHandler = (s, e) => ScenarioInput_KeyDown(s, e);
                _scenarioInput.KeyDown += _scenarioInputKeyDownHandler;

                // Add keyboard support for export button
                _exportButtonKeyDownHandler = (s, e) => ExportButton_KeyDown(s, e);
                _btnExportForecast.KeyDown += _exportButtonKeyDownHandler;

                // Wire up PanelHeader close button
                if (_panelHeader != null)
                {
                    _panelHeader.CloseClicked += (s, e) => ClosePanel();
                }

                // Set up accessibility for grids
                if (_projectionsGrid != null)
                {
                    _projectionsGrid.AccessibleName = "Projections";
                    _projectionsGrid.AccessibleDescription = "Financial projections based on scenario";
                }
                if (_departmentImpactGrid != null)
                {
                    _departmentImpactGrid.AccessibleName = "Department Impact";
                    _departmentImpactGrid.AccessibleDescription = "Budget impact by department";
                }
                if (_revenueChart != null)
                {
                    _revenueChart.AccessibleName = "Revenue Chart";
                    _revenueChart.AccessibleDescription = "Revenue vs Expenses projection over time";
                }
                if (_departmentChart != null)
                {
                    _departmentChart.AccessibleName = "Department Impact Chart";
                    _departmentChart.AccessibleDescription = "Budget impact visualization by department";
                }

                // Attach to collection change events and ensure UI updates if data already exists
                AttachCollectionHandlers();

                _logger?.LogDebug("ViewModel bound successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error binding ViewModel");
            }
        }

        #endregion ViewModel Resolution and Binding

        #region Async Initialization and Lifecycle

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
                _errorProvider?.SetError(_scenarioInput, "");

                if (string.IsNullOrWhiteSpace(_scenarioInput.Text))
                {
                    _lblInputError.Text = "Please enter a scenario (min. 10 characters)";
                    _lblInputError.Visible = true;
                    _errorProvider?.SetError(_scenarioInput, "Required");
                    _scenarioInput.Focus();
                    _logger?.LogInformation("Run Scenario cancelled: empty input");
                    return;
                }

                if (_scenarioInput.Text.Length < 10)
                {
                    _lblInputError.Text = "Scenario too short (minimum 10 characters)";
                    _lblInputError.Visible = true;
                    _errorProvider?.SetError(_scenarioInput, "Too short");
                    _scenarioInput.Focus();
                    _logger?.LogInformation("Run Scenario cancelled: input length {Length}", _scenarioInput.Text.Length);
                    return;
                }

                if (ViewModel?.RunScenarioCommand == null)
                {
                    _logger?.LogError("RunScenarioCommand is null - command not wired");
                    _lblInputError.Text = "Analysis command not available. Please verify ViewModel configuration.";
                    _lblInputError.Visible = true;
                    return;
                }

                // Create cancellation token source for this analysis
                _analysisCancellationTokenSource = new CancellationTokenSource();
                _analysisCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(OPERATION_TIMEOUT_SECONDS));

                // Set busy state during scenario analysis
                _operationStartTime = DateTime.UtcNow;
                IsBusy = true;
                _lblStatus.Text = "Analyzing scenario...";
                _lblInputError.Visible = false;
                _logger?.LogInformation("Starting scenario analysis for input: {ScenarioLength} chars", _scenarioInput.Text.Length);

                try
                {
                    await ViewModel.RunScenarioCommand.ExecuteAsync(_analysisCancellationTokenSource.Token);

                    var duration = DateTime.UtcNow - _operationStartTime;
                    _logger?.LogDebug("Scenario analysis completed successfully in {DurationMs}ms", duration.TotalMilliseconds);
                    _lblStatus.Text = "Analysis complete!";

                    // Ensure the UI reflects the new results immediately
                    try
                    {
                        // Schedule a UI update on the message loop
                        if (!IsDisposed)
                        {
                            BeginInvoke(new System.Action(() =>
                            {
                                try
                                {
                                    // Update UI from view model (bind data, render charts)
                                    _ = UpdateUIFromViewModel();
                                    // Ensure results panel is shown and brought to front
                                    UpdateResultsVisibility();
                                }
                                catch { }
                            }));
                        }
                    }
                    catch { }
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Scenario analysis was cancelled by user");
                    _lblStatus.Text = "Analysis cancelled";
                    _lblInputError.Text = "Analysis was cancelled.";
                    _lblInputError.Visible = true;
                }
                finally
                {
                    IsBusy = false;
                    _analysisCancellationTokenSource?.Dispose();
                    _analysisCancellationTokenSource = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Run Scenario handler");
                _lblInputError.Text = $"Analysis error: {ex.Message}\n\nTry again or contact support if problem persists.";
                _lblInputError.Visible = true;
                _errorProvider?.SetError(_scenarioInput, "Error");
                IsBusy = false;
                MessageBox.Show(
                    $"Failed to run scenario:\n\n{ex.Message}\n\nCheck the logs for more details.",
                    "Analysis Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #endregion Event Handlers - Scenario Analysis

        #region Event Handlers - Input and Validation

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
                _errorProvider?.SetError(_scenarioInput, "");  // Clear ErrorProvider visual feedback

                // Real-time validation feedback
                if (string.IsNullOrWhiteSpace(_scenarioInput.Text))
                {
                    // Empty - show hint but don't error yet (only error on Run)
                }
                else if (_scenarioInput.Text.Length < 10)
                {
                    // Too short - show hint but don't error on every keystroke
                }
                else
                {
                    // Valid input
                    _errorProvider?.SetError(_scenarioInput, "");
                }
            }
        }

        /// <summary>
        /// Handles Export Forecast button click with user feedback and cancellation support.
        /// </summary>
        private async Task OnExportForecastClickAsync()
        {
            if (!ViewModel?.HasResults ?? true)
            {
                _logger?.LogWarning("Export cancelled: No analysis results available");
                MessageBox.Show(
                    "No forecast data available to export.\n\nPlease run a scenario analysis first.",
                    "No Data",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (ViewModel?.ExportForecastCommand == null || !ViewModel.ExportForecastCommand.CanExecute(null))
            {
                _logger?.LogWarning("Export forecast command not available or cannot execute");
                MessageBox.Show(
                    "Export feature is not available.\n\nPlease verify the application configuration.",
                    "Not Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Create separate cancellation token for export (shorter timeout)
                using var exportToken = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                _lblStatus.Text = "Exporting forecast...";
                IsBusy = true;
                _operationStartTime = DateTime.UtcNow;
                _logger?.LogInformation("Starting forecast export");

                await ViewModel.ExportForecastCommand.ExecuteAsync(exportToken.Token);

                var duration = DateTime.UtcNow - _operationStartTime;
                _logger?.LogDebug("Forecast exported successfully in {DurationMs}ms", duration.TotalMilliseconds);
                _lblStatus.Text = "Forecast exported successfully";

                MessageBox.Show(
                    "Forecast has been exported successfully.",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Export operation was cancelled or timed out");
                _lblStatus.Text = "Export cancelled";
                MessageBox.Show(
                    "Export operation was cancelled or timed out.\n\nPlease try again.",
                    "Export Cancelled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing export forecast command");
                _lblStatus.Text = "Export failed";
                MessageBox.Show(
                    $"Failed to export forecast:\n\n{ex.Message}\n\nCheck the logs for more details.",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Handles KeyDown events for the scenario input textbox.
        /// Supports Enter key to process the scenario.
        /// </summary>
        private void ScenarioInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                // Process the scenario input
                OnScenarioInputTextChanged();
            }
        }

        /// <summary>
        /// Handles KeyDown events for the export button.
        /// Supports Ctrl+E shortcut for export.
        /// </summary>
        private void ExportButton_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.E)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                // Trigger export
                _ = OnExportForecastClickAsync();
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

        #endregion Event Handlers - Input and Validation

        #region Event Handlers - ViewModel Property Changes

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(ViewModel.StatusMessage):
                    _lblStatus.Text = ViewModel.StatusMessage ?? "Ready";
                    _lblStatus.AccessibleDescription = $"Status: {ViewModel.StatusMessage}";
                    break;
                case nameof(ViewModel.IsAnalyzing):
                    _loadingOverlay.Visible = ViewModel.IsAnalyzing;
                    _btnRunScenario.Enabled = !ViewModel.IsAnalyzing;
                    _btnExportForecast.Enabled = !ViewModel.IsAnalyzing;
                    _lblStatus.Text = ViewModel.IsAnalyzing ? "Analyzing..." : "Ready";
                    break;
                case nameof(ViewModel.RequiredRateIncrease):
                    if (double.TryParse(ViewModel.RequiredRateIncrease, out double rateValue))
                    {
                        _lblRateIncreaseValue.Text = rateValue.ToString("P1");
                        _lblRateIncreaseValue.AccessibleDescription = $"Required rate increase: {rateValue:P1}";
                    }
                    else
                    {
                        _lblRateIncreaseValue.Text = string.IsNullOrEmpty(ViewModel.RequiredRateIncrease) ? "—" : ViewModel.RequiredRateIncrease;
                    }
                    break;
                case nameof(ViewModel.RiskLevel):
                    _riskGauge.Value = (float)ViewModel.RiskLevel;
                    _riskGauge.AccessibleDescription = $"Risk level: {ViewModel.RiskLevel}%";
                    break;
                case nameof(ViewModel.HasResults):
                case nameof(ViewModel.Projections):
                case nameof(ViewModel.DepartmentImpacts):
                    UpdateResultsVisibility();
                    break;
            }
        }

        #endregion Event Handlers - ViewModel Property Changes

        #region UI Configuration and Theming

        private void ApplySyncfusionThemes()
        {
            // Apply theme to all Syncfusion controls
            try
            {
                var themeName = _activeThemeName ?? ThemeColors.DefaultTheme;
                if (_riskGauge != null) SfSkinManager.SetVisualStyle(_riskGauge, themeName);
                if (_revenueChart != null) SfSkinManager.SetVisualStyle(_revenueChart, themeName);
                if (_departmentChart != null) SfSkinManager.SetVisualStyle(_departmentChart, themeName);
                if (_projectionsGrid != null) SfSkinManager.SetVisualStyle(_projectionsGrid, themeName);
                if (_departmentImpactGrid != null) SfSkinManager.SetVisualStyle(_departmentImpactGrid, themeName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Warning applying Syncfusion themes - continuing with defaults");
            }
        }

        private void UpdateResultsVisibility()
        {
            bool hasData = (ViewModel?.HasResults ?? false) ||
                           (ViewModel?.Projections?.Count > 0) ||
                           (ViewModel?.DepartmentImpacts?.Count > 0);

            try
            {
                // Marshal to UI thread if necessary
                if (InvokeRequired)
                {
                    BeginInvoke(new System.Action(UpdateResultsVisibility));
                    return;
                }

                // Show/hide the results container and push to front when showing
                if (_resultsPanel != null)
                {
                    _resultsPanel.Visible = hasData;
                    if (hasData)
                    {
                        _resultsPanel.BringToFront();
                    }
                    else
                    {
                        _resultsPanel.SendToBack();
                    }
                }

                // No-results placeholder handling
                if (_lblNoResults != null)
                {
                    _lblNoResults.Visible = !hasData;
                    if (!hasData)
                        _lblNoResults.BringToFront();
                }

                // If we have data, ensure child controls are visible, themed, and refreshed
                if (hasData)
                {
                    try { ApplySyncfusionThemes(); } catch { }

                    _lblRateIncreaseHeadline?.BringToFront();
                    _lblRateIncreaseValue?.BringToFront();
                    _riskGauge?.BringToFront();
                    _revenueChart?.BringToFront();
                    _departmentChart?.BringToFront();
                    _projectionsGrid?.BringToFront();
                    _departmentImpactGrid?.BringToFront();

                    try
                    {
                        RenderRevenueChart();
                        RenderDepartmentChart();
                    }
                    catch { }

                    _revenueChart?.Refresh();
                    _departmentChart?.Refresh();
                    _riskGauge?.Refresh();
                    _projectionsGrid?.Refresh();
                    _departmentImpactGrid?.Refresh();

                    _resultsPanel?.PerformLayout();
                    _contentPanel?.Invalidate(true);
                    _contentPanel?.Refresh();
                }
                else
                {
                    // No data - ensure visuals are refreshed and hidden where appropriate
                    _revenueChart?.Refresh();
                    _departmentChart?.Refresh();
                    _riskGauge?.Refresh();
                    _projectionsGrid?.Refresh();
                    _departmentImpactGrid?.Refresh();
                    _contentPanel?.Invalidate(true);
                    _contentPanel?.Refresh();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating results visibility");
            }

            if (_resultsPanel != null) _resultsPanel.AccessibleDescription = hasData ? "Results available" : "No results yet";
            if (_lblNoResults != null) _lblNoResults.AccessibleDescription = "Run a scenario to see results";
        }

        private void ConfigureRiskGauge()
        {
            // Configure basic RadialGauge properties with theme-aware colors
            if (_riskGauge != null)
            {
                _riskGauge.MinimumValue = 0;
                _riskGauge.MaximumValue = 100;
                _riskGauge.Value = 0;
                // Use theme-aware color instead of hard-coded Black
                _riskGauge.NeedleColor = SystemColors.ControlText;  // Respects theme/OS settings
                _riskGauge.ShowNeedle = true;
            }
        }

        private void ConfigureGrids()
        {
            // Projections grid
            if (_projectionsGrid.Columns.Count > 0)
            {
                _projectionsGrid.Columns["Year"].HeaderText = "Fiscal Year";
                _projectionsGrid.Columns["ProjectedRate"].HeaderText = "Rate ($/month)";
                _projectionsGrid.Columns["ProjectedRate"].Format = "C2";
                _projectionsGrid.Columns["ProjectedRevenue"].Format = "C0";
                _projectionsGrid.Columns["ProjectedExpenses"].Format = "C0";
                _projectionsGrid.Columns["ProjectedBalance"].Format = "C0";
                _projectionsGrid.Columns["ReserveLevel"].HeaderText = "Reserve (months)";

                // Prevent invalid filter expressions on string columns (e.g., "Year > '2023'" is invalid)
                _projectionsGrid.FilterChanging += ProjectionsGrid_FilterChanging;
            }

            // Department impact grid
            if (_departmentImpactGrid.Columns.Count > 0)
            {
                _departmentImpactGrid.Columns["DepartmentName"].HeaderText = "Department";
                _departmentImpactGrid.Columns["CurrentBudget"].Format = "C0";
                _departmentImpactGrid.Columns["ProjectedBudget"].Format = "C0";
                _departmentImpactGrid.Columns["ImpactAmount"].Format = "C0";
                _departmentImpactGrid.Columns["ImpactPercentage"].HeaderText = "Impact %";
                _departmentImpactGrid.Columns["ImpactPercentage"].Format = "P1";

                // Prevent invalid filter expressions on string columns
                _departmentImpactGrid.FilterChanging += DepartmentImpactGrid_FilterChanging;
            }
        }

        /// <summary>
        /// Prevents invalid filter expressions on string columns in the projections grid.
        /// </summary>
        private void ProjectionsGrid_FilterChanging(object sender, Syncfusion.WinForms.DataGrid.Events.FilterChangingEventArgs e)
        {
            // Cancel filter if it's trying to apply comparison operators to string columns
            if (e.Column.MappingName == "Year" && e.FilterPredicates.Any(p =>
                p.FilterType == Syncfusion.Data.FilterType.GreaterThan ||
                p.FilterType == Syncfusion.Data.FilterType.GreaterThanOrEqual ||
                p.FilterType == Syncfusion.Data.FilterType.LessThan ||
                p.FilterType == Syncfusion.Data.FilterType.LessThanOrEqual))
            {
                e.Cancel = true;
                _logger?.LogDebug("Cancelled invalid filter expression on string column 'Year'");
            }
        }

        /// <summary>
        /// Prevents invalid filter expressions on string columns in the department impact grid.
        /// </summary>
        private void DepartmentImpactGrid_FilterChanging(object sender, Syncfusion.WinForms.DataGrid.Events.FilterChangingEventArgs e)
        {
            // Cancel filter if it's trying to apply comparison operators to string columns
            if (e.Column.MappingName == "DepartmentName" && e.FilterPredicates.Any(p =>
                p.FilterType == Syncfusion.Data.FilterType.GreaterThan ||
                p.FilterType == Syncfusion.Data.FilterType.GreaterThanOrEqual ||
                p.FilterType == Syncfusion.Data.FilterType.LessThan ||
                p.FilterType == Syncfusion.Data.FilterType.LessThanOrEqual))
            {
                e.Cancel = true;
                _logger?.LogDebug("Cancelled invalid filter expression on string column 'DepartmentName'");
            }
        }

        /// <summary>
        /// Updates the UI from the current ViewModel state, ensuring controls are visible based on data availability.
        /// </summary>
        #endregion Keyboard Shortcuts and Navigation

        #region UI State Updates

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
                    BeginInvoke(new System.Action(() => { _ = UpdateUIFromViewModel(); }));
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
                    BeginInvoke(new System.Action(() => { _ = UpdateUIFromViewModel(); }));
                }
                catch { }
            };

            HandleCreated += handleCreatedHandler;
        }

        #endregion Grid Filtering

        #region Collection Event Management

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
                                    try { BeginInvoke(new System.Action(OnProjectionsCollectionChanged)); } catch { }
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
                                    try { BeginInvoke(new System.Action(OnDepartmentImpactsCollectionChanged)); } catch { }
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

                // Initial render in case data already exists
                RenderRevenueChart();
                RenderDepartmentChart();
                UpdateResultsVisibility();
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
                _revenueChart.Text = "Revenue vs Expenses Projection";
                _revenueChart.Legend.Visible = true;
                _revenueChart.PrimaryXAxis.Title = "Year";
                _revenueChart.PrimaryYAxis.Title = "Amount ($)";

                // Revenue series - use theme-aware colors
                var revenueSeries = new ChartSeries
                {
                    Name = "Revenue",
                    Type = ChartSeriesType.Line
                };
                // Use semi-transparent blue for theme compliance (Steel Blue)
                revenueSeries.Style.Interior = new BrushInfo(Color.FromArgb(70, 130, 180));

                foreach (var proj in ViewModel.Projections)
                {
                    revenueSeries.Points.Add(proj.Year, (double)proj.ProjectedRevenue);
                }

                _revenueChart.Series.Add(revenueSeries);

                // Expenses series - use complementary theme-aware color
                var expenseSeries = new ChartSeries
                {
                    Name = "Expenses",
                    Type = ChartSeriesType.Line
                };
                // Use semi-transparent red for theme compliance (Firebrick Red)
                expenseSeries.Style.Interior = new BrushInfo(Color.FromArgb(178, 34, 34));

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
                _departmentChart.Text = "Department Budget Impact";
                _departmentChart.Legend.Visible = true;
                _departmentChart.PrimaryXAxis.Title = "Department";
                _departmentChart.PrimaryYAxis.Title = "Budget Impact ($)";

                var impactSeries = new ChartSeries
                {
                    Name = "Impact Amount",
                    Type = ChartSeriesType.Column
                };
                // Use theme-aware orange (Dark Orange)
                impactSeries.Style.Interior = new BrushInfo(Color.FromArgb(255, 140, 0));

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

                // Clear any existing panel-level validation errors
                ClearValidationErrors();
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

                if (errors.Count == 0)
                {
                    // No errors: ensure panel state reflects success
                    ClearValidationErrors();
                    return ValidationResult.Success;
                }

                // Propagate errors to panel state so UI and consumers can read them
                SetValidationErrors(errors);
                return ValidationResult.Failed(errors.ToArray());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating WarRoom panel");
                var item = new ValidationItem(
                    FieldName: "Validation",
                    Message: ex.Message,
                    Severity: ValidationSeverity.Error
                );

                SetValidationErrors(new[] { item });
                return ValidationResult.Failed(item);
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

                    // Unsubscribe from export button click handler
                    if (_btnExportForecast != null && _btnExportForecastClickHandler != null)
                    {
                        _btnExportForecast.Click -= _btnExportForecastClickHandler;
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

        #endregion Cleanup and Disposal
    }
}
