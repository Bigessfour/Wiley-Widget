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

        public event EventHandler<PanelActivatedEventArgs>? PanelActivated;

        public PanelNavigationService(Form owner, IServiceProvider serviceProvider, ILogger<PanelNavigationService> logger)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            if (panelOrForm is Form form)
            {
                host = form;
            }
            else
            {
                host = GetOrCreateHost(panelName, panelOrForm as UserControl);
            }

            host.Text = string.IsNullOrWhiteSpace(host.Text) ? panelName : host.Text;
            host.StartPosition = host.StartPosition == FormStartPosition.Manual ? FormStartPosition.Manual : FormStartPosition.CenterParent;
            host.Location = host.StartPosition == FormStartPosition.Manual ? host.Location : CascadeLocation();
            host.ShowInTaskbar = false;
            host.Owner = _owner;
            host.TopMost = true;  // Ensure floating panels are visible on top
            host.Show();
            host.BringToFront();

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
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                ShowIcon = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = CascadeLocation(),
                Size = panel?.PreferredSize.IsEmpty == false ? panel.PreferredSize : new Size(900, 600),
                Text = panelName
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
                host.Controls.Clear();
                host.Controls.Add(panel);
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
