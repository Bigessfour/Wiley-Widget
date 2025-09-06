using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.File;
using WileyWidget.Infrastructure.Logging;
using WileyWidget.Infrastructure.Security;
using WileyWidget.Infrastructure.Monitoring;
using WileyWidget.Services;

namespace WileyWidget.Infrastructure.Bootstrap;

/// <summary>
/// Application bootstrapper that orchestrates the startup process.
/// Follows Microsoft dependency injection patterns and startup performance guidelines.
/// </summary>
public class ApplicationBootstrapper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly Serilog.ILogger _logger;

    public ApplicationBootstrapper()
    {
        // Build configuration first
        _configuration = BuildConfiguration();

        // Configure Serilog first
        ConfigureSerilog();

        // Get logger directly from Serilog
        _logger = Log.Logger;

        // Build service provider
        _serviceProvider = ConfigureServices();
    }

    /// <summary>
    /// Initializes the application asynchronously.
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.Information("🚀 Starting application bootstrap process...");

        try
        {
            // Phase 1: Core infrastructure
            InitializeCoreInfrastructure();

            // Phase 2: Application services
            await InitializeApplicationServicesAsync();

            // Phase 3: UI and theming
            await InitializeUserInterfaceAsync();

            _logger.Information("✅ Application bootstrap completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "💥 Application bootstrap failed");
            throw;
        }
    }

    private IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(_configuration);

        // Core infrastructure services
        services.AddSingleton<ISecurityAuditor, SecurityAuditor>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<StartupProgressTracker>();

        // Health checks
        services.AddSingleton<IHealthCheck, DatabaseHealthCheck>();
        services.AddSingleton<IHealthCheck, MemoryHealthCheck>();
        services.AddSingleton<IHealthCheck, DiskSpaceHealthCheck>();

        // Application services
        WileyWidget.Infrastructure.Services.ServiceCollectionExtensions.AddApplicationServices(services, _configuration);

        // Bootstrapper logging: Log service count
        _logger.Information("Services Registered: {Count}", services.Count);

        var serviceProvider = services.BuildServiceProvider();

        // Test service registration confidence check
        // var themeCoordinator = serviceProvider.GetRequiredService<WileyWidget.Services.IThemeCoordinator>();
        // _logger.Information("✅ IThemeCoordinator service registration confirmed");

        return serviceProvider;
    }

    private void InitializeCoreInfrastructure()
    {
        _logger.Information("🔧 Initializing core infrastructure...");

        // DEPRECATED: License registration now handled in App.xaml.cs constructor
        // per official Syncfusion WPF 30.2.7 documentation

        // Start monitoring
        StartResourceMonitoring();

        _logger.Information("✅ Core infrastructure initialized");
    }

    private async Task InitializeApplicationServicesAsync()
    {
        _logger.Information("🔧 Initializing application services...");

        // Initialize database context
        await InitializeDatabaseAsync();

        // Load user settings
        await LoadUserSettingsAsync();

        _logger.Information("✅ Application services initialized");
    }

    private async Task InitializeUserInterfaceAsync()
    {
        _logger.Information("🔧 Initializing user interface...");

        // Configure UI components
        await ConfigureUIComponentsAsync();

        _logger.Information("✅ User interface initialized");
    }

    private void ConfigureSerilog()
    {
        var logRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logRoot);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithProperty("Application", "WileyWidget")
            .Enrich.WithProperty("StartupPhase", "Bootstrap")
            .Enrich.FromLogContext()
            .Enrich.With(new ApplicationEnricher())
            .WriteTo.File(
                path: Path.Combine(logRoot, "structured-.log"),
                rollingInterval: RollingInterval.Day,
                formatter: new Serilog.Formatting.Json.JsonFormatter())
            .WriteTo.File(
                path: Path.Combine(logRoot, "app-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Application} {CorrelationId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logRoot, "errors-.log"),
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Error)
            .CreateLogger();
    }



    private void StartResourceMonitoring()
    {
        var resourceMonitor = _serviceProvider.GetRequiredService<ResourceMonitor>();
        // Resource monitor starts automatically in constructor
    }

    private async Task InitializeDatabaseAsync()
    {
        // Database initialization logic here
        await Task.CompletedTask;
    }

    private async Task LoadUserSettingsAsync()
    {
        // User settings loading logic here
        await Task.CompletedTask;
    }

    private async Task ConfigureUIComponentsAsync()
    {
        // UI component configuration logic here
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    public IConfiguration Configuration => _configuration;
}
