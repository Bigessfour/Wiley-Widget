using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
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
                    }

                    EnsureRightDockArtifactsDockOrder(ResolveRightDockHost());

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

            EnsureRightDockArtifactsDockOrder(host);
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

                var jarvisHost = new Panel
                {
                    Name = "JARVISDockHost",
                    Dock = DockStyle.Fill,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty,
                };

                var jarvisSplitContainer = new SplitContainer
                {
                    Name = "JARVISDockVerticalResizeHost",
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Horizontal,
                    FixedPanel = FixedPanel.None,
                    IsSplitterFixed = false,
                    Panel1MinSize = 30,
                    Panel2MinSize = 0,
                    SplitterWidth = 10,
                    Cursor = Cursors.HSplit,
                    BorderStyle = BorderStyle.None,
                    BackColor = SystemColors.ControlDark,
                };

                jarvisSplitContainer.SplitterMoved += (_, _) =>
                {
                    _jarvisTopInset = jarvisSplitContainer.SplitterDistance;
                    _logger?.LogInformation(
                        "[NAV-RESIZE] JARVIS top splitter moved - SplitterDistance={SplitterDistance}, Panel1Height={Panel1Height}, Panel2Height={Panel2Height}, StoredInset={StoredInset}",
                        jarvisSplitContainer.SplitterDistance,
                        jarvisSplitContainer.Panel1.ClientSize.Height,
                        jarvisSplitContainer.Panel2.ClientSize.Height,
                        _jarvisTopInset);
                };
                jarvisSplitContainer.SplitterMoving += (_, e) =>
                {
                    _logger?.LogDebug(
                        "[NAV-RESIZE] JARVIS top splitter moving - SplitX={SplitX}, SplitY={SplitY}, CurrentDistance={CurrentDistance}, HostHeight={HostHeight}",
                        e.SplitX,
                        e.SplitY,
                        jarvisSplitContainer.SplitterDistance,
                        jarvisSplitContainer.ClientSize.Height);
                };
                jarvisSplitContainer.SizeChanged += (_, _) => ApplyJarvisTopInset(jarvisSplitContainer, jarvisChatPanel.MinimumSize.Height, preserveStoredInset: true);
                jarvisSplitContainer.HandleCreated += (_, _) => ApplyJarvisTopInset(jarvisSplitContainer, jarvisChatPanel.MinimumSize.Height, preserveStoredInset: true);
                jarvisSplitContainer.Panel1.BackColor = SystemColors.ControlLight;
                jarvisSplitContainer.Panel1.Controls.Add(CreateJarvisTopResizeGrip(jarvisSplitContainer));
                jarvisSplitContainer.Panel2.Controls.Add(jarvisChatPanel);
                jarvisHost.Controls.Add(jarvisSplitContainer);

                jarvisTab.SuspendLayout();
                try
                {
                    jarvisTab.Controls.Clear();
                    jarvisTab.Controls.Add(jarvisHost);
                }
                finally
                {
                    jarvisTab.ResumeLayout(performLayout: false);
                }

                _rightDockJarvisPanel = jarvisChatPanel;
                _rightDockJarvisSplitContainer = jarvisSplitContainer;
                ApplyThemeToRightDockArtifacts(_themeService?.CurrentTheme ?? SfSkinManager.ApplicationVisualTheme ?? Themes.ThemeColors.DefaultTheme);
                BeginInvoke(new System.Action(() => ApplyJarvisTopInset(jarvisSplitContainer, jarvisChatPanel.MinimumSize.Height, preserveStoredInset: true)));
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
                EnsureRightDockArtifactsDockOrder(host);
                EnsureJarvisResizeToolTip();
                _jarvisResizeToolTip?.SetToolTip(_rightDockSplitter, "Drag left or right to resize the JARVIS sidebar.");
                return;
            }

            _rightDockSplitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = 8,
                Cursor = Cursors.VSplit,
                Name = "RightDockSplitter",
                MinSize = 350,
                AccessibleName = "JARVIS sidebar resize handle",
                AccessibleDescription = "Drag left or right to resize the JARVIS sidebar.",
                BackColor = SystemColors.ControlDark,
            };
            _rightDockSplitter.Paint += (_, e) => PaintRightDockSplitter(e.Graphics, _rightDockSplitter.ClientRectangle, _rightDockSplitter.BackColor);
            _rightDockSplitter.MouseEnter += (_, _) => _rightDockSplitter.BackColor = SystemColors.ControlDarkDark;
            _rightDockSplitter.MouseLeave += (_, _) => _rightDockSplitter.BackColor = SystemColors.ControlDark;
            _rightDockSplitter.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                _logger?.LogInformation(
                    "[NAV-RESIZE] Right dock splitter drag start - PanelWidth={PanelWidth}, HostWidth={HostWidth}, SelectedTab={SelectedTab}",
                    _rightDockPanel?.Width,
                    host.ClientSize.Width,
                    _rightDockTabs?.SelectedTab?.Name ?? "<none>");
            };
            _rightDockSplitter.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                _logger?.LogInformation(
                    "[NAV-RESIZE] Right dock splitter drag end - PanelWidth={PanelWidth}, StoredJarvisWidth={StoredJarvisWidth}, StoredActivityWidth={StoredActivityWidth}",
                    _rightDockPanel?.Width,
                    _jarvisExpandedWidth,
                    _activityLogExpandedWidth);
                LogRightDockResizeState("RightDockSplitter.MouseUp");
            };
            _rightDockSplitter.SplitterMoving += (_, e) =>
            {
                _logger?.LogDebug(
                    "[NAV-RESIZE] Right dock splitter moving - SplitX={SplitX}, SplitY={SplitY}, PanelWidth={PanelWidth}, HostWidth={HostWidth}",
                    e.SplitX,
                    e.SplitY,
                    _rightDockPanel?.Width,
                    host.ClientSize.Width);
            };
            _rightDockSplitter.SplitterMoved += (_, e) =>
            {
                _logger?.LogInformation(
                    "[NAV-RESIZE] Right dock splitter moved - SplitX={SplitX}, SplitY={SplitY}, PanelWidth={PanelWidth}, SelectedTab={SelectedTab}",
                    e.SplitX,
                    e.SplitY,
                    _rightDockPanel?.Width,
                    _rightDockTabs?.SelectedTab?.Name ?? "<none>");
                LogRightDockResizeState("RightDockSplitter.SplitterMoved");
            };
            EnsureJarvisResizeToolTip();
            _jarvisResizeToolTip?.SetToolTip(_rightDockSplitter, "Drag left or right to resize the JARVIS sidebar.");
            host.Controls.Add(_rightDockSplitter);
            EnsureRightDockArtifactsDockOrder(host);
            _logger?.LogInformation(
                "[NAV-RESIZE] Right dock splitter ready - Host={Host}, Width={Width}, MinPanelWidth={MinPanelWidth}",
                host.Name,
                _rightDockSplitter.Width,
                _rightDockSplitter.MinSize);
        }

        private void EnsureJarvisResizeToolTip()
        {
            _jarvisResizeToolTip ??= new ToolTip
            {
                InitialDelay = 200,
                ReshowDelay = 100,
                AutoPopDelay = 5000,
                ShowAlways = true,
            };
        }

        private Control CreateJarvisTopResizeGrip(SplitContainer jarvisSplitContainer)
        {
            var gripPanel = new Panel
            {
                Name = "JarvisTopResizeGrip",
                Dock = DockStyle.Fill,
                Cursor = Cursors.HSplit,
                AccessibleName = "JARVIS top resize handle",
                AccessibleDescription = "Drag up or down to resize the JARVIS panel height. Double-click to reset.",
                Padding = new Padding(12, 4, 12, 4),
                BackColor = SystemColors.ControlLight,
                Margin = Padding.Empty,
            };

            var gripIconLabel = new Label
            {
                Name = "JarvisTopResizeGripIcon",
                Dock = DockStyle.Left,
                Width = 32,
                Text = "====",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = SystemColors.ControlDarkDark,
                Cursor = Cursors.HSplit,
            };

            var gripCaptionLabel = new Label
            {
                Name = "JarvisTopResizeGripCaption",
                Dock = DockStyle.Fill,
                Text = "Drag to resize JARVIS height. Double-click to reset.",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = SystemColors.ControlDarkDark,
                Cursor = Cursors.HSplit,
            };

            gripPanel.Controls.Add(gripCaptionLabel);
            gripPanel.Controls.Add(gripIconLabel);

            AttachJarvisTopGripHandlers(gripPanel, jarvisSplitContainer);
            AttachJarvisTopGripHandlers(gripCaptionLabel, jarvisSplitContainer);
            AttachJarvisTopGripHandlers(gripIconLabel, jarvisSplitContainer);

            EnsureJarvisResizeToolTip();
            _jarvisResizeToolTip?.SetToolTip(gripPanel, "Drag up or down to resize the JARVIS panel height. Double-click to reset.");
            _jarvisResizeToolTip?.SetToolTip(gripCaptionLabel, "Drag up or down to resize the JARVIS panel height. Double-click to reset.");
            _jarvisResizeToolTip?.SetToolTip(gripIconLabel, "Drag up or down to resize the JARVIS panel height. Double-click to reset.");

            gripPanel.Paint += (_, e) => PaintJarvisTopGrip(e.Graphics, gripPanel.ClientRectangle);
            gripPanel.MouseEnter += (_, _) => gripPanel.BackColor = SystemColors.ControlLightLight;
            gripPanel.MouseLeave += (_, _) =>
            {
                if (!_isDraggingJarvisTopGrip)
                {
                    gripPanel.BackColor = SystemColors.ControlLight;
                }
            };

            _logger?.LogInformation(
                "[NAV-RESIZE] JARVIS top grip ready - SplitterWidth={SplitterWidth}, Panel1MinSize={Panel1MinSize}",
                jarvisSplitContainer.SplitterWidth,
                jarvisSplitContainer.Panel1MinSize);

            return gripPanel;
        }

        private void AttachJarvisTopGripHandlers(Control control, SplitContainer jarvisSplitContainer)
        {
            control.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                _isDraggingJarvisTopGrip = true;
                _jarvisTopGripStartInset = ResolveJarvisTopInset(jarvisSplitContainer, preserveStoredInset: true);
                _jarvisTopGripStartScreenY = Control.MousePosition.Y;
                control.Capture = true;
                _logger?.LogInformation(
                    "[NAV-RESIZE] JARVIS top grip drag start - StartInset={StartInset}, ScreenY={ScreenY}, HostHeight={HostHeight}, Panel2MinSize={Panel2MinSize}",
                    _jarvisTopGripStartInset,
                    _jarvisTopGripStartScreenY,
                    jarvisSplitContainer.ClientSize.Height,
                    jarvisSplitContainer.Panel2MinSize);
            };

            control.MouseMove += (_, _) =>
            {
                if (!_isDraggingJarvisTopGrip)
                {
                    return;
                }

                var deltaY = Control.MousePosition.Y - _jarvisTopGripStartScreenY;
                _logger?.LogDebug(
                    "[NAV-RESIZE] JARVIS top grip moving - StartInset={StartInset}, DeltaY={DeltaY}, RequestedInset={RequestedInset}, CurrentDistance={CurrentDistance}",
                    _jarvisTopGripStartInset,
                    deltaY,
                    _jarvisTopGripStartInset + deltaY,
                    jarvisSplitContainer.SplitterDistance);
                ApplyJarvisTopInset(jarvisSplitContainer, jarvisSplitContainer.Panel2MinSize, preserveStoredInset: false, requestedInset: _jarvisTopGripStartInset + deltaY);
            };

            control.MouseUp += (_, _) =>
            {
                _isDraggingJarvisTopGrip = false;
                control.Capture = false;
                _logger?.LogInformation(
                    "[NAV-RESIZE] JARVIS top grip drag end - SplitterDistance={SplitterDistance}, StoredInset={StoredInset}, Panel2Height={Panel2Height}",
                    jarvisSplitContainer.SplitterDistance,
                    _jarvisTopInset,
                    jarvisSplitContainer.Panel2.ClientSize.Height);
            };

            control.DoubleClick += (_, _) =>
            {
                _logger?.LogInformation(
                    "[NAV-RESIZE] JARVIS top grip reset requested - ResetInset={ResetInset}",
                    jarvisSplitContainer.Panel1MinSize);
                ApplyJarvisTopInset(jarvisSplitContainer, jarvisSplitContainer.Panel2MinSize, preserveStoredInset: false, requestedInset: jarvisSplitContainer.Panel1MinSize);
            };
        }

        private int ResolveJarvisTopInset(SplitContainer jarvisSplitContainer, bool preserveStoredInset)
        {
            var availableHeight = jarvisSplitContainer.ClientSize.Height;
            var panel1Min = Math.Max(0, jarvisSplitContainer.Panel1MinSize);
            var splitterWidth = Math.Max(0, jarvisSplitContainer.SplitterWidth);
            var requestedInset = preserveStoredInset && _jarvisTopInset > 0
                ? _jarvisTopInset
                : panel1Min;

            if (availableHeight <= 0)
            {
                _logger?.LogDebug(
                    "[NAV-RESIZE] ResolveJarvisTopInset deferred - AvailableHeight={AvailableHeight}, Panel1Min={Panel1Min}, RequestedInset={RequestedInset}",
                    availableHeight,
                    panel1Min,
                    requestedInset);
                return Math.Max(panel1Min, requestedInset);
            }

            var maxInset = Math.Max(panel1Min, availableHeight - jarvisSplitContainer.Panel2MinSize - splitterWidth);
            return Math.Max(panel1Min, Math.Min(maxInset, requestedInset));
        }

        private void ApplyJarvisTopInset(
            SplitContainer jarvisSplitContainer,
            int desiredPanel2MinSize,
            bool preserveStoredInset,
            int? requestedInset = null)
        {
            if (jarvisSplitContainer.IsDisposed)
            {
                return;
            }

            var availableHeight = jarvisSplitContainer.ClientSize.Height;
            var panel1Min = Math.Max(0, jarvisSplitContainer.Panel1MinSize);
            var splitterWidth = Math.Max(0, jarvisSplitContainer.SplitterWidth);
            if (availableHeight <= 0 || availableHeight <= panel1Min + splitterWidth)
            {
                _logger?.LogDebug(
                    "[NAV-RESIZE] ApplyJarvisTopInset skipped - AvailableHeight={AvailableHeight}, Panel1Min={Panel1Min}, SplitterWidth={SplitterWidth}",
                    availableHeight,
                    panel1Min,
                    splitterWidth);
                return;
            }

            var maxPanel2MinSize = Math.Max(0, availableHeight - panel1Min - splitterWidth);
            var effectivePanel2MinSize = Math.Max(0, Math.Min(desiredPanel2MinSize, maxPanel2MinSize));
            if (jarvisSplitContainer.Panel2MinSize != effectivePanel2MinSize)
            {
                jarvisSplitContainer.Panel2MinSize = effectivePanel2MinSize;
            }

            var targetInset = requestedInset ?? (preserveStoredInset && _jarvisTopInset > 0 ? _jarvisTopInset : panel1Min);
            var maxInset = Math.Max(panel1Min, availableHeight - effectivePanel2MinSize - splitterWidth);
            var unclampedInset = targetInset;
            targetInset = Math.Max(panel1Min, Math.Min(maxInset, targetInset));

            if (jarvisSplitContainer.SplitterDistance != targetInset)
            {
                jarvisSplitContainer.SplitterDistance = targetInset;
            }

            _jarvisTopInset = targetInset;
            _logger?.LogDebug(
                "[NAV-RESIZE] ApplyJarvisTopInset applied - RequestedInset={RequestedInset}, ClampedInset={ClampedInset}, MaxInset={MaxInset}, Panel2MinSize={Panel2MinSize}, Panel2Height={Panel2Height}",
                unclampedInset,
                targetInset,
                maxInset,
                effectivePanel2MinSize,
                jarvisSplitContainer.Panel2.ClientSize.Height);
        }

        private void EnsureRightDockArtifactsDockOrder(Control host)
        {
            if (host.IsDisposed)
            {
                return;
            }

            var orderedRightDockArtifacts = new List<Control>(3);

            if (_rightDockPanel != null && !_rightDockPanel.IsDisposed && ReferenceEquals(_rightDockPanel.Parent, host))
            {
                orderedRightDockArtifacts.Add(_rightDockPanel);
            }

            if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed && ReferenceEquals(_rightDockSplitter.Parent, host))
            {
                orderedRightDockArtifacts.Add(_rightDockSplitter);
            }

            if (_jarvisAutoHideStrip != null && !_jarvisAutoHideStrip.IsDisposed && ReferenceEquals(_jarvisAutoHideStrip.Parent, host))
            {
                orderedRightDockArtifacts.Add(_jarvisAutoHideStrip);
            }

            // DockStyle.Right controls are laid out in reverse z-order. Make the visual order
            // explicit so the right-dock panel stays on the outer edge, the splitter stays just
            // to its left, and the auto-hide strip remains accessible beside them.
            for (var index = 0; index < orderedRightDockArtifacts.Count; index++)
            {
                var control = orderedRightDockArtifacts[index];
                host.Controls.SetChildIndex(control, orderedRightDockArtifacts.Count - 1 - index);
            }

            _logger?.LogDebug(
                "[NAV-RESIZE] Right dock artifact order enforced on host '{Host}' - PanelIndex={PanelIndex}, SplitterIndex={SplitterIndex}, StripIndex={StripIndex}",
                host.Name,
                _rightDockPanel != null && !_rightDockPanel.IsDisposed && ReferenceEquals(_rightDockPanel.Parent, host) ? host.Controls.GetChildIndex(_rightDockPanel) : -1,
                _rightDockSplitter != null && !_rightDockSplitter.IsDisposed && ReferenceEquals(_rightDockSplitter.Parent, host) ? host.Controls.GetChildIndex(_rightDockSplitter) : -1,
                _jarvisAutoHideStrip != null && !_jarvisAutoHideStrip.IsDisposed && ReferenceEquals(_jarvisAutoHideStrip.Parent, host) ? host.Controls.GetChildIndex(_jarvisAutoHideStrip) : -1);
        }

        private void ApplyThemeToRightDockArtifacts(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                return;
            }

            try
            {
                if (_rightDockPanel != null && !_rightDockPanel.IsDisposed)
                {
                    SfSkinManager.SetVisualStyle(_rightDockPanel, themeName);
                    _rightDockPanel.Invalidate(true);
                }

                if (_rightDockTabs != null && !_rightDockTabs.IsDisposed)
                {
                    _rightDockTabs.ThemeName = themeName;
                    SfSkinManager.SetVisualStyle(_rightDockTabs, themeName);
                    _rightDockTabs.Invalidate(true);
                }

                if (_rightDockJarvisPanel != null && !_rightDockJarvisPanel.IsDisposed)
                {
                    SyncfusionControlFactory.ApplyThemeToAllControls(_rightDockJarvisPanel, themeName, _logger);
                }

                if (_jarvisAutoHideStrip != null && !_jarvisAutoHideStrip.IsDisposed)
                {
                    SfSkinManager.SetVisualStyle(_jarvisAutoHideStrip, themeName);
                }

                if (_jarvisAutoHideButton != null && !_jarvisAutoHideButton.IsDisposed)
                {
                    SfSkinManager.SetVisualStyle(_jarvisAutoHideButton, themeName);
                }

                ApplyThemeToJarvisResizeSurfaces(themeName);

                _logger?.LogInformation(
                    "[THEME] Refreshed right-dock/JARVIS theme surfaces for {Theme} - RightDockVisible={RightDockVisible}, JarvisMaterialized={JarvisMaterialized}",
                    themeName,
                    _rightDockPanel?.Visible,
                    _rightDockJarvisPanel != null && !_rightDockJarvisPanel.IsDisposed);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[THEME] Failed to refresh right-dock/JARVIS theme surfaces for {Theme}", themeName);
            }
        }

        private void ApplyThemeToJarvisResizeSurfaces(string themeName)
        {
            var isDarkTheme = themeName.Contains("Dark", StringComparison.OrdinalIgnoreCase)
                || themeName.Contains("Black", StringComparison.OrdinalIgnoreCase);
            var baseSurface = _rightDockTabs?.BackColor
                ?? _rightDockPanel?.BackColor
                ?? SystemColors.Control;
            var accentSurface = isDarkTheme ? ControlPaint.Light(baseSurface) : ControlPaint.Dark(baseSurface);
            var hoverSurface = isDarkTheme ? ControlPaint.LightLight(baseSurface) : ControlPaint.Light(baseSurface);
            var textColor = _rightDockTabs?.ForeColor
                ?? _rightDockPanel?.ForeColor
                ?? SystemColors.ControlText;

            if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
            {
                _rightDockSplitter.BackColor = accentSurface;
                _rightDockSplitter.Invalidate();
            }

            if (_rightDockJarvisSplitContainer != null && !_rightDockJarvisSplitContainer.IsDisposed)
            {
                _rightDockJarvisSplitContainer.BackColor = accentSurface;
                _rightDockJarvisSplitContainer.Panel1.BackColor = hoverSurface;

                var gripPanel = _rightDockJarvisSplitContainer.Panel1.Controls["JarvisTopResizeGrip"];
                if (gripPanel != null)
                {
                    gripPanel.BackColor = hoverSurface;
                    gripPanel.ForeColor = textColor;
                    gripPanel.Invalidate();
                }

                var gripIconLabel = _rightDockJarvisSplitContainer.Panel1.Controls.Find("JarvisTopResizeGripIcon", true);
                foreach (var control in gripIconLabel)
                {
                    control.ForeColor = textColor;
                    control.BackColor = hoverSurface;
                    control.Invalidate();
                }

                var gripCaptionLabel = _rightDockJarvisSplitContainer.Panel1.Controls.Find("JarvisTopResizeGripCaption", true);
                foreach (var control in gripCaptionLabel)
                {
                    control.ForeColor = textColor;
                    control.BackColor = hoverSurface;
                    control.Invalidate();
                }

                _rightDockJarvisSplitContainer.Invalidate(true);
            }
        }

        private void LogRightDockResizeState(string reason)
        {
            _logger?.LogInformation(
                "[NAV-RESIZE] {Reason} - PanelWidth={PanelWidth}, PanelVisible={PanelVisible}, SplitterVisible={SplitterVisible}, SelectedTab={SelectedTab}, StoredJarvisWidth={StoredJarvisWidth}, StoredActivityWidth={StoredActivityWidth}, JarvisTopInset={JarvisTopInset}",
                reason,
                _rightDockPanel?.Width,
                _rightDockPanel?.Visible,
                _rightDockSplitter?.Visible,
                _rightDockTabs?.SelectedTab?.Name ?? "<none>",
                _jarvisExpandedWidth,
                _activityLogExpandedWidth,
                _jarvisTopInset);
        }

        private static void PaintJarvisTopGrip(Graphics graphics, Rectangle bounds)
        {
            using var borderPen = new Pen(SystemColors.ControlDark);
            graphics.DrawLine(borderPen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

            var gripY = Math.Max(6, bounds.Height / 2 - 2);
            var startX = bounds.Right - 44;
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    graphics.FillEllipse(SystemBrushes.ControlDarkDark, startX + (column * 6), gripY + (row * 6), 2, 2);
                }
            }
        }

        private static void PaintRightDockSplitter(Graphics graphics, Rectangle bounds, Color backColor)
        {
            using var backgroundBrush = new SolidBrush(backColor);
            graphics.FillRectangle(backgroundBrush, bounds);

            using var centerLinePen = new Pen(SystemColors.ControlLightLight);
            var centerX = bounds.Width / 2;
            graphics.DrawLine(centerLinePen, centerX, 0, centerX, bounds.Height);

            var startY = Math.Max(24, bounds.Height / 2 - 20);
            for (var index = 0; index < 6; index++)
            {
                var y = startY + (index * 8);
                graphics.FillEllipse(SystemBrushes.ControlLightLight, centerX - 1, y, 2, 2);
            }
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
                EnsureRightDockArtifactsDockOrder(host);
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
            EnsureRightDockArtifactsDockOrder(host);
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

                    EnsureRightDockArtifactsDockOrder(ResolveRightDockHost());
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

            _logger?.LogDebug(
                "[NAV-RESIZE] RightDockPanel_SizeChanged - Width={Width}, Height={Height}, SelectedTab={SelectedTab}, ApplyingWidthGuard={ApplyingWidthGuard}",
                _rightDockPanel.Width,
                _rightDockPanel.Height,
                _rightDockTabs?.SelectedTab?.Name ?? "<none>",
                _isApplyingRightDockWidth);

            if (_rightDockJarvisSplitContainer != null && !_rightDockJarvisSplitContainer.IsDisposed)
            {
                ApplyJarvisTopInset(_rightDockJarvisSplitContainer, _rightDockJarvisPanel?.MinimumSize.Height ?? _rightDockJarvisSplitContainer.Panel2MinSize, preserveStoredInset: true);
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
                _logger?.LogDebug(
                    "[NAV-RESIZE] ApplyRightDockWidthForSelectedTab - Force={Force}, TargetWidth={TargetWidth}, AppliedWidth={AppliedWidth}, SelectedTab={SelectedTab}",
                    force,
                    targetWidth,
                    _rightDockPanel.Width,
                    _rightDockTabs.SelectedTab.Name);
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
                return _activityLogExpandedWidth > 0 ? _activityLogExpandedWidth : taggedWidth;
            }

            if (string.Equals(selectedTab.Name, RightDockPanelFactory.JarvisTabName, StringComparison.OrdinalIgnoreCase))
            {
                return _jarvisExpandedWidth > 0 ? _jarvisExpandedWidth : taggedWidth;
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

            EnsureRightDockArtifactsDockOrder(newHost);
        }
    }
}
