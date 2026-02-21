#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("WileyWidget.Tests")]

namespace WileyWidget.WinForms.Controls.Base;

/// <summary>
/// Non-generic base class for panels that require scoped ViewModels.
/// This class is designed to be instantiable by the WinForms designer.
/// </summary>
public abstract class ScopedPanelBase : UserControl, ICompletablePanel, INotifyPropertyChanged, ISupportInitialize
{
    // Note: not readonly to allow a lightweight design-time fallback factory when the designer instantiates controls.
    private IServiceScopeFactory _scopeFactory;
    protected readonly ILogger _logger;
    protected object? _viewModel;
    private object? _dataContext;
    private bool _disposed;
    private bool _isBusy;
    private IThemeService? _themeService;
    private IServiceScope? _scope;

    // ICompletablePanel backing fields
    protected bool _isLoaded;
    protected bool _hasUnsavedChanges;
    protected PanelMode? _mode = PanelMode.View;
    protected readonly List<ValidationItem> _validationErrors = new();
    protected CancellationTokenSource? _currentOperationCts;
    private DateTimeOffset? _lastSavedAt = null;

    // Reflection caching for DataContext property lookups per derived type
    // This cache ensures reflection is performed only once per derived class type,
    // improving performance when multiple instances of the same panel are created.
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> DataContextPropertyCache = new();

    // Reflection caching for ThemeName property lookups per control type
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> ThemeNamePropertyCache = new();

    /// <summary>
    /// Gets or sets the ViewModel instance.
    /// If not set manually, it is resolved from the scoped service provider after the panel handle is created.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.DefaultValue(null)]
    public object? ViewModel
    {
        get => _viewModel;
        set => _viewModel = value;
    }

    /// <summary>
    /// ICompletablePanel: whether the panel completed initial load (ViewModel resolved and OnViewModelResolved executed).
    /// </summary>
    [Browsable(false)]
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// ICompletablePanel: whether the panel is performing a long-running operation.
    /// Automatically notifies subscribers of property and state changes when modified.
    /// </summary>
    [Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// ICompletablePanel: indicates unsaved changes tracked by the panel.
    /// </summary>
    [Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    protected void SetHasUnsavedChanges(bool value)
    {
        if (_hasUnsavedChanges == value) return;
        _hasUnsavedChanges = value;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// ICompletablePanel: aggregated validation state.
    /// </summary>
    [Browsable(false)]
    public bool IsValid => !_validationErrors.Any();

    public IReadOnlyList<ValidationItem> ValidationErrors => _validationErrors;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public PanelMode? Mode
    {
        get => _mode;
        protected set
        {
            if (_mode == value) return;
            _mode = value;
            OnPropertyChanged(nameof(Mode));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CancellationTokenSource? CurrentOperationCts => _currentOperationCts;

    public DateTimeOffset? LastSavedAt => _lastSavedAt;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? StateChanged;

    /// <summary>
    /// Raises the StateChanged event. Protected so derived classes can notify of state changes.
    /// </summary>
    protected void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Gets the logger instance for diagnostic logging.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets the scoped service provider for resolving additional dependencies.
    /// </summary>
    protected IServiceProvider? ServiceProvider => _scope?.ServiceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedPanelBase"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scopes to resolve scoped dependencies.</param>
    /// <param name="logger">The logger instance for diagnostic logging.</param>
    protected ScopedPanelBase(
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        if (GetType() == typeof(ScopedPanelBase))
        {
            throw new InvalidOperationException("ScopedPanelBase is not intended to be instantiated directly. Use a derived class instead.");
        }

        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <summary>
    /// Lightweight parameterless constructor intended for design-time (Visual Studio designer) instantiation.
    /// It provides a minimal, no-op IServiceScopeFactory and a null logger so the designer can create an instance
    /// without triggering runtime DI resolution. Heavy initialization is deferred to OnHandleCreated / OnHandleCreatedAsync.
    /// </summary>
    protected ScopedPanelBase()
    {
        _scopeFactory = new DesignTimeServiceScopeFactory();
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    // Minimal IServiceScopeFactory implementation for design-time to avoid requiring runtime DI in the designer.
    private sealed class DesignTimeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new DesignTimeServiceScope();

        private sealed class DesignTimeServiceScope : IServiceScope
        {
            // Provide an empty service provider to avoid null references; resolution will fail if attempted at design-time.
            public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Called when the panel handle is created. Creates a service scope and resolves the ViewModel.
    /// For heavy initialization, consider overriding OnHandleCreatedAsync() for non-blocking async work.
    /// </summary>
    /// <remarks>
    /// In designer mode, calls CreateDesignerViewModel() instead of using DI.
    /// On handle recreation, disposes of the old scope before creating a new one.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Caught and logged if the service provider is disposed during initialization.</exception>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Skip DI initialization in designer mode
        if (DesignMode)
        {
            try
            {
                _viewModel = CreateDesignerViewModel();
                if (_viewModel != null)
                {
                    TrySetDataContext(_viewModel);
                    OnViewModelResolved(_viewModel);
                    _logger.LogDebug("Initialized designer ViewModel for {PanelType}", GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to create designer ViewModel for {PanelType}", GetType().Name);
            }
            return;
        }

        if (_scope != null)
        {
            _logger.LogWarning("Handle recreated for {PanelType} - disposing old scope and creating new scope", GetType().Name);
            _scope.Dispose();
            _scope = null;
            _viewModel = null;
        }

        // Skip initialization if the control is being disposed or disposed
        if (Disposing || IsDisposed)
        {
            _logger.LogDebug("Skipping scope creation for {PanelType} - control is disposing/disposed", GetType().Name);
            return;
        }

        try
        {
            // Create scope for scoped services (DbContext, repositories, etc.)
            _scope = _scopeFactory.CreateScope();
            _logger.LogDebug("Created service scope for {PanelType}", GetType().Name);

            // Resolve ViewModel from scoped provider if not already set
            if (_viewModel == null)
            {
                _viewModel = ResolveViewModel(_scope.ServiceProvider);
                _logger.LogDebug("Resolved ViewModel from scoped provider for {PanelType}", GetType().Name);
            }
            else
            {
                _logger.LogDebug("Using manually assigned ViewModel for {PanelType}", GetType().Name);
            }

            // Populate DataContext (base + derived new DataContext via reflection)
            TrySetDataContext(_viewModel);

            // Ensure theme cascade reaches dynamically created panel and children
            ApplyThemeCascade();

            // Allow derived classes to perform additional initialization with the ViewModel
            OnViewModelResolved(_viewModel);

            // Subscribe to theme changes for runtime theme switching
            try
            {
                _themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeService>(_scope.ServiceProvider);
                if (_themeService != null)
                {
                    _themeService.ThemeChanged += OnThemeChanged;
                    _logger.LogDebug("Subscribed to theme change events for {PanelType}", GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to subscribe to theme change events for {PanelType}", GetType().Name);
            }

            // Mark panel as loaded for consumers (tests, automation, commands)
            _isLoaded = true;
            OnPropertyChanged(nameof(IsLoaded));
            StateChanged?.Invoke(this, EventArgs.Empty);

            // Defer heavy async initialization to avoid blocking UI thread
            BeginInvoke(new Action(() => _ = OnHandleCreatedAsyncSafe()));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "Service provider disposed during handle creation for {PanelType} - skipping ViewModel resolution", GetType().Name);
            // Don't throw - allow the panel to continue without ViewModel (common in test scenarios)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scope or resolve ViewModel for {PanelType}",
                GetType().Name);

            // Dispose partially created scope
            _scope?.Dispose();
            _scope = null;
            _viewModel = null;

            throw;
        }
    }

    /// <summary>
    /// Resolves the ViewModel from the service provider. Override in generic derived class.
    /// </summary>
    protected virtual object? ResolveViewModel(IServiceProvider serviceProvider)
    {
        return null;
    }

    /// <summary>
    /// Called to create a mock ViewModel for Visual Studio designer support.
    /// Override this method to provide design-time data for the designer.
    /// </summary>
    /// <returns>A mock ViewModel instance, or null if no designer support is needed.</returns>
    /// <remarks>
    /// This method is only called when DesignMode is true (i.e., in Visual Studio designer).
    /// Default implementation returns null, which skips ViewModel setup in the designer.
    /// Override to provide a lightweight mock ViewModel that displays sample data in the designer.
    /// Do NOT perform expensive operations (database queries, network calls) in this method;
    /// the designer may call it repeatedly, and it should return quickly.
    /// Example:
    /// <code>
    /// protected override object? CreateDesignerViewModel()
    /// {
    ///     return new MyViewModel
    ///     {
    ///         Items = new() { new Item { Id = 1, Name = "Sample Item" } },
    ///         IsLoading = false
    ///     };
    /// }
    /// </code>
    /// </remarks>
    protected virtual object? CreateDesignerViewModel()
    {
        return null;
    }

    /// <summary>
    /// Called after the ViewModel has been successfully resolved from the scoped service provider.
    /// Override this method to perform additional initialization logic with the ViewModel.
    /// </summary>
    /// <param name="viewModel">The resolved ViewModel instance.</param>
    protected virtual void OnViewModelResolved(object? viewModel)
    {
        // Default: no additional initialization
        // Derived classes can override to bind data, subscribe to events, etc.
    }

    /// <summary>
    /// Virtual async hook called after OnHandleCreated completes. Useful for heavy initialization (database queries, network calls) without blocking the UI thread.
    /// Override this method in derived classes to perform long-running operations after the ViewModel is resolved.
    /// This method fires asynchronously via BeginInvoke, so the panel is already displayed and responsive.
    /// </summary>
    /// <remarks>
    /// Default implementation is a no-op. Derived classes should override if they need to load data or perform expensive setup after the panel is visible.
    /// Exceptions in this method are logged but not thrown to prevent crashing the panel after display.
    /// </remarks>
    protected virtual Task OnHandleCreatedAsync()
    {
        return Task.CompletedTask;
    }

    private async Task OnHandleCreatedAsyncSafe()
    {
        try
        {
            await OnHandleCreatedAsync().ConfigureAwait(true);
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("OnHandleCreatedAsync canceled - control disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnHandleCreatedAsync failed for {PanelType}", GetType().Name);
        }
    }

    /// <summary>
    /// Virtual async validation hook. Override to perform server-side validation checks (e.g., checking for duplicate accounts).
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the validation operation.</param>
    /// <returns>
    /// A ValidationResult indicating whether validation succeeded and any errors found.
    /// Return ValidationResult.Success if validation passes; ValidationResult.Failed(...) if validation fails.
    /// </returns>
    /// <remarks>
    /// Default implementation returns ValidationResult.Success (no validation errors).
    /// Override this method to add async validation rules that require server-side checks.
    /// Client-side validation (UI field rules) should be handled in OnViewModelResolved via binding or property change handlers.
    /// Always use CancellationToken to support operation cancellation and timeouts.
    /// </remarks>
    public virtual Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        return Task.FromResult(ValidationResult.Success);
    }

    /// <summary>
    /// Virtual async validation hook with progress reporting. Override to perform server-side validation checks.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the validation operation.</param>
    /// <param name="progress">Optional progress reporter for tracking validation stages.</param>
    /// <returns>
    /// A ValidationResult indicating whether validation succeeded and any errors found.
    /// Return ValidationResult.Success if validation passes; ValidationResult.Failed(...) if validation fails.
    /// </returns>
    /// <remarks>
    /// Override this method in derived classes to report progress during validation (e.g., "Checking for duplicates...").
    /// If progress is null, report calls are safely ignored. Default implementation reports no progress.
    /// </remarks>
    public virtual Task<ValidationResult> ValidateAsync(CancellationToken ct, IProgress<string>? progress)
    {
        return ValidateAsync(ct);
    }

    /// <summary>
    /// Focus the first control referenced in the validation list and optionally scroll it into view.
    /// </summary>
    /// <remarks>
    /// If no validation errors exist, this method is a no-op.
    /// Useful in UI handlers to direct user attention to the first field with an error.
    /// </remarks>
    public virtual void FocusFirstError()
    {
        var item = _validationErrors.FirstOrDefault();
        item?.ControlRef?.Focus();
    }

    /// <summary>
    /// Default save hook - derived panels should override to persist data to the database or service.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the save operation.</param>
    /// <remarks>
    /// Override this method in derived panels to save ViewModel state (e.g., via repository or DbContext).
    /// Call StateChanged event after successful save. Use RegisterOperation(out ct) to track the operation.
    /// If save fails, log the error and consider adding validation errors for user feedback.
    /// </remarks>
    public virtual Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Save hook with progress reporting - derived panels can override to persist data with progress updates.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the save operation.</param>
    /// <param name="progress">Optional progress reporter for tracking save stages (e.g., "Saving account...", "Updating ledger...").param>
    /// <remarks>
    /// Override this method to report save progress to the UI (e.g., via a progress bar).
    /// If progress is null, report calls are safely ignored.
    /// Default implementation delegates to SaveAsync(ct) without progress reporting.
    /// </remarks>
    public virtual Task SaveAsync(CancellationToken ct, IProgress<string>? progress) => SaveAsync(ct);

    /// <summary>
    /// Default load hook - derived panels should override to fetch data from the database or service.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the load operation.</param>
    /// <remarks>
    /// Override this method in derived panels to load ViewModel state (e.g., from repository or DbContext).
    /// This is called automatically via OnHandleCreatedAsync, or manually by derived classes for refresh operations.
    /// If load fails, log the error and consider updating HasUnsavedChanges to false.
    /// </remarks>
    public virtual Task LoadAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Load hook with progress reporting - derived panels can override to fetch data with progress updates.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the load operation.</param>
    /// <param name="progress">Optional progress reporter for tracking load stages (e.g., "Loading accounts...", "Loading transactions...").param>
    /// <remarks>
    /// Override this method to report load progress to the UI (e.g., via a progress bar or spinner).
    /// If progress is null, report calls are safely ignored.
    /// Default implementation delegates to LoadAsync(ct) without progress reporting.
    /// Useful for panels with multiple data sources or large datasets that take time to retrieve.
    /// </remarks>
    public virtual Task LoadAsync(CancellationToken ct, IProgress<string>? progress) => LoadAsync(ct);

    /// <summary>
    /// Safe wrapper for fire-and-forget LoadAsync calls.
    /// Handles cross-thread exceptions and ensures UI operations are properly marshaled.
    /// Use this for background panel load calls that don't need to be awaited.
    /// </summary>
    /// <remarks>
    /// Exceptions are logged but not thrown, allowing the panel to remain functional even if loading fails.
    /// This is useful for non-critical background data loading after the panel is displayed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Silently caught and logged; does not propagate.</exception>
    protected async Task LoadAsyncSafe()
    {
        try
        {
            await LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("LoadAsyncSafe canceled - control disposed");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cross-thread"))
        {
            _logger.LogError(ex, "Cross-thread operation detected in LoadAsync - UI operation must be marshaled to UI thread");
            // Don't rethrow - this exception indicates a threading issue that should be fixed elsewhere
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadAsyncSafe failed for {PanelType}", GetType().Name);
        }
    }

    /// <summary>
    /// Safely invokes an action on the UI thread if required.
    /// Use this when updating UI controls from background threads in async operations.
    /// </summary>
    /// <param name="action">The action to invoke on the UI thread.</param>
    /// <remarks>
    /// If the current thread is already the UI thread (InvokeRequired is false), the action is executed directly.
    /// If a different thread, the action is marshaled to the UI thread using Invoke().
    /// ObjectDisposedException is silently handled if the control handle has been disposed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the control handle is invalid or disposed during marshaling.</exception>
    protected void InvokeOnUiThread(Action action)
    {
        if (InvokeRequired)
        {
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("InvokeOnUiThread canceled - control disposed");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Cross-thread"))
            {
                _logger.LogError(ex, "Cross-thread operation in InvokeOnUiThread");
            }
        }
        else
        {
            action?.Invoke();
        }
    }

    /// <summary>
    /// Helper to register a new operation token; cancels any previous operation.
    /// Use this when starting a long-running async operation to ensure only one operation runs at a time.
    /// </summary>
    /// <returns>A CancellationToken that can be passed to async methods to support cancellation.</returns>
    protected CancellationToken RegisterOperation()
    {
        CancelCurrentOperation();
        _currentOperationCts = new CancellationTokenSource();
        return _currentOperationCts.Token;
    }

    protected void CancelCurrentOperation()
    {
        try
        {
            if (_currentOperationCts != null)
            {
                _currentOperationCts.Cancel();
                _currentOperationCts.Dispose();
                _currentOperationCts = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error cancelling current operation for {PanelType}", GetType().Name);
        }
    }

    /// <summary>
    /// Adds a validation error to the panel's error list and notifies subscribers of validation state changes.
    /// Automatically triggers OnPropertyChanged for IsValid and ValidationErrors, and raises StateChanged event.
    /// </summary>
    /// <param name="error">The ValidationItem to add.</param>
    protected void AddValidationError(ValidationItem error)
    {
        if (error is null) return;
        _validationErrors.Add(error);
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationErrors));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears all validation errors from the panel and notifies subscribers of validation state changes.
    /// Automatically triggers OnPropertyChanged for IsValid and ValidationErrors, and raises StateChanged event.
    /// </summary>
    protected void ClearValidationErrors()
    {
        if (_validationErrors.Count == 0) return;
        _validationErrors.Clear();
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationErrors));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes validation errors for a specific field by field name and notifies subscribers of validation state changes.
    /// Automatically triggers OnPropertyChanged for IsValid and ValidationErrors, and raises StateChanged event.
    /// </summary>
    /// <param name="fieldName">The name of the field whose errors should be removed.</param>
    protected void RemoveValidationError(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName) || !_validationErrors.Any(e => e.FieldName == fieldName)) return;
        _validationErrors.RemoveAll(e => e.FieldName == fieldName);
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationErrors));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Replaces all validation errors with the provided collection and notifies subscribers of validation state changes.
    /// Automatically triggers OnPropertyChanged for IsValid and ValidationErrors, and raises StateChanged event.
    /// </summary>
    /// <param name="errors">The collection of ValidationItems to set. If null or empty, clears all errors.</param>
    protected void SetValidationErrors(IEnumerable<ValidationItem>? errors)
    {
        _validationErrors.Clear();
        if (errors != null)
        {
            _validationErrors.AddRange(errors);
        }
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationErrors));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes the service scope and releases all managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(); false if called from finalizer.</param>
    /// <remarks>
    /// Cancels any running operations, disposes the ViewModel if it implements IDisposable, and disposes the service scope.
    /// Safe to call multiple times; subsequent calls are no-ops.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">May be thrown by underlying resources during disposal, but is caught and logged.</exception>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _logger.LogDebug("Disposing scope for {PanelType}", GetType().Name);

                // Unsubscribe from theme change events to prevent leaks
                if (_themeService != null)
                {
                    try
                    {
                        _themeService.ThemeChanged -= OnThemeChanged;
                        _logger.LogDebug("Unsubscribed from theme change events for {PanelType}", GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error unsubscribing from theme change events for {PanelType}", GetType().Name);
                    }
                    _themeService = null;
                }

                // Cancel any running operations (async work)
                CancelCurrentOperation();

                // Dispose ViewModel if it implements IDisposable
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }

                // Dispose service scope (releases DbContext and other scoped services)
                _scope?.Dispose();
                _scope = null;
                _viewModel = null;
                _dataContext = null;
            }

            _disposed = true;
        }

        // Guard against disposal during handle creation (can happen when DockingManager disposes during form lifecycle)
        // If handle is being created, defer disposal to avoid InvalidOperationException
        try
        {
            base.Dispose(disposing);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Dispose() cannot be called while doing CreateHandle()"))
        {
            _logger.LogDebug("Deferred disposal during CreateHandle for {PanelType}", GetType().Name);
            // Disposal will complete when handle creation finishes
        }
    }

    /// <summary>
    /// Attempts to set the DataContext property on this panel to the ViewModel instance (if a DataContext property exists).
    /// </summary>
    /// <param name="viewModel">The ViewModel instance to set as DataContext, or null to clear.</param>
    /// <remarks>
    /// Uses reflection to locate a public or private DataContext property on the derived panel type.
    /// Results are cached per derived type to avoid repeated reflection calls.
    /// If no DataContext property exists or is read-only, this method is silently skipped (no exception thrown).
    /// This enables XAML or binding scenarios where panels expose a typed DataContext property.
    /// </remarks>
    private void TrySetDataContext(object? viewModel)
    {
        _dataContext = viewModel;

        if (viewModel is null)
        {
            return;
        }

        try
        {
            // Use static cache to avoid repeated reflection per derived type
            var cachedProperty = DataContextPropertyCache.GetOrAdd(
                GetType(),
                type => type.GetProperty("DataContext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

            if (cachedProperty?.CanWrite == true)
            {
                cachedProperty.SetValue(this, viewModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to set DataContext for {PanelType}", GetType().Name);
        }
    }

    /// <summary>
    /// Applies the current SfSkinManager theme cascade to this panel and all child controls recursively.
    /// Called automatically during OnHandleCreated, but can also be invoked manually when the theme changes at runtime.
    /// </summary>
    /// <remarks>
    /// SfSkinManager is the single source of truth for theming in this application.
    /// This method ensures all Syncfusion controls and children inherit the active theme.
    /// Best-effort approach: controls that don't support theming are silently skipped.
    /// </remarks>
    private void ApplyThemeCascade()
    {
        try
        {
            // Get current application theme - SFSkinManager cascade should handle this automatically
            // Only apply explicitly if control was created after theme initialization
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(this, currentTheme);
            ApplyThemeRecursively(this, currentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Theme cascade skipped for {PanelType}", GetType().Name);
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Handles theme change notifications from the application theme service.
    /// Re-applies the current theme cascade to this panel and all children.
    /// </summary>
    private void OnThemeChanged(object? sender, string themeName)
    {
        try
        {
            InvokeOnUiThread(() =>
            {
                _logger.LogDebug("Theme changed to {ThemeName} for {PanelType} - reapplying theme cascade", themeName, GetType().Name);
                OnThemeChanged(themeName);
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error handling theme change for {PanelType}", GetType().Name);
        }
    }

    /// <summary>
    /// Virtual method called when the theme changes at runtime.
    /// Override in derived classes to perform custom theme-related updates.
    /// </summary>
    /// <param name="themeName">The new theme name.</param>
    protected virtual void OnThemeChanged(string themeName)
    {
        ApplyThemeCascade();
    }

    private static void ApplyThemeRecursively(Control control, string themeName)
    {
        try
        {
            TrySetThemeName(control, themeName);
            SfSkinManager.SetVisualStyle(control, themeName);
        }
        catch
        {
            // Best-effort only; some controls may not support theming directly.
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeRecursively(child, themeName);
        }
    }

    private static void TrySetThemeName(Control control, string themeName)
    {
        try
        {
            var themeNameProperty = ThemeNamePropertyCache.GetOrAdd(
                control.GetType(),
                type => type.GetProperty("ThemeName", BindingFlags.Instance | BindingFlags.Public));

            if (themeNameProperty?.CanWrite == true && themeNameProperty.PropertyType == typeof(string))
            {
                themeNameProperty.SetValue(control, themeName);
            }
        }
        catch
        {
            // Best-effort only; ignore controls without ThemeName support.
        }
    }

    #region ISupportInitialize Implementation
    /// <summary>
    /// Signals the object that initialization is starting.
    /// </summary>
    public void BeginInit()
    {
        // No special initialization needed
    }

    /// <summary>
    /// Signals the object that initialization is complete.
    /// </summary>
    public void EndInit()
    {
        // No special initialization needed
    }
    #endregion
}

/// <summary>
/// Generic base class for panels that require scoped ViewModels with dependencies on scoped services (e.g., DbContext, repositories).
/// Handles proper scope creation, ViewModel resolution, and disposal to prevent DI lifetime violations.
/// </summary>
/// <remarks>
/// <para>
/// This class manages the complete lifecycle of a scoped DI container for each panel instance:
/// 1. <strong>Scope Creation:</strong> Created in OnHandleCreated (when WinForms handle is created) to tie lifecycle to control's lifetime.
/// 2. <strong>ViewModel Resolution:</strong> TViewModel is resolved from the scoped provider; if not manually assigned, resolution is automatic.
/// 3. <strong>Initialization Hook:</strong> OnViewModelResolved is called for derived classes to perform custom initialization.
/// 4. <strong>Async Initialization:</strong> OnHandleCreatedAsync allows heavy operations (DB queries, network calls) without blocking the UI thread.
/// 5. <strong>Theme Management:</strong> Automatically applies SfSkinManager theme cascade to this panel and all children.
/// 6. <strong>Disposal:</strong> Scope is disposed in Dispose, releasing all scoped services including DbContext.
/// </para>
/// <para>
/// Implements ICompletablePanel to support state tracking (IsLoaded, IsBusy, HasUnsavedChanges, IsValid, ValidationErrors).
/// Integrates with IThemeService for runtime theme switching without requiring panel reload.
/// Uses reflection caching to optimize DataContext property lookups per derived type.
/// </para>
/// <para>
/// Best Practices:
/// - Keep constructors lightweight; defer heavy initialization to OnHandleCreatedAsync.
/// - Use AddValidationError, ClearValidationErrors, and similar helpers for consistent validation management.
/// - Subscribe to ViewModel events in OnViewModelResolved; unsubscribe in Dispose to prevent leaks.
/// - Use InvokeOnUiThread when updating UI from background threads to ensure thread safety.
/// - Use RegisterOperation to manage concurrent async operations; only one operation can run at a time.
/// </para>
/// </remarks>
/// <typeparam name="TViewModel">The ViewModel type to resolve from the scoped service provider.</typeparam>
public class ScopedPanelBase<TViewModel> : ScopedPanelBase
    where TViewModel : class
{
    /// <summary>
    /// Gets or sets the ViewModel instance.
    /// If not set manually, it is resolved from the scoped service provider after the panel handle is created.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.DefaultValue(null)]
    public new TViewModel? ViewModel
    {
        get => (TViewModel?)base.ViewModel;
        set => base.ViewModel = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedPanelBase{TViewModel}"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scopes to resolve scoped dependencies.</param>
    /// <param name="logger">The logger instance for diagnostic logging.</param>
    protected ScopedPanelBase(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<TViewModel>>? logger = null)
        : base(scopeFactory, logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ScopedPanelBase<TViewModel>>.Instance)
    {
        if (GetType() == typeof(ScopedPanelBase<TViewModel>))
        {
            throw new InvalidOperationException("ScopedPanelBase<TViewModel> is not intended to be instantiated directly. Use a derived class instead.");
        }
    }

    /// <summary>
    /// Lightweight parameterless constructor intended for design-time (Visual Studio designer) instantiation.
    /// It provides a minimal, no-op IServiceScopeFactory and a null logger so the designer can create an instance
    /// without triggering runtime DI resolution. Heavy initialization is deferred to OnHandleCreated / OnHandleCreatedAsync.
    /// </summary>
    protected ScopedPanelBase()
        : base()
    {
    }

    /// <summary>
    /// Resolves the ViewModel from the service provider.
    /// </summary>
    protected override object? ResolveViewModel(IServiceProvider serviceProvider)
    {
        return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TViewModel>(serviceProvider);
    }

    /// <summary>
    /// Called to create a mock ViewModel for Visual Studio designer support.
    /// Override this method to provide design-time data for the designer.
    /// </summary>
    /// <returns>A mock ViewModel instance, or null if no designer support is needed.</returns>
    /// <remarks>
    /// This method is only called when DesignMode is true (i.e., in Visual Studio designer).
    /// Default implementation returns null, which skips ViewModel setup in the designer.
    /// Override to provide a lightweight mock ViewModel that displays sample data in the designer.
    /// Do NOT perform expensive operations (database queries, network calls) in this method;
    /// the designer may call it repeatedly, and it should return quickly.
    /// Example:
    /// <code>
    /// protected override TViewModel? CreateDesignerViewModel()
    /// {
    ///     return new MyViewModel
    ///     {
    ///         Items = new() { new Item { Id = 1, Name = "Sample Item" } },
    ///         IsLoading = false
    ///     };
    /// }
    /// </code>
    /// </remarks>
    protected override TViewModel? CreateDesignerViewModel()
    {
        return null;
    }

    /// <summary>
    /// Called after the ViewModel has been successfully resolved from the scoped service provider.
    /// Override this method to perform additional initialization logic with the ViewModel.
    /// </summary>
    /// <param name="viewModel">The resolved ViewModel instance.</param>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is TViewModel typedViewModel)
        {
            OnViewModelResolved(typedViewModel);
        }
    }

    /// <summary>
    /// Called after the ViewModel has been successfully resolved from the scoped service provider.
    /// Override this method to perform additional initialization logic with the ViewModel.
    /// </summary>
    /// <param name="viewModel">The resolved ViewModel instance.</param>
    protected virtual void OnViewModelResolved(TViewModel viewModel)
    {
        // Default: no additional initialization
        // Derived classes can override to bind data, subscribe to events, etc.
    }
}
