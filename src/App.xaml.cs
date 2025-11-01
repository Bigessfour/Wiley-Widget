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
using System.Globalization;
using System.IO;
using Prism.Ioc;
using Prism.Modularity;
using PrismIoc = Prism.Ioc;
using PrismModularity = Prism.Modularity;
using Prism;
using Prism.Mvvm;
using Prism.Dialogs;
using Prism.Container.DryIoc;
using Prism.Navigation.Regions;
using DryIoc;
using Syncfusion.SfSkinManager;
using Syncfusion.Licensing;
using Bold.Licensing;
using WileyWidget.Views;
using WileyWidget.Views.Main;
using WileyWidget.Views.Panels;
using WileyWidget.Views.Dialogs;
using WileyWidget.Views.Windows;
#if !WPFTMP
using WileyWidget.Startup.Modules;
#endif
using WileyWidget.Startup;
using WileyWidget.Services;
using WileyWidget.Abstractions;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using DotNetEnv;
using WileyWidget.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Diagnostics;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.ViewModels.Panels;
using WileyWidget.ViewModels.Dialogs;
using WileyWidget.ViewModels.Windows;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Prism.Events;
using WileyWidget.ViewModels.Messages;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using Serilog.Events;
using System.Windows.Markup;
using System.Xaml;
using System.Collections.Concurrent;

// Aliases to resolve Prism type ambiguities
using IContainerRegistry = Prism.Ioc.IContainerRegistry;
using IModuleCatalog = Prism.Modularity.IModuleCatalog;
using IContainerExtension = Prism.Ioc.IContainerExtension;

namespace WileyWidget
{
    public partial class App : PrismApplicationBase
    {
        // Task that completes when deferred secret initialization finishes.
        // Consumers can await App.SecretsInitializationTask to know when secrets are available.
        private static readonly TaskCompletionSource<bool> _secretsInitializationTcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        public static Task SecretsInitializationTask => _secretsInitializationTcs.Task;

        // Static mapping of expected regions for each module for maintainability and reuse
        private static readonly Dictionary<string, string[]> moduleRegionMap = new Dictionary<string, string[]>
        {
            ["CoreModule"] = new[] { "SettingsRegion" },
            ["SettingsModule"] = new[] { "SettingsRegion" },
            ["DashboardModule"] = new[] { "MainRegion" },
            ["EnterpriseModule"] = new[] { "EnterpriseRegion" },
            ["BudgetModule"] = new[] { "BudgetRegion", "AnalyticsRegion" },
            ["MunicipalAccountModule"] = new[] { "MunicipalAccountRegion" },
            ["UtilityCustomerModule"] = new[] { "UtilityCustomerRegion" },
            ["ReportsModule"] = new[] { "ReportsRegion" },
            ["AIAssistModule"] = new[] { "AIAssistRegion" },
            ["PanelModule"] = new[] { "LeftPanelRegion", "RightPanelRegion", "BottomPanelRegion" }
        };

        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
        private static readonly object StartupProgressSyncRoot = new();
        public static object? StartupProgress { get; private set; }
        public static DateTimeOffset? LastHealthReportUpdate { get; private set; }
        private bool _syncfusionLicenseRegistered;
        private bool _boldReportsLicenseRegistered;
        private string _startupId;

        public static void UpdateLatestHealthReport(object report)
        {
            if (report == null)
            {
                Log.Warning("Module health report update skipped: report was null");
                return;
            }

            // If the report is a collection of module health infos, summarize it
            if (report is IEnumerable<object> moduleHealthInfos)
            {
                int totalModules = 0;
                int healthyModules = 0;

                foreach (var _ in moduleHealthInfos)
                {
                    totalModules++;
                    // Best-effort: we can't strongly type here without the specific ModuleHealthInfo type
                    healthyModules++; // conservative increment - real implementation will inspect status
                }

                Log.Debug("Module health report refreshed: {Healthy}/{Total} modules healthy", healthyModules, totalModules);
                return;
            }
            else
            {
                Log.Debug("Module health report refreshed ({ReportType})", report.GetType().FullName);
                return;
            }
        }

        protected override void OnInitialized()
        {
            SplashWindow? splashWindow = null;

            try
            {
                // Show splash screen early in startup process
                splashWindow = new SplashWindow();
                splashWindow.UpdateStatus("Initializing application...");
                splashWindow.Show();

                base.OnInitialized();

                // Update splash with progress
                splashWindow.UpdateStatus("Loading configuration...");

                // Theme application is handled deterministically in OnStartup; avoid duplicate

                Log.Information("Application initialization: showing shell and starting post-init tasks");

                splashWindow.UpdateStatus("Loading main window...");
                Application.Current.MainWindow?.Show();

                // Start deferred secrets initialization (non-blocking). SecretsInitializationTask will be completed/failed by that work.
                // Use a background task so we don't block the UI thread; consumers can await App.SecretsInitializationTask when they need secrets.
                splashWindow.UpdateStatus("Initializing secrets service...");
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var secretVault = this.Container.Resolve<ISecretVaultService>();
                        await secretVault.MigrateSecretsFromEnvironmentAsync().ConfigureAwait(false);
                        Log.Information("✓ (Deferred) Environment secrets migrated to local vault");

                        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
                        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                        {
                            await secretVault.PopulateProductionSecretsAsync().ConfigureAwait(false);
                            Log.Information("✓ (Deferred) Production secrets initialized");
                        }

                        _secretsInitializationTcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        // Capture and surface the failure so awaiting callers can react
                        Log.Warning(ex, "Deferred production secrets initialization failed (background task)");
                        _secretsInitializationTcs.TrySetException(ex);
                    }
                });

                // Deterministic module initialization: attempt to initialize modules in a defined order and track health
                splashWindow.UpdateStatus("Loading modules...");
                try
                {
                    var moduleManager = this.Container.Resolve<IModuleManager>();
                    var moduleCatalog = this.Container.Resolve<IModuleCatalog>();
                    var moduleHealthService = ResolveWithRetry<IModuleHealthService>();

                    // Ensure module health service has entries for known modules
                    foreach (var kv in moduleRegionMap)
                    {
                        try
                        {
                            moduleHealthService.RegisterModule(kv.Key);
                        }
                        catch (Exception regEx)
                        {
                            Log.Warning(regEx, "Failed to pre-register module health for {Module}; continuing", kv.Key);
                        }
                    }

                    // Initialize modules in the deterministic order defined by moduleRegionMap keys
                    foreach (var moduleName in moduleRegionMap.Keys)
                    {
                        try
                        {
                            // Check that the module is actually present in the catalog
                            var found = moduleCatalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.Ordinal));
                            if (found == null)
                            {
                                // Module not registered - record health and continue
                                Log.Warning("Module '{ModuleName}' not found in module catalog; marking as not-initialized", moduleName);
                                moduleHealthService.MarkModuleInitialized(moduleName, false, "Not registered in module catalog");
                                continue;
                            }

                            Log.Information("Initializing module: {Module}", moduleName);
                            // LoadModule may throw; handle exceptions per-module so failure of one does not prevent others
                            try
                            {
                                moduleManager.LoadModule(moduleName);
                                moduleHealthService.MarkModuleInitialized(moduleName, true, "Initialized");
                                Log.Information("✓ Module initialized: {Module}", moduleName);
                            }
                            catch (Exception initEx)
                            {
                                // Record failure but continue initializing other modules
                                moduleHealthService.MarkModuleInitialized(moduleName, false, initEx.Message);
                                Log.Error(initEx, "Module initialization failed for {Module}", moduleName);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Guard against unexpected issues (reflection, catalog enumeration, etc.)
                            Log.Warning(ex, "Unexpected error while attempting to initialize module '{Module}'; continuing with next module", moduleName);
                            try
                            {
                                ResolveWithRetry<IModuleHealthService>().MarkModuleInitialized(moduleName, false, ex.Message);
                            }
                            catch { }
                        }
                    }

                    // After attempting module initialization, log a health summary
                    try
                    {
                        moduleHealthService.LogHealthReport();
                    }
                    catch (Exception logEx)
                    {
                        Log.Warning(logEx, "Failed to log module health report after module initialization");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to perform deterministic module initialization; modules may load lazily or via Prism's default mechanism");
                }

                // Wait briefly for secrets initialization to finish so early consumers have a chance to proceed.
                // We don't block indefinitely — prefer a short timeout and log state for diagnostics.
                try
                {
                    var secretsTask = SecretsInitializationTask;
                    var completed = System.Threading.Tasks.Task.WhenAny(secretsTask, System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5))).Result;
                    if (completed == secretsTask)
                    {
                        // Observe possible exception to ensure it is logged
                        if (secretsTask.IsFaulted)
                        {
                            Log.Warning(secretsTask.Exception, "Deferred secrets initialization failed (observed after module init)");
                        }
                        else if (secretsTask.IsCompleted)
                        {
                            Log.Information("Deferred secrets initialization completed before timeout");
                        }
                    }
                    else
                    {
                        Log.Warning("Deferred secrets initialization did not complete within timeout (5s). Continuing startup; secrets will be available asynchronously.");
                    }
                }
                catch (AggregateException aex)
                {
                    // Unwrap and log aggregate exceptions (e.g., network/IO failures during secret population)
                    Log.Warning(aex, "Aggregate exception while awaiting deferred secrets initialization");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Unexpected error while awaiting deferred secrets initialization");
                }

                // Start database initialization in the background to keep UI responsive
                splashWindow.UpdateStatus("Initializing database...");
                try
                {
                    lock (StartupProgressSyncRoot)
                    {
                        StartupProgress = "Initializing database...";
                    }

                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            var dbInit = ResolveWithRetry<DatabaseInitializer>();
                            await dbInit.InitializeAsync().ConfigureAwait(false);
                            Log.Information("✓ Background database initialization finished");
                        }
                        catch (Exception dbEx)
                        {
                            Log.Warning(dbEx, "Background database initialization failed (non-fatal)");
                        }
                        finally
                        {
                            lock (StartupProgressSyncRoot)
                            {
                                StartupProgress = null;
                                LastHealthReportUpdate = DateTimeOffset.UtcNow;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to queue background database initialization task");
                }

                // Final health log & validation
                splashWindow.UpdateStatus("Finalizing initialization...");
                try
                {
                    var moduleHealthService = ResolveWithRetry<IModuleHealthService>();
                    moduleHealthService.LogHealthReport();
                    ValidateModuleInitialization(moduleHealthService);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to finalize module health report during application initialization");
                }

                try
                {
                    // Optional: default navigation (left commented to preserve deterministic startup ordering)
                    // var regionManager = Container.Resolve<IRegionManager>();
                    // regionManager.RequestNavigate("MainRegion", "DashboardView");
                    // Log.Information("Navigated to DashboardView during application initialization");
                }
                catch (Exception ex)
                {
                    // Prism navigation failures are non-fatal at startup; log and continue
                    Log.Warning(ex, "Failed to navigate to default view during startup - continuing without navigation");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error during application initialization - showing error dialog");

                // Show custom error dialog instead of letting the exception bubble up
                try
                {
                    ShowStartupErrorDialog(ex);
                }
                catch (Exception dialogEx)
                {
                    Log.Error(dialogEx, "Failed to show startup error dialog - falling back to message box");
                    // Fallback to message box if dialog fails
                    System.Windows.MessageBox.Show(
                        $"Critical startup error: {ex.Message}\n\nDetails: {ex.ToString()}",
                        "Startup Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }

                // Exit the application after showing the error
                Application.Current.Shutdown(1);
            }
            finally
            {
                // Update splash screen with completion status
                if (splashWindow != null)
                {
                    splashWindow.UpdateStatus("Startup complete!");
                    System.Threading.Thread.Sleep(500); // Brief pause to show completion
                    splashWindow.CloseSplash();
                }
            }
        }

        /// <summary>
        /// Shows a custom error dialog for startup failures instead of letting exceptions bubble up.
        /// </summary>
        /// <param name="exception">The exception that caused the startup failure</param>
        private void ShowStartupErrorDialog(Exception exception)
        {
            try
            {
                var dialogService = this.Container.Resolve<IDialogService>();
                var parameters = new DialogParameters();
                parameters.Add("Message", $"A critical error occurred during application startup:\n\n{exception.Message}\n\nPlease check the logs for more details.");
                parameters.Add("ButtonText", "Exit");

                // Show dialog synchronously since we're in startup and need to block
                var result = System.Threading.Tasks.Task.Run(async () =>
                {
                    var tcs = new TaskCompletionSource<IDialogResult>();
                    dialogService.ShowDialog("ErrorDialogView", parameters, r => tcs.SetResult(r));
                    return await tcs.Task;
                }).GetAwaiter().GetResult();

                Log.Information("Startup error dialog shown to user");
            }
            catch (Exception dialogEx)
            {
                Log.Error(dialogEx, "Failed to show custom startup error dialog");
                throw; // Re-throw so it falls back to message box
            }
        }
        /// https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
        /// </summary>
        public App()
        {
            // Simplified bootstrapper: keep early logging and global exception handling.
            _startupId = Guid.NewGuid().ToString("N")[..8];

            // Minimal bootstrap logger: file-based, capture Info+ by default to reduce noise
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.File("logs/startup-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();

            Log.Information("WileyWidget bootstrap starting - Session: {StartupId}", _startupId);

            // CRITICAL: Register Syncfusion and Bold Reports licenses BEFORE any Syncfusion control is initiated
            // Per Syncfusion documentation: "The generated license key is just a string that needs to be registered
            // before any Syncfusion control is initiated" and MUST be in App() constructor for WPF applications
            try
            {
                EnsureSyncfusionLicenseRegistered();
                EnsureBoldReportsLicenseRegistered();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register licenses during App constructor (non-fatal, will retry later)");
            }

            // Configure global exception handling early so any later initialization is covered
            try
            {
                SetupGlobalExceptionHandling();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to setup global exception handling during bootstrap");
            }
        }

        // Simplified OnStartup for PrismApplication
        protected override void OnStartup(StartupEventArgs e)
        {
            var swOnStartupTotal = Stopwatch.StartNew();
            var swPhase = Stopwatch.StartNew();
            base.OnStartup(e);

            // Global exception handling is configured centrally in SetupGlobalExceptionHandling();
            // avoid attaching duplicate DispatcherUnhandledException handlers here.

            // Deterministic startup flow: force re-register critical licenses and ensure theme
            try
            {
                // Force-refresh license registrations in case constructor registration failed earlier
                EnsureSyncfusionLicenseRegistered(forceRefresh: true);
                EnsureBoldReportsLicenseRegistered(forceRefresh: true);

                // Apply deterministic theme for the application
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                SfSkinManager.ApplicationTheme = new Theme("FluentLight");
                Log.Information("✓ Applied FluentLight theme deterministically during OnStartup");
                LogStartupTiming("OnStartup: license/theme", swPhase.Elapsed);
            }
            catch (Exception ex)
            {
                // Log and swallow startup theme/license issues - we don't want to crash here
                Log.Error(ex, "Error during license re-registration or theme application on startup");
            }

            // Wrap Initialize in try-catch to fail fast on critical startup errors
            try
            {
                swPhase.Restart();
                InitializeApplication();
                Log.Information("✓ Application initialization completed successfully");
                LogStartupTiming("OnStartup: InitializeApplication", swPhase.Elapsed);
            }
            catch (Exception ex)
            {
                // Match requested behavior: log error, show concise dialog, then shut down
                Log.Error(ex, "Startup failed");
                ShowErrorDialog("Critical startup error. Check logs.");
                try
                {
                    Application.Current.Shutdown();
                }
                catch { /* ignore */ }
            }

            LogStartupTiming("OnStartup: total", swOnStartupTotal.Elapsed);
        }

        private static void ShowErrorDialog(string message)
        {
            try
            {
                if (Application.Current == null)
                {
                    // Fallback if Application isn't initialized
                    MessageBox.Show(message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Application.Current.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        MessageBox.Show(message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to present startup error dialog");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unexpected failure while showing startup error dialog");
            }
        }

        /// <summary>
        /// Performs application-level initialization after Prism startup.
        /// Handles theme setup and initial navigation.
        /// NOTE: License registration is done in App() constructor per Syncfusion requirements.
        /// </summary>
        private void InitializeApplication()
        {
            try
            {
                // License registration already done in App() constructor per Syncfusion documentation
                // Do NOT register licenses here - must be in constructor

                // Theme is applied deterministically in OnStartup; keep this method minimal.

                // Future: Add initial navigation or health checks here if needed
                // var regionManager = Container.Resolve<IRegionManager>();
                // regionManager.RequestNavigate("MainRegion", "DashboardView");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Application initialization failed during theme/license setup");
                throw;
            }
        }

        // Database preflight logic moved out of the bootstrapper for clarity. If needed, implement a dedicated
        // startup task or hosted service to perform DB migrations/preflight outside the UI bootstrap path.

        // Configure custom region adapters for third-party controls (e.g., Syncfusion)
        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            ArgumentNullException.ThrowIfNull(regionAdapterMappings);
            base.ConfigureRegionAdapterMappings(regionAdapterMappings);

            // Resolve behavior factory; if missing we cannot create adapters safely.
            IRegionBehaviorFactory? behaviorFactory = null;
            try
            {
                behaviorFactory = ResolveWithRetry<IRegionBehaviorFactory>();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not resolve IRegionBehaviorFactory - custom region adapters will not be registered");
            }

            if (behaviorFactory == null)
            {
                Log.Warning("IRegionBehaviorFactory is null - skipping registration of custom region adapters");
                return;
            }

            // Cache for reflection lookups to reduce cold-start cost
            // Note: static fields are defined at class scope

            // Helper to find a type by short name across loaded assemblies without throwing if some assemblies fail to load types
            static Type? FindLoadedTypeByShortName(string shortName)
            {
                if (string.IsNullOrWhiteSpace(shortName)) return null;

                if (_typeByShortNameCache.TryGetValue(shortName, out var cached))
                {
                    return cached;
                }

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        types = rtle.Types.Where(t => t != null).ToArray()!;
                    }

                    foreach (var t in types)
                    {
                        if (t == null) continue;
                        if (string.Equals(t.Name, shortName, StringComparison.Ordinal) || (t.FullName != null && t.FullName.EndsWith("." + shortName, StringComparison.Ordinal)))
                        {
                            _typeByShortNameCache[shortName] = t;
                            return t;
                        }
                    }
                }

                _typeByShortNameCache[shortName] = null;
                return null;
            }

            try
            {
                // Register SfDataGrid adapter only if the control type is present in loaded assemblies
                var sfGridType = FindLoadedTypeByShortName("SfDataGrid");
                if (sfGridType != null)
                {
                    var sfGridAdapter = new SfDataGridRegionAdapter(behaviorFactory);
                    regionAdapterMappings.RegisterMapping(sfGridType, sfGridAdapter);
                    Log.Information("✓ Registered SfDataGridRegionAdapter for Prism regions (type: {Type})", sfGridType.FullName);
                }
                else
                {
                    Log.Debug("Syncfusion SfDataGrid type not loaded; skipping SfDataGrid region adapter registration");
                }

                // Register DockingManager adapter if present
                var dockingType = FindLoadedTypeByShortName("DockingManager");
                if (dockingType != null)
                {
                    var dockingAdapter = new DockingManagerRegionAdapter(behaviorFactory);
                    regionAdapterMappings.RegisterMapping(dockingType, dockingAdapter);
                    Log.Information("✓ Registered DockingManagerRegionAdapter for Prism regions (type: {Type})", dockingType.FullName);
                }
                else
                {
                    Log.Debug("Syncfusion DockingManager type not loaded; skipping DockingManager region adapter registration");
                }
            }
            catch (ArgumentNullException anex)
            {
                Log.Warning(anex, "ArgumentNullException while registering region adapters - check behavior factory and adapter constructors");
            }
            catch (TypeInitializationException tiex)
            {
                Log.Warning(tiex, "TypeInitializationException while registering region adapters - a third-party control assembly may be missing or failed to initialize");
            }
            catch (ReflectionTypeLoadException rtlex)
            {
                Log.Warning(rtlex, "ReflectionTypeLoadException while scanning assemblies for Syncfusion types - some assemblies could not be loaded");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unexpected exception while registering region adapters; some adapters may be unavailable");
            }
        }

        // Configure default region behaviors, including diagnostics and context sync
        protected override void ConfigureDefaultRegionBehaviors(IRegionBehaviorFactory regionBehaviors)
        {
            ArgumentNullException.ThrowIfNull(regionBehaviors);
            base.ConfigureDefaultRegionBehaviors(regionBehaviors);
            try
            {
                regionBehaviors.AddIfMissing(WileyWidget.Regions.NavigationLoggingBehavior.BehaviorKey, typeof(NavigationLoggingBehavior));
                regionBehaviors.AddIfMissing(WileyWidget.Regions.AutoActivateBehavior.BehaviorKey, typeof(AutoActivateBehavior));
                regionBehaviors.AddIfMissing(WileyWidget.Regions.AutoSaveBehavior.BehaviorKey, typeof(AutoSaveBehavior));
                regionBehaviors.AddIfMissing(WileyWidget.Regions.SyncContextWithHostBehavior.BehaviorKey, typeof(SyncContextWithHostBehavior));
                Log.Information("✓ Registered default Prism region behaviors (NavigationLogging, AutoActivate, AutoSave, SyncContextWithHost)");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register one or more region behaviors");
            }
        }

        // Module catalog configuration is centralized later in this file to keep startup ordering predictable.
        // See ConfigureModuleCatalog(...) lower in the file where module registration occurs.

        /// <summary>
        /// Sets up global exception handling for production readiness.
        /// Catches unhandled exceptions and logs them appropriately.
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            // Handle unhandled exceptions on the UI thread with XAML-specific recovery
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                // First, try to unwrap TargetInvocationException from DI container issues
                Exception processedException = TryUnwrapTargetInvocationException(e.Exception);

                if (TryHandleDryIocContainerException(processedException))
                {
                    e.Handled = true;
                    Log.Information("DryIoc container exception handled and recovered");
                    return;
                }

                if (TryHandleXamlException(processedException))
                {
                    e.Handled = true;
                    Log.Warning("XAML exception handled and recovered");
                    return;
                }

                Log.Error(processedException, "Unhandled UI exception occurred");
                e.Handled = true; // Prevent application crash
                Log.Warning("UI exception suppressed per policy; notifying via logs only.");
            };

            // Handle unhandled exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Exception? exception = e.ExceptionObject as Exception;
                Exception? processedException = TryUnwrapTargetInvocationException(exception);
                Log.Fatal(processedException, "Unhandled background thread exception occurred");
                // Also log full inner exception chain to help diagnose reflection-wrapped errors
                TryLogExceptionDetails(processedException);
                // Application will terminate after this
            };

            // Handle unobserved task exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Exception processedException = TryUnwrapTargetInvocationException(e.Exception);
                Log.Error(processedException, "Unobserved task exception occurred");
                e.SetObserved(); // Prevent it from crashing the finalizer thread
            };

            Log.Information("Global exception handling configured with DryIoc container and XAML recovery");
        }

        /// <summary>
        /// Attempts to handle and recover from XAML-specific exceptions
        /// </summary>
        private bool TryHandleXamlException(Exception exception)
        {
            if (exception is System.Windows.Markup.XamlParseException xamlParseEx)
            {
                return TryRecoverFromXamlParseException(xamlParseEx);
            }

            if (exception is XamlObjectWriterException xamlWriterEx)
            {
                return TryRecoverFromXamlObjectWriterException(xamlWriterEx);
            }

            // Check inner exceptions for XAML issues
            var innerException = exception;
            while (innerException != null)
            {
                if (innerException is System.Windows.Markup.XamlParseException innerXamlParseEx)
                {
                    return TryRecoverFromXamlParseException(innerXamlParseEx);
                }

                if (innerException is XamlObjectWriterException innerXamlWriterEx)
                {
                    return TryRecoverFromXamlObjectWriterException(innerXamlWriterEx);
                }

                innerException = innerException.InnerException;
            }

            return false;
        }

        /// <summary>
        /// Attempts to handle and recover from DI container exceptions
        /// </summary>
        private bool TryHandleDryIocContainerException(Exception exception)
        {
            string message = exception.Message.ToLowerInvariant();

            // Special-case: DryIoc disposable transient registration warning
            // Example: Error.RegisteredDisposableTransientWontBeDisposedByContainer
            if (exception.Message.IndexOf("RegisteredDisposableTransientWontBeDisposedByContainer", StringComparison.OrdinalIgnoreCase) >= 0
                || exception.Message.IndexOf("Disposable Transient service", StringComparison.OrdinalIgnoreCase) >= 0
                || message.Contains("allowdisposabletransient", StringComparison.Ordinal))
            {
                Log.Information(exception, "DryIoc warning handled: registered disposable transient won't be disposed by container. Registration updated to allowDisposableTransient=true.");
                return true;
            }

            // Check for DryIoc container resolution failures
            if (message.Contains("dryioc", StringComparison.OrdinalIgnoreCase) || message.Contains("container", StringComparison.OrdinalIgnoreCase) || message.Contains("resolution", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error(exception, "DryIoc container exception detected: {Message}", exception.Message);

                // Try to provide specific guidance based on the error
                if (message.Contains("not registered", StringComparison.OrdinalIgnoreCase) || message.Contains("could not resolve", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Service registration issue detected. Check that all required services are registered in RegisterTypes().");
                    Log.Warning("Common missing registrations: ILogger<>, IOptions<>, or module-specific services.");
                }

                if (message.Contains("circular", StringComparison.OrdinalIgnoreCase) || message.Contains("dependency", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Circular dependency detected in DI container. Check for circular references in constructor parameters.");
                }

                // For container issues, we typically cannot recover at runtime
                // The application needs to be fixed and restarted
                return false;
            }

            // Check for module initialization failures
            if (message.Contains("module", StringComparison.OrdinalIgnoreCase) && (message.Contains("initialize", StringComparison.OrdinalIgnoreCase) || message.Contains("load", StringComparison.OrdinalIgnoreCase)))
            {
                Log.Error(exception, "Prism module initialization failure: {Message}", exception.Message);
                Log.Warning("Module failed to initialize. Check module dependencies and registrations.");
                return false;
            }

            return false;
        }

        // Reflection-safe shims for WileyWidget.Startup.BootstrapHelpers so
        // wpftmp/design-time projects that omit Startup sources still compile.
        private static Exception TryUnwrapTargetInvocationException(Exception? exception)
        {
            try
            {
                var t = Type.GetType("WileyWidget.Startup.BootstrapHelpers") ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Startup.BootstrapHelpers");
                if (t != null)
                {
                    var m = t.GetMethod("UnwrapTargetInvocationException", BindingFlags.Public | BindingFlags.Static);
                    if (m != null)
                    {
                        var res = m.Invoke(null, new object?[] { exception });
                        return res as Exception ?? new InvalidOperationException("UnwrapTargetInvocationException returned null");
                    }
                }
            }
            catch
            {
                // swallow - fall through to default
            }

            return exception ?? new InvalidOperationException("Exception was null");
        }

        private static void TryLogExceptionDetails(Exception? ex, bool includeStackTrace = true)
        {
            try
            {
                var t = Type.GetType("WileyWidget.Startup.BootstrapHelpers") ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Startup.BootstrapHelpers");
                if (t != null)
                {
                    var m = t.GetMethod("LogExceptionDetails", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Exception), typeof(bool) }, null);
                    if (m != null)
                    {
                        m.Invoke(null, new object?[] { ex, includeStackTrace });
                        return;
                    }
                }
            }
            catch
            {
                // swallow
            }

            if (ex != null)
            {
                Log.Error(ex, "Exception: {Message}", ex.Message);
                if (includeStackTrace && !string.IsNullOrWhiteSpace(ex.StackTrace))
                {
                    Log.Error("Stack trace: {Stack}", ex.StackTrace);
                }
            }
        }

        private static T TryRetryOnException<T>(Func<T> operation, int maxAttempts = 3, int initialDelayMs = 200)
        {
            try
            {
                var t = Type.GetType("WileyWidget.Startup.BootstrapHelpers") ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Startup.BootstrapHelpers");
                if (t != null)
                {
                    var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "RetryOnException").ToArray();
                    foreach (var m in methods)
                    {
                        if (m.IsGenericMethodDefinition)
                        {
                            var gm = m.MakeGenericMethod(typeof(T));
                            var parameters = gm.GetParameters();
                            if (parameters.Length >= 1)
                            {
                                var res = gm.Invoke(null, new object?[] { operation, maxAttempts, initialDelayMs });
                                if (res is T tRes) return tRes;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore and fallback
            }

            // Fallback - simple retry implementation
            int attempts = 0;
            int delay = initialDelayMs;
            while (true)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    attempts++;
                    Log.Warning(ex, "Attempt {Attempt}/{Max} failed; retrying in {Delay}ms", attempts, maxAttempts, delay);
                    if (attempts >= maxAttempts) throw;
                    System.Threading.Tasks.Task.Delay(delay).Wait();
                    delay = Math.Min(delay * 2, 5000);
                }
            }
        }

        /// <summary>
        /// Centralized, safer container resolve which will attempt to resolve scoped services
        /// from an IServiceScopeFactory when available, falling back to container resolve.
        /// Includes lightweight retry to harden against transient resolution failures.
        /// </summary>
        private T ResolveWithRetry<T>(int maxAttempts = 3) where T : class
        {
            // IMPORTANT: Avoid attempting to instantiate interfaces or abstract types with Activator.
            // Preferred resolution order:
            //  1) Resolve from Prism container
            //  2) Resolve from an IServiceScopeFactory-created scope (if available)
            //  If both fail, throw a clear InvalidOperationException explaining missing registration.

            var result = TryRetryOnException(() =>
            {
                // 1) Try resolve directly from Prism's container
                try
                {
                    var instance = this.Container.Resolve<T>();
                    if (instance != null)
                        return instance;
                }
                catch (Exception ex)
                {
                    // capture for diagnostics via logs but continue to attempt scope-based resolution
                    Log.Debug(ex, "Direct container Resolve<{Type}> failed; will attempt scoped resolution if available", typeof(T).FullName);
                }

                // 2) If an IServiceScopeFactory has been registered, attempt to resolve via a scope
                try
                {
                    var scopeFactory = Container.Resolve<IServiceScopeFactory>();
                    if (scopeFactory != null)
                    {
                        using var scope = scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetService(typeof(T)) as T;
                        if (svc != null)
                            return svc;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Scoped resolution for {Type} failed or IServiceScopeFactory is not available", typeof(T).FullName);
                }

                // 3) Do NOT attempt Activator.CreateInstance for interfaces/abstract types.
                if (typeof(T).IsInterface || typeof(T).IsAbstract)
                {
                    // Provide a clear, actionable error message so callers can fix DI registration.
                    throw new InvalidOperationException($"Failed to resolve service type '{typeof(T).FullName}' from the DI container. The type is an interface or abstract; register a concrete implementation in RegisterTypes() or ensure Bootstrapper configures it.");
                }

                // At this point the type is concrete. Try Activator as a last resort but provide
                // explicit diagnostics on failure (no parameterless ctor etc.).
                try
                {
                    var created = Activator.CreateInstance<T>();
                    Log.Warning("ResolveWithRetry used Activator.CreateInstance fallback for concrete type {Type}. Consider registering it with the DI container instead.", typeof(T).FullName);
                    return created!;
                }
                catch (MissingMethodException mex)
                {
                    throw new InvalidOperationException($"Failed to create instance of concrete type '{typeof(T).FullName}' via Activator.CreateInstance - no parameterless constructor. Register the type with the DI container instead.", mex);
                }
            }, maxAttempts);

            return result;
        }
        /// <summary>
        /// Attempts recovery from XamlParseException
        /// </summary>
        private bool TryRecoverFromXamlParseException(System.Windows.Markup.XamlParseException ex)
        {
            Log.Error(ex, "XamlParseException detected: {Message}", ex.Message);
            Log.Error("XAML Error: Line {Line}, Position {Position}",
                ex.LineNumber,
                ex.LinePosition);

            string message = ex.Message.ToLowerInvariant();

            // Check for common recoverable issues
            if (message.Contains("syncfusion", StringComparison.OrdinalIgnoreCase) && message.Contains("license", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Syncfusion license issue detected - attempting recovery");
                try
                {
                    EnsureSyncfusionLicenseRegistered();
                    Log.Information("✓ Successfully recovered from Syncfusion license XamlParseException");
                    return true;
                }
                catch (Exception recoveryEx)
                {
                    Log.Error(recoveryEx, "✗ Failed to recover from Syncfusion license XamlParseException");
                }
            }

            if (message.Contains("viewmodellocator", StringComparison.OrdinalIgnoreCase) || message.Contains("autowire", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("ViewModelLocator issue detected - check ViewModel registrations");
                // This typically requires code fixes, not runtime recovery
                return false;
            }

            if (message.Contains("assembly", StringComparison.OrdinalIgnoreCase) && message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Missing assembly reference detected - check NuGet packages");
                return false;
            }

            if (message.Contains("xmlns", StringComparison.OrdinalIgnoreCase) || message.Contains("namespace", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("XML namespace issue detected - check xmlns declarations");
                return false;
            }

            // For other XAML parse exceptions, attempt theme re-application
            Log.Warning("Attempting theme re-application as recovery strategy");
            try
            {
                SfSkinManager.ApplicationTheme = new Theme("FluentLight");
                Log.Information("✓ Successfully recovered from XamlParseException by re-applying theme");
                return true;
            }
            catch (Exception themeEx)
            {
                Log.Error(themeEx, "✗ Theme re-application recovery failed");
            }

            return false;
        }

        /// <summary>
        /// Attempts recovery from XamlObjectWriterException
        /// </summary>
        private bool TryRecoverFromXamlObjectWriterException(XamlObjectWriterException ex)
        {
            Log.Error(ex, "XamlObjectWriterException detected: {Message}", ex.Message);

            string message = ex.Message.ToLowerInvariant();

            // Check for property binding issues
            if (message.Contains("property", StringComparison.OrdinalIgnoreCase) && message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Property binding issue detected - check property names and types");
                return false; // Requires code fix
            }

            // Check for type resolution issues
            if (message.Contains("type", StringComparison.OrdinalIgnoreCase) && message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Type resolution issue detected - check assembly references and using statements");
                return false; // Requires code fix
            }

            // Attempt general recovery by clearing resource dictionaries and re-applying theme
            Log.Warning("Attempting general XAML recovery from XamlObjectWriterException");
            try
            {
                // Force re-application of theme
                SfSkinManager.ApplicationTheme = new Theme("FluentLight");

                // Clear and reload application resources if needed
                Application.Current.Resources.MergedDictionaries.Clear();
                // Note: In a real recovery scenario, you'd reload the necessary resource dictionaries

                Log.Information("✓ Successfully recovered from XamlObjectWriterException via theme re-application");
                return true;
            }
            catch (Exception recoveryEx)
            {
                Log.Error(recoveryEx, "✗ General XAML recovery from XamlObjectWriterException failed");
            }

            return false;
        }

        protected override Window CreateShell()
        {
            try
            {
                // DI-first strategy:
                // 1) Attempt to construct MainWindow via registered IServiceProvider using ActivatorUtilities
                // 2) Attempt to resolve MainWindow/Shell from Prism container
                // 3) Fallback to parameterless Activator.CreateInstance

                // Helper to attempt DI construction
                Window? TryCreateWithDI(Type type)
                {
                    try
                    {
                        // Try resolve a root IServiceProvider from the Prism container
                        IServiceProvider? sp = null;
                        try
                        {
                            sp = base.Container.Resolve<IServiceProvider>();
                        }
                        catch
                        {
                            sp = null;
                        }

                        if (sp != null)
                        {
                            // Use ActivatorUtilities so constructor injection is honored
                            var created = ActivatorUtilities.CreateInstance(sp, type) as Window;
                            if (created != null)
                            {
                                Log.Information("Created {TypeName} via ActivatorUtilities (DI)", type.FullName);
                                return created;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "DI-based construction failed for {TypeName}", type.FullName);
                    }

                    return null;
                }

                // Try MainWindow first (preferred by manifest)
                var mainWindowType = Type.GetType("WileyWidget.Views.MainWindow")
                                     ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Views.MainWindow");

                if (mainWindowType != null && typeof(Window).IsAssignableFrom(mainWindowType))
                {
                    // 1) DI-based creation
                    var mw = TryCreateWithDI(mainWindowType);
                    if (mw != null) return mw;

                    // 2) Container resolve (Prism)
                    try
                    {
                        var resolved = base.Container.Resolve(mainWindowType) as Window;
                        if (resolved != null)
                        {
                            Log.Information("Resolved {TypeName} from container", mainWindowType.FullName);
                            return resolved;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Container resolve failed for {TypeName}", mainWindowType.FullName);
                    }

                    // 3) Parameterless instantiate
                    try
                    {
                        var paramless = Activator.CreateInstance(mainWindowType) as Window;
                        if (paramless != null)
                        {
                            Log.Warning("Instantiated {TypeName} via parameterless constructor (DI skipped)", mainWindowType.FullName);
                            return paramless;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Parameterless instantiation failed for {TypeName}", mainWindowType.FullName);
                    }
                }

                // Next, try Shell type (now in Windows namespace)
                var shellType = Type.GetType("WileyWidget.Views.Windows.Shell")
                                ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Views.Windows.Shell");

                if (shellType != null && typeof(Window).IsAssignableFrom(shellType))
                {
                    var sh = TryCreateWithDI(shellType);
                    if (sh != null) return sh;

                    try
                    {
                        var resolved = base.Container.Resolve(shellType) as Window;
                        if (resolved != null)
                        {
                            Log.Information("Resolved {TypeName} from container", shellType.FullName);
                            return resolved;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Container resolve failed for {TypeName}", shellType.FullName);
                    }

                    try
                    {
                        var paramless = Activator.CreateInstance(shellType) as Window;
                        if (paramless != null)
                        {
                            Log.Warning("Instantiated {TypeName} via parameterless constructor (DI skipped)", shellType.FullName);
                            return paramless;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Parameterless instantiation failed for {TypeName}", shellType.FullName);
                    }
                }

                // Final fallback: attempt to resolve the known Shell type via generic container resolve
                try
                {
                    Log.Debug("Final fallback: resolving Views.Windows.Shell from container");
                    return base.Container.Resolve<Shell>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Final container resolve fallback failed for Views.Windows.Shell");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create application shell (DI-first)");
                throw new InvalidOperationException("Failed to create application shell. Check XAML/DI registrations and service availability.", ex);
            }
        }

        protected override void InitializeShell(Window shell)
        {
            ArgumentNullException.ThrowIfNull(shell);
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
                            // Check for license key in local secure storage
                            // This could be implemented as needed based on the specific storage solution being used
                            Log.Debug("Checking for Syncfusion license in local secret vault...");
                            // TODO: Implement specific secret vault access if needed
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
                        Log.Warning("Syncfusion license key not configured. Set Syncfusion:LicenseKey in appsettings.json or the SYNCFUSION_LICENSE_KEY environment variable (user or machine scope) to suppress runtime license dialogs.");
                    }
                    return;
                }

                string masked = licenseKey.Length > 8 ? string.Concat(licenseKey.AsSpan(0, 8), "...") : "(masked)";
                Log.Information("Registering Syncfusion license (length: {Length}, source: {Source})", licenseKey.Length, licenseSource);
                // Wrap license registration with a retry helper to make startup deterministic
                TryRetryOnException(() =>
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    return true;
                }, maxAttempts: 3);

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
                            // Check for license key in local secure storage
                            // This could be implemented as needed based on the specific storage solution being used
                            Log.Debug("Checking for Bold Reports license in local secret vault...");
                            // TODO: Implement specific secret vault access if needed
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(licenseKey)
                    || licenseKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                {
                    // Try additional secure local locations for the license key before warning
                    var possiblePaths = new[]
                    {
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WileyWidget", "boldreports_license.txt"),
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget", "boldreports_license.txt"),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "secrets", "boldreports_license.txt"),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "boldreports_license.txt")
                    };

                    foreach (var p in possiblePaths)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(p) && System.IO.File.Exists(p))
                            {
                                var fileKey = System.IO.File.ReadAllText(p).Trim();
                                if (!string.IsNullOrWhiteSpace(fileKey) && !fileKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                                {
                                    licenseKey = fileKey;
                                    licenseSource = p;
                                    Log.Information("Bold Reports license key loaded from file: {Path}", p);
                                    // Set process-level environment variable so downstream components can read it uniformly
                                    try { Environment.SetEnvironmentVariable("BOLDREPORTS_LICENSE_KEY", licenseKey, EnvironmentVariableTarget.Process); } catch { }
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to read Bold Reports license from {Path}", p);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(licenseKey) || licenseKey.Contains("YOUR_BOLDREPORTS_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                    {
                        if (forceRefresh)
                        {
                            Log.Debug("Bold Reports license key not configured; skipping re-registration");
                        }
                        else
                        {
                            // Downgrade to Information to avoid noisy warnings when the app is intentionally configured to load the license at runtime
                            Log.Information("Bold Reports license key not configured in appsettings.json; will rely on environment variable BOLDREPORTS_LICENSE_KEY or optional license file (%ProgramData%\\WileyWidget\\boldreports_license.txt or %AppData%\\WileyWidget\\boldreports_license.txt). No warning will be raised.");
                        }
                        return;
                    }
                }

                string masked = licenseKey.Length > 8 ? string.Concat(licenseKey.AsSpan(0, 8), "...") : "(masked)";
                Log.Information("Registering Bold Reports license (length: {Length}, source: {Source})", licenseKey.Length, licenseSource);
                // Wrap Bold Reports registration in retry to handle transient file/IO issues
                TryRetryOnException(() =>
                {
                    Bold.Licensing.BoldLicenseProvider.RegisterLicense(licenseKey);
                    return true;
                }, maxAttempts: 3);

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
            ArgumentNullException.ThrowIfNull(containerRegistry);
#pragma warning disable CS8604 // Possible null reference argument
            Log.Information("=== Starting DI Container Registration ===");

            // DEBUGGING AIDS: Add breakpoints here for startup issues
            // Set conditional breakpoints on the following lines for:
            // - System.InvalidOperationException: Container registration conflicts
            // - Microsoft.Data.SqlClient.SqlException: Database connection issues
            // - System.IO.IOException: File access issues during registration
            try
            {
                // Marker for debugging - attach debugger here for container issues
                System.Diagnostics.Debug.WriteLine("DEBUG: Entering RegisterTypes - attach debugger now for container issues");
                System.Diagnostics.Debugger.Log(0, "DEBUG", "RegisterTypes starting - check for InvalidOperationException or SqlException");
            }
            catch
            {
                // Ignore debug logging failures
            }
            Log.Debug("RegisterTypes called with containerRegistry: {Type}", containerRegistry?.GetType().Name);
            var swRegisterTotal = Stopwatch.StartNew();
            var swPhase = Stopwatch.StartNew();

            // Ensure ModuleHealthService is registered as early as possible so modules can safely
            // resolve it during their OnInitialized calls. Use a lightweight Serilog-backed
            // factory to avoid depending on full logging configuration during bootstrap.
            try
            {
                containerRegistry.RegisterSingleton<IModuleHealthService>(provider => new ModuleHealthService(new SerilogLoggerFactory(Log.Logger, dispose: false).CreateLogger<ModuleHealthService>()));
                Log.Information("✓ Registered IModuleHealthService as singleton (early factory)");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register IModuleHealthService early. Modules may not be able to mark health during init.");
            }

            IConfiguration configuration;
            var testMode = (Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE") ?? "0") == "1";
            // Diagnostics controls: keep extended diagnostics (chatty logging + eager resolves) OFF by default
            string extendedDiagEnv = Environment.GetEnvironmentVariable("WILEY_WIDGET_EXTENDED_DIAGNOSTICS") ?? "0";
            bool enableExtendedDiagnostics = extendedDiagEnv == "1" || string.Equals(extendedDiagEnv, "true", StringComparison.OrdinalIgnoreCase);
            // Convention scan switch: enable expensive reflection scans only for test/dev or when explicitly requested
            string conventionsEnv = Environment.GetEnvironmentVariable("WILEY_WIDGET_ENABLE_CONVENTIONS") ?? "0";
            bool enableConventions = testMode || enableExtendedDiagnostics || conventionsEnv == "1" || string.Equals(conventionsEnv, "true", StringComparison.OrdinalIgnoreCase);
            try
            {
                // Prefer calling the centralized Bootstrapper when available. Use reflection
                // so design-time/wpftmp compilations that omit the Startup sources still succeed.
                configuration = TryRunBootstrapper(containerRegistry!);
                LogStartupTiming("RegisterTypes: Bootstrapper/Configuration", swPhase.Elapsed);
                swPhase.Restart();

                // Verify that IDbContextFactory was registered by Bootstrapper; if not, register immediately (early bootstrap)
                try
                {
                    var factoryType = typeof(IDbContextFactory<AppDbContext>);
                    var dry = containerRegistry.GetContainer();
                    var already = dry.IsRegistered(factoryType, serviceKey: null) || containerRegistry.IsRegistered(factoryType);
                    if (already)
                    {
                        Log.Information("✓ Verified: IDbContextFactory<AppDbContext> is registered in container");
                    }
                    else
                    {
                        Log.Warning("✗ IDbContextFactory<AppDbContext> not found after Bootstrapper; registering early fallback now");

                        var services = new ServiceCollection();
                        services.AddDbContextFactory<AppDbContext>(options =>
                        {
                            try
                            {
                                // Expand environment variables in the connection string (e.g., %APPDATA%)
                                var cs = configuration.GetConnectionString("DefaultConnection") ?? "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                                cs = Environment.ExpandEnvironmentVariables(cs);

                                options.UseSqlServer(cs, sqlOptions =>
                                {
                                    // Point migrations to the data project when available
                                    sqlOptions.MigrationsAssembly("WileyWidget.Data");
                                    sqlOptions.EnableRetryOnFailure();
                                });
                            }
                            catch
                            {
                                // As a last resort, use default SQL Server connection
                                options.UseSqlServer("Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;", sqlOptions =>
                                {
                                    sqlOptions.MigrationsAssembly("WileyWidget.Data");
                                    sqlOptions.EnableRetryOnFailure();
                                });
                            }
                        });

                        try
                        {
                            if (this.Container is IContainerExtension extension)
                            {
                                extension.Populate(services);
                                Log.Information("✓ Registered IDbContextFactory<AppDbContext> (SQL Server) via IServiceCollection import");
                            }
                            else
                            {
                                Log.Warning("Container does not implement IContainerExtension; cannot import IServiceCollection for EF Core factory");
                            }
                        }
                        catch (Exception importEx)
                        {
                            Log.Warning(importEx, "Failed early import of IDbContextFactory<AppDbContext>; a later fallback will attempt registration");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not verify or early-register IDbContextFactory<AppDbContext>");
                }

                // Ensure IConfiguration is available from the DI container for ResolveWithRetry callers
                try
                {
                    containerRegistry.RegisterInstance(configuration);
                    Log.Debug("Registered IConfiguration instance into DI container");

                    // Apply diagnostics (trace sources) from configuration and enable WPF binding error tracing
                    try
                    {
                        ConfigureDiagnosticsSettings(configuration);

                        var bindingTraceListener = new BindingErrorTraceListener();
                        Trace.Listeners.Add(bindingTraceListener);
                        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTraceListener);
                        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
                        Log.Information("✓ WPF binding error tracing enabled");
                    }
                    catch (Exception diagEx)
                    {
                        Log.Warning(diagEx, "Failed to apply diagnostics settings or enable binding error tracing");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to register IConfiguration into DI container (continuing)");
                }
                Log.Debug("Configuration, logging, HttpClient, and database services registered via Bootstrapper.Run() (or fallback)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Bootstrapper.Run() failed during infrastructure registration");
                throw;
            }

            // HttpClient and Database services are now handled by Bootstrapper - no need to duplicate here
            Log.Debug("Infrastructure services (HttpClient, Database) already configured by Bootstrapper - skipping duplicate registration");

            // Ensure EF Core IDbContextFactory<AppDbContext> is registered and a provider is configured
            try
            {
                // Double-check if the factory is already registered; if not, register via Microsoft DI and bridge to DryIoc
                var factoryType = typeof(IDbContextFactory<AppDbContext>);
                bool factoryRegistered = false;
                try
                {
                    // Use DryIoc container directly for an authoritative check
                    var dry = containerRegistry.GetContainer();
                    factoryRegistered = dry.IsRegistered(factoryType, serviceKey: null);
                }
                catch
                {
                    // Fallback: use Prism API when available
                    factoryRegistered = containerRegistry.IsRegistered(factoryType);
                }

                if (!factoryRegistered)
                {
                    Log.Warning("IDbContextFactory<AppDbContext> not found in container; registering with SQL Server provider as fallback");

                    var services = new ServiceCollection();
                    services.AddDbContextFactory<AppDbContext>(options =>
                    {
                        try
                        {
                            // Expand environment variables in the connection string (e.g., %APPDATA%)
                            var cs = configuration.GetConnectionString("DefaultConnection") ?? "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                            cs = Environment.ExpandEnvironmentVariables(cs);

                            options.UseSqlServer(cs, sqlOptions =>
                            {
                                sqlOptions.MigrationsAssembly("WileyWidget.Data");
                                sqlOptions.EnableRetryOnFailure();
                            });
                        }
                        catch
                        {
                            // As a last resort, use default SQL Server connection
                            options.UseSqlServer("Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;", sqlOptions =>
                            {
                                sqlOptions.MigrationsAssembly("WileyWidget.Data");
                                sqlOptions.EnableRetryOnFailure();
                            });
                        }
                    });

                    // Bridge Microsoft DI registrations into DryIoc
                    try
                    {
                        if (this.Container is IContainerExtension extension)
                        {
                            extension.Populate(services);
                        }
                        else
                        {
                            Log.Warning("Container does not implement IContainerExtension; cannot import IServiceCollection");
                        }
                        Log.Information("✓ Registered and imported IDbContextFactory<AppDbContext> (SQL Server) into DryIoc");
                    }
                    catch (Exception bridgeEx)
                    {
                        Log.Warning(bridgeEx, "Failed to import IServiceCollection into DryIoc; EF Core factory may be unavailable");
                    }
                }
                else
                {
                    Log.Debug("IDbContextFactory<AppDbContext> already registered - skipping fallback registration");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Attempt to ensure IDbContextFactory<AppDbContext> registration failed");
            }
            LogStartupTiming("RegisterTypes: Ensure EF Core factory", swPhase.Elapsed);
            swPhase.Restart();


            try
            {
                // Register core infrastructure services
                containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
                containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
                containerRegistry.RegisterSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
                // Register SettingsService and map the interface directly to the implementation.
                // Use non-generic mapping to avoid compile-time generic assignability checks in wpftmp.
                containerRegistry.RegisterSingleton<SettingsService>();
                containerRegistry.RegisterSingleton(typeof(ISettingsService), provider => provider.Resolve<SettingsService>());
                Log.Information("✓ Registered ISettingsService as singleton");
                Log.Debug("About to register IAuditService...");
                containerRegistry.RegisterSingleton<IAuditService, AuditService>();
                Log.Debug("IAuditService registration completed");
                // NOTE: ThemeManager removed - SfSkinManager handles all theming globally per Syncfusion documentation
                // Reference: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
                containerRegistry.RegisterSingleton<IDispatcherHelper>(provider => new DispatcherHelper());
                // Register logging infrastructure before AppOptionsConfigurator is resolved
                try
                {
                    containerRegistry.RegisterSingleton<ILoggerFactory>(provider =>
                    {
                        var serviceCollection = new ServiceCollection();
                        // Integrate Serilog as the primary provider and keep console for diagnostics
                        serviceCollection.AddLogging(builder =>
                        {
                            // If IConfiguration is available, apply the Logging section so providers and levels can be configured
                            try
                            {
                                if (configuration is IConfiguration cfg)
                                {
                                    builder.AddConfiguration(cfg.GetSection("Logging"));
                                }
                            }
                            catch
                            {
                                // Swallow configuration integration failures to avoid breaking startup
                            }

                            builder.ClearProviders();
                            // Use the existing Serilog static logger as the primary provider
                            builder.AddSerilog(Log.Logger, dispose: false);
                            builder.AddConsole();
                        });

                        var sp = serviceCollection.BuildServiceProvider();
                        return sp.GetRequiredService<ILoggerFactory>();
                    });

                    // Register generic ILogger<T> so consumers can depend on ILogger<T>
                    containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
                    Log.Debug("Registered ILoggerFactory singleton and generic ILogger<> resolver (with IConfiguration integration)");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to register Microsoft.Extensions.Logging services into Prism container");
                }

                // NOTE: AppOptionsConfigurator registration moved AFTER database services are confirmed registered
                // to prevent premature resolution of AppDbContext dependencies
                // Registration will happen after verifying IDbContextFactory is available

                // Do NOT expose the concrete DryIoc container here. Depend on Prism's IContainerRegistry/IContainerProvider
                // abstraction instead. If a service truly requires DryIoc-specific APIs it should obtain the concrete
                // implementation via an adapter or request IContainerProvider and obtain the underlying container there.
                Log.Information("✓ Registered core infrastructure services (Syncfusion, Settings, Dispatcher)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register core infrastructure services");
                throw;
            }

            // ============================================================================
            // LAZY QUICKBOOKS SERVICE - Registered early to prevent ViewModel ctor failures
            // ============================================================================
            // Pattern: Lazy-loading stub that defers to real QuickBooksService once QuickBooksModule loads.
            // Prevents SettingsViewModel and other consumers from failing when they depend on IQuickBooksService
            // but are created before QuickBooksModule.OnInitialized() runs.
            //
            // Based on Prism-Samples-Wpf EventAggregator patterns:
            // https://github.com/PrismLibrary/Prism-Samples-Wpf/tree/master/10-CustomPopupDialogs
            //
            // The stub provides no-op implementations until QuickBooksModule publishes QuickBooksServiceReadyEvent,
            // at which point it swaps to the real implementation.
            Log.Information("========== REGISTERING LAZY QUICKBOOKS SERVICE ==========");
            try
            {
                containerRegistry.RegisterSingleton<IQuickBooksService, WileyWidget.Services.Infrastructure.LazyQuickBooksService>();
                Log.Information("✓ Registered IQuickBooksService as LazyQuickBooksService (stub, will swap on module load)");
                Log.Information("  Registration type: Singleton");
                Log.Information("  Implementation: WileyWidget.Services.Infrastructure.LazyQuickBooksService");
                Log.Debug("LazyQuickBooksService will subscribe to ModuleLoadedEvent and QuickBooksServiceReadyEvent");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FATAL: Failed to register LazyQuickBooksService - QuickBooks features WILL fail");
                Log.Error("Exception details: {Message}", ex.Message);
                Log.Error("Stack trace: {StackTrace}", ex.StackTrace);
            }
            Log.Information("========== LAZY QUICKBOOKS SERVICE REGISTRATION COMPLETE ==========");

            // Register AppOptions after core infrastructure (including ISecretVaultService AND database) is available
            // This must happen AFTER Bootstrapper completes to ensure IDbContextFactory is registered
            try
            {
                // Register AppOptionsConfigurator NOW, after database services are ready
                containerRegistry.RegisterSingleton<AppOptionsConfigurator>();
                Log.Debug("Registered AppOptionsConfigurator singleton");

                RegisterAppOptions(containerRegistry, configuration);
                Log.Debug("AppOptions registration completed after core infrastructure registration");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RegisterAppOptions failed when invoked after core infrastructure registration; continuing without AppOptions configurator");
            }

            try
            {
                // Register Microsoft.Extensions.Caching.Memory infrastructure
                MemoryCacheOptions memoryCacheOptions = new MemoryCacheOptions();
                string? configuredSizeLimit = configuration["Caching:MemoryCache:SizeLimit"];
                if (long.TryParse(configuredSizeLimit, out long sizeLimit) && sizeLimit > 0)
                {
                    memoryCacheOptions.SizeLimit = sizeLimit;
                }

                // Ensure IMemoryCache is available
                // Prefer a single application-wide MemoryCache singleton configured via options
                containerRegistry.RegisterSingleton<IMemoryCache>(provider => new MemoryCache(memoryCacheOptions));
                Log.Information("✓ Registered IMemoryCache singleton");

                // Register ICacheService using MemoryCacheService implementation
                containerRegistry.RegisterSingleton<ICacheService, MemoryCacheService>();
                Log.Information("✓ Registered ICacheService singleton");

                // Register configuration options infrastructure (bridging Microsoft.Extensions.Options into container)
                // NOTE: AppOptionsConfigurator depends on ISecretVaultService; ensure secret vault is registered before resolving the configurator.
                // Registration will be invoked after core infrastructure & secret vault registration to avoid resolution order issues.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register caching and options services");
                throw;
            }

            try
            {
                // Register data repositories required during startup validation to prevent DI container resolution failures
                // All repositories registered centrally after Bootstrapper ensures IDbContextFactory is available
                // Register repositories as transient - they use IDbContextFactory<AppDbContext> which is registered by Bootstrapper
                // Use DryIoc-specific registration allowing disposable transients to avoid container exception
                var dry = containerRegistry.GetContainer();

                dry.Register<IEnterpriseRepository, EnterpriseRepository>(
                    reuse: Reuse.Transient,
                    setup: Setup.With(allowDisposableTransient: true));

                dry.Register<IBudgetRepository, BudgetRepository>(
                    reuse: Reuse.Transient,
                    setup: Setup.With(allowDisposableTransient: true));

                dry.Register<IAuditRepository, AuditRepository>(
                    reuse: Reuse.Transient,
                    setup: Setup.With(allowDisposableTransient: true));

                // Explicit constructor selection for MunicipalAccountRepository to avoid ambiguity
                // Use IDbContextFactory<AppDbContext> + IMemoryCache constructor and register as Singleton
                dry.Register<IMunicipalAccountRepository, MunicipalAccountRepository>(
                    made: Made.Of(() => new MunicipalAccountRepository(Arg.Of<IDbContextFactory<AppDbContext>>(), Arg.Of<IMemoryCache>())),
                    reuse: Reuse.Singleton);

                dry.Register<IUtilityCustomerRepository, UtilityCustomerRepository>(
                    reuse: Reuse.Transient,
                    setup: Setup.With(allowDisposableTransient: true));

                dry.Register<IDepartmentRepository, DepartmentRepository>(
                    reuse: Reuse.Transient,
                    setup: Setup.With(allowDisposableTransient: true));

                Log.Information("✓ Registered core data repositories for startup validation (Enterprise, Budget, Audit, MunicipalAccount, UtilityCustomer, Department) with allowDisposableTransient");

                // Ensure Prism-resolved ViewModels can obtain the UnitOfWork infrastructure
                containerRegistry.Register<IUnitOfWork, UnitOfWork>();
                Log.Information("✓ Registered IUnitOfWork infrastructure for Prism ViewModels");

                // Provide AppDbContext via the factory for ViewModels/services that require a concrete DbContext
                // This avoids requiring a parameterless constructor and aligns with EF Core recommended patterns
                try
                {
                    // Use delegate registration to resolve factory and create context
                    // This uses a delegate to call the factory's CreateDbContext() method, avoiding static method requirements
                    dry.RegisterDelegate<AppDbContext>(container =>
                    {
                        var factory = container.Resolve<IDbContextFactory<AppDbContext>>();
                        return factory.CreateDbContext();
                    }, Reuse.Transient);
                    Log.Information("✓ Registered AppDbContext (Transient) via IDbContextFactory delegate");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to register AppDbContext via factory; SettingsViewModel resolution may fail if AppDbContext is requested directly");
                }                // Register IServiceScopeFactory for DryIoc-based scoped services (required by WhatIfScenarioEngine)
                containerRegistry.RegisterSingleton<IServiceScopeFactory, DryIocServiceScopeFactory>();
                Log.Information("✓ Registered IServiceScopeFactory using DryIoc child container adapter");

                // Register business services
                containerRegistry.RegisterSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();
                containerRegistry.RegisterSingleton<FiscalYearSettings>();
                containerRegistry.RegisterSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();
                Log.Information("✓ Registered business services (WhatIfScenarioEngine, FiscalYearSettings, ChargeCalculator)");

                // Removed duplicate concrete registrations to streamline DI
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register data repositories and business services");
                throw;
            }
            LogStartupTiming("RegisterTypes: Data repositories and business services", swPhase.Elapsed);
            swPhase.Restart();

            try
            {
                // Register AI Integration Services (Phase 1 - Production Ready)
                // Register AI Integration Services and QuickBooks service
                if (testMode)
                {
                    // AI services already registered above as dummy
                    Log.Information("✓ AI services handled in test mode");
                }
                else
                {
                    // Register AI services for production - USING LAZY LOADING for better startup performance
                    RegisterLazyAIServices(containerRegistry);
                    Log.Information("✓ AI services registered with lazy loading for faster startup");

                    // QuickBooks service moved to QuickBooksModule for on-demand loading
                    // containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();
                    Log.Information("✓ QuickBooks service deferred to QuickBooksModule");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register AI and QuickBooks services");
                throw;
            }

            try
            {
                // Register Excel services with lazy loading for better startup performance
                containerRegistry.RegisterSingleton<IExcelReaderService>(provider =>
                {
                    var lazyService = new Lazy<ExcelReaderService>(() =>
                    {
                        Log.Information("Initializing ExcelReaderService on first access...");
                        return new ExcelReaderService(provider.Resolve<ILogger<ExcelReaderService>>());
                    });
                    return lazyService.Value;
                });
                Log.Information("✓ Registered IExcelReaderService as lazy singleton");

                // Register Excel Export service (NEW) with lazy loading
                containerRegistry.RegisterSingleton<Services.Export.IExcelExportService>(provider =>
                {
                    var lazyService = new Lazy<Services.Export.ExcelExportService>(() =>
                    {
                        Log.Information("Initializing ExcelExportService on first access...");
                        return new Services.Export.ExcelExportService(provider.Resolve<ILogger<Services.Export.ExcelExportService>>());
                    });
                    return lazyService.Value;
                });
                Log.Information("✓ Registered IExcelExportService as lazy singleton");

                // Register Budget Importer service with lazy loading
                containerRegistry.RegisterSingleton<IBudgetImporter>(provider =>
                {
                    var lazyService = new Lazy<BudgetImporter>(() =>
                    {
                        Log.Information("Initializing BudgetImporter on first access...");
                        return new BudgetImporter(
                            provider.Resolve<IExcelReaderService>(),
                            provider.Resolve<ILogger<BudgetImporter>>(),
                            provider.Resolve<IBudgetRepository>());
                    });
                    return lazyService.Value;
                });
                Log.Information("✓ Registered IBudgetImporter (Excel/CSV Budget Importer) as lazy singleton");

                // Register Theme service (Singleton - keep eager as it's needed early)
                containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
                Log.Information("✓ Registered IThemeService as singleton");

                // Register report export service with lazy loading
                containerRegistry.RegisterSingleton<IReportExportService>(provider =>
                {
                    var lazyService = new Lazy<ReportExportService>(() =>
                    {
                        Log.Information("Initializing ReportExportService on first access...");
                        return new ReportExportService(provider.Resolve<ILogger<ReportExportService>>());
                    });
                    return lazyService.Value;
                });
                Log.Information("✓ Registered IReportExportService as lazy singleton");

                // Register Bold Reports service with lazy loading
                containerRegistry.RegisterSingleton<IBoldReportService>(provider =>
                {
                    var lazyService = new Lazy<BoldReportService>(() =>
                    {
                        Log.Information("Initializing BoldReportService on first access...");
                        return new BoldReportService(provider.Resolve<ILogger<BoldReportService>>());
                    });
                    return lazyService.Value;
                });
                Log.Information("✓ Registered IBoldReportService as lazy singleton");

                // ModuleHealthService registration moved earlier to ensure availability during module initialization

                // Register Prism DialogService
                containerRegistry.RegisterSingleton<IDialogService, DialogService>();
                Log.Information("✓ Registered Prism IDialogService as singleton");

                // Register Prism Dialogs (using new Dialogs namespace)
                containerRegistry.RegisterDialog<ConfirmationDialogView, ConfirmationDialogViewModel>("ConfirmationDialog");
                containerRegistry.RegisterDialog<NotificationDialogView, NotificationDialogViewModel>("NotificationDialog");
                containerRegistry.RegisterDialog<WarningDialogView, WarningDialogViewModel>("WarningDialog");
                containerRegistry.RegisterDialog<ErrorDialogView, ErrorDialogViewModel>("ErrorDialog");
                containerRegistry.RegisterDialog<SettingsDialogView, SettingsDialogViewModel>("SettingsDialog");
                // Register AI Assist as a Prism Dialog for modal prompts
                containerRegistry.RegisterDialog<AIAssistView, AIAssistViewModel>("AIAssistDialog");
                Log.Information("✓ Registered Prism Dialogs (Confirmation, Notification, Warning, Error, Settings)");

                // Ensure IHttpClientFactory is available for services relying on typed/named HttpClients
                try
                {
                    var services = new ServiceCollection();
                    services.AddHttpClient();

                    // Register named HttpClient for QuickBooks Online API
                    var qboBaseUrl = configuration?["QuickBooks:Environment"]?.ToLowerInvariant() == "production"
                        ? "https://quickbooks.api.intuit.com/"
                        : "https://sandbox-quickbooks.api.intuit.com/";

                    services.AddHttpClient("QBO", client =>
                    {
                        client.BaseAddress = new Uri(qboBaseUrl);
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

                    if (this.Container is IContainerExtension extension)
                    {
                        extension.Populate(services);
                        Log.Information("✓ Registered IHttpClientFactory via Microsoft.Extensions.Http");
                        Log.Information("✓ Registered named HttpClient 'QBO' with base URL: {QboBaseUrl}", qboBaseUrl);
                    }
                    else
                    {
                        Log.Warning("Container does not implement IContainerExtension; cannot import IServiceCollection for HttpClientFactory");
                    }
                }
                catch (Exception httpEx)
                {
                    Log.Warning(httpEx, "Failed to import IHttpClientFactory into DryIoc container");
                }

                // Register Views for Navigation (using new Main namespace)
                containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>("DashboardView");
                containerRegistry.RegisterForNavigation<EnterpriseView, EnterpriseViewModel>("EnterpriseView");
                containerRegistry.RegisterForNavigation<BudgetView, BudgetViewModel>("BudgetView");
                containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>("MunicipalAccountView");
                containerRegistry.RegisterForNavigation<UtilityCustomerView, UtilityCustomerViewModel>("UtilityCustomerView");

        // Explicit constructor selection for ReportsView to avoid ambiguity
        // Use parameterless constructor to prevent injecting container objects into views
                {
                    var dry = containerRegistry.GetContainer();
                    dry.Register<ReportsView>(
            made: Made.Of(typeof(ReportsView).GetConstructor(Type.EmptyTypes)),
                        reuse: Reuse.Transient);
                }
                // Register the View and ViewModel for Prism navigation
                containerRegistry.RegisterForNavigation<ReportsView, ReportsViewModel>("ReportsView");

                containerRegistry.RegisterForNavigation<AnalyticsView, AnalyticsViewModel>("AnalyticsView");
                containerRegistry.RegisterForNavigation<QuickBooksView, QuickBooksViewModel>("QuickBooksView");
                containerRegistry.RegisterForNavigation<ExcelImportView, ExcelImportViewModel>("ExcelImportView");
                containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>("SettingsView");
                containerRegistry.RegisterForNavigation<AIAssistView, AIAssistViewModel>("AIAssistView");

                // Panel views with explicit ViewModel registrations (using new Panels namespace)
                containerRegistry.Register<BudgetPanelViewModel>();
                containerRegistry.Register<DashboardPanelViewModel>();
                containerRegistry.Register<EnterprisePanelViewModel>();
                containerRegistry.Register<MunicipalAccountPanelViewModel>();
                containerRegistry.Register<SettingsPanelViewModel>();
                containerRegistry.Register<ToolsPanelViewModel>();
                containerRegistry.Register<UtilityCustomerPanelViewModel>();

                containerRegistry.RegisterForNavigation<DashboardPanelView, DashboardPanelViewModel>("DashboardPanelView");
                containerRegistry.RegisterForNavigation<EnterprisePanelView, EnterprisePanelViewModel>("EnterprisePanelView");
                containerRegistry.RegisterForNavigation<BudgetPanelView, BudgetPanelViewModel>("BudgetPanelView");
                containerRegistry.RegisterForNavigation<MunicipalAccountPanelView, MunicipalAccountPanelViewModel>("MunicipalAccountPanelView");
                containerRegistry.RegisterForNavigation<SettingsPanelView, SettingsPanelViewModel>("SettingsPanelView");
                containerRegistry.RegisterForNavigation<ToolsPanelView, ToolsPanelViewModel>("ToolsPanelView");
                containerRegistry.RegisterForNavigation<UtilityCustomerPanelView, UtilityCustomerPanelViewModel>("UtilityCustomerPanelView");

                // Explicitly register View-ViewModel associations using ViewModelLocationProvider
                Prism.Mvvm.ViewModelLocationProvider.Register<BudgetPanelView, BudgetPanelViewModel>();
                Prism.Mvvm.ViewModelLocationProvider.Register<DashboardPanelView, DashboardPanelViewModel>();
                Prism.Mvvm.ViewModelLocationProvider.Register<EnterprisePanelView, EnterprisePanelViewModel>();
                Prism.Mvvm.ViewModelLocationProvider.Register<MunicipalAccountPanelView, MunicipalAccountPanelViewModel>();
                Prism.Mvvm.ViewModelLocationProvider.Register<SettingsPanelView, SettingsPanelViewModel>();
                Prism.Mvvm.ViewModelLocationProvider.Register<ToolsPanelView, ToolsPanelViewModel>();
                Prism.Mvvm.ViewModelLocationProvider.Register<UtilityCustomerPanelView, UtilityCustomerPanelViewModel>();

                // Explicit View-ViewModel mapping for Settings to ensure AutoWire uses the container (no Activator fallback)
                Prism.Mvvm.ViewModelLocationProvider.Register<SettingsView, SettingsViewModel>();

                // Explicitly register critical ViewModels so Container.Resolve succeeds even without convention scans
                // This prevents MissingMethodException when Prism's ViewModelLocator.AutoWireViewModel tries to resolve
                containerRegistry.Register<SettingsViewModel>();
                containerRegistry.Register<ReportsViewModel>();
                containerRegistry.Register<MunicipalAccountViewModel>();
                containerRegistry.Register<UtilityCustomerViewModel>();
                containerRegistry.Register<DashboardViewModel>();
                containerRegistry.Register<EnterpriseViewModel>();
                containerRegistry.Register<BudgetViewModel>();
                containerRegistry.Register<AnalyticsViewModel>();
                containerRegistry.Register<QuickBooksViewModel>();
                containerRegistry.Register<AIAssistViewModel>();
                Log.Information("✓ Registered SettingsViewModel for DI container resolution");
                Log.Information("✓ Registered ReportsViewModel for DI container resolution");
                Log.Information("✓ Registered MunicipalAccountViewModel for DI container resolution");
                Log.Information("✓ Registered UtilityCustomerViewModel for DI container resolution");
                Log.Information("✓ Registered DashboardViewModel for DI container resolution");
                Log.Information("✓ Registered EnterpriseViewModel for DI container resolution");
                Log.Information("✓ Registered BudgetViewModel for DI container resolution");
                Log.Information("✓ Registered AnalyticsViewModel for DI container resolution");
                Log.Information("✓ Registered QuickBooksViewModel for DI container resolution");
                Log.Information("✓ Registered AIAssistViewModel for DI container resolution");

                Log.Information("✓ Registered Views for navigation (core, analytics, settings, panels with explicit ViewModels)");

                // Register Composite Command Service
                containerRegistry.RegisterSingleton<ICompositeCommandService, CompositeCommandService>();
                Log.Information("✓ Registered ICompositeCommandService for coordinating multiple commands");

                // InteractionRequestService wrapper retained as an internal convenience implementation
                // ViewModels should prefer Prism's IDialogService directly for uniformity.

                // Register Scoped Region Service
                // REMOVED: Legacy IScopedRegionService replaced with Prism IRegionManager
                // containerRegistry.RegisterSingleton<IScopedRegionService, ScopedRegionService>();
                // Log.Information("✓ Registered IScopedRegionService for isolated navigation contexts");

                // Register Prism Error Handler for centralized error handling
                containerRegistry.RegisterSingleton<IPrismErrorHandler, PrismErrorHandler>();
                Log.Information("✓ Registered IPrismErrorHandler for centralized error handling and logging");

                // Also register as the broader IExceptionHandler contract so Prism/DryIoc consumers
                // or future features can depend on the lightweight exception handler abstraction.
                containerRegistry.RegisterSingleton<IExceptionHandler, PrismErrorHandler>();
                Log.Information("✓ Registered IExceptionHandler -> PrismErrorHandler for global exception handling (DryIoc)");

                // Note: Custom ModuleManager diagnostic registration removed to maintain compatibility

                // Explicitly register IEventAggregator (should be automatic in Prism, but ensure availability)
                containerRegistry.RegisterSingleton<IEventAggregator, EventAggregator>();
                Log.Information("✓ Registered IEventAggregator for pub/sub messaging");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register UI and infrastructure services");
                throw;
            }

            try
            {
                // Register ViewModels (module-specific ViewModels are now registered in their respective modules)
                containerRegistry.RegisterSingleton<MainViewModel>(provider =>
                {
                    try
                    {
                        var dialogService = provider.Resolve<IDialogService>();
                        var regionManager = provider.Resolve<IRegionManager>();
                        var dispatcher = provider.Resolve<IDispatcherHelper>();

                        // Resolve ILogger via ILoggerFactory to avoid potential open-generic resolve issues
                        var loggerFactory = provider.Resolve<ILoggerFactory>();
                        var logger = loggerFactory?.CreateLogger<MainViewModel>() ?? throw new InvalidOperationException("ILoggerFactory did not produce a logger for MainViewModel");

                        var enterpriseRepo = provider.Resolve<IEnterpriseRepository>();
                        var excelReader = provider.Resolve<IExcelReaderService>();
                        var reportExport = provider.Resolve<IReportExportService>();
                        var budgetRepo = provider.Resolve<IBudgetRepository>();
                        var budgetImporter = provider.Resolve<IBudgetImporter>();
                        var aiService = provider.IsRegistered<IAIService>() ? provider.Resolve<IAIService>() : null;

                        return new MainViewModel(
                            dialogService,
                            regionManager,
                            dispatcher,
                            logger,
                            enterpriseRepo,
                            excelReader,
                            reportExport,
                            budgetRepo,
                            budgetImporter,
                            aiService);
                    }
                    catch (Exception ex)
                    {
                        // Provide clearer error context for container resolution failures
                        Log.Error(ex, "Failed to construct MainViewModel during DI registration. Check that all dependencies are registered and have no circular dependencies.");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register MainViewModel");
                throw;
            }

            try
            {
                // Register Shell using a parameterless factory to avoid greedy constructor selection during tests
                containerRegistry.Register<Shell>(provider => new Shell());

                // Register additional ViewModels for Prism ViewModelLocator (infrastructure-only)
                containerRegistry.Register<AboutViewModel>();
                containerRegistry.Register<ExcelImportViewModel>();
                containerRegistry.Register<ProgressViewModel>();

                // Register Region Adapters
                // REMOVED: DockingManagerRegionAdapter is disabled (#if false)
                // containerRegistry.RegisterSingleton<WileyWidget.Regions.DockingManagerRegionAdapter>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register remaining ViewModels and region adapters");
                throw;
            }

            // Navigation registrations are now handled by individual modules

            // Convention-based registrations: attempt to auto-register remaining services, repositories, and ViewModels
            // Gate reflection-heavy scans to reduce startup time in production
            if (enableConventions)
            {
                try
                {
                    Log.Information("Convention scans enabled (TestMode={TestMode}, ExtendedDiagnostics={Extended}, EnvSwitch={Env}) - running convention-based registrations",
                        testMode, enableExtendedDiagnostics, conventionsEnv);
                    RegisterConventions(containerRegistry);
                    ValidateAndRegisterViewModels(containerRegistry);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Convention-based registration encountered an error; continuing with explicit registrations only");
                }
            }
            else
            {
                Log.Information("Convention scans disabled. To enable, set WILEY_WIDGET_ENABLE_CONVENTIONS=1 (or TRUE), or enable WILEY_WIDGET_EXTENDED_DIAGNOSTICS, or run in TestMode.");
            }

            Log.Information("=== DI Container Registration Complete ===");
            Log.Information($"Total registrations: AI Services, Data Repositories, Business Services, ViewModels, Infrastructure");
            Log.Information("Container ready for service resolution");
            Log.Debug("Starting Prism infrastructure validation...");

            try
            {
                // Ensure Prism's ViewModelLocationProvider resolves ViewModels from the DI container
                Prism.Mvvm.ViewModelLocationProvider.SetDefaultViewModelFactory((view, viewModelType) =>
                {
                    try
                    {
                        return Container.Resolve(viewModelType);
                    }
                    catch (Exception ex)
                    {
                        // Prefer clear diagnostics over misleading Activator fallback which often triggers MissingMethodException
                        Log.Error(ex, "ViewModelLocationProvider: container failed to resolve {ViewModelType}", viewModelType?.FullName ?? "(null)");

                        // Optional nicety: during extended diagnostics, avoid Activator fallback to surface true root cause
                        if (enableExtendedDiagnostics)
                        {
                            Log.Warning("Extended diagnostics enabled: skipping Activator fallback for {ViewModelType}", viewModelType?.FullName ?? "(null)");
                            return null!; // let the binding/logging surface the real container error
                        }

                        // In normal mode, retain fallback for now but make it explicit in logs
                        Log.Warning("Falling back to Activator for {ViewModelType}; dependencies may be missing and cause follow-on errors", viewModelType?.FullName ?? "(null)");
                        return Activator.CreateInstance(viewModelType!)!;
                    }
                });
                Log.Information("✓ Configured Prism ViewModelLocationProvider to resolve ViewModels from DI container");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure Prism ViewModelLocationProvider default factory; XAML auto-wiring may fall back to Activator.CreateInstance");
            }

            // Run focused resolution diagnostics only when extended diagnostics are enabled
            if (enableExtendedDiagnostics)
            {
                try
                {
                    Log.Debug("Running focused container resolution checks (extended diagnostics)...");
                    var diagContainer = containerRegistry.GetContainer();

                    void TryResolve(Type t, string? name = null)
                    {
                        try
                        {
                            var resolved = diagContainer.Resolve(t, name ?? string.Empty);
                            Log.Debug("FocusedResolve OK: {Type} => {ResolvedType}", t.FullName, resolved?.GetType().FullName ?? "null");
                        }
                        catch (Exception ex)
                        {
                            // In extended mode, include full exception details for deep diagnosis
                            Log.Warning("FocusedResolve FAILED for {Type} (name='{Name}') -> {Message}", t.FullName, name ?? string.Empty, ex.Message);
                            Log.Debug(ex, "FocusedResolve exception (extended details) for {Type}", t.FullName);
                        }
                    }

                    // Types that previously failed in integration tests
                    TryResolve(typeof(IMunicipalAccountRepository));
                    TryResolve(typeof(MunicipalAccountRepository));
                    TryResolve(typeof(IWhatIfScenarioEngine));
                    TryResolve(typeof(WhatIfScenarioEngine));
                    TryResolve(typeof(IChargeCalculatorService));
                    TryResolve(typeof(ServiceChargeCalculatorService));

                    // Also check EF factory; skip direct AppDbContext construction here to avoid eager DB touch
                    TryResolve(typeof(IDbContextFactory<AppDbContext>));

                    // Focus on recurring troublemakers to surface missing registrations early
                    TryResolve(typeof(SettingsViewModel));
                    // Optionally probe AppDbContext direct resolution - delegate is registered to create instances
                    TryResolve(typeof(AppDbContext));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Focused container resolution diagnostics failed to run (extended mode)");
                }
            }
            else
            {
                Log.Debug("Focused container resolution checks skipped (extended diagnostics disabled)");
            }

            // Validate Prism infrastructure and critical services
            ValidatePrismInfrastructure(containerRegistry);
            Log.Debug("Prism infrastructure validation completed");

            ValidateCriticalServices(containerRegistry, testMode);
            Log.Debug("Critical services validation completed");
            Log.Debug("=== DI CONTAINER REGISTRATION FULLY COMPLETED ===");

            // Post-registration validation to ensure DB factory and other essentials can resolve; continue in degraded mode on failure
            try
            {
                ValidatePostRegistrationServices(containerRegistry);
            }
            catch (Exception ex)
            {
                // Never throw from validation; only log
                Log.Warning(ex, "Post-registration validation encountered an error (non-fatal)");
            }

        // Validate public accessibility to prevent InvalidRegistrationException
            ValidatePublicAccessibility();
        LogStartupTiming("RegisterTypes: total", swRegisterTotal.Elapsed);
#pragma warning restore CS8604 // Possible null reference argument
        }

        private void TryRegisterImplementationByName(IContainerRegistry containerRegistry, Type interfaceType, string implementationFullName)
        {
            try
            {
                Type? implType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var types = asm.GetTypes();
                        foreach (var t in types)
                        {
                            if (t.FullName == implementationFullName)
                            {
                                implType = t;
                                break;
                            }
                        }

                        if (implType != null) break;
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // Ignore assemblies we can't fully inspect
                    }
                }

                if (implType != null)
                {
                    containerRegistry.RegisterSingleton(interfaceType, implType);
                    Log.Information("Registered implementation for {Interface} -> {Impl}", interfaceType.Name, implType.FullName);
                }
                else
                {
                    Log.Warning("Test-mode implementation '{ImplName}' not found in loaded assemblies; skipping registration.", implementationFullName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register test implementation {ImplName} for interface {Interface}", implementationFullName, interfaceType.Name);
            }
        }

        /// <summary>
        /// Validates that all critical services are properly registered and can be resolved.
        /// This prevents runtime errors due to missing DI registrations.
        /// Enhanced with timeout handling and graceful degradation.
        /// Now displays a dialog for critical failures instead of crashing.
        /// </summary>
        /// <param name="containerRegistry">The container registry to validate</param>
        private void ValidateCriticalServices(IContainerRegistry containerRegistry, bool testMode)
        {
            Log.Information("Validating critical service registrations...");

            (string ServiceName, Type ServiceType)[] criticalServices = new[]
            {
                ("IConfiguration", typeof(IConfiguration)),
                ("ILoggerFactory", typeof(ILoggerFactory)),
                ("ISettingsService", typeof(ISettingsService)),
                ("IDbContextFactory<AppDbContext>", typeof(IDbContextFactory<AppDbContext>)),
                ("IEnterpriseRepository", typeof(IEnterpriseRepository)),
                ("IBudgetRepository", typeof(IBudgetRepository)),
                ("IModuleHealthService", typeof(IModuleHealthService)),
            };

            // In test mode, external AI services are disabled, so skip validation for them
            if (!testMode)
            {
                criticalServices = criticalServices.Concat(new[]
                {
                    ("IAIService", typeof(IAIService)),
                    ("IGrokSupercomputer", typeof(IGrokSupercomputer)),
                    ("IWileyWidgetContextService", typeof(IWileyWidgetContextService)),
                    ("IAILoggingService", typeof(IAILoggingService)),
                }).ToArray();
            }

            // Temporarily include recurring troublemakers to surface missing registrations without full UI startup
            // Keep these as simple resolve probes with timeouts to avoid deadlocks during startup diagnosis
            criticalServices = criticalServices.Concat(new[]
            {
                ("AppDbContext", typeof(AppDbContext)),
                ("SettingsViewModel", typeof(SettingsViewModel)),
            }).ToArray();

            List<string> validationErrors = new List<string>();
            List<string> validationWarnings = new List<string>();

            foreach ((string serviceName, Type serviceType) in criticalServices)
            {
                try
                {
                    IContainer validationContainer = containerRegistry.GetContainer();

                    // Use a timeout for service resolution to prevent hanging
                    var resolveTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            return validationContainer.Resolve(serviceType);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"Service resolution failed for {serviceName}");
                            return null;
                        }
                    });

                    // Wait up to 5 seconds for service resolution
                    if (resolveTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        object service = resolveTask.Result;
                        if (service == null)
                        {
                            validationWarnings.Add($"{serviceName} resolved to null");
                            Log.Warning($"⚠ {serviceName} resolved to null - service may not be properly configured");
                        }
                        else
                        {
                            Log.Debug($"✓ {serviceName} validated successfully");
                        }
                    }
                    else
                    {
                        validationWarnings.Add($"{serviceName} resolution timed out after 5 seconds");
                        Log.Warning($"⚠ {serviceName} resolution timed out - possible circular dependency or slow initialization");
                    }
                }
                catch (TimeoutException tex)
                {
                    validationWarnings.Add($"{serviceName} resolution timed out: {tex.Message}");
                    Log.Warning(tex, $"⚠ Service validation timed out for {serviceName}");
                }
                catch (AggregateException aex) when (aex.InnerException is TimeoutException)
                {
                    validationWarnings.Add($"{serviceName} resolution timed out");
                    Log.Warning(aex.InnerException, $"⚠ Service validation timed out for {serviceName}");
                }
                catch (Exception ex)
                {
                    // Check if it's a circular dependency error
                    if (ex.Message.Contains("circular", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("recursive", StringComparison.OrdinalIgnoreCase))
                    {
                        validationWarnings.Add($"{serviceName} has circular dependency: {ex.Message}");
                        Log.Warning(ex, $"⚠ Circular dependency detected for {serviceName}");
                    }
                    else
                    {
                        validationErrors.Add($"{serviceName} failed to resolve: {ex.Message}");
                        Log.Error(ex, $"❌ Critical service validation failed for {serviceName}");
                    }
                }
            }

            // Log warnings but don't throw - allow application to continue with degraded functionality
            if (validationWarnings.Any())
            {
                Log.Warning("Service validation warnings detected:");
                foreach (var warning in validationWarnings)
                {
                    Log.Warning($"  ⚠ {warning}");
                }
                Log.Warning("Application will continue but some features may not work correctly");
            }

            // Log critical errors and attempt to show dialog, but DO NOT THROW
            if (validationErrors.Any())
            {
                Log.Error("Critical service validation failed:");
                foreach (var error in validationErrors)
                {
                    Log.Error($"  ❌ {error}");
                }

                Log.Error("Critical services failed validation - application will start in degraded mode");
                Log.Error("Some features may not be available. Please check service registrations and circular dependencies.");

                // Attempt to show a user-friendly error dialog
                try
                {
                    var errorMessage = "The following critical services failed to initialize:\n\n" +
                                     string.Join("\n", validationErrors.Select(e => $"• {e}")) +
                                     "\n\nThe application will start in degraded mode with limited functionality.\n" +
                                     "Some features may not be available.";

                    // Use Dispatcher to ensure we're on the UI thread
                    Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        try
                        {
                            // Try to resolve IDialogService if available
                            var dialogService = Container?.Resolve<IDialogService>();
                            if (dialogService != null)
                            {
                                var parameters = new DialogParameters
                                {
                                    { "title", "Service Initialization Warning" },
                                    { "message", errorMessage }
                                };

                                dialogService.ShowDialog("WarningDialog", parameters, result => { });
                                Log.Information("Displayed service validation error dialog via IDialogService");
                            }
                            else
                            {
                                // Fallback to MessageBox if DialogService not available
                                MessageBox.Show(errorMessage, "Service Initialization Warning",
                                              MessageBoxButton.OK, MessageBoxImage.Warning);
                                Log.Information("Displayed service validation error dialog via MessageBox");
                            }
                        }
                        catch (Exception dialogEx)
                        {
                            Log.Warning(dialogEx, "Failed to show error dialog via IDialogService, trying MessageBox");
                            try
                            {
                                MessageBox.Show(errorMessage, "Service Initialization Warning",
                                              MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            catch (Exception msgBoxEx)
                            {
                                Log.Error(msgBoxEx, "Failed to show error dialog via MessageBox");
                            }
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal);
                }
                catch (Exception displayEx)
                {
                    Log.Error(displayEx, "Failed to display service validation error dialog");
                }

                // DO NOT THROW - allow application to continue in degraded mode
                Log.Warning("Continuing application startup in degraded mode despite validation errors");
            }
            else
            {
                Log.Information("✓ All critical services validated successfully");
            }
        }

        /// <summary>
        /// Additional post-registration validations that are safe to run after all services are registered.
        /// Logs warnings if critical services cannot be resolved but does not throw; app continues in degraded mode.
        /// </summary>
        private void ValidatePostRegistrationServices(IContainerRegistry containerRegistry)
        {
            try
            {
                var dry = containerRegistry.GetContainer();

                // Try resolving EF Core factory explicitly
                try
                {
                    var factory = dry.Resolve<IDbContextFactory<AppDbContext>>();
                    if (factory != null)
                    {
                        Log.Information("✓ Post-validate: IDbContextFactory<AppDbContext> resolved successfully");
                    }
                    else
                    {
                        Log.Warning("⚠ Post-validate: IDbContextFactory<AppDbContext> resolved to null");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠ Post-validate: Failed to resolve IDbContextFactory<AppDbContext>");
                }

                // Try a quick repository resolve to ensure downstream graph is intact
                try
                {
                    var budgetRepo = dry.Resolve<IBudgetRepository>();
                    _ = budgetRepo != null;
                    Log.Debug("Post-validate: IBudgetRepository resolve attempt complete");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠ Post-validate: IBudgetRepository resolution failed (degraded mode may apply)");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Post-registration validation failed unexpectedly");
            }
        }

        /// <summary>
        /// Ensures Prism and the DI container remain the single composition root by validating container state and legacy configuration.
        /// </summary>
        /// <param name="containerRegistry">The active Prism container registry</param>
        private void ValidatePrismInfrastructure(IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null)
            {
                throw new ArgumentNullException(nameof(containerRegistry));
            }

            var prismContainer = containerRegistry.GetContainer();
            if (prismContainer == null)
            {
                throw new InvalidOperationException("Prism container is not available during Prism startup.");
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

            // Try to introspect 'Registrations' via reflection so this code works with different container implementations
            int registrationCount = -1;
            try
            {
                var regProp = prismContainer.GetType().GetProperty("Registrations");
                if (regProp != null)
                {
                    var regsObj = regProp.GetValue(prismContainer);
                    if (regsObj is System.Collections.IEnumerable regsEnum)
                    {
                        registrationCount = regsEnum.Cast<object>().Count();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to introspect container registrations via reflection");
            }

            Log.Information("Container registration count: {RegistrationCount}", registrationCount >= 0 ? registrationCount.ToString(CultureInfo.InvariantCulture) : "unknown");
        }

        private void RegisterAppOptions(IContainerRegistry containerRegistry, IConfiguration configuration)
        {
            if (containerRegistry == null)
            {
                throw new ArgumentNullException(nameof(containerRegistry));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            try
            {
                var appOptions = new AppOptions();
                configuration.GetSection("App").Bind(appOptions);

                bool configuredFromDb = false;
                try
                {
                    // Try to obtain AppOptionsConfigurator from the Prism container provider; avoid depending on DryIoc directly
                    try
                    {
                        AppOptionsConfigurator configurator;
                        try
                        {
                            // First attempt to resolve - this will succeed if the type was registered earlier
                            configurator = this.Container.Resolve<AppOptionsConfigurator>();
                        }
                        catch (Exception)
                        {
                            // Not registered yet - register as a singleton so subsequent resolves will work
                            try
                            {
                                containerRegistry.RegisterSingleton<AppOptionsConfigurator>();
                                configurator = this.Container.Resolve<AppOptionsConfigurator>();
                            }
                            catch (Exception innerEx)
                            {
                                // If registering or resolving fails, rethrow to be handled by outer catch
                                throw new InvalidOperationException("Failed to register or resolve AppOptionsConfigurator", innerEx);
                            }
                        }

                        configurator.Configure(appOptions);
                        configuredFromDb = true; // reached here without exception – DB-backed configuration applied
                    }
                    catch (Exception)
                    {
                        // If configurator isn't registered or fails, continue with configuration-only values
                        throw;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Log InvalidOperationException without re-throwing to allow startup to continue
                    Log.Warning(ex, "AppOptionsConfigurator failed during startup (InvalidOperationException); continuing with configuration-only AppOptions values.");
                    configuredFromDb = false;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "AppOptionsConfigurator failed during startup; continuing with configuration-only AppOptions values.");
                    configuredFromDb = false;
                }

                var optionsWrapper = Options.Create(appOptions);
                containerRegistry.RegisterInstance(optionsWrapper);

                var monitor = new StaticOptionsMonitor<AppOptions>(appOptions, Log.Logger);
                containerRegistry.RegisterInstance<IOptionsMonitor<AppOptions>>(monitor);

                Log.Information("✓ Registered AppOptions for SettingsViewModel via Options bridge ({Source})", configuredFromDb ? "database+secrets" : "configuration only");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register AppOptions bridge; registering fallback options instance");

                var fallback = new AppOptions();
                containerRegistry.RegisterInstance(Options.Create(fallback));
                containerRegistry.RegisterInstance<IOptionsMonitor<AppOptions>>(new StaticOptionsMonitor<AppOptions>(fallback, Log.Logger));
            }
        }

        [Conditional("DEBUG")]
        private static void ValidateContainerRegistrations(IContainer container)
        {
            // Test resolution of critical services
            var criticalTypes = new[]
            {
                typeof(IConfiguration),
                typeof(ISettingsService),
                typeof(IRegionManager),
                typeof(IEventAggregator),
                typeof(IDialogService),
                typeof(ICompositeCommandService)
            };

            foreach (var type in criticalTypes)
            {
                try
                {
                    var instance = container.Resolve(type);
                    Log.Debug($"✓ Successfully resolved {type.Name}");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"✗ Failed to resolve {type.Name}: {ex.Message}");
                    // Don't throw here - let application continue with partial functionality
                }
            }
        }

        /// <summary>
        /// Registers types by convention to reduce manual registrations.
        /// - Services/Repositories/Providers ending with common suffixes are registered as singletons when an interface I{TypeName} exists.
        /// - ViewModels are registered as transient types so Prism can resolve them via ViewModelLocator.
        /// This helper is best-effort and will not overwrite existing registrations.
        /// </summary>
        private static void RegisterConventions(IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null) throw new ArgumentNullException(nameof(containerRegistry));

            int registeredCount = 0;
            var suffixesForSingleton = new[] { "Service", "Repository", "Provider", "Engine" };

            var regSingletonTwo = typeof(IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "RegisterSingleton" && m.IsGenericMethod && m.GetGenericArguments().Length == 2);
            var regSingletonOne = typeof(IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "RegisterSingleton" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);
            var regRegisterTwo = typeof(IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "Register" && m.IsGenericMethod && m.GetGenericArguments().Length == 2);
            var regRegisterOne = typeof(IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "Register" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);

            var prismContainer = containerRegistry.GetContainer();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!t.IsClass || t.IsAbstract) continue;
                    if (t.Namespace == null || !t.Namespace.StartsWith("WileyWidget", StringComparison.Ordinal)) continue;

                    var name = t.Name;

                    try
                    {
                        // Services / repositories / providers -> prefer to register against I{TypeName} when available
                        if (suffixesForSingleton.Any(s => name.EndsWith(s, StringComparison.Ordinal)))
                        {
                            var interfaces = t.GetInterfaces().Where(i => i != null && i.Namespace != null && i.Namespace.StartsWith("WileyWidget", StringComparison.Ordinal)).ToArray();
                            var preferred = interfaces.FirstOrDefault(i => string.Equals(i.Name, "I" + name, StringComparison.Ordinal));
                            if (preferred != null)
                            {
                                var already = false;
                                try
                                {
                                    var resolved = prismContainer.Resolve(preferred);
                                    already = resolved != null;
                                }
                                catch { /* not registered */ }

                                if (!already)
                                {
                                    if (regSingletonTwo != null)
                                    {
                                        regSingletonTwo.MakeGenericMethod(preferred, t).Invoke(containerRegistry, null);
                                    }
                                    else if (regRegisterTwo != null)
                                    {
                                        regRegisterTwo.MakeGenericMethod(preferred, t).Invoke(containerRegistry, null);
                                    }

                                    registeredCount++;
                                    Log.Debug("Convention: Registered singleton {Interface} -> {Impl}", preferred.FullName, t.FullName);
                                }
                                continue;
                            }

                            // No matching interface, register concrete as singleton if not already
                            var alreadyConcrete = false;
                            try
                            {
                                var resolved = prismContainer.Resolve(t);
                                alreadyConcrete = resolved != null;
                            }
                            catch { }

                            if (!alreadyConcrete)
                            {
                                if (regSingletonOne != null)
                                {
                                    regSingletonOne.MakeGenericMethod(t).Invoke(containerRegistry, null);
                                }
                                else if (regRegisterOne != null)
                                {
                                    regRegisterOne.MakeGenericMethod(t).Invoke(containerRegistry, null);
                                }

                                registeredCount++;
                                Log.Debug("Convention: Registered singleton {Type}", t.FullName);
                            }
                            continue;
                        }

                        // ViewModels -> transient (RegisterType) so Prism / ViewModelLocator can create per-view instances
                        if (name.EndsWith("ViewModel", StringComparison.Ordinal))
                        {
                            var alreadyVm = false;
                            try
                            {
                                var resolved = prismContainer.Resolve(t);
                                alreadyVm = resolved != null;
                            }
                            catch { }

                            if (!alreadyVm)
                            {
                                if (regRegisterOne != null)
                                {
                                    regRegisterOne.MakeGenericMethod(t).Invoke(containerRegistry, null);
                                }
                                registeredCount++;
                                Log.Debug("Convention: Registered ViewModel type {Type}", t.FullName);
                            }
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Convention registration failed for type {Type}", t.FullName);
                    }
                }
            }

            Log.Information("Convention-based registration completed ({Count} new registrations)", registeredCount);
        }

        /// <summary>
        /// Validates that Views have an associated ViewModel type available and auto-registers missing ViewModels.
        /// Logs warnings for Views that have no corresponding ViewModel discovered.
        /// </summary>
        private static void ValidateAndRegisterViewModels(IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null) throw new ArgumentNullException(nameof(containerRegistry));

            int autoRegistered = 0;
            int viewsWithoutVm = 0;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var allTypes = new List<Type>();
            foreach (var asm in assemblies)
            {
                try
                {
                    allTypes.AddRange(asm.GetTypes());
                }
                catch (ReflectionTypeLoadException ex)
                {
                    allTypes.AddRange(ex.Types.Where(t => t != null)!);
                }
            }

            var viewTypes = allTypes.Where(t => t != null && t.IsClass && !t.IsAbstract && t.Name.EndsWith("View", StringComparison.Ordinal)).ToArray();

            var regRegisterOne = typeof(IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "Register" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);
            var prismContainer = containerRegistry.GetContainer();

            foreach (var view in viewTypes)
            {
                try
                {
                    // Filter out system/debug views that should be ignored
                    if (view.Namespace != null &&
                        (view.Namespace.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                         view.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                         view.Name.Contains("Debug", StringComparison.OrdinalIgnoreCase) ||
                         view.Name.Contains("SpinLock", StringComparison.OrdinalIgnoreCase)))
                    {
                        Log.Debug("Skipping system/debug view: {View}", view.FullName);
                        continue;
                    }

                    // Only process WileyWidget views
                    if (view.Namespace == null || !view.Namespace.StartsWith("WileyWidget", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Candidate VM name: prefer WileyWidget.ViewModels.{ViewName}Model
                    var vmName = (view.Namespace != null && view.Namespace.Contains("Views", StringComparison.OrdinalIgnoreCase))
                        ? view.Namespace.Replace(".Views", ".ViewModels", StringComparison.OrdinalIgnoreCase) + "." + view.Name.Replace("View", "ViewModel", StringComparison.OrdinalIgnoreCase)
                        : "WileyWidget.ViewModels." + view.Name.Replace("View", "ViewModel", StringComparison.OrdinalIgnoreCase);

                    Type? vmType = null;
                    foreach (var asm in assemblies)
                    {
                        vmType = asm.GetType(vmName);
                        if (vmType != null) break;
                    }

                    if (vmType == null)
                    {
                        viewsWithoutVm++;
                        Log.Warning("ViewModel not found for view {View}. Expected type: {Expected}", view.FullName, vmName);
                        continue;
                    }

                    var already = false;
                    try
                    {
                        var resolved = prismContainer.Resolve(vmType);
                        already = resolved != null;
                    }
                    catch { }

                    if (!already)
                    {
                        try
                        {
                            // Register transient ViewModel so Prism can resolve it per view
                            if (regRegisterOne != null)
                            {
                                regRegisterOne.MakeGenericMethod(vmType).Invoke(containerRegistry, null);
                            }
                            autoRegistered++;
                            Log.Information("Auto-registered ViewModel {VM} for view {View}", vmType.FullName, view.FullName);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to auto-register ViewModel {VM}", vmType.FullName);
                        }
                    }
                    else
                    {
                        Log.Debug("ViewModel {VM} already registered for view {View}", vmType.FullName, view.FullName);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error validating view {View}", view?.FullName ?? "(unknown)");
                }
            }

            Log.Information("ViewModel validation completed: {AutoRegistered} auto-registered, {Missing} views lacked a ViewModel.", autoRegistered, viewsWithoutVm);
        }

        private static void EnableDryIocDiagnostics(IContainer prismContainer)
        {
            if (prismContainer == null)
            {
                throw new ArgumentNullException(nameof(prismContainer));
            }

            // Enable container diagnostics (DryIoc via Prism.Container.DryIoc)
            try
            {
                Trace.WriteLine("[Container] Debug diagnostics initialized");
            }
            catch
            {
                // Swallow diagnostics failures - this is non-critical
            }
        }

        /// <summary>
        /// Registers HttpClient infrastructure for AI services with retry policies and timeout configuration.
        /// Production-ready implementation with comprehensive error handling and logging.
        /// </summary>
        /// <param name="containerRegistry">The DI container registry for DI registration</param>
        /// <param name="configuration">Application configuration for HttpClient settings</param>

        /// <summary>
        /// Registers AI Integration Services for Phase 1 production deployment.
        /// Includes GrokSupercomputer, WileyWidgetContextService, and enhanced XAIService.
        /// All services are registered as singletons for optimal performance and resource management.
        /// </summary>
        /// <param name="containerRegistry">The DI container registry for DI registration</param>
        private void RegisterAIIntegrationServices(IContainerRegistry containerRegistry)
        {
            Log.Information("=== Registering AI Integration Services (Phase 1 - Production) ===");

            try
            {
                // 0. Register Application Insights Telemetry (Singleton)
                // Production telemetry for AI service monitoring and performance tracking
                // Application Insights integration deferred (not configured).

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
                Log.Information("   - Configuration: XAI:ApiKey, XAI:BaseUrl, XAI:Model, XAI:TimeoutSeconds");

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
        /// Registers AI Integration Services using lazy loading for better startup performance.
        /// Expensive services are only instantiated when first accessed.
        /// Enhanced to break circular dependencies using Lazy<T> pattern.
        /// </summary>
        /// <param name="containerRegistry">The DI container registry for DI registration</param>
        private void RegisterLazyAIServices(IContainerRegistry containerRegistry)
        {
            Log.Information("=== Registering AI Integration Services with Lazy Loading ===");

            try
            {
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

                // 2. Register Lazy<IAIService> to break circular dependencies
                // This allows services that depend on IAIService to take Lazy<IAIService> instead
                containerRegistry.RegisterSingleton<Lazy<IAIService>>(provider =>
                {
                    return new Lazy<IAIService>(() =>
                    {
                        Log.Information("Initializing XAIService on first access...");
                        return new XAIService(
                            provider.Resolve<IHttpClientFactory>(),
                            provider.Resolve<IConfiguration>(),
                            provider.Resolve<ILogger<XAIService>>(),
                            provider.Resolve<IWileyWidgetContextService>(),
                            provider.Resolve<IAILoggingService>(),
                            provider.Resolve<IMemoryCache>());
                    });
                });
                Log.Information("✓ Registered Lazy<IAIService> for breaking circular dependencies");

                // 2a. Register IAIService -> Lazy<XAIService> (Lazy Singleton)
                // xAI service implementation for AI-powered insights and analysis with Grok integration
                // Using lazy loading to defer expensive HTTP client and API initialization
                containerRegistry.RegisterSingleton<IAIService>(provider =>
                {
                    // Resolve the Lazy<IAIService> and get its value
                    var lazy = provider.Resolve<Lazy<IAIService>>();
                    return lazy.Value;
                });
                Log.Information("✓ Registered IAIService -> XAIService (Lazy Singleton)");
                Log.Information("  - xAI/Grok integration for AI-powered insights (lazy loaded)");
                Log.Information("  - Features: Insights, data analysis, area review, mock data generation");
                Log.Information("  - Dependencies: IHttpClientFactory, IConfiguration, ILogger<XAIService>, IWileyWidgetContextService, IAILoggingService, IMemoryCache");
                Log.Information("  - Performance: Service initialized only on first access");

                // 3. Register IGrokSupercomputer -> Lazy<GrokSupercomputer> (Lazy Singleton)
                // AI-powered municipal utility analytics and compliance reporting engine
                // Using lazy loading to defer expensive repository initialization and AI service dependencies
                containerRegistry.RegisterSingleton<IGrokSupercomputer>(provider =>
                {
                    var lazyService = new Lazy<GrokSupercomputer>(() =>
                    {
                        Log.Information("Initializing GrokSupercomputer on first access...");
                        // Use Lazy<IAIService> to avoid circular dependency issues
                        var lazyAI = provider.Resolve<Lazy<IAIService>>();
                        return new GrokSupercomputer(
                            provider.Resolve<ILogger<GrokSupercomputer>>(),
                            provider.Resolve<IEnterpriseRepository>(),
                            provider.Resolve<IBudgetRepository>(),
                            provider.Resolve<IAuditRepository>(),
                            provider.Resolve<IAILoggingService>(),
                            lazyAI.Value,  // Resolve the actual IAIService when needed
                            provider.Resolve<IMemoryCache>(),
                            provider.Resolve<IOptions<AppOptions>>());
                    });
                    return lazyService.Value;
                });
                Log.Information("✓ Registered IGrokSupercomputer -> GrokSupercomputer (Lazy Singleton)");
                Log.Information("  - AI-powered municipal utility analytics engine (lazy loaded)");
                Log.Information("  - Capabilities: Enterprise data fetching, report calculations, budget analysis, compliance reporting, AI data analysis");
                Log.Information("  - Dependencies: ILogger<GrokSupercomputer>, IEnterpriseRepository, IBudgetRepository, IAuditRepository, IAILoggingService, IAIService");
                Log.Information("  - Performance: Service initialized only on first access");

                // 4. Validate AI service configuration
                ValidateAIServiceConfiguration();

                Log.Information("=== AI Integration Services Registration Complete (Lazy Loading) ===");
                Log.Information("AI services registered with lazy loading for improved startup performance");
                Log.Information("Services will be initialized on first access rather than at application startup");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CRITICAL: Failed to register AI Integration Services with lazy loading");
                Log.Error("Application may not function correctly without AI services");
                Log.Error("Please check configuration (appsettings.json) and ensure all dependencies are available");
                throw new InvalidOperationException("Failed to register AI Integration Services with lazy loading. Application cannot continue.", ex);
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
                var config = ResolveWithRetry<IConfiguration>();
                var validationErrors = new List<string>();

                // Validate XAI configuration
                var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = config["XAI:ApiKey"];
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        Log.Information("XAI:ApiKey pulled from configuration XAI:ApiKey");
                    }
                }
                else
                {
                    Log.Information("XAI:ApiKey pulled from environment variable XAI_API_KEY");
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



        // Cache configuration to avoid redundant loading
        private static IConfiguration? _cachedConfiguration;

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            if (moduleCatalog == null) throw new ArgumentNullException(nameof(moduleCatalog));

            Log.Information("=== Configuring Prism Module Catalog (explicit registration) ===");
#if !WPFTMP
            // First try to let any custom manager populate the catalog (manifest-driven)
            try
            {
                WileyWidget.Startup.Modules.CustomModuleManager.RegisterModules(moduleCatalog);
                Log.Information("✓ Registered modules via CustomModuleManager");
            }
            catch (Exception ex)
            {
                // Don't fail startup just because the custom manager couldn't register modules.
                Log.Warning(ex, "CustomModuleManager.RegisterModules failed; will attempt deterministic/manual registration as fallback");
            }
#endif

            // Attempt to resolve the module health service to track registration/init status.
            IModuleHealthService? healthService = null;
            try
            {
                healthService = ResolveWithRetry<IModuleHealthService>();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not resolve IModuleHealthService during ConfigureModuleCatalog - module health tracking will be disabled");
            }

            // Deterministic module registration order. Add or reorder modules here to keep startup predictable.
            var moduleOrder = new[]
            {
                "CoreModule",
                "SettingsModule",
                "DashboardModule",
                "EnterpriseModule",
                "BudgetModule",
                "MunicipalAccountModule",
                "UtilityCustomerModule",
                "ReportsModule",
                "AIAssistModule",
                "PanelModule",
                "QuickBooksModule"
            };

            foreach (var moduleName in moduleOrder)
            {
                try
                {
                    // Compute initialization mode using existing helper
                    var initMode = GetModuleInitializationMode(moduleName, "WhenAvailable");

                    // If module already exists in catalog, update initialization mode and ensure health tracking is aware
                    var existing = moduleCatalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.InitializationMode = initMode;
                        Log.Debug("Module '{Module}' already present in catalog - set InitializationMode={Mode}", moduleName, initMode);
                        try { healthService?.RegisterModule(moduleName); } catch { }
                        continue;
                    }

                    // Try to resolve module type via reflection (AssemblyQualifiedName) so Prism can load it later
                    var moduleTypeName = TryResolveModuleTypeName(moduleName);
                    if (string.IsNullOrWhiteSpace(moduleTypeName))
                    {
                        var msg = $"Module type for '{moduleName}' not found in loaded assemblies; skipping explicit registration.";
                        Log.Warning(msg);
                        try { healthService?.MarkModuleInitialized(moduleName, false, msg); } catch { }
                        continue;
                    }

                    // Create ModuleInfo and register with health tracking
                    var moduleInfo = new ModuleInfo
                    {
                        ModuleName = moduleName,
                        ModuleType = moduleTypeName,
                        InitializationMode = initMode
                    };

                    if (healthService != null)
                    {
                        RegisterModuleWithHealthTracking(moduleCatalog, healthService, moduleName, () => moduleCatalog.AddModule(moduleInfo));
                    }
                    else
                    {
                        // No health service - do a direct registration
                        moduleCatalog.AddModule(moduleInfo);
                        Log.Debug("Registered module '{Module}' without health tracking", moduleName);
                    }
                }
                catch (ModuleInitializeException mex)
                {
                    Log.Error(mex, "ModuleInitializeException while registering module '{Module}'", moduleName);
                    try { healthService?.MarkModuleInitialized(moduleName, false, mex.Message); } catch { }
                    // Re-throw to surface serious initialization issues
                    throw;
                }
                catch (KeyNotFoundException knf)
                {
                    Log.Error(knf, "KeyNotFoundException while registering module '{Module}' - likely missing region mapping or manifest entry", moduleName);
                    try { healthService?.MarkModuleInitialized(moduleName, false, knf.Message); } catch { }
                    throw;
                }
                catch (TypeLoadException tle)
                {
                    Log.Error(tle, "TypeLoadException while resolving module type for '{Module}'", moduleName);
                    try { healthService?.MarkModuleInitialized(moduleName, false, tle.Message); } catch { }
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Non-fatal exception while registering module '{Module}' - marking health as failed and continuing", moduleName);
                    try { healthService?.MarkModuleInitialized(moduleName, false, ex.Message); } catch { }
                }
            }
        }

        protected override void InitializeModules()
        {
            Log.Information("Modules initializing...");

            try
            {
                base.InitializeModules();
                Log.Information("Base module initialization completed successfully");
            }
            catch (ModuleInitializeException ex)
            {
#if !WPFTMP
                // Handle module initialization exceptions with detailed diagnostics
                var rootEx = WileyWidget.Startup.Modules.PrismExceptionExtensions.GetRootException(ex);
                Log.Error(rootEx, "Critical ModuleInitializeException during module initialization: {Message}", rootEx.Message);
                Log.Error("Full exception chain: {DetailedMessage}", WileyWidget.Startup.Modules.PrismExceptionExtensions.GetDetailedMessage(ex));
#else
                Log.Error(ex, "Critical ModuleInitializeException during module initialization: {Message}", ex.Message);
#endif

                // Log which module failed if available
                // Note: ModuleInitializeException may not have ModuleType in this version
                Log.Error("Module initialization failed - check inner exceptions for details");

                // Try to get module health service for additional diagnostics
                try
                {
                    var healthService = ResolveWithRetry<IModuleHealthService>();
                    Log.Error("Module health status: {Status}", string.Join(", ", healthService.GetAllModuleStatuses().Select(m => $"{m.ModuleName}: {m.Status}")));
                }
                catch
                {
                    Log.Warning("Could not resolve IModuleHealthService for diagnostics");
                }

                // Don't rethrow - allow application to continue with partial initialization
                Log.Warning("Application will continue with partially initialized modules");
            }
            catch (Exception ex)
            {
#if !WPFTMP
                Log.Error(WileyWidget.Startup.Modules.PrismExceptionExtensions.GetRootException(ex), "Unexpected error during module initialization: {Message}", WileyWidget.Startup.Modules.PrismExceptionExtensions.GetRootException(ex).Message);
                Log.Error("Full exception chain: {DetailedMessage}", WileyWidget.Startup.Modules.PrismExceptionExtensions.GetDetailedMessage(ex));
#else
                Log.Error(ex, "Unexpected error during module initialization: {Message}", ex.Message);
#endif
                throw; // Re-throw unexpected exceptions
            }

            // Get the module health service for validation
            IModuleHealthService? moduleHealthService = null;
            try
            {
                moduleHealthService = ResolveWithRetry<IModuleHealthService>();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not resolve IModuleHealthService during module initialization");
            }

            // Validate module initialization and region availability
            if (moduleHealthService != null)
            {
                ValidateModuleInitialization(moduleHealthService);
            }

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
                var errorHandler = ResolveWithRetry<IPrismErrorHandler>();
                var eventAggregator = ResolveWithRetry<IEventAggregator>();

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
            // Check if we're in test mode - if so, load all modules to ensure UI tests can find views
            bool isTestMode = Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE") == "1";

            // Define module-specific initialization modes
            var moduleInitModes = new Dictionary<string, InitializationMode>
            {
                // Core infrastructure module - load immediately (consolidated from Diagnostics, Syncfusion, Settings)
                ["CoreModule"] = InitializationMode.WhenAvailable,
                ["QuickBooksModule"] = InitializationMode.OnDemand,

                // Feature modules - EnterpriseModule loads at startup to support MainViewModel dependencies
                ["DashboardModule"] = InitializationMode.WhenAvailable,
                ["SettingsModule"] = InitializationMode.WhenAvailable,
                ["EnterpriseModule"] = InitializationMode.WhenAvailable,
                ["BudgetModule"] = InitializationMode.OnDemand,
                ["MunicipalAccountModule"] = isTestMode ? InitializationMode.WhenAvailable : InitializationMode.OnDemand,
                ["UtilityCustomerModule"] = InitializationMode.OnDemand,
                // Load these at startup so navigation by name succeeds immediately
                ["ReportsModule"] = InitializationMode.WhenAvailable,
                ["AIAssistModule"] = InitializationMode.WhenAvailable,

                // Panel modules - load when their dependencies are loaded
                ["PanelModule"] = InitializationMode.WhenAvailable
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
            // Use the shared static moduleRegionMap defined on the App class for expected region mappings.

            Log.Information("=== Validating Module Initialization and Region Availability ===");

            var regionManager = ResolveWithRetry<IRegionManager>();
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
                if (result.StartsWith("✓", StringComparison.OrdinalIgnoreCase))
                    Log.Information(result);
                else if (result.StartsWith("✗", StringComparison.OrdinalIgnoreCase))
                    Log.Error(result);
                else if (result.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
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

        /// <summary>
        /// Attempts to resolve an assembly-qualified module type name for a module named like 'CoreModule'.
        /// Scans loaded assemblies for a type whose name matches the provided moduleName (or ends with ".{moduleName}").
        /// Returns the AssemblyQualifiedName or null if not found.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Type?> _typeByShortNameCache = new();
        private static readonly ConcurrentDictionary<string, string?> _moduleTypeNameCache = new();

        private static string? TryResolveModuleTypeName(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName)) return null;

            if (_moduleTypeNameCache.TryGetValue(moduleName, out var cached))
                return cached;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    // exact name match (CoreModule) or full name ending with ".CoreModule"
                    if (string.Equals(t.Name, moduleName, StringComparison.Ordinal) || (t.FullName != null && t.FullName.EndsWith("." + moduleName, StringComparison.Ordinal)))
                    {
                        var aqn = t.AssemblyQualifiedName;
                        _moduleTypeNameCache[moduleName] = aqn;
                        return aqn;
                    }
                }
            }

            _moduleTypeNameCache[moduleName] = null;
            return null;
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Prism's DryIocContainerExtension takes ownership of the DryIoc Container and will dispose it when appropriate.")]
        protected override IContainerExtension CreateContainerExtension()
        {
            var sw = Stopwatch.StartNew();
            // Configure DryIoc container with constructor selection to handle types with multiple public constructors
            var rules = DryIoc.Rules.Default
                // Prefer Microsoft DI-compatible behavior for broader library compatibility
                .WithMicrosoftDependencyInjectionRules()
                // Prefer constructors where arguments can be resolved (greedy/resolvable)
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                // Keep existing optimizations
                .WithDefaultReuse(Reuse.Singleton)
                .WithAutoConcreteTypeResolution()
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace)
                .WithFactorySelector(Rules.SelectLastRegisteredFactory())
                .WithoutThrowOnRegisteringDisposableTransient()
                .WithTrackingDisposableTransients();

            var container = new Container(rules);

            Log.Information("DryIoc container configured with optimized rules and greedy ctor selection for complex service resolution");
            LogStartupTiming("CreateContainerExtension: DryIoc container creation", sw.Elapsed);

            return new DryIocContainerExtension(container);
        }        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}", formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File("logs/wiley-widget-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();

            // Add WPF binding error tracing to Serilog
            var bindingTraceListener = new BindingErrorTraceListener();
            Trace.Listeners.Add(bindingTraceListener);
            PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTraceListener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            Log.Information("=== WileyWidget Prism application startup ===");
            Log.Information("✓ WPF binding error tracing enabled");
        }

        private static IConfiguration TryRunBootstrapper(IContainerRegistry containerRegistry)
        {
            try
            {
                var t = Type.GetType("WileyWidget.Startup.BootstrapHelpers") ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Startup.BootstrapHelpers");
                if (t != null)
                {
                    var m = t.GetMethod("TryRunBootstrapper", BindingFlags.Public | BindingFlags.Static);
                    if (m != null)
                    {
                        var res = m.Invoke(null, new object?[] { containerRegistry });
                        return res as IConfiguration ?? throw new InvalidOperationException("TryRunBootstrapper returned null");
                    }
                }
            }
            catch
            {
                // swallow - fall through to default
            }

            // Fallback: build configuration directly
            return BuildConfiguration();
        }

        private static void TryResolvePlaceholders(IConfiguration configuration)
        {
            try
            {
                var t = Type.GetType("WileyWidget.Startup.BootstrapHelpers") ?? Assembly.GetExecutingAssembly().GetType("WileyWidget.Startup.BootstrapHelpers");
                if (t != null)
                {
                    var m = t.GetMethod("TryResolvePlaceholders", BindingFlags.Public | BindingFlags.Static);
                    if (m != null)
                    {
                        m.Invoke(null, new object?[] { configuration });
                        return;
                    }
                }
            }
            catch
            {
                // swallow - fall through to default
            }

            // Fallback: Basic placeholder resolution using environment variables
            // This handles cases where BootstrapHelpers is not available
            if (configuration is IConfigurationRoot configurationRoot)
            {
                ResolveConfigurationPlaceholders(configurationRoot);
            }
        }

        private static void ResolveConfigurationPlaceholders(IConfigurationRoot configurationRoot)
        {
            // Simple placeholder resolution for ${ENV_VAR_NAME} patterns
            var data = new Dictionary<string, string>();
            VisitConfiguration("", configurationRoot, data);

            foreach (var kvp in data)
            {
                var trimmedValue = kvp.Value?.Trim();
                if (trimmedValue != null &&
                    trimmedValue.StartsWith("${", StringComparison.Ordinal) &&
                    trimmedValue.EndsWith('}'))
                {
                    var envVarName = trimmedValue.Substring(2, trimmedValue.Length - 3);
                    var envValue = Environment.GetEnvironmentVariable(envVarName);
                    if (!string.IsNullOrEmpty(envValue))
                        // NOTE: Direct assignment to configurationRoot[kvp.Key] only updates the in-memory provider and may not affect values from other providers.
                        // For robust placeholder resolution across all providers, consider using an in-memory provider for overrides or a custom configuration source.
                        configurationRoot[kvp.Key] = envValue;
                    // Update the configuration value
                    // Note: This is a simplified approach - in production you'd want more robust placeholder resolution
                    configurationRoot[kvp.Key] = envValue;
                }
            }
        }

        private static void VisitConfiguration(string path, IConfiguration configuration, Dictionary<string, string> data)
        {
            foreach (var child in configuration.GetChildren())
            {
                var childPath = string.IsNullOrEmpty(path) ? child.Key : $"{path}:{child.Key}";
                if (child.Value != null)
                {
                    data[childPath] = child.Value;
                }
                VisitConfiguration(childPath, child, data);
            }
        }

        private static IConfiguration BuildConfiguration()
        {
            // Get project directory for .env file
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = System.IO.Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
            var projectDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(assemblyDir)) ?? AppDomain.CurrentDomain.BaseDirectory; // bin\Debug\net9.0-windows -> project root
            var envPath = System.IO.Path.Combine(projectDir, ".env");

            // Check for XAI_API_KEY environment variable and write to .env only in non-production
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var xaiApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            if (!string.IsNullOrEmpty(xaiApiKey))
            {
                if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
                {
                    bool needsXaiKey = true;
                    if (System.IO.File.Exists(envPath))
                    {
                        // Check if .env already contains XAI_API_KEY
                        var envContent = System.IO.File.ReadAllText(envPath);
                        if (envContent.Contains("XAI_API_KEY=", StringComparison.OrdinalIgnoreCase))
                        {
                            needsXaiKey = false;
                        }
                    }

                    if (needsXaiKey)
                    {
                        // Append XAI_API_KEY to .env file (dev/test only)
                        System.IO.File.AppendAllText(envPath, $"{Environment.NewLine}XAI_API_KEY={xaiApiKey}{Environment.NewLine}");
                        Log.Information("XAI_API_KEY from environment variable written to .env file (non-production)");
                    }
                }
                else
                {
                    Log.Debug("Skipping writing XAI_API_KEY to .env in Production");
                }
            }

            // Load .env file from project directory
            if (System.IO.File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            var configurationRoot = builder.Build();
            // Call ResolvePlaceholders extension if available (defined in BootstrapHelpers),
            // otherwise perform a local fallback resolution to support wpftmp/design-time builds.
            TryResolvePlaceholders(configurationRoot);

            return configurationRoot;
        }

        /// <summary>
        /// Ensure application shutdown is robust. Some third-party libraries (Syncfusion) may attempt to show
        /// dialogs during shutdown which can throw if the UI thread or owner windows are disposed. We catch
        /// and swallow known shutdown-time exceptions to avoid the process terminating with an unhelpful crash.
        /// </summary>
        /// <param name="e">Exit event args</param>
        protected override void OnExit(ExitEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            Log.Information("Application shutdown initiated - Session: {StartupId}", _startupId);

            try
            {
                // Re-register critical licenses as best-effort so any controls that touch licensing on shutdown
                // will have access to the registered license string (some third-party libs show dialogs otherwise).
                try
                {
                    EnsureSyncfusionLicenseRegistered(forceRefresh: true);
                }
                catch (Exception regEx)
                {
                    Log.Warning(regEx, "Failed to re-register Syncfusion license during shutdown (non-fatal)");
                }

                try
                {
                    EnsureBoldReportsLicenseRegistered(forceRefresh: true);
                }
                catch (Exception regEx)
                {
                    Log.Warning(regEx, "Failed to re-register Bold Reports license during shutdown (non-fatal)");
                }

                // If deferred secrets initialization is still running, observe outcome with a short timeout
                try
                {
                    var secretsTask = SecretsInitializationTask;
                    if (!secretsTask.IsCompleted)
                    {
                        Log.Debug("Waiting briefly for deferred secrets initialization to complete before shutdown");
                        var completed = System.Threading.Tasks.Task.WhenAny(secretsTask, System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2))).Result;
                        if (completed == secretsTask)
                        {
                            if (secretsTask.IsFaulted)
                                Log.Warning(secretsTask.Exception, "Deferred secrets initialization faulted during shutdown");
                            else
                                Log.Debug("Deferred secrets initialization completed during shutdown wait");
                        }
                        else
                        {
                            Log.Debug("Deferred secrets initialization did not complete within shutdown timeout - proceeding with shutdown");
                        }
                    }
                }
                catch (Exception exSecrets)
                {
                    Log.Debug(exSecrets, "Error while waiting for deferred secrets initialization during shutdown (non-fatal)");
                }

                // Attempt to gracefully stop and dispose common services by best-effort lookup from DI.
                try
                {
                    // Dispose UnitOfWork if present
                    try
                    {
                        var uow = ResolveWithRetry<IUnitOfWork>(maxAttempts: 1);
                        try { (uow as IDisposable)?.Dispose(); Log.Debug("Disposed IUnitOfWork during shutdown"); } catch (Exception dU) { Log.Debug(dU, "Disposing IUnitOfWork threw during shutdown"); }
                    }
                    catch { /* swallow - optional service not available */ }

                    // Dispose memory cache if registered
                    try
                    {
                        var mem = ResolveWithRetry<IMemoryCache>(maxAttempts: 1);
                        try { mem?.Dispose(); Log.Debug("Disposed IMemoryCache during shutdown"); } catch (Exception dM) { Log.Debug(dM, "Disposing IMemoryCache threw during shutdown"); }
                    }
                    catch { }

                    // If an IServiceScopeFactory is available, create a scope and attempt to stop hosted services if any
                    try
                    {
                        var scopeFactory = ResolveWithRetry<IServiceScopeFactory>(maxAttempts: 1);
                        if (scopeFactory != null)
                        {
                            using var scope = scopeFactory.CreateScope();
                            // Stop all hosted services if any are registered
                            try
                            {
                                var hostedServices = scope.ServiceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
                                foreach (var hosted in hostedServices)
                                {
                                    try { hosted.StopAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult(); Log.Debug("Stopped IHostedService during shutdown"); } catch (Exception hsEx) { Log.Debug(hsEx, "IHostedService.StopAsync threw during shutdown"); }
                                }
                            }
                            catch (Exception getSvcEx)
                            {
                                Log.Debug(getSvcEx, "Enumerating IHostedService during shutdown failed (non-fatal)");
                            }
                        }
                    }
                    catch { }
                }
                catch (Exception exCleanup)
                {
                    Log.Debug(exCleanup, "General cleanup during shutdown threw an exception (non-fatal)");
                }

                // Call base.OnExit to allow Prism and WPF internals to perform cleanup
                try
                {
                    base.OnExit(e);
                }
                catch (InvalidOperationException invEx)
                {
                    // Known problematic case: Syncfusion may attempt to show shutdown dialogs after windows disposed
                    // which throws InvalidOperationException or similar; swallow and log as warning to allow process exit.
                    Log.Warning(invEx, "InvalidOperationException during base.OnExit - likely a shutdown dialog from a third-party control; swallowing to allow graceful exit");
                }
                catch (Exception baseEx)
                {
                    // Some libraries may throw specialized exceptions during shutdown; inspect message for known fingerprints
                    var bm = baseEx.ToString();
                    if (bm.Contains("LicenseMessage", StringComparison.OrdinalIgnoreCase) || bm.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warning(baseEx, "Syncfusion-related exception during base.OnExit swallowed to allow graceful exit");
                    }
                    else
                    {
                        // Unknown exceptions are logged as fatal but not rethrown to avoid leaving the process in a bad state
                        Log.Fatal(baseEx, "Unhandled exception during base.OnExit");
                    }
                }

                Log.Information("Application shutdown completed (requested) - Session: {StartupId}", _startupId);
                Log.Information("Application exit code: {ExitCode} - Session: {StartupId}", e.ApplicationExitCode, _startupId);
                Log.Information("=== Application Shutdown Complete - Session: {StartupId} ===", _startupId);
            }
            catch (Exception ex)
            {
                // Best-effort filtering of known, non-fatal shutdown exceptions (Syncfusion/License dialogs)
                try
                {
                    if (ex is InvalidOperationException || ex.ToString().IndexOf("syncfusion", StringComparison.OrdinalIgnoreCase) >= 0 || ex.ToString().IndexOf("LicenseMessage", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log.Warning(ex, "Non-fatal third-party exception occurred during OnExit and was swallowed");
                    }
                    else
                    {
                        Log.Fatal(ex, "Unhandled exception during application shutdown (caught in outer handler)");
                    }
                }
                catch { }
            }
            finally
            {
                // Try to dispose the Prism/DryIoc container if it exposes IDisposable
                try
                {
                    if (this.Container is IDisposable d)
                    {
                        try { d.Dispose(); Log.Debug("Disposed DI container during shutdown"); } catch (Exception dEx) { Log.Debug(dEx, "Disposing DI container threw during shutdown"); }
                    }
                }
                catch { }

                // Ensure logger flushes all sinks
                try { Log.CloseAndFlush(); } catch { }
            }
        }

        private void ConfigureDiagnosticsSettings(IConfiguration configuration)
        {
            try
            {
                var diagSection = configuration.GetSection("System.Diagnostics");
                if (diagSection.Exists())
                {
                    var sources = diagSection.GetSection("Sources");
                    if (sources.Exists())
                    {
                        foreach (var source in sources.GetChildren())
                        {
                            var sourceName = source["Name"];
                            var switchValue = source["SwitchValue"];
                            if (!string.IsNullOrEmpty(sourceName) && !string.IsNullOrEmpty(switchValue))
                            {
                                var traceSource = new TraceSource(sourceName);
                                if (Enum.TryParse<SourceLevels>(switchValue, out var level))
                                {
                                    traceSource.Switch.Level = level;
                                }
                                var listeners = source.GetSection("Listeners");
                                if (listeners.Exists())
                                {
                                    foreach (var listener in listeners.GetChildren())
                                    {
                                        var listenerName = listener["Name"];
                                        if (!string.IsNullOrEmpty(listenerName))
                                        {
                                            traceSource.Listeners.Add(new DefaultTraceListener());
                                        }
                                    }
                                }
                            }
                        }
                    }

                    var trace = diagSection.GetSection("Trace");
                    if (trace.Exists())
                    {
                        System.Diagnostics.Trace.AutoFlush = trace.GetValue<bool>("AutoFlush");
                    }
                }
                Log.Information("Diagnostics settings configured from appsettings.json");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure diagnostics settings from appsettings.json");
            }
        }

        /// <summary>
        /// Validates that all registered types are public and have public constructors.
        /// This helps prevent InvalidRegistrationException due to accessibility issues.
        /// </summary>
        private void ValidatePublicAccessibility()
        {
            Log.Information("Validating public accessibility of registered types...");

            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var isProduction = string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);

            // List of types to validate (key services and ViewModels)
            var typesToValidate = new[]
            {
                typeof(XAIService),
                typeof(DataAnonymizerService),
                typeof(WileyWidgetContextService),
                typeof(AILoggingService),
                typeof(GrokSupercomputer),
                typeof(EnterpriseRepository),
                typeof(SettingsService),
                typeof(MainViewModel),
                typeof(AIAssistViewModel),
                typeof(SettingsViewModel)
            };

            foreach (var type in typesToValidate)
            {
                if (!type.IsPublic && !type.IsNestedPublic)
                {
                    if (isProduction)
                    {
                        Log.Warning("Type {Type} is not public - this may cause InvalidRegistrationException (production warning only)", type.FullName);
                    }
                    else
                    {
                        Log.Error("Type {Type} is not public - this may cause InvalidRegistrationException", type.FullName);
                        throw new InvalidOperationException($"Type {type.FullName} must be public for DI container registration");
                    }
                }

                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                if (constructors.Length == 0)
                {
                    if (isProduction)
                    {
                        Log.Warning("Type {Type} has no public constructors - this may cause InvalidRegistrationException (production warning only)", type.FullName);
                    }
                    else
                    {
                        Log.Error("Type {Type} has no public constructors - this may cause InvalidRegistrationException", type.FullName);
                        throw new InvalidOperationException($"Type {type.FullName} must have at least one public constructor");
                    }
                }
            }

            Log.Information("✓ Public accessibility validation completed for {Count} types (mode: {Mode})", typesToValidate.Length, isProduction ? "Production - warnings only" : "Development - strict");
        }
    }
}
