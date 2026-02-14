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
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PanelNavigationService> _logger;

        private readonly Dictionary<string, UserControl> _cachedPanels = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<UserControl> _registeredPanels = new();
        private readonly Dictionary<string, (DockingStyle PreferredStyle, bool AllowFloating)> _panelPreferences = new(StringComparer.OrdinalIgnoreCase);
        private string? _activePanelName;

        public event EventHandler<PanelActivatedEventArgs>? PanelActivated;

        public PanelNavigationService(
            DockingManager dockingManager,
            Control parentControl,
            IServiceProvider serviceProvider,
            ILogger<PanelNavigationService> logger)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
            _contentContainer = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            EnsureContainerVisible();
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
                    try
                    {
                        _dockingManager.SetDockVisibility(panel, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to hide docked panel {PanelName}; using Visible=false fallback", panelName);
                        panel.Visible = false;
                    }
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

            if (panel.Parent == null || panel.Parent.IsDisposed)
            {
                _contentContainer.Controls.Add(panel);
            }

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
                EnsurePreferredDockSize(panel, normalizedStyle);
            }

            try
            {
                _dockingManager.SetDockVisibility(panel, true);
            }
            catch
            {
                panel.Visible = true;
            }

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

        private void RegisterAndDockPanel(UserControl panel, string panelName, DockingStyle preferredStyle, bool allowFloating)
        {
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
                _dockingManager.DockControl(panel, dockingHost, resolvedStyle, size);
                _registeredPanels.Add(panel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed docking panel {PanelName} with style {Style}; retrying with Right dock on host container",
                    panelName,
                    preferredStyle);

                try
                {
                    _dockingManager.DockControl(panel, _contentContainer, DockingStyle.Right, GetPreferredDockSize(DockingStyle.Right, _contentContainer, panel));
                    _registeredPanels.Add(panel);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback docking failed for panel {PanelName}", panelName);
                }
            }
        }

        private void EnsurePreferredDockSize(UserControl panel, DockingStyle preferredStyle)
        {
            try
            {
                var dockingHost = ResolveDockHost(panel, preferredStyle, out var resolvedStyle);
                var targetSize = GetPreferredDockSize(resolvedStyle, _contentContainer, panel);
                _dockingManager.DockControl(panel, dockingHost, resolvedStyle, targetSize);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Failed to re-apply preferred dock size for panel {PanelName}",
                    panel.Name);
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
            var baseSize = style switch
            {
                DockingStyle.Left => 320,
                DockingStyle.Right => 380,
                DockingStyle.Top => 280,
                DockingStyle.Bottom => 320,
                _ => 320
            };

            var hostExtent = style switch
            {
                DockingStyle.Left or DockingStyle.Right => host.ClientSize.Width,
                DockingStyle.Top or DockingStyle.Bottom => host.ClientSize.Height,
                _ => 0
            };

            var panelDesignedSize = GetDesignedDockSize(style, panel);
            var desiredSize = Math.Max(baseSize, panelDesignedSize);

            if (hostExtent <= 0)
            {
                return desiredSize;
            }

            var maxRatio = style is DockingStyle.Left or DockingStyle.Right ? 0.90 : 0.85;
            var minimumSize = style is DockingStyle.Left or DockingStyle.Right ? 260 : 220;
            var cappedSize = Math.Max(minimumSize, (int)(hostExtent * maxRatio));

            return Math.Min(desiredSize, cappedSize);
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
