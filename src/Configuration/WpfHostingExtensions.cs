using System;
using System.IO;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Hosting;
using WileyWidget.Services.Telemetry;
using WileyWidget.Services.Threading;
using WileyWidget.Startup;
using WileyWidget.ViewModels;

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
        builder.Services.AddEnterpriseDatabaseServices(builder.Configuration);

        // Startup pipeline helpers
        RegisterStartupPipeline(builder.Services);

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
            builder.Configuration.AddUserSecrets<App>();
            Log.Debug("🔧 User secrets added for development environment");
        }

        // After base config assembled, attempt to add Azure Key Vault provider if a vault name is configured.
        var tempConfig = builder.Configuration.Build();
        var keyVaultName = tempConfig["Azure:KeyVaultName"]; // expected config key
        if (!string.IsNullOrWhiteSpace(keyVaultName))
        {
            try
            {
                // TODO: Re-implement Azure Key Vault configuration for WPF apps
                // The AddAzureKeyVault method from ASP.NET Core is not available in desktop apps
                // Need to implement custom Key Vault configuration provider
                Log.Warning("Azure Key Vault configuration temporarily disabled for WPF compatibility");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add Azure Key Vault configuration provider for vault {Vault}", keyVaultName);
            }
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
            Console.WriteLine($"[Bootstrap] Failed to create logs directory at '{contentRoot}'; using fallback '{logsDirectory}'. Exception: {directoryEx.Message}");
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
        Console.WriteLine($"[Bootstrap] Serilog configuration section found: {serilogSection.Exists()}");
        Console.WriteLine($"[Bootstrap] Serilog WriteTo[0] Name from configuration: {firstSinkName}");

        if (!serilogSection.Exists() || string.IsNullOrWhiteSpace(firstSinkName))
        {
            Console.WriteLine("[Bootstrap] Serilog configuration appears missing or invalid. Current configuration view:");

            if (builder.Configuration is IConfigurationRoot configurationRoot)
            {
                Console.WriteLine(configurationRoot.GetDebugView());
            }
            else
            {
                Console.WriteLine(builder.Configuration.Build().GetDebugView());
            }
        }

        // Microsoft documented pattern: Create configured logger and set as global
        var configuredLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console()
            .CreateLogger();

        // Replace the bootstrap logger with the configured logger
        Log.Logger = configuredLogger;

        // Clear providers and add the configured Serilog
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(configuredLogger, dispose: true);

        Log.Information("Serilog logging configured from configuration sources for {Environment} environment", 
            builder.Environment.EnvironmentName);

        // Application Insights (optional) - support ConnectionString or InstrumentationKey
        var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
        var aiInstrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"]; // legacy
        if (!string.IsNullOrWhiteSpace(aiConnectionString) || !string.IsNullOrWhiteSpace(aiInstrumentationKey))
        {
            var isDevelopment = builder.Environment.IsDevelopment();

            // Create + register TelemetryConfiguration manually for non-ASP.NET WPF host
            builder.Services.AddSingleton(sp =>
            {
                var cfg = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.CreateDefault();
                if (!string.IsNullOrWhiteSpace(aiConnectionString))
                    cfg.ConnectionString = aiConnectionString;
                else if (!string.IsNullOrWhiteSpace(aiInstrumentationKey))
                    cfg.ConnectionString = $"InstrumentationKey={aiInstrumentationKey}"; // legacy support

                if (cfg.TelemetryChannel != null && isDevelopment)
                {
                    // Fast flush & no sampling in dev
                    cfg.TelemetryChannel.DeveloperMode = true;
                }
                return cfg;
            });

            // TelemetryClient
            builder.Services.AddSingleton(sp => new TelemetryClient(sp.GetRequiredService<TelemetryConfiguration>()));

            // Initializer + startup hosted service
            builder.Services.AddSingleton<ITelemetryInitializer, WileyWidget.Services.Telemetry.EnvironmentTelemetryInitializer>();
            builder.Services.AddHostedService<WileyWidget.Services.Telemetry.TelemetryStartupService>();

            Log.Information("Application Insights telemetry configured (Environment: {Environment}, DeveloperMode: {DeveloperMode})", 
                builder.Environment.EnvironmentName, isDevelopment);
        }
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<AzureOptions>(configuration.GetSection("Azure"));
        services.Configure<SyncfusionOptions>(configuration.GetSection("Syncfusion"));
        services.Configure<HealthCheckConfiguration>(configuration.GetSection("HealthChecks"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<HealthCheckConfiguration>>().Value);
    }

    private static void ConfigureCoreServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
        services.AddSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
        services.AddSingleton<ApplicationMetricsService>();
        services.AddSingleton(SettingsService.Instance);
        services.AddSingleton(ErrorReportingService.Instance);
        services.AddSingleton<LocalizationService>();
    services.AddSingleton<SyncfusionLicenseState>();
        services.AddSingleton<IStartupProgressReporter>(_ => App.StartupProgress);
        services.AddSingleton<IViewManager, ViewManager>();
        services.AddSingleton<IThemeManager>(_ => ThemeManager.Instance);
        services.AddMemoryCache();
        services.AddSingleton<IDispatcherHelper, DispatcherHelper>();
        services.AddTransient<IProgressReporter, ProgressReporter>();
        services.AddTransient<IExcelReaderService, ExcelReaderService>();
        services.AddScoped<IGrokSupercomputer, GrokSupercomputer>();

        services.AddSingleton<IAIService>(sp =>
        {
            var logger = sp.GetService<ILogger<XAIService>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XAIService>.Instance;
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var keyVaultService = sp.GetService<IAzureKeyVaultService>();

            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ??
                         TryGetFromKeyVault(keyVaultService, "XAI-API-KEY", logger) ??
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

                return new NullAIService();
            }

            try
            {
                logger.LogInformation("Initializing XAIService with provided API key (length {Len}).", apiKey.Length);
                return new XAIService(httpClientFactory, configuration, logger, apiKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize XAIService. Falling back to NullAIService");
                return new NullAIService();
            }
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
        services.AddTransient<MainWindow>();
        services.AddSingleton<SplashScreenWindow>(sp => SplashScreenFactory.Create(sp));
        services.AddTransient<AboutWindow>();

        services.AddTransient<MainViewModel>(sp => ActivatorUtilities.CreateInstance<MainViewModel>(sp, false));
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
    }

    private static void ConfigureHostedServices(IServiceCollection services)
    {
        services.AddSingleton<BackgroundInitializationService>();
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundInitializationService>());
        services.AddHostedService<HealthCheckHostedService>();
        services.AddHostedService<HostedWpfApplication>();
        services.AddHostedService<StartupTaskRunner>();
    }

    private static void RegisterStartupPipeline(IServiceCollection services)
    {
        services.AddSingleton<StartupTaskRunner>();
        services.AddSingleton<IStartupTask, SettingsStartupTask>();
        services.AddSingleton<IStartupTask, DiagnosticsStartupTask>();
        services.AddSingleton<IStartupTask, QuickBooksStartupTask>();
    }

    private static string? TryGetFromKeyVault(IAzureKeyVaultService? keyVaultService, string secretName, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (keyVaultService == null)
        {
            return null;
        }

        try
        {
            var task = keyVaultService.GetSecretAsync(secretName);
            task.Wait();
            var secret = task.Result;
            if (!string.IsNullOrEmpty(secret))
            {
                logger.LogInformation("Retrieved XAI API key from Azure Key Vault");
                return secret;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve XAI API key from Azure Key Vault, trying next source");
        }

        return null;
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

        return "AzureKeyVault";
    }

    private static class SplashScreenFactory
    {
        public static SplashScreenWindow Create(IServiceProvider serviceProvider)
        {
            if (App.SplashScreenInstance is { } existing)
            {
                return existing;
            }

            SplashScreenWindow? splash = null;

            void CreateSplash()
            {
                var created = ActivatorUtilities.CreateInstance<SplashScreenWindow>(serviceProvider);
                splash = created;
                App.SetSplashScreenInstance(created);
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