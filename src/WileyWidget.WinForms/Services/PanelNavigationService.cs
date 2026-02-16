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

        public void ShowPanel<TPanel>(string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            ShowPanel<TPanel>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowPanel<TPanel>(string panelName, object? parameters, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            // 🔴 BREAKPOINT 1: ShowPanel Entry
            NavigationDebugger.BreakOnShowPanelEntry(panelName, typeof(TPanel).Name);

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
                form.Owner = _owner;

                _cachedPanels[panelName] = form;
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
                ShowFloating(panel, panelName);
            });

            await Task.CompletedTask.ConfigureAwait(true);
        }

        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            if (_hosts.TryGetValue(panelName, out var host) && !host.IsDisposed)
            {
                ExecuteOnUiThread(() => host.Hide());
                return true;
            }

            return false;
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

            // Configure and show
            if (_useMDI)
            {
                host.MdiParent = _owner;
                host.WindowState = FormWindowState.Maximized;
                host.Show();
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
                MinimumSize = panel?.MinimumSize.IsEmpty == false && panel.MinimumSize.Width > 0 && panel.MinimumSize.Height > 0
                    ? panel.MinimumSize
                    : new Size(400, 300),
                Text = panelName,
                AutoScaleMode = AutoScaleMode.Dpi,
                AutoScroll = false,
                Padding = Padding.Empty
            };

            EnsurePanelAttached(host, panel);

            _hosts[panelName] = host;
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
                MinimumSize = panel?.MinimumSize.IsEmpty == false ? panel.MinimumSize : new Size(400, 300),
                Padding = Padding.Empty
            };

            if (panel != null)
            {
                EnsurePanelAttached(mdiChild, panel);
            }

            _hosts[panelName] = mdiChild;

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
