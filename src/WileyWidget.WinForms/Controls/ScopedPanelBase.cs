using System;
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

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Abstract base class for panels that require scoped ViewModels with dependencies on scoped services (e.g., DbContext, repositories).
/// Handles proper scope creation, ViewModel resolution, and disposal to prevent DI lifetime violations.
/// </summary>
/// <typeparam name="TViewModel">The ViewModel type to resolve from the scoped service provider.</typeparam>
public abstract class ScopedPanelBase<TViewModel> : UserControl, ICompletablePanel, INotifyPropertyChanged
    where TViewModel : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    protected readonly ILogger<ScopedPanelBase<TViewModel>> _logger;
    private IServiceScope? _scope;
    protected TViewModel? _viewModel;
    private object? _dataContext;
    private bool _disposed;

    // ICompletablePanel backing fields
    protected bool _isLoaded;
    protected bool _isBusy;
    protected bool _hasUnsavedChanges;
    protected PanelMode? _mode = PanelMode.View;
    protected readonly List<ValidationItem> _validationErrors = new();
    protected CancellationTokenSource? _currentOperationCts;
    private DateTimeOffset? _lastSavedAt = null;

    /// <summary>
    /// Gets or sets the ViewModel instance.
    /// If not set manually, it is resolved from the scoped service provider after the panel handle is created.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TViewModel? ViewModel
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
    /// Initializes a new instance of the <see cref="ScopedPanelBase{TViewModel}"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scopes to resolve scoped dependencies.</param>
    /// <param name="logger">The logger instance for diagnostic logging.</param>
    protected ScopedPanelBase(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<TViewModel>>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ScopedPanelBase<TViewModel>>.Instance;
    }

    /// <summary>
    /// Called when the panel handle is created. Creates a service scope and resolves the ViewModel.
    /// </summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_scope != null)
        {
            _logger.LogWarning("Scope already exists in OnHandleCreated - handle may have been recreated");
            return;
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
                _viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TViewModel>(_scope.ServiceProvider);
                _logger.LogDebug("Resolved {ViewModelType} from scoped provider", typeof(TViewModel).Name);
            }
            else
            {
                _logger.LogDebug("Using manually assigned {ViewModelType} for {PanelType}", typeof(TViewModel).Name, GetType().Name);
            }

            // Populate DataContext (base + derived new DataContext via reflection)
            TrySetDataContext(_viewModel);

            // Ensure theme cascade reaches dynamically created panel and children
            ApplyThemeCascade();

            // Allow derived classes to perform additional initialization with the resolved ViewModel
            OnViewModelResolved(_viewModel);

            // Mark panel as loaded for consumers (tests, automation, commands)
            _isLoaded = true;
            OnPropertyChanged(nameof(IsLoaded));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "Service provider disposed during handle creation for {PanelType} - skipping ViewModel resolution", GetType().Name);
            // Don't throw - allow the panel to continue without ViewModel (common in test scenarios)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scope or resolve {ViewModelType} for {PanelType}",
                typeof(TViewModel).Name, GetType().Name);

            // Dispose partially created scope
            _scope?.Dispose();
            _scope = null;
            _viewModel = null;

            throw;
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

    /// <summary>
    /// Default (no-op) async validation hook. Panels may override to perform server-side checks.
    /// </summary>
    public virtual Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        return Task.FromResult(ValidationResult.Success);
    }

    /// <summary>
    /// Focus the first control referenced in the validation list.
    /// </summary>
    public virtual void FocusFirstError()
    {
        var item = _validationErrors.FirstOrDefault();
        item?.ControlRef?.Focus();
    }

    /// <summary>
    /// Default save/load hooks - derived panels should implement real logic.
    /// </summary>
    public virtual Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    public virtual Task LoadAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Helper to register a new operation token; cancels any previous operation.
    /// </summary>
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
    /// Disposes the service scope and releases all managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(); false if called from finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _logger.LogDebug("Disposing scope for {PanelType}", GetType().Name);

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

        base.Dispose(disposing);
    }

    private void TrySetDataContext(TViewModel? viewModel)
    {
        _dataContext = viewModel;

        if (viewModel is null)
        {
            return;
        }

        try
        {
            var dataContextProperty = GetType().GetProperty("DataContext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dataContextProperty?.CanWrite == true)
            {
                dataContextProperty.SetValue(this, viewModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to set DataContext for {PanelType}", GetType().Name);
        }
    }

    private void ApplyThemeCascade()
    {
        try
        {
            // Get current application theme - SkinManager cascade should handle this automatically
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

    private static void ApplyThemeRecursively(Control control, string themeName)
    {
        try
        {
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
}
