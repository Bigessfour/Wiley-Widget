#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Factories;
using System.Diagnostics;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms.Controls.Base
{
    /// <summary>
    /// Non-generic base for type-erased runtime access (e.g., QuickAccess toolbar reflection).
    /// </summary>
    public abstract class ScopedPanelBase : UserControl
    {
        /// <summary>Type-erased ViewModel for runtime reflection access.</summary>
        public virtual object? UntypedViewModel => null;

        protected ScopedPanelBase()
        {
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
        }
    }

    /// <summary>
    /// Generic DI-scoped panel base class. Manages a single DI scope, resolves the ViewModel,
    /// and provides lifecycle helpers (load, save, validate, close, operation tracking).
    /// </summary>
    public abstract class ScopedPanelBase<TViewModel> : ScopedPanelBase, ICompletablePanel
        where TViewModel : class, INotifyPropertyChanged
    {
        protected readonly IServiceScope? _scope;
        protected readonly ILogger _logger;

        /// <summary>Logger property alias for backward compatibility with panels using PascalCase Logger.</summary>
        protected ILogger Logger => _logger;

        protected TViewModel? ViewModel { get; set; }

        /// <inheritdoc/>
        public override object? UntypedViewModel => ViewModel;

        protected bool IsLoaded { get; set; }
        protected bool IsBusy { get; set; }
        protected bool HasUnsavedChanges { get; private set; }

        /// <summary>Provides access to the DI service provider from the panel's scope.</summary>
        protected IServiceProvider ServiceProvider => _scope!.ServiceProvider;

        /// <summary>Factory for creating Syncfusion controls with mandatory properties pre-set.</summary>
        protected SyncfusionControlFactory ControlFactory =>
            ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(_scope!.ServiceProvider);

        /// <summary>Accumulated validation errors — cleared and repopulated by ValidateAsync overrides.</summary>
        protected List<ValidationItem> ValidationErrors { get; } = new();

        private CancellationTokenSource? _operationCts;
        private System.Windows.Forms.Timer? _finalLayoutTimer;
        private bool _shownFired;

        protected ScopedPanelBase(IServiceScopeFactory scopeFactory, ILogger logger)
        {
            _scope = scopeFactory.CreateScope();
            _logger = logger;

            // Resolve ViewModel once from scope
            ViewModel = ServiceProviderServiceExtensions.GetService<TViewModel>(_scope.ServiceProvider);

            // Notify derived panels that the ViewModel is available
            OnViewModelResolved(ViewModel);
            _logger?.LogDebug("[{Panel}] Initialized — ViewModel: {VmType}",
                GetType().Name, ViewModel?.GetType().Name ?? $"{typeof(TViewModel).Name} (null)");

            // Handle form close button
            Load += ScopedPanelBase_Load;
        }

        protected virtual void OnViewModelResolved(TViewModel? vm) { }
        protected virtual void OnViewModelResolved(object? vm)
        {
            if (vm is TViewModel typed) OnViewModelResolved(typed);
        }
        protected virtual void OnThemeChanged(string themeName) { }

        /// <summary>
        /// Cancels any in-flight operation and starts a new one, returning its cancellation token.
        /// </summary>
        protected CancellationToken RegisterOperation()
        {
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            return _operationCts.Token;
        }

        /// <summary>
        /// Hides this panel via the navigation service (panel stays alive in memory, DI scope preserved).
        /// Falls back to <c>Visible = false</c> when MainForm is not reachable (e.g. design-time).
        /// </summary>
        protected virtual void ClosePanel()
        {
            // GetType().Name is always registered as a panel alias by CachePanelAliases, so it
            // is the most reliable key regardless of the DisplayName used at ShowPanel time.
            var panelName = GetType().Name;

            // The panel can be hosted directly by MainForm or inside a floating wrapper Form
            // whose Owner is MainForm.  Walk both levels.
            var hostForm = FindForm();
            var mainForm = hostForm as MainForm
                        ?? hostForm?.Owner as MainForm;

            if (mainForm != null)
            {
                // Check if this is a floating form (not the main form itself)
                if (hostForm != null && hostForm != mainForm)
                {
                    // For floating forms, close the host form directly
                    hostForm.Close();
                    _logger?.LogDebug("[{Panel}] ClosePanel → HostForm.Close() for floating panel", GetType().Name);
                }
                else
                {
                    // For docked panels, use MainForm.ClosePanel
                    mainForm.ClosePanel(panelName);
                    _logger?.LogDebug("[{Panel}] ClosePanel → MainForm.ClosePanel({Name})", GetType().Name, panelName);
                }
            }
            else
            {
                Visible = false;
                _logger?.LogWarning("[{Panel}] ClosePanel fallback: MainForm not reachable, set Visible=false", GetType().Name);
            }
        }

        private void ScopedPanelBase_Load(object? sender, EventArgs e)
        {
            var hostForm = FindForm();
            if (hostForm != null)
            {
                hostForm.FormClosing += HostForm_FormClosing;
            }
        }

        private void HostForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Allow host forms to close normally and avoid recursive ClosePanel calls.
            // PanelNavigationService cleans up hosts on FormClosed.
            if (sender is MainForm && !e.Cancel)
            {
                // For MainForm closures, do not re-route; let the application close naturally.
                return;
            }
        }

        /// <summary>
        /// Forces a recursive layout pass on this panel and its first two levels of children.
        /// Called via <see cref="BeginInvoke"/> after the Syncfusion DockingManager finishes
        /// sizing the hosted <see cref="UserControl"/>, so that content controls render at their
        /// correct positions instead of collapsing to 0×0 during the initial docking pass.
        /// Override in derived panels to target deeper containers (e.g., SplitContainerAdv inner panels).
        /// </summary>
        protected virtual void ForceFullLayout()
        {
            if (IsDisposed || !IsHandleCreated) return;

            SuspendLayout();
            try
            {
                PerformLayoutRecursive(this);
                Invalidate(true);
                Update();
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }

            _logger?.LogDebug("[{Panel}] ForceFullLayout completed ({W}x{H})", GetType().Name, Width, Height);
        }

        private static void PerformLayoutRecursive(Control control)
        {
            control.PerformLayout();
            foreach (Control child in control.Controls)
            {
                PerformLayoutRecursive(child);
            }
        }

        /// <summary>
        /// Clears all accumulated validation errors from <see cref="ValidationErrors"/>.
        /// </summary>
        protected void ClearValidationErrors() => ValidationErrors.Clear();

        /// <summary>
        /// Replaces the current validation errors with the provided items.
        /// </summary>
        protected void SetValidationErrors(IEnumerable<ValidationItem> errors)
        {
            ValidationErrors.Clear();
            ValidationErrors.AddRange(errors);
        }

        /// <summary>
        /// Runs <see cref="LoadAsync"/> in a fire-and-forget context, swallowing exceptions to
        /// the logger so callers are not blocked.
        /// </summary>
        protected async void LoadAsyncSafe()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await LoadAsync(CancellationToken.None);
                _logger?.LogDebug("[{Panel}] LoadAsync completed in {Ms}ms", GetType().Name, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LoadAsyncSafe failed in {Panel} after {Ms}ms", GetType().Name, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Dispatches <paramref name="action"/> to the UI thread; safe to call from any thread.
        /// </summary>
        protected void InvokeOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        public virtual async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsLoaded) return;
            IsBusy = true;
            try
            {
                if (ViewModel is IAsyncInitializable init)
                    await init.InitializeAsync(ct);

                IsLoaded = true;
            }
            finally { IsBusy = false; }
        }

        public virtual Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public virtual Task<ValidationResult> ValidateAsync(CancellationToken ct = default) => Task.FromResult(ValidationResult.Success);
        public virtual void FocusFirstError() { }

        protected void SetHasUnsavedChanges(bool value) => HasUnsavedChanges = value;

        /// <summary>
        /// Called the first time this panel becomes visible inside the DockingManager.
        /// Starts a one-shot 180ms timer that fires <see cref="ForceFullLayout"/> after
        /// DockingManager finishes its resize pass.  Override in derived panels to add
        /// panel-specific splitter configuration inside a <c>BeginInvoke</c> callback
        /// — always call <c>base.OnShown(e)</c> first to start the timer.
        /// </summary>
        protected virtual void OnShown(EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;

            _finalLayoutTimer?.Stop();
            _finalLayoutTimer?.Dispose();
            _finalLayoutTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _finalLayoutTimer.Tick += (s, _) =>
            {
                _finalLayoutTimer?.Stop();
                _finalLayoutTimer?.Dispose();
                _finalLayoutTimer = null;
                if (!IsDisposed && IsHandleCreated)
                    ForceFullLayout();
            };
            _finalLayoutTimer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _logger?.LogTrace("[{Panel}] Visibility → {Visible} ({W}x{H})", GetType().Name, Visible, Width, Height);
            QueueDeferredLayoutPass(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Visible)
            {
                QueueDeferredLayoutPass(e);
            }
        }

        private void QueueDeferredLayoutPass(EventArgs e)
        {
            // Queue a deferred layout pass so DockingManager has already finished resizing
            // this UserControl before we force children to lay out at the correct dimensions.
            if (!Visible || IsDisposed || !IsHandleCreated || Width <= 0 || Height <= 0)
            {
                return;
            }

            if (!_shownFired)
            {
                _shownFired = true;
                OnShown(e);
            }

            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && IsHandleCreated && Visible)
                {
                    ForceFullLayout();
                }
            }));
        }

        /// <summary>
        /// Emits a structured lifecycle debug landmark: <c>[PanelType] EventName: Detail</c>.
        /// Panels call this to add consistent log entries without repeating the message format.
        /// </summary>
        protected void LogPanelLifecycle(string eventName, string? detail = null)
        {
            if (detail is null)
                _logger?.LogDebug("[{Panel}] {Event}", GetType().Name, eventName);
            else
                _logger?.LogDebug("[{Panel}] {Event}: {Detail}", GetType().Name, eventName, detail);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _logger?.LogDebug("[{Panel}] Disposing", GetType().Name);
                    _operationCts?.Cancel();
                    _operationCts?.Dispose();
                    _finalLayoutTimer?.Stop();
                    _finalLayoutTimer?.Dispose();
                    _finalLayoutTimer = null;

                    if (ContextMenuStrip != null)
                    {
                        ContextMenuStrip.Dispose();
                        ContextMenuStrip = null;
                    }

                    _scope?.Dispose();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _logger?.LogWarning(ex, "[{Panel}] Dispose encountered a non-fatal exception", GetType().Name);
                }
                catch
                {
                    // Never allow dispose logging failures to bubble up.
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
