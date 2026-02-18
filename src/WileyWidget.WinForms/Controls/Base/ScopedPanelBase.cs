#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Factories;
using System.Diagnostics;

namespace WileyWidget.WinForms.Controls.Base
{
    /// <summary>
    /// Non-generic base for type-erased runtime access (e.g., QuickAccess toolbar reflection).
    /// </summary>
    public abstract class ScopedPanelBase : UserControl
    {
        /// <summary>Type-erased ViewModel for runtime reflection access.</summary>
        public virtual object? UntypedViewModel => null;
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

        /// <summary>Hides the panel. Panels wire close-button events to this.</summary>
        protected virtual void ClosePanel() => Visible = false;

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

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _logger?.LogTrace("[{Panel}] Visibility → {Visible} ({W}x{H})", GetType().Name, Visible, Width, Height);
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
            if (disposing)
            {
                _logger?.LogDebug("[{Panel}] Disposing", GetType().Name);
                _operationCts?.Cancel();
                _operationCts?.Dispose();
                _scope?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
