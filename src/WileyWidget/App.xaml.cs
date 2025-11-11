// App.xaml.cs - Refactored WileyWidget Prism WPF Application Bootstrapper
//
// PARTIAL CLASS STRUCTURE (Phase 0-1 Complete, 2025-11-09):
// This class is split into 6 partial files (~2,000 LOC total) for maintainability:
//
// 1. App.xaml.cs (555 LOC) - Main entry point
//    - Assembly resolution infrastructure
//    - Static helper utilities
//    - Public API fields (ModuleOrder, ModuleRegionMap)
//    - App constructor and CloseAllDialogWindows
//
// 2. App.DependencyInjection.cs (749 LOC) - DI container & Prism configuration
//    - CreateContainerExtension: DryIoc setup and rules
//    - RegisterTypes: Critical service registrations
//    - ConfigureModuleCatalog: Module catalog setup (CoreModule, QuickBooksModule)
//    - ConfigureDefaultRegionBehaviors: Custom region behaviors
//    - ConfigureRegionAdapterMappings: Syncfusion region adapters (requires theme)
//    - RegisterConventionTypes, RegisterCoreInfrastructure, RegisterRepositories, etc.
//    - BuildConfiguration: IConfiguration builder with caching
//
// 3. App.Lifecycle.cs (656 LOC) - Application lifecycle management
//    - OnStartup: 4-phase startup (validation → Prism bootstrap)
//    - OnInitialized: Module and service initialization
//    - OnExit: Graceful shutdown and cleanup
//    - CreateShell: Shell window creation
//    - InitializeModules: Custom module initialization with retry logic
//
// 4. App.Telemetry.cs - Telemetry & observability
//    - InitializeSigNozTelemetry: Distributed tracing setup
//    - IntegrateTelemetryServices: Metrics and monitoring integration
//    - SigNoz Activity spans and trace correlation
//
// 5. App.Resources.cs - Resource & theme management
//    - LoadApplicationResourcesSync: Synchronous WPF resource loading
//    - VerifyAndApplyTheme: Syncfusion theme application (fail-fast if memory insufficient)
//    - Pack URI resolution and error handling
//
// 6. App.ExceptionHandling.cs - Global exception handling
//    - SetupGlobalExceptionHandling: Wire up exception handlers
//    - DispatcherUnhandledException handler
//    - EventAggregator error subscriptions
//    - ShowEmergencyErrorDialog: Last-resort error UI
//
// Key Refactoring Changes:
// - Standardized Prism bootstrap flow: Custom early init in OnStartup before base.OnStartup
// - Eliminated duplicate module initialization: Custom logic moved to override InitializeModules()
// - Deferred container resolutions with Lazy<T> where possible to avoid early failures
// - Extracted static caches and helpers for performance (e.g., assembly scanning)
// - Removed unused methods and dead code (Phase 0: deleted 11 modules, Bootstrapper.cs, WPFTMP support)
// - Integrated global error handling into SetupGlobalExceptionHandling()
// - Config-driven timeouts removed; modules hardcoded in ConfigureModuleCatalog (2 active)
// - Ensured Syncfusion theme/license registration per docs: https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
// - Aligned with Wiley-Widget GitHub patterns: Modular, resilient startup with health checks

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prism;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Dialogs;
using Prism.Navigation.Regions;
using Prism.Events;
using Prism.Container.DryIoc;
using DryIoc;
using Syncfusion.SfSkinManager;
using Syncfusion.Licensing;
using Bold.Licensing;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using Polly;
using DotNetEnv;
using WileyWidget.Views;
using WileyWidget.Views.Main;
using WileyWidget.Views.Panels;
using WileyWidget.Views.Dialogs;
using WileyWidget.Views.Windows;
using WileyWidget.Startup.Modules;
using WileyWidget.Startup;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;
using WileyWidget.Abstractions;
using WileyWidget.Configuration;
using WileyWidget.Configuration.Resilience;
using WileyWidget.Data;
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
using WileyWidget.ViewModels.Messages;
using System.Diagnostics.CodeAnalysis;  // For SuppressMessage
using Serilog.Events;  // For LogEventLevel

// Aliases for Prism types
using IContainerRegistry = Prism.Ioc.IContainerRegistry;
using IModuleCatalog = Prism.Modularity.IModuleCatalog;
using IContainerExtension = Prism.Ioc.IContainerExtension;

namespace WileyWidget
{
    public partial class App : Prism.DryIoc.PrismApplication
    {
        #region Assembly Resolution Infrastructure

        // Assembly resolution cache to avoid repeated file system lookups
        private static readonly ConcurrentDictionary<string, Assembly?> _resolvedAssemblies = new();

        // Known NuGet package prefixes that we may need to resolve at runtime
        private static readonly HashSet<string> _knownPackagePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Prism", "DryIoc", "Syncfusion", "Bold", "Serilog",
            "Microsoft.Extensions", "Microsoft.EntityFrameworkCore",
            "System.Text.Json", "Polly", "Microsoft.Data",
            "Microsoft.Xaml", "System.Runtime"
        };

        // Dynamic NuGet global packages directory detection with multiple fallbacks
        private static readonly Lazy<string?> _nugetPackagesPath = new(() =>
        {
            // Priority 1: Environment variable (allows custom NuGet configuration)
            var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrEmpty(nugetPackages) && Directory.Exists(nugetPackages))
            {
                Log.Debug("Using NUGET_PACKAGES environment variable: {Path}", nugetPackages);
                return nugetPackages;
            }

            // Priority 2: Try to get actual global-packages folder from dotnet CLI
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "nuget locals global-packages --list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(output, @"global-packages:\s*(.+)");
                        if (match.Success)
                        {
                            var path = match.Groups[1].Value.Trim();
                            if (Directory.Exists(path))
                            {
                                Log.Debug("Detected NuGet global-packages folder via dotnet CLI: {Path}", path);
                                return path;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to detect NuGet global-packages folder via dotnet CLI");
            }

            // Priority 3: Standard fallback locations
            var fallbackLocations = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "v3-cache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "packages")
            };

            foreach (var location in fallbackLocations)
            {
                if (Directory.Exists(location))
                {
                    Log.Debug("Using fallback NuGet packages location: {Path}", location);
                    return location;
                }
            }

            Log.Warning("No valid NuGet packages directory found - assembly resolution may fail for NuGet packages");
            return null;
        });

        // Target framework monikers to probe in priority order
        private static readonly string[] _targetFrameworks =
        {
            "net9.0-windows10.0.19041.0",
            "net9.0-windows",
            "net9.0",
            "net8.0",
            "net6.0",
            "netstandard2.1",
            "netstandard2.0",
            "netstandard1.6"
        };

        /// <summary>
        /// Handles assembly resolution failures by probing multiple locations for NuGet package assemblies.
        /// This is a last-resort fallback when normal probing paths fail.
        /// </summary>
        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                // Parse the assembly name
                var assemblyName = new AssemblyName(args.Name);
                var simpleName = assemblyName.Name ?? string.Empty;

                // Check cache first for performance
                if (_resolvedAssemblies.TryGetValue(args.Name, out var cachedAssembly))
                {
                    return cachedAssembly;
                }

                // Suppress warnings for satellite resource assemblies (localization) - these are optional
                // Examples: Syncfusion.Themes.FluentLight.WPF.resources, Prism.Wpf.resources
                if (simpleName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedAssemblies.TryAdd(args.Name, null);
                    return null; // Silent fail for resource assemblies
                }

                // Only attempt to resolve known NuGet packages to avoid interfering with system assemblies
                if (!_knownPackagePrefixes.Any(prefix => simpleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    _resolvedAssemblies.TryAdd(args.Name, null);
                    return null;
                }

                var dllName = simpleName + ".dll";

                // Probe locations in priority order:
                // 1. Application base directory (bin) - most likely with CopyLocalLockFileAssemblies
                var appBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName);
                if (File.Exists(appBasePath))
                {
                    var assembly = Assembly.LoadFrom(appBasePath);
                    _resolvedAssemblies.TryAdd(args.Name, assembly);
                    Log.Information("Assembly resolved from app directory: {AssemblyName} -> {Path}", simpleName, appBasePath);
                    return assembly;
                }

                // 2. Subdirectories defined in App.config probing paths
                var probePaths = new[] { "bin", "lib", "packages", "bin/plugins", "lib/syncfusion" };
                foreach (var probePath in probePaths)
                {
                    var fullProbePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, probePath, dllName);
                    if (File.Exists(fullProbePath))
                    {
                        var assembly = Assembly.LoadFrom(fullProbePath);
                        _resolvedAssemblies.TryAdd(args.Name, assembly);
                        Log.Information("Assembly resolved from probe path: {AssemblyName} -> {Path}", simpleName, fullProbePath);
                        return assembly;
                    }
                }

                // 3. NuGet global packages cache - probe multiple target frameworks
                var nugetPath = _nugetPackagesPath.Value;
                if (!string.IsNullOrEmpty(nugetPath) && Directory.Exists(nugetPath))
                {
                    var packagePath = Path.Combine(nugetPath, simpleName.ToLowerInvariant());
                    if (Directory.Exists(packagePath))
                    {
                        // Find the most recent version directory
                        var versionDirs = Directory.GetDirectories(packagePath)
                            .Select(d => new DirectoryInfo(d))
                            .OrderByDescending(d => d.Name)
                            .ToArray();

                        foreach (var versionDir in versionDirs)
                        {
                            foreach (var tfm in _targetFrameworks)
                            {
                                var libPath = Path.Combine(versionDir.FullName, "lib", tfm, dllName);
                                if (File.Exists(libPath))
                                {
                                    try
                                    {
                                        var assembly = Assembly.LoadFrom(libPath);
                                        _resolvedAssemblies.TryAdd(args.Name, assembly);
                                        Log.Information("Assembly resolved from NuGet cache: {AssemblyName} -> {Path}", simpleName, libPath);
                                        return assembly;
                                    }
                                    catch (Exception loadEx)
                                    {
                                        Log.Debug(loadEx, "Failed to load assembly from NuGet path: {Path}", libPath);
                                        // Continue trying other paths
                                    }
                                }
                            }
                        }
                    }
                }
                else if (string.IsNullOrEmpty(nugetPath))
                {
                    Log.Debug("No NuGet packages path available for assembly resolution: {AssemblyName}", simpleName);
                }

                // 4. Additional fallback: check GAC and other standard locations
                try
                {
                    // Attempt to load from GAC or other standard locations
                    var gacAssembly = Assembly.Load(assemblyName);
                    if (gacAssembly != null)
                    {
                        _resolvedAssemblies.TryAdd(args.Name, gacAssembly);
                        Log.Information("Assembly resolved from GAC: {AssemblyName}", simpleName);
                        return gacAssembly;
                    }
                }
                catch (Exception gacEx)
                {
                    Log.Debug(gacEx, "Failed to load assembly from GAC: {AssemblyName}", simpleName);
                }

                // Assembly not found - cache null result to avoid repeated lookups
                _resolvedAssemblies.TryAdd(args.Name, null);
                Log.Warning("Failed to resolve assembly: {AssemblyName} (requested by {RequestingAssembly})",
                    simpleName, args.RequestingAssembly?.FullName ?? "unknown");
                return null;
            }
            catch (Exception ex)
            {
                // Don't throw from AssemblyResolve - log and return null
                Log.Error(ex, "Error in AssemblyResolve handler for {AssemblyName}", args.Name);
                return null;
            }
        }

        #endregion

        #region License Management and Status Tracking

        /// <summary>
        /// License registration status tracking for runtime validation
        /// </summary>
        public enum LicenseRegistrationStatus
        {
            NotAttempted,
            Success,
            TrialMode,
            Failed,
            InvalidKey,
            NetworkError
        }

        // Static license status tracking
        private static LicenseRegistrationStatus _syncfusionLicenseStatus = LicenseRegistrationStatus.NotAttempted;
        private static LicenseRegistrationStatus _boldLicenseStatus = LicenseRegistrationStatus.NotAttempted;
        private static string _syncfusionLicenseError = string.Empty;
        private static string _boldLicenseError = string.Empty;

        /// <summary>
        /// Gets the current Syncfusion license registration status
        /// </summary>
        public static LicenseRegistrationStatus SyncfusionLicenseStatus => _syncfusionLicenseStatus;

        /// <summary>
        /// Gets the current Bold Reports license registration status
        /// </summary>
        public static LicenseRegistrationStatus BoldLicenseStatus => _boldLicenseStatus;

        /// <summary>
        /// Gets the last Syncfusion license error message (if any)
        /// </summary>
        public static string SyncfusionLicenseError => _syncfusionLicenseError;

        /// <summary>
        /// Gets the last Bold Reports license error message (if any)
        /// </summary>
        public static string BoldLicenseError => _boldLicenseError;

        #endregion

        // Static constructor: Register Syncfusion licenses BEFORE any instance members or controls
        // Per Syncfusion docs: https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
        // This runs once, before any App instance is created
        static App()
        {
            // Initialize comprehensive Serilog logger FIRST - before assembly resolver that uses Log
            // This prevents NullReferenceException if assembly resolution happens early
            // Configured with all diagnostic features from the start (no hot-swapping needed)
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()  // Capture all debug messages
                .Enrich.WithMachineName()  // Add machine name for distributed debugging
                .Enrich.WithProcessId()  // Add process ID for multi-process scenarios
                .Enrich.WithThreadId()  // Add thread ID for async debugging
                .Enrich.FromLogContext()  // Allow contextual properties
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/wiley-widget-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    shared: true,  // Allow multiple processes to write
                    flushToDiskInterval: TimeSpan.FromSeconds(5))  // Flush every 5 seconds for better diagnostics
                .WriteTo.File(
                    path: "logs/startup-diagnostic-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u4}] {MachineName} {ProcessId}:{ThreadId} {SourceContext}{NewLine}    {Message:lj}{NewLine}{Exception}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();

            // Register assembly resolution handler as early as possible
            // This provides a fallback when normal probing paths fail
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // Read license keys directly from environment variables (no complex configuration loading)
            var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            var boldKey = Environment.GetEnvironmentVariable("BOLD_LICENSE_KEY");

            // Enhanced Syncfusion license registration with specific error handling
            if (!string.IsNullOrWhiteSpace(syncfusionKey) && !syncfusionKey.StartsWith("${"))
            {
                try
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.Success;
                    System.Diagnostics.Debug.WriteLine("✓ Syncfusion license registered successfully from SYNCFUSION_LICENSE_KEY");
                }
                catch (ArgumentException argEx) when (argEx.Message.Contains("license") || argEx.Message.Contains("key"))
                {
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.InvalidKey;
                    _syncfusionLicenseError = argEx.Message;
                    System.Diagnostics.Debug.WriteLine($"✗ Invalid Syncfusion license key: {argEx.Message}");
                }
                catch (System.Net.WebException webEx)
                {
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.NetworkError;
                    _syncfusionLicenseError = webEx.Message;
                    System.Diagnostics.Debug.WriteLine($"✗ Network error during Syncfusion license validation: {webEx.Message}");
                }
                catch (Exception ex)
                {
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.Failed;
                    _syncfusionLicenseError = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"✗ Unexpected error registering Syncfusion license: {ex.Message}");
                }
            }
            else
            {
                _syncfusionLicenseStatus = LicenseRegistrationStatus.TrialMode;
                _syncfusionLicenseError = "License key not configured";
                System.Diagnostics.Debug.WriteLine("⚠ SYNCFUSION_LICENSE_KEY not set - application will run in trial mode");
                System.Diagnostics.Debug.WriteLine("  Get FREE Community License: https://www.syncfusion.com/account/downloads");
            }

            // Enhanced Bold Reports license registration with fallback logic
            var boldLicenseKey = !string.IsNullOrWhiteSpace(boldKey) && !boldKey.StartsWith("${") ? boldKey : syncfusionKey;
            if (!string.IsNullOrWhiteSpace(boldLicenseKey) && !boldLicenseKey.StartsWith("${"))
            {
                try
                {
                    Bold.Licensing.BoldLicenseProvider.RegisterLicense(boldLicenseKey);
                    _boldLicenseStatus = LicenseRegistrationStatus.Success;
                    System.Diagnostics.Debug.WriteLine("✓ Bold Reports license registered successfully");
                }
                catch (ArgumentException argEx) when (argEx.Message.Contains("license") || argEx.Message.Contains("key"))
                {
                    _boldLicenseStatus = LicenseRegistrationStatus.InvalidKey;
                    _boldLicenseError = argEx.Message;
                    System.Diagnostics.Debug.WriteLine($"✗ Invalid Bold Reports license key: {argEx.Message}");
                }
                catch (System.Net.WebException webEx)
                {
                    _boldLicenseStatus = LicenseRegistrationStatus.NetworkError;
                    _boldLicenseError = webEx.Message;
                    System.Diagnostics.Debug.WriteLine($"✗ Network error during Bold Reports license validation: {webEx.Message}");
                }
                catch (Exception ex)
                {
                    _boldLicenseStatus = LicenseRegistrationStatus.Failed;
                    _boldLicenseError = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"✗ Unexpected error registering Bold Reports license: {ex.Message}");
                }
            }
            else
            {
                _boldLicenseStatus = LicenseRegistrationStatus.TrialMode;
                _boldLicenseError = "License key not configured";
                System.Diagnostics.Debug.WriteLine("⚠ Bold Reports license key not set - will use trial mode or Syncfusion license");
            }

            // Log final license status summary
            System.Diagnostics.Debug.WriteLine($"[LICENSE SUMMARY] Syncfusion: {_syncfusionLicenseStatus}, Bold Reports: {_boldLicenseStatus}");
        }

        // Deferred secrets task, lifecycle fields, telemetry tracking moved to App.Lifecycle.cs

        // Config-driven module ordering and regions (load from appsettings.json in BuildConfiguration)
        /// <summary>Config-driven module-to-region mapping, public read-only.</summary>
        public static IReadOnlyDictionary<string, string[]> ModuleRegionMap { get; private set; } = new Dictionary<string, string[]>();
        /// <summary>Config-driven module initialization order, public read-only.</summary>
        public static IReadOnlyList<string> ModuleOrder { get; private set; } = Array.Empty<string>();

        // Static caches for performance
        private static readonly ConcurrentDictionary<string, Type?> TypeByShortNameCache = new();
        private static readonly ConcurrentDictionary<string, string?> ModuleTypeNameCache = new();

        // Startup metadata and early container for 4-phase startup
        private static readonly object StartupProgressSyncRoot = new();
        public static object? StartupProgress { get; private set; }
        public static DateTimeOffset? LastHealthReportUpdate { get; private set; }

        // Config-driven timeouts (from appsettings.json)
        private static TimeSpan SecretsTimeout => TimeSpan.FromSeconds(GetConfigValue("Startup:SecretsTimeoutSeconds", 30));
        private static TimeSpan BriefAwaitTimeout => TimeSpan.FromSeconds(GetConfigValue("Startup:BriefAwaitTimeoutSeconds", 5));
        private static int MaxResolveRetries => GetConfigValue("Startup:MaxResolveRetries", 3);

        private static int GetConfigValue(string key, int defaultValue) => int.TryParse(GetConfigValue<string>(key, defaultValue.ToString()), out var val) ? val : defaultValue;
        private static T GetConfigValue<T>(string key, T defaultValue)
        {
            // Fallback to env var or static config (in full impl, resolve IConfiguration)
            var envVal = Environment.GetEnvironmentVariable(key.Replace(":", "_").ToUpper());
            return string.IsNullOrEmpty(envVal) ? defaultValue : (T)Convert.ChangeType(envVal, typeof(T));
        }

        /// <summary>
        /// Finds a type by short name from loaded assemblies with caching for performance.
        /// Used for region adapter registration where types may not be loaded yet.
        /// Enhanced with better ReflectionTypeLoadException handling.
        /// </summary>
        /// <param name="shortName">Short type name (e.g., "DockingManager", "SfDataGrid")</param>
        /// <returns>Type if found, null otherwise</returns>
        private static Type? FindLoadedTypeByShortName(string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
                return null;

            // Check cache first
            if (TypeByShortNameCache.TryGetValue(shortName, out var cachedType))
                return cachedType;

            try
            {
                // Search in loaded assemblies with improved error handling
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !IsWpfTmpAssembly(a)) // Filter out WPFTMP assemblies
                    .ToArray();

                foreach (var assembly in loadedAssemblies)
                {
                    try
                    {
                        // First try the normal path
                        var type = assembly.GetTypes()
                            .FirstOrDefault(t => t.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase));

                        if (type != null)
                        {
                            TypeByShortNameCache.TryAdd(shortName, type);
                            Log.Debug("Found type {TypeName} in assembly {AssemblyName}", type.FullName, assembly.FullName);
                            return type;
                        }
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        // Handle partial type loading - some types may have loaded successfully
                        Log.Debug("ReflectionTypeLoadException in assembly {AssemblyName}, checking successfully loaded types", assembly.FullName);

                        try
                        {
                            var successfullyLoadedTypes = rtle.Types.Where(t => t != null);
                            var type = successfullyLoadedTypes
                                .FirstOrDefault(t => t!.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase));

                            if (type != null)
                            {
                                TypeByShortNameCache.TryAdd(shortName, type);
                                Log.Debug("Found type {TypeName} in partially loaded assembly {AssemblyName}", type.FullName, assembly.FullName);
                                return type;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Log.Debug(innerEx, "Error processing partially loaded types from assembly {AssemblyName}", assembly.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error searching types in assembly {AssemblyName}", assembly.FullName);
                    }
                }

                // Not found - cache null to avoid repeated searches
                TypeByShortNameCache.TryAdd(shortName, null);
                Log.Debug("Type {ShortName} not found in loaded assemblies", shortName);
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error finding type by short name: {ShortName}", shortName);
                TypeByShortNameCache.TryAdd(shortName, null);
                return null;
            }
        }

        /// <summary>
        /// Determines if an assembly is a WPF temporary project assembly that should be excluded from type scanning.
        /// </summary>
        private static bool IsWpfTmpAssembly(Assembly assembly)
        {
            try
            {
                var location = assembly.Location;
                var fullName = assembly.FullName;

                // Check for wpftmp in assembly location or full name
                return (!string.IsNullOrEmpty(location) && location.Contains("wpftmp", StringComparison.OrdinalIgnoreCase)) ||
                       (!string.IsNullOrEmpty(fullName) && fullName.Contains("wpftmp", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // If we can't determine, err on the side of caution and include it
                return false;
            }
        }

        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);

        // UpdateLatestHealthReport moved to Services.Startup.HealthReportingService (Phase 2: TODO 2.3)
        // RevealErrorsAndWarnings moved to Services.Startup.DiagnosticsService (Phase 2: TODO 2.4)

        // OnInitialized() moved to App.Lifecycle.cs
        // CreateShell() moved to App.Lifecycle.cs
        // InitializeShell() moved to App.Lifecycle.cs
        // SetupGlobalExceptionHandling() moved to App.ExceptionHandling.cs

        // CreateShell(), InitializeShell(), InitializeModules() moved to App.Lifecycle.cs

        // ValidateModuleInitialization moved to Services.Startup.StartupEnvironmentValidator (Phase 2: TODO 2.2)

        private static bool IsTransientModuleException(Exception ex)
        {
            // ... (your existing logic)
            if (ex is TypeLoadException || ex is FileLoadException || ex is TimeoutException /* ... */)
                return true;
            // ... rest
            return false;
        }

        private static Type? FindTypeByShortName(string shortName)
        {
            if (string.IsNullOrWhiteSpace(shortName)) return null;
            return TypeByShortNameCache.GetOrAdd(shortName, name =>
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsWpfTmpAssembly(a)))
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t?.Name == name || t?.FullName?.EndsWith("." + name) == true) return t;
                        }
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        // Handle partial loading - check successfully loaded types
                        try
                        {
                            foreach (var t in rtle.Types?.Where(t => t != null) ?? Enumerable.Empty<Type>())
                            {
                                if (t.Name == name || t.FullName?.EndsWith("." + name) == true) return t;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Log.Debug(innerEx, "Error processing partially loaded types from assembly {AssemblyName}", asm.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error scanning assembly {AssemblyName} for type {TypeName}", asm.FullName, name);
                    }
                }
                return null;
            });
        }

        private static string? TryResolveModuleTypeName(string moduleName)
        {
            return ModuleTypeNameCache.GetOrAdd(moduleName, name =>
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t?.Name == name || t?.FullName?.EndsWith("." + name) == true)
                            {
                                return t.AssemblyQualifiedName;
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException) { /* Handled in FindTypeByShortName */ }
                }
                return null;
            });
        }

        private T ResolveWithRetry<T>(int maxAttempts = 0) where T : class
        {
            maxAttempts = maxAttempts > 0 ? maxAttempts : MaxResolveRetries;
            for (int i = 0; i < maxAttempts; i++)
            {
                try { return this.Container.Resolve<T>(); }
                catch (Exception ex) when (i < maxAttempts - 1)
                {
                    Log.Warning(ex, "[RESOLVE_RETRY] Attempt {Attempt}/{Max} failed for {Type}", i + 1, maxAttempts, typeof(T).Name);
                    Thread.Sleep(100 * (i + 1));  // Backoff
                }
            }
            throw new InvalidOperationException($"Failed to resolve {typeof(T).Name} after {maxAttempts} attempts");
        }

        // InitializeSigNozTelemetry() moved to App.Telemetry.cs
        // IntegrateTelemetryServices() moved to App.Telemetry.cs

        // CreateContainerExtension moved to App.DependencyInjection.cs
        // RegisterConventionTypes, RegisterCoreInfrastructure, RegisterRepositories, RegisterBusinessServices, RegisterViewModels, RegisterLazyAIServices moved to App.DependencyInjection.cs

        // BuildConfiguration, TryResolvePlaceholders moved to App.DependencyInjection.cs
        // ConfigureModuleCatalog, ConfigureDefaultRegionBehaviors, ConfigureRegionAdapterMappings moved to App.DependencyInjection.cs
        // ValidateAndRegisterViewModels, ValidateAIServiceConfiguration moved to App.DependencyInjection.cs

        // AppDomain handler (early, pre-DI)
        public App()
        {
            _startupId = Guid.NewGuid().ToString("N")[..8];
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Log.Fatal(ex, "AppDomain unhandled exception (Terminating: {IsTerminating})", args.IsTerminating);
                File.AppendAllText("logs/critical-startup-failures.log", $"[{DateTime.UtcNow:O}] {ex}\n==========\n\n");
            };

            // NOTE: License registration moved to static constructor per Syncfusion documentation
            // License MUST be registered before any Syncfusion types are instantiated
            // See static App() constructor above

            // NOTE: Theme application moved to OnStartup after resources are loaded
            // per requirements - SfSkinManager will apply FluentLight theme post-resources
        }

        // ConfigureModuleCatalog, ConfigureDefaultRegionBehaviors, ConfigureRegionAdapterMappings moved to App.DependencyInjection.cs
        // OnExit moved to App.Lifecycle.cs

        /// <summary>
        /// Closes all dialog windows before container disposal to prevent NullReferenceException
        /// in Prism DialogService during shutdown.
        /// </summary>
        private void CloseAllDialogWindows()
        {
            try
            {
                if (Application.Current?.Windows == null)
                {
                    Log.Debug("No windows to close during shutdown");
                    return;
                }

                // Find all dialog windows (exclude MainWindow/Shell)
                var dialogWindows = Application.Current.Windows
                    .OfType<Window>()
                    .Where(w => w != null &&
                                w != MainWindow &&
                                (w.GetType().Name.Contains("Dialog", StringComparison.OrdinalIgnoreCase) ||
                                 w.Owner != null)) // Dialogs typically have an owner
                    .ToList();

                if (dialogWindows.Count == 0)
                {
                    Log.Debug("No dialog windows found during shutdown");
                    return;
                }

                Log.Information("Closing {Count} dialog window(s) before container disposal", dialogWindows.Count);

                foreach (var dialog in dialogWindows)
                {
                    try
                    {
                        // Close dialog gracefully - check if still valid
                        if (dialog.IsLoaded)
                        {
                            // Try to set DialogResult for modal dialogs
                            try { dialog.DialogResult = false; } catch { /* Not modal */ }
                            dialog.Close();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Dialog may not be modal or already closed
                        try { dialog.Close(); } catch { /* Ignore */ }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error closing dialog window {DialogType} during shutdown", dialog.GetType().Name);
                        // Continue closing other dialogs
                    }
                }                Log.Debug("Completed dialog window closure during shutdown");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error closing dialog windows during shutdown (non-fatal)");
            }
        }

        // ... (remaining helpers: EnableDryIocDiagnostics if needed, etc.)
    }
}
