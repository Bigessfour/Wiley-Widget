using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Services.Http;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Plugins;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net.Http;
using System.Linq;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Syncfusion.Blazor;
using Serilog;
using Serilog.Extensions.Logging;
using WileyWidget.WinForms.Controls;

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

        public static IServiceCollection AddWinFormsServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.TryAddSingleton<IConfiguration>(configuration);

            // Register QuickBooks OAuth configuration BEFORE calling ConfigureServicesInternal
            services.Configure<QuickBooksOAuthOptions>(configuration.GetSection("Services:QuickBooks:OAuth"));

            ConfigureServicesInternal(services, includeDefaults: true);
            return services;
        }

        public static IServiceProvider ConfigureServices()
        {
            var services = CreateServiceCollection();

            // Ensure core services (moved into WileyWidget.Services) are available when using the standalone ConfigureServices helper
            var defaultConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:IsUiTestHarness"] = "true"
                })
                .Build();

            services.AddWileyWidgetCoreServices(defaultConfig);

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
            if (includeDefaults)
            {
                var defaultConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                        ["Logging:LogLevel:Default"] = "Information",
                        ["UI:IsUiTestHarness"] = "true"
                    })
                    .Build();
                services.TryAddSingleton<IConfiguration>(defaultConfig);
            }

            // Logging (Singleton - Serilog logger)
            services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(sp =>
                new Serilog.Extensions.Logging.SerilogLoggerFactory(Serilog.Log.Logger)
                    .CreateLogger(string.Empty));

            // Also register Serilog's native logger for components that depend on Serilog.ILogger directly
            services.AddSingleton<Serilog.ILogger>(_ => Serilog.Log.Logger);

            // Microsoft Logging Framework (provides ILogger<T> for dependency injection)
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(Serilog.Log.Logger);
            });

            // Health Check Configuration (Singleton)
            services.AddSingleton(new HealthCheckConfiguration());

            // Register Microsoft health checks to provide HealthCheckService for custom wrapper
            services.AddHealthChecks();

            // HTTP Client Factory (Singleton factory, Transient clients)
            services.AddHttpClient();

            // Global resilience enricher - logs resilience events (retries, breaks, hedging) to Serilog/ILogger
            // Provides free structured logging like: "[INF] Retry attempt 2 for GrokResilience after 503"
            services.AddResilienceEnricher();

            // =====================================================================
            // NAMED HTTP CLIENTS WITH RESILIENCE PIPELINES
            // =====================================================================
            // Each service has a tailored pipeline: xAI favors speed (hedging), QB favors reliability (long backoff)

            // XAI/Grok Pipeline: High-volume, rate-limit heavy - prioritize retries + hedging for low latency
            services.AddHttpClient("GrokClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddHttpMessageHandler(sp =>
                {
                    var logger = DI.ServiceProviderServiceExtensions.GetService<ILogger<HttpBulkheadHandler>>(sp);
                    return new HttpBulkheadHandler(maxConcurrentRequests: 20, clientName: "GrokClient", logger: logger);
                })
                .AddResilienceHandler("GrokResilience", builder =>
                {
                    // Retry: Exponential backoff, handle transients + 429
                    builder.AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 6,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(1),
                        MaxDelay = TimeSpan.FromSeconds(15),
                        ShouldHandle = args => new ValueTask<bool>(
                            args.Outcome.Exception is HttpRequestException ||
                            (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||     // 429
                             args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||  // 503
                             (int?)args.Outcome.Result?.StatusCode >= 500))  // 5xx
                    });

                    // Hedging: Fire parallel attempts for faster response (great for Grok latency)
                    // When initial attempt is slow, spawn hedged attempts staggered by 500ms
                    builder.AddHedging(new HttpHedgingStrategyOptions
                    {
                        MaxHedgedAttempts = 3,
                        Delay = TimeSpan.FromMilliseconds(500)
                    });

                    // Circuit Breaker: Prevent hammering when Grok is down
                    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = 10,
                        BreakDuration = TimeSpan.FromMinutes(2),
                        SamplingDuration = TimeSpan.FromMinutes(1)
                    });

                    // Timeout per call
                    builder.AddTimeout(new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(15)
                    });

                    // NOTE: Bulkhead implemented at application level via HttpBulkheadHandler (20 concurrent limit)
                    // Circuit breaker and timeout provide additional backpressure for Grok API rate limits
                });

            // QuickBooks Pipeline: OAuth + rate limits - longer backoff, stronger breaker
            services.AddHttpClient("QuickBooksClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddHttpMessageHandler(sp =>
                {
                    var logger = DI.ServiceProviderServiceExtensions.GetService<ILogger<HttpBulkheadHandler>>(sp);
                    return new HttpBulkheadHandler(maxConcurrentRequests: 10, clientName: "QuickBooksClient", logger: logger);
                })
                .AddResilienceHandler("QuickBooksResilience", builder =>
                {
                    // Retry: Exponential backoff for Intuit's strict rate limiting
                    builder.AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 4,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(3),
                        MaxDelay = TimeSpan.FromSeconds(30),
                        ShouldHandle = args => new ValueTask<bool>(
                            args.Outcome.Exception is HttpRequestException ||
                            (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||     // 429 rate limit
                             args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.Unauthorized ||        // 401 token refresh
                             (int?)args.Outcome.Result?.StatusCode >= 500))  // 5xx
                    });

                    // Circuit Breaker: Strong - Intuit downtime is rare but serious
                    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.3,
                        MinimumThroughput = 8,
                        BreakDuration = TimeSpan.FromMinutes(5),
                        SamplingDuration = TimeSpan.FromMinutes(1)
                    });

                    // Timeout: QB calls can be slow (report generation, sync operations)
                    builder.AddTimeout(new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(60)
                    });

                    // NOTE: Bulkhead implemented at application level via HttpBulkheadHandler (10 concurrent limit)
                    // Circuit breaker and timeout provide additional backpressure for QB API rate limits
                });

            // Memory Cache (Singleton)
            // Per Microsoft documentation (https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory):
            // "Create a cache singleton for caching" - this prevents premature disposal during DI scope cleanup
            // The framework's AddMemoryCache() was being disposed too early causing ObjectDisposedException in repositories
            services.AddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(sp =>
            {
                var options = new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
                {
                    // SizeLimit = 1024: Prevent unbounded cache growth per Microsoft
                    // https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-setsize-size-and-sizelimit-to-limit-cache-size
                    // Quote: "If SizeLimit isn't set, the cache grows without bound. Apps must be architected to limit cache growth."
                    // Units are arbitrary; 1024 allows ~200-300 typical entries (1-5 units each per MemoryCacheService.MapOptions)
                    SizeLimit = 1024
                };
                return new Microsoft.Extensions.Caching.Memory.MemoryCache(options);
            });

            // Blazor WebView Services (Required for BlazorWebView controls)
            services.AddWindowsFormsBlazorWebView();

            // Syncfusion Blazor Components (for InteractiveChat and other Blazor components)
            // NOTE: Smart Components (AI-powered textarea, etc.) are not yet available in Syncfusion.Blazor.SmartComponents
            // The package exists but the actual components/APIs are not released yet
            services.AddSyncfusionBlazor();

            // Bind Grok recommendation options from configuration (appsettings: GrokRecommendation)
            // Use deferred options configuration so IConfiguration is resolved from the final provider (host builder)
            services.AddOptions<WileyWidget.Business.Configuration.GrokRecommendationOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.GetSection("GrokRecommendation").Bind(opts));

            services.AddOptions<StartupOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.GetSection("Startup").Bind(opts));

            services.AddOptions<WileyWidget.Models.AppOptions>()
                .Configure<IConfiguration>((opts, cfg) => cfg.Bind(opts));

            // Report Viewer Launch Options (Singleton - command-line triggers for report viewer)
            services.AddSingleton(ReportViewerLaunchOptions.Disabled);

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
            // Adapter: map legacy Services.Abstractions.IActivityLogRepository to a WinForms adapter
            // that delegates to the canonical Business layer implementation. This preserves
            // compatibility for consumers compiled against the Abstractions package while
            // keeping the authoritative implementation in the Business project.
            services.AddScoped<WileyWidget.Services.Abstractions.IActivityLogRepository, ActivityLogRepositoryAdapter>();
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

            // Application event bus for cross-scope in-process notifications
            // Registered in core via AddWileyWidgetCoreServices(configuration)

            // Startup Timeline Monitoring Service (tracks initialization order and timing)
            services.TryAddSingleton<IStartupTimelineService, StartupTimelineService>();

            // Startup orchestration (license, theme, DI validation)
            services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

            // Deferred async startup service - runs after MainForm is shown
            // Prevents blocking UI thread with heavy initialization
            services.AddHostedService<StartupHostedService>();

            // DI Validation Service (uses layered approach: core + WinForms-specific wrapper)
            services.AddSingleton<DiValidationService>();
            services.AddSingleton<WileyWidget.Services.Abstractions.IDiValidationService, DiValidationService>();
            services.AddSingleton<IWinFormsDiValidator, WinFormsDiValidator>();

            // =====================================================================
            // BUSINESS DOMAIN SERVICES
            // =====================================================================

            // QuickBooks Token Store (in-memory cache + disk persistence)
            services.TryAddSingleton<QuickBooksTokenStore>();

            // QuickBooks Auth Service (OAuth token lifecycle management - public interface for DI)
            services.TryAddSingleton<IQuickBooksAuthService, QuickBooksAuthService>();
            services.TryAddSingleton<QuickBooksAuthService>(sp =>
                (QuickBooksAuthService)DI.ServiceProviderServiceExtensions.GetRequiredService<IQuickBooksAuthService>(sp));

            // QuickBooks OAuth Callback Handler (HTTP listener on port 5000 for OAuth redirect)
            services.TryAddSingleton<QuickBooksOAuthCallbackHandler>();

            // QuickBooks Token Store (persists and caches OAuth tokens + realm ID)
            services.TryAddSingleton<QuickBooksTokenStore>();

            // QuickBooks Integration Services (with resilience & proper lifecycle)
            // HttpClient factory already registered above with resilience for QB
            services.TryAddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();
            services.TryAddSingleton<IQuickBooksService, QuickBooksService>();

            // QuickBooks Account Data Services (Company Info & Chart of Accounts)
            services.AddSingleton<IQuickBooksCompanyInfoService, QuickBooksCompanyInfoService>();
            services.AddSingleton<IQuickBooksChartOfAccountsService, QuickBooksChartOfAccountsService>();

            // QuickBooks Sandbox Seeding Service (creates sample accounts after OAuth)
            services.AddSingleton<IQuickBooksSandboxSeederService, QuickBooksSandboxSeederService>();
            // NOTE: QuickBooksAuthService is now available via IQuickBooksAuthService

            // Business service to sync QuickBooks actuals into BudgetEntries
            services.AddScoped<WileyWidget.Business.Interfaces.IQuickBooksBudgetSyncService, WileyWidget.Business.Services.QuickBooksBudgetSyncService>();

            // Dashboard Service (Transient - short-lived data aggregation)
            services.AddTransient<IDashboardService, DashboardService>();

            // Data Prefetch Service (Transient - runs once after startup)
            // NOTE: Must not be registered as Singleton because it depends on IDashboardService (transient/scoped)
            services.AddTransient<WileyWidget.Abstractions.IAsyncInitializable, DataPrefetchService>();

            // Budget Category Service (Scoped - works with DbContext)
            services.AddScoped<IBudgetCategoryService, BudgetCategoryService>();

            // Context Service (Scoped - per-request context)
            services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();

            // User Context (Scoped - for Blazor components and user-specific context in BlazorWebView)
            services.AddScoped<IUserContext, WileyWidget.Services.UserContext>();

            // AI Services (Scoped - may hold request-specific context)
            services.AddScoped<IAIService>(static sp => (GrokAgentService)sp.GetService(typeof(GrokAgentService))!);

            // Model discovery service for xAI: discovers available models and picks a best-fit based on aliases/families
            services.AddSingleton<IXaiModelDiscoveryService, XaiModelDiscoveryService>();

            services.TryAddScoped<IAILoggingService, AILoggingService>();

            // AI-Powered Search and Analysis Services
            services.AddSingleton<WileyWidget.Services.Abstractions.ISemanticSearchService, WileyWidget.Services.SemanticSearchService>();
            services.AddSingleton<WileyWidget.Services.Abstractions.IAnomalyDetectionService, WileyWidget.Services.AnomalyDetectionService>();
            services.AddScoped<IConversationRepository, EfConversationRepository>();

            // JARVIS Personality Service (Scoped - depends on IAILoggingService which is scoped)
            services.AddScoped<IJARVISPersonalityService, JARVISPersonalityService>();

            // Telemetry Logging Service (Scoped - writes to database)
            services.AddScoped<global::WileyWidget.Services.Abstractions.ITelemetryLogService, TelemetryLogService>();

            // Audit Service (Scoped - depends on repositories and logging which are scoped)
            services.AddScoped<IAuditService, AuditService>();

            // Global Search Service (Singleton - stateless search aggregation across modules)
            services.AddSingleton<IGlobalSearchService, GlobalSearchService>();

            // =====================================================================
            // REPORTING & EXPORT SERVICES
            // =====================================================================

            // Report Services (Singleton - stateless report generation)
            services.TryAddScoped<IReportExportService, ReportExportService>();
            services.AddSingleton<IReportService, FastReportService>();

            // Excel Services (Transient - I/O operations, disposable)
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();

            // =====================================================================
            // UTILITY & CALCULATION SERVICES (Transient - Stateless, Per-Use)
            // =====================================================================

            services.TryAddScoped<IDataAnonymizerService, DataAnonymizerService>();
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddTransient<IAnalyticsService, AnalyticsService>();

            // Analytics Pipeline (Scoped - may aggregate data across request)
            services.AddScoped<IAnalyticsPipeline, AnalyticsPipeline>();
            services.AddScoped<IGrokSupercomputer, GrokSupercomputer>();

            // Department Expense & Recommendation Services (Scoped - may query external APIs)
            services.AddScoped<IDepartmentExpenseService, Business.Services.DepartmentExpenseService>();
            services.AddScoped<IGrokRecommendationService, Business.Services.GrokRecommendationService>();

            // What-If Scenario Engine (Scoped - generates comprehensive financial scenarios)
            services.AddScoped<IWhatIfScenarioEngine, WhatIfScenarioEngine>();

            // =====================================================================
            // SEMANTIC KERNEL PLUGINS (Scoped - Injected into Kernel via KernelPluginRegistrar)
            // =====================================================================

            // Rate Scenario Tools (Scoped - provides rate analysis and what-if scenario functions to Grok kernel)
            // Depends on IWhatIfScenarioEngine and IChargeCalculatorService (both registered above)
            services.AddScoped<RateScenarioTools>();

            // =====================================================================
            // UI SERVICES & THEME (Singleton - Application-wide state)
            // =====================================================================

            // Panel Navigation Service
            // NOTE: NOT registered in DI because it requires both MainForm and DockingManager,
            // which are UI components not created until the form is shown.
            // MainForm creates this directly in OnShown() after docking manager is initialized.
            // See: MainForm.OnShown() - line 378

            // UI Configuration (Singleton)
            services.AddSingleton(static sp =>
                UIConfiguration.FromConfiguration(DI.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(sp)));

            // Theme Service (Singleton - manages global theme state and notifications)
            services.AddSingleton<IThemeService, ThemeService>();

            // Activity Log Service (Singleton - tracks navigation and application events for audit trail)
            services.AddSingleton<IActivityLogService, ActivityLogService>();

            // Chat Bridge Service (Singleton - event-based communication between Blazor and WinForms)
            services.AddSingleton<IChatBridgeService, ChatBridgeService>();

            // =====================================================================
            // XAI GROK AI AGENT CONFIGURATION
            // =====================================================================

            // Grok API Key Provider (Singleton - loads from user.secrets, env vars, config)
            // Follows Microsoft configuration hierarchy:
            // 1. User Secrets (XAI:ApiKey) - highest priority, secure
            // 2. Environment Variables (XAI_API_KEY) - CI/CD friendly
            // 3. appsettings.json (XAI:ApiKey) - lowest priority, public
            services.AddSingleton<IGrokApiKeyProvider, GrokApiKeyProvider>();

            // Grok Health Checks (registered with health check service)
            services.AddHealthChecks()
                .AddCheck<GrokHealthCheck>("grok-api", tags: new[] { "startup", "ai" })
                .AddCheck<ChatHistoryHealthCheck>("chat-history", tags: new[] { "startup", "persistence" });

            // Grok AI agent service (Scoped - depends on IJARVISPersonalityService which is scoped)
            // Register as Scoped since it depends on scoped services. Heavy initialization is deferred to InitializeAsync().
            // NOTE: Scoped lifetime is correct here: GrokAgentService is instantiated per-scope and can safely depend on scoped dependencies.
            // Use AddScoped directly instead of ActivatorUtilities.CreateInstance to avoid trying to instantiate during validation.
            services.TryAddScoped<GrokAgentService>();

            // Proactive Insights Background Service (Hosted Service - runs continuously)
            // Analyzes enterprise data using Grok and publishes insights to observable collection
            // NOTE: ProactiveInsightsService accepts GrokAgentService as optional dependency in constructor,
            // so it can be singleton even though GrokAgentService is now scoped. The optional null value
            // is acceptable for graceful degradation during DI validation.
            services.AddSingleton<ProactiveInsightsService>(sp =>
            {
                // ProactiveInsightsService accepts nullable GrokAgentService for graceful degradation
                // Do not try to resolve it from DI since it's scoped - pass null instead
                var logger = DI.ServiceProviderServiceExtensions.GetService<ILogger<ProactiveInsightsService>>(sp);
                return new ProactiveInsightsService(grokAgentService: null, logger: logger);
            });
            services.AddHostedService<ProactiveInsightsService>(sp => DI.ServiceProviderServiceExtensions.GetRequiredService<ProactiveInsightsService>(sp));

            // =====================================================================
            // TIER 3+ ADVANCED UI SERVICES (Enterprise Features)
            // =====================================================================

            // Real-time Dashboard Service (Singleton - manages live data updates)
            services.AddSingleton<RealtimeDashboardService>();

            // User Preferences Service (Singleton - manages user settings persistence)
            // Registered in core via AddWileyWidgetCoreServices(configuration)

            // Role-Based Access Control (Singleton - manages permissions and roles)
            services.AddSingleton<RoleBasedAccessControl>();

            // Enterprise Audit Logger (Scoped - logs all user actions for compliance)
            services.AddScoped<EnterpriseAuditLogger>();

            // Advanced Search Service (Singleton - cross-grid search capability)
            services.AddSingleton<AdvancedSearchService>();

            // FloatingPanelManager and DockingKeyboardNavigator are UI-scoped helpers that depend
            // on runtime UI objects (MainForm, Syncfusion DockingManager). Registering them at
            // the root DI container causes ValidateOnBuild to attempt resolution of framework
            // UI types and fail during host build. Instantiate these classes at runtime after
            // the MainForm and DockingManager are created (for example, in MainForm.OnShown
            // or PanelNavigationService). Do NOT register them here to avoid build-time DI validation.

            // =====================================================================
            // MAINFORM PERSISTENCE & IMPORT SERVICES
            // =====================================================================

            // Window State Service (Singleton - manages window position, size, and MRU list persistence via Registry)
            services.AddSingleton<IWindowStateService, WindowStateService>();

            // File Import Service (Transient - async file import with JSON/XML parsing support)
            // Registered in core via AddWileyWidgetCoreServices(configuration)

            // =====================================================================
            // VIEWMODELS (Scoped - One instance per panel scope)
            // ScopedPanelBase<T> requires ViewModels to be scoped for proper lifecycle management
            // =====================================================================

            services.AddScoped<IWarRoomViewModel, WarRoomViewModel>();
            services.AddScoped<WarRoomViewModel>();
            services.AddScoped<IBudgetAnalyticsViewModel, BudgetAnalyticsViewModel>();
            services.AddScoped<BudgetAnalyticsViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<UtilityBillViewModel>();
            services.AddScoped<AccountsViewModel>();
            services.AddScoped<DashboardViewModel>();
            services.AddScoped<AnalyticsViewModel>();
            services.AddScoped<ChartViewModel>();
            services.AddScoped<BudgetOverviewViewModel>();
            services.AddScoped<BudgetViewModel>();
            services.AddScoped<CustomersViewModel>();
            services.AddScoped<WileyWidget.WinForms.Forms.MainViewModel>();
            services.AddScoped<ReportsViewModel>();
            services.AddScoped<DepartmentSummaryViewModel>();
            services.AddScoped<RevenueTrendsViewModel>();
            services.AddScoped<AuditLogViewModel>();
            services.AddScoped<RecommendedMonthlyChargeViewModel>();
            services.AddScoped<IQuickBooksViewModel, QuickBooksViewModel>();
            services.AddScoped<QuickBooksViewModel>();
            services.AddScoped<IInsightFeedViewModel, InsightFeedViewModel>();
            services.AddScoped<InsightFeedViewModel>();
            services.AddScoped<WileyWidget.WinForms.Controls.ActivityLogViewModel>();
            // Example panels' ViewModels - sometimes omitted during refactor
            services.AddScoped<WileyWidget.WinForms.Examples.AsyncLoadingExampleViewModel>();
            // JARVIS Chat ViewModel for docked chat control
            services.AddScoped<WileyWidget.WinForms.Controls.JARVISChatViewModel>();

            // =====================================================================
            // CONTROLS / PANELS (Scoped - One instance per panel scope)
            // Panels are UI controls that display ViewModels and must be scoped for proper DI resolution
            // =====================================================================

            services.AddScoped<WileyWidget.WinForms.Controls.DashboardPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.AccountsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.BudgetAnalyticsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.BudgetPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.ReportsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.SettingsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.BudgetOverviewPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.DepartmentSummaryPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.RevenueTrendsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.AuditLogPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.ActivityLogPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.CustomersPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.UtilityBillPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.QuickBooksPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.AnalyticsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.ProactiveInsightsPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.WarRoomPanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.RecommendedMonthlyChargePanel>();
            services.AddScoped<WileyWidget.WinForms.Controls.CsvMappingWizardPanel>();

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
        }
    }

}
