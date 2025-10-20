using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Reflection;
using Prism.Unity;
using Prism.Modularity;
using Prism.Container.Unity;
using Prism.Ioc;
using Unity;
using Unity.Resolution;
using Syncfusion.SfSkinManager;
using Syncfusion.Licensing;
using Bold.Licensing;
using WileyWidget.Views;
using WileyWidget.Startup.Modules;
using WileyWidget.Services;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using DotNetEnv;
using WileyWidget.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using System.Text.RegularExpressions;
using Prism.Events;
using WileyWidget.ViewModels.Messages;
// using Microsoft.ApplicationInsights;
// using Microsoft.ApplicationInsights.Extensibility;
using Serilog.Events;

namespace WileyWidget
{
    public class App : PrismApplication
    {
        // Static mapping of expected regions for each module for maintainability and reuse
        private static readonly Dictionary<string, string[]> moduleRegionMap = new Dictionary<string, string[]>
        {
            ["CoreModule"] = new[] { "SettingsRegion" }, // Core module handles settings
            ["DashboardModule"] = new[] { "MainRegion" },
            ["EnterpriseModule"] = new[] { "EnterpriseRegion" },
            ["BudgetModule"] = new[] { "BudgetRegion", "AnalyticsRegion" },
            ["MunicipalAccountModule"] = new[] { "MunicipalAccountRegion" },
            ["UtilityCustomerModule"] = new[] { "UtilityCustomerRegion" },
            ["ReportsModule"] = new[] { "ReportsRegion" },
            ["AIAssistModule"] = new[] { "AIAssistRegion" },
            ["PanelModule"] = new[] { "LeftPanelRegion", "RightPanelRegion", "BottomPanelRegion" },
            ["ToolsModule"] = new[] { "BottomPanelRegion" }
        };
        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
        private static readonly object StartupProgressSyncRoot = new();
        public static object? StartupProgress { get; private set; }
        public static DateTimeOffset? LastHealthReportUpdate { get; private set; }
    private bool _syncfusionLicenseRegistered;
    private bool _boldReportsLicenseRegistered;

        public static void UpdateLatestHealthReport(object report)
        {
            if (report == null)
            {
                Log.Warning("Module health report update skipped: report was null");
                return;
            }

            lock (StartupProgressSyncRoot)
            {
                StartupProgress = report;
                LastHealthReportUpdate = DateTimeOffset.UtcNow;
            }

            if (report is IEnumerable<ModuleHealthInfo> moduleHealthInfos)
            {
                int totalModules = 0;
                int healthyModules = 0;

                foreach (ModuleHealthInfo module in moduleHealthInfos)
                {
                    totalModules++;
                    if (module.Status == ModuleHealthStatus.Healthy)
                    {
                        healthyModules++;
                    }
                }

                Log.Debug("Module health report refreshed: {Healthy}/{Total} modules healthy", healthyModules, totalModules);
            }
            else if (report is ModuleHealthInfo singleModule)
            {
                Log.Debug("Module health report refreshed for {ModuleName}: {Status}", singleModule.ModuleName, singleModule.Status);
            }
            else
            {
                Log.Debug("Module health report refreshed ({ReportType})", report.GetType().FullName);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Enable WPF binding diagnostics
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new System.Diagnostics.DefaultTraceListener());
            // Set up global exception handling before anything else
            SetupGlobalExceptionHandling();

            // Apply Syncfusion theme globally as early as possible per official docs
            // Reference: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
            try
            {
                // Load application ResourceDictionaries safely to avoid XAML parse errors
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var candidatePaths = new[]
                    {
                        System.IO.Path.Combine(baseDir, "Themes", "Generic.xaml"),
                        System.IO.Path.Combine(baseDir, "Themes", "WileyTheme.xaml")
                    };

                    foreach (var p in candidatePaths)
                    {
                        try
                        {
                            if (!System.IO.File.Exists(p))
                            {
                                LogDebugEvent("Theme", $"Resource dictionary not found: {p}");
                                continue;
                            }

                            var xaml = System.IO.File.ReadAllText(p);
                            using var sr = new System.IO.StringReader(xaml);
                            using var xr = System.Xml.XmlReader.Create(sr);
                            var rdObj = System.Windows.Markup.XamlReader.Load(xr);
                            if (rdObj is ResourceDictionary rd && rd.Count > 0)
                            {
                                // Validate brushes/colors quickly to avoid late XAML parse exceptions
                                foreach (var key in rd.Keys.OfType<object>().ToList())
                                {
                                    try
                                    {
                                        var val = rd[key];
                                        if (val is System.Windows.Media.SolidColorBrush scb)
                                        {
                                            // access Color to force a parse
                                            var c = scb.Color;
                                        }
                                    }
                                    catch (Exception innerEx)
                                    {
                                        Log.Warning(innerEx, "Invalid resource '{Key}' in {Path} - replacing with Transparent.", key, p);
                                        rd[key] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
                                    }
                                }

                                Application.Current.Resources.MergedDictionaries.Add(rd);
                                LogDebugEvent("Theme", $"Merged resource dictionary: {p}");
                            }
                            else
                            {
                                LogDebugEvent("Theme", $"Parsed empty or non-dictionary resource: {p}");
                            }
                        }
                        catch (System.Xaml.XamlParseException xex)
                        {
                            Log.Warning(xex, "Failed to parse resource dictionary {Path} - skipping.", p);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Unexpected error loading resource dictionary {Path} - skipping.", p);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to safely load resource dictionaries; continuing without them.");
                }

                // CRITICAL: Set ApplyStylesOnApplication FIRST per Syncfusion documentation
                // This ensures all theme resources are merged into Application.Current.Resources
                SfSkinManager.ApplyStylesOnApplication = true;
                
                // Default to FluentDark for the entire application
#pragma warning disable CA2000 // Theme objects are managed by SfSkinManager
                SfSkinManager.ApplicationTheme = new Theme("FluentDark");
#pragma warning restore CA2000
                
                Log.Information("SfSkinManager initialized with FluentDark theme globally");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize SfSkinManager theme - falling back to default");
                // Fallback to FluentLight if FluentDark is unavailable for any reason
                try
                {
#pragma warning disable CA2000
                    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
#pragma warning restore CA2000
                }
                catch
                {
                    Log.Error("FluentLight fallback also failed - continuing without theme");
                }
            }

            ConfigureLogging();
            Trace.WriteLine("[App] ConfigureLogging completed");

            EnsureSyncfusionLicenseRegistered();
            EnsureBoldReportsLicenseRegistered();

            base.OnStartup(e);

            Log.Information("Application startup completed");
        }

        /// <summary>
        /// Sets up global exception handling for production readiness.
        /// Catches unhandled exceptions and logs them appropriately.
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            // Handle unhandled exceptions on the UI thread
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI exception occurred");
                e.Handled = true; // Prevent application crash
                Log.Warning("UI exception suppressed per policy; notifying via logs only.");
            };

            // Handle unhandled exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Exception? exception = e.ExceptionObject as Exception;
                Log.Fatal(exception, "Unhandled background thread exception occurred");
                // Application will terminate after this
            };

            // Handle unobserved task exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception occurred");
                e.SetObserved(); // Prevent it from crashing the finalizer thread
            };

            Log.Information("Global exception handling configured");
        }

        protected override Window CreateShell()
        {
            EnsureSyncfusionLicenseRegistered();
            EnsureBoldReportsLicenseRegistered();

            try
            {
                MainWindow shell = Container.Resolve<MainWindow>();
                Log.Information("MainWindow shell resolved successfully");
                return shell;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create application shell");
                throw new InvalidOperationException("Failed to create application shell. Check DI registrations and service availability.", ex);
            }
        }

        protected override void InitializeShell(Window shell)
        {
            // With SfSkinManager.ApplicationTheme set in OnStartup, all windows inherit the theme automatically
            Application.Current.MainWindow = shell;
            shell.Show();
        }

        private void EnsureSyncfusionLicenseRegistered(bool forceRefresh = false)
        {
            if (!forceRefresh && _syncfusionLicenseRegistered)
            {
                return;
            }

            try
            {
                IConfiguration configuration = _cachedConfiguration ??= BuildConfiguration();
                string? licenseKey = null;
                string licenseSource = "unknown";

                // Priority order: Machine env var > User env var > Configuration
                // Machine scope is most secure for production deployments
                
                // Check machine environment variable first (highest security)
                licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                {
                    licenseSource = "machine environment variable";
                }
                else
                {
                    // Check user/process environment variable
                    licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User)
                             ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Process);

                    if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                    {
                        licenseSource = "user environment variable";
                    }
                    else
                    {
                        // Check configuration as fallback
                        licenseKey = configuration["Syncfusion:LicenseKey"]
                                 ?? configuration["Syncfusion:License"];

                        if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                        {
                            licenseSource = "configuration";
                        }
                        else
                        {
                            // Check for license key in local secure storage (Azure Key Vault local cache or similar)
                            // This could be implemented as needed based on the specific key vault solution being used
                            Log.Debug("Checking for Syncfusion license in local key vault...");
                            // TODO: Implement specific key vault access if needed
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(licenseKey)
                    || licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                {
                    if (forceRefresh)
                    {
                        Log.Debug("Syncfusion license key not configured; skipping re-registration");
                    }
                    else
                    {
                        Log.Warning("Syncfusion license key not configured. Set Syncfusion:LicenseKey in appsettings.json, SYNCFUSION_LICENSE_KEY environment variable (user or machine scope), or ensure it's available in your local key vault to suppress runtime license dialogs.");
                    }
                    return;
                }

                string masked = licenseKey.Length > 8 ? string.Concat(licenseKey.AsSpan(0, 8), "...") : "(masked)";
                Log.Information("Registering Syncfusion license (length: {Length}, source: {Source})", licenseKey.Length, licenseSource);
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                _syncfusionLicenseRegistered = true;

                if (forceRefresh)
                {
                    Log.Debug("Syncfusion license re-registered from {Source} (masked: {Mask})", licenseSource, masked);
                }
                else
                {
                    Log.Information("Syncfusion license registered from {Source} (masked: {Mask})", licenseSource, masked);
                }
            }
            catch (Exception ex)
            {
                string message = forceRefresh
                    ? "Failed to re-register Syncfusion license during shutdown"
                    : "Failed to register Syncfusion license during startup - continuing without license registration (this may show license dialogs on first use)";
                Log.Warning(ex, message);
            }
        }

        private void EnsureBoldReportsLicenseRegistered(bool forceRefresh = false)
        {
            if (!forceRefresh && _boldReportsLicenseRegistered)
            {
                return;
            }

            try
            {
                IConfiguration configuration = _cachedConfiguration ??= BuildConfiguration();
                string? licenseKey = null;
                string licenseSource = "unknown";

                // Priority order: Machine env var > User env var > Configuration
                // Machine scope is most secure for production deployments

                // Check machine environment variable first (highest security)
                licenseKey = Environment.GetEnvironmentVariable("BOLDREPORTS_LICENSE_KEY", EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                {
                    licenseSource = "machine environment variable";
                }
                else
                {
                    // Check user/process environment variable
                    licenseKey = Environment.GetEnvironmentVariable("BOLDREPORTS_LICENSE_KEY", EnvironmentVariableTarget.User)
                             ?? Environment.GetEnvironmentVariable("BOLDREPORTS_LICENSE_KEY", EnvironmentVariableTarget.Process);

                    if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                    {
                        licenseSource = "user environment variable";
                    }
                    else
                    {
                        // Check configuration as fallback
                        licenseKey = configuration["BoldReports:LicenseKey"]
                                 ?? configuration["BoldReports:License"];

                        if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                        {
                            licenseSource = "configuration";
                        }
                        else
                        {
                            // Check for license key in local secure storage (Azure Key Vault local cache or similar)
                            // This could be implemented as needed based on the specific key vault solution being used
                            Log.Debug("Checking for Bold Reports license in local key vault...");
                            // TODO: Implement specific key vault access if needed
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(licenseKey)
                    || licenseKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                {
                    if (forceRefresh)
                    {
                        Log.Debug("Bold Reports license key not configured; skipping re-registration");
                    }
                    else
                    {
                        Log.Warning("Bold Reports license key not configured. Set BoldReports:LicenseKey in appsettings.json, BOLDREPORTS_LICENSE_KEY environment variable (user or machine scope), or ensure it's available in your local key vault to suppress runtime license dialogs.");
                    }
                    return;
                }

                string masked = licenseKey.Length > 8 ? string.Concat(licenseKey.AsSpan(0, 8), "...") : "(masked)";
                Log.Information("Registering Bold Reports license (length: {Length}, source: {Source})", licenseKey.Length, licenseSource);
                Bold.Licensing.BoldLicenseProvider.RegisterLicense(licenseKey);
                _boldReportsLicenseRegistered = true;

                if (forceRefresh)
                {
                    Log.Debug("Bold Reports license re-registered from {Source} (masked: {Mask})", licenseSource, masked);
                }
                else
                {
                    Log.Information("Bold Reports license registered from {Source} (masked: {Mask})", licenseSource, masked);
                }
            }
            catch (Exception ex)
            {
                string message = forceRefresh
                    ? "Failed to re-register Bold Reports license during shutdown"
                    : "Failed to register Bold Reports license during startup - continuing without license registration (this may show license dialogs on first use)";
                Log.Warning(ex, message);
            }
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Log.Information("=== Starting DI Container Registration ===");
            
            // Build configuration first
            IConfiguration configuration = BuildConfiguration();
            
            // Register configuration as singleton
            containerRegistry.RegisterInstance<IConfiguration>(configuration);
            Log.Information("✓ Registered IConfiguration as singleton instance");

            // Register Microsoft.Extensions.Logging integration with Serilog
#pragma warning disable CA2000
            SerilogLoggerFactory loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
            containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
            Log.Information("✓ Registered ILoggerFactory and ILogger<> with Serilog integration");

            // Register HttpClient infrastructure for AI services
            RegisterHttpClientServices(containerRegistry, configuration);

            // Register database services
            RegisterDatabaseServices(containerRegistry, configuration);

            // Register core infrastructure services
            containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
            containerRegistry.RegisterSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            // NOTE: ThemeManager removed - SfSkinManager handles all theming globally per Syncfusion documentation
            // Reference: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
            containerRegistry.RegisterSingleton<IDispatcherHelper>(provider => new DispatcherHelper());
            containerRegistry.RegisterSingleton<AppOptionsConfigurator>();

            IUnityContainer unityContainer = containerRegistry.GetContainer();
            EnableUnityDiagnostics(unityContainer);
            Log.Information("✓ Registered core infrastructure services (Syncfusion, Settings, Dispatcher)");

            // Initialize production secrets (synchronous for reliability)
            try
            {
                ISecretVaultService secretVault = Container.Resolve<ISecretVaultService>();
                secretVault.MigrateSecretsFromEnvironmentAsync().GetAwaiter().GetResult();
                Log.Information("✓ Environment secrets migrated to local vault");

                // Only populate production secrets if we're in production environment
                string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
                if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                {
                    secretVault.PopulateProductionSecretsAsync().GetAwaiter().GetResult();
                    Log.Information("✓ Production secrets initialized");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize production secrets");
            }
            
            // Register Microsoft.Extensions.Caching.Memory infrastructure
            MemoryCacheOptions memoryCacheOptions = new MemoryCacheOptions();
            string? configuredSizeLimit = configuration["Caching:MemoryCache:SizeLimit"];
            if (long.TryParse(configuredSizeLimit, out long sizeLimit) && sizeLimit > 0)
            {
                memoryCacheOptions.SizeLimit = sizeLimit;
            }

#pragma warning disable CA2000 // Unity will dispose the registered singleton when the container is disposed
            MemoryCache memoryCache = new MemoryCache(memoryCacheOptions);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<IMemoryCache>(memoryCache);
            Log.Information("✓ Registered IMemoryCache using Prism-managed MemoryCache instance");
            
            // Register configuration options infrastructure (bridging Microsoft.Extensions.Options into Unity)
            RegisterAppOptions(containerRegistry, configuration, unityContainer);

            // Register data repositories required during startup validation to prevent Unity resolution failures
            containerRegistry.Register<IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
            containerRegistry.Register<IBudgetRepository, WileyWidget.Data.BudgetRepository>();
            containerRegistry.Register<IAuditRepository, WileyWidget.Data.AuditRepository>();
            containerRegistry.Register<IMunicipalAccountRepository, WileyWidget.Data.MunicipalAccountRepository>();
            containerRegistry.Register<IUtilityCustomerRepository, WileyWidget.Data.UtilityCustomerRepository>();
            containerRegistry.Register<IDepartmentRepository, WileyWidget.Data.DepartmentRepository>();
            Log.Information("✓ Registered core data repositories for startup validation (Enterprise, Budget, Audit, MunicipalAccount, UtilityCustomer, Department)");

            // Ensure Prism-resolved ViewModels can obtain the UnitOfWork infrastructure
            containerRegistry.Register<IUnitOfWork, UnitOfWork>();
            Log.Information("✓ Registered IUnitOfWork infrastructure for Prism ViewModels");
            
            // Register IServiceScopeFactory for Unity-based scoped services (required by WhatIfScenarioEngine)
            containerRegistry.RegisterSingleton<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory, UnityServiceScopeFactory>();
            Log.Information("✓ Registered IServiceScopeFactory using Unity child container adapter");
            
            // Register business services
            containerRegistry.RegisterSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();
            containerRegistry.RegisterSingleton<FiscalYearSettings>();
            containerRegistry.RegisterSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();
            Log.Information("✓ Registered business services (WhatIfScenarioEngine, FiscalYearSettings, ChargeCalculator)");
            
            // Register AI Integration Services (Phase 1 - Production Ready)
            RegisterAIIntegrationServices(containerRegistry);
            
            // Register QuickBooks service
            containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();
            Log.Information("✓ Registered IQuickBooksService as singleton");
            
            // Register Excel services
            containerRegistry.RegisterSingleton<IExcelReaderService, ExcelReaderService>();
            Log.Information("✓ Registered IExcelReaderService as singleton");
            
            // Register report export service
            containerRegistry.RegisterSingleton<IReportExportService, ReportExportService>();
            Log.Information("✓ Registered IReportExportService as singleton");
            
            // Register Module Health Service
            containerRegistry.RegisterSingleton<IModuleHealthService, ModuleHealthService>();
            Log.Information("✓ Registered IModuleHealthService as singleton");
            
            // Register Prism DialogService
            containerRegistry.RegisterSingleton<Prism.Dialogs.IDialogService, Prism.Dialogs.DialogService>();
            Log.Information("✓ Registered Prism IDialogService as singleton");

            // Register Prism Dialogs
            containerRegistry.RegisterDialog<Views.ConfirmationDialogView, ViewModels.ConfirmationDialogViewModel>("ConfirmationDialog");
            containerRegistry.RegisterDialog<Views.NotificationDialogView, ViewModels.NotificationDialogViewModel>("NotificationDialog");
            containerRegistry.RegisterDialog<Views.WarningDialogView, ViewModels.WarningDialogViewModel>("WarningDialog");
            containerRegistry.RegisterDialog<Views.ErrorDialogView, ViewModels.ErrorDialogViewModel>("ErrorDialog");
            containerRegistry.RegisterDialog<Views.SettingsDialogView, ViewModels.SettingsDialogViewModel>("SettingsDialog");
            Log.Information("✓ Registered Prism Dialogs (Confirmation, Notification, Warning, Error, Settings)");

            // Register Navigation Service with Journal support
            containerRegistry.RegisterSingleton<INavigationService, NavigationService>();
            Log.Information("✓ Registered INavigationService with journal support");

            // Register Composite Command Service
            containerRegistry.RegisterSingleton<ICompositeCommandService, CompositeCommandService>();
            Log.Information("✓ Registered ICompositeCommandService for coordinating multiple commands");

            // Register Interaction Request Service
            containerRegistry.RegisterSingleton<IInteractionRequestService, InteractionRequestService>();
            Log.Information("✓ Registered IInteractionRequestService for ViewModel-View communication");

            // Register Scoped Region Service
            containerRegistry.RegisterSingleton<IScopedRegionService, ScopedRegionService>();
            Log.Information("✓ Registered IScopedRegionService for isolated navigation contexts");
            
            // Register Prism Error Handler for centralized error handling
            containerRegistry.RegisterSingleton<IPrismErrorHandler, PrismErrorHandler>();
            Log.Information("✓ Registered IPrismErrorHandler for centralized error handling and logging");
            
            // Register ViewModels (module-specific ViewModels are now registered in their respective modules)
            containerRegistry.RegisterSingleton<MainViewModel>(provider => new MainViewModel(
                provider.Resolve<IRegionManager>(),
                provider.Resolve<IDialogService>(),
                provider.Resolve<IDispatcherHelper>(),
                provider.Resolve<ILogger<MainViewModel>>(),
                provider.Resolve<IEnterpriseRepository>(),
                provider.Resolve<IExcelReaderService>(),
                provider.Resolve<IReportExportService>(),
                provider.Resolve<IBudgetRepository>(),
                provider.Resolve<IAIService>()));

            // Register additional ViewModels for Prism ViewModelLocator (infrastructure-only)
            containerRegistry.Register<AboutViewModel>();
            containerRegistry.Register<ExcelImportViewModel>();
            containerRegistry.Register<ProgressViewModel>();
            containerRegistry.Register<EnterpriseViewModel>();
            
            // Register Region Adapters
            containerRegistry.RegisterSingleton<WileyWidget.Regions.DockingManagerRegionAdapter>();

            // Navigation registrations are now handled by individual modules

            Log.Information("=== DI Container Registration Complete ===");
            Log.Information($"Total registrations: AI Services, Data Repositories, Business Services, ViewModels, Infrastructure");
            Log.Information("Container ready for service resolution");

            // Validate Prism infrastructure and critical services
            ValidatePrismInfrastructure(containerRegistry);
            ValidateCriticalServices(containerRegistry);
        }

        /// <summary>
        /// Validates that all critical services are properly registered and can be resolved.
        /// This prevents runtime errors due to missing DI registrations.
        /// </summary>
        /// <param name="containerRegistry">The container registry to validate</param>
        private void ValidateCriticalServices(IContainerRegistry containerRegistry)
        {
            Log.Information("Validating critical service registrations...");

            (string ServiceName, Type ServiceType)[] criticalServices = new[]
            {
                ("IConfiguration", typeof(IConfiguration)),
                ("ILoggerFactory", typeof(ILoggerFactory)),
                ("ISettingsService", typeof(ISettingsService)),
                ("IEnterpriseRepository", typeof(IEnterpriseRepository)),
                ("IBudgetRepository", typeof(IBudgetRepository)),
                ("IAIService", typeof(IAIService)),
                ("IGrokSupercomputer", typeof(IGrokSupercomputer)),
                ("IWileyWidgetContextService", typeof(IWileyWidgetContextService)),
                ("IAILoggingService", typeof(IAILoggingService)),
                ("IModuleHealthService", typeof(IModuleHealthService)),
            };

            List<string> validationErrors = new List<string>();

            foreach ((string serviceName, Type serviceType) in criticalServices)
            {
                try
                {
                    IUnityContainer validationContainer = containerRegistry.GetContainer();
                    object service = validationContainer.Resolve(serviceType);
                    if (service == null)
                    {
                        validationErrors.Add($"{serviceName} resolved to null");
                    }
                    else
                    {
                        Log.Debug($"✓ {serviceName} validated successfully");
                    }
                }
                catch (Exception ex)
                {
                    validationErrors.Add($"{serviceName} failed to resolve: {ex.Message}");
                    Log.Warning(ex, $"Critical service validation failed for {serviceName}");
                }
            }

            if (validationErrors.Any())
            {
                Log.Error("Critical service validation failed:");
                foreach (var error in validationErrors)
                {
                    Log.Error($"  - {error}");
                }
                throw new InvalidOperationException($"Critical services failed validation: {string.Join(", ", validationErrors)}");
            }

            Log.Information("✓ All critical services validated successfully");
        }

        /// <summary>
        /// Ensures Prism and Unity remain the single composition root by validating container state and legacy configuration.
        /// </summary>
        /// <param name="containerRegistry">The active Prism container registry</param>
        private void ValidatePrismInfrastructure(IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null)
            {
                throw new ArgumentNullException(nameof(containerRegistry));
            }

            var unityContainer = containerRegistry.GetContainer();
            if (unityContainer == null)
            {
                throw new InvalidOperationException("Unity container is not available during Prism startup.");
            }

            var prismContainerType = Container?.GetType().FullName ?? "(unavailable)";
            Log.Information("Prism container provider: {PrismContainerType}", prismContainerType);

            if (Application.Current is not App)
            {
                throw new InvalidOperationException("Application.Current is not WileyWidget.App. Legacy WPF startup path detected.");
            }

            if (Application.Current.StartupUri != null)
            {
                Log.Warning("StartupUri detected ({StartupUri}); Prism requires StartupUri to remain null. Clearing legacy configuration to enforce Prism-first navigation.", Application.Current.StartupUri);
                Application.Current.StartupUri = null;
            }

            var unityRegistrationCount = unityContainer.Registrations.Count();
            Log.Information("Unity container registration count: {RegistrationCount}", unityRegistrationCount);
        }

        private void RegisterAppOptions(IContainerRegistry containerRegistry, IConfiguration configuration, IUnityContainer unityContainer)
        {
            if (containerRegistry == null)
            {
                throw new ArgumentNullException(nameof(containerRegistry));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (unityContainer == null)
            {
                throw new ArgumentNullException(nameof(unityContainer));
            }

            try
            {
                var appOptions = new AppOptions();
                configuration.GetSection("App").Bind(appOptions);

                try
                {
                    var configurator = unityContainer.Resolve<AppOptionsConfigurator>();
                    configurator.Configure(appOptions);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "AppOptionsConfigurator failed during startup; continuing with configuration-only AppOptions values.");
                }

                var optionsWrapper = Options.Create(appOptions);
                containerRegistry.RegisterInstance<IOptions<AppOptions>>(optionsWrapper);

                var monitor = new StaticOptionsMonitor<AppOptions>(appOptions, Log.Logger);
                containerRegistry.RegisterInstance<IOptionsMonitor<AppOptions>>(monitor);

                Log.Information("✓ Registered AppOptions for SettingsViewModel via Options bridge");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register AppOptions bridge; registering fallback options instance");

                var fallback = new AppOptions();
                containerRegistry.RegisterInstance<IOptions<AppOptions>>(Options.Create(fallback));
                containerRegistry.RegisterInstance<IOptionsMonitor<AppOptions>>(new StaticOptionsMonitor<AppOptions>(fallback, Log.Logger));
            }
        }

        [Conditional("DEBUG")]
        private static void EnableUnityDiagnostics(IUnityContainer unityContainer)
        {
            if (unityContainer == null)
            {
                throw new ArgumentNullException(nameof(unityContainer));
            }

            unityContainer.AddExtension(new UnityDebugExtension());

            if (!Trace.Listeners.OfType<ConsoleTraceListener>().Any(listener => listener.Name == "UnityConsole"))
            {
                Trace.Listeners.Add(new ConsoleTraceListener { Name = "UnityConsole" });
            }

            Trace.WriteLine("[Unity] Debug diagnostics initialized");
        }

        /// <summary>
        /// Registers HttpClient infrastructure for AI services with retry policies and timeout configuration.
        /// Production-ready implementation with comprehensive error handling and logging.
        /// </summary>
        /// <param name="containerRegistry">The Unity container registry for DI registration</param>
        /// <param name="configuration">Application configuration for HttpClient settings</param>
        private void RegisterHttpClientServices(IContainerRegistry containerRegistry, IConfiguration configuration)
        {
            Log.Information("=== Registering HttpClient Infrastructure for AI Services ===");
            
            try
            {
                var xaiBaseUrl = configuration["XAI:BaseUrl"];
                if (string.IsNullOrWhiteSpace(xaiBaseUrl))
                {
                    xaiBaseUrl = "https://api.x.ai/v1/";
                }

                if (!double.TryParse(configuration["XAI:TimeoutSeconds"], out var timeoutSeconds) || timeoutSeconds <= 0)
                {
                    timeoutSeconds = 30d;
                }

                var aiTimeout = TimeSpan.FromSeconds(timeoutSeconds);
                var defaultTimeout = TimeSpan.FromSeconds(30);

                Func<string, HttpClient> clientBuilder = name =>
                {
                    var normalized = string.IsNullOrWhiteSpace(name) ? "Default" : name;

                    var handler = new SocketsHttpHandler
                    {
                        AllowAutoRedirect = true,
                        MaxAutomaticRedirections = 3,
                        AutomaticDecompression = DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                    };

                    var client = new HttpClient(handler, disposeHandler: true)
                    {
                        Timeout = defaultTimeout
                    };

                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget/1.0");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    if (string.Equals(normalized, "AIServices", StringComparison.OrdinalIgnoreCase))
                    {
                        client.BaseAddress = new Uri(xaiBaseUrl);
                        client.Timeout = aiTimeout;
                        Log.Debug("Configured HttpClient '{ClientName}' with BaseAddress {BaseAddress} and Timeout {TimeoutSeconds}s", normalized, client.BaseAddress, aiTimeout.TotalSeconds);
                    }
                    else
                    {
                        Log.Debug("Configured HttpClient '{ClientName}' with default timeout {TimeoutSeconds}s", normalized, defaultTimeout.TotalSeconds);
                    }

                    return client;
                };

                #pragma warning disable CA2000 // Prism container manages the lifetime of the registered factory singleton
                var httpClientFactory = new PrismHttpClientFactory(clientBuilder);
                #pragma warning restore CA2000
                containerRegistry.RegisterInstance<IHttpClientFactory>(httpClientFactory);

                Log.Information("✓ Registered PrismHttpClientFactory for IHttpClientFactory");
                Log.Information("  - Named client 'AIServices' => Base URL: {BaseUrl}, Timeout: {Timeout}s", xaiBaseUrl, aiTimeout.TotalSeconds);
                Log.Information("  - Default timeout for unnamed clients: {DefaultTimeout}s", defaultTimeout.TotalSeconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register HttpClient infrastructure for AI services");
                throw new InvalidOperationException("Failed to configure HttpClient for AI services. Check configuration and network settings.", ex);
            }
        }

        /// <summary>
        /// Registers database services using Microsoft.Extensions.DependencyInjection pattern
        /// </summary>
        /// <param name="containerRegistry">The Unity container registry for DI registration</param>
        /// <param name="configuration">Application configuration for database settings</param>
        private void RegisterDatabaseServices(IContainerRegistry containerRegistry, IConfiguration configuration)
        {
            Log.Information("=== Registering Database Services ===");

            try
            {
                // Unity-only approach: build DbContextOptions<AppDbContext> from configuration
                // and register a small Unity-friendly factory implementation that takes
                // DbContextOptions<AppDbContext> in its constructor. This removes the
                // cross-container ServiceCollection usage and keeps DI with Unity only.

                var connectionString = configuration.GetConnectionString("DefaultConnection")
                                       ?? "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                // Configure SQL Server with reasonable defaults (migrations assembly, retries, timeout)
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("WileyWidget.Data");
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                });

                optionsBuilder.EnableDetailedErrors();
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

                var options = optionsBuilder.Options;

                // Register the options instance with Unity
                containerRegistry.RegisterInstance<DbContextOptions<AppDbContext>>(options);

                // Register a Unity-friendly factory implementation that Unity can construct
                // because it takes DbContextOptions<AppDbContext> in its ctor
                containerRegistry.RegisterSingleton<IDbContextFactory<AppDbContext>, WileyWidget.Data.UnityAppDbContextFactory>();

                // Register AppDbContext using the factory
                containerRegistry.Register<AppDbContext>(provider => provider.Resolve<IDbContextFactory<AppDbContext>>().CreateDbContext());

                Log.Information("✓ Registered database services (AppDbContext, IDbContextFactory via Unity)");
                Log.Information("  - Provider: SQL Server with connection pooling");
                Log.Information("  - Features: Unity-only registration, repositories, audit logging");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register database services");
                throw new InvalidOperationException("Failed to configure database services. Check connection string and database availability.", ex);
            }
        }

        /// <summary>
        /// Registers AI Integration Services for Phase 1 production deployment.
        /// Includes GrokSupercomputer, WileyWidgetContextService, and enhanced XAIService.
        /// All services are registered as singletons for optimal performance and resource management.
        /// </summary>
        /// <param name="containerRegistry">The Unity container registry for DI registration</param>
        private void RegisterAIIntegrationServices(IContainerRegistry containerRegistry)
        {
            Log.Information("=== Registering AI Integration Services (Phase 1 - Production) ===");
            
            try
            {
                // 0. Register Application Insights Telemetry (Singleton)
                // Production telemetry for AI service monitoring and performance tracking
                // NOTE: Commented out until Azure/Application Insights is configured
                /*
                var config = Container.Resolve<IConfiguration>();
                var instrumentationKey = config["ApplicationInsights:InstrumentationKey"];
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    var telemetryConfiguration = new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration();
                    telemetryConfiguration.ConnectionString = config["ApplicationInsights:ConnectionString"] ?? $"InstrumentationKey={instrumentationKey}";
                    
                    var telemetryClient = new Microsoft.ApplicationInsights.TelemetryClient(telemetryConfiguration);
                    containerRegistry.RegisterInstance(telemetryClient);
                    Log.Information("✓ Registered Application Insights TelemetryClient (Singleton)");
                    Log.Information("  - Production telemetry for AI service monitoring");
                    Log.Information("  - Features: Request tracking, dependency monitoring, custom events, metrics");
                    Log.Information("  - Configuration: InstrumentationKey, ConnectionString from appsettings.json");
                }
                else
                {
                    Log.Warning("Application Insights not configured - set ApplicationInsights:InstrumentationKey in appsettings.json for production telemetry");
                }
                */
                
                // 0. Register IDataAnonymizerService -> DataAnonymizerService (Singleton)
                // Provides privacy-compliant data anonymization for AI operations
                containerRegistry.RegisterSingleton<IDataAnonymizerService, DataAnonymizerService>();
                Log.Information("✓ Registered IDataAnonymizerService -> DataAnonymizerService (Singleton)");
                Log.Information("  - Provides GDPR-compliant data anonymization");
                Log.Information("  - Features: Enterprise anonymization, budget data masking, deterministic hashing");
                Log.Information("  - Dependencies: ILogger<DataAnonymizerService>");
                
                // 1. Register IWileyWidgetContextService -> WileyWidgetContextService (Singleton)
                // Provides dynamic context building for AI operations including system state, enterprises, budgets, and operations
                containerRegistry.RegisterSingleton<IWileyWidgetContextService, WileyWidgetContextService>();
                Log.Information("✓ Registered IWileyWidgetContextService -> WileyWidgetContextService (Singleton)");
                Log.Information("  - Provides dynamic context for AI operations with anonymization support");
                Log.Information("  - Dependencies: ILogger<WileyWidgetContextService>, IEnterpriseRepository, IBudgetRepository, IAuditRepository, IDataAnonymizerService");
                
                // 1.5. Register IAILoggingService -> AILoggingService (Singleton)
                // AI usage tracking and logging service for monitoring XAI operations
                containerRegistry.RegisterSingleton<IAILoggingService, AILoggingService>();
                Log.Information("✓ Registered IAILoggingService -> AILoggingService (Singleton)");
                Log.Information("  - AI usage tracking and monitoring service");
                Log.Information("  - Features: Query/response logging, error tracking, usage metrics, statistics");
                Log.Information("  - Logging: Dedicated Serilog file sink at logs/ai-usage.log");
                Log.Information("  - Dependencies: ILogger<AILoggingService>");
                
                // 2. Register IAIService -> XAIService (Singleton) - Enhanced with context service and logging
                // xAI service implementation for AI-powered insights and analysis with Grok integration
                containerRegistry.RegisterSingleton<IAIService, XAIService>();
                Log.Information("✓ Registered IAIService -> XAIService (Singleton) [Enhanced]");
                Log.Information("  - xAI/Grok integration for AI-powered insights");
                Log.Information("  - Features: Insights, data analysis, area review, mock data generation");
                Log.Information("  - Dependencies: IHttpClientFactory, IConfiguration, ILogger<XAIService>, IWileyWidgetContextService, IAILoggingService, IMemoryCache");
                Log.Information("  - Configuration: XAI:ApiKey, XAI:BaseUrl, XAI:Model, XAI:TimeoutSeconds");
                
                // 3. Register IGrokSupercomputer -> GrokSupercomputer (Singleton)
                // AI-powered municipal utility analytics and compliance reporting engine
                containerRegistry.RegisterSingleton<IGrokSupercomputer, GrokSupercomputer>();
                Log.Information("✓ Registered IGrokSupercomputer -> GrokSupercomputer (Singleton)");
                Log.Information("  - AI-powered municipal utility analytics engine");
                Log.Information("  - Capabilities: Enterprise data fetching, report calculations, budget analysis, compliance reporting, AI data analysis");
                Log.Information("  - Dependencies: ILogger<GrokSupercomputer>, IEnterpriseRepository, IBudgetRepository, IAuditRepository, IAILoggingService, IAIService");
                
                // 4. Validate AI service configuration
                ValidateAIServiceConfiguration();
                
                Log.Information("=== AI Integration Services Registration Complete ===");
                Log.Information("All AI services registered successfully with singleton lifetime scope");
                Log.Information("Services ready for production use with comprehensive dependency injection");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CRITICAL: Failed to register AI Integration Services");
                Log.Error("Application may not function correctly without AI services");
                Log.Error("Please check configuration (appsettings.json) and ensure all dependencies are available");
                throw new InvalidOperationException("Failed to register AI Integration Services. Application cannot continue.", ex);
            }
        }

        /// <summary>
        /// Validates AI service configuration to ensure all required settings are present.
        /// Production-ready validation with comprehensive error reporting.
        /// </summary>
        private void ValidateAIServiceConfiguration()
        {
            Log.Information("Validating AI service configuration...");
            
            try
            {
                var config = Container.Resolve<IConfiguration>();
                var validationErrors = new List<string>();
                
                // Validate XAI configuration
                var apiKey = config["XAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? string.Empty;
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        Log.Information("XAI:ApiKey pulled from environment variable XAI_API_KEY");
                    }
                }
                Log.Information("XAI:ApiKey resolved to: {ApiKeyMasked} (length: {Length})", 
                    string.IsNullOrEmpty(apiKey) ? "null/empty" : $"{apiKey.Substring(0, Math.Min(10, apiKey.Length))}...", 
                    apiKey?.Length ?? 0);
                
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    validationErrors.Add("XAI:ApiKey is missing or empty");
                }
                else if (apiKey.Length < 20)
                {
                    validationErrors.Add("XAI:ApiKey appears invalid (too short, expected 20+ characters)");
                }
                
                var baseUrl = config["XAI:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    Log.Warning("XAI:BaseUrl not configured, using default: https://api.x.ai/v1/");
                }
                else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    validationErrors.Add($"XAI:BaseUrl is invalid: {baseUrl}");
                }
                
                var model = config["XAI:Model"];
                if (string.IsNullOrWhiteSpace(model))
                {
                    Log.Warning("XAI:Model not configured, using default: grok-4-0709");
                }
                
                var timeout = config["XAI:TimeoutSeconds"];
                if (!string.IsNullOrWhiteSpace(timeout) && !double.TryParse(timeout, out var timeoutValue))
                {
                    validationErrors.Add($"XAI:TimeoutSeconds is invalid: {timeout}");
                }
                
                if (validationErrors.Any())
                {
                    Log.Error("AI Service configuration validation failed:");
                    foreach (var error in validationErrors)
                    {
                        Log.Error($"  - {error}");
                    }
                    throw new InvalidOperationException($"AI Service configuration is invalid: {string.Join(", ", validationErrors)}");
                }
                
                Log.Information("✓ AI service configuration validated successfully");
                Log.Information($"  - API Key: Configured ({apiKey?.Substring(0, Math.Min(8, apiKey.Length))}...)");
                Log.Information($"  - Base URL: {baseUrl ?? "Default (https://api.x.ai/v1/)"}");
                Log.Information($"  - Model: {model ?? "Default (grok-4-0709)"}");
                Log.Information($"  - Timeout: {timeout ?? "Default (30)"} seconds");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to validate AI service configuration");
                throw;
            }
        }

        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            base.ConfigureRegionAdapterMappings(regionAdapterMappings);

            // Standard WPF control region adapters are registered by base.ConfigureRegionAdapterMappings()
            // including TabControl, ContentControl, ItemsControl, and Selector

            // Custom adapter for Syncfusion controls (e.g., DockingManager)
            regionAdapterMappings.RegisterMapping<Syncfusion.Windows.Tools.Controls.DockingManager>(Container.Resolve<WileyWidget.Regions.DockingManagerRegionAdapter>());

            // Register region behaviors
            var regionBehaviorFactory = Container.Resolve<IRegionBehaviorFactory>();

            // Navigation logging behavior for all regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.NavigationLoggingBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.NavigationLoggingBehavior));

            // Auto-save behavior for data entry regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.AutoSaveBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.AutoSaveBehavior));

            // Navigation history behavior for main content regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.NavigationHistoryBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.NavigationHistoryBehavior));

            // Auto-activate behavior for single-view regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.AutoActivateBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.AutoActivateBehavior));

            Log.Information("✓ Registered Prism region behaviors: NavigationLogging, AutoSave, NavigationHistory, AutoActivate");
        }

        // Cache configuration to avoid redundant loading
        private IConfiguration? _cachedConfiguration;

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            Log.Information("=== Configuring Prism Module Catalog with Auto-Discovery and Initialization Modes ===");

            // Auto-discover modules using reflection
            // Find all types in the current assembly that implement IModule and have [Module] attribute
            var moduleTypes = typeof(App).Assembly
                .GetTypes()
                .Where(t => typeof(IModule).IsAssignableFrom(t) &&
                           t.GetCustomAttributes(typeof(ModuleAttribute), false).Any() &&
                           !t.IsAbstract &&
                           t.IsClass)
                .ToList();

            Log.Information("Found {Count} modules with [Module] attribute", moduleTypes.Count);

            // Get initialization mode from configuration
            IConfiguration configuration = _cachedConfiguration ??= BuildConfiguration();
            var defaultInitMode = configuration.GetValue("Prism:DefaultModuleInitializationMode", "WhenAvailable");

            foreach (var moduleType in moduleTypes)
            {
                var moduleAttribute = (ModuleAttribute)moduleType.GetCustomAttributes(typeof(ModuleAttribute), false).First();
                var moduleName = moduleAttribute.ModuleName;

                // Determine initialization mode for this module
                var initMode = GetModuleInitializationMode(moduleName, defaultInitMode);

                // Add module with specified initialization mode
                moduleCatalog.AddModule(moduleType, initMode);

                Log.Information("Registered module: {ModuleName} ({TypeName}) with initialization mode: {InitMode}",
                    moduleName, moduleType.Name, initMode);
            }

            Log.Information("✓ Auto-discovery completed for {Count} modules with configurable initialization modes", moduleTypes.Count);
        }

        protected override void InitializeModules()
        {
            Log.Information("Modules initializing...");

            base.InitializeModules();

            // Get the module health service for validation
            var moduleHealthService = Container.Resolve<IModuleHealthService>();

            // Validate module initialization and region availability
            ValidateModuleInitialization(moduleHealthService);

            // Initialize global error handling for Prism navigation and general errors
            InitializeGlobalErrorHandling();

            Log.Information("Modules initialized.");
        }

        /// <summary>
        /// Initializes global error handling for Prism applications.
        /// Sets up EventAggregator subscriptions for centralized error handling and logging.
        /// </summary>
        private void InitializeGlobalErrorHandling()
        {
            Log.Information("=== Initializing Global Error Handling ===");

            try
            {
                // Resolve the error handler service
                var errorHandler = Container.Resolve<IPrismErrorHandler>();
                var eventAggregator = Container.Resolve<Prism.Events.IEventAggregator>();

                if (eventAggregator == null)
                {
                    Log.Warning("IEventAggregator could not be resolved from the container. Global error handling subscriptions will not be registered.");
                }
                else
                {
                    // Subscribe to navigation error events for global handling
                    eventAggregator.GetEvent<NavigationErrorEvent>().Subscribe(
                        errorEvent =>
                        {
                            Log.Error("Global navigation error handler: Region '{RegionName}' failed to navigate to '{TargetView}': {ErrorMessage}",
                                errorEvent.RegionName, errorEvent.TargetView, errorEvent.ErrorMessage);
                        },
                        ThreadOption.UIThread); // Handle on UI thread for dialog display

                    // Subscribe to general error events for global handling
                    eventAggregator.GetEvent<GeneralErrorEvent>().Subscribe(
                        errorEvent =>
                        {
                            var logLevel = errorEvent.IsHandled ? LogEventLevel.Warning : LogEventLevel.Error;
                            Log.Write(logLevel, errorEvent.Error, "Global error handler: {Source}.{Operation} - {ErrorMessage}",
                                errorEvent.Source, errorEvent.Operation, errorEvent.ErrorMessage);
                        },
                        ThreadOption.UIThread);
                }

                if (errorHandler != null)
                {
                    errorHandler.RegisterGlobalNavigationHandlers();
                    Log.Information("✓ Global navigation handlers registered");
                }
                else
                {
                    Log.Warning("IPrismErrorHandler could not be resolved from the container. Global navigation handlers not registered.");
                }

                Log.Information("✓ Global error handling initialized with EventAggregator subscriptions");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize global error handling");
            }
        }

        private InitializationMode GetModuleInitializationMode(string moduleName, string defaultMode)
        {
            // Define module-specific initialization modes
            var moduleInitModes = new Dictionary<string, InitializationMode>
            {
                // Core infrastructure module - load immediately (consolidated from Diagnostics, Syncfusion, Settings)
                ["CoreModule"] = InitializationMode.WhenAvailable,
                ["QuickBooksModule"] = InitializationMode.OnDemand,

                // Feature modules - load on demand to improve startup performance
                ["DashboardModule"] = InitializationMode.WhenAvailable,
                ["EnterpriseModule"] = InitializationMode.OnDemand,
                ["BudgetModule"] = InitializationMode.OnDemand,
                ["MunicipalAccountModule"] = InitializationMode.OnDemand,
                ["UtilityCustomerModule"] = InitializationMode.OnDemand,
                // Load these at startup so navigation by name succeeds immediately
                ["ReportsModule"] = InitializationMode.WhenAvailable,
                ["AIAssistModule"] = InitializationMode.WhenAvailable,

                // Panel modules - load when their dependencies are loaded
                ["PanelModule"] = InitializationMode.WhenAvailable,
                ["ToolsModule"] = InitializationMode.OnDemand
            };

            return moduleInitModes.TryGetValue(moduleName, out var mode) ? mode : Enum.Parse<InitializationMode>(defaultMode);
        }

        /// <summary>
        /// Validates that all modules are properly initialized and their regions are available.
        /// Provides comprehensive diagnostics for module and region health.
        /// </summary>
        /// <param name="moduleHealthService">The module health service for status checking</param>
        private void ValidateModuleInitialization(IModuleHealthService moduleHealthService)
        {
            // Align expected regions with actual regions declared in MainWindow.xaml
            var moduleRegionMap = new Dictionary<string, string[]>
            {
                ["DashboardModule"] = new[] { "DashboardRegion" },
                ["EnterpriseModule"] = new[] { "EnterpriseRegion" },
                ["BudgetModule"] = new[] { "BudgetRegion" },
                ["MunicipalAccountModule"] = new[] { "MunicipalAccountRegion" },
                ["UtilityCustomerModule"] = new[] { "UtilityCustomerRegion" },
                ["ReportsModule"] = new[] { "ReportsRegion" },
                ["AIAssistModule"] = new[] { "AIAssistRegion" },
                ["PanelModule"] = new[] { "LeftPanelRegion", "RightPanelRegion", "BottomPanelRegion" },
                ["ToolsModule"] = new[] { "BottomPanelRegion" }
            };

            Log.Information("=== Validating Module Initialization and Region Availability ===");

            var regionManager = Container.Resolve<IRegionManager>();
            var validationResults = new List<string>();

            foreach (var moduleStatus in moduleHealthService.GetAllModuleStatuses())
            {
                var moduleName = moduleStatus.ModuleName;
                var status = moduleStatus.Status;

                if (status == ModuleHealthStatus.Healthy)
                {
                    // Check if expected regions are available for healthy modules
                    if (moduleRegionMap.TryGetValue(moduleName, out var expectedRegions))
                    {
                        var missingRegions = expectedRegions.Where(region => !regionManager.Regions.ContainsRegionWithName(region)).ToList();
                        if (missingRegions.Any())
                        {
                            validationResults.Add($"WARNING: Module '{moduleName}' healthy but missing regions: {string.Join(", ", missingRegions)}");
                        }
                        else
                        {
                            validationResults.Add($"✓ Module '{moduleName}' validation passed - all regions available");
                        }
                    }
                    else
                    {
                        validationResults.Add($"✓ Module '{moduleName}' validation passed - no regions to validate");
                    }
                }
                else if (status == ModuleHealthStatus.Failed)
                {
                    validationResults.Add($"✗ Module '{moduleName}' failed initialization: {moduleStatus.ErrorMessage}");
                }
                else
                {
                    validationResults.Add($"? Module '{moduleName}' status: {status}");
                }
            }

            // Log validation results
            foreach (var result in validationResults)
            {
                if (result.StartsWith("✓"))
                    Log.Information(result);
                else if (result.StartsWith("✗"))
                    Log.Error(result);
                else if (result.StartsWith("WARNING"))
                    Log.Warning(result);
                else
                    Log.Information(result);
            }

            var healthyModules = moduleHealthService.GetAllModuleStatuses().Count(m => m.Status == ModuleHealthStatus.Healthy);
            var totalModules = moduleHealthService.GetAllModuleStatuses().Count();

            Log.Information("=== Module Validation Complete ===");
            Log.Information("Modules Healthy: {Healthy}/{Total}", healthyModules, totalModules);

            if (healthyModules == totalModules)
            {
                Log.Information("✓ All modules validated successfully - application ready");
            }
            else
            {
                Log.Warning("⚠ Some modules failed validation - application may have reduced functionality");
            }
        }

        protected override void OnInitialized()
        {
            try
            {
                base.OnInitialized();

                Log.Information("Application initialization completed successfully");
                Log.Information("All services registered and container ready for use");

                Application.Current.MainWindow?.Show();

                // Log module health report after initialization
                try
                {
                    var moduleHealthService = Container.Resolve<IModuleHealthService>();
                    moduleHealthService.LogHealthReport();

                    // Validate module initialization and region availability
                    ValidateModuleInitialization(moduleHealthService);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to generate module health report during application initialization");
                }

                try
                {
                    // Optional: Navigate to default view
                    // var regionManager = Container.Resolve<IRegionManager>();
                    // regionManager.RequestNavigate("MainRegion", "DashboardView");
                    // Log.Information("Navigated to DashboardView during application initialization");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to navigate to default view during startup - continuing without navigation");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error during application initialization");
                throw;
            }
        }

        /// <summary>
        /// Registers a module with the module catalog and tracks it with the health service.
        /// </summary>
        /// <param name="moduleCatalog">The module catalog to add the module to</param>
        /// <param name="healthService">The health service to track the module</param>
        /// <param name="moduleName">The name of the module for tracking</param>
        /// <param name="registerAction">The action to perform the module registration</param>
        private void RegisterModuleWithHealthTracking(IModuleCatalog moduleCatalog, IModuleHealthService healthService, string moduleName, Action registerAction)
        {
            try
            {
                healthService.RegisterModule(moduleName);
                registerAction();
                Log.Debug("Successfully registered module '{ModuleName}' with catalog and health tracking", moduleName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register module '{ModuleName}' with catalog", moduleName);
                healthService.MarkModuleInitialized(moduleName, false, ex.Message);
                throw;
            }
        }

        protected override IContainerExtension CreateContainerExtension()
        {
#pragma warning disable CA2000 // Call System.IDisposable.Dispose on object created by 'new UnityContainer()' before all references to it are out of scope
            var container = new UnityContainer();
#pragma warning restore CA2000
            container.AddExtension(new Diagnostic());
            return new UnityContainerExtension(container);
        }

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/wiley-widget-.log", 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== WileyWidget Prism application startup ===");
        }

        private IConfiguration BuildConfiguration()
        {
            // Load .env file if it exists
            DotNetEnv.Env.Load();

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            var configurationRoot = builder.Build();
            ResolveConfigurationPlaceholders(configurationRoot);

            return configurationRoot;
        }

        private static void ResolveConfigurationPlaceholders(IConfigurationRoot configurationRoot)
        {
            if (configurationRoot == null)
            {
                return;
            }

            var values = configurationRoot.AsEnumerable().ToList();
            foreach (var entry in values)
            {
                if (string.IsNullOrWhiteSpace(entry.Value) || entry.Value.IndexOf("${", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var replaced = PlaceholderRegex.Replace(entry.Value, match =>
                {
                    var variableName = match.Groups["name"].Value;
                    if (string.IsNullOrWhiteSpace(variableName))
                    {
                        return match.Value;
                    }

                    var resolved = Environment.GetEnvironmentVariable(variableName);
                    if (string.IsNullOrEmpty(resolved))
                    {
                        resolved = configurationRoot[variableName];
                    }

                    return string.IsNullOrEmpty(resolved) ? match.Value : resolved;
                });

                if (!string.Equals(replaced, entry.Value, StringComparison.Ordinal))
                {
                    configurationRoot[entry.Key] = replaced;
                }
            }
        }

        private static readonly Regex PlaceholderRegex = new("\\$\\{(?<name>[A-Za-z0-9_]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Ensure application shutdown is robust. Some third-party libraries (Syncfusion) may attempt to show
        /// dialogs during shutdown which can throw if the UI thread or owner windows are disposed. We catch
        /// and swallow known shutdown-time exceptions to avoid the process terminating with an unhelpful crash.
        /// </summary>
        /// <param name="e">Exit event args</param>
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutdown initiated");

            try
            {
                // Best-effort: re-register Syncfusion license in case some Syncfusion component needs it during shutdown
                EnsureSyncfusionLicenseRegistered(forceRefresh: true);

                base.OnExit(e);
                Log.Information("Application shutdown completed successfully");
            }
            catch (Exception ex)
            {
                // Special handling: Syncfusion LicenseMessage.ShowDialog can throw during shutdown
                var msg = ex.ToString();
                if (msg.Contains("LicenseMessage", StringComparison.OrdinalIgnoreCase) || msg.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
                {
                    // Log and swallow to prevent fatal shutdown crash
                    Log.Warning(ex, "Non-fatal Syncfusion-related exception occurred during OnExit; swallowing to allow graceful exit");
                }
                else
                {
                    // Unknown exception - log as fatal but do not rethrow to avoid ungraceful termination
                    Log.Fatal(ex, "Unhandled exception during application shutdown");
                }
            }
            finally
            {
                // Ensure logger flush
                try { Log.CloseAndFlush(); } catch { }
            }
        }

        /// <summary>
        /// Provides access to Prism's active container provider for scenarios where
        /// code-behind needs to resolve services outside of the ViewModelLocator pipeline.
        /// </summary>
        public static IContainerProvider GetContainerProvider()
        {
            if (Application.Current is not App app)
            {
                throw new InvalidOperationException("Application is not initialized or not of type App.");
            }

            return app.Container
                ?? throw new InvalidOperationException("Prism container is not available during application lifetime.");
        }
    }
}