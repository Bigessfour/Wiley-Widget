using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Controls;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Controls;

/// <summary>
/// Base class for panel unit tests providing common infrastructure, mocked dependencies, and test utilities.
/// Supports ScopedPanelBase testing with proper DI scoping, ViewModel mocking, and lifecycle validation.
/// </summary>
/// <typeparam name="TPanel">The panel type being tested (must inherit from UserControl)</typeparam>
/// <typeparam name="TViewModel">The ViewModel type for the panel</typeparam>
public abstract class PanelTestBase<TPanel, TViewModel> : IDisposable
    where TPanel : ScopedPanelBase<TViewModel>
    where TViewModel : class
{
    protected ServiceCollection Services { get; }
    protected ServiceProvider ServiceProvider { get; private set; }
    protected Mock<IServiceScopeFactory> MockScopeFactory { get; }
    protected Mock<ILogger<ScopedPanelBase<TViewModel>>> MockLogger { get; }
    protected Mock<TViewModel> MockViewModel { get; }
    protected TPanel? Panel { get; set; }

    private IServiceScope? _currentScope;
    private bool _disposed;

    /// <summary>
    /// Initializes base test infrastructure with mocked dependencies.
    /// Override ConfigureServices to add panel-specific mocks.
    /// </summary>
    protected PanelTestBase()
    {
        Services = new ServiceCollection();
        MockScopeFactory = new Mock<IServiceScopeFactory>();
        MockLogger = new Mock<ILogger<ScopedPanelBase<TViewModel>>>();
        MockViewModel = CreateViewModelMock();

        // Configure default scope behavior
        _currentScope = CreateMockScope();
        MockScopeFactory.Setup(f => f.CreateScope())
            .Returns(() => CreateMockScope());

        // Add common services
        Services.AddLogging();
        Services.AddScoped(_ => MockViewModel.Object);

        // Allow derived classes to configure additional services
        ConfigureServices(Services);

        ServiceProvider = Services.BuildServiceProvider();
    }

    /// <summary>
    /// Override to register panel-specific services and mocks.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    protected virtual void ConfigureServices(ServiceCollection services)
    {
        // Derived classes override to add specific mocks
    }

    /// <summary>
    /// Creates a mock for the ViewModel. Override this method in derived classes
    /// if the ViewModel has constructor parameters that need to be provided.
    /// Default implementation attempts to create a parameterless mock.
    /// </summary>
    /// <returns>Mock instance of the ViewModel</returns>
    protected virtual Mock<TViewModel> CreateViewModelMock()
    {
        // Try to create a mock without parameters first (for simple ViewModels)
        try
        {
            return new Mock<TViewModel>();
        }
        catch (ArgumentException)
        {
            // ViewModel has constructor parameters - derived test must override CreateViewModelMock
            throw new InvalidOperationException(
                $"Cannot create mock for {typeof(TViewModel).Name} because it requires constructor arguments. " +
                $"Override CreateViewModelMock() in your test class to provide the required constructor arguments.");
        }
    }

    /// <summary>
    /// Creates a mock service scope with the test service provider.
    /// </summary>
    private IServiceScope CreateMockScope()
    {
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(ServiceProvider);
        return mockScope.Object;
    }

    /// <summary>
    /// Creates and initializes the panel with mocked dependencies.
    /// Override CreatePanel to provide panel-specific constructor parameters.
    /// </summary>
    protected virtual TPanel CreateAndInitializePanel()
    {
        Panel = CreatePanel();

        // Trigger ViewModel resolution by creating the handle
        var handle = Panel.Handle; // Force handle creation to trigger OnViewModelResolved

        return Panel;
    }

    /// <summary>
    /// Override to create the panel with specific constructor parameters.
    /// Default implementation uses IServiceScopeFactory and ILogger.
    /// </summary>
    protected abstract TPanel CreatePanel();

    /// <summary>
    /// Verifies the panel properly inherits from ScopedPanelBase.
    /// </summary>
    [Fact]
    public void Panel_ShouldInheritFrom_ScopedPanelBase()
    {
        // Act
        var panel = CreateAndInitializePanel();

        // Assert
        Assert.IsAssignableFrom<ScopedPanelBase<TViewModel>>(panel);
    }

    /// <summary>
    /// Verifies the panel has a valid ViewModel after initialization.
    /// </summary>
    [Fact]
    public virtual void Panel_AfterInitialization_ShouldHaveViewModel()
    {
        // Act
        var panel = CreateAndInitializePanel();

        // Assert
        Assert.NotNull(panel.GetViewModelForTesting());
    }

    /// <summary>
    /// Verifies the panel disposes properly without throwing exceptions.
    /// </summary>
    [Fact]
    public void Panel_Dispose_ShouldNotThrow()
    {
        // Arrange
        var panel = CreateAndInitializePanel();

        // Act & Assert
        var exception = Record.Exception(() => panel.Dispose());
        Assert.Null(exception);
    }

    /// <summary>
    /// Disposes test resources including the panel and service provider.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                Panel?.Dispose();
            }
            catch
            {
                // Suppress disposal exceptions in tests
            }

            _currentScope?.Dispose();
            ServiceProvider?.Dispose();
        }

        _disposed = true;
    }
}
