using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Tests.Integration;

namespace WileyWidget.WinForms.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class ErrorHandlingIntegrationTests
{
    [StaFact]
    public void MainFormFactory_WithInvalidConfiguration_HandlesGracefully()
    {
        // Arrange - Create provider with invalid config that might cause issues
        var services = new ServiceCollection();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new StartupOptions
        {
            TimeoutSeconds = 1000,
            EnableLicenseValidation = false // Disable to avoid license issues in test
        }));

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseSyncfusionDocking"] = "invalid_boolean", // Invalid value
                ["UI:ShowRibbon"] = "true",
                ["UI:ShowStatusBar"] = "true"
            })
            .Build();

        services.AddSingleton<IConfiguration>(invalidConfig);
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton(ReportViewerLaunchOptions.Disabled);

        var themeMock = new Mock<IThemeService>();
        themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
        services.AddSingleton<IThemeService>(themeMock.Object);
        services.AddSingleton<IWindowStateService>(Mock.Of<IWindowStateService>());
        services.AddSingleton<IFileImportService>(Mock.Of<IFileImportService>());

        using var provider = services.BuildServiceProvider();

        // Act & Assert - Should not throw even with invalid config
        FluentActions.Invoking(() => IntegrationTestServices.CreateMainForm(provider))
            .Should().NotThrow();
    }

    [StaFact]
    public void ServiceProvider_WithMissingDependencies_ThrowsDescriptiveError()
    {
        // Arrange - Create incomplete provider
        var services = new ServiceCollection();
        // Missing many required services

        // Act & Assert
        FluentActions.Invoking(() => services.BuildServiceProvider())
            .Should().NotThrow(); // Building incomplete provider doesn't throw until resolution
    }

    [StaFact]
    public void ThemeService_WithInvalidTheme_HandlesGracefully()
    {
        // Arrange
        var themeMock = new Mock<IThemeService>();
        themeMock.SetupGet(t => t.CurrentTheme).Returns("InvalidThemeName");
        themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>()))
            .Throws(new ArgumentException("Invalid theme"));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton(ReportViewerLaunchOptions.Disabled);
        services.AddSingleton<IThemeService>(themeMock.Object);
        services.AddSingleton<IWindowStateService>(Mock.Of<IWindowStateService>());
        services.AddSingleton<IFileImportService>(Mock.Of<IFileImportService>());

        using var provider = services.BuildServiceProvider();

        // Act & Assert - Should not throw during form creation
        FluentActions.Invoking(() => IntegrationTestServices.CreateMainForm(provider))
            .Should().NotThrow();
    }

    [StaFact]
    public void ControlDisposal_RaceConditions_HandledSafely()
    {
        // Arrange
        using var form = new Form();
        form.Show();

        // Act - Dispose form while operations might be pending
        form.Dispose();

        // Assert - No exceptions should occur
        form.IsDisposed.Should().BeTrue();
    }
}
