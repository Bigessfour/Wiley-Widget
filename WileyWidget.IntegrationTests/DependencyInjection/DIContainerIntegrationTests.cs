using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // Resolve the project folder for WileyWidget.WinForms. Tests may run from bin folders,
        // so walk upward until we find a folder that contains WileyWidget.WinForms or the solution file.
        string current = Directory.GetCurrentDirectory();

        string? candidate = null;
        var dir = new DirectoryInfo(current);
        // Walk up at most 6 levels to be robust across environments
        for (int i = 0; i < 6 && dir != null; i++)
        {
            var possible = Path.Combine(dir.FullName, "WileyWidget.WinForms");
            if (Directory.Exists(possible))
            {
                candidate = possible;
                break;
            }

            // Check for solution presence as a hint to the repo root
            var slnFound = Directory.EnumerateFiles(dir.FullName, "WileyWidget.sln", SearchOption.TopDirectoryOnly).Any();
            if (slnFound)
            {
                // assume WinForms project folder is next to solution
                var next = Path.Combine(dir.FullName, "WileyWidget.WinForms");
                if (Directory.Exists(next)) { candidate = next; break; }
            }

            dir = dir.Parent;
        }

        // If not found, fallback to a path relative to current directory but avoid non-existent path usage
        if (string.IsNullOrEmpty(candidate))
        {
            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "WileyWidget.WinForms");
            candidate = Path.GetFullPath(fallback);
        }

        if (!Directory.Exists(candidate))
        {
            // In CI/dev containers the project layout may differ; throw a helpful error for diagnosis
            throw new DirectoryNotFoundException($"Could not locate WileyWidget.WinForms directory. Candidate path: {candidate}");
        }

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(candidate)
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

    // For test runs, replace expensive/external services with test doubles so the container can be validated
            ReplaceExpensiveServicesWithTestDoubles(services);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

    var provider = services.BuildServiceProvider();

            // Act
    var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<HealthCheckService>(provider);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

    var provider = services.BuildServiceProvider();

            // Act
    var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ISettingsService>(provider);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

    var provider = services.BuildServiceProvider();

            // Act
    var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ISecretVaultService>(provider);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

    var provider = services.BuildServiceProvider();

    // Act
            using var scope1 = provider.CreateScope();
    using var scope2 = provider.CreateScope();

            var dbContext1 = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<AppDbContext>(scope1.ServiceProvider);
    var dbContext2 = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<AppDbContext>(scope2.ServiceProvider);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

    var provider = services.BuildServiceProvider();

    // Act
            var service1 = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<HealthCheckService>(provider);
    var service2 = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<HealthCheckService>(provider);

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

    ReplaceExpensiveServicesWithTestDoubles(services);

    var provider = services.BuildServiceProvider();

    // Act
            using var scope = provider.CreateScope();
    var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<MainForm>(scope.ServiceProvider);

        // Assert
            Assert.NotNull(mainForm);
        }

        /// <summary>
        /// Replace services that require external secrets or network calls with local test doubles.
        /// This keeps DI container validation deterministic and safe for CI environments that lack secrets.
        /// </summary>
        private static void ReplaceExpensiveServicesWithTestDoubles(IServiceCollection services)
        {
            // Remove existing registrations (real implementations) if present
            services.RemoveAll(typeof(IAIService));
            services.RemoveAll(typeof(IGrokSupercomputer));
            services.RemoveAll(typeof(ITelemetryService));

            // Register test doubles suitable for integration tests
            services.AddSingleton<IAIService, TestDoubles.NullAIServiceDouble>();
            services.AddSingleton<IGrokSupercomputer, TestDoubles.NullGrokSupercomputerDouble>();
            services.AddSingleton<ITelemetryService, TestDoubles.NullTelemetryService>();
        }
    }
