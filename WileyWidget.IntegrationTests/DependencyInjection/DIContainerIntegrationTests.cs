using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Data;
using WileyWidget.WinForms.Forms;
using System.IO;

namespace WileyWidget.IntegrationTests.DependencyInjection;

/// <summary>
/// Integration tests that verify the complete DI container setup.
/// These tests mirror the actual startup procedure to ensure all services
/// can be constructed together without errors.
/// </summary>
public class DIContainerIntegrationTests
{
    private IConfiguration CreateWinFormsConfiguration()
    {
        // Use the actual appsettings.json from WinForms project
        var basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "WileyWidget.WinForms");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables();

        return configBuilder.Build();
    }

    [Fact(DisplayName = "DI Container should build successfully with all services")]
    public void DIContainer_ShouldBuildSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();

        // Act - Call the actual DI configuration method
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Assert - If we get here without exception, DI is properly configured
        Assert.NotNull(provider);
    }

    [Fact(DisplayName = "HealthCheckService should be resolvable from DI container")]
    public void HealthCheckService_ShouldBeResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetService<HealthCheckService>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact(DisplayName = "ISettingsService should be resolvable")]
    public void ISettingsService_ShouldBeResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetService<ISettingsService>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact(DisplayName = "ISecretVaultService should be resolvable")]
    public void ISecretVaultService_ShouldBeResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetService<ISecretVaultService>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact(DisplayName = "All core services should be resolvable")]
    public void CoreServices_ShouldAllBeResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        var serviceTypes = new[]
        {
            typeof(HealthCheckService),
            typeof(ISettingsService),
            typeof(ISecretVaultService),
            typeof(IAIService),
            typeof(IAILoggingService),
            typeof(IAuditService)
        };

        // Act & Assert
        foreach (var serviceType in serviceTypes)
        {
            var service = provider.GetService(serviceType);
            Assert.NotNull(service);
        }
    }

    [Fact(DisplayName = "DbContext should be resolvable as scoped service")]
    public void DbContext_ShouldBeResolvableAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Act
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var dbContext1 = scope1.ServiceProvider.GetService<AppDbContext>();
        var dbContext2 = scope2.ServiceProvider.GetService<AppDbContext>();

        // Assert
        Assert.NotNull(dbContext1);
        Assert.NotNull(dbContext2);
        Assert.NotSame(dbContext1, dbContext2); // Different instances per scope
    }

    [Fact(DisplayName = "HealthCheckService should be singleton")]
    public void HealthCheckService_ShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Act
        var service1 = provider.GetRequiredService<HealthCheckService>();
        var service2 = provider.GetRequiredService<HealthCheckService>();

        // Assert - Should be same instance (singleton behavior)
        Assert.Same(service1, service2);
    }

    [Fact(DisplayName = "MainForm should be resolvable")]
    public void MainForm_ShouldBeResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateWinFormsConfiguration();
        WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var mainForm = scope.ServiceProvider.GetService<MainForm>();

        // Assert
        Assert.NotNull(mainForm);
    }
}
