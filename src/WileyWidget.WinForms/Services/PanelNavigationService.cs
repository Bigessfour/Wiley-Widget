using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Interface for panels that can be initialized with parameters.
    /// </summary>
    public interface IParameterizedPanel
    {
        void InitializeWithParameters(object parameters);
    }

    /// <summary>
    /// Simple navigation service that shows one panel/form at a time in a fixed container.
    /// </summary>
    public interface IPanelNavigationService
    {
        void ShowPanel<TPanel>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl;

        void ShowPanel<TPanel>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl;

        void ShowForm<TForm>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form;

        void ShowForm<TForm>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form;

        bool HidePanel(string panelName);

        Task AddPanelAsync(UserControl panel, string panelName, DockingStyle preferredStyle = DockingStyle.Right, bool allowFloating = true);

        string? GetActivePanelName();

        event EventHandler<PanelActivatedEventArgs>? PanelActivated;
    }

    public sealed class PanelNavigationService : IPanelNavigationService, IDisposable
    {
        private readonly DockingManager _dockingManager;
        private readonly Control _contentContainer;
        private readonly Control? _centralDocumentContainer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PanelNavigationService> _logger;

        private readonly Dictionary<string, UserControl> _cachedPanels = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<UserControl> _registeredPanels = new();
        private readonly Dictionary<string, (DockingStyle PreferredStyle, bool AllowFloating)> _panelPreferences = new(StringComparer.OrdinalIgnoreCase);
        private string? _activePanelName;
        private bool _disableDockingMutations;

        public event EventHandler<PanelActivatedEventArgs>? PanelActivated;

        public PanelNavigationService(
            DockingManager dockingManager,
            Control parentControl,
            IServiceProvider serviceProvider,
            ILogger<PanelNavigationService> logger)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
            _contentContainer = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _centralDocumentContainer = ResolveCentralDocumentContainer(parentControl);
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Enable proper Syncfusion docking - exception handling in RegisterAndDockPanel() and EnsurePreferredDockSize()
            // will automatically fall back to safe mode if Syncfusion encounters instability.
            _disableDockingMutations = false;

            EnsureContainerVisible();
            if (_centralDocumentContainer != null)
            {
                _logger.LogDebug("PanelNavigationService detected central document container '{ContainerName}'", _centralDocumentContainer.Name);
            }
            _logger.LogInformation("PanelNavigationService initialized with full Syncfusion DockingManager support");
            _logger.LogDebug("PanelNavigationService initialized with DockingManager navigation");
        }

        public void ShowPanel<TPanel>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            ShowPanel<TPanel>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowPanel<TPanel>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));
            }

            ExecuteOnUiThread(() =>
            {
                if (!_cachedPanels.TryGetValue(panelName, out var panel) || panel.IsDisposed)
                {
                    panel = ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);
                    _cachedPanels[panelName] = panel;
                }

                if (parameters is not null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                ShowInDockingManager(panel, panelName, preferredStyle, allowFloating);
            });
        }

        public void ShowForm<TForm>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form
        {
            ShowForm<TForm>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowForm<TForm>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));
            }

            ExecuteOnUiThread(() =>
            {
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel) && !existingPanel.IsDisposed)
                {
                    ShowInDockingManager(existingPanel, panelName, preferredStyle, allowFloating);
                    return;
                }

                var form = ActivatorUtilities.CreateInstance<TForm>(_serviceProvider);
                if (parameters is not null && form is IParameterizedPanel parameterizedForm)
                {
                    parameterizedForm.InitializeWithParameters(parameters);
                }

                var host = new FormHostPanel();
                host.HostForm(form);
                _cachedPanels[panelName] = host;

                ShowInDockingManager(host, panelName, preferredStyle, allowFloating);
            });
        }

        public async Task AddPanelAsync(UserControl panel, string panelName, DockingStyle preferredStyle = DockingStyle.Right, bool allowFloating = true)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            ExecuteOnUiThread(() =>
            {
                _cachedPanels[panelName] = panel;
                ShowInDockingManager(panel, panelName, preferredStyle, allowFloating);
            });

            await Task.CompletedTask.ConfigureAwait(true);
        }

        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));
            }

            if (!_cachedPanels.TryGetValue(panelName, out var panel) || panel.IsDisposed)
            {
                return false;
            }

            ExecuteOnUiThread(() =>
            {
                if (_registeredPanels.Contains(panel))
                {
                    TrySetDockVisibilitySafe(panel, false, panelName, "HidePanel");
                }
                else
                {
                    panel.Visible = false;
                }

                if (string.Equals(_activePanelName, panelName, StringComparison.Ordinal))
                {
                    _activePanelName = null;
                }
            });

            return true;
        }

        public string? GetActivePanelName() => _activePanelName;

        public void Dispose()
        {
            foreach (var panel in _cachedPanels.Values)
            {
                try
                {
                    panel.Dispose();
                }
                catch
                {
                }
            }

            _cachedPanels.Clear();
            _registeredPanels.Clear();
            _panelPreferences.Clear();
        }

        private void ShowInDockingManager(UserControl panel, string panelName, DockingStyle preferredStyle, bool allowFloating)
        {
            EnsureContainerVisible();

            panel.Name = panelName.Replace(" ", string.Empty, StringComparison.Ordinal);

            if (panel.Parent != null &&
                !ReferenceEquals(panel.Parent, _contentContainer) &&
                (_centralDocumentContainer == null || !ReferenceEquals(panel.Parent, _centralDocumentContainer)))
            {
                panel.Parent.Controls.Remove(panel);
            }

            panel.Margin = Padding.Empty;

            var normalizedStyle = NormalizeDockingStyle(preferredStyle);
            var shouldRedock = !_registeredPanels.Contains(panel) ||
                !_panelPreferences.TryGetValue(panelName, out var priorPreference) ||
                priorPreference.PreferredStyle != normalizedStyle ||
                priorPreference.AllowFloating != allowFloating;

            if (shouldRedock)
            {
                RegisterAndDockPanel(panel, panelName, normalizedStyle, allowFloating);
            }
            else
            {
                EnsurePreferredDockSize(panel, panelName, normalizedStyle, allowFloating);
            }

            if (_registeredPanels.Contains(panel))
            {
                TrySetDockVisibilitySafe(panel, true, panelName, "ShowInDockingManager");
            }
            else
            {
                panel.Visible = true;
            }

            ResetScrollableViewport(panel);
            EnsureControlHierarchyVisible(panel);
            panel.Visible = true;
            panel.BringToFront();

            _panelPreferences[panelName] = (normalizedStyle, allowFloating);
            _activePanelName = panelName;
            PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, panel.GetType()));

            _ = InitializeIfAsync(panel, panelName);
            _logger.LogInformation("Displayed panel {PanelName} via DockingManager (Style={Style}, AllowFloating={AllowFloating})",
                panelName,
                normalizedStyle,
                allowFloating);
        }

        private static void ResetScrollableViewport(Control root)
        {
            if (root == null || root.IsDisposed)
            {
                return;
            }

            if (root is ScrollableControl rootScrollable && rootScrollable.AutoScroll)
            {
                try
                {
                    rootScrollable.AutoScrollPosition = System.Drawing.Point.Empty;
                }
                catch
                {
                }
            }

            foreach (Control child in root.Controls)
            {
                if (child is ScrollableControl scrollable && scrollable.AutoScroll)
                {
                    try
                    {
                        scrollable.AutoScrollPosition = System.Drawing.Point.Empty;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void RegisterAndDockPanel(UserControl panel, string panelName, DockingStyle preferredStyle, bool allowFloating)
        {
            if (_disableDockingMutations)
            {
                AttachPanelWithoutDocking(panel, panelName);
                return;
            }

            try
            {
                _dockingManager.SetEnableDocking(panel, true);
                _dockingManager.SetDockLabel(panel, panelName);

                if (!allowFloating)
                {
                    try
                    {
                        _dockingManager.SetAutoHideMode(panel, false);
                    }
                    catch
                    {
                    }
                }

                var dockingHost = ResolveDockHost(panel, preferredStyle, out var resolvedStyle);
                var size = GetPreferredDockSize(resolvedStyle, _contentContainer, panel);
                ApplyDockMinimumSize(panel, resolvedStyle);

                // CRITICAL: Ensure panel has at least one child before docking to prevent DockHost paint crashes
                if (panel.Controls.Count == 0)
                {
                    var placeholder = new Label
                    {
                        Text = "",
                        Dock = DockStyle.Fill,
                        AutoSize = false
                    };
                    panel.Controls.Add(placeholder);
                    _logger.LogDebug("Added placeholder control to panel {PanelName} before docking", panel.Name);
                }

                // CRITICAL: Suspend layout on host control to prevent paint during docking operations
                var hostControl = _dockingManager.HostControl;
                hostControl?.SuspendLayout();
                try
                {
                    _dockingManager.DockControl(panel, dockingHost, resolvedStyle, size);
                }
                finally
                {
                    hostControl?.ResumeLayout(performLayout: false);
                }
                _registeredPanels.Add(panel);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _disableDockingMutations = true;
                _logger.LogError(ex,
                    "[CRITICAL] DockingManager entered unstable state while docking panel {PanelName} (style={Style}). Safe mode activated. Stack: {Stack}. " +
                    "This usually indicates docking system not ready or corrupted layout state. NavService will use fallback host.",
                    panelName,
                    preferredStyle,
                    ex.StackTrace ?? "none");

                _registeredPanels.Remove(panel);
                AttachPanelWithoutDocking(panel, panelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed docking panel {PanelName} with style {Style}; retrying with Tabbed dock on central container",
                    panelName,
                    preferredStyle);

                try
                {
                    var fallbackHost = _centralDocumentContainer ?? _contentContainer;
                    var fallbackStyle = ReferenceEquals(fallbackHost, _contentContainer) ? DockingStyle.Right : DockingStyle.Tabbed;

                    // CRITICAL: Suspend layout on host control to prevent paint during fallback docking
                    var hostControl = _dockingManager.HostControl;
                    hostControl?.SuspendLayout();
                    try
                    {
                        _dockingManager.DockControl(panel, fallbackHost, fallbackStyle, GetPreferredDockSize(fallbackStyle, fallbackHost, panel));
                    }
                    finally
                    {
                        hostControl?.ResumeLayout(performLayout: false);
                    }
                    _registeredPanels.Add(panel);
                }
                catch (ArgumentOutOfRangeException fallbackAoorEx)
                {
                    _disableDockingMutations = true;
                    _logger.LogError(fallbackAoorEx,
                        "[CRITICAL] Fallback docking entered unstable state for panel {PanelName}. Safe mode activated. Stack: {Stack}. " +
                        "Docking system appears fundamentally unstable - using host fallback for all panels.",
                        panelName,
                        fallbackAoorEx.StackTrace ?? "none");

                    _registeredPanels.Remove(panel);
                    AttachPanelWithoutDocking(panel, panelName);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback docking failed for panel {PanelName}", panelName);

                    AttachPanelWithoutDocking(panel, panelName);
                }
            }
        }

        private void EnsurePreferredDockSize(UserControl panel, string panelName, DockingStyle preferredStyle, bool allowFloating)
        {
            if (_disableDockingMutations || !_registeredPanels.Contains(panel))
            {
                AttachPanelWithoutDocking(panel, panelName);
                return;
            }

            try
            {
                var dockingHost = ResolveDockHost(panel, preferredStyle, out var resolvedStyle);
                var targetSize = GetPreferredDockSize(resolvedStyle, _contentContainer, panel);
                ApplyDockMinimumSize(panel, resolvedStyle);

                // CRITICAL: Suspend layout on host control to prevent paint during re-docking
                var hostControl = _dockingManager.HostControl;
                hostControl?.SuspendLayout();
                try
                {
                    _dockingManager.DockControl(panel, dockingHost, resolvedStyle, targetSize);
                }
                finally
                {
                    hostControl?.ResumeLayout(performLayout: false);
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _disableDockingMutations = true;
                _logger.LogError(ex,
                    "[CRITICAL] DockingManager became unstable while resizing panel {PanelName} (style={Style}). Safe mode activated. Stack: {Stack}. " +
                    "This indicates DockingManager.GetControlsSequence() or similar failed - likely corrupted internal state.",
                    panelName,
                    preferredStyle,
                    ex.StackTrace ?? "none");

                _registeredPanels.Remove(panel);
                AttachPanelWithoutDocking(panel, panelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to re-apply preferred dock size for panel {PanelName}; forcing full re-dock recovery",
                    panelName);

                try
                {
                    _registeredPanels.Remove(panel);
                    RegisterAndDockPanel(panel, panelName, preferredStyle, allowFloating);
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx,
                        "Failed re-dock recovery for panel {PanelName}",
                        panelName);
                }
            }
        }

        private Control ResolveDockHost(UserControl panel, DockingStyle style, out DockingStyle resolvedStyle)
        {
            if (style == DockingStyle.Tabbed &&
                !string.IsNullOrWhiteSpace(_activePanelName) &&
                _cachedPanels.TryGetValue(_activePanelName, out var activePanel) &&
                !ReferenceEquals(activePanel, panel) &&
                !activePanel.IsDisposed)
            {
                resolvedStyle = DockingStyle.Tabbed;
                return activePanel;
            }

            if (style == DockingStyle.Fill)
            {
                if (_centralDocumentContainer != null && !_centralDocumentContainer.IsDisposed)
                {
                    resolvedStyle = DockingStyle.Tabbed;
                    return _centralDocumentContainer;
                }

                resolvedStyle = DockingStyle.Right;
                return _contentContainer;
            }

            resolvedStyle = style;
            return _contentContainer;
        }

        private static DockingStyle NormalizeDockingStyle(DockingStyle preferredStyle)
        {
            return preferredStyle switch
            {
                DockingStyle.Left => DockingStyle.Left,
                DockingStyle.Right => DockingStyle.Right,
                DockingStyle.Top => DockingStyle.Top,
                DockingStyle.Bottom => DockingStyle.Bottom,
                DockingStyle.Fill => DockingStyle.Fill,
                DockingStyle.Tabbed => DockingStyle.Tabbed,
                _ => DockingStyle.Right
            };
        }

        private static int GetPreferredDockSize(DockingStyle style, Control host, Control panel)
        {
            var hostExtent = style switch
            {
                DockingStyle.Left or DockingStyle.Right => host.ClientSize.Width,
                DockingStyle.Top or DockingStyle.Bottom => host.ClientSize.Height,
                _ => 0
            };

            var minimumSize = style is DockingStyle.Left or DockingStyle.Right ? 260 : 220;
            var preferredRatio = style switch
            {
                DockingStyle.Left => 0.28,
                DockingStyle.Right => 0.32,
                DockingStyle.Top => 0.30,
                DockingStyle.Bottom => 0.34,
                _ => 0.30
            };

            var panelDesignedSize = GetDesignedDockSize(style, panel);
            var hostPreferredSize = hostExtent > 0
                ? Math.Max(minimumSize, (int)Math.Round(hostExtent * preferredRatio))
                : minimumSize;
            var desiredSize = Math.Max(hostPreferredSize, panelDesignedSize);

            if (hostExtent <= 0)
            {
                return desiredSize;
            }

            var maxRatio = style is DockingStyle.Left or DockingStyle.Right ? 0.75 : 0.70;
            var cappedSize = Math.Max(minimumSize, (int)(hostExtent * maxRatio));

            return Math.Min(desiredSize, cappedSize);
        }

        private static Control? ResolveCentralDocumentContainer(Control root)
        {
            if (root == null || root.IsDisposed)
            {
                return null;
            }

            if (string.Equals(root.Name, "CentralDocumentPanel", StringComparison.Ordinal))
            {
                return root;
            }

            foreach (Control child in root.Controls)
            {
                var resolved = ResolveCentralDocumentContainer(child);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private void ApplyDockMinimumSize(Control panel, DockingStyle style)
        {
            try
            {
                var minimum = style switch
                {
                    DockingStyle.Left => new System.Drawing.Size(260, 0),
                    DockingStyle.Right => new System.Drawing.Size(300, 0),
                    DockingStyle.Top => new System.Drawing.Size(0, 220),
                    DockingStyle.Bottom => new System.Drawing.Size(0, 240),
                    _ => System.Drawing.Size.Empty
                };

                if (minimum == System.Drawing.Size.Empty)
                {
                    return;
                }

                _dockingManager.SetControlMinimumSize(panel, minimum);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply docking minimum size for panel {PanelName}", panel.Name);
            }
        }

        private static int GetDesignedDockSize(DockingStyle style, Control panel)
        {
            if (panel == null || panel.IsDisposed)
            {
                return 0;
            }

            var useWidth = style is DockingStyle.Left or DockingStyle.Right;

            // First, try to use PreferredDockSize extension if panel is a UserControl
            if (panel is UserControl userControlPanel)
            {
                var preferredDockSize = userControlPanel.PreferredDockSize();
                var extensionSize = useWidth ? preferredDockSize.Width : preferredDockSize.Height;
                if (extensionSize > 0)
                {
                    return extensionSize;
                }
            }

            // Fallback to panel's own size properties
            var panelSize = useWidth ? panel.Size.Width : panel.Size.Height;
            var panelMinimum = useWidth ? panel.MinimumSize.Width : panel.MinimumSize.Height;
            var panelPreferred = useWidth ? panel.PreferredSize.Width : panel.PreferredSize.Height;

            var designedSize = Math.Max(panelSize, Math.Max(panelMinimum, panelPreferred));

            if (panel is FormHostPanel formHostPanel && formHostPanel.HostedForm != null)
            {
                var hostedForm = formHostPanel.HostedForm;
                var formSize = useWidth ? hostedForm.Size.Width : hostedForm.Size.Height;
                var formMinimum = useWidth ? hostedForm.MinimumSize.Width : hostedForm.MinimumSize.Height;
                var formPreferred = useWidth ? hostedForm.PreferredSize.Width : hostedForm.PreferredSize.Height;
                designedSize = Math.Max(designedSize, Math.Max(formSize, Math.Max(formMinimum, formPreferred)));
            }

            return designedSize;
        }

        private bool TrySetDockVisibilitySafe(Control panel, bool visible, string panelName, string operation)
        {
            if (panel == null || panel.IsDisposed)
            {
                return false;
            }

            if (_disableDockingMutations || panel is not UserControl userControlPanel || !_registeredPanels.Contains(userControlPanel))
            {
                panel.Visible = visible;
                return false;
            }

            var hostControl = _dockingManager.HostControl;
            if (hostControl == null || hostControl.IsDisposed || !hostControl.IsHandleCreated)
            {
                panel.Visible = visible;
                return false;
            }

            if (panel.Parent == null || panel.Parent.IsDisposed)
            {
                panel.Visible = visible;
                return false;
            }

            try
            {
                // CRITICAL: Suspend layout during visibility changes to prevent paint crashes
                panel.Parent?.SuspendLayout();
                try
                {
                    _dockingManager.SetDockVisibility(panel, visible);
                    panel.Visible = visible;
                    return true;
                }
                finally
                {
                    panel.Parent?.ResumeLayout(performLayout: false);
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _disableDockingMutations = true;
                _logger.LogWarning(ex,
                    "SetDockVisibility failed with unstable docking state for panel {PanelName} during {Operation}; disabling docking mutations and applying Visible fallback",
                    panelName,
                    operation);
                panel.Visible = visible;
                return false;
            }
            catch (DockingManagerException ex)
            {
                _logger.LogDebug(ex,
                    "SetDockVisibility failed for panel {PanelName} during {Operation}; applying Visible fallback",
                    panelName,
                    operation);
                panel.Visible = visible;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Unexpected SetDockVisibility failure for panel {PanelName} during {Operation}; applying Visible fallback",
                    panelName,
                    operation);
                panel.Visible = visible;
                return false;
            }
        }

        private void AttachPanelWithoutDocking(UserControl panel, string panelName)
        {
            var fallbackParent = _centralDocumentContainer ?? _contentContainer;

            if (panel.Parent != null && !ReferenceEquals(panel.Parent, fallbackParent))
            {
                panel.Parent.Controls.Remove(panel);
            }

            if (!ReferenceEquals(panel.Parent, fallbackParent))
            {
                fallbackParent.Controls.Add(panel);
            }

            HideOtherFallbackPanels(fallbackParent, panel);

            panel.Dock = DockStyle.Fill;
            EnsureControlHierarchyVisible(panel);
            panel.Visible = true;
            panel.BringToFront();

            _logger.LogWarning("Panel {PanelName} displayed via non-docking fallback host due to docking instability", panelName);
        }

        private void HideOtherFallbackPanels(Control host, Control activePanel)
        {
            foreach (Control child in host.Controls)
            {
                if (ReferenceEquals(child, activePanel) || child.IsDisposed)
                {
                    continue;
                }

                if (child is UserControl && _cachedPanels.ContainsValue((UserControl)child))
                {
                    child.Visible = false;
                }
            }
        }

        /// <summary>
        /// Ensures the content container and its entire parent chain are visible.
        /// Fixes issue where panels are added but container hierarchy is hidden.
        /// </summary>
        private void EnsureContainerVisible()
        {
            var current = _contentContainer;
            while (current != null)
            {
                if (!current.Visible)
                {
                    _logger.LogWarning("Container '{Name}' was hidden - making visible", current.Name);
                    current.Visible = true;
                }
                current = current.Parent;
            }
        }

        private static void EnsureControlHierarchyVisible(Control control)
        {
            var current = control;
            while (current != null && !current.IsDisposed)
            {
                current.Visible = true;
                current = current.Parent;
            }
        }

        private async Task InitializeIfAsync(UserControl panel, string panelName)
        {
            if (panel is not IAsyncInitializable asyncInitializable)
            {
                return;
            }

            try
            {
                await asyncInitializable.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Async initialization failed for panel {PanelName}", panelName);
            }
        }

        private void ExecuteOnUiThread(System.Action action)
        {
            if (_contentContainer.IsDisposed)
            {
                return;
            }

            if (_contentContainer.InvokeRequired)
            {
                _contentContainer.Invoke(action);
                return;
            }

            action();
        }
    }

    public class PanelActivatedEventArgs : EventArgs
    {
        public string PanelName { get; set; }
        public Type PanelType { get; set; }

        public PanelActivatedEventArgs(string panelName, Type panelType)
        {
            PanelName = panelName;
            PanelType = panelType;
        }
    }
}
