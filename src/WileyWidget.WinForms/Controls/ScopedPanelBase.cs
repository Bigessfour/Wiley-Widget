using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Abstract base class for panels that require scoped ViewModels with dependencies on scoped services (e.g., DbContext, repositories).
/// Handles proper scope creation, ViewModel resolution, and disposal to prevent DI lifetime violations.
/// </summary>
/// <typeparam name="TViewModel">The ViewModel type to resolve from the scoped service provider.</typeparam>
public abstract class ScopedPanelBase<TViewModel> : UserControl
    where TViewModel : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScopedPanelBase<TViewModel>> _logger;
    private IServiceScope? _scope;
    private TViewModel? _viewModel;
    private bool _disposed;

    /// <summary>
    /// Gets the ViewModel instance resolved from the scoped service provider.
    /// Available after the panel handle is created.
    /// </summary>
    protected TViewModel? ViewModel => _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedPanelBase{TViewModel}"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scopes to resolve scoped dependencies.</param>
    /// <param name="logger">The logger instance for diagnostic logging.</param>
    protected ScopedPanelBase(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<TViewModel>> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        try
        {
            // Create scope for scoped services (DbContext, repositories, etc.)
            _scope = _scopeFactory.CreateScope();
            _logger.LogDebug("Created service scope for {PanelType}", GetType().Name);

            // Resolve ViewModel from scoped provider
            _viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TViewModel>(_scope.ServiceProvider);
            _logger.LogDebug("Resolved {ViewModelType} from scoped provider", typeof(TViewModel).Name);

            // Allow derived classes to perform additional initialization with the resolved ViewModel
            OnViewModelResolved(_viewModel);
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

                // Dispose ViewModel if it implements IDisposable
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }

                // Dispose service scope (releases DbContext and other scoped services)
                _scope?.Dispose();
                _scope = null;
                _viewModel = null;
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
