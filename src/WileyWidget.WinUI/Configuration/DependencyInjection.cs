using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.IO;
using WileyWidget.Abstractions;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Telemetry;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Export;
using WileyWidget.WinUI.Services;
using WileyWidget.WinUI.ViewModels.Main;

namespace WileyWidget.WinUI.Configuration
{
    /// <summary>
    /// Configures dependency injection for the Wiley Widget application.
    /// Follows Microsoft best practices for WinUI 3 applications.
    /// References: https://learn.microsoft.com/en-us/windows/apps/tutorials/winui-mvvm-toolkit/dependency-injection
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Configures all services required by the application.
        /// Called from App.xaml.cs constructor.
        /// </summary>
        /// <returns>Configured IServiceProvider for the application</returns>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // ===== Logging Services =====
            ConfigureLogging(services);

            // ===== Core Services (Singleton - expensive to create, stateless) =====
            ConfigureCoreServices(services);

            // ===== Data Services (Singleton - database/API access) =====
            ConfigureDataServices(services);

            // ===== UI Services (Singleton - shared across app) =====
            ConfigureUIServices(services);

            // ===== Cache Services (Singleton - shared cache) =====
            ConfigureCacheServices(services);

            // ===== Feature Services (Singleton/Scoped based on usage) =====
            ConfigureFeatureServices(services);

            // ===== ViewModels (Transient - new instance per request) =====
            ConfigureViewModels(services);

            // Build and return the service provider
            var serviceProvider = services.BuildServiceProvider();

            Log.Information("[DI] Service provider configured successfully with {ServiceCount} services", services.Count);

            return serviceProvider;
        }

        /// <summary>
        /// Configure logging services using Serilog integration
        /// </summary>
        private static void ConfigureLogging(IServiceCollection services)
        {
            // Add Serilog as the logging provider
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            Log.Debug("[DI] Logging services configured");
        }

        /// <summary>
        /// Configure core singleton services that are stateless and expensive to create
        /// </summary>
        private static void ConfigureCoreServices(IServiceCollection services)
        {
            // Settings Service - Singleton (shared configuration)
            services.AddSingleton<WileyWidget.Services.ISettingsService, SettingsService>();

            // Secret Vault Service - Singleton (shared secrets management)
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();

            // Health Check Service - Singleton (monitors app health)
            services.AddSingleton<HealthCheckService>();

            Log.Debug("[DI] Core services configured");
        }

        /// <summary>
        /// Configure data access services (database, API clients)
        /// </summary>
        private static void ConfigureDataServices(IServiceCollection services)
        {
            // Entity Framework Core DbContext - Scoped (per-request lifecycle)
            // Using SQLite for local development - connection string from environment or default
            var connectionString = Environment.GetEnvironmentVariable("WW_CONNECTION_STRING")
                ?? GetDefaultConnectionString();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(connectionString);
                options.EnableSensitiveDataLogging(); // Only for development
                options.EnableDetailedErrors();
            }, ServiceLifetime.Scoped);

            // HttpClient for QuickBooks API - Singleton
            services.AddHttpClient<IQuickBooksApiClient, QuickBooksApiClient>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            // QuickBooks Service - Singleton (shared API client)
            services.AddSingleton<IQuickBooksService, WileyWidget.WinUI.Services.QuickBooksService>();

            // Database Context Service - Singleton (EF Core context factory would go here)
            // services.AddSingleton<IWileyWidgetContextService, WileyWidgetContextService>();

            Log.Debug("[DI] Data services configured with DbContext");
        }

        /// <summary>
        /// Get default SQLite connection string for development
        /// </summary>
        private static string GetDefaultConnectionString()
        {
            // Use application data folder for SQLite database
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbPath = Path.Combine(appDataPath, "WileyWidget", "wileywidget.db");
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            
            Log.Information("[DI] Using default SQLite database: {DbPath}", dbPath);
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// Configure UI-related services (navigation, dialogs)
        /// NOTE: Navigation and Dialog services need Frame/XamlRoot which are only available
        /// after window activation. These will be registered but may need re-initialization.
        /// </summary>
        private static void ConfigureUIServices(IServiceCollection services)
        {
            // Navigation Service - Transient (needs Frame which is set per window)
            // Will be initialized properly in MainWindow after activation
            services.AddTransient<INavigationService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultNavigationService>>();
                // Frame will be set by MainWindow after creation
                return new DefaultNavigationService(null!, logger, sp);
            });

            // Dialog Service - Transient (needs XamlRoot per dialog)
            services.AddTransient<IDialogService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DialogService>>();
                // XamlRoot will be set per dialog call
                return new DialogService(logger, null);
            });

            // Dialog Tracking Service - Singleton
            services.AddSingleton<IDialogTrackingService, DialogTrackingService>();

            Log.Debug("[DI] UI services configured");
        }

        /// <summary>
        /// Configure caching services
        /// </summary>
        private static void ConfigureCacheServices(IServiceCollection services)
        {
            // Memory Cache - Singleton (using extension method from WileyWidget.Services)
            services.AddWileyMemoryCache();

            Log.Debug("[DI] Cache services configured");
        }

        /// <summary>
        /// Configure feature-specific services
        /// </summary>
        private static void ConfigureFeatureServices(IServiceCollection services)
        {
            // AI Services - Singleton
            services.AddSingleton<IAIService, XAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();

            // Audit Service - Singleton
            services.AddSingleton<IAuditService, AuditService>();

            // Telemetry Service - Singleton
            services.AddSingleton<ITelemetryService, SigNozTelemetryService>();

            // Report Services - Singleton
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IBoldReportService, BoldReportService>();

            // Excel Services - Transient (new instance per operation)
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();

            // Data Anonymizer - Transient (per-operation state)
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();

            // Calculator Services - Transient
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();

            // DI Validation Service - Singleton (validates container registrations)
            services.AddSingleton<IDiValidationService, DiValidationService>();

            Log.Debug("[DI] Feature services configured");
        }

        /// <summary>
        /// Configure ViewModels - all registered as Transient for clean state per instance
        /// </summary>
        private static void ConfigureViewModels(IServiceCollection services)
        {
            // Main ViewModels - Transient (new instance per page navigation)
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<BudgetViewModel>();
            services.AddTransient<QuickBooksViewModel>();
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ToolsViewModel>();
            services.AddTransient<EnterpriseViewModel>();
            services.AddTransient<DepartmentViewModel>();
            services.AddTransient<MunicipalAccountViewModel>();
            services.AddTransient<UtilityCustomerViewModel>();
            services.AddTransient<AIAssistViewModel>();

            // Data ViewModels - Transient
            services.AddTransient<DataViewModel>();
            services.AddTransient<ChartViewModel>();

            Log.Debug("[DI] ViewModels configured");
        }

        /// <summary>
        /// Validates that critical services can be resolved at startup.
        /// Uses the new IDiValidationService for comprehensive validation.
        /// Call this after building the service provider to catch configuration errors early.
        /// </summary>
        /// <param name="serviceProvider">The configured service provider to validate</param>
        /// <returns>True if all critical services are resolvable, false otherwise</returns>
        public static bool ValidateDependencies(IServiceProvider serviceProvider)
        {
            try
            {
                // Use the new DI validation service for comprehensive checking
                var validator = serviceProvider.GetService<IDiValidationService>();
                
                if (validator == null)
                {
                    Log.Warning("[DI] IDiValidationService not registered - falling back to basic validation");
                    return ValidateDependenciesBasic(serviceProvider);
                }

                // Run core service validation first (fast check)
                if (!validator.ValidateCoreServices())
                {
                    Log.Error("[DI] Core service validation failed - critical services missing");
                    return false;
                }

                Log.Information("[DI] Core service validation passed");

                // Run full validation scan (logs details)
                var report = validator.ValidateRegistrations();
                
                Log.Information("[DI] Full validation: {Summary}", report.GetSummary());

                // Log missing services as warnings
                foreach (var missing in report.MissingServices)
                {
                    Log.Warning("[DI] Missing registration: {ServiceType}", missing);
                }

                // Log errors
                foreach (var error in report.Errors)
                {
                    Log.Error("[DI] Registration error: {Error}", error.ToString());
                }

                // Return true if core services work (allow non-critical missing services)
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DI] Dependency validation failed");
                return false;
            }
        }

        /// <summary>
        /// Fallback basic validation if IDiValidationService isn't available.
        /// </summary>
        private static bool ValidateDependenciesBasic(IServiceProvider serviceProvider)
        {
            try
            {
                // Validate critical services can be resolved
                var criticalServices = new[]
                {
                    typeof(ILogger<>),
                    typeof(WileyWidget.Services.ISettingsService),
                    typeof(ISecretVaultService),
                    typeof(INavigationService),
                    typeof(IDialogService),
                    typeof(ICacheService),
                    typeof(IQuickBooksService),
                    typeof(IAIService),
                    typeof(ITelemetryService),
                    typeof(IAuditService),
                    typeof(IDialogTrackingService),
                    typeof(AppDbContext) // EF Core DbContext
                };

                foreach (var serviceType in criticalServices)
                {
                    try
                    {
                        if (serviceType.IsGenericType)
                        {
                            // For generic types like ILogger<T>, test with a concrete type
                            var concreteType = serviceType.MakeGenericType(typeof(MainViewModel));
                            var service = serviceProvider.GetService(concreteType);
                            if (service == null)
                            {
                                Log.Warning("[DI] Failed to resolve critical service: {ServiceType}", serviceType.Name);
                                return false;
                            }
                        }
                        else
                        {
                            var service = serviceProvider.GetService(serviceType);
                            if (service == null)
                            {
                                Log.Warning("[DI] Failed to resolve critical service: {ServiceType}", serviceType.Name);
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DI] Exception resolving service: {ServiceType}", serviceType.Name);
                        return false;
                    }
                }

                Log.Information("[DI] Basic dependency validation completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DI] Basic dependency validation failed");
                return false;
            }
        }
    }
}
