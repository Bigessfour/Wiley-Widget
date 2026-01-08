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
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net.Http;
using System.Linq;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;

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
                        ["Logging:LogLevel:Default"] = "Information",
                        ["UI:IsUiTestHarness"] = "true"
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

            // Named HttpClient for Grok with resilience
            services.AddHttpClient("GrokClient")
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
            services.AddMemoryCache();

            // Blazor WebView Services (Required for BlazorWebView controls)
            services.AddWindowsFormsBlazorWebView();

            // Bind Grok recommendation options from configuration (appsettings: GrokRecommendation)
            // Use deferred options configuration so IConfiguration is resolved from the final provider (host builder)
            services.AddOptions<WileyWidget.Business.Configuration.GrokRecommendationOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.GetSection("GrokRecommendation").Bind(opts));

            // =====================================================================
            // DATABASE CONTEXT (Scoped - one per request/scope)
            // =====================================================================

            // For tests, register DbContext with in-memory database
            if (includeDefaults && !services.Any(sd => sd.ServiceType == typeof(AppDbContext)))
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            }
            if (includeDefaults && !services.Any(sd => sd.ServiceType == typeof(IDbContextFactory<AppDbContext>)))
            {
                // CRITICAL: DbContextFactory must be Scoped to avoid lifetime conflicts
                // DbContextOptions internally resolves IDbContextOptionsConfiguration which is scoped
                services.AddDbContextFactory<AppDbContext>((sp, options) =>
                    options.UseInMemoryDatabase("TestDb"), ServiceLifetime.Scoped);
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

            // Telemetry startup service for DB health checks
            services.AddHostedService<TelemetryStartupService>();

            // Startup Timeline Monitoring Service (tracks initialization order and timing)
            if (!services.Any(sd => sd.ServiceType == typeof(IStartupTimelineService)))
            {
                services.AddSingleton<IStartupTimelineService, StartupTimelineService>();
            }

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

            // Data Prefetch Service (Transient - runs once after startup)
            // NOTE: Must not be registered as Singleton because it depends on IDashboardService (transient/scoped)
            services.AddTransient<WileyWidget.Abstractions.IAsyncInitializable, DataPrefetchService>();

            // Budget Category Service (Scoped - works with DbContext)
            services.AddScoped<IBudgetCategoryService, BudgetCategoryService>();

            // Context Service (Scoped - per-request context)
            services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();

            // AI Services (Scoped - may hold request-specific context)
            services.AddScoped<IAIService, XAIService>();

            // Model discovery service for xAI: discovers available models and picks a best-fit based on aliases/families
            services.AddSingleton<IXaiModelDiscoveryService, XaiModelDiscoveryService>();

            services.AddSingleton<IAILoggingService, AILoggingService>();
            services.AddScoped<IConversationRepository, EfConversationRepository>();

            // JARVIS Personality Service (Singleton - stateless personality injection)
            services.AddSingleton<IJARVISPersonalityService, JARVISPersonalityService>();

            // Telemetry Logging Service (Scoped - writes to database)
            services.AddScoped<global::WileyWidget.Services.Abstractions.ITelemetryLogService, TelemetryLogService>();

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

            // DPI-aware image service using Syncfusion ImageListAdv (automatic multi-DPI support)
            services.AddSingleton<DpiAwareImageService>();

            // Path provider: supplies export and other filesystem paths in a testable way
            services.AddSingleton<IPathProvider, PathProvider>();

            // Panel Navigation Service
            // NOTE: This service depends on MainForm's DockingManager + central document panel.
            // Those are created during MainForm deferred initialization (OnShown), so we avoid
            // registering a DI factory that resolves MainForm (circular dependency).
            // MainForm creates PanelNavigationService once docking is ready.

            // UI Configuration (Singleton)
            services.AddSingleton(static sp =>
                UIConfiguration.FromConfiguration(DI.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(sp)));

            // Chat Bridge Service (Singleton - event-based communication between Blazor and WinForms)
            services.AddSingleton<IChatBridgeService, ChatBridgeService>();

            // Grok AI agent service (Singleton - reused across chat sessions)
            // Register a lightweight GrokAgentService for tests and DI validation. Heavy initialization is deferred to InitializeAsync().
            if (!services.Any(sd => sd.ServiceType == typeof(GrokAgentService)))
            {
                services.AddSingleton<GrokAgentService>(sp => Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<GrokAgentService>(sp));
            }

            // Proactive Insights Background Service (Hosted Service - runs continuously)
            // Analyzes enterprise data using Grok and publishes insights to observable collection
            services.AddSingleton<ProactiveInsightsService>();
            services.AddHostedService<ProactiveInsightsService>(sp => DI.ServiceProviderServiceExtensions.GetRequiredService<ProactiveInsightsService>(sp));

            // =====================================================================
            // VIEWMODELS (Scoped - One instance per panel scope)
            // ScopedPanelBase<T> requires ViewModels to be scoped for proper lifecycle management
            // =====================================================================

            services.AddScoped<ChartViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<UtilityBillViewModel>();
            services.AddScoped<AccountsViewModel>();
            services.AddScoped<DashboardViewModel>();
            services.AddScoped<AnalyticsViewModel>();
            services.AddScoped<BudgetOverviewViewModel>();
            services.AddScoped<BudgetViewModel>();
            services.AddScoped<CustomersViewModel>();
            services.AddScoped<MainViewModel>();
            services.AddScoped<ReportsViewModel>();
            services.AddScoped<DepartmentSummaryViewModel>();
            services.AddScoped<RevenueTrendsViewModel>();
            services.AddScoped<AuditLogViewModel>();
            services.AddScoped<RecommendedMonthlyChargeViewModel>();
            services.AddScoped<QuickBooksViewModel>();
            services.AddScoped<ChatPanelViewModel>();
            services.AddScoped<InsightFeedViewModel>();

            // =====================================================================
            // CONTROLS / PANELS (Scoped - One instance per panel scope)
            // Panels are UI controls that display ViewModels and must be scoped for proper DI resolution
            // =====================================================================

            services.AddScoped<WileyWidget.WinForms.Controls.ChatPanel>();

            // =====================================================================
            // FORMS (Singleton for MainForm, Transient for child forms)
            // MainForm: Singleton because it's the application's main window (lives for app lifetime)
            // Child Forms: Transient because they're created/disposed multiple times
            // =====================================================================

            // Main Form (Scoped - resolved from UI scope to ensure scoped dependencies are available)
            services.AddScoped<MainForm>();

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
