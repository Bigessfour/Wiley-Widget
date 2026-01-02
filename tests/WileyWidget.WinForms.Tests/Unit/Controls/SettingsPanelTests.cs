using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Controls;

/// <summary>
/// Unit tests for SettingsPanel validating ScopedPanelBase architecture, disposal, and ViewModel binding.
/// Tests verify proper DI lifecycle, SafeDispose patterns, and OnViewModelResolved implementation.
/// </summary>
[Collection("WinForms")]
public class SettingsPanelTests : PanelTestBase<SettingsPanel, SettingsViewModel>
{
    private readonly Mock<IThemeService> _mockThemeService = new Mock<IThemeService>();
    private readonly Mock<ILogger<SettingsViewModel>> _mockViewModelLogger = new Mock<ILogger<SettingsViewModel>>();

    public SettingsPanelTests()
    {
    }

    protected override Mock<SettingsViewModel> CreateViewModelMock()
    {
        // SettingsViewModel requires ILogger<SettingsViewModel> and optional ISettingsService
        // Pass null for ISettingsService since it's optional in the constructor
        return new Mock<SettingsViewModel>(_mockViewModelLogger.Object, null);
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);

        // Register SettingsPanel-specific dependencies
        services.AddSingleton(_mockThemeService.Object);
    }

    protected override SettingsPanel CreatePanel()
    {
        return new SettingsPanel(
            MockScopeFactory.Object,
            MockLogger.Object,
            _mockThemeService.Object);
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => CreatePanel());
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SettingsPanel(
                null!,
                MockLogger.Object,
                _mockThemeService.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SettingsPanel(
                MockScopeFactory.Object,
                null!,
                _mockThemeService.Object));
    }

    [Fact]
    public void Constructor_WithNullThemeService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SettingsPanel(
                MockScopeFactory.Object,
                MockLogger.Object,
                null!));
    }

    [Fact]
    public void Panel_AfterCreation_HasCorrectName()
    {
        // Act
        var panel = CreateAndInitializePanel();

        // Assert
        Assert.Equal("SettingsPanel", panel.Name);
    }

    [Fact]
    public void Panel_Initialization_SubscribesToThemeService()
    {
        // Arrange
        _mockThemeService.SetupAdd(s => s.ThemeChanged += It.IsAny<EventHandler<AppTheme>>());

        // Act
        var panel = CreateAndInitializePanel();

        // Assert
        _mockThemeService.VerifyAdd(s => s.ThemeChanged += It.IsAny<EventHandler<AppTheme>>(), Times.Once);
    }

    [Fact]
    public void Panel_Dispose_UnsubscribesFromThemeService()
    {
        // Arrange
        _mockThemeService.SetupRemove(s => s.ThemeChanged -= It.IsAny<EventHandler<AppTheme>>());
        var panel = CreateAndInitializePanel();

        // Act
        panel.Dispose();

        // Assert
        _mockThemeService.VerifyRemove(s => s.ThemeChanged -= It.IsAny<EventHandler<AppTheme>>(), Times.Once);
    }

    [Fact]
    public void Panel_MultipleDisposeCalls_DoesNotThrow()
    {
        // Arrange
        var panel = CreateAndInitializePanel();

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            panel.Dispose();
            panel.Dispose();
            panel.Dispose();
        });

        Assert.Null(exception);
    }

    [Fact(Skip = "Requires STA thread and full WinForms initialization")]
    public void Panel_LoadSettingsCommand_PopulatesViewModel()
    {
        // This test requires full WinForms initialization and STA thread
        // Will be tested in E2E tests instead
    }

    [Fact(Skip = "Requires STA thread and full WinForms initialization")]
    public void Panel_SaveSettingsCommand_PersistsChanges()
    {
        // This test requires full WinForms initialization and STA thread
        // Will be tested in E2E tests instead
    }
}
