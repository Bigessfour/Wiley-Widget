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
                services.AddSingleton<IConfiguration>(defaultConfig);
            }

            // Logging (Singleton - Serilog logger)
            services.AddSingleton(Serilog.Log.Logger);

            // Health Check Configuration (Singleton)
            services.AddSingleton(new HealthCheckConfiguration());

            // HTTP Client Factory (Singleton factory, Transient clients)
            services.AddHttpClient();

            // Memory Cache (Singleton)
            services.AddMemoryCache();

            // =====================================================================
            // DATABASE CONTEXT (Scoped - one per request/scope)
            // =====================================================================

            // For tests, register DbContext with in-memory database
            if (!services.Any(sd => sd.ServiceType == typeof(AppDbContext)))
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            }
            if (!services.Any(sd => sd.ServiceType == typeof(IDbContextFactory<AppDbContext>)))
            {
                // Register DbContextOptions as singleton to avoid lifetime conflicts with the factory
                services.AddSingleton(sp =>
                {
                    var builder = new DbContextOptionsBuilder<AppDbContext>();
                    builder.UseInMemoryDatabase("TestDb");
                    return builder.Options;
                });

                services.AddDbContextFactory<AppDbContext>((sp, options) =>
                    options.UseInMemoryDatabase("TestDb"));
            }

            // =====================================================================
            // DATABASE REPOSITORIES (Scoped - one per request/scope)
            // Per Microsoft: DbContext types use Scoped lifetime by default
            // Repository pattern over DbContext should also be Scoped
            // =====================================================================

            services.AddScoped<IAccountsRepository, AccountsRepository>();
            services.AddScoped<Business.Interfaces.IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IAuditRepository, AuditRepository>();
            services.AddScoped<IBudgetRepository, BudgetRepository>();
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
            services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
            services.AddScoped<IUtilityBillRepository, UtilityBillRepository>();
            services.AddScoped<IUtilityCustomerRepository, UtilityCustomerRepository>();

            // =====================================================================
            // CORE APPLICATION SERVICES (Singleton - Stateless, Thread-Safe)
            // =====================================================================

            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISettingsService>(sp => DI.ServiceProviderServiceExtensions.GetRequiredService<SettingsService>(sp));
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            services.AddSingleton<HealthCheckService>();
            services.AddSingleton<ErrorReportingService>();
            services.AddSingleton<ITelemetryService, SigNozTelemetryService>();

            // Startup Timeline Monitoring Service (tracks initialization order and timing)
            services.AddSingleton<IStartupTimelineService, StartupTimelineService>();

            // Startup orchestration (license, theme, DI validation)
            services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

            // DI Validation Service (uses layered approach: core + WinForms-specific wrapper)
            services.AddSingleton<IDiValidationService, DiValidationService>();
            services.AddSingleton<IWinFormsDiValidator, WinFormsDiValidator>();

            // =====================================================================
            // BUSINESS DOMAIN SERVICES
            // =====================================================================

            // QuickBooks Integration (Singleton - external API client)
            services.AddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();
            services.AddSingleton<IQuickBooksService, QuickBooksService>();

            // Dashboard Service (Transient - short-lived data aggregation)
            services.AddTransient<IDashboardService, DashboardService>();

            // Budget Category Service (Scoped - works with DbContext)
            services.AddScoped<IBudgetCategoryService, BudgetCategoryService>();

            // Context Service (Scoped - per-request context)
            services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();

            // AI Services (Scoped - may hold request-specific context)
            services.AddScoped<IAIService, XAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();

            // Audit Service (Singleton - writes to repository through scopes)
            services.AddSingleton<IAuditService, AuditService>();

            // =====================================================================
            // REPORTING & EXPORT SERVICES
            // =====================================================================

            // Report Services (Singleton - stateless report generation)
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IReportService, FastReportService>();

            // Excel Services (Transient - I/O operations, disposable)
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();

            // =====================================================================
            // UTILITY & CALCULATION SERVICES (Transient - Stateless, Per-Use)
            // =====================================================================

            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddTransient<IAnalyticsService, AnalyticsService>();

            // Analytics Pipeline (Scoped - may aggregate data across request)
            services.AddScoped<IAnalyticsPipeline, AnalyticsPipeline>();
            services.AddScoped<IGrokSupercomputer, NullGrokSupercomputer>();

            // Department Expense & Recommendation Services (Scoped - may query external APIs)
            services.AddScoped<IDepartmentExpenseService, Business.Services.DepartmentExpenseService>();
            services.AddScoped<IGrokRecommendationService, Business.Services.GrokRecommendationService>();

            // =====================================================================
            // UI SERVICES & THEME (Singleton - Application-wide state)
            // =====================================================================

            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IThemeIconService, ThemeIconService>();

            // Panel Navigation Service
            // NOTE: This service depends on MainForm's DockingManager + central document panel.
            // Those are created during MainForm deferred initialization (OnShown), so we avoid
            // registering a DI factory that resolves MainForm (circular dependency).
            // MainForm creates PanelNavigationService once docking is ready.

            // UI Configuration (Singleton)
            services.AddSingleton(static sp =>
                UIConfiguration.FromConfiguration(DI.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(sp)));

            // =====================================================================
            // VIEWMODELS (Transient - New instance per form/view)
            // Per Microsoft: ViewModels are typically Transient as they represent
            // view-specific state and shouldn't be shared
            // =====================================================================

            services.AddTransient<ChartViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AccountsViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<BudgetOverviewViewModel>();
            services.AddTransient<BudgetViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<DepartmentSummaryViewModel>();
            services.AddTransient<RevenueTrendsViewModel>();
            services.AddTransient<AuditLogViewModel>();
            services.AddTransient<RecommendedMonthlyChargeViewModel>();

            // =====================================================================
            // FORMS (Singleton for MainForm, Transient for child forms)
            // MainForm: Singleton because it's the application's main window (lives for app lifetime)
            // Child Forms: Transient because they're created/disposed multiple times
            // =====================================================================

            // Main Form (Singleton - Application's primary window)
            services.AddSingleton<MainForm>();

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
