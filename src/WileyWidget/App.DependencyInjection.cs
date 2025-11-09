// App.DependencyInjection.cs - Dependency Injection & Configuration Partial Class
// Contains: DI container setup, Prism configuration, module catalog, region adapters
// Part of App.xaml.cs partial class split for maintainability

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using DryIoc;
using Microsoft.Extensions.Configuration;
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

            // Convention-based registrations (implemented in App.DependencyInjection.Convention.cs)
            RegisterConventionTypes(containerExtension);

            // Lazy AI services (implemented in App.DependencyInjection.Convention.cs)
            RegisterLazyAIServices(containerExtension);

            // ViewModel validation/auto-reg (implemented in App.DependencyInjection.Convention.cs)
            ValidateAndRegisterViewModels(containerExtension);

            // Load config-driven module map/order
            var config = BuildConfiguration();
            ModuleRegionMap = config.GetSection("Modules:Regions").Get<System.Collections.Generic.Dictionary<string, string[]>>()
                ?? new System.Collections.Generic.Dictionary<string, string[]>();
            ModuleOrder = config.GetSection("Modules:Order").Get<string[]>()
                ?? new[] { "CoreModule" };

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
            containerRegistry.RegisterSingleton<Services.TelemetryStartupService>();
            containerRegistry.RegisterSingleton<Abstractions.IModuleHealthService, Services.ModuleHealthService>();

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
            containerRegistry.RegisterSingleton<Abstractions.IDialogTrackingService, Services.DialogTrackingService>();

            // Register enhanced startup diagnostics service for 4-phase startup
            containerRegistry.RegisterSingleton<Abstractions.IStartupDiagnosticsService, Startup.StartupDiagnosticsService>();

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
            containerRegistry.RegisterSingleton<Abstractions.IPrismErrorHandler, Services.PrismErrorHandler>();

            // Register enterprise resource loader for Polly-based resilient resource loading
            containerRegistry.RegisterSingleton<Abstractions.IResourceLoader, Startup.EnterpriseResourceLoader>();

            Log.Information("✓ Critical services registered");

            // CRITICAL: Call convention-based registration helpers
            // These methods are still in App.xaml.cs and contain bulk of service registrations
            RegisterConventionTypes(containerRegistry);
            RegisterCoreInfrastructure(containerRegistry);
            RegisterRepositories(containerRegistry);
            RegisterBusinessServices(containerRegistry);
            RegisterViewModels(containerRegistry);
            RegisterLazyAIServices(containerRegistry);
            ValidateAndRegisterViewModels(containerRegistry);

            Log.Information("✓ All convention-based services registered");
        }

        /// <summary>
        /// Configures the module catalog with application modules.
        /// </summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            try
            {
                Log.Information("[PRISM] Configuring module catalog...");

                // Register essential modules only (Phase 0: Dead modules deleted)
                moduleCatalog.AddModule<Startup.Modules.CoreModule>();
                moduleCatalog.AddModule<Startup.Modules.QuickBooksModule>();

                Log.Information("✓ [PRISM] Module catalog configured with CoreModule and QuickBooksModule");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ [PRISM] Failed to configure module catalog");
                throw; // Re-throw to prevent invalid startup state
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
        /// </summary>
        private static void TryResolvePlaceholders(IConfigurationRoot? config)
        {
            if (config == null)
            {
                return;
            }

            try
            {
                // Placeholder resolution logic
                // Example: Replace ${ENV_VAR} with actual environment variable values
                foreach (var section in config.GetChildren())
                {
                    var value = section.Value;
                    if (!string.IsNullOrEmpty(value) && value.Contains("${"))
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
                                section.Value = resolvedValue;
                                Log.Debug("Resolved placeholder {Placeholder} in configuration", envVarName);
                            }
                        }
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

                // Register ILoggerFactory bridging Serilog to Microsoft.Extensions.Logging
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddLogging(builder => builder.AddSerilog(dispose: false));
                var serviceProvider = serviceCollection.BuildServiceProvider();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                registry.RegisterInstance<ILoggerFactory>(loggerFactory);
                Log.Information("✓ ILoggerFactory registered (Serilog bridge)");

                // Register IHttpClientFactory with resilience policies
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

                var httpServiceProvider = serviceCollection.BuildServiceProvider();
                var httpClientFactory = httpServiceProvider.GetRequiredService<IHttpClientFactory>();
                registry.RegisterInstance<IHttpClientFactory>(httpClientFactory);
                Log.Information("✓ IHttpClientFactory registered with Default, QuickBooks, and XAI clients");

                // Register IDbContextFactory<AppDbContext> if connection string exists
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    var dbServiceCollection = new ServiceCollection();
                    dbServiceCollection.AddDbContextFactory<WileyWidget.Data.AppDbContext>(options =>
                    {
                        options.UseSqlServer(connectionString, sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                            sqlOptions.CommandTimeout(30);
                        });
                    });
                    var dbServiceProvider = dbServiceCollection.BuildServiceProvider();
                    var dbContextFactory = dbServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>>();
                    registry.RegisterInstance(dbContextFactory);
                    Log.Information("✓ IDbContextFactory<AppDbContext> registered");
                }
                else
                {
                    Log.Warning("⚠ No DefaultConnection found - database features will be unavailable");
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
        /// </summary>
        private static void RegisterBusinessServices(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering business services...");

                var servicesAssembly = Assembly.Load("WileyWidget.Services");
                var serviceTypes = servicesAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service"))
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

                Log.Information("✓ Registered {Count} business services", serviceTypes.Count);
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
        /// Currently only SettingsViewModel is active per manifest.
        /// </summary>
        private static void RegisterViewModels(IContainerRegistry registry)
        {
            try
            {
                Log.Information("Registering ViewModels by convention...");

                // Register SettingsViewModel (only active ViewModel per manifest)
                var settingsVMType = Type.GetType("WileyWidget.ViewModels.Main.SettingsViewModel, WileyWidget");
                if (settingsVMType != null)
                {
                    registry.Register(settingsVMType);
                    Log.Information("  ✓ SettingsViewModel registered");
                }
                else
                {
                    Log.Warning("⚠ SettingsViewModel type not found");
                }

                Log.Information("✓ ViewModel registration complete (1 ViewModel)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to register ViewModels");
                throw;
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
        /// </summary>
        private static void ValidateAndRegisterViewModels(IContainerRegistry registry)
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

        #endregion

        #endregion
    }
}
