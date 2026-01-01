using Microsoft.Extensions.DependencyInjection;
using DI = Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Export;
using WileyWidget.Services.Telemetry;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net.Http;

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        public static ServiceCollection CreateServiceCollection(bool includeDefaults = true)
        {
            var services = new ServiceCollection();
            ConfigureServicesInternal(services, includeDefaults);
            return services;
        }

        public static IServiceProvider ConfigureServices()
        {
            var services = CreateServiceCollection();
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        }

        private static void ConfigureServicesInternal(IServiceCollection services, bool includeDefaults = true)
        {
            // =====================================================================
            // INFRASTRUCTURE SERVICES (Configuration, Logging, Health Checks)
            // =====================================================================

            // Configuration (Singleton - lives for app lifetime)
            // NOTE: IConfiguration is provided by HostApplicationBuilder - DO NOT create a new one here
            // The host's configuration will be automatically available when services are registered
            // This ensures .env, user secrets, and environment variables are all properly loaded
            // For tests, provide a default in-memory configuration
            if (includeDefaults && !services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
            {
                var defaultConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                        ["Logging:LogLevel:Default"] = "Information"
                    })
                    .Build();
                _ = services.AddSingleton<IConfiguration>(defaultConfig);
            }

            // Logging (Singleton - Serilog logger)
            _ = services.AddSingleton(Serilog.Log.Logger);

            // Health Check Configuration (Singleton)
            _ = services.AddSingleton(new HealthCheckConfiguration());

            // HTTP Client Factory (Singleton factory, Transient clients)
            _ = services.AddHttpClient();

            // Named HttpClient for Grok with resilience
            _ = services.AddHttpClient("GrokClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddResilienceHandler("GrokResilience", builder =>
                {
                    builder.AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        Delay = TimeSpan.FromMilliseconds(600),
                        BackoffType = DelayBackoffType.Linear
                    });
                    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromMinutes(1),
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromMinutes(2)
                    });
                    builder.AddTimeout(new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(15)
                    });
                });

            // Memory Cache (Singleton)
            _ = services.AddMemoryCache();

            // Bind Grok recommendation options from configuration (appsettings: GrokRecommendation)
            // Use deferred options configuration so IConfiguration is resolved from the final provider (host builder)
            _ = services.AddOptions<WileyWidget.Business.Configuration.GrokRecommendationOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.GetSection("GrokRecommendation").Bind(opts));

            // =====================================================================
            // DATABASE CONTEXT (Scoped - one per request/scope)
            // =====================================================================

            // For tests, register DbContext with in-memory database
            if (includeDefaults && !services.Any(sd => sd.ServiceType == typeof(AppDbContext)))
            {
                _ = services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            }
            if (includeDefaults && !services.Any(sd => sd.ServiceType == typeof(IDbContextFactory<AppDbContext>)))
            {
                // Register DbContextOptions as singleton to avoid lifetime conflicts with the factory
                _ = services.AddSingleton(sp =>
                {
                    var builder = new DbContextOptionsBuilder<AppDbContext>();
                    _ = builder.UseInMemoryDatabase("TestDb");
                    return builder.Options;
                });

                _ = services.AddDbContextFactory<AppDbContext>((sp, options) =>
                    options.UseInMemoryDatabase("TestDb"));
            }

            // =====================================================================
            // DATABASE REPOSITORIES (Scoped - one per request/scope)
            // Per Microsoft: DbContext types use Scoped lifetime by default
            // Repository pattern over DbContext should also be Scoped
            // =====================================================================

            _ = services.AddScoped<IAccountsRepository, AccountsRepository>();
            _ = services.AddScoped<Business.Interfaces.IActivityLogRepository, ActivityLogRepository>();
            _ = services.AddScoped<IAuditRepository, AuditRepository>();
            _ = services.AddScoped<IBudgetRepository, BudgetRepository>();
            _ = services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            _ = services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
            _ = services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
            _ = services.AddScoped<IUtilityBillRepository, UtilityBillRepository>();
            _ = services.AddScoped<IUtilityCustomerRepository, UtilityCustomerRepository>();

            // =====================================================================
            // CORE APPLICATION SERVICES (Singleton - Stateless, Thread-Safe)
            // =====================================================================

            _ = services.AddSingleton<SettingsService>();
            _ = services.AddSingleton<ISettingsService>(sp => DI.ServiceProviderServiceExtensions.GetRequiredService<SettingsService>(sp));
            _ = services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            _ = services.AddSingleton<HealthCheckService>();
            _ = services.AddSingleton<ErrorReportingService>();
            _ = services.AddSingleton<ITelemetryService, SigNozTelemetryService>();

            // Startup Timeline Monitoring Service (tracks initialization order and timing)
            _ = services.AddSingleton<IStartupTimelineService, StartupTimelineService>();

            // Startup orchestration (license, theme, DI validation)
            _ = services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

            // DI Validation Service (uses layered approach: core + WinForms-specific wrapper)
            _ = services.AddSingleton<IDiValidationService, DiValidationService>();
            _ = services.AddSingleton<IWinFormsDiValidator, WinFormsDiValidator>();

            // =====================================================================
            // BUSINESS DOMAIN SERVICES
            // =====================================================================

            // QuickBooks Integration (Singleton - external API client)
            _ = services.AddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();
            _ = services.AddSingleton<IQuickBooksService, QuickBooksService>();

            // Dashboard Service (Transient - short-lived data aggregation)
            _ = services.AddTransient<IDashboardService, DashboardService>();

            // Budget Category Service (Scoped - works with DbContext)
            _ = services.AddScoped<IBudgetCategoryService, BudgetCategoryService>();

            // Context Service (Scoped - per-request context)
            _ = services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();

            // AI Services (Scoped - may hold request-specific context)
            _ = services.AddScoped<IAIService, XAIService>();
            _ = services.AddSingleton<IAILoggingService, AILoggingService>();

            // Audit Service (Singleton - writes to repository through scopes)
            _ = services.AddSingleton<IAuditService, AuditService>();

            // =====================================================================
            // REPORTING & EXPORT SERVICES
            // =====================================================================

            // Report Services (Singleton - stateless report generation)
            _ = services.AddSingleton<IReportExportService, ReportExportService>();
            _ = services.AddSingleton<IReportService, FastReportService>();

            // Excel Services (Transient - I/O operations, disposable)
            _ = services.AddTransient<IExcelReaderService, ExcelReaderService>();
            _ = services.AddTransient<IExcelExportService, ExcelExportService>();

            // =====================================================================
            // UTILITY & CALCULATION SERVICES (Transient - Stateless, Per-Use)
            // =====================================================================

            _ = services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            _ = services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            _ = services.AddTransient<IAnalyticsService, AnalyticsService>();

            // Analytics Pipeline (Scoped - may aggregate data across request)
            _ = services.AddScoped<IAnalyticsPipeline, AnalyticsPipeline>();
            _ = services.AddScoped<IGrokSupercomputer, NullGrokSupercomputer>();

            // Department Expense & Recommendation Services (Scoped - may query external APIs)
            _ = services.AddScoped<IDepartmentExpenseService, Business.Services.DepartmentExpenseService>();
            _ = services.AddScoped<IGrokRecommendationService, Business.Services.GrokRecommendationService>();

            // =====================================================================
            // UI SERVICES & THEME (Singleton - Application-wide state)
            // =====================================================================

            _ = services.AddSingleton<IThemeService, ThemeService>();
            _ = services.AddSingleton<IThemeIconService, ThemeIconService>();

            // Panel Navigation Service
            // NOTE: This service depends on MainForm's DockingManager + central document panel.
            // Those are created during MainForm deferred initialization (OnShown), so we avoid
            // registering a DI factory that resolves MainForm (circular dependency).
            // MainForm creates PanelNavigationService once docking is ready.

            // UI Configuration (Singleton)
            _ = services.AddSingleton(static sp =>
                UIConfiguration.FromConfiguration(DI.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(sp)));

            // =====================================================================
            // VIEWMODELS (Transient - New instance per form/view)
            // Per Microsoft: ViewModels are typically Transient as they represent
            // view-specific state and shouldn't be shared
            // =====================================================================

            _ = services.AddTransient<ChartViewModel>();
            _ = services.AddTransient<SettingsViewModel>();
            _ = services.AddTransient<AccountsViewModel>();
            _ = services.AddTransient<DashboardViewModel>();
            _ = services.AddTransient<AnalyticsViewModel>();
            _ = services.AddTransient<BudgetOverviewViewModel>();
            _ = services.AddTransient<BudgetViewModel>();
            _ = services.AddTransient<CustomersViewModel>();
            _ = services.AddTransient<MainViewModel>();
            _ = services.AddTransient<ReportsViewModel>();
            _ = services.AddTransient<DepartmentSummaryViewModel>();
            _ = services.AddTransient<RevenueTrendsViewModel>();
            _ = services.AddTransient<AuditLogViewModel>();
            _ = services.AddTransient<RecommendedMonthlyChargeViewModel>();

            // =====================================================================
            // FORMS (Singleton for MainForm, Transient for child forms)
            // MainForm: Singleton because it's the application's main window (lives for app lifetime)
            // Child Forms: Transient because they're created/disposed multiple times
            // =====================================================================

            // Main Form (Scoped - resolved from UI scope to ensure scoped dependencies are available)
            _ = services.AddScoped<MainForm>();

            // Child Forms (Transient - Created/disposed as needed)
            // NOTE: RecommendedMonthlyChargePanel is now a UserControl panel - use IPanelNavigationService.ShowPanel<RecommendedMonthlyChargePanel>()

            // NOTE: Child forms removed - application now uses panel-based navigation via IPanelNavigationService
            // Legacy forms (ChartForm, SettingsForm, AccountsForm, etc.) have been superseded by UserControl panels
            // (DashboardPanel, AccountsPanel, ChartPanel, BudgetOverviewPanel, DepartmentSummaryPanel, RevenueTrendsPanel, SettingsPanel)
            // Panels are resolved via IPanelNavigationService.ShowPanel<TPanel>() which uses DI to create instances

            // =====================================================================
            // NOTES ON OMISSIONS
            // =====================================================================
            // - DbContext: Registered in Program.cs to avoid dual provider conflict
            // - AIChatControl: Requires IAIAssistantService implementation (not yet available)
        }
    }
}
