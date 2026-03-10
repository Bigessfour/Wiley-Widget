using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms
{
    public partial class MainForm
    {
        public IPanelNavigationService? PanelNavigator => _panelNavigator;

        private IPanelNavigationService? _panelNavigator;

        private PanelNavigationService EnsurePanelNavigationServiceInitialized(string reason)
        {
            if (_panelNavigationService != null)
            {
                if (_tabbedMdi != null)
                {
                    _panelNavigationService.SetTabbedManager(_tabbedMdi);
                }

                _panelNavigator = _panelNavigationService;
                return _panelNavigationService;
            }

            InitializeLayoutComponents();

            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider unavailable.");
            }

            _logger?.LogDebug("[NAV] Creating PanelNavigationService ({Reason})", reason);

            var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<ILogger<PanelNavigationService>>(_serviceProvider);

            _panelNavigationService = new PanelNavigationService(
                this,
                _serviceProvider,
                navLogger ?? NullLogger<PanelNavigationService>.Instance);

            if (_tabbedMdi != null)
            {
                _panelNavigationService.SetTabbedManager(_tabbedMdi);
                _logger?.LogDebug("[NAV] {Reason}: TabbedMDIManager wired to PanelNavigationService", reason);
            }

            _panelNavigator = _panelNavigationService;
            return _panelNavigationService;
        }

        private void EnsurePanelNavigatorInitialized()
        {
            _ = EnsurePanelNavigationServiceInitialized("EnsurePanelNavigatorInitialized");
        }

        public void ShowPanel<TPanel>(string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            EnsurePanelNavigatorInitialized();
            using var uiProbeScope = BeginUiProbeOperationScope($"ShowPanel<{typeof(TPanel).Name}>:{panelName}");
            _panelNavigator?.ShowPanel<TPanel>(panelName, style, allowFloating);
            RefreshProfessionalStatusBarSnapshot();
        }

        public void ShowForm<TForm>(string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
            where TForm : Form
        {
            EnsurePanelNavigatorInitialized();
            using var uiProbeScope = BeginUiProbeOperationScope($"ShowForm<{typeof(TForm).Name}>:{panelName}");
            _panelNavigator?.ShowForm<TForm>(panelName, style, allowFloating);
            RefreshProfessionalStatusBarSnapshot();
        }

        public void ClosePanel(string panelName)
        {
            _panelNavigator?.HidePanel(panelName);
        }

        /// <summary>
        /// Non-generic panel show for runtime-type-based navigation (e.g. layout restore, global search).
        /// </summary>
        public bool ShowPanel(Type panelType, string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
        {
            EnsurePanelNavigatorInitialized();
            try
            {
                if (panelType == null || !typeof(UserControl).IsAssignableFrom(panelType))
                {
                    _logger?.LogWarning("[NAV] ShowPanel(Type) rejected invalid panel type {PanelType}", panelType?.FullName ?? "<null>");
                    return false;
                }

                if (panelType == typeof(FormHostPanel))
                {
                    if (string.Equals(panelName, "Rates", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowForm<RatesPage>(panelName, style, allowFloating: true);
                        return true;
                    }
                }

                if (_panelNavigator == null)
                {
                    _logger?.LogWarning("[NAV] ShowPanel(Type) failed because panel navigator is unavailable");
                    return false;
                }

                using var uiProbeScope = BeginUiProbeOperationScope($"ShowPanel({panelType.Name}):{panelName}");
                _panelNavigator.ShowPanel(panelType, panelName, style, allowFloating);
                RefreshProfessionalStatusBarSnapshot();

                var activePanelName = _panelNavigator.GetActivePanelName();
                if (string.IsNullOrWhiteSpace(activePanelName))
                {
                    _logger?.LogDebug("[NAV] ShowPanel(Type) completed but active panel is null for {PanelType}", panelType.Name);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] ShowPanel(Type) failed for {PanelType}", panelType?.Name);
                return false;
            }
        }

        /// <summary>Hides the Settings panel (called by SettingsPanel close button).</summary>
        public void CloseSettingsPanel() => ClosePanel("Settings");

        internal bool ShowJarvisInRightDock(object? parameters = null)
        {
            _logger?.LogDebug(
                "[NAV] ShowJarvisInRightDock entered — panel={PanelNull}, tabs={TabsNull}, jarvis={JarvisNull}",
                _rightDockPanel == null ? "null" : (_rightDockPanel.IsDisposed ? "disposed" : "ok"),
                _rightDockTabs == null ? "null" : (_rightDockTabs.IsDisposed ? "disposed" : "ok"),
                _rightDockJarvisPanel == null ? "null" : (_rightDockJarvisPanel.IsDisposed ? "disposed" : "ok"));

            try
            {
                if (!EnsureRightDockPanelInitialized())
                {
                    _logger?.LogWarning("[NAV] ShowJarvisInRightDock: EnsureRightDockPanelInitialized returned false — JARVIS will not appear");
                    return false;
                }

                var jarvisTab = FindRightDockTab(RightDockPanelFactory.JarvisTabName);
                var alreadySelected = _rightDockTabs != null
                    && jarvisTab != null
                    && ReferenceEquals(_rightDockTabs.SelectedTab, jarvisTab);
                var alreadyVisible = _rightDockPanel?.Visible == true;

                // Fast path: avoid extra layout and focus churn when the right dock is already
                // visible on the JARVIS tab and no new parameters are being applied.
                if (alreadySelected
                    && alreadyVisible
                    && parameters is null
                    && _rightDockJarvisPanel != null
                    && !_rightDockJarvisPanel.IsDisposed)
                {
                    return true;
                }

                var redrawSuspended = TrySuspendRedraw("ShowJarvisInRightDock");
                _rightDockPanel?.SuspendLayout();
                _rightDockTabs?.SuspendLayout();

                EnsureRightDockJarvisPanelMaterialized(parameters);

                try
                {
                    if (jarvisTab == null)
                    {
                        _logger?.LogWarning(
                            "[NAV] ShowJarvisInRightDock: JARVIS tab 'RightDockTab_JARVIS' not found — tab count={TabCount}",
                            _rightDockTabs?.TabPages.Count ?? -1);
                    }
                    else if (_rightDockTabs != null && !ReferenceEquals(_rightDockTabs.SelectedTab, jarvisTab))
                    {
                        _logger?.LogDebug("[NAV] ShowJarvisInRightDock: switching selected tab from '{Current}' to 'RightDockTab_JARVIS'",
                            _rightDockTabs.SelectedTab?.Name ?? "<none>");
                        _rightDockTabs.SelectedTab = jarvisTab;
                    }

                    ApplyRightDockWidthForSelectedTab(force: true);

                    if (_rightDockPanel != null)
                    {
                        _rightDockPanel.Visible = true;
                        if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
                        {
                            _rightDockSplitter.Visible = true;
                        }

                        SetJarvisAutoHideButtonState(isSidebarVisible: true);
                        _rightDockPanel.BringToFront();
                    }

                    _rightDockTabs?.BringToFront();

                    // Showing the sidebar changes the usable MDI width immediately.
                    // Force a fresh constrain pass so the active panel shrinks instead
                    // of being left underneath the newly visible right dock.
                    RefreshPanelHostLayout("ShowJarvisInRightDock", force: true);

                    _logger?.LogDebug(
                        "[NAV] JARVIS Chat shown — panel Visible={Visible}, Bounds={Bounds}, Parent={Parent}",
                        _rightDockPanel?.Visible,
                        _rightDockPanel?.Bounds,
                        _rightDockPanel?.Parent?.Name ?? "<no parent>");
                    return true;
                }
                finally
                {
                    _rightDockTabs?.ResumeLayout(performLayout: false);
                    _rightDockPanel?.ResumeLayout(performLayout: false);
                    ResumeRedraw(redrawSuspended, "ShowJarvisInRightDock");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] Failed to show JARVIS in right dock panel");
                return false;
            }
        }

        private bool EnsureRightDockPanelInitialized()
        {
            using var uiProbeScope = BeginUiProbeOperationScope("EnsureRightDockPanelInitialized");

            if (_rightDockPanel != null
                && !_rightDockPanel.IsDisposed
                && _rightDockTabs != null
                && !_rightDockTabs.IsDisposed)
            {
                _logger?.LogDebug(
                    "[NAV] EnsureRightDockPanelInitialized: already initialized — panel={Panel}, Visible={Visible}, Bounds={Bounds}, Parent={Parent}",
                    _rightDockPanel.Name, _rightDockPanel.Visible, _rightDockPanel.Bounds,
                    _rightDockPanel.Parent?.Name ?? "<no parent>");
                return true;
            }

            // Log which fields caused the guard to fail so we know exactly what triggered re-init.
            _logger?.LogInformation(
                "[NAV] EnsureRightDockPanelInitialized: (re)initializing — panel={Panel} tabs={Tabs} jarvis={Jarvis}",
                _rightDockPanel == null ? "null" : (_rightDockPanel.IsDisposed ? "disposed" : "ok"),
                _rightDockTabs == null ? "null" : (_rightDockTabs.IsDisposed ? "disposed" : "ok"),
                _rightDockJarvisPanel == null ? "deferred" : (_rightDockJarvisPanel.IsDisposed ? "disposed" : "ok"));

            if (_serviceProvider == null)
            {
                _logger?.LogWarning("[NAV] Cannot initialize right dock panel because service provider is unavailable");
                return false;
            }

            // Capture the previous panel reference BEFORE replacing the field, so it can be
            // removed from the host. Without this, the stub persists as a second DockStyle.Right
            // panel that crowds out the real JARVIS panel.
            var oldPanel = _rightDockPanel;

            var (rightDockPanel, rightDockTabs, _, _) = RightDockPanelFactory.CreateRightDockPanel(
                this,
                _serviceProvider,
                _logger);

            _rightDockPanel = rightDockPanel;
            _rightDockTabs = rightDockTabs;
            _rightDockJarvisPanel = null;
            _rightDockJarvisPanelCreationQueued = 0;
            _rightDockTabs.SelectedIndexChanged -= RightDockTabs_SelectedIndexChanged;
            _rightDockTabs.SelectedIndexChanged += RightDockTabs_SelectedIndexChanged;
            _rightDockPanel.SizeChanged -= RightDockPanel_SizeChanged;
            _rightDockPanel.SizeChanged += RightDockPanel_SizeChanged;

            // Remove the old temporary panel that was created by InitializeLayoutComponents.
            // If we don't do this, both the stub and the real panel sit in host.Controls with
            // DockStyle.Right and steal ~740 px of horizontal space from the MDI client area.
            if (oldPanel != null && !oldPanel.IsDisposed && !ReferenceEquals(oldPanel, rightDockPanel))
            {
                oldPanel.Parent?.Controls.Remove(oldPanel);
                _logger?.LogDebug("[NAV] Removed previous right dock panel '{PanelName}' before adding real panel", oldPanel.Name);
            }

            // Prefer the form as host in MDI/tabbed runs so right-dock participates in
            // the same dock layout space as MdiClient.
            var host = ResolveRightDockHost();
            if (_rightDockPanel.Parent != host)
            {
                _rightDockPanel.Parent?.Controls.Remove(_rightDockPanel);
                host.Controls.Add(_rightDockPanel);
            }
            _rightDockPanel.Dock = DockStyle.Right;
            // 350 px minimum; no MaximumSize so the user can resize via the splitter.
            // Width is already 420 from the factory; re-assert here in case re-init is called.
            _rightDockPanel.MinimumSize = new Size(350, 0);
            _rightDockPanel.MaximumSize = new Size(0, 0);   // 0,0 = no maximum constraint
            if (_rightDockPanel.Width < 350) _rightDockPanel.Width = RightDockPanelFactory.ActivityLogPreferredWidth;
            _rightDockPanel.Visible = true;  // explicit: sidebar is always shown once initialized

            EnsureRightDockSplitter(host);
            EnsureJarvisAutoHideStrip(host);
            RecoverRightDockLayoutIfInvalid(ref host);

            // Honor startup JARVIS auto-open by selecting the JARVIS tab immediately
            // when the right dock is first materialized.
            if (ShouldAutoOpenJarvisOnStartup())
            {
                var startupJarvisTab = FindRightDockTab(RightDockPanelFactory.JarvisTabName);
                if (startupJarvisTab != null && _rightDockTabs != null)
                {
                    _rightDockTabs.SelectedTab = startupJarvisTab;
                    EnsureRightDockJarvisPanelMaterialized(parameters: null);
                }
            }

            ApplyRightDockWidthForSelectedTab(force: true);

            SetJarvisAutoHideButtonState(isSidebarVisible: _rightDockPanel.Visible);

            _logger?.LogDebug(
                "[NAV] Right dock panel configured — Dock={Dock}, Width={Width}, Host={Host}, ControlsInHost={ControlCount}",
                _rightDockPanel.Dock, _rightDockPanel.Width,
                host.Name, host.Controls.Count);

            _rightDockPanel.BringToFront();
            RefreshPanelHostLayout("EnsureRightDockPanelInitialized");

            _logger?.LogInformation(
                "[NAV] Right dock panel initialized — Visible={Visible}, Bounds={Bounds}, Parent={Parent}, Tabs={TabCount}",
                _rightDockPanel.Visible, _rightDockPanel.Bounds,
                _rightDockPanel.Parent?.Name ?? "<no parent>",
                _rightDockTabs?.TabPages.Count ?? 0);

            TraceLayoutSnapshot("EnsureRightDockPanelInitialized.Complete");

            // BlazorWebView init is now lazy: JARVISChatUserControl.OnVisibleChanged fires
            // the first time the JARVIS tab is selected, saving ~400 ms at startup.

            return true;
        }

        private void EnsureRightDockJarvisPanelMaterialized(object? parameters)
        {
            if (parameters != null)
            {
                _pendingRightDockJarvisParameters = parameters;
            }

            if (_rightDockJarvisPanel != null && !_rightDockJarvisPanel.IsDisposed)
            {
                if (parameters is not null)
                {
                    _rightDockJarvisPanel.InitializeWithParameters(parameters);
                }

                return;
            }

            if (Interlocked.CompareExchange(ref _rightDockJarvisPanelCreationQueued, 1, 0) != 0)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)MaterializeRightDockJarvisPanel);
            }
            catch (Exception ex)
            {
                _rightDockJarvisPanelCreationQueued = 0;
                _logger?.LogDebug(ex, "[NAV] Failed to queue JARVIS right dock materialization");
            }
        }

        private void MaterializeRightDockJarvisPanel()
        {
            try
            {
                if (_rightDockJarvisPanel != null && !_rightDockJarvisPanel.IsDisposed)
                {
                    if (_pendingRightDockJarvisParameters is not null)
                    {
                        _rightDockJarvisPanel.InitializeWithParameters(_pendingRightDockJarvisParameters);
                        _pendingRightDockJarvisParameters = null;
                    }

                    return;
                }

                if (_serviceProvider == null)
                {
                    _logger?.LogWarning("[NAV] Cannot materialize JARVIS right dock panel because service provider is unavailable");
                    return;
                }

                var jarvisTab = FindRightDockTab("RightDockTab_JARVIS");
                if (jarvisTab == null)
                {
                    _logger?.LogWarning("[NAV] Cannot materialize JARVIS right dock panel because the JARVIS tab is unavailable");
                    return;
                }

                var jarvisChatPanel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<JARVISChatUserControl>(_serviceProvider);
                jarvisChatPanel.Dock = DockStyle.Fill;
                jarvisChatPanel.Name = "JARVISDockPanel";

                if (_pendingRightDockJarvisParameters is not null && jarvisChatPanel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(_pendingRightDockJarvisParameters);
                    _pendingRightDockJarvisParameters = null;
                }

                jarvisTab.SuspendLayout();
                try
                {
                    jarvisTab.Controls.Clear();
                    jarvisTab.Controls.Add(jarvisChatPanel);
                }
                finally
                {
                    jarvisTab.ResumeLayout(performLayout: false);
                }

                _rightDockJarvisPanel = jarvisChatPanel;
                _logger?.LogInformation("[NAV] JARVIS right dock panel materialized on demand");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] Failed to materialize JARVIS right dock panel");
            }
            finally
            {
                _rightDockJarvisPanelCreationQueued = 0;
            }
        }

        /// <summary>Creates the resize splitter for the right dock panel, if not already present.</summary>
        private void EnsureRightDockSplitter(Control host)
        {
            if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
            {
                if (!ReferenceEquals(_rightDockSplitter.Parent, host))
                {
                    _rightDockSplitter.Parent?.Controls.Remove(_rightDockSplitter);
                    host.Controls.Add(_rightDockSplitter);
                }

                _rightDockSplitter.Dock = DockStyle.Right;
                return;
            }

            _rightDockSplitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = 5,
                Cursor = Cursors.VSplit,
                Name = "RightDockSplitter",
                MinSize = 350,
            };
            host.Controls.Add(_rightDockSplitter);
        }

        /// <summary>
        /// Creates (or recreates) the 22 px auto-hide strip that sits to the left of the right dock panel.
        /// The strip is always visible so the user can re-expand JARVIS after collapsing it.
        /// Only pure WinForms layout primitives are used here (Panel/Button) — the SfSkinManager
        /// theme cascade applied via <see cref="SfSkinManager.SetVisualStyle"/> ensures visual consistency.
        /// </summary>
        private void EnsureJarvisAutoHideStrip(Control host)
        {
            if (_jarvisAutoHideStrip != null && !_jarvisAutoHideStrip.IsDisposed)
            {
                if (!ReferenceEquals(_jarvisAutoHideStrip.Parent, host))
                {
                    _jarvisAutoHideStrip.Parent?.Controls.Remove(_jarvisAutoHideStrip);
                    host.Controls.Add(_jarvisAutoHideStrip);
                }

                _jarvisAutoHideStrip.Dock = DockStyle.Right;
                SetJarvisAutoHideButtonState(isSidebarVisible: _rightDockPanel?.Visible ?? true);
                return;
            }

            var themeName = SfSkinManager.ApplicationVisualTheme
                            ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            // Thin vertical strip — always occupies 22 px on the right edge of the host.
            _jarvisAutoHideStrip = new Panel
            {
                Name = "JarvisAutoHideStrip",
                Dock = DockStyle.Right,
                Width = 30,
                Padding = Padding.Empty,
            };
            SfSkinManager.SetVisualStyle(_jarvisAutoHideStrip, themeName);

            // Collapse / expand button — sits at the very top of the strip.
            _jarvisAutoHideButton = new Button
            {
                Name = "JarvisAutoHideButton",
                Text = "<<",
                Dock = DockStyle.Top,
                Height = 64,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 8f),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = "JARVIS sidebar toggle",
                AccessibleDescription = "Hide or show the JARVIS / Activity Log sidebar",
            };
            _jarvisAutoHideButton.FlatAppearance.BorderSize = 0;
            _jarvisAutoHideButton.Click += (_, _) => ToggleJarvisAutoHide();

            _jarvisAutoHideStrip.Controls.Add(_jarvisAutoHideButton);
            host.Controls.Add(_jarvisAutoHideStrip);
            SetJarvisAutoHideButtonState(isSidebarVisible: _rightDockPanel?.Visible ?? true);

            _logger?.LogDebug("[NAV] JARVIS auto-hide strip created");
        }

        private void SetJarvisAutoHideButtonState(bool isSidebarVisible)
        {
            if (_jarvisAutoHideButton == null || _jarvisAutoHideButton.IsDisposed)
            {
                return;
            }

            _jarvisAutoHideButton.Text = isSidebarVisible ? "<<" : ">>";
            _jarvisAutoHideButton.AccessibleDescription = isSidebarVisible
                ? "Hide the JARVIS / Activity Log sidebar"
                : "Show the JARVIS / Activity Log sidebar";
        }

        /// <summary>
        /// Toggles the JARVIS / Activity Log right dock panel between expanded and collapsed.
        /// When collapsed the right dock panel and its resize splitter are hidden, freeing the
        /// full content width for panels.  The 22 px auto-hide strip remains visible so the
        /// user can re-open the sidebar at any time.
        /// </summary>
        private void ToggleJarvisAutoHide()
        {
            if (_rightDockPanel == null || _rightDockPanel.IsDisposed)
                return;

            var redrawSuspended = TrySuspendRedraw("ToggleJarvisAutoHide");
            _rightDockPanel.SuspendLayout();

            var isCurrentlyVisible = _rightDockPanel.Visible;

            try
            {
                if (isCurrentlyVisible)
                {
                    // Save current expanded width before collapsing.
                    if (_rightDockPanel.Width > 0)
                    {
                        RememberRightDockWidth(_rightDockPanel.Width);
                        _jarvisExpandedWidth = _rightDockPanel.Width;
                    }

                    _rightDockPanel.Visible = false;
                    if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
                        _rightDockSplitter.Visible = false;
                    SetJarvisAutoHideButtonState(isSidebarVisible: false);

                    _logger?.LogDebug("[NAV] JARVIS sidebar collapsed — width saved: {Width}px", _jarvisExpandedWidth);
                }
                else
                {
                    ApplyRightDockWidthForSelectedTab(force: true);

                    _rightDockPanel.Visible = true;
                    if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
                        _rightDockSplitter.Visible = true;
                    SetJarvisAutoHideButtonState(isSidebarVisible: true);

                    _rightDockPanel.BringToFront();
                    _logger?.LogDebug("[NAV] JARVIS sidebar expanded — width: {Width}px", _rightDockPanel.Width);
                }

                // Re-constrain the MdiClient immediately so panel content fills the newly available space.
                RefreshPanelHostLayout("ToggleJarvisAutoHide", force: true);
            }
            finally
            {
                _rightDockPanel.ResumeLayout(performLayout: false);
                ResumeRedraw(redrawSuspended, "ToggleJarvisAutoHide");
            }
        }

        private TabPageAdv? FindRightDockTab(string tabName)
        {
            if (_rightDockTabs?.TabPages == null)
            {
                return null;
            }

            foreach (TabPageAdv tab in _rightDockTabs.TabPages)
            {
                if (string.Equals(tab.Name, tabName, StringComparison.OrdinalIgnoreCase))
                {
                    return tab;
                }
            }

            return null;
        }

        private void RightDockTabs_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_rightDockTabs?.SelectedTab == null)
            {
                return;
            }

            ApplyRightDockWidthForSelectedTab(force: true);

            if (string.Equals(_rightDockTabs.SelectedTab.Name, RightDockPanelFactory.JarvisTabName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureRightDockJarvisPanelMaterialized(parameters: null);
            }
        }

        private void RightDockPanel_SizeChanged(object? sender, EventArgs e)
        {
            if (_isApplyingRightDockWidth || _rightDockPanel == null || _rightDockPanel.IsDisposed || !_rightDockPanel.Visible)
            {
                return;
            }

            if (_rightDockPanel.Width >= _rightDockPanel.MinimumSize.Width)
            {
                RememberRightDockWidth(_rightDockPanel.Width);
            }
        }

        private void ApplyRightDockWidthForSelectedTab(bool force)
        {
            if (_rightDockPanel == null || _rightDockPanel.IsDisposed || _rightDockTabs?.SelectedTab == null)
            {
                return;
            }

            var targetWidth = ResolveRightDockWidth(_rightDockTabs.SelectedTab);
            if (!force && _rightDockPanel.Width == targetWidth)
            {
                return;
            }

            _isApplyingRightDockWidth = true;
            try
            {
                _rightDockPanel.Width = Math.Max(_rightDockPanel.MinimumSize.Width, targetWidth);
            }
            finally
            {
                _isApplyingRightDockWidth = false;
            }
        }

        private int ResolveRightDockWidth(TabPageAdv selectedTab)
        {
            var taggedWidth = selectedTab.Tag as int? ?? RightDockPanelFactory.JarvisPreferredWidth;

            if (string.Equals(selectedTab.Name, RightDockPanelFactory.ActivityLogTabName, StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(_activityLogExpandedWidth, taggedWidth);
            }

            if (string.Equals(selectedTab.Name, RightDockPanelFactory.JarvisTabName, StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(_jarvisExpandedWidth, taggedWidth);
            }

            return taggedWidth;
        }

        private void RememberRightDockWidth(int width)
        {
            if (_rightDockTabs?.SelectedTab == null || width <= 0)
            {
                return;
            }

            if (string.Equals(_rightDockTabs.SelectedTab.Name, RightDockPanelFactory.ActivityLogTabName, StringComparison.OrdinalIgnoreCase))
            {
                _activityLogExpandedWidth = width;
                return;
            }

            if (string.Equals(_rightDockTabs.SelectedTab.Name, RightDockPanelFactory.JarvisTabName, StringComparison.OrdinalIgnoreCase))
            {
                _jarvisExpandedWidth = width;
            }
        }

        private Control ResolveRightDockHost()
        {
            if (IsMdiContainer || _tabbedMdi != null)
            {
                return this;
            }

            return (_panelHost as Control) ?? this;
        }

        private void RecoverRightDockLayoutIfInvalid(ref Control host)
        {
            if (_rightDockPanel == null || _rightDockPanel.IsDisposed)
            {
                return;
            }

            var panelBounds = _rightDockPanel.Bounds;
            var hostClient = host.ClientSize;
            var looksInvalid = panelBounds.Left < 0
                || panelBounds.Height < 140
                || (hostClient.Width > 0 && panelBounds.Width > hostClient.Width);

            if (!looksInvalid)
            {
                return;
            }

            _logger?.LogWarning(
                "[NAV] Right dock layout invalid on host '{Host}' — Bounds={Bounds}, HostClient={HostClient}. Reparenting to form host.",
                host.Name,
                panelBounds,
                hostClient);

            if (!ReferenceEquals(host, this))
            {
                host = this;
                ReparentRightDockArtifacts(host);
            }

            if (_rightDockPanel.Width > ClientSize.Width && ClientSize.Width > 360)
            {
                _rightDockPanel.Width = Math.Max(320, ClientSize.Width / 3);
            }

            RefreshPanelHostLayout("RecoverRightDockLayoutIfInvalid");
        }

        private void ReparentRightDockArtifacts(Control newHost)
        {
            if (_rightDockPanel != null && !_rightDockPanel.IsDisposed)
            {
                if (!ReferenceEquals(_rightDockPanel.Parent, newHost))
                {
                    _rightDockPanel.Parent?.Controls.Remove(_rightDockPanel);
                    newHost.Controls.Add(_rightDockPanel);
                }

                _rightDockPanel.Dock = DockStyle.Right;
            }

            if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
            {
                if (!ReferenceEquals(_rightDockSplitter.Parent, newHost))
                {
                    _rightDockSplitter.Parent?.Controls.Remove(_rightDockSplitter);
                    newHost.Controls.Add(_rightDockSplitter);
                }

                _rightDockSplitter.Dock = DockStyle.Right;
            }

            if (_jarvisAutoHideStrip != null && !_jarvisAutoHideStrip.IsDisposed)
            {
                if (!ReferenceEquals(_jarvisAutoHideStrip.Parent, newHost))
                {
                    _jarvisAutoHideStrip.Parent?.Controls.Remove(_jarvisAutoHideStrip);
                    newHost.Controls.Add(_jarvisAutoHideStrip);
                }

                _jarvisAutoHideStrip.Dock = DockStyle.Right;
            }
        }
    }
}
