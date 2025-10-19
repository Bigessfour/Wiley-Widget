using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Configuration;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Hosting;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using WileyWidget.Views;

namespace WileyWidget.Configuration;

/// <summary>
/// Extension methods for configuring WPF applications with the Generic Host pattern.
/// Provides a clean, enterprise-grade setup for WPF applications following Microsoft's
/// recommended hosting patterns.
/// </summary>
public static class WpfHostingExtensions
{
    /// <summary>
    /// Configures the host application builder for a WPF application with enterprise-grade services.
    /// This method sets up configuration, logging, dependency injection, and WPF-specific services.
    /// </summary>
    /// <param name="builder">The host application builder to configure</param>
    /// <returns>The configured host application builder for method chaining</returns>
    public static IHostApplicationBuilder ConfigureWpfApplication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Configure configuration sources with proper hierarchy
        ConfigureApplicationConfiguration(builder);

        // Configure enterprise logging
        ConfigureApplicationLogging(builder);

        // Core configuration and options
        ConfigureOptions(builder.Services, builder.Configuration);

        // Core domain services
        ConfigureCoreServices(builder.Services, builder.Configuration);

        // HTTP clients used across the application
        ConfigureHttpClients(builder.Services);

        // UI layer services and view models
        ConfigureWpfServices(builder.Services);

        // Hosted/background services
        ConfigureHostedServices(builder.Services);

        // Database integration

        return builder;
    }

    /// <summary>
    /// Configures the application configuration sources with proper hierarchy and validation.
    /// </summary>
    private static void ConfigureApplicationConfiguration(IHostApplicationBuilder builder)
    {
        // Configuration is automatically set up by the host builder, but we can add additional sources
        builder.Configuration.Sources.Clear();

        var environmentName = builder.Environment.EnvironmentName;
        Log.Information("🔧 Configuring application settings - Environment: {Environment}, IsDevelopment: {IsDevelopment}",
            environmentName, builder.Environment.IsDevelopment());
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        Log.Debug("🔧 Configuration sources added - Base appsettings, environment-specific, and environment variables");

        // Add user secrets in development
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<WileyWidget.App>();
            Log.Debug("🔧 User secrets added for development environment");
        }

        Log.Information("✅ Application configuration setup completed");
    }

    /// <summary>
    /// Configures enterprise-grade logging using Serilog with Microsoft's documented pattern.
    /// Uses Serilog integration with HostApplicationBuilder for proper integration.
    /// </summary>
    private static void ConfigureApplicationLogging(IHostApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath ?? Directory.GetCurrentDirectory();
        var logsDirectory = Path.Combine(contentRoot, "logs");
        try
        {
            Directory.CreateDirectory(logsDirectory);
        }
        catch (Exception directoryEx)
        {
            // Fall back to temp folder if we cannot create the directory
            var tempLogsDirectory = Path.Combine(Path.GetTempPath(), "WileyWidget", "logs");
            Directory.CreateDirectory(tempLogsDirectory);
            logsDirectory = tempLogsDirectory;
            Log.Warning(directoryEx, "[Bootstrap] Failed to create logs directory at '{ContentRoot}'; using fallback '{FallbackDirectory}'", contentRoot, logsDirectory);
        }

        var selfLogPath = Path.Combine(logsDirectory, "serilog-selflog.txt");
        SelfLog.Enable(message =>
        {
            try
            {
                File.AppendAllText(selfLogPath, message + Environment.NewLine);
            }
            catch
            {
                // Ignore failures writing self-log to avoid recursive errors
            }
        });

        var serilogSection = builder.Configuration.GetSection("Serilog");
        var firstSinkName = builder.Configuration["Serilog:WriteTo:0:Name"];
        Log.Debug("[Bootstrap] Serilog configuration section found: {Exists}", serilogSection.Exists());
        Log.Debug("[Bootstrap] Serilog WriteTo[0] Name from configuration: {FirstSinkName}", firstSinkName);

        // Diagnostic: Log all WriteTo sinks to verify Email is not present
        var writeTo = builder.Configuration.GetSection("Serilog:WriteTo").GetChildren();
        var sinkNames = string.Join(", ", writeTo.Select(s => $"{s.Key}={s["Name"]}"));
        Log.Debug("[Bootstrap] All configured WriteTo sinks: {SinkNames}", sinkNames);

        // Diagnostic: Check if Email sink exists anywhere
        var emailSink = writeTo.FirstOrDefault(s => s["Name"]?.Equals("Email", StringComparison.OrdinalIgnoreCase) == true);
        if (emailSink != null)
        {
            Log.Error("[Bootstrap] ⚠️ EMAIL SINK DETECTED! This should not exist. Section path: {Path}", emailSink.Path);
            Log.Error("[Bootstrap] Email sink config: {EmailConfig}", string.Join(", ", 
                emailSink.GetSection("Args").GetChildren().Select(c => $"{c.Key}={c.Value}")));
        }
        else
        {
            Log.Information("[Bootstrap] ✅ Email sink NOT found - configuration is correct");
        }

        if (!serilogSection.Exists() || string.IsNullOrWhiteSpace(firstSinkName))
        {
            Log.Warning("[Bootstrap] Serilog configuration appears missing or invalid. Current configuration view will be logged at Debug level.");

            if (builder.Configuration is IConfigurationRoot configurationRoot)
            {
                Log.Debug("Serilog configuration debug view: {DebugView}", configurationRoot.GetDebugView());
            }
            else
            {
                Log.Debug("Serilog configuration debug view: {DebugView}", builder.Configuration.Build().GetDebugView());
            }
        }

        // Microsoft documented pattern: Create configured logger and set as global
        var configuredLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .CreateLogger();

        // Replace the bootstrap logger with the configured logger
        Log.Logger = configuredLogger;

        // Clear providers and add the configured Serilog
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(configuredLogger, dispose: true);

        Log.Information("Serilog logging configured from configuration sources for {Environment} environment", 
            builder.Environment.EnvironmentName);
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<SyncfusionOptions>(configuration.GetSection("Syncfusion"));
        services.Configure<HealthCheckConfiguration>(configuration.GetSection("HealthChecks"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<HealthCheckConfiguration>>().Value);

        // Configure AppOptions with database and secrets integration
        services.Configure<AppOptions>(configuration.GetSection("App"));
        services.AddTransient<IConfigureOptions<AppOptions>, AppOptionsConfigurator>();
        services.AddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();
    }

    private static void ConfigureCoreServices(IServiceCollection services, IConfiguration configuration)
    {
        // Log connection string for diagnostics
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        Log.Information("🔗 Connection string loaded from configuration: {HasConnection}, Length: {Length}", 
            !string.IsNullOrEmpty(connectionString), 
            connectionString?.Length ?? 0);
        
        if (string.IsNullOrEmpty(connectionString))
        {
            Log.Warning("⚠️ DefaultConnection string is missing or empty! Database operations will fail.");
        }
        else
        {
            // Log sanitized version (hide credentials)
            var sanitized = connectionString.Contains("Password=") 
                ? connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "..." 
                : connectionString.Substring(0, Math.Min(100, connectionString.Length));
            Log.Debug("Connection string preview: {Preview}", sanitized);
        }

        // Database services - must be first as other services depend on it
        // NOTE: Database registration is fully handled by AddEnterpriseDatabaseServices
        // Do NOT call AddDbContext here as it creates duplicate/conflicting registrations
        services.AddEnterpriseDatabaseServices(configuration);

        // Critical services - load immediately
    services.AddSingleton<AuthenticationService>();
        services.AddSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
    services.AddSingleton<ApplicationMetricsService>();
    services.AddSingleton<SettingsService>();
    services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton(ErrorReportingService.Instance);
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<SyncfusionLicenseState>();
        services.AddSingleton<IStartupProgressReporter>(sp => (IStartupProgressReporter)WileyWidget.App.StartupProgress);
        
        // ViewManager removed - MainWindow handles all child view navigation via DockingManager
        // MainWindow lifecycle now handled directly in App.xaml.cs OnStartup
        
    services.AddSingleton<ThemeManager>();
    services.AddSingleton<IThemeManager>(sp => sp.GetRequiredService<ThemeManager>());
        services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
        services.AddMemoryCache();
        services.AddSingleton<IDispatcherHelper, DispatcherHelper>();

        // Lazy-loaded services - defer heavy initialization
        services.AddTransient<IExcelReaderService, ExcelReaderService>();
        services.AddScoped<IGrokSupercomputer, GrokSupercomputer>();
        services.AddScoped<IChargeCalculatorService, ServiceChargeCalculatorService>();

        // AI Service with lazy initialization
        services.AddSingleton<IAIService>(sp =>
        {
            // Return a lazy wrapper that defers actual initialization
            return new LazyAIService(sp);
        });

        services.AddSingleton<WileyWidget.Services.HealthCheckService>();
    }

    private static void ConfigureHttpClients(IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddHttpClient("Default", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget/1.0");
        });

        services.AddHttpClient("AIServices", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget-AI/1.0");
        });

        services.AddHttpClient("ExternalAPIs", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget-API/1.0");
        });
    }

    private static void ConfigureWpfServices(IServiceCollection services)
    {
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
    services.AddTransient<MainViewModel>();
        
        // CRITICAL: MainWindow must be Singleton to prevent duplicate window instances
        // MainWindow lifecycle now handled directly in App.xaml.cs OnStartup
        services.AddSingleton<MainWindow>();
        
        services.AddSingleton<SplashScreenWindow>(sp => SplashScreenFactory.Create(sp));
        services.AddTransient<AboutWindow>();

        services.AddTransient<AboutViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<EnterpriseViewModel>();
        services.AddTransient<BudgetViewModel>();
        services.AddTransient<AIAssistViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ToolsViewModel>();
        services.AddTransient<ProgressViewModel>();
        services.AddTransient<MunicipalAccountViewModel>();
        services.AddTransient<UtilityCustomerViewModel>();
    }

    private static void ConfigureHostedServices(IServiceCollection services)
    {
        // Defer heavy background services to reduce startup time
        // BackgroundInitializationService removed - now using Prism modules
        // Only start critical hosted services immediately

        // Defer non-critical services to background initialization
        services.AddSingleton<HealthCheckHostedService>();
    }



    private static string GetApiKeySource(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "${XAI_API_KEY}")
        {
            return "None";
        }

        if (Environment.GetEnvironmentVariable("XAI_API_KEY") == apiKey)
        {
            return "Environment";
        }

    return "SecretVault";
    }

    /// <summary>
    /// Lazy wrapper for AI service to defer expensive initialization until first use
    /// </summary>
    private class LazyAIService : IAIService
    {
        private readonly IServiceProvider _serviceProvider;
        private IAIService? _instance;
        private readonly object _lock = new();

        public LazyAIService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private IAIService GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        var logger = _serviceProvider.GetService<ILogger<XAIService>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XAIService>.Instance;
                        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
                        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
                        var contextService = _serviceProvider.GetRequiredService<IWileyWidgetContextService>();

                        var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ??
                                     configuration["XAI:ApiKey"];

                        var requireAi = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_AI_SERVICE"), "true", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(configuration["XAI:RequireService"], "true", StringComparison.OrdinalIgnoreCase);

                        logger.LogInformation("🤖 XAI CONFIGURATION: API_KEY_SET={ApiKeySet}, REQUIRE_AI={RequireAi}, API_KEY_LENGTH={Length}, SOURCE={Source}",
                            !string.IsNullOrEmpty(apiKey) && apiKey != "${XAI_API_KEY}",
                            requireAi,
                            string.IsNullOrEmpty(apiKey) ? 0 : apiKey.Length,
                            GetApiKeySource(apiKey));

                        if (string.IsNullOrEmpty(apiKey) || apiKey == "${XAI_API_KEY}")
                        {
                            if (requireAi)
                            {
                                logger.LogError("AI service required but XAI_API_KEY not set. Falling back to stub; functionality limited.");
                            }
                            else
                            {
                                logger.LogWarning("XAI_API_KEY not set. Using NullAIService stub. Configure XAI:ApiKey in appsettings.json or set XAI_API_KEY environment variable.");
                            }

                            _instance = new NullAIService();
                        }
                        else
                        {
                            try
                            {
                                logger.LogInformation("Initializing XAIService with provided API key (length {Len}).", apiKey.Length);
                                var aiLoggingService = _serviceProvider.GetRequiredService<IAILoggingService>();
                                var memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
                                _instance = new XAIService(httpClientFactory, configuration, logger, contextService, aiLoggingService, memoryCache);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to initialize XAIService. Falling back to NullAIService");
                                _instance = new NullAIService();
                            }
                        }
                    }
                }
            }
            return _instance;
        }

        public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
            => GetInstance().GetInsightsAsync(context, question, cancellationToken);

        public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
            => GetInstance().AnalyzeDataAsync(data, analysisType, cancellationToken);

        public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
            => GetInstance().ReviewApplicationAreaAsync(areaName, currentState, cancellationToken);

        public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
            => GetInstance().GenerateMockDataSuggestionsAsync(dataType, requirements, cancellationToken);
    }

    private static class SplashScreenFactory
    {
        public static SplashScreenWindow Create(IServiceProvider serviceProvider)
        {
            // Splash screen functionality removed - Prism handles window initialization
            SplashScreenWindow? splash = null;

            void CreateSplash()
            {
                var created = ActivatorUtilities.CreateInstance<SplashScreenWindow>(serviceProvider);
                splash = created;
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                CreateSplash();
            }
            else if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(CreateSplash);
            }
            else
            {
                CreateSplash();
            }

            return splash ?? throw new InvalidOperationException("Failed to create SplashScreenWindow on the UI dispatcher");
        }
    }
}