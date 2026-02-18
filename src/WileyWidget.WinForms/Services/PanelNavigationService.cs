using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Diagnostics;

namespace WileyWidget.WinForms.Services
{
    public interface IParameterizedPanel
    {
        void InitializeWithParameters(object parameters);
    }

    public interface IPanelNavigationService : IDisposable
    {
        void ShowPanel<TPanel>(string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl;

        void ShowPanel<TPanel>(string panelName, object? parameters, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl;

        void ShowForm<TForm>(string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TForm : Form;

        void ShowForm<TForm>(string panelName, object? parameters, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TForm : Form;

        bool HidePanel(string panelName);
        Task AddPanelAsync(UserControl panel, string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true);
        string? GetActivePanelName();
        void SetTabbedManager(Syncfusion.Windows.Forms.Tools.TabbedMDIManager tabbedMdi);
        event EventHandler<PanelActivatedEventArgs>? PanelActivated;
    }

    public sealed class PanelNavigationService : IPanelNavigationService
    {
        private readonly Form _owner;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PanelNavigationService> _logger;
        private readonly Dictionary<string, Form> _hosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Control> _cachedPanels = new(StringComparer.OrdinalIgnoreCase);
        private string? _activePanelName;
        private readonly bool _useMDI;
        private Syncfusion.Windows.Forms.Tools.TabbedMDIManager? _tabbedMdi;

        public bool IsMdiEnabled => _useMDI;

        public event EventHandler<PanelActivatedEventArgs>? PanelActivated;

        public PanelNavigationService(Form owner, IServiceProvider serviceProvider, ILogger<PanelNavigationService> logger)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Check if owner is MDI container
            _useMDI = owner.IsMdiContainer;
            _logger.LogDebug("[PANEL_NAV] PanelNavigationService initialized - MDI mode: {UseMDI}", _useMDI);
        }

        /// <summary>
        /// Sets the TabbedMDIManager for pure tabbed layout navigation.
        /// Called during MainTabbedLayoutFactory initialization.
        /// </summary>
        public void SetTabbedManager(Syncfusion.Windows.Forms.Tools.TabbedMDIManager tabbedMdi)
        {
            _tabbedMdi = tabbedMdi ?? throw new ArgumentNullException(nameof(tabbedMdi));
            _logger.LogDebug("[PANEL_NAV] TabbedMDIManager set for tab-based navigation");
        }

        public void ShowPanel<TPanel>(string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            ShowPanel<TPanel>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowPanel<TPanel>(string panelName, object? parameters, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            ExecuteOnUiThread(() =>
            {
                if (!_cachedPanels.TryGetValue(panelName, out var panel) || panel.IsDisposed)
                {
                    panel = ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);
                    _cachedPanels[panelName] = panel;
                    CachePanelAliases(panelName, panel);
                }

                if (parameters is not null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                ShowFloating(panel, panelName);
            });
        }

        public void ShowForm<TForm>(string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TForm : Form
        {
            ShowForm<TForm>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowForm<TForm>(string panelName, object? parameters, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TForm : Form
        {
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            ExecuteOnUiThread(() =>
            {
                if (_cachedPanels.TryGetValue(panelName, out var existingHost) && !existingHost.IsDisposed)
                {
                    ShowFloating(existingHost, panelName);
                    return;
                }

                var form = ActivatorUtilities.CreateInstance<TForm>(_serviceProvider);
                if (parameters is not null && form is IParameterizedPanel parameterizedForm)
                {
                    parameterizedForm.InitializeWithParameters(parameters);
                }

                form.StartPosition = FormStartPosition.Manual;
                form.Location = CascadeLocation();
                form.ShowInTaskbar = false;
                form.ShowIcon = true;
                form.Text = string.IsNullOrWhiteSpace(form.Text) ? panelName : form.Text;
                if (_useMDI)
                {
                    form.Owner = null;
                }
                else
                {
                    form.Owner = _owner;
                }

                _cachedPanels[panelName] = form;
                CachePanelAliases(panelName, form);
                ShowFloating(form, panelName);
            });
        }

        public async Task AddPanelAsync(UserControl panel, string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            ExecuteOnUiThread(() =>
            {
                _cachedPanels[panelName] = panel;
                CachePanelAliases(panelName, panel);
                ShowFloating(panel, panelName);
            });

            await Task.CompletedTask.ConfigureAwait(true);
        }

        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            var closed = false;

            ExecuteOnUiThread(() =>
            {
                if (!TryResolveHost(panelName, out var resolvedKey, out var host) || host == null || host.IsDisposed)
                {
                    _logger.LogDebug("[PANEL_NAV] HidePanel could not resolve host for '{PanelName}'", panelName);
                    return;
                }

                try
                {
                    if (_useMDI || host.MdiParent != null)
                    {
                        _logger.LogDebug("[PANEL_NAV] Closing MDI host '{ResolvedKey}' for request '{PanelName}'", resolvedKey, panelName);
                        host.Close();
                    }
                    else
                    {
                        _logger.LogDebug("[PANEL_NAV] Closing floating host '{ResolvedKey}' for request '{PanelName}'", resolvedKey, panelName);
                        host.Close();
                    }

                    CleanupClosedHost(resolvedKey, host);
                    closed = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PANEL_NAV] Failed to close host '{ResolvedKey}' for request '{PanelName}'", resolvedKey, panelName);
                }
            });

            return closed;
        }

        public string? GetActivePanelName() => _activePanelName;

        public void Dispose()
        {
            foreach (var host in _hosts.Values)
            {
                try { host.Dispose(); } catch { }
            }
            foreach (var panel in _cachedPanels.Values)
            {
                try { panel.Dispose(); } catch { }
            }
            _hosts.Clear();
            _cachedPanels.Clear();
        }

        private void ShowFloating(Control panelOrForm, string panelName)
        {
            Form host;

            // If owner is MDI container, create MDI child instead of floating window
            if (_useMDI && panelOrForm is not Form)
            {
                host = CreateMDIChild(panelName, panelOrForm as UserControl);
            }
            else if (panelOrForm is Form form)
            {
                host = form;
            }
            else
            {
                host = GetOrCreateHost(panelName, panelOrForm as UserControl);
            }

            EnsureTrackedHost(panelName, host);

            // Configure and show
            if (_useMDI)
            {
                PrepareHostForMdi(host, panelName);

                if (!ReferenceEquals(host.MdiParent, _owner))
                {
                    host.MdiParent = _owner;
                }

                if (!host.Visible)
                {
                    host.Show();
                }

                if (host.WindowState != FormWindowState.Maximized)
                {
                    host.WindowState = FormWindowState.Maximized;
                }
            }
            else
            {
                host.Text = string.IsNullOrWhiteSpace(host.Text) ? panelName : host.Text;
                host.StartPosition = host.StartPosition == FormStartPosition.Manual ? FormStartPosition.Manual : FormStartPosition.CenterParent;
                host.Location = host.StartPosition == FormStartPosition.Manual ? host.Location : CascadeLocation();
                host.ShowInTaskbar = false;
                host.Owner = _owner;
                host.TopMost = true;
                host.WindowState = FormWindowState.Normal;
                host.Show();
            }

            host.BringToFront();
            host.Activate();

            _activePanelName = panelName;
            PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, panelOrForm.GetType()));
        }

        private void PrepareHostForMdi(Form host, string panelName)
        {
            host.Text = string.IsNullOrWhiteSpace(host.Text) ? panelName : host.Text;
            host.ShowInTaskbar = false;
            host.StartPosition = FormStartPosition.Manual;

            if (host.MinimumSize != Size.Empty)
            {
                _logger.LogDebug("[PANEL_NAV] Resetting non-default MinimumSize {MinimumSize} for MDI host '{PanelName}'", host.MinimumSize, panelName);
                host.MinimumSize = Size.Empty;
            }

            if (host.Owner != null)
            {
                host.Owner = null;
            }
        }

        private void EnsureTrackedHost(string panelName, Form host)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                return;
            }

            if (_hosts.TryGetValue(panelName, out var existing) && ReferenceEquals(existing, host))
            {
                return;
            }

            _hosts[panelName] = host;
            host.FormClosed += (_, __) => CleanupClosedHost(panelName, host);
        }

        private Form GetOrCreateHost(string panelName, UserControl? panel)
        {
            if (_hosts.TryGetValue(panelName, out var existing) && !existing.IsDisposed)
            {
                EnsurePanelAttached(existing, panel);
                return existing;
            }

            var host = new Form
            {
                FormBorderStyle = FormBorderStyle.Sizable,
                ShowIcon = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = CascadeLocation(),
                Size = panel?.Size.IsEmpty == false && panel.Size.Width > 0 && panel.Size.Height > 0
                    ? panel.Size
                    : new Size(900, 600),
                MinimumSize = new Size(0, 0),
                Text = panelName,
                AutoScaleMode = AutoScaleMode.Dpi,
                AutoScroll = false,
                Padding = Padding.Empty
            };

            EnsurePanelAttached(host, panel);

            EnsureTrackedHost(panelName, host);
            return host;
        }

        private void EnsurePanelAttached(Form host, UserControl? panel)
        {
            if (panel == null) return;

            if (panel.Parent != null && !ReferenceEquals(panel.Parent, host))
            {
                panel.Parent.Controls.Remove(panel);
            }

            if (!host.Controls.Contains(panel))
            {
                panel.Dock = DockStyle.Fill;
                panel.Margin = Padding.Empty;
                host.Padding = Padding.Empty;
                host.Controls.Clear();
                host.Controls.Add(panel);

                // Force handle creation to trigger OnHandleCreated and ViewModel resolution
                if (!panel.IsHandleCreated)
                {
                    _ = panel.Handle;
                }

                // Trigger Load event explicitly if handle already exists
                panel.PerformLayout();
            }
        }

        private Point CascadeLocation()
        {
            const int offset = 24;
            var screen = Screen.FromControl(_owner).WorkingArea;
            var baseX = Math.Max(screen.Left + offset, screen.Left + 50);
            var baseY = Math.Max(screen.Top + offset, screen.Top + 50);
            var cascadeCount = _hosts.Count % 8;
            var location = new Point(baseX + cascadeCount * offset, baseY + cascadeCount * offset);
            return location;
        }

        /// <summary>
        /// Creates an MDI child form to host a panel.
        /// Used when owner is MDI container.
        /// </summary>
        private Form CreateMDIChild(string panelName, UserControl? panel)
        {
            if (_hosts.TryGetValue(panelName, out var existing) && !existing.IsDisposed)
            {
                if (panel != null)
                {
                    EnsurePanelAttached(existing, panel);
                }
                return existing;
            }

            var mdiChild = new Form
            {
                Text = panelName,
                FormBorderStyle = FormBorderStyle.Sizable,
                ShowIcon = false,
                WindowState = FormWindowState.Maximized,
                AutoScaleMode = AutoScaleMode.Dpi,
                MinimumSize = new Size(0, 0),
                Padding = Padding.Empty
            };

            if (panel != null)
            {
                EnsurePanelAttached(mdiChild, panel);
            }

            EnsureTrackedHost(panelName, mdiChild);

            // Apply theme
            var currentTheme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(mdiChild, currentTheme);

            return mdiChild;
        }

        private void ExecuteOnUiThread(Action action)
        {
            if (_owner.IsDisposed) return;

            if (_owner.InvokeRequired)
            {
                _owner.Invoke(action);
                return;
            }

            action();
        }

        private static string NormalizePanelKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        }

        private void CachePanelAliases(string panelName, Control panel)
        {
            if (panel == null || panel.IsDisposed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(panelName))
            {
                _cachedPanels[panelName] = panel;
            }

            if (!string.IsNullOrWhiteSpace(panel.Name))
            {
                _cachedPanels[panel.Name] = panel;
            }

            var typeName = panel.GetType().Name;
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                _cachedPanels[typeName] = panel;
            }
        }

        private bool TryResolveHost(string panelName, out string resolvedKey, out Form? resolvedHost)
        {
            resolvedKey = panelName;
            resolvedHost = null;

            if (_hosts.TryGetValue(panelName, out var exactHost) && exactHost != null && !exactHost.IsDisposed)
            {
                resolvedHost = exactHost;
                return true;
            }

            var normalizedRequest = NormalizePanelKey(panelName);

            foreach (var kvp in _hosts)
            {
                var key = kvp.Key;
                var host = kvp.Value;
                if (host == null || host.IsDisposed)
                {
                    continue;
                }

                var keyMatches = string.Equals(key, panelName, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(NormalizePanelKey(key), normalizedRequest, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(host.Text, panelName, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(NormalizePanelKey(host.Text), normalizedRequest, StringComparison.OrdinalIgnoreCase);

                if (keyMatches || HostContainsPanelIdentifier(host, panelName, normalizedRequest))
                {
                    resolvedKey = key;
                    resolvedHost = host;
                    return true;
                }
            }

            if (_cachedPanels.TryGetValue(panelName, out var cachedPanel) && cachedPanel != null && !cachedPanel.IsDisposed)
            {
                foreach (var kvp in _hosts)
                {
                    var host = kvp.Value;
                    if (host == null || host.IsDisposed)
                    {
                        continue;
                    }

                    if (host.Controls.Contains(cachedPanel))
                    {
                        resolvedKey = kvp.Key;
                        resolvedHost = host;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HostContainsPanelIdentifier(Form host, string panelName, string normalizedRequest)
        {
            foreach (Control child in host.Controls)
            {
                if (string.Equals(child.Name, panelName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.GetType().Name, panelName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizePanelKey(child.Name), normalizedRequest, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizePanelKey(child.GetType().Name), normalizedRequest, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Text, panelName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizePanelKey(child.Text), normalizedRequest, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupClosedHost(string panelName, Form host)
        {
            if (_hosts.TryGetValue(panelName, out var trackedHost) && ReferenceEquals(trackedHost, host))
            {
                _hosts.Remove(panelName);
            }

            if (_cachedPanels.TryGetValue(panelName, out var cachedPanel) && (cachedPanel == null || cachedPanel.IsDisposed || (host != null && host.Controls.Contains(cachedPanel))))
            {
                _cachedPanels.Remove(panelName);
            }

            var aliasesToRemove = new List<string>();
            foreach (var alias in _cachedPanels)
            {
                var control = alias.Value;
                if (control == null || control.IsDisposed || (host != null && host.Controls.Contains(control)))
                {
                    aliasesToRemove.Add(alias.Key);
                }
            }

            foreach (var alias in aliasesToRemove)
            {
                _cachedPanels.Remove(alias);
            }

            if (string.Equals(_activePanelName, panelName, StringComparison.OrdinalIgnoreCase))
            {
                _activePanelName = null;
            }
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
