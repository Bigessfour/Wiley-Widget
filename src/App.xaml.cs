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
using Prism.Ioc;
using Prism.Modularity;
using PrismIoc = Prism.Ioc;
using PrismModularity = Prism.Modularity;
using Prism;
using Prism.Mvvm;
using Prism.Container.DryIoc;
using Prism.Navigation.Regions;
using DryIoc;
using Syncfusion.SfSkinManager;
using Syncfusion.Licensing;
using Bold.Licensing;
using WileyWidget.Views;
using WileyWidget.Startup.Modules;
using WileyWidget.Startup;
using WileyWidget.Services;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
// Removed duplicate using System.Diagnostics (already included above)
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
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
// Azure.Identity removed from App.xaml.cs — azure-specific logic moved out of bootstrapper
using System.Text.RegularExpressions;
using Prism.Events;
using WileyWidget.ViewModels.Messages;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using Prism;
// using Microsoft.ApplicationInsights;
// using Microsoft.ApplicationInsights.Extensibility;
using Serilog.Events;
using System.Windows.Markup;
using System.Xaml;

namespace WileyWidget
{
    public partial class App : Prism.PrismApplicationBase
    {
        // Task that completes when de1ferred secret initialization finishes.
        // Consumers can await App.SecretsInitializationTask to know when secrets are available.
        private static readonly System.Threading.Tasks.TaskCompletionSource<bool> _secretsInitializationTcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        public static System.Threading.Tasks.Task SecretsInitializationTask => _secretsInitializationTcs.Task;

        // Static mapping of expected regions for each module for maintainability and reuse
        private static readonly Dictionary<string, string[]> moduleRegionMap = new Dictionary<string, string[]>
        {
            ["CoreModule"] = Array.Empty<string>(),
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
            if (report is IEnumerable<ModuleHealthInfo> moduleHealthInfos)
            {
                int totalModules = 0;
                int healthyModules = 0;

                foreach (var module in moduleHealthInfos)
                {
                    totalModules++;
                    if (module.Status == ModuleHealthStatus.Healthy)
                    {
                        healthyModules++;
                    }
                }

                Log.Debug("Module health report refreshed: {Healthy}/{Total} modules healthy", healthyModules, totalModules);
                return;
            }
            else if (report is ModuleHealthInfo singleModule)
            {
                Log.Debug("Module health report refreshed for {ModuleName}: {Status}", singleModule.ModuleName, singleModule.Status);
                return;
            }
            else
            {
                Log.Debug("Module health report refreshed ({ReportType})", report.GetType().FullName);
                return;
            }
        }

        /// <summary>
        /// Bootstrapper constructor - handles early startup logging and diagnostics
        /// moved from Program.cs as per minimal entry point requirements.
        /// CRITICAL: Syncfusion license registration MUST be in App() constructor per official documentation:
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
                .WriteTo.File("logs/startup-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
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
            base.OnStartup(e);

            // Ensure we capture any unhandled exceptions on the UI Dispatcher as a last line of defense
            Application.Current.DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    // Log and swallow (mark handled) to avoid abrupt crashes in production UIs
                    var ex = WileyWidget.Startup.BootstrapHelpers.UnwrapTargetInvocationException(args.Exception);
                    Log.Error(ex, "Unhandled UI dispatcher exception caught in OnStartup hook");
                    args.Handled = true;
                }
                catch (Exception hookEx)
                {
                    // If logging fails, ensure we still mark handled to avoid crash loops
                    try { Log.Error(hookEx, "Error while handling DispatcherUnhandledException"); } catch { }
                    args.Handled = true;
                }
            };

            InitializeApplication();
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

                // Apply theme globally via SfSkinManager per Syncfusion best practices
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                SfSkinManager.ApplicationTheme = new Theme("FluentLight");
                Log.Information("✓ Applied FluentLight theme globally via SfSkinManager");

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
            base.ConfigureRegionAdapterMappings(regionAdapterMappings);
            try
            {
                var behaviorFactory = ResolveWithRetry<IRegionBehaviorFactory>();
                var sfGridAdapter = new WileyWidget.Regions.SfDataGridRegionAdapter(behaviorFactory);
                regionAdapterMappings.RegisterMapping(typeof(Syncfusion.UI.Xaml.Grid.SfDataGrid), sfGridAdapter);
                Log.Information("✓ Registered SfDataGridRegionAdapter for Prism regions");

                // Register Syncfusion DockingManager adapter
                var dockingAdapter = new WileyWidget.Regions.DockingManagerRegionAdapter(behaviorFactory);
                regionAdapterMappings.RegisterMapping(typeof(Syncfusion.Windows.Tools.Controls.DockingManager), dockingAdapter);
                Log.Information("✓ Registered DockingManagerRegionAdapter for Prism regions");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register SfDataGridRegionAdapter; grid regions will be unavailable");
            }
        }

        // Configure default region behaviors, including diagnostics and context sync
    protected override void ConfigureDefaultRegionBehaviors(IRegionBehaviorFactory regionBehaviors)
        {
            base.ConfigureDefaultRegionBehaviors(regionBehaviors);
            try
            {
                regionBehaviors.AddIfMissing(WileyWidget.Regions.NavigationLoggingBehavior.BehaviorKey, typeof(WileyWidget.Regions.NavigationLoggingBehavior));
                regionBehaviors.AddIfMissing(WileyWidget.Regions.AutoActivateBehavior.BehaviorKey, typeof(WileyWidget.Regions.AutoActivateBehavior));
                regionBehaviors.AddIfMissing(WileyWidget.Regions.AutoSaveBehavior.BehaviorKey, typeof(WileyWidget.Regions.AutoSaveBehavior));
                regionBehaviors.AddIfMissing(WileyWidget.Regions.SyncContextWithHostBehavior.BehaviorKey, typeof(WileyWidget.Regions.SyncContextWithHostBehavior));
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
                Exception processedException = WileyWidget.Startup.BootstrapHelpers.UnwrapTargetInvocationException(e.Exception);

                if (TryHandleDryIocContainerException(processedException))
                {
                    e.Handled = true;
                    Log.Warning("DryIoc container exception handled and recovered");
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
                Exception? processedException = WileyWidget.Startup.BootstrapHelpers.UnwrapTargetInvocationException(exception);
                Log.Fatal(processedException, "Unhandled background thread exception occurred");
                // Also log full inner exception chain to help diagnose reflection-wrapped errors
                WileyWidget.Startup.BootstrapHelpers.LogExceptionDetails(processedException);
                // Application will terminate after this
            };

            // Handle unobserved task exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Exception processedException = WileyWidget.Startup.BootstrapHelpers.UnwrapTargetInvocationException(e.Exception);
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

            if (exception is System.Xaml.XamlObjectWriterException xamlWriterEx)
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

                if (innerException is System.Xaml.XamlObjectWriterException innerXamlWriterEx)
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

            // Check for DryIoc container resolution failures
            if (message.Contains("dryioc") || message.Contains("container") || message.Contains("resolution"))
            {
                Log.Error(exception, "DryIoc container exception detected: {Message}", exception.Message);

                // Try to provide specific guidance based on the error
                if (message.Contains("not registered") || message.Contains("could not resolve"))
                {
                    Log.Warning("Service registration issue detected. Check that all required services are registered in RegisterTypes().");
                    Log.Warning("Common missing registrations: ILogger<>, IOptions<>, or module-specific services.");
                }

                if (message.Contains("circular") || message.Contains("dependency"))
                {
                    Log.Warning("Circular dependency detected in DI container. Check for circular references in constructor parameters.");
                }

                // For container issues, we typically cannot recover at runtime
                // The application needs to be fixed and restarted
                return false;
            }

            // Check for module initialization failures
            if (message.Contains("module") && (message.Contains("initialize") || message.Contains("load")))
            {
                Log.Error(exception, "Prism module initialization failure: {Message}", exception.Message);
                Log.Warning("Module failed to initialize. Check module dependencies and registrations.");
                return false;
            }

            return false;
        }

        /// <summary>
        /// Centralized, safer container resolve which will attempt to resolve scoped services
        /// from an IServiceScopeFactory when available, falling back to container resolve.
        /// Includes lightweight retry to harden against transient resolution failures.
        /// </summary>
        private T ResolveWithRetry<T>(int maxAttempts = 3) where T : class
        {
            // IMPORTANT: Avoid self-recursion when attempting to resolve IServiceScopeFactory.
            // We first try the root Prism container, and only if a scope factory is ALREADY available
            // do we create a scope to resolve the requested service. We never call ResolveWithRetry
            // from within itself.
            bool activatorFallbackUsed = false;

            var result = WileyWidget.Startup.BootstrapHelpers.RetryOnException(() =>
            {
                // 1) Try resolve directly from Prism's container
                try
                {
                    var instance = this.Container.Resolve<T>();
                    if (instance != null)
                        return instance;
                }
                catch
                {
                    // ignore and try via scope below
                }

                // 2) If an IServiceScopeFactory has been registered, attempt to resolve via a scope
                try
                {
                    var scopeFactory = Container.Resolve<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
                    if (scopeFactory != null)
                    {
                        using var scope = scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetService(typeof(T)) as T;
                        if (svc != null)
                            return svc;
                    }
                }
                catch
                {
                    // ignore and fall back to Activator
                }

                // 3) Last resort
                activatorFallbackUsed = true;
                return Activator.CreateInstance<T>();
            }, maxAttempts);

            if (activatorFallbackUsed)
            {
                // Warn when we had to fallback to Activator. This indicates a missing or misconfigured
                // registration in the DI container and should be investigated (but is tolerated at runtime).
                Log.Warning("ResolveWithRetry used Activator.CreateInstance fallback for type {Type}. This may indicate a missing DI registration.", typeof(T).FullName);
            }

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
            if (message.Contains("syncfusion") && message.Contains("license"))
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

            if (message.Contains("viewmodellocator") || message.Contains("autowire"))
            {
                Log.Warning("ViewModelLocator issue detected - check ViewModel registrations");
                // This typically requires code fixes, not runtime recovery
                return false;
            }

            if (message.Contains("assembly") && message.Contains("not found"))
            {
                Log.Warning("Missing assembly reference detected - check NuGet packages");
                return false;
            }

            if (message.Contains("xmlns") || message.Contains("namespace"))
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
            if (message.Contains("property") && message.Contains("not found"))
            {
                Log.Warning("Property binding issue detected - check property names and types");
                return false; // Requires code fix
            }

            // Check for type resolution issues
            if (message.Contains("type") && message.Contains("not found"))
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
                return base.Container.Resolve<Views.Shell>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve application shell from container");
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

    protected override void RegisterTypes(Prism.Ioc.IContainerRegistry containerRegistry)
        {
            Log.Information("=== Starting DI Container Registration ===");
            Log.Debug("RegisterTypes called with containerRegistry: {Type}", containerRegistry?.GetType().Name);

            IConfiguration configuration;
            var testMode = (Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE") ?? "0") == "1";
            // Diagnostics controls: keep extended diagnostics (chatty logging + eager resolves) OFF by default
            string extendedDiagEnv = Environment.GetEnvironmentVariable("WILEY_WIDGET_EXTENDED_DIAGNOSTICS") ?? "0";
            bool enableExtendedDiagnostics = extendedDiagEnv == "1" || string.Equals(extendedDiagEnv, "true", StringComparison.OrdinalIgnoreCase);
            try
            {
                // Use expanded Bootstrapper.Run() for centralized startup orchestration
                // Handles: Configuration, Logging, HttpClient (with Polly), DbContext (with retries)
                var bootstrapper = new WileyWidget.Startup.Bootstrapper();
                configuration = bootstrapper.Run(containerRegistry);
                Log.Debug("Configuration, logging, HttpClient, and database services registered via Bootstrapper.Run()");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Bootstrapper.Run() failed during infrastructure registration");
                throw;
            }

            // HttpClient and Database services are now handled by Bootstrapper - no need to duplicate here
            Log.Debug("Infrastructure services (HttpClient, Database) already configured by Bootstrapper - skipping duplicate registration");


            try
            {
                // Register core infrastructure services
                containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
                containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
                containerRegistry.RegisterSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
                containerRegistry.RegisterSingleton<SettingsService>();
                containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
                Log.Debug("About to register IAuditService...");
                containerRegistry.RegisterSingleton<IAuditService, AuditService>();
                Log.Debug("IAuditService registration completed");
                // NOTE: ThemeManager removed - SfSkinManager handles all theming globally per Syncfusion documentation
                // Reference: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
                containerRegistry.RegisterSingleton<IDispatcherHelper>(provider => new DispatcherHelper());
                containerRegistry.RegisterSingleton<AppOptionsConfigurator>();

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

            // Defer secrets initialization to a background task to avoid blocking startup
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var secretVault = ResolveWithRetry<ISecretVaultService>();
                    await secretVault.MigrateSecretsFromEnvironmentAsync().ConfigureAwait(false);
                    Log.Information("✓ (Deferred) Environment secrets migrated to local vault");

                    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
                    if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                    {
                        await secretVault.PopulateProductionSecretsAsync().ConfigureAwait(false);
                        Log.Information("✓ (Deferred) Production secrets initialized");
                    }

                    // Signal successful completion of deferred secret initialization
                    _secretsInitializationTcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Deferred production secrets initialization failed");
                    // Surface failure to any awaiters so readiness checks can act accordingly
                    _secretsInitializationTcs.TrySetException(ex);
                }
            });

            try
            {
                // Register Microsoft.Extensions.Caching.Memory infrastructure
                MemoryCacheOptions memoryCacheOptions = new MemoryCacheOptions();
                string? configuredSizeLimit = configuration["Caching:MemoryCache:SizeLimit"];
                if (long.TryParse(configuredSizeLimit, out long sizeLimit) && sizeLimit > 0)
                {
                    memoryCacheOptions.SizeLimit = sizeLimit;
                }

#pragma warning disable CA2000 // DI container will dispose the registered singleton when the container is disposed
                MemoryCache memoryCache = new MemoryCache(memoryCacheOptions);
#pragma warning restore CA2000
                containerRegistry.RegisterInstance<IMemoryCache>(memoryCache);
                Log.Information("✓ Registered IMemoryCache using Prism-managed MemoryCache instance");

                // Register configuration options infrastructure (bridging Microsoft.Extensions.Options into container)
                RegisterAppOptions(containerRegistry, configuration);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register caching and options services");
                throw;
            }

            try
            {
                // Register data repositories required during startup validation to prevent DI container resolution failures
                // Enterprise repository is registered by EnterpriseModule; remove duplicate central registration to
                // avoid multiple registrations and maintain module ownership of its services.
                // containerRegistry.Register<IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
                containerRegistry.Register<IBudgetRepository, WileyWidget.Data.BudgetRepository>();
                containerRegistry.Register<IAuditRepository, WileyWidget.Data.AuditRepository>();
                containerRegistry.Register<IMunicipalAccountRepository, WileyWidget.Data.MunicipalAccountRepository>();
                containerRegistry.Register<IUtilityCustomerRepository, WileyWidget.Data.UtilityCustomerRepository>();
                containerRegistry.Register<IDepartmentRepository, WileyWidget.Data.DepartmentRepository>();
                Log.Information("✓ Registered core data repositories for startup validation (Enterprise, Budget, Audit, MunicipalAccount, UtilityCustomer, Department)");

                // Ensure Prism-resolved ViewModels can obtain the UnitOfWork infrastructure
                containerRegistry.Register<IUnitOfWork, UnitOfWork>();
                Log.Information("✓ Registered IUnitOfWork infrastructure for Prism ViewModels");

                // Register IServiceScopeFactory for DryIoc-based scoped services (required by WhatIfScenarioEngine)
                containerRegistry.RegisterSingleton<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory, DryIocServiceScopeFactory>();
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
                    // Register AI services for production
                    RegisterAIIntegrationServices(containerRegistry);

                    // Register QuickBooks service
                    // Use non-generic Type-based registration to avoid temporary wpftmp generic constraint issues
                    // (wpftmp can produce duplicate-type/assembly identity problems during XAML compile).
                    containerRegistry.RegisterSingleton(typeof(IQuickBooksService), typeof(QuickBooksService));
                    Log.Information("✓ Registered IQuickBooksService as singleton");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register AI and QuickBooks services");
                throw;
            }

            try
            {
                // Register Excel services
                containerRegistry.RegisterSingleton<IExcelReaderService, ExcelReaderService>();
                Log.Information("✓ Registered IExcelReaderService as singleton");

                // Register Excel Export service (NEW)
                containerRegistry.RegisterSingleton<WileyWidget.Services.Export.IExcelExportService, WileyWidget.Services.Export.ExcelExportService>();
                Log.Information("✓ Registered IExcelExportService as singleton");

                // Register Theme service (NEW)
                containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
                Log.Information("✓ Registered IThemeService as singleton");

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
                // Register AI Assist as a Prism Dialog for modal prompts
                containerRegistry.RegisterDialog<WileyWidget.AIAssistView, ViewModels.AIAssistViewModel>("AIAssistDialog");
                Log.Information("✓ Registered Prism Dialogs (Confirmation, Notification, Warning, Error, Settings)");

                // Register Views for Navigation (recommended)
                containerRegistry.RegisterForNavigation<WileyWidget.DashboardView, ViewModels.DashboardViewModel>("DashboardView");
                containerRegistry.RegisterForNavigation<WileyWidget.EnterpriseView, ViewModels.EnterpriseViewModel>("EnterpriseView");
                containerRegistry.RegisterForNavigation<WileyWidget.BudgetView, ViewModels.BudgetViewModel>("BudgetView");
                containerRegistry.RegisterForNavigation<WileyWidget.Views.MunicipalAccountView, ViewModels.MunicipalAccountViewModel>("MunicipalAccountView");
                containerRegistry.RegisterForNavigation<WileyWidget.UtilityCustomerView, ViewModels.UtilityCustomerViewModel>("UtilityCustomerView");
                containerRegistry.RegisterForNavigation<WileyWidget.ReportsView, ViewModels.ReportsViewModel>("ReportsView");
                containerRegistry.RegisterForNavigation<WileyWidget.AnalyticsView, ViewModels.AnalyticsViewModel>("AnalyticsView");
                containerRegistry.RegisterForNavigation<WileyWidget.QuickBooksView, ViewModels.QuickBooksViewModel>("QuickBooksView");
                containerRegistry.RegisterForNavigation<WileyWidget.Views.ExcelImportView, ViewModels.ExcelImportViewModel>("ExcelImportView");
                containerRegistry.RegisterForNavigation<WileyWidget.SettingsView, ViewModels.SettingsViewModel>("SettingsView");
                containerRegistry.RegisterForNavigation<WileyWidget.AIAssistView, ViewModels.AIAssistViewModel>("AIAssistView");

                // Panel views (no dedicated ViewModels)
                containerRegistry.RegisterForNavigation<Views.DashboardPanelView>("DashboardPanelView");
                containerRegistry.RegisterForNavigation<Views.EnterprisePanelView>("EnterprisePanelView");
                containerRegistry.RegisterForNavigation<Views.BudgetPanelView>("BudgetPanelView");
                containerRegistry.RegisterForNavigation<Views.MunicipalAccountPanelView>("MunicipalAccountPanelView");
                containerRegistry.RegisterForNavigation<Views.SettingsPanelView>("SettingsPanelView");
                containerRegistry.RegisterForNavigation<Views.ToolsPanelView>("ToolsPanelView");
                containerRegistry.RegisterForNavigation<Views.AIAssistPanelView>("AIAssistPanelView");
                containerRegistry.RegisterForNavigation<Views.UtilityCustomerPanelView>("UtilityCustomerPanelView");
                Log.Information("✓ Registered Views for navigation (core, analytics, settings, panels)");

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
                containerRegistry.RegisterSingleton<Prism.Events.IEventAggregator, Prism.Events.EventAggregator>();
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
                containerRegistry.Register<EnterpriseViewModel>();

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
            try
            {
                RegisterConventions(containerRegistry);
                ValidateAndRegisterViewModels(containerRegistry);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Convention-based registration encountered an error; continuing with explicit registrations only");
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
                    catch (Exception)
                    {
                        // Fallback: allow Activator to create the ViewModel if it's not registered in the container
                        return Activator.CreateInstance(viewModelType);
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

                    void TryResolve(Type t, string name = null)
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
                    TryResolve(typeof(WileyWidget.Business.Interfaces.IMunicipalAccountRepository));
                    TryResolve(typeof(WileyWidget.Data.MunicipalAccountRepository));
                    TryResolve(typeof(WileyWidget.Services.IWhatIfScenarioEngine));
                    TryResolve(typeof(WileyWidget.Services.WhatIfScenarioEngine));
                    TryResolve(typeof(WileyWidget.Services.IChargeCalculatorService));
                    TryResolve(typeof(WileyWidget.Services.ServiceChargeCalculatorService));

                    // Also check EF factory; skip direct AppDbContext construction here to avoid eager DB touch
                    TryResolve(typeof(Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>));
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

            // Validate public accessibility to prevent InvalidRegistrationException
            ValidatePublicAccessibility();
        }

    private void TryRegisterImplementationByName(PrismIoc.IContainerRegistry containerRegistry, Type interfaceType, string implementationFullName)
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
        /// </summary>
        /// <param name="containerRegistry">The container registry to validate</param>
    private void ValidateCriticalServices(PrismIoc.IContainerRegistry containerRegistry, bool testMode)
        {
            Log.Information("Validating critical service registrations...");

            (string ServiceName, Type ServiceType)[] criticalServices = new[]
            {
                ("IConfiguration", typeof(IConfiguration)),
                ("ILoggerFactory", typeof(ILoggerFactory)),
                ("ISettingsService", typeof(ISettingsService)),
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

            List<string> validationErrors = new List<string>();

            foreach ((string serviceName, Type serviceType) in criticalServices)
            {
                try
                {
                    IContainer validationContainer = containerRegistry.GetContainer();
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
    /// Ensures Prism and the DI container remain the single composition root by validating container state and legacy configuration.
        /// </summary>
        /// <param name="containerRegistry">The active Prism container registry</param>
    private void ValidatePrismInfrastructure(PrismIoc.IContainerRegistry containerRegistry)
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

            Log.Information("Container registration count: {RegistrationCount}", registrationCount >= 0 ? registrationCount.ToString() : "unknown");
        }

    private void RegisterAppOptions(PrismIoc.IContainerRegistry containerRegistry, IConfiguration configuration)
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

                try
                {
                    // Try to obtain AppOptionsConfigurator from the Prism container provider; avoid depending on DryIoc directly
                    try
                    {
                        var configurator = ResolveWithRetry<AppOptionsConfigurator>();
                        configurator.Configure(appOptions);
                    }
                    catch (Exception)
                    {
                        // If configurator isn't registered or fails, continue with configuration-only values
                        throw;
                    }
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
        private static void ValidateContainerRegistrations(IContainer container)
        {
            // Test resolution of critical services
            var criticalTypes = new[]
            {
                typeof(IConfiguration),
                typeof(ISettingsService),
                typeof(IRegionManager),
                typeof(IEventAggregator),
                typeof(Prism.Dialogs.IDialogService),
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
    private static void RegisterConventions(PrismIoc.IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null) throw new ArgumentNullException(nameof(containerRegistry));

            int registeredCount = 0;
            var suffixesForSingleton = new[] { "Service", "Repository", "Provider", "Engine" };

            var regSingletonTwo = typeof(PrismIoc.IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "RegisterSingleton" && m.IsGenericMethod && m.GetGenericArguments().Length == 2);
            var regSingletonOne = typeof(PrismIoc.IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "RegisterSingleton" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);
            var regRegisterTwo = typeof(PrismIoc.IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "Register" && m.IsGenericMethod && m.GetGenericArguments().Length == 2);
            var regRegisterOne = typeof(PrismIoc.IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "Register" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);

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
                    if (t.Namespace == null || !t.Namespace.StartsWith("WileyWidget")) continue;

                    var name = t.Name;

                    try
                    {
                        // Services / repositories / providers -> prefer to register against I{TypeName} when available
                        if (suffixesForSingleton.Any(s => name.EndsWith(s, StringComparison.Ordinal)))
                        {
                            var interfaces = t.GetInterfaces().Where(i => i != null && i.Namespace != null && i.Namespace.StartsWith("WileyWidget")).ToArray();
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
    private static void ValidateAndRegisterViewModels(PrismIoc.IContainerRegistry containerRegistry)
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

            var regRegisterOne = typeof(Prism.Ioc.IContainerRegistry).GetMethods().FirstOrDefault(m => m.Name == "Register" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);
            var prismContainer = containerRegistry.GetContainer();

            foreach (var view in viewTypes)
            {
                try
                {
                    // Candidate VM name: prefer WileyWidget.ViewModels.{ViewName}Model
                    var vmName = (view.Namespace != null && view.Namespace.Contains("Views"))
                        ? view.Namespace.Replace(".Views", ".ViewModels") + "." + view.Name.Replace("View", "ViewModel")
                        : "WileyWidget.ViewModels." + view.Name.Replace("View", "ViewModel");

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
    private void RegisterAIIntegrationServices(PrismIoc.IContainerRegistry containerRegistry)
        {
            Log.Information("=== Registering AI Integration Services (Phase 1 - Production) ===");

            try
            {
                // 0. Register Application Insights Telemetry (Singleton)
                // Production telemetry for AI service monitoring and performance tracking
                // NOTE: Commented out until Azure/Application Insights is configured
                /*
                var config = ResolveWithRetry<IConfiguration>();
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



        // Cache configuration to avoid redundant loading
        private IConfiguration? _cachedConfiguration;

    protected override void ConfigureModuleCatalog(Prism.Modularity.IModuleCatalog moduleCatalog)
        {
            Log.Information("=== Configuring Prism Module Catalog (explicit registration) ===");
            try
            {
                WileyWidget.Startup.Modules.CustomModuleManager.RegisterModules(moduleCatalog);
                Log.Information("✓ Registered modules via CustomModuleManager");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure module catalog via CustomModuleManager");
                throw;
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
            catch (Prism.Modularity.ModuleInitializeException ex)
            {
                // Handle module initialization exceptions with detailed diagnostics
                var rootEx = WileyWidget.Startup.Modules.PrismExceptionExtensions.GetRootException(ex);
                Log.Error(rootEx, "Critical ModuleInitializeException during module initialization: {Message}", rootEx.Message);
                Log.Error("Full exception chain: {DetailedMessage}", WileyWidget.Startup.Modules.PrismExceptionExtensions.GetDetailedMessage(ex));

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
                Log.Error(WileyWidget.Startup.Modules.PrismExceptionExtensions.GetRootException(ex), "Unexpected error during module initialization: {Message}", WileyWidget.Startup.Modules.PrismExceptionExtensions.GetRootException(ex).Message);
                Log.Error("Full exception chain: {DetailedMessage}", WileyWidget.Startup.Modules.PrismExceptionExtensions.GetDetailedMessage(ex));
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
                var eventAggregator = ResolveWithRetry<Prism.Events.IEventAggregator>();

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

                // Feature modules - load on demand to improve startup performance
                ["DashboardModule"] = InitializationMode.WhenAvailable,
                ["SettingsModule"] = InitializationMode.WhenAvailable,
                ["EnterpriseModule"] = InitializationMode.OnDemand,
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
            // Align expected regions with actual regions declared in Shell.xaml
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
                ["SettingsModule"] = new[] { "SettingsRegion" }
            };

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

                // AutoWireViewModelCreated subscription removed: prefer Prism's IContainerProvider + ViewModelLocationProvider
                // for resolving and creating ViewModels. Custom runtime validation or conventions can be implemented
                // using container-based validation or as part of build-time analyzers. See RegisterTypes() where
                // ViewModel factory is configured to resolve from the DI container.

                // Apply Syncfusion theme globally as early as possible per official docs
                // Reference: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
                try
                {
                    Log.Debug("Initializing SfSkinManager theme...");
                    // CRITICAL: Set theme FIRST before loading resources that use DynamicResource
                    // Use the official API to inherit theme to all controls/windows
                    SfSkinManager.ApplyThemeAsDefaultStyle = true;
#pragma warning disable CA2000 // Theme objects are managed by SfSkinManager
                    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
#pragma warning restore CA2000

                    Log.Information("SfSkinManager initialized with FluentLight theme globally");
                    Log.Debug("SfSkinManager initialization completed successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize SfSkinManager theme - falling back to default");
                    Log.Debug("SfSkinManager initialization failed: {Message}", ex.Message);
                    // Fallback to FluentLight if FluentDark is unavailable for any reason
                    try
                    {
#pragma warning disable CA2000
                        SfSkinManager.ApplicationTheme = new Theme("FluentLight");
#pragma warning restore CA2000
                        Log.Debug("SfSkinManager fallback to FluentLight succeeded");
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(fallbackEx, "FluentLight fallback also failed - continuing without theme");
                        Log.Debug("SfSkinManager fallback failed: {Message}", fallbackEx.Message);
                    }
                }

                Log.Information("Application initialization completed successfully");
                Log.Information("All services registered and container ready for use");

                Application.Current.MainWindow?.Show();

                // Log module health report after initialization
                try
                {
                    var moduleHealthService = ResolveWithRetry<IModuleHealthService>();
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
    private void RegisterModuleWithHealthTracking(PrismModularity.IModuleCatalog moduleCatalog, IModuleHealthService healthService, string moduleName, Action registerAction)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Prism's DryIocContainerExtension takes ownership of the DryIoc Container and will dispose it when appropriate.")]
    protected override Prism.Ioc.IContainerExtension CreateContainerExtension()
        {
            return new DryIocContainerExtension();
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

            // Add WPF binding error tracing to Serilog
            var bindingTraceListener = new WileyWidget.Diagnostics.BindingErrorTraceListener();
            Trace.Listeners.Add(bindingTraceListener);
            PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTraceListener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            Log.Information("=== WileyWidget Prism application startup ===");
            Log.Information("✓ WPF binding error tracing enabled");
        }

        private IConfiguration BuildConfiguration()
        {
            // Load .env file from project directory
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = System.IO.Path.GetDirectoryName(assemblyLocation);
            var projectDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(assemblyDir)); // bin\Debug\net9.0-windows -> project root
            var envPath = System.IO.Path.Combine(projectDir, ".env");
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
            configurationRoot.ResolvePlaceholders();

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
            Log.Information("Application shutdown initiated - Session: {StartupId}", _startupId);

            try
            {
                // Best-effort: re-register Syncfusion license in case some Syncfusion component needs it during shutdown
                EnsureSyncfusionLicenseRegistered(forceRefresh: true);

                base.OnExit(e);
                Log.Information("Application shutdown completed successfully - Session: {StartupId}", _startupId);
                Log.Information("Application exited with code: {ExitCode} - Session: {StartupId}", e.ApplicationExitCode, _startupId);
                Log.Information("=== Application Shutdown Complete - Session: {StartupId} ===", _startupId);
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
                                var traceSource = new System.Diagnostics.TraceSource(sourceName);
                                if (Enum.TryParse<System.Diagnostics.SourceLevels>(switchValue, out var level))
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
                                            traceSource.Listeners.Add(new System.Diagnostics.DefaultTraceListener());
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
                    Log.Error("Type {Type} is not public - this may cause InvalidRegistrationException", type.FullName);
                    throw new InvalidOperationException($"Type {type.FullName} must be public for DI container registration");
                }

                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                if (constructors.Length == 0)
                {
                    Log.Error("Type {Type} has no public constructors - this may cause InvalidRegistrationException", type.FullName);
                    throw new InvalidOperationException($"Type {type.FullName} must have at least one public constructor");
                }
            }

            Log.Information("✓ Public accessibility validation passed for {Count} types", typesToValidate.Length);
        }
    }

}
