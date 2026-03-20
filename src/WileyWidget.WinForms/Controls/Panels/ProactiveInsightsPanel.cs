#nullable enable

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls.Supporting;
using System.Drawing;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Panel for displaying proactive AI insights with gradient header and data grid.
    /// Acts as a container for the InsightFeedPanel with toolbar and navigation controls.
    /// Integrates with SfSkinManager for theme cascading to all child controls.
    /// </summary>
    public partial class ProactiveInsightsPanel : ScopedPanelBase<InsightFeedViewModel>
    {
        private readonly ILogger<ProactiveInsightsPanel>? _panelLogger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IServiceScopeFactory? _scopeFactory;
        private readonly ILogger<ScopedPanelBase<InsightFeedViewModel>>? _insightFeedLogger;
        private InsightFeedPanel? _insightFeedPanel;

        // Internal child controls (kept as fields for disposal and layout control)
        private Panel? _topPanel;
        private PanelHeader? _panelHeader;
        private FlowLayoutPanel? _buttonContainer;
        private SfButton? _btnRefresh;
        private SfButton? _btnClear;
        private EventHandler? _btnRefreshClickHandler;
        private EventHandler? _btnClearClickHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        private EventHandler? _panelHeaderHelpHandler;
        private EventHandler? _panelHeaderPinToggledHandler;
        private ToolTip? _buttonToolTip;
        private Label? _statusLabel;
        private bool _childLoadRequested;

        /// <summary>
        /// Creates a new instance of the ProactiveInsightsPanel.
        /// </summary>
        internal ProactiveInsightsPanel() : this(ResolveLogger(), ResolveRequiredScopeFactory(), ResolveInsightFeedLogger(), ResolveServiceProvider())
        {
        }

        /// <summary>
        /// Creates a new instance with explicit logger.
        /// </summary>
        public ProactiveInsightsPanel(
            ILogger<ProactiveInsightsPanel>? logger = null,
            IServiceScopeFactory? scopeFactory = null,
            ILogger<ScopedPanelBase<InsightFeedViewModel>>? insightFeedLogger = null,
            IServiceProvider? serviceProvider = null)
            : base(
                scopeFactory ?? ResolveRequiredScopeFactory(serviceProvider ?? ResolveServiceProvider()),
                ResolveScopedBaseLogger(serviceProvider ?? ResolveServiceProvider(), insightFeedLogger))
        {
            _serviceProvider = serviceProvider ?? ResolveServiceProvider();
            _panelLogger = logger ?? ResolveLogger(_serviceProvider);
            _scopeFactory = scopeFactory ?? ResolveScopeFactory(_serviceProvider);
            _insightFeedLogger = insightFeedLogger ?? ResolveInsightFeedLogger(_serviceProvider);

            SafeSuspendAndLayout(InitializeComponent);

            VisibleChanged += (_, _) => QueueLayoutRefresh();
            SizeChanged += (_, _) => QueueLayoutRefresh();

            _panelLogger?.LogInformation("ProactiveInsightsPanel initializing");
            ApplyTheme();

            this.PerformLayout();
            this.Refresh();

            _panelLogger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);

            _panelLogger?.LogInformation("ProactiveInsightsPanel initialized successfully");
        }

        private void QueueLayoutRefresh()
        {
            if (IsDisposed || !Visible || Width <= 0 || Height <= 0)
            {
                return;
            }

            try
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new System.Action(() =>
                    {
                        if (IsDisposed)
                        {
                            return;
                        }

                        PerformLayout();
                        Invalidate(true);
                        Update();
                    }));
                }
            }
            catch (Exception ex)
            {
                _panelLogger?.LogDebug(ex, "Deferred ProactiveInsightsPanel layout refresh failed");
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dock = DockStyle.Fill;
            PerformLayout();
            Invalidate(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            EnsureChildLoaded();
        }

        /// <summary>
        /// Initializes the UI controls with gradient panel header and insights feed.
        /// </summary>
        private void InitializeComponent()
        {
            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Dock = DockStyle.Fill;
            Padding = new Padding(8);
            AccessibleName = "Proactive Insights Panel";
            AccessibleDescription = "Displays proactive AI insights with header and actions";

            // Create gradient top panel with header
            _topPanel = new Panel
            {
                // Removed fixed Height to allow growth; MinimumSize ensures a reasonable min height
                MinimumSize = new Size(0, 60), // ensures header can't collapse below 60px
                Dock = DockStyle.Top,
                Padding = new Padding(12, 8, 12, 8),
                Name = "ProactiveTopPanel",
                AccessibleName = "Proactive Insights Header",
                AccessibleDescription = "Header area containing title and action toolbar",
                AccessibleRole = AccessibleRole.Pane
            };
            Controls.Add(_topPanel);

            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Fill,
                Title = "Proactive AI Insights",
                ShowRefreshButton = false,
                ShowHelpButton = false,
                ShowPinButton = false,
                AccessibleName = "Proactive Insights Title",
                AccessibleDescription = "Title of the Proactive Insights panel"
            };
            _topPanel.Controls.Add(_panelHeader);

            // Wire PanelHeader events
            _panelHeaderRefreshHandler = (s, e) => BtnRefresh_Click(s, e);
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeaderHelpHandler = PanelHeader_HelpClicked;
            _panelHeaderPinToggledHandler = PanelHeader_PinToggled;
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            _panelHeader.HelpClicked += _panelHeaderHelpHandler;
            _panelHeader.PinToggled += _panelHeaderPinToggledHandler;

            // Button container for actions (using FlowLayoutPanel instead of ToolStrip for consistency)
            _buttonContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0),
                Padding = new Padding(4, 2, 4, 2),
                Name = "ProactiveToolStrip",
                AccessibleName = "Proactive Actions Toolbar",
                AccessibleDescription = "Toolbar for proactive insights actions",
                MaximumSize = new Size(0, 72)
            };

            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(this, currentTheme);

            // Refresh button (using SfButton for Syncfusion consistency)
            _btnRefresh = new SfButton
            {
                Text = "&Refresh Insights",
                AutoSize = true,
                MinimumSize = new Size(120, 32),
                Name = "ProactiveRefresh",
                AccessibleName = "Refresh Insights Button",
                AccessibleDescription = "Click to refresh proactive insights",
                Margin = new Padding(4, 0, 4, 6),
                TabIndex = 1,
                TabStop = true
            };
            _buttonToolTip ??= new ToolTip();
            _buttonToolTip.SetToolTip(_btnRefresh, "Refresh the latest proactive insights.");
            SfSkinManager.SetVisualStyle(_btnRefresh, currentTheme);
            _btnRefresh.ThemeName = currentTheme;
            _buttonContainer.Controls.Add(_btnRefresh);

            // Clear button (using SfButton for Syncfusion consistency)
            _btnClear = new SfButton
            {
                Text = "&Clear Insights",
                AutoSize = true,
                MinimumSize = new Size(120, 32),
                Name = "ProactiveClear",
                AccessibleName = "Clear Insights Button",
                AccessibleDescription = "Click to clear all proactive insights",
                Margin = new Padding(4, 0, 4, 6),
                TabIndex = 2,
                TabStop = true
            };
            _buttonToolTip.SetToolTip(_btnClear, "Clear insights from the current view.");
            SfSkinManager.SetVisualStyle(_btnClear, currentTheme);
            _btnClear.ThemeName = currentTheme;
            _buttonContainer.Controls.Add(_btnClear);

            _topPanel.Controls.Add(_buttonContainer);

            // Insights feed panel (displays grid and status)
            _insightFeedPanel = CreateInsightFeedPanel();
            if (_insightFeedPanel != null)
            {
                _insightFeedPanel.Dock = DockStyle.Fill;
                _insightFeedPanel.Name = "InsightFeedPanel";
                _insightFeedPanel.AccessibleName = "Insight Feed";
                _insightFeedPanel.AccessibleDescription = "Displays the list of proactive insights and statuses";
                Controls.Add(_insightFeedPanel);
            }
            else
            {
                Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Name = "InsightFeedUnavailableLabel",
                    Text = "Insight feed unavailable.",
                    TextAlign = ContentAlignment.MiddleCenter,
                    AccessibleName = "Insight Feed Unavailable",
                    AccessibleDescription = "Insight feed is unavailable because required runtime services are missing"
                });
            }

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Padding = new Padding(8, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Ready",
                AccessibleName = "Proactive insights status"
            };
            Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            // Hook up toolbar actions to named handlers so we can unsubscribe later
            _btnRefreshClickHandler = (s, e) => BtnRefresh_Click(s, e);
            _btnClearClickHandler = (s, e) => BtnClear_Click(s, e);
            _btnRefresh.Click += _btnRefreshClickHandler;
            _btnClear.Click += _btnClearClickHandler;
        }

        private void EnsureChildLoaded()
        {
            if (_childLoadRequested || _insightFeedPanel == null || IsDisposed)
            {
                return;
            }

            _childLoadRequested = true;

            try
            {
                BeginInvoke(new System.Action(async () =>
                {
                    await RefreshChildPanelAsync(manualRequest: false);
                }));
            }
            catch (Exception ex)
            {
                _childLoadRequested = false;
                _panelLogger?.LogDebug(ex, "Failed to queue proactive insight feed load");
            }
        }

        private async Task RefreshChildPanelAsync(bool manualRequest)
        {
            if (_insightFeedPanel == null || IsDisposed)
            {
                return;
            }

            try
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = true;
                }

                SetStatusMessage(manualRequest ? "Refreshing proactive insights..." : "Loading proactive insights...");
                await _insightFeedPanel.LoadAsync();

                var childStatus = _insightFeedPanel.ViewModel?.StatusMessage;
                SetStatusMessage(string.IsNullOrWhiteSpace(childStatus)
                    ? "Proactive insights ready."
                    : childStatus);
            }
            catch (Exception ex)
            {
                _panelLogger?.LogError(ex, "Failed to refresh proactive insights panel");
                SetStatusMessage($"Unable to load proactive insights: {ex.Message}");
            }
            finally
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = false;
                }
            }
        }

        protected override void ClosePanel()
        {
            try
            {
                var form = FindForm();
                if (form is WileyWidget.WinForms.Forms.MainForm mainForm && mainForm.PanelNavigator != null)
                {
                    if (mainForm.PanelNavigator.HidePanel("Proactive Insights"))
                    {
                        return;
                    }
                    if (mainForm.PanelNavigator.HidePanel("Proactive AI Insights"))
                    {
                        return;
                    }
                }

                // Fallback: hide the panel directly
                Visible = false;
            }
            catch (Exception ex)
            {
                _panelLogger?.LogDebug(ex, "Failed to close ProactiveInsightsPanel via docking manager");
                Visible = false;
            }
        }

        private void SetStatusMessage(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
            }
        }

        private InsightFeedPanel? CreateInsightFeedPanel()
        {
            try
            {
                if (_serviceProvider != null)
                {
                    return ((IServiceProvider)_serviceProvider).GetService(typeof(InsightFeedPanel)) as InsightFeedPanel
                        ?? ActivatorUtilities.CreateInstance<InsightFeedPanel>(_serviceProvider);
                }

                if (_scopeFactory != null)
                {
                    return new InsightFeedPanel(_scopeFactory, _insightFeedLogger);
                }
            }
            catch (Exception ex)
            {
                _panelLogger?.LogError(ex, "Failed to create InsightFeedPanel for ProactiveInsightsPanel");
            }

            return null;
        }

        /// <summary>
        /// Dispose implementation - clean up event handlers and child controls.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe event handlers
                if (_btnRefresh != null && _btnRefreshClickHandler != null)
                    _btnRefresh.Click -= _btnRefreshClickHandler;
                if (_btnClear != null && _btnClearClickHandler != null)
                    _btnClear.Click -= _btnClearClickHandler;
                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshHandler != null)
                        _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                    if (_panelHeaderCloseHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    if (_panelHeaderHelpHandler != null)
                        _panelHeader.HelpClicked -= _panelHeaderHelpHandler;
                    if (_panelHeaderPinToggledHandler != null)
                        _panelHeader.PinToggled -= _panelHeaderPinToggledHandler;
                }

                // Dispose controls
                _buttonToolTip?.Dispose();
                _btnRefresh?.SafeDispose();
                _btnClear?.SafeDispose();
                _buttonContainer?.SafeDispose();
                _panelHeader?.SafeDispose();
                _topPanel?.SafeDispose();
                _insightFeedPanel?.SafeDispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Applies current application theme to the panel using SfSkinManager.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // Apply current application theme via SfSkinManager (authoritative theme source)
                var currentTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, currentTheme);

                // Ensure theme cascades to key child containers as well
                if (_topPanel != null)
                {
                    SfSkinManager.SetVisualStyle(_topPanel, currentTheme);
                }

                if (_insightFeedPanel != null)
                {
                    SfSkinManager.SetVisualStyle(_insightFeedPanel, currentTheme);
                }

                _panelLogger?.LogDebug("Theme applied successfully to ProactiveInsightsPanel");
            }
            catch (Exception ex)
            {
                _panelLogger?.LogError(ex, "Failed to apply theme to ProactiveInsightsPanel");
            }
        }

        /// <summary>
        /// Handles clicks on the Refresh toolbar button.
        /// </summary>
        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            try
            {
                _panelLogger?.LogInformation("[PROACTIVE_INSIGHTS] Refresh clicked");
                await RefreshChildPanelAsync(manualRequest: true);
            }
            catch (Exception ex)
            {
                _panelLogger?.LogError(ex, "[PROACTIVE_INSIGHTS] Refresh action failed");
                SetStatusMessage($"Unable to refresh insights: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles clicks on the Clear toolbar button.
        /// </summary>
        private void BtnClear_Click(object? sender, EventArgs e)
        {
            try
            {
                _panelLogger?.LogInformation("[PROACTIVE_INSIGHTS] Clear clicked");

                if (_insightFeedPanel?.ViewModel is InsightFeedViewModel viewModel)
                {
                    viewModel.InsightCards.Clear();
                    viewModel.HighPriorityCount = 0;
                    viewModel.MediumPriorityCount = 0;
                    viewModel.LowPriorityCount = 0;
                    viewModel.StatusMessage = "Insights cleared";
                    _panelLogger?.LogInformation("[PROACTIVE_INSIGHTS] Cleared visible insight cards");
                    SetStatusMessage("Insights cleared.");
                }
            }
            catch (Exception ex)
            {
                _panelLogger?.LogError(ex, "[PROACTIVE_INSIGHTS] Clear action failed");
                SetStatusMessage($"Unable to clear insights: {ex.Message}");
            }
        }

        private void PanelHeader_HelpClicked(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "Proactive Insights Help: AI-generated insights for budget optimization.",
                "Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void PanelHeader_PinToggled(object? sender, EventArgs e)
        {
            // Pin behavior is managed by host docking infrastructure.
        }

        /// <summary>
        /// Resolves the logger from DI.
        /// </summary>
        private static IServiceProvider? ResolveServiceProvider() => Program.Services;

        private static ILogger<ProactiveInsightsPanel>? ResolveLogger(IServiceProvider? provider = null)
        {
            var services = provider ?? ResolveServiceProvider();
            if (services == null)
            {
                return null;
            }

            try
            {
                return ((IServiceProvider)services).GetService(typeof(ILogger<ProactiveInsightsPanel>)) as ILogger<ProactiveInsightsPanel>;
            }
            catch
            {
                return null;
            }
        }

        private static IServiceScopeFactory? ResolveScopeFactory(IServiceProvider? provider = null)
        {
            var services = provider ?? ResolveServiceProvider();
            if (services == null)
            {
                return null;
            }

            try
            {
                return ((IServiceProvider)services).GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            }
            catch
            {
                return null;
            }
        }

        private static IServiceScopeFactory ResolveRequiredScopeFactory(IServiceProvider? provider = null)
        {
            return ResolveScopeFactory(provider) ?? throw new InvalidOperationException(
                "IServiceScopeFactory is required to initialize ProactiveInsightsPanel.");
        }

        private static ILogger ResolveScopedBaseLogger(
            IServiceProvider? provider,
            ILogger<ScopedPanelBase<InsightFeedViewModel>>? logger)
        {
            return (ILogger?)logger ?? (ILogger?)ResolveInsightFeedLogger(provider) ?? NullLogger.Instance;
        }

        private static ILogger<ScopedPanelBase<InsightFeedViewModel>>? ResolveInsightFeedLogger(IServiceProvider? provider = null)
        {
            var services = provider ?? ResolveServiceProvider();
            if (services == null)
            {
                return null;
            }

            try
            {
                return ((IServiceProvider)services).GetService(typeof(ILogger<ScopedPanelBase<InsightFeedViewModel>>)) as ILogger<ScopedPanelBase<InsightFeedViewModel>>;
            }
            catch
            {
                return null;
            }
        }
    }
}
