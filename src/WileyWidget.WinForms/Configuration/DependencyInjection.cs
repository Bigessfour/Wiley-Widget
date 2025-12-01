using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Telemetry;
using WileyWidget.Services.Startup; // central resilience pipeline registration helpers
using WileyWidget.Services.Export;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels; // ViewModels in the base namespace (DashboardViewModel, MainViewModel, etc.)

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Configure application services.
        /// </summary>
        /// <param name="configuration">Configuration instance</param>
        public static IServiceProvider ConfigureServices(IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var services = new ServiceCollection();

            // Register configuration instance
            services.AddSingleton(configuration);

            // Configure logging (Serilog) — ensure Serilog reads from configuration at startup
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Database registration: require configured connection string (production-only)
            var connectionString = configuration.GetConnectionString("WileyWidgetDb");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: new int[] { 4060, 18456 });
                    });

                    // Log EF Core "pending model changes" warnings instead of allowing stricter diagnostics to bubble as errors.
                    options.ConfigureWarnings(warnings =>
                    {
                        warnings.Log(RelationalEventId.PendingModelChangesWarning);
                    });

                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging(false);
                });

                // Also register the factory for scoped consumer scenarios
                services.AddDbContextFactory<AppDbContext>(options =>
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: new int[] { 4060, 18456 });
                    });

                    options.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning));
                });
            }
            else
            {
                // Enforce production-only behavior — require a valid connection string
                Log.Fatal("Missing or empty connection string 'WileyWidgetDb'. Application requires a SQL Server connection and will not start without one.");
                throw new InvalidOperationException("Missing connection string 'WileyWidgetDb' in configuration. Add a valid SQL Server connection string to appsettings.json.");
            }

            // Core Services
            // WinForms-aware dispatcher helper for UI-thread marshaling (captures SynchronizationContext at construction)
            services.AddSingleton<WileyWidget.Services.Threading.IDispatcherHelper, WileyWidget.Services.Threading.WinFormsDispatcherHelper>();

            services.AddSingleton<ISettingsService, SettingsService>();
            // Make concrete SettingsService resolvable for components that take the concrete type
            services.AddSingleton<SettingsService>(sp => (SettingsService)Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISettingsService>(sp));

            // Memory cache for repositories/cache-aware components
            services.AddMemoryCache();
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            // Theme / Icon services
            services.AddSingleton<WileyWidget.WinForms.Services.IThemeService, WileyWidget.WinForms.Services.ThemeService>();
            services.AddSingleton<WileyWidget.WinForms.Services.IThemeIconService, WileyWidget.WinForms.Services.ThemeIconService>();
            services.AddSingleton<HealthCheckService>();

            // Telemetry & diagnostics
            // Register a lightweight telemetry service (SigNoz fallback) and a central ErrorReportingService.
            services.AddSingleton<ITelemetryService, SigNozTelemetryService>();
            services.AddSingleton<ErrorReportingService>();

            // Register centralized resilience pipelines (HTTP: XAI, QuickBooks)
            services.AddWileyResiliencePolicies(configuration);

            // Ensure IHttpClientFactory is available for services that rely on named clients
            services.AddHttpClient();

            // Data Services
            services.AddSingleton<IQuickBooksService, QuickBooksService>();

            // Feature Services
            // Use a safe no-op AI service by default to avoid runtime failure when XAI keys are not configured
            services.AddSingleton<IAIService, NullAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();
            services.AddSingleton<IAuditService, AuditService>();
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IBoldReportService, BoldReportService>();
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddSingleton<IDiValidationService, DiValidationService>();

            // ViewModels (use fully-qualified names due to mixed namespaces)
            services.AddSingleton<WileyWidget.ViewModels.MainViewModel>(); // Main app coordinator - singleton for app lifetime
            services.AddTransient<WileyWidget.ViewModels.BudgetOverviewViewModel>();
            services.AddTransient<WileyWidget.WinForms.ViewModels.ChartViewModel>();
            services.AddTransient<WileyWidget.ViewModels.DashboardViewModel>();
            services.AddTransient<WileyWidget.WinForms.ViewModels.SettingsViewModel>();
            services.AddTransient<WileyWidget.WinForms.ViewModels.AccountsViewModel>();

            // Quick-unblock registrations (repositories, infra) — scoped for per-operation dependencies
            services.AddScoped<WileyWidget.Data.IAppDbContext, WileyWidget.Data.AppDbContext>();

            // Repository implementations (scoped)
            services.AddScoped<WileyWidget.Business.Interfaces.IAccountTypeRepository, WileyWidget.Data.AccountTypeRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IAuditRepository, WileyWidget.Data.AuditRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IBudgetRepository, WileyWidget.Data.BudgetRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IChartOfAccountsRepository, WileyWidget.Data.ChartOfAccountsRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IDepartmentRepository, WileyWidget.Data.DepartmentRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IFundRepository, WileyWidget.Data.FundRepository>();
            // MunicipalAccountRepository has multiple constructors — register explicitly using factory + cache
            services.AddScoped<WileyWidget.Business.Interfaces.IMunicipalAccountRepository>(sp =>
            {
                var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>>(sp);
                var cache = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(sp);
                return new WileyWidget.Data.MunicipalAccountRepository(factory, cache);
            });
            services.AddScoped<WileyWidget.Business.Interfaces.IUtilityBillRepository, WileyWidget.Data.UtilityBillRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IUtilityCustomerRepository, WileyWidget.Data.UtilityCustomerRepository>();

            // Infra services
            services.AddScoped<WileyWidget.Services.IUserContext, WileyWidget.Services.UserContext>();
            services.AddSingleton<WileyWidget.Services.IQuickBooksApiClient, WileyWidget.Services.QuickBooksApiClient>();
            services.AddSingleton<WileyWidget.Services.IQuickBooksService, WileyWidget.Services.QuickBooksService>();
            services.AddTransient<WileyWidget.Services.IBudgetImporter, WileyWidget.Services.BudgetImporter>();
            services.AddTransient<WileyWidget.Services.IMemoryProfiler, WileyWidget.Services.NoOpMemoryProfiler>();
            services.AddSingleton<WileyWidget.Services.Abstractions.IGrokSupercomputer, WileyWidget.Services.GrokSupercomputer>();
            // Provide a minimal dashboard service in case the real implementation is not registered
            services.AddSingleton<WileyWidget.Services.Abstractions.IDashboardService, WileyWidget.Services.NoOpDashboardService>();

            // Context builder service used by AI and other services
            services.AddSingleton<WileyWidget.Services.IWileyWidgetContextService, WileyWidget.Services.WileyWidgetContextService>();
            // Provide a safe PDF service (Syncfusion backed) so ReportExportService can resolve
            services.AddSingleton<WileyWidget.Services.Abstractions.IPdfService, WileyWidget.Services.SyncfusionPdfService>();
            // PDF content builder registration (used by some tests / components)
            services.AddTransient<WileyWidget.Services.Abstractions.IPdfContentBuilder, WileyWidget.Services.PdfContentBuilder>();

            // Forms (kept for fallback modal dialogs)
            services.AddTransient<MainForm>(); // only keep those that still exist.

            // UserControl Panels (for DockingManager integration - preferred over Forms)
            services.AddTransient<AccountsPanel>();
            services.AddTransient<AccountEditPanel>();
            services.AddTransient<ChartPanel>();
            services.AddTransient<DashboardPanel>();
            services.AddTransient<SettingsPanel>();

            var provider = services.BuildServiceProvider();

            // Validate critical DI registrations at startup
            ValidateServiceRegistrations(provider);

            return provider;
        }

        /// <summary>
        /// Validates that critical services are properly registered and resolvable.
        /// Logs diagnostic information about the DI container state.
        /// </summary>
        private static void ValidateServiceRegistrations(IServiceProvider provider)
        {
            Log.Information("=== DI Container Validation ===");

            // Critical singletons that must be resolvable
            var criticalSingletons = new (Type serviceType, string name)[]
            {
                (typeof(WileyWidget.Services.Threading.IDispatcherHelper), "IDispatcherHelper"),
                (typeof(ISettingsService), "ISettingsService"),
                (typeof(WileyWidget.WinForms.Services.IThemeService), "IThemeService"),
                (typeof(IDbContextFactory<AppDbContext>), "IDbContextFactory<AppDbContext>"),
                (typeof(IConfiguration), "IConfiguration"),
            };

            foreach (var (serviceType, name) in criticalSingletons)
            {
                try
                {
                    var service = provider.GetService(serviceType);
                    if (service != null)
                    {
                        Log.Debug("DI: {ServiceName} registered OK ({Type})", name, service.GetType().Name);
                    }
                    else
                    {
                        Log.Warning("DI: {ServiceName} NOT REGISTERED - consumers will receive null", name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DI: {ServiceName} resolution FAILED", name);
                }
            }

            // Critical ViewModels (transient)
            var criticalViewModels = new (Type vmType, string name)[]
            {
                (typeof(WileyWidget.ViewModels.DashboardViewModel), "DashboardViewModel"),
                (typeof(WileyWidget.WinForms.ViewModels.AccountsViewModel), "AccountsViewModel"),
                (typeof(WileyWidget.WinForms.ViewModels.SettingsViewModel), "SettingsViewModel"),
                (typeof(WileyWidget.WinForms.ViewModels.ChartViewModel), "ChartViewModel"),
            };

            foreach (var (vmType, name) in criticalViewModels)
            {
                try
                {
                    var vm = provider.GetService(vmType);
                    if (vm != null)
                    {
                        Log.Debug("DI: {ViewModelName} resolvable OK", name);
                    }
                    else
                    {
                        Log.Warning("DI: {ViewModelName} NOT REGISTERED - panel constructors will use fallbacks", name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DI: {ViewModelName} resolution FAILED - constructor may throw", name);
                }
            }

            // Scoped services (create a scope to test)
            using var scope = provider.CreateScope();
            var scopedServices = new (Type serviceType, string name)[]
            {
                (typeof(WileyWidget.Data.IAppDbContext), "IAppDbContext"),
                (typeof(WileyWidget.Business.Interfaces.IMunicipalAccountRepository), "IMunicipalAccountRepository"),
            };

            foreach (var (serviceType, name) in scopedServices)
            {
                try
                {
                    var service = scope.ServiceProvider.GetService(serviceType);
                    if (service != null)
                    {
                        Log.Debug("DI: {ServiceName} (scoped) resolvable OK", name);
                    }
                    else
                    {
                        Log.Warning("DI: {ServiceName} (scoped) NOT REGISTERED", name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DI: {ServiceName} (scoped) resolution FAILED", name);
                }
            }

            Log.Information("=== DI Container Validation Complete ===");
        }
    }
}
