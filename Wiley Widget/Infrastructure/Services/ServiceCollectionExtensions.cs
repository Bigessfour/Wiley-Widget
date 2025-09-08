using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyWidget.Services; // includes ISettingsService, ThemeManager, export pipeline
using WileyWidget.ViewModels;
using WileyWidget.Views;
using WileyWidget.UI.Theming;
using WileyWidget.Configuration;
using WileyWidget.Infrastructure.Security;
using WileyWidget.Infrastructure.Monitoring;
using WileyWidget.Data;
using WileyWidget.Infrastructure.Bootstrap;

namespace WileyWidget.Infrastructure.Services;

/// <summary>
/// Extension methods for configuring application services.
/// Follows Microsoft dependency injection patterns.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all application services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection with application services added.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core application services
        services.AddSingleton<ISettingsService>(sp => WileyWidget.Services.SettingsService.Instance);
        services.AddSingleton<WileyWidget.Services.SettingsService>(sp => WileyWidget.Services.SettingsService.Instance);
    services.AddSingleton<IThemeService, WileyWidget.Services.ThemeService>(); // legacy simple service (can be deprecated)
    services.AddSingleton<WileyWidget.Services.IThemeManager, WileyWidget.Services.ThemeManager>(); // central theme manager
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Startup and initialization services
        services.AddSingleton<ApplicationInitializationService>();
        services.AddSingleton<ErrorHandlingService>();
        services.AddSingleton<StartupPerformanceService>();
        services.AddSingleton<DeferredInitializer>();

        // Supporting services used by ApplicationInitializationService
        services.AddSingleton<LoggingService>();
        services.AddSingleton<AssemblyValidationService>();
        services.AddSingleton<SplashScreenService>();
        services.AddSingleton<ResourceMonitorService>();
        services.AddSingleton<IResourceManagementService, ResourceManagementService>();

        // Additional service registrations
    services.AddTransient<IThemeCoordinator, ThemeCoordinator>(); // coordinator works atop ThemeManager
        services.AddTransient<IWindowStateService, WindowStateService>();

        // View models
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views (if needed for dependency injection)
        services.AddTransient<MainWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<SettingsView>();

        // Infrastructure services
        services.AddSingleton<ISecurityAuditor, SecurityAuditor>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<StartupProgressTracker>();

        // Health checks
        services.AddSingleton<IHealthCheck, DatabaseHealthCheck>();
        services.AddSingleton<IHealthCheck, MemoryHealthCheck>();
        services.AddSingleton<IHealthCheck, DiskSpaceHealthCheck>();

        // Database services
        services.AddDatabaseServices(configuration);

        // Business services
        services.AddTransient<IBusinessLogicService, BusinessLogicService>();
        services.AddTransient<IDataService, DataService>();
        services.AddTransient<IValidationService, ValidationService>();
        services.AddTransient<IDiagramBuilderService, DiagramBuilderService>();

        // API Key services
        services.AddSingleton<WileyWidget.Services.IApiKeyFacade, WileyWidget.Services.ApiKeyFacade>();
        services.AddSingleton<WileyWidget.Services.ApiKeyService>(WileyWidget.Services.ApiKeyService.Instance);

        // Logging services
        services.AddLogging();

    // Document export pipeline (PDF + Excel) using Syncfusion documented APIs
    WileyWidget.Services.ExportServiceCollectionExtensions.AddDocumentExport(services);

        // QuickBooks services with caching
        services.AddSingleton<QuickBooksService>();
        services.AddSingleton<IQuickBooksService>(sp =>
        {
            var innerService = sp.GetRequiredService<QuickBooksService>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            var logger = sp.GetRequiredService<ILogger<CachedQuickBooksService>>();
            var settings = sp.GetRequiredService<IOptions<QuickBooksSettings>>();
            return new CachedQuickBooksService(innerService, cache, logger, settings);
        });

        // Configuration binding
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<DatabaseSettings>(configuration.GetSection("Database"));
        services.Configure<SecuritySettings>(configuration.GetSection("Security"));
        services.Configure<QuickBooksSettings>(configuration.GetSection("QuickBooks"));

        return services;
    }
}
