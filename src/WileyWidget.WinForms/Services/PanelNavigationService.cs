using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;

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

        void ShowPanel(Type panelType, string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true);

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
        private const int MinimumPanelTopInsetLogical = 8;

        private readonly Form _owner;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PanelNavigationService> _logger;
        private readonly Dictionary<string, Form> _hosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Control> _cachedPanels = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Control> _initializedAsyncPanels = new();
        private readonly HashSet<Control> _firstAttachCompletedPanels = new();
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

        public void ShowPanel(Type panelType, string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle = Syncfusion.Windows.Forms.Tools.DockingStyle.Right, bool allowFloating = true)
        {
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));
            if (panelType == null) throw new ArgumentNullException(nameof(panelType));
            if (!typeof(UserControl).IsAssignableFrom(panelType))
            {
                throw new ArgumentException($"Panel type must inherit {nameof(UserControl)}.", nameof(panelType));
            }

            ExecuteOnUiThread(() =>
            {
                Control? panel = null;
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel) && !existingPanel.IsDisposed)
                {
                    if (existingPanel.GetType() == panelType)
                    {
                        panel = existingPanel;
                    }
                    else
                    {
                        _logger.LogWarning("[PANEL_NAV] Replacing cached panel '{PanelName}' type {ExistingType} with {RequestedType}",
                            panelName,
                            existingPanel.GetType().Name,
                            panelType.Name);

                        existingPanel.Dispose();
                    }
                }

                if (panel == null)
                {
                    var createdPanel = ActivatorUtilities.CreateInstance(_serviceProvider, panelType);
                    if (createdPanel is not UserControl typedPanel)
                    {
                        throw new InvalidOperationException($"Resolved panel type '{panelType.FullName}' is not a UserControl.");
                    }

                    panel = typedPanel;
                    _cachedPanels[panelName] = panel;
                    CachePanelAliases(panelName, panel);
                }

                ConfigureSpecialHostPanels(panel, panelName);
                ShowInTabbedMdi(panel, panelName, preferredStyle);
            });
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

                ConfigureSpecialHostPanels(panel, panelName);

                if (parameters is not null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                ShowInTabbedMdi(panel, panelName, preferredStyle);
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
                    ShowInTabbedMdi(existingHost, panelName, preferredStyle);
                    return;
                }

                var form = ActivatorUtilities.CreateInstance<TForm>(_serviceProvider);
                if (parameters is not null && form is IParameterizedPanel parameterizedForm)
                {
                    parameterizedForm.InitializeWithParameters(parameters);
                }

                form.ShowInTaskbar = false;
                form.ShowIcon = true;
                form.Text = string.IsNullOrWhiteSpace(form.Text) ? panelName : form.Text;
                form.Owner = null;

                _cachedPanels[panelName] = form;
                CachePanelAliases(panelName, form);
                ShowInTabbedMdi(form, panelName, preferredStyle);
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
                ShowInTabbedMdi(panel, panelName, preferredStyle);
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
                    _logger.LogDebug("[PANEL_NAV] Closing MDI host '{ResolvedKey}' for request '{PanelName}'", resolvedKey, panelName);
                    host.Close();

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

        private void ShowInTabbedMdi(Control panelOrForm, string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle)
        {
            if (_owner.IsDisposed || _owner.Disposing)
            {
                _logger.LogWarning("[PANEL_NAV] ShowInTabbedMdi skipped — owner is disposed/disposing for panel '{PanelName}'", panelName);
                return;
            }

            Form host;
            UserControl? hostedPanel = null;

            if (panelOrForm is Form form)
            {
                host = form;
                host.Text = string.IsNullOrWhiteSpace(host.Text) ? panelName : host.Text;
                host.ShowInTaskbar = false;
                host.ShowIcon = false;
                host.Owner = null;
                if (!ReferenceEquals(host.MdiParent, _owner))
                {
                    host.MdiParent = _owner;
                }
            }
            else
            {
                hostedPanel = panelOrForm as UserControl;
                host = CreateMDIChild(panelName, hostedPanel);
            }

            EnsureTrackedHost(panelName, host);

            var suspendedControls = SuspendLayouts(_owner, host, hostedPanel);
            var panelAttached = false;

            try
            {
                if (hostedPanel != null)
                {
                    panelAttached = EnsurePanelAttached(host, hostedPanel);
                }

                PrepareHostForMdi(host, panelName);

                if (!ReferenceEquals(host.MdiParent, _owner))
                {
                    host.MdiParent = _owner;
                }

                if (!host.Visible)
                {
                    host.Show();
                }

                if (ShouldMaximizeMdiHost(preferredStyle))
                {
                    if (host.WindowState != FormWindowState.Maximized)
                    {
                        host.WindowState = FormWindowState.Maximized;
                    }
                }
                else if (host.WindowState == FormWindowState.Maximized)
                {
                    host.WindowState = FormWindowState.Normal;
                }

                host.BringToFront();
                host.Activate();

                EnsureControlVisibleAndLaidOut(host);
                if (hostedPanel != null)
                {
                    EnsureControlVisibleAndLaidOut(hostedPanel);
                }

                if (panelAttached)
                {
                    QueuePostShowLayoutPass(host, hostedPanel);
                }

                _activePanelName = panelName;
                PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, panelOrForm.GetType()));
            }
            finally
            {
                ResumeLayouts(suspendedControls, performLayout: true);
            }
        }

        private static bool ShouldMaximizeMdiHost(Syncfusion.Windows.Forms.Tools.DockingStyle preferredStyle)
            => preferredStyle is Syncfusion.Windows.Forms.Tools.DockingStyle.Fill
                or Syncfusion.Windows.Forms.Tools.DockingStyle.Tabbed;

        /// <summary>
        /// Validates and corrects MDI child form properties after creation.
        /// Only writes properties whose values differ from the desired state to avoid
        /// triggering <see cref="Control.RecreateHandle"/> (e.g. changing ShowInTaskbar on a
        /// live handle triggers RecreateHandle → potential ObjectDisposedException in Maximized MDI).
        /// Per Microsoft docs, MdiParent must be assigned before the form is shown and
        /// ideally before the handle is created; <see cref="CreateMDIChild"/> owns that assignment.
        /// </summary>
        private void PrepareHostForMdi(Form host, string panelName)
        {
            if (host.IsDisposed || host.Disposing)
            {
                _logger.LogWarning("[PANEL_NAV] PrepareHostForMdi skipped — host is disposed/disposing for '{PanelName}'", panelName);
                return;
            }

            if (host.RecreatingHandle)
            {
                _logger.LogWarning("[PANEL_NAV] PrepareHostForMdi skipped — host is mid-RecreateHandle for '{PanelName}'", panelName);
                return;
            }

            // Title — safe to set at any time.
            if (string.IsNullOrWhiteSpace(host.Text))
            {
                host.Text = panelName;
            }

            // ShowInTaskbar — changing this on a form whose handle is already created triggers
            // UpdateStyles() → RecreateHandle(). Only write when the value actually differs so
            // re-entrant calls on an existing cached host are safe no-ops.
            if (host.ShowInTaskbar)
            {
                host.ShowInTaskbar = false;
            }

            // StartPosition is consumed at Show() time; writing it after the form is
            // already visible has no effect, so guard to avoid misleading state writes.
            if (!host.Visible && host.StartPosition != FormStartPosition.Manual)
            {
                host.StartPosition = FormStartPosition.Manual;
            }

            // In tabbed MDI mode, keep host borderless so only TabbedMDI chrome is rendered.
            // Restrict to pre-handle to avoid late CreateParams changes.
            if (_tabbedMdi != null && !host.IsHandleCreated && host.FormBorderStyle != FormBorderStyle.None)
            {
                host.FormBorderStyle = FormBorderStyle.None;
            }

            // MinimumSize — safe to mutate at any time.
            if (host.MinimumSize != Size.Empty)
            {
                _logger.LogDebug("[PANEL_NAV] Resetting non-default MinimumSize {MinimumSize} for MDI host '{PanelName}'", host.MinimumSize, panelName);
                host.MinimumSize = Size.Empty;
            }

            // Owner must be null for MDI children — clearing after handle creation is safe.
            if (host.Owner != null)
            {
                host.Owner = null;
            }

            // Sanity check: CreateMDIChild should have assigned MdiParent before this method runs.
            if (!host.IsMdiChild)
            {
                _logger.LogDebug("[PANEL_NAV] PrepareHostForMdi: host '{PanelName}' is not yet flagged as MDI child (Visible={Visible}, ParentAssigned={ParentAssigned}).",
                    panelName,
                    host.Visible,
                    host.MdiParent != null);
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

        private bool EnsurePanelAttached(Form host, UserControl? panel)
        {
            if (panel == null)
            {
                return false;
            }

            var attached = false;
            var applyVisibilityGuard = !_firstAttachCompletedPanels.Contains(panel);

            if (panel.Parent != null && !ReferenceEquals(panel.Parent, host))
            {
                panel.Parent.Controls.Remove(panel);
            }

            if (!host.Controls.Contains(panel))
            {
                attached = true;

                if (applyVisibilityGuard)
                {
                    panel.Visible = false;
                }

                panel.Dock = DockStyle.Fill;
                panel.Margin = Padding.Empty;
                ApplyMinimumTopInset(panel);
                var minimumWidth = Math.Max(640, panel.MinimumSize.Width);
                var minimumHeight = Math.Max(420, panel.MinimumSize.Height);
                panel.MinimumSize = new Size(minimumWidth, minimumHeight);

                if (panel.Width < panel.MinimumSize.Width || panel.Height < panel.MinimumSize.Height)
                {
                    panel.Size = new Size(
                        Math.Max(panel.Width, panel.MinimumSize.Width),
                        Math.Max(panel.Height, panel.MinimumSize.Height));
                }

                host.Padding = Padding.Empty;
                host.Controls.Clear();
                host.Controls.Add(panel);
                panel.Bounds = host.ClientRectangle;

                // Force handle creation to trigger OnHandleCreated and ViewModel resolution
                if (!panel.IsHandleCreated)
                {
                    _ = panel.Handle;
                }

                TryInitializeAsyncPanel(panel, host.Text);
            }

            _firstAttachCompletedPanels.Add(panel);

            panel.Visible = true;
            panel.Dock = DockStyle.Fill;
            panel.BringToFront();
            ResetAutoScrollOffsets(panel);
            EnsureControlVisibleAndLaidOut(panel);
            EnsureControlVisibleAndLaidOut(host);

            return attached;
        }

        private static void ApplyMinimumTopInset(Control control)
        {
            var dpi = control.DeviceDpi > 0 ? control.DeviceDpi : 96;
            var scale = dpi / 96f;
            var minimumTopInset = (int)Math.Ceiling(MinimumPanelTopInsetLogical * scale);

            if (control.Padding.Top >= minimumTopInset)
            {
                return;
            }

            control.Padding = new Padding(
                control.Padding.Left,
                minimumTopInset,
                control.Padding.Right,
                control.Padding.Bottom);
        }

        private static void ResetAutoScrollOffsets(Control root)
        {
            if (root is ScrollableControl scrollable && scrollable.AutoScroll)
            {
                try
                {
                    scrollable.AutoScrollPosition = Point.Empty;
                }
                catch
                {
                    // Best effort.
                }
            }

            foreach (Control child in root.Controls)
            {
                ResetAutoScrollOffsets(child);
            }
        }

        private void TryInitializeAsyncPanel(Control panel, string panelName)
        {
            if (panel is not IAsyncInitializable asyncInitializable)
            {
                return;
            }

            if (_initializedAsyncPanels.Contains(panel))
            {
                return;
            }

            _initializedAsyncPanels.Add(panel);

            _ = InitializeAsyncPanelCore(asyncInitializable, panel, panelName);
        }

        private void ConfigureSpecialHostPanels(Control panel, string panelName)
        {
            if (panel is not FormHostPanel formHostPanel)
            {
                return;
            }

            try
            {
                if (string.Equals(panelName, "Rates", StringComparison.OrdinalIgnoreCase))
                {
                    if (formHostPanel.HostedForm == null || formHostPanel.HostedForm.IsDisposed)
                    {
                        var ratesForm = ActivatorUtilities.CreateInstance<RatesPage>(_serviceProvider);
                        formHostPanel.HostForm(ratesForm);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PANEL_NAV] Failed to configure FormHostPanel content for '{PanelName}'", panelName);
            }
        }

        private async Task InitializeAsyncPanelCore(IAsyncInitializable asyncInitializable, Control panel, string panelName)
        {
            try
            {
                _logger.LogDebug("[PANEL_NAV] Initializing async panel '{PanelName}' ({PanelType})", panelName, panel.GetType().Name);
                await asyncInitializable.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
                _logger.LogDebug("[PANEL_NAV] Async panel initialized '{PanelName}'", panelName);

                ExecuteOnUiThread(() =>
                {
                    EnsureControlVisibleAndLaidOut(panel);

                    var host = panel.FindForm();
                    if (host != null && !host.IsDisposed)
                    {
                        EnsureControlVisibleAndLaidOut(host);
                    }
                });
            }
            catch (Exception ex)
            {
                _initializedAsyncPanels.Remove(panel);
                _logger.LogWarning(ex, "[PANEL_NAV] Async initialization failed for '{PanelName}' ({PanelType})", panelName, panel.GetType().Name);
            }
        }

        private static void EnsureControlVisibleAndLaidOut(Control control, bool includeChildren = false)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            // === FIX: Guard against CreateHandle race (handle not ready yet) ===
            if (control is Form form)
            {
                if (!form.IsHandleCreated)
                {
                    // Defer layout until handle is created (Syncfusion v32.2.3 stability)
                    form.Shown += (s, e) =>
                    {
                        if (!form.IsDisposed)
                            EnsureControlVisibleAndLaidOut(form);
                    };
                    return;
                }
            }
            else if (!control.IsHandleCreated)
            {
                // For non-form controls, queue event and retry
                control.HandleCreated += (s, e) =>
                {
                    if (!control.IsDisposed)
                        EnsureControlVisibleAndLaidOut(control);
                };
                return;
            }

            control.Visible = true;

            // BlazorWebView uses a custom BlazorWebViewControlCollection that throws
            // NotSupportedException from SetChildIndex (called internally by BringToFront).
            // Detect this by checking the type name to avoid a hard assembly reference.
            var parentIsBlazorWebView = control.Parent?.GetType().Name == "BlazorWebView";
            if (!parentIsBlazorWebView)
            {
                try
                {
                    control.BringToFront();
                }
                catch (NotSupportedException)
                {
                    // Swallow: parent control collection does not support reordering (e.g. BlazorWebView).
                }
            }

            control.PerformLayout();
            control.Invalidate();

            if (control.IsHandleCreated)
            {
                try
                {
                    control.Update();
                }
                catch
                {
                    // Best effort only; avoid throwing during transient handle recreation.
                }
            }

            if (!includeChildren)
            {
                return;
            }

            foreach (Control child in control.Controls)
            {
                // Do not recurse into BlazorWebView — its internal Controls collection
                // is managed exclusively by the Blazor host and throws on most mutations.
                if (child.GetType().Name == "BlazorWebView")
                {
                    continue;
                }

                EnsureControlVisibleAndLaidOut(child, includeChildren: false);
            }
        }

        private static void QueuePostShowLayoutPass(Form host, UserControl? hostedPanel)
        {
            if (host == null || host.IsDisposed || !host.IsHandleCreated)
            {
                return;
            }

            try
            {
                host.BeginInvoke(new Action(() =>
                {
                    if (host.IsDisposed)
                    {
                        return;
                    }

                    EnsureControlVisibleAndLaidOut(host);

                    if (hostedPanel != null && !hostedPanel.IsDisposed && ReferenceEquals(hostedPanel.Parent, host))
                    {
                        hostedPanel.Dock = DockStyle.Fill;
                        ApplyMinimumTopInset(hostedPanel);
                        ResetAutoScrollOffsets(hostedPanel);
                        EnsureControlVisibleAndLaidOut(hostedPanel);
                    }
                }));
            }
            catch
            {
                // best-effort deferred layout pass
            }
        }

        /// <summary>
        /// Creates an MDI child form to host a panel, or returns the cached instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Handle-creation order matters.</b> In WinForms, accessing
        /// <see cref="Control.Handle"/> on a control whose parent has no HWND yet forces the
        /// parent's handle to be created first. <see cref="EnsurePanelAttached"/> calls
        /// <c>_ = panel.Handle</c> to trigger <c>OnHandleCreated</c> for ViewModel wiring,
        /// which means the host form's HWND is created as a side effect.
        /// </para>
        /// <para>
        /// Properties that affect <see cref="Control.CreateParams"/> (e.g.
        /// <see cref="Form.ShowInTaskbar"/>, <see cref="Form.FormBorderStyle"/>) and
        /// <see cref="Form.MdiParent"/> trigger <see cref="Control.RecreateHandle"/> when set
        /// after the HWND is live. A RecreateHandle on a Maximized MDI child can race with
        /// MDI client bookkeeping inside <see cref="Form.CreateHandle"/> and throw
        /// <see cref="ObjectDisposedException"/> — the crash observed in <c>OnShown</c>.
        /// </para>
        /// <para>
        /// The fix (per Microsoft docs — <em>"assign MdiParent before calling Show()"</em>):
        /// set <see cref="Form.MdiParent"/> in the object initializer so the HWND is born
        /// with the <c>WS_CHILD</c> style applied — no RecreateHandle is ever triggered.
        /// </para>
        /// </remarks>
        private Form CreateMDIChild(string panelName, UserControl? panel)
        {
            if (_hosts.TryGetValue(panelName, out var existing) && !existing.IsDisposed)
            {
                return existing;
            }

            if (_owner.IsDisposed || _owner.Disposing)
            {
                _logger.LogWarning("[PANEL_NAV] CreateMDIChild skipped — owner is disposed/disposing for panel '{PanelName}'", panelName);
                throw new ObjectDisposedException(nameof(_owner),
                    $"Cannot create MDI child for '{panelName}': owner form is disposed or disposing.");
            }

            if (!_owner.IsMdiContainer)
            {
                _logger.LogError("[PANEL_NAV] CreateMDIChild: owner is not an MDI container for '{PanelName}'.", panelName);
                throw new InvalidOperationException($"Owner form must be an MDI container before showing panel '{panelName}'.");
            }

            // All CreateParams-affecting properties (ShowInTaskbar, FormBorderStyle, etc.)
            // and MdiParent are set here, before any HWND exists. EnsurePanelAttached below
            // forces panel.Handle which also creates the host HWND as a side effect — but by
            // then the form is already wired as an MDI child (WS_CHILD), so no RecreateHandle
            // is ever triggered. Per MS docs: child.MdiParent = this; child.Show();
            var mdiChild = new Form
            {
                Text = panelName,
                // FormBorderStyle.None: TabbedMDIManager provides all chrome via the tab strip.
                // Sizable would render a caption bar INSIDE the tabbed area — nested-window look.
                FormBorderStyle = FormBorderStyle.None,
                ShowIcon = false,
                ShowInTaskbar = false,                   // Must precede HWND creation
                StartPosition = FormStartPosition.Manual, // Must precede Show()
                WindowState = FormWindowState.Normal,
                AutoScaleMode = AutoScaleMode.Dpi,
                MinimumSize = Size.Empty,
                Padding = Padding.Empty,
                // Assign MdiParent before EnsurePanelAttached forces HWND creation so the
                // form's native window is created as WS_CHILD in one shot — never re-created.
                MdiParent = _owner,
            };

            EnsureTrackedHost(panelName, mdiChild);

            // Apply theme — SfSkinManager handles pre-handle state gracefully.
            var currentTheme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme
                ?? "Office2019Colorful";
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(mdiChild, currentTheme);

            _logger.LogDebug("[PANEL_NAV] MDI child created for '{PanelName}' (IsMdiChild={IsMdiChild})", panelName, mdiChild.IsMdiChild);

            return mdiChild;
        }

        private void ExecuteOnUiThread(Action action)
        {
            if (_owner.IsDisposed || _owner.Disposing) return;

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

            _initializedAsyncPanels.RemoveWhere(control => control == null || control.IsDisposed || (host != null && host.Controls.Contains(control)));
            _firstAttachCompletedPanels.RemoveWhere(control => control == null || control.IsDisposed || (host != null && host.Controls.Contains(control)));

            if (string.Equals(_activePanelName, panelName, StringComparison.OrdinalIgnoreCase))
            {
                _activePanelName = null;
            }
        }

        private static List<Control> SuspendLayouts(params Control?[] controls)
        {
            var suspended = new List<Control>();

            foreach (var control in controls)
            {
                if (control == null || control.IsDisposed)
                {
                    continue;
                }

                if (suspended.Contains(control))
                {
                    continue;
                }

                control.SuspendLayout();
                suspended.Add(control);
            }

            return suspended;
        }

        private static void ResumeLayouts(List<Control> controls, bool performLayout)
        {
            for (var index = controls.Count - 1; index >= 0; index--)
            {
                var control = controls[index];
                if (control.IsDisposed)
                {
                    continue;
                }

                control.ResumeLayout(performLayout);
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
