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

        private void EnsurePanelNavigatorInitialized()
        {
            if (_panelNavigator != null) return;

            _logger?.LogDebug("[NAV] Creating PanelNavigationService");

            var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<ILogger<PanelNavigationService>>(_serviceProvider!);

            var navigator = new PanelNavigationService(
                this,
                _serviceProvider!,
                navLogger ?? NullLogger<PanelNavigationService>.Instance);

            // Wire TabbedMDI if already initialized so panels appear as proper tabs
            // rather than raw floating MDI children (defensive: covers the lazy-init path).
            if (_tabbedMdi != null)
            {
                navigator.SetTabbedManager(_tabbedMdi!);
                _logger?.LogDebug("[NAV] EnsurePanelNavigatorInitialized: TabbedMDIManager wired to new navigator");
            }

            _panelNavigator = navigator;
        }

        public void ShowPanel<TPanel>(string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            if (typeof(TPanel) == typeof(JARVISChatUserControl))
            {
                ShowJarvisInRightDock();
                return;
            }

            EnsurePanelNavigatorInitialized();
            _panelNavigator?.ShowPanel<TPanel>(panelName, style, allowFloating);
        }

        public void ShowForm<TForm>(string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
            where TForm : Form
        {
            EnsurePanelNavigatorInitialized();
            _panelNavigator?.ShowForm<TForm>(panelName, style, allowFloating);
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

                if (panelType == typeof(JARVISChatUserControl))
                {
                    return ShowJarvisInRightDock();
                }

                if (_panelNavigator == null)
                {
                    _logger?.LogWarning("[NAV] ShowPanel(Type) failed because panel navigator is unavailable");
                    return false;
                }

                _panelNavigator.ShowPanel(panelType, panelName, style, allowFloating);

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

                if (_rightDockJarvisPanel is IParameterizedPanel parameterizedPanel && parameters is not null)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                var jarvisTab = FindRightDockTab("RightDockTab_JARVIS");
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

                if (_rightDockPanel != null)
                {
                    _rightDockPanel.Visible = true;
                    _rightDockPanel.BringToFront();
                }

                _rightDockTabs?.BringToFront();
                _rightDockTabs?.Focus();
                QueueRightDockJarvisInitialization();

                _logger?.LogDebug(
                    "[NAV] JARVIS Chat shown — panel Visible={Visible}, Bounds={Bounds}, Parent={Parent}",
                    _rightDockPanel?.Visible,
                    _rightDockPanel?.Bounds,
                    _rightDockPanel?.Parent?.Name ?? "<no parent>");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] Failed to show JARVIS in right dock panel");
                return false;
            }
        }

        private void QueueRightDockJarvisInitialization()
        {
            void queueCore()
            {
                if (_rightDockJarvisPanel is not IAsyncInitializable asyncInitializable || _rightDockJarvisPanel.IsDisposed)
                {
                    return;
                }

                if (_rightDockJarvisInitializationTask != null && !_rightDockJarvisInitializationTask.IsCompleted)
                {
                    return;
                }

                _rightDockJarvisInitializationTask = InitializeRightDockJarvisPanelAsync(asyncInitializable);
            }

            if (IsDisposed || Disposing)
            {
                return;
            }

            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke((MethodInvoker)queueCore);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Fall back to direct invocation while the handle is being recreated.
                }
            }

            queueCore();
        }

        private async Task InitializeRightDockJarvisPanelAsync(IAsyncInitializable asyncInitializable)
        {
            try
            {
                await asyncInitializable.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
                _logger?.LogDebug("[NAV] JARVIS right dock runtime initialized");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] Failed to initialize JARVIS right dock runtime");
            }
        }

        private bool EnsureRightDockPanelInitialized()
        {
            if (_rightDockPanel != null
                && !_rightDockPanel.IsDisposed
                && _rightDockTabs != null
                && !_rightDockTabs.IsDisposed
                && _rightDockJarvisPanel != null
                && !_rightDockJarvisPanel.IsDisposed)
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
                _rightDockJarvisPanel == null ? "null" : (_rightDockJarvisPanel.IsDisposed ? "disposed" : "ok"));

            if (_serviceProvider == null)
            {
                _logger?.LogWarning("[NAV] Cannot initialize right dock panel because service provider is unavailable");
                return false;
            }

            // Capture the previous panel reference BEFORE replacing the field, so it can be
            // removed from the host. Without this, the stub persists as a second DockStyle.Right
            // panel that crowds out the real JARVIS panel.
            var oldPanel = _rightDockPanel;

            var (rightDockPanel, rightDockTabs, _, jarvisChatPanel) = RightDockPanelFactory.CreateRightDockPanel(
                this,
                _serviceProvider,
                _logger);

            _rightDockPanel = rightDockPanel;
            _rightDockTabs = rightDockTabs;
            _rightDockJarvisPanel = jarvisChatPanel;
            _rightDockJarvisInitializationTask = null;

            var host = (Control)this;
            var redrawSuspended = TrySuspendRedraw("RIGHT_DOCK_INIT");
            host.SuspendLayout();

            try
            {
                _rightDockPanel.SuspendLayout();

                try
                {
                    // Remove the old temporary panel that was created by InitializeLayoutComponents.
                    // If we don't do this, both the stub and the real panel sit in host.Controls with
                    // DockStyle.Right and steal ~740 px of horizontal space from the MDI client area.
                    if (oldPanel != null && !oldPanel.IsDisposed && !ReferenceEquals(oldPanel, rightDockPanel))
                    {
                        oldPanel.Parent?.Controls.Remove(oldPanel);
                        _logger?.LogDebug("[NAV] Removed previous right dock panel '{PanelName}' before adding real panel", oldPanel.Name);
                    }

                    // Add the real panel to the form so the native WinForms MDI client can continue to own
                    // the remaining DockStyle.Fill area.
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
                    if (_rightDockPanel.Width < 350) _rightDockPanel.Width = 500;
                    _rightDockPanel.Visible = true;  // explicit: sidebar is always shown once initialized

                    EnsureRightDockSplitter(host);
                    EnsureJarvisAutoHideStrip(host);

                    _logger?.LogDebug(
                        "[NAV] Right dock panel configured — Dock={Dock}, Width={Width}, Host={Host}, ControlsInHost={ControlCount}",
                        _rightDockPanel.Dock, _rightDockPanel.Width,
                        host.Name, host.Controls.Count);

                    _rightDockPanel.BringToFront();
                }
                finally
                {
                    _rightDockPanel.ResumeLayout(false);
                }
            }
            finally
            {
                host.ResumeLayout(true);
                ResumeRedraw(redrawSuspended, "RIGHT_DOCK_INIT");
            }

            RequestMdiConstrain("EnsureRightDockPanelInitialized", force: true);

            _logger?.LogInformation(
                "[NAV] Right dock panel initialized — Visible={Visible}, Bounds={Bounds}, Parent={Parent}, Tabs={TabCount}",
                _rightDockPanel.Visible, _rightDockPanel.Bounds,
                _rightDockPanel.Parent?.Name ?? "<no parent>",
                _rightDockTabs?.TabPages.Count ?? 0);

            // JARVIS initializes its native Syncfusion assistant panel on first use.

            return true;
        }

        /// <summary>Creates the resize splitter for the right dock panel, if not already present.</summary>
        private void EnsureRightDockSplitter(Control host)
        {
            if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
                return;

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
                return;

            var themeName = SfSkinManager.ApplicationVisualTheme
                            ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            // Thin vertical strip — always occupies 22 px on the right edge of the host.
            _jarvisAutoHideStrip = new Panel
            {
                Name = "JarvisAutoHideStrip",
                Dock = DockStyle.Right,
                Width = 22,
                Padding = Padding.Empty,
            };
            SfSkinManager.SetVisualStyle(_jarvisAutoHideStrip, themeName);

            // Collapse / expand button — sits at the very top of the strip.
            _jarvisAutoHideButton = new Button
            {
                Name = "JarvisAutoHideButton",
                Text = "◄",         // ◄ = panel is open (click to close)
                Dock = DockStyle.Top,
                Height = 56,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 8f),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = "Toggle JARVIS sidebar",
                AccessibleDescription = "Collapse or expand the JARVIS / Activity Log sidebar",
            };
            _jarvisAutoHideButton.FlatAppearance.BorderSize = 0;
            _jarvisAutoHideButton.Click += (_, _) => ToggleJarvisAutoHide();

            _jarvisAutoHideStrip.Controls.Add(_jarvisAutoHideButton);
            host.Controls.Add(_jarvisAutoHideStrip);

            _logger?.LogDebug("[NAV] JARVIS auto-hide strip created");
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

            var isCurrentlyVisible = _rightDockPanel.Visible;

            if (isCurrentlyVisible)
            {
                // Save current expanded width before collapsing.
                if (_rightDockPanel.Width > 0)
                    _jarvisExpandedWidth = _rightDockPanel.Width;

                _rightDockPanel.Visible = false;
                if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
                    _rightDockSplitter.Visible = false;

                if (_jarvisAutoHideButton != null)
                    _jarvisAutoHideButton.Text = "►";   // ► = panel is closed (click to open)

                _logger?.LogDebug("[NAV] JARVIS sidebar collapsed — width saved: {Width}px", _jarvisExpandedWidth);
            }
            else
            {
                // Restore the previously saved width.
                if (_rightDockPanel.Width < 350)
                    _rightDockPanel.Width = _jarvisExpandedWidth;

                _rightDockPanel.Visible = true;
                if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed)
                    _rightDockSplitter.Visible = true;

                if (_jarvisAutoHideButton != null)
                    _jarvisAutoHideButton.Text = "◄";   // ◄ = panel is open (click to close)

                _rightDockPanel.BringToFront();
                _logger?.LogDebug("[NAV] JARVIS sidebar expanded — width: {Width}px", _rightDockPanel.Width);
            }

            // Re-constrain the MdiClient so panel content fills the newly available space.
            PerformLayout();
            RequestMdiConstrain("ToggleJarvisAutoHide", force: true);
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
    }
}
