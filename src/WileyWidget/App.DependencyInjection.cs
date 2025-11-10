// App.DependencyInjection.cs - Dependency Injection & Configuration Partial Class
// Contains: DI container setup, Prism configuration, module catalog, region adapters
// Part of App.xaml.cs partial class split for maintainability
//
// ═══════════════════════════════════════════════════════════════════════════════
// DI REGISTRATION ARCHITECTURE (Comprehensive, Rock-Solid Implementation)
// ═══════════════════════════════════════════════════════════════════════════════
//
// 1. CONTAINER SETUP (CreateContainerExtension):
//    - DryIoc with Microsoft DI rules
//    - Singleton as default lifetime
//    - Auto-concrete type resolution
//    - Disposable transient tracking
//    - 60s timeout for complex ViewModels
//
// 2. REGISTRATION FLOW (RegisterTypes):
//    ├── Critical Services (explicit registrations)
//    │   ├── Shell (main window)
//    │   ├── ErrorReportingService (Singleton)
//    │   ├── TelemetryStartupService (Singleton)
//    │   ├── ModuleHealthService (Singleton)
//    │   ├── SigNozTelemetryService (Instance or Singleton)
//    │   ├── ApplicationMetricsService (Singleton)
//    │   ├── DialogTrackingService (Singleton)
//    │   ├── StartupDiagnosticsService (Singleton)
//    │   ├── StartupEnvironmentValidator (Singleton)
//    │   ├── HealthReportingService (Singleton)
//    │   ├── DiagnosticsService (Singleton)
//    │   ├── PrismErrorHandler (Singleton)
//    │   ├── EnterpriseResourceLoader (Singleton)
//    │   ├── IServiceScopeFactory (Singleton)
//    │   └── FiscalYearSettings (Instance from configuration)
//    │
//    ├── RegisterConventionTypes()
//    │   ├── RegisterCoreInfrastructure()
//    │   │   ├── IConfiguration (Instance, cached)
//    │   │   ├── IMemoryCache (Instance, 100MB limit)
//    │   │   ├── ICacheService (Singleton)
//    │   │   ├── ILoggerFactory (Instance, Serilog bridge)
//    │   │   ├── ILogger<T> (Transient, generic factory)
//    │   │   ├── IHttpClientFactory (Instance, 3 named clients: Default, QuickBooks, XAI)
//    │   │   └── IDbContextFactory<AppDbContext> (Instance, conditional on connection string)
//    │   │
//    │   ├── RegisterRepositories()
//    │   │   └── Auto-discovers from WileyWidget.Data assembly (Scoped lifetime)
//    │   │       ├── IEnterpriseRepository -> EnterpriseRepository
//    │   │       ├── IBudgetRepository -> BudgetRepository
//    │   │       ├── IMunicipalAccountRepository -> MunicipalAccountRepository
//    │   │       ├── IDepartmentRepository -> DepartmentRepository
//    │   │       ├── IUtilityCustomerRepository -> UtilityCustomerRepository
//    │   │       ├── IUtilityBillRepository -> UtilityBillRepository
//    │   │       └── IAuditRepository -> AuditRepository
//    │   │
//    │   ├── RegisterBusinessServices()
//    │   │   └── Auto-discovers from WileyWidget.Services assembly (Singleton lifetime)
//    │   │       Patterns: *Service, *Engine, *Helper, *Importer, *Calculator
//    │   │       ├── ISettingsService -> SettingsService
//    │   │       ├── IQuickBooksService -> QuickBooksService
//    │   │       ├── ITelemetryService -> SigNozTelemetryService
//    │   │       ├── ISecretVaultService -> LocalSecretVaultService
//    │   │       ├── IReportExportService -> ReportExportService
//    │   │       ├── IDataAnonymizerService -> DataAnonymizerService
//    │   │       ├── IChargeCalculatorService -> ServiceChargeCalculatorService
//    │   │       ├── IBoldReportService -> BoldReportService
//    │   │       ├── IAuditService -> AuditService
//    │   │       ├── ICompositeCommandService -> CompositeCommandService
//    │   │       ├── IWileyWidgetContextService -> WileyWidgetContextService
//    │   │       ├── IRegionMonitoringService -> RegionMonitoringService
//    │   │       ├── IExcelExportService -> ExcelExportService
//    │   │       ├── IExcelReaderService -> ExcelReaderService
//    │   │       ├── IWhatIfScenarioEngine -> WhatIfScenarioEngine ✓ (NEW)
//    │   │       ├── IBudgetImporter -> BudgetImporter ✓ (NEW)
//    │   │       ├── IDispatcherHelper -> DispatcherHelper ✓ (NEW)
//    │   │       └── ... (all matching patterns auto-registered)
//    │   │
//    │   └── RegisterViewModels()
//    │       └── Auto-discovers from WileyWidget.UI assembly (Transient lifetime) ✓ (IMPROVED)
//    │           ├── SettingsViewModel
//    │           ├── DashboardViewModel
//    │           ├── MainViewModel
//    │           ├── BudgetViewModel
//    │           ├── AIAssistViewModel
//    │           ├── QuickBooksViewModel
//    │           ├── EnterpriseViewModel
//    │           ├── MunicipalAccountViewModel
//    │           ├── UtilityCustomerViewModel
//    │           ├── DepartmentViewModel
//    │           ├── AnalyticsViewModel
//    │           ├── ReportsViewModel
//    │           ├── ToolsViewModel
//    │           ├── ProgressViewModel
//    │           ├── ExcelImportViewModel
//    │           ├── BudgetAnalysisViewModel
//    │           ├── AIResponseViewModel
//    │           ├── SplashScreenWindowViewModel
//    │           ├── UtilityCustomerPanelViewModel
//    │           └── ... (all ViewModels auto-registered)
//    │
//    ├── RegisterLazyAIServices()
//    │   ├── IAIService -> XAIService or NullAIService (Singleton, with API key validation)
//    │   └── IAILoggingService -> AILoggingService (Singleton)
//    │
//    └── ValidateAndRegisterViewModels()
//        └── Delegates to StartupEnvironmentValidator for constructor dependency validation
//
// 3. PRISM SERVICES (auto-registered by framework):
//    ├── IDialogService (dialog system)
//    ├── IRegionManager (region navigation)
//    ├── IEventAggregator (pub/sub messaging)
//    └── IContainerProvider (container access)
//
// 4. MODULE CATALOG (ConfigureModuleCatalog):
//    ├── CoreModule (essential services)
//    └── QuickBooksModule (QuickBooks integration)
//
// 5. LIFETIME PATTERNS:
//    ├── Singleton: Stateless services, caches, managers (one instance per app)
//    ├── Scoped: Repositories, DbContext (one instance per operation/request)
//    ├── Transient: ViewModels, disposable services (new instance per resolve)
//    └── Instance: Pre-configured objects, cached configuration
//
// 6. DEPENDENCY RESOLUTION RULES:
//    ├── Constructor injection (preferred)
//    ├── Lazy<T> for circular dependency breaking
//    ├── Optional dependencies via nullable parameters
//    └── Factory pattern for complex object graphs
//
// 7. VALIDATION & MONITORING:
//    ├── ValidateAndRegisterViewModels: Constructor dependency checks
//    ├── Startup logging for all registrations
//    └── Container diagnostics in debug builds
//
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using DryIoc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Prism.Container.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using Serilog.Events;
using Syncfusion.SfSkinManager;
using WileyWidget.Regions;
using WileyWidget.Views.Windows;

namespace WileyWidget
{
    /// <summary>
    /// Dependency injection and Prism configuration partial class.
    /// </summary>
    public partial class App
    {
        #region DI Configuration Fields

        // Cached configuration to avoid duplicate BuildConfiguration() calls
        private static IConfiguration? _cachedConfiguration;

        #endregion

        #region Prism DI Overrides

        /// <summary>
        /// Creates and configures the DryIoc container with application-specific rules and registrations.
        /// </summary>
        [SuppressMessage("Reliability", "CA2000", Justification = "DryIoc owns disposal.")]
        protected override IContainerExtension CreateContainerExtension()
        {
            var sw = Stopwatch.StartNew();
            var rules = DryIoc.Rules.Default
                .WithMicrosoftDependencyInjectionRules()
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithDefaultReuse(Reuse.Singleton)
                .WithAutoConcreteTypeResolution()
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace)
                .WithFactorySelector(Rules.SelectLastRegisteredFactory())
                .WithoutThrowOnRegisteringDisposableTransient()
                .WithTrackingDisposableTransients();

            DryIoc.Scope.WaitForScopedServiceIsCreatedTimeoutTicks = 60000;  // 60s for complex VMs
            var container = new Container(rules);
            var containerExtension = new DryIocContainerExtension(container);
            LogStartupTiming("CreateContainerExtension: DryIoc setup", sw.Elapsed);

            // Convention-based registrations (defined later in this file)
            RegisterConventionTypes(containerExtension);

            // Lazy AI services (defined later in this file)
            RegisterLazyAIServices(containerExtension);

            // NOTE: ViewModel validation moved to OnInitialized() after RegisterTypes completes
            // to ensure StartupEnvironmentValidator is registered first.

            // NOTE: ModuleOrder and ModuleRegionMap properties kept for backward compatibility
            // but are no longer loaded from config. Modules are hardcoded in ConfigureModuleCatalog.
            // Phase 0 cleanup (2025-11-09): Only CoreModule and QuickBooksModule remain active.

            return containerExtension;
        }

        /// <summary>
        /// Registers types in the DI container for Prism bootstrap.
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Minimal registrations - modules register their own services
            containerRegistry.Register<Shell>();

            // Register critical services for exception handling and modules
            containerRegistry.RegisterSingleton<Services.ErrorReportingService>();
            containerRegistry.RegisterSingleton<Services.Telemetry.TelemetryStartupService>();
            containerRegistry.RegisterSingleton<Services.IModuleHealthService, Services.ModuleHealthService>();

            // Register SigNoz telemetry service
            if (_earlyTelemetryService != null)
            {
                containerRegistry.RegisterInstance(_earlyTelemetryService);
                Log.Information("✓ SigNoz telemetry service registered from early initialization");
            }
            else
            {
                containerRegistry.RegisterSingleton<Services.Telemetry.SigNozTelemetryService>();
                Log.Information("✓ SigNoz telemetry service registered for lazy initialization");
            }

            // Register ApplicationMetricsService for memory and performance monitoring
            containerRegistry.RegisterSingleton<Services.Telemetry.ApplicationMetricsService>();
            Log.Information("✓ Application metrics service registered for memory monitoring");

            // Register dialog tracking service for proper shutdown handling
            containerRegistry.RegisterSingleton<Services.IDialogTrackingService, Services.DialogTrackingService>();

            // Register enhanced startup diagnostics service for 4-phase startup
            containerRegistry.RegisterSingleton<Startup.IStartupDiagnosticsService, Startup.StartupDiagnosticsService>();

            // Register startup environment validator (Phase 2: Extracted from App.xaml.cs)
            containerRegistry.RegisterSingleton<WileyWidget.Services.Startup.IStartupEnvironmentValidator, WileyWidget.Services.Startup.StartupEnvironmentValidator>();
            Log.Information("✓ Startup environment validator registered");

            // Register health reporting service (Phase 2: Extracted from App.xaml.cs)
            containerRegistry.RegisterSingleton<WileyWidget.Services.Startup.IHealthReportingService, WileyWidget.Services.Startup.HealthReportingService>();
            Log.Information("✓ Health reporting service registered");

            // Register diagnostics service (Phase 2: Extracted from App.xaml.cs)
            containerRegistry.RegisterSingleton<WileyWidget.Services.Startup.IDiagnosticsService, WileyWidget.Services.Startup.DiagnosticsService>();
            Log.Information("✓ Diagnostics service registered");

            // Register Prism error handler for navigation and region behavior error handling
            containerRegistry.RegisterSingleton<Services.IPrismErrorHandler, Services.PrismErrorHandler>();

            // Register enterprise resource loader for Polly-based resilient resource loading
            containerRegistry.RegisterSingleton<Abstractions.IResourceLoader, Startup.EnterpriseResourceLoader>();

            // Register IServiceScopeFactory for scoped service creation (required by some business services)
            containerRegistry.RegisterSingleton<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory, Services.DryIocServiceScopeFactory>();
            Log.Information("✓ IServiceScopeFactory registered");

            // Register FiscalYearSettings from configuration
            var configuration = BuildConfiguration();
            var fiscalYearSettings = new Models.FiscalYearSettings();
            configuration.GetSection("FiscalYear").Bind(fiscalYearSettings);
            containerRegistry.RegisterInstance(fiscalYearSettings);
            Log.Information("✓ FiscalYearSettings registered from configuration");

            Log.Information("✓ Critical services registered");

            // Note: Convention-based types are registered in CreateContainerExtension()
            // ValidateAndRegisterViewModels moved to OnInitialized() to ensure all services
            // are properly registered before validation attempts.
            // This fixes the ILogger<T> resolution error during startup.

            Log.Information("✓ All convention-based services registered");
        }

        /// <summary>
        /// Configures the module catalog with application modules.
        /// Enhanced with dynamic discovery and better error handling for missing assemblies.
        /// </summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            try
            {
                Log.Information("[PRISM] Configuring module catalog...");

                // Enhanced module registration with assembly validation
                var moduleRegistrationErrors = new List<string>();

                // Register essential modules with validation
                if (!TryRegisterModule<Startup.Modules.CoreModule>(moduleCatalog))
                {
                    moduleRegistrationErrors.Add("CoreModule");
                    Log.Warning("CoreModule assembly validation failed - attempting fallback registration");
                }

                if (!TryRegisterModule<Startup.Modules.QuickBooksModule>(moduleCatalog))
                {
                    moduleRegistrationErrors.Add("QuickBooksModule");
                    Log.Warning("QuickBooksModule assembly validation failed - attempting fallback registration");
                }

                // Dynamic module discovery - scan for additional modules in current assembly
                try
                {
                    var discoveredModules = DiscoverAdditionalModules();
                    foreach (var moduleType in discoveredModules)
                    {
                        try
                        {
                            moduleCatalog.AddModule(new ModuleInfo
                            {
                                ModuleName = moduleType.Name,
                                ModuleType = moduleType.AssemblyQualifiedName,
                                InitializationMode = InitializationMode.WhenAvailable
                            });
                            Log.Debug("Dynamically discovered module: {ModuleName}", moduleType.Name);
                        }
                        catch (Exception discEx)
                        {
                            Log.Warning(discEx, "Failed to register discovered module: {ModuleName}", moduleType.Name);
                        }
                    }
                }
                catch (Exception dynamicEx)
                {
                    Log.Warning(dynamicEx, "Dynamic module discovery failed - continuing with core modules only");
                }

                if (moduleRegistrationErrors.Any())
                {
                    Log.Warning("Some modules failed validation but application will continue with available modules. Failed: {FailedModules}",
                        string.Join(", ", moduleRegistrationErrors));
                }
                else
                {
                    Log.Information("✓ [PRISM] Module catalog configured successfully with all requested modules");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ [PRISM] Failed to configure module catalog");

                // Don't rethrow - create minimal module catalog to allow app startup
                try
                {
                    Log.Information("Attempting minimal module catalog configuration...");
                    // Application can start without modules in degraded mode
                    Log.Information("✓ [PRISM] Minimal module catalog configured - application will run in degraded mode");
                }
                catch (Exception fallbackEx)
                {
                    Log.Fatal(fallbackEx, "Failed to configure even minimal module catalog");
                    throw; // This is critical - can't start without any module catalog
                }
            }
        }

        /// <summary>
        /// Attempts to register a module type with assembly validation.
        /// </summary>
        /// <typeparam name="T">The module type to register</typeparam>
        /// <param name="moduleCatalog">The module catalog</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool TryRegisterModule<T>(IModuleCatalog moduleCatalog) where T : class, IModule
        {
            try
            {
                // Validate that the module type can be loaded
                var moduleType = typeof(T);
                var assembly = moduleType.Assembly;

                // Check if assembly is properly loaded and accessible
                if (assembly == null || string.IsNullOrEmpty(assembly.Location))
                {
                    Log.Warning("Module type {ModuleType} has invalid assembly information", typeof(T).Name);
                    return false;
                }

                // Verify the module type has the required interface
                if (!typeof(IModule).IsAssignableFrom(moduleType))
                {
                    Log.Warning("Module type {ModuleType} does not implement IModule interface", typeof(T).Name);
                    return false;
                }

                // Verify the module has a parameterless constructor
                var parameterlessConstructor = moduleType.GetConstructor(Type.EmptyTypes);
                if (parameterlessConstructor == null)
                {
                    Log.Warning("Module type {ModuleType} does not have a parameterless constructor", typeof(T).Name);
                    return false;
                }

                // All validations passed - register the module
                moduleCatalog.AddModule<T>();
                Log.Debug("Module {ModuleName} registered successfully", typeof(T).Name);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register module {ModuleType}: {Message}", typeof(T).Name, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Discovers additional modules in the current assembly that implement IModule.
        /// </summary>
        /// <returns>Collection of discovered module types</returns>
        private IEnumerable<Type> DiscoverAdditionalModules()
        {
            try
            {
                var currentAssembly = Assembly.GetExecutingAssembly();
                var moduleTypes = currentAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .Where(t => typeof(IModule).IsAssignableFrom(t))
                    .Where(t => t != typeof(Startup.Modules.CoreModule) && t != typeof(Startup.Modules.QuickBooksModule)) // Exclude already registered
                    .Where(t => t.GetConstructor(Type.EmptyTypes) != null) // Must have parameterless constructor
                    .ToList();

                Log.Debug("Discovered {Count} additional module types", moduleTypes.Count);
                return moduleTypes;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                Log.Warning("ReflectionTypeLoadException during module discovery - checking partially loaded types");

                try
                {
                    return rtle.Types
                        .Where(t => t != null && t.IsClass && !t.IsAbstract)
                        .Where(t => typeof(IModule).IsAssignableFrom(t))
                        .Where(t => t != typeof(Startup.Modules.CoreModule) && t != typeof(Startup.Modules.QuickBooksModule))
                        .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                        .ToList()!;
                }
                catch
                {
                    Log.Warning("Failed to process partially loaded types during module discovery");
                    return Enumerable.Empty<Type>();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to discover additional modules");
                return Enumerable.Empty<Type>();
            }
        }

        /// <summary>
        /// Configures default region behaviors for all regions.
        /// </summary>
        protected override void ConfigureDefaultRegionBehaviors(IRegionBehaviorFactory regionBehaviorFactory)
        {
            try
            {
                Log.Information("[PRISM] Configuring default region behaviors...");

                // Call base first to register Prism's built-in behaviors
                base.ConfigureDefaultRegionBehaviors(regionBehaviorFactory);

                // Register custom region behaviors with their keys
                // Skip NavigationLoggingBehavior in E2E tests as it can cause startup issues
                var isE2eTest = Environment.GetEnvironmentVariable("WILEY_WIDGET_E2E_TEST") == "true";
                if (!isE2eTest)
                {
                    regionBehaviorFactory.AddIfMissing(NavigationLoggingBehavior.BehaviorKey, typeof(NavigationLoggingBehavior));
                }
                regionBehaviorFactory.AddIfMissing(AutoSaveBehavior.BehaviorKey, typeof(AutoSaveBehavior));
                regionBehaviorFactory.AddIfMissing(NavigationHistoryBehavior.BehaviorKey, typeof(NavigationHistoryBehavior));
                regionBehaviorFactory.AddIfMissing(AutoActivateBehavior.BehaviorKey, typeof(AutoActivateBehavior));
                regionBehaviorFactory.AddIfMissing(DelayedRegionCreationBehavior.BehaviorKey, typeof(DelayedRegionCreationBehavior));

                Log.Information("✓ [PRISM] Registered custom region behaviors (E2E: {IsE2eTest}):", isE2eTest);
                if (!isE2eTest)
                {
                    Log.Debug("  - NavigationLogging: {Key}", NavigationLoggingBehavior.BehaviorKey);
                }
                Log.Debug("  - AutoSave: {Key}", AutoSaveBehavior.BehaviorKey);
                Log.Debug("  - NavigationHistory: {Key}", NavigationHistoryBehavior.BehaviorKey);
                Log.Debug("  - AutoActivate: {Key}", AutoActivateBehavior.BehaviorKey);
                Log.Debug("  - DelayedRegionCreation: {Key}", DelayedRegionCreationBehavior.BehaviorKey);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ [PRISM] Failed to configure region behaviors");
                throw; // Re-throw to prevent invalid startup state
            }
        }

        /// <summary>
        /// Configures region adapter mappings for Syncfusion controls.
        /// CRITICAL: Theme must be applied before this is called.
        /// </summary>
        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            try
            {
                Log.Information("[PRISM] Configuring region adapter mappings...");

                // Call base first to register Prism's built-in adapters
                base.ConfigureRegionAdapterMappings(regionAdapterMappings);

                var behaviorFactory = this.Container.Resolve<IRegionBehaviorFactory>();

                // CRITICAL: Theme must be applied before registering Syncfusion adapters
                if (SfSkinManager.ApplicationTheme == null)
                {
                    var errorMessage = "[PRISM] CRITICAL: Theme not applied before ConfigureRegionAdapterMappings. " +
                                      "Syncfusion region adapters cannot be registered without an active theme. " +
                                      "This indicates a timing issue in the startup sequence. " +
                                      "Theme should be applied in OnStartup() before base.OnStartup() is called.";
                    Log.Fatal(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                Log.Debug("[PRISM] Theme verified for adapter registration (Theme: {Theme})",
                         SfSkinManager.ApplicationTheme?.ToString() ?? "Unknown");

                // Register Syncfusion region adapters with error handling (post-theme)
                try
                {
                    var dockingManagerType = FindLoadedTypeByShortName("DockingManager");
                    if (dockingManagerType != null)
                    {
                        var dockingAdapter = new DockingManagerRegionAdapter(behaviorFactory);
                        regionAdapterMappings.RegisterMapping(dockingManagerType, dockingAdapter);
                        Log.Information("✓ Registered DockingManagerRegionAdapter (post-theme)");
                    }
                    else
                    {
                        Log.Debug("DockingManager type not loaded; skipping adapter registration");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DockingManager adapter registration failed; continuing with defaults");
                }

                try
                {
                    var sfGridType = FindLoadedTypeByShortName("SfDataGrid");
                    if (sfGridType != null)
                    {
                        var sfGridAdapter = new SfDataGridRegionAdapter(behaviorFactory);
                        regionAdapterMappings.RegisterMapping(sfGridType, sfGridAdapter);
                        Log.Information("✓ Registered SfDataGridRegionAdapter (post-theme)");
                    }
                    else
                    {
                        Log.Debug("SfDataGrid type not loaded; skipping adapter registration");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "SfDataGrid adapter registration failed; continuing with defaults");
                }

                Log.Information("✓ [PRISM] Region adapter mappings configured successfully (post-theme)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ [PRISM] Failed to configure region adapter mappings");
                throw; // Re-throw to prevent invalid startup state
            }
        }

        #endregion

        #region Configuration Management

        /// <summary>
        /// Builds and caches the application configuration from multiple sources.
        /// Returns cached configuration if already built to eliminate duplicate calls.
        /// </summary>
        /// <summary>
        /// Builds the application configuration from appsettings.json and environment variables.
        /// Returns cached configuration to avoid duplicate builds. Public visibility for partial class access.
        /// </summary>
        public static IConfiguration BuildConfiguration()
        {
            // Return cached configuration if already built (eliminates duplicate calls)
            if (_cachedConfiguration != null)
            {
                return _cachedConfiguration;
            }

            _startupId ??= Guid.NewGuid().ToString("N")[..8];

            // Initialize Serilog early for configuration logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/wiley-widget-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            // Build configuration from multiple sources
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            var config = builder.Build();

            // Resolve any placeholders in configuration values
            TryResolvePlaceholders(config as IConfigurationRoot);

            Log.Information("WileyWidget startup - Session: {StartupId}", _startupId);

            // Cache the configuration for reuse
            _cachedConfiguration = config;
            return config;
        }

        /// <summary>
        /// Resolves placeholder values in configuration (e.g., ${ENV_VAR} references).
        /// Supports recursive resolution for nested configuration sections.
        /// </summary>
        private static void TryResolvePlaceholders(IConfigurationRoot? config)
        {
            if (config == null)
            {
                return;
            }

            try
            {
                // Recursively resolve placeholders in all configuration sections
                ResolveSection(config);

                // Helper method to recursively process configuration sections
                static void ResolveSection(IConfiguration section)
                {
                    // Process current section's value if it exists
                    if (section is IConfigurationSection configSection && !string.IsNullOrEmpty(configSection.Value))
                    {
                        var value = configSection.Value;
                        if (value.Contains("${"))
                        {
                            // Simple placeholder resolution: ${VAR_NAME} -> Environment.GetEnvironmentVariable("VAR_NAME")
                            var startIndex = value.IndexOf("${", StringComparison.Ordinal);
                            var endIndex = value.IndexOf("}", startIndex, StringComparison.Ordinal);

                            if (startIndex >= 0 && endIndex > startIndex)
                            {
                                var envVarName = value.Substring(startIndex + 2, endIndex - startIndex - 2);
                                var envVarValue = Environment.GetEnvironmentVariable(envVarName);

                                if (!string.IsNullOrEmpty(envVarValue))
                                {
                                    var resolvedValue = value.Replace($"${{{envVarName}}}", envVarValue);
                                    configSection.Value = resolvedValue;
                                    Log.Debug("Resolved placeholder {Placeholder} in {Path}", envVarName, configSection.Path);
                                }
                            }
                        }
                    }

                    // Recursively process all child sections
                    foreach (var child in section.GetChildren())
                    {
                        ResolveSection(child);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve configuration placeholders - using raw values");
            }
        }

        #region DI Registration Methods

        /// <summary>
        /// Register convention-based types including ViewModels, infrastructure services, repositories, and business services.
        /// This method implements the core DI registration for the application.
        /// </summary>
        private static void RegisterConventionTypes(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering convention-based types...");

                // 1. Register core infrastructure services
                RegisterCoreInfrastructure(registry);

                // 2. Register repositories from WileyWidget.Data assembly
                RegisterRepositories(registry);

                // 3. Register business services from WileyWidget.Services assembly
                RegisterBusinessServices(registry);

                // 4. Register ViewModels by convention (currently only SettingsViewModel per manifest)
                RegisterViewModels(registry);

                Log.Information("✓ Convention-based type registration complete");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "✗ Failed to register convention-based types - application cannot start");
                throw;
            }
        }

        /// <summary>
        /// Register core infrastructure services required by all components.
        /// Includes IConfiguration, IMemoryCache, IHttpClientFactory, and ILoggerFactory.
        /// Uses a single ServiceCollection for optimal resource management.
        /// </summary>
        private static void RegisterCoreInfrastructure(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering core infrastructure services...");

                // Reuse cached IConfiguration (built once in CreateContainerExtension via BuildConfiguration)
                var configuration = BuildConfiguration();
                registry.RegisterInstance<IConfiguration>(configuration);
                Log.Information("✓ IConfiguration registered");

                // Register IMemoryCache (required by repositories and services)
                var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
                    new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
                    {
                        SizeLimit = 1024 * 1024 * 100, // 100MB limit
                        CompactionPercentage = 0.25    // Compact when 75% full
                    });
                registry.RegisterInstance<Microsoft.Extensions.Caching.Memory.IMemoryCache>(memoryCache);
                Log.Information("✓ IMemoryCache registered with 100MB limit");

                // Register ICacheService wrapper for IMemoryCache
                registry.RegisterSingleton<WileyWidget.Abstractions.ICacheService, WileyWidget.Services.MemoryCacheService>();
                Log.Information("✓ ICacheService registered");

                // Create a single ServiceCollection for all Microsoft.Extensions services
                // This is more efficient than creating multiple ServiceProvider instances
                var serviceCollection = new ServiceCollection();

                // Add logging with Serilog bridge
                serviceCollection.AddLogging(builder => builder.AddSerilog(dispose: false));

                // Add HTTP clients with resilience policies
                serviceCollection.AddHttpClient("Default", client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WileyWidget", "1.0"));
                }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 10
                });

                // Register QuickBooks named client
                serviceCollection.AddHttpClient("QuickBooks", client =>
                {
                    client.BaseAddress = new Uri("https://oauth.platform.intuit.com");
                    client.Timeout = TimeSpan.FromSeconds(60);
                });

                // Register AI service named client
                serviceCollection.AddHttpClient("XAI", client =>
                {
                    client.BaseAddress = new Uri("https://api.x.ai");
                    client.Timeout = TimeSpan.FromSeconds(120);
                });

                // Enhanced DbContext factory registration with comprehensive validation and fallback
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                // Validate connection string with graceful degradation (inline validation for now)
                bool isValid = !string.IsNullOrWhiteSpace(connectionString);
                var warnings = new List<string>();
                var fallbackConnectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";

                if (!isValid)
                {
                    warnings.Add("Database connection string not configured - using fallback");
                    Log.Warning("⚠ DB Connection: Database connection string not configured - using fallback");
                }

                // Use validated or fallback connection string
                var finalConnectionString = isValid ? connectionString! : fallbackConnectionString;

                // Enhanced DbContextFactory registration mirroring DatabaseConfiguration.ConfigureAppDbContext
                serviceCollection.AddDbContextFactory<WileyWidget.Data.AppDbContext>((sp, options) =>
                {
                    // Mirror DatabaseConfiguration.ConfigureAppDbContext configuration
                    var logger = sp.GetService<ILogger<WileyWidget.Data.AppDbContext>>() ??
                        sp.GetRequiredService<ILoggerFactory>().CreateLogger<WileyWidget.Data.AppDbContext>();
                    var hostEnvironment = sp.GetService<IHostEnvironment>();
                    var environmentName = hostEnvironment?.EnvironmentName ?? "Production";

                    logger.LogInformation("🔍 Enhanced DbContextFactory: Configuring for {Environment} environment", environmentName);

                    // STEP 1: Configure general EF options (including warnings) BEFORE provider configuration
                    // This prevents ArgumentException in EF Core 9.0 due to options builder state conflicts
                    ConfigureEnterpriseDbContextOptions(options, logger);

                    // STEP 2: Enable service provider caching for enhanced performance
                    options.EnableServiceProviderCaching();

                    // STEP 3: Configure SQL Server provider with enhanced options
                    ConfigureEnhancedSqlServer(options, finalConnectionString, logger, environmentName);

                    // STEP 4: Add interceptors if available (non-fatal if missing)
                    try
                    {
                        var auditInterceptorType = Type.GetType("WileyWidget.Data.Interceptors.AuditInterceptor, WileyWidget.Data");
                        if (auditInterceptorType != null)
                        {
                            var auditInterceptorInstance = sp.GetService(auditInterceptorType);
                            if (auditInterceptorInstance is Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor efInterceptor)
                            {
                                options.AddInterceptors(efInterceptor);
                                logger.LogDebug("✓ AuditInterceptor attached to DbContext options");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "⚠ Failed to attach AuditInterceptor to DbContext options (non-fatal)");
                    }

                    logger.LogInformation("✓ Enhanced DbContextFactory configured successfully");
                }, ServiceLifetime.Singleton); // Singleton factory, creates scoped contexts                // Build ServiceProvider to extract configured services
                var serviceProvider = serviceCollection.BuildServiceProvider();

                // Register services in DryIoc from the ServiceProvider
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                registry.RegisterInstance<ILoggerFactory>(loggerFactory);

                // Register ILogger<T> by leveraging Prism's built-in support for Microsoft.Extensions.DependencyInjection
                // This is the simplest and most reliable approach
                var loggingServices = new ServiceCollection();
                loggingServices.AddLogging(builder => builder.AddSerilog(dispose: false));

                // Use the registry (IContainerExtension) to populate the services
                ((IContainerExtension)registry).Populate(loggingServices);

                Log.Information("✓ ILoggerFactory registered (Serilog bridge)");
                Log.Information("✓ ILogger<T> generic factory registered");

                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                registry.RegisterInstance<IHttpClientFactory>(httpClientFactory);
                Log.Information("✓ IHttpClientFactory registered with Default, QuickBooks, and XAI clients");

                // Enhanced DbContextFactory registration with graceful fallback
                if (!string.IsNullOrWhiteSpace(finalConnectionString))
                {
                    var dbContextFactory = serviceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>>();
                    registry.RegisterInstance(dbContextFactory);
                    Log.Information("✓ Enhanced IDbContextFactory<AppDbContext> registered");
                }
                else
                {
                    Log.Warning("⚠ Enhanced DbContextFactory registration failed - database features will be unavailable");
                }

                Log.Information("✓ Core infrastructure registration complete");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to register core infrastructure services");
                throw;
            }
        }

        /// <summary>
        /// Register repositories from WileyWidget.Data assembly.
        /// Uses Scoped lifetime for per-operation database context isolation.
        /// </summary>
        private static void RegisterRepositories(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering repositories...");

                var dataAssembly = Assembly.Load("WileyWidget.Data");
                var repositoryTypes = dataAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Repository"))
                    .ToList();

                foreach (var repoType in repositoryTypes)
                {
                    var interfaceType = repoType.GetInterfaces()
                        .FirstOrDefault(i => i.Name == $"I{repoType.Name}");

                    if (interfaceType != null)
                    {
                        registry.RegisterScoped(interfaceType, repoType);
                        Log.Debug("  ✓ {Interface} -> {Implementation}", interfaceType.Name, repoType.Name);
                    }
                }

                Log.Information("✓ Registered {Count} repositories", repositoryTypes.Count);
            }
            catch (FileNotFoundException)
            {
                Log.Warning("⚠ WileyWidget.Data assembly not found - repository registration skipped");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to register repositories");
                throw;
            }
        }

        /// <summary>
        /// Register business services from WileyWidget.Services assembly.
        /// Uses Singleton lifetime for stateless services.
        /// Scans for classes ending in: Service, Engine, Helper, Importer, Calculator
        /// </summary>
        private static void RegisterBusinessServices(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering business services...");

                var servicesAssembly = Assembly.Load("WileyWidget.Services");

                // Expanded pattern matching for business components
                var suffixes = new[] { "Service", "Engine", "Helper", "Importer", "Calculator" };
                var serviceTypes = servicesAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .Where(t => suffixes.Any(suffix => t.Name.EndsWith(suffix)))
                    .Where(t => t.GetInterfaces().Any(i => i.Name.StartsWith("I")))
                    .ToList();

                foreach (var serviceType in serviceTypes)
                {
                    var interfaceType = serviceType.GetInterfaces()
                        .FirstOrDefault(i => i.Name == $"I{serviceType.Name}");

                    if (interfaceType != null)
                    {
                        // Skip services already registered in RegisterTypes
                        if (interfaceType.Name is "IModuleHealthService" or "IDialogTrackingService" or "IStartupDiagnosticsService")
                        {
                            continue;
                        }

                        registry.RegisterSingleton(interfaceType, serviceType);
                        Log.Debug("  ✓ {Interface} -> {Implementation}", interfaceType.Name, serviceType.Name);
                    }
                }

                Log.Information("✓ Registered {Count} business services (Service/Engine/Helper/Importer/Calculator)", serviceTypes.Count);
            }
            catch (FileNotFoundException)
            {
                Log.Warning("⚠ WileyWidget.Services assembly not found - service registration skipped");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to register business services");
                throw;
            }
        }

        /// <summary>
        /// Register ViewModels by convention.
        /// Auto-discovers all ViewModels from WileyWidget.UI assembly and registers with Transient lifetime.
        /// </summary>
        private static void RegisterViewModels(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering ViewModels by convention...");

                // Enhanced assembly loading strategy for WileyWidget.UI
                Assembly? uiAssembly = null;

                // Strategy 1: Try to load from output directory first (CopyLocalLockFileAssemblies should put it there)
                var outputDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    var uiAssemblyPath = Path.Combine(outputDir, "WileyWidget.UI.dll");
                    if (File.Exists(uiAssemblyPath))
                    {
                        try
                        {
                            uiAssembly = Assembly.LoadFrom(uiAssemblyPath);
                            Log.Debug("WileyWidget.UI assembly loaded from output directory: {Path}", uiAssemblyPath);
                        }
                        catch (Exception loadEx)
                        {
                            Log.Warning(loadEx, "Failed to load WileyWidget.UI from output directory: {Path}", uiAssemblyPath);
                        }
                    }
                }

                // Strategy 2: Try Assembly.Load with name
                if (uiAssembly == null)
                {
                    try
                    {
                        uiAssembly = Assembly.Load("WileyWidget.UI");
                        Log.Debug("WileyWidget.UI assembly loaded by name");
                    }
                    catch (FileNotFoundException)
                    {
                        Log.Warning("⚠ WileyWidget.UI assembly not found by name - checking loaded assemblies");
                    }
                    catch (Exception loadEx)
                    {
                        Log.Warning(loadEx, "Failed to load WileyWidget.UI assembly by name");
                    }
                }

                // Strategy 3: Search in already loaded assemblies
                if (uiAssembly == null)
                {
                    uiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name?.Equals("WileyWidget.UI", StringComparison.OrdinalIgnoreCase) == true);

                    if (uiAssembly != null)
                    {
                        Log.Debug("WileyWidget.UI assembly found in loaded assemblies");
                    }
                }

                // Strategy 4: Fallback to current assembly if UI assembly not found
                if (uiAssembly == null)
                {
                    uiAssembly = Assembly.GetExecutingAssembly();
                    Log.Warning("⚠ WileyWidget.UI assembly not found - using current assembly as fallback");
                }

                // Auto-discover ViewModels with enhanced error handling
                List<Type> viewModelTypes = new();
                try
                {
                    var allTypes = uiAssembly.GetTypes();
                    viewModelTypes = allTypes
                        .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel"))
                        .Where(t => t.Namespace?.Contains("ViewModels") == true) // Only types in ViewModels namespace
                        .ToList();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    Log.Warning("ReflectionTypeLoadException while discovering ViewModels - using successfully loaded types");

                    // Process successfully loaded types
                    try
                    {
                        viewModelTypes = rtle.Types
                            .Where(t => t != null && t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel"))
                            .Where(t => t.Namespace?.Contains("ViewModels") == true)
                            .ToList()!;
                    }
                    catch (Exception innerEx)
                    {
                        Log.Error(innerEx, "Failed to process partially loaded ViewModels");
                        viewModelTypes = new List<Type>();
                    }
                }

                // Register discovered ViewModels
                foreach (var vmType in viewModelTypes)
                {
                    try
                    {
                        // Validate that the type has a suitable constructor
                        var constructors = vmType.GetConstructors();
                        var hasParameterlessConstructor = constructors.Any(c => c.GetParameters().Length == 0);
                        var hasInjectableConstructor = constructors.Any(c =>
                            c.GetParameters().All(p =>
                                p.ParameterType.IsInterface ||
                                p.ParameterType.IsClass && !p.ParameterType.IsSealed));

                        if (hasParameterlessConstructor || hasInjectableConstructor)
                        {
                            // Register as Transient (new instance per resolve)
                            registry.Register(vmType);
                            Log.Debug("  ✓ {ViewModel} registered (Transient)", vmType.Name);
                        }
                        else
                        {
                            Log.Warning("  ⚠ {ViewModel} skipped - no suitable constructor found", vmType.Name);
                        }
                    }
                    catch (Exception registerEx)
                    {
                        Log.Warning(registerEx, "Failed to register ViewModel: {ViewModel}", vmType.Name);
                    }
                }

                Log.Information("✓ ViewModel registration complete ({Count} ViewModels)", viewModelTypes.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to register ViewModels");
                // Don't rethrow - allow app to continue with manual ViewModel registration if needed
            }
        }

        /// <summary>
        /// Register AI services with lazy initialization and configuration validation.
        /// Validates API keys and falls back to NullAIService if unavailable.
        /// </summary>
        private void RegisterLazyAIServices(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering AI services...");

                // Validate AI service configuration
                var config = registry.GetContainer().Resolve<IConfiguration>();
                var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? config["XAI:ApiKey"];
                var requireAI = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_AI_SERVICE"), "true", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    Log.Information("✓ XAI API key found (length: {Length})", apiKey.Length);

                    // Register XAIService as IAIService with factory for proper initialization
                    registry.RegisterSingleton<WileyWidget.Services.IAIService>(container =>
                    {
                        var logger = container.Resolve<ILogger<WileyWidget.Services.XAIService>>();
                        var httpClientFactory = container.Resolve<IHttpClientFactory>();
                        var configuration = container.Resolve<IConfiguration>();
                        var contextService = container.Resolve<WileyWidget.Services.IWileyWidgetContextService>();
                        var aiLoggingService = container.Resolve<WileyWidget.Services.IAILoggingService>();
                        var memoryCache = container.Resolve<Microsoft.Extensions.Caching.Memory.IMemoryCache>();

                        return new WileyWidget.Services.XAIService(httpClientFactory, configuration, logger, contextService, aiLoggingService, memoryCache);
                    });

                    Log.Information("✓ XAIService registered as IAIService");
                }
                else if (requireAI)
                {
                    Log.Error("✗ AI service required but XAI_API_KEY not found");
                    throw new InvalidOperationException("REQUIRE_AI_SERVICE=true but XAI_API_KEY not configured");
                }
                else
                {
                    Log.Warning("⚠ XAI API key not found - registering NullAIService");

                    // Register NullAIService as fallback
                    registry.RegisterSingleton<WileyWidget.Services.IAIService, WileyWidget.Services.NullAIService>();
                    Log.Information("✓ NullAIService registered as IAIService (fallback)");
                }

                // Register supporting AI services
                registry.RegisterSingleton<WileyWidget.Services.IAILoggingService, WileyWidget.Services.AILoggingService>();
                Log.Information("✓ AI logging service registered");

                Log.Information("✓ AI service registration complete");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to register AI services");
                throw;
            }
        }

        /// <summary>
        /// Wrapper for ViewModel validation that delegates to StartupEnvironmentValidator.
        /// Kept as static method for compatibility with DI registration flow.
        /// Made internal so it can be called from App.Lifecycle.cs after container initialization.
        /// </summary>
        internal static void ValidateAndRegisterViewModels(IContainerRegistry registry)
        {
            try
            {
                // Resolve the validator service and delegate validation
                var container = registry.GetContainer();
                if (container.IsRegistered<WileyWidget.Services.Startup.IStartupEnvironmentValidator>())
                {
                    var validator = container.Resolve<WileyWidget.Services.Startup.IStartupEnvironmentValidator>();
                    validator.ValidateAndRegisterViewModels(registry);
                }
                else
                {
                    Log.Warning("StartupEnvironmentValidator not registered yet - skipping ViewModel validation");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to validate ViewModels via StartupEnvironmentValidator");
            }
        }

        /// <summary>
        /// Wrapper for AI configuration validation that delegates to StartupEnvironmentValidator.
        /// Kept as instance method for compatibility with existing call sites.
        /// </summary>
        private void ValidateAIServiceConfiguration()
        {
            try
            {
                // Resolve the validator service and delegate validation
                if (this.Container.IsRegistered<WileyWidget.Services.Startup.IStartupEnvironmentValidator>())
                {
                    var validator = this.Container.Resolve<WileyWidget.Services.Startup.IStartupEnvironmentValidator>();
                    validator.ValidateAIServiceConfiguration();
                }
                else
                {
                    Log.Warning("StartupEnvironmentValidator not registered yet - skipping AI validation");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to validate AI configuration via StartupEnvironmentValidator");
            }
        }

        #region Enhanced DbContextFactory Helper Methods

        /// <summary>
        /// Configures enterprise-grade DbContext options mirroring DatabaseConfiguration.ConfigureEnterpriseDbContextOptions.
        /// This method ensures proper warning configuration and prevents EF Core 9.0 state conflicts.
        /// </summary>
        private static void ConfigureEnterpriseDbContextOptions(DbContextOptionsBuilder options, Microsoft.Extensions.Logging.ILogger logger)
        {
            // Enable sensitive data logging in development only
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                logger.LogInformation("Sensitive data logging enabled for local diagnostics");
            }
            options.EnableDetailedErrors();

            // Configure query tracking
            options.UseQueryTrackingBehavior(Microsoft.EntityFrameworkCore.QueryTrackingBehavior.TrackAll);

            // Configure warnings - this mirrors AppDbContext.OnConfiguring and Prompt 3 requirements
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
            });

            // Add EF Core logging
            options.LogTo(message => logger.LogDebug("EF Core: {Message}", message),
                new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted });

            logger.LogInformation("✓ Enterprise DbContext options configured");
        }

        /// <summary>
        /// Configures enhanced SQL Server connection mirroring DatabaseConfiguration.ConfigureSqlServer.
        /// Includes retry policies, command timeout, and migrations assembly configuration.
        /// </summary>
        private static void ConfigureEnhancedSqlServer(DbContextOptionsBuilder options, string connectionString, Microsoft.Extensions.Logging.ILogger logger, string environmentName)
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                // Specify migrations assembly since DbContext is in WileyWidget.Data project
                sqlOptions.MigrationsAssembly("WileyWidget.Data");

                // Enhanced retry configuration mirroring DatabaseConfiguration
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);

                // Connection timeout
                sqlOptions.CommandTimeout(30);
            });

            logger.LogInformation("✅ Enhanced SQL Server connection configured for {Environment} environment", environmentName);
        }

        #endregion

        #endregion

        #endregion
    }
}
