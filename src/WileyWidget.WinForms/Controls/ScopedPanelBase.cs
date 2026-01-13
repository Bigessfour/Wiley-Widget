using System;
using System.Reflection;
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
public abstract class ScopedPanelBase<TViewModel> : UserControl
    where TViewModel : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    protected readonly ILogger<ScopedPanelBase<TViewModel>> _logger;
    private IServiceScope? _scope;
    protected TViewModel? _viewModel;
    private object? _dataContext;
    private bool _disposed;

    /// <summary>
    /// Gets the ViewModel instance resolved from the scoped service provider.
    /// Available after the panel handle is created.
    /// </summary>
    protected TViewModel? ViewModel => _viewModel;

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

            // Resolve ViewModel from scoped provider
            _viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TViewModel>(_scope.ServiceProvider);
            _logger.LogDebug("Resolved {ViewModelType} from scoped provider", typeof(TViewModel).Name);

            // Populate DataContext (base + derived new DataContext via reflection)
            TrySetDataContext(_viewModel);

            // Ensure theme cascade reaches dynamically created panel and children
            ApplyThemeCascade();

            // Allow derived classes to perform additional initialization with the resolved ViewModel
            OnViewModelResolved(_viewModel);
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
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            SfSkinManager.SetVisualStyle(this, ThemeColors.DefaultTheme);
            ApplyThemeRecursively(this, ThemeColors.DefaultTheme);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Theme cascade skipped for {PanelType}", GetType().Name);
        }
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
