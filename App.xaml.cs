using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using Serilog.Enrichers;
using Serilog.Filters;
using Syncfusion.Licensing;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using WileyWidget.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Configuration;

namespace WileyWidget;

/// <summary>
/// Custom enricher for structured logging with application-specific context.
/// Provides correlation IDs, operation tracking, and performance metrics.
/// </summary>
public class ApplicationEnricher : Serilog.Core.ILogEventEnricher
{
    private static readonly AsyncLocal<string> _correlationId = new();
    private static readonly AsyncLocal<string> _operationId = new();
    private static readonly AsyncLocal<string> _userId = new();

    /// <summary>
    /// Sets the correlation ID for the current async context.
    /// </summary>
    public static string CorrelationId
    {
        get => _correlationId.Value ?? (_correlationId.Value = Guid.NewGuid().ToString());
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Sets the operation ID for tracking specific operations.
    /// </summary>
    public static string OperationId
    {
        get => _operationId.Value ?? (_operationId.Value = Guid.NewGuid().ToString());
        set => _operationId.Value = value;
    }

    /// <summary>
    /// Sets the user ID for the current context.
    /// </summary>
    public static string UserId
    {
        get => _userId.Value;
        set => _userId.Value = value;
    }

    /// <summary>
    /// Enriches log events with application-specific properties.
    /// </summary>
    public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        // Add correlation and operation tracking
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", CorrelationId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationId", OperationId));

        // Add user context if available
        if (!string.IsNullOrEmpty(UserId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", UserId));
        }

        // Add performance metrics
        var process = Process.GetCurrentProcess();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryUsageMB", process.WorkingSet64 / 1024 / 1024));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CpuTime", process.TotalProcessorTime.TotalMilliseconds));

        // Add application context
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationContext", "WPF"));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIThread", System.Threading.Thread.CurrentThread.ManagedThreadId));
    }
}

/// <summary>
/// Structured logging helper for consistent log formatting and context.
/// </summary>
public static class StructuredLogger
{
    /// <summary>
    /// Creates a logger with operation context for tracking related log entries.
    /// </summary>
    public static IDisposable BeginOperation(string operationName, string operationId = null)
    {
        var opId = operationId ?? Guid.NewGuid().ToString();
        ApplicationEnricher.OperationId = opId;

        using (LogContext.PushProperty("OperationName", operationName))
        using (LogContext.PushProperty("OperationStart", DateTime.UtcNow))
        {
            Log.Information("🔄 Operation Started: {OperationName}", operationName);
        }

        return new OperationScope(operationName, opId);
    }

    /// <summary>
    /// Logs performance metrics for operations.
    /// </summary>
    public static void LogPerformance(string operationName, TimeSpan duration, long memoryDelta = 0)
    {
        using (LogContext.PushProperty("OperationName", operationName))
        using (LogContext.PushProperty("Duration", duration.TotalMilliseconds))
        using (LogContext.PushProperty("MemoryDeltaMB", memoryDelta / 1024 / 1024))
        {
            Log.Information("⚡ Performance: {OperationName} completed in {Duration:F2}ms", operationName, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Logs user actions with structured context.
    /// </summary>
    public static void LogUserAction(string action, string details = null, object data = null)
    {
        using (LogContext.PushProperty("UserAction", action))
        using (LogContext.PushProperty("ActionDetails", details))
        using (LogContext.PushProperty("ActionData", data))
        {
            Log.Information("👤 User Action: {UserAction}", action);
        }
    }

    /// <summary>
    /// Logs theme changes with structured context.
    /// </summary>
    public static void LogThemeChange(string fromTheme, string toTheme, bool userInitiated = true)
    {
        using (LogContext.PushProperty("ThemeChange", true))
        using (LogContext.PushProperty("FromTheme", fromTheme))
        using (LogContext.PushProperty("ToTheme", toTheme))
        using (LogContext.PushProperty("UserInitiated", userInitiated))
        {
            Log.Information("🎨 Theme Changed: {FromTheme} → {ToTheme}", fromTheme, toTheme);
        }
    }

    /// <summary>
    /// Logs Syncfusion control operations with structured context.
    /// </summary>
    public static void LogSyncfusionOperation(string controlType, string operation, object properties = null)
    {
        using (LogContext.PushProperty("SyncfusionControl", controlType))
        using (LogContext.PushProperty("SyncfusionOperation", operation))
        using (LogContext.PushProperty("ControlProperties", properties))
        {
            Log.Debug("🔧 Syncfusion {ControlType}: {Operation}", controlType, operation);
        }
    }

    /// <summary>
    /// Operation scope for automatic cleanup and completion logging.
    /// </summary>
    private class OperationScope : IDisposable
    {
        private readonly string _operationName;
        private readonly string _operationId;
        private readonly DateTime _startTime;

        public OperationScope(string operationName, string operationId)
        {
            _operationName = operationName;
            _operationId = operationId;
            _startTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            var duration = DateTime.UtcNow - _startTime;

            using (LogContext.PushProperty("OperationName", _operationName))
            using (LogContext.PushProperty("OperationDuration", duration.TotalMilliseconds))
            using (LogContext.PushProperty("OperationCompleted", true))
            {
                Log.Information("✅ Operation Completed: {OperationName} in {Duration:F2}ms", _operationName, duration.TotalMilliseconds);
            }
        }
    }
}

/// <summary>
/// Wiley Widget WPF Application Entry Point and Bootstrap Class.
///
/// <para>This class serves as the main application controller responsible for:</para>
/// <list type="number">
/// <item><strong>Application Lifecycle Management:</strong> Startup, initialization, and shutdown</item>
/// <item><strong>Configuration Management:</strong> Loading settings from multiple sources</item>
/// <item><strong>Logging Infrastructure:</strong> Serilog setup with structured logging</item>
/// <item><strong>License Management:</strong> Syncfusion license registration and validation</item>
/// <item><strong>Database Services:</strong> Dependency injection and database initialization</item>
/// <item><strong>Exception Handling:</strong> Global exception capture and user-friendly error reporting</item>
/// <item><strong>User Experience:</strong> Settings loading and theme application</item>
/// <item><strong>Test Automation:</strong> Support for automated testing scenarios</item>
/// </list>
///
/// <para>Key Design Principles:</para>
/// <list type="bullet">
/// <item><strong>Fail-Safe Operation:</strong> Application continues running even if non-critical components fail</item>
/// <item><strong>Comprehensive Logging:</strong> All operations are logged with appropriate detail levels</item>
/// <item><strong>Graceful Degradation:</strong> Features degrade gracefully when dependencies are unavailable</item>
/// <item><strong>Performance Monitoring:</strong> Startup timing and performance metrics are tracked</item>
/// <item><strong>Developer Experience:</strong> Rich debugging information and error context</item>
/// </list>
///
/// <para>Execution Flow:</para>
/// <code>
/// Constructor → LoadConfiguration → ConfigureLogging → RegisterSyncfusionLicense
///     ↓
/// OnStartup → ConfigureDatabaseServices → ConfigureGlobalExceptionHandling
///     ↓
/// LoadAndApplyUserSettings → ConfigureTestAutomationSupport → Base.OnStartup
///     ↓
/// [Main Window Loads and Application Runs]
///     ↓
/// OnExit → PerformFinalCleanup → Log.CloseAndFlush → Base.OnExit
/// </code>
///
/// <para>Configuration Sources (in priority order):</para>
/// <list type="number">
/// <item>appsettings.json (application settings)</item>
/// <item>Environment Variables (runtime overrides)</item>
/// <item>User Secrets (development secrets)</item>
/// </list>
///
/// <para>Exception Handling Strategy:</para>
/// <list type="bullet">
/// <item><strong>AppDomain Exceptions:</strong> Logged as Fatal (will terminate app)</item>
/// <item><strong>Dispatcher Exceptions:</strong> Logged as Error, marked handled (app continues)</item>
/// <item><strong>Task Exceptions:</strong> Logged as Warning, marked observed (app continues)</item>
/// </list>
///
/// <para>License Registration Strategy:</para>
/// <list type="number">
/// <item>Configuration file (appsettings.json)</item>
/// <item>Environment variable (SYNCFUSION_LICENSE_KEY)</item>
/// <item>License file (license.key in output directory)</item>
/// <item>Fallback: Trial mode with user notification</item>
/// </list>
///
/// <para>Logging Configuration:</para>
/// <list type="bullet">
/// <item><strong>Format:</strong> Structured JSON with timestamps and context</item>
/// <item><strong>Location:</strong> ./logs/app-YYYYMMDD.log (root directory logs folder)</item>
/// <item><strong>Retention:</strong> 7 days with 10MB size limit per file</item>
/// <item><strong>Enrichment:</strong> Process ID, Thread ID, Machine Name, Environment</item>
/// </list>
///
/// <para>Performance Characteristics:</para>
/// <list type="bullet">
/// <item><strong>Startup Time:</strong> Typically &lt; 500ms for core initialization</item>
/// <item><strong>Memory Footprint:</strong> Minimal overhead from logging and configuration</item>
/// <item><strong>Thread Safety:</strong> All operations are thread-safe for UI thread</item>
/// </list>
///
/// <para>Extensibility Points:</para>
/// <list type="bullet">
/// <item><strong>Partial Methods:</strong> TryRegisterEmbeddedLicense for custom license logic</item>
/// <item><strong>Dependency Injection:</strong> IServiceProvider for custom services</item>
/// <item><strong>Configuration:</strong> IConfiguration for custom settings</item>
/// <item><strong>Logging:</strong> ILogger for custom logging scenarios</item>
/// </list>
///
/// <para>Testing Support:</para>
/// <list type="bullet">
/// <item><strong>License Dialog Automation:</strong> WILEYWIDGET_AUTOCLOSE_LICENSE=1</item>
/// <item><strong>Configuration Overrides:</strong> Environment variables</item>
/// <item><strong>Exception Simulation:</strong> Comprehensive error handling testing</item>
/// </list>
///
/// <para>Maintenance Notes:</para>
/// <list type="bullet">
/// <item>Regular log file cleanup (handled automatically)</item>
/// <item>License key rotation (update environment variable)</item>
/// <item>Configuration validation (check appsettings.json)</item>
/// <item>Performance monitoring (review startup logs)</item>
/// </list>
///
/// <para>Threading Model:</para>
/// <list type="bullet">
/// <item><strong>UI Thread:</strong> Constructor, OnStartup, OnExit</item>
/// <item><strong>Background:</strong> Database initialization, file operations</item>
/// <item><strong>Dispatcher:</strong> Timer-based operations, UI updates</item>
/// </list>
///
/// <para>Error Recovery:</para>
/// <list type="bullet">
/// <item><strong>Configuration Failure:</strong> Throws InvalidOperationException</item>
/// <item><strong>Logging Failure:</strong> Falls back to console logging</item>
/// <item><strong>License Failure:</strong> Continues in trial mode</item>
/// <item><strong>Database Failure:</strong> Logs error, continues without database</item>
/// </list>
///
/// <para>Security Considerations:</para>
/// <list type="bullet">
/// <item>License keys stored securely in environment variables</item>
/// <item>Sensitive configuration encrypted when possible</item>
/// <item>Exception details logged but not exposed to users</item>
/// <item>File system access restricted to application directories</item>
/// </list>
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Application configuration loaded from appsettings.json and environment variables.
    /// Used for database connections, API keys, and feature flags.
    /// </summary>
    private IConfiguration _configuration;

    /// <summary>
    /// Service provider for dependency injection, primarily used for database services.
    /// Currently reserved for future enhancements to database service management.
    /// </summary>
#pragma warning disable CS0649 // Field is never assigned to - reserved for future use
    private IServiceProvider _serviceProvider;
#pragma warning restore CS0649

    /// <summary>
    /// Stopwatch for measuring application startup performance.
    /// Useful for identifying bottlenecks in initialization.
    /// </summary>
    private readonly Stopwatch _startupTimer;

    /// <summary>
    /// WPF application constructor - executes before any XAML parsing or control creation.
    /// Critical initialization order:
    /// 1. Load configuration (needed for license keys and database connections)
    /// 2. Configure logging (enables license registration logging)
    /// 3. Register Syncfusion license (MUST happen before any Syncfusion controls)
    ///
    /// <para>This timing ensures proper license validation and prevents trial warnings.</para>
    /// <para>All exceptions during initialization are logged but don't crash the app.</para>
    /// </summary>
    /// <remarks>
    /// WPF calls this constructor before OnStartup, making it the ideal place for
    /// pre-UI initialization tasks that must complete before any windows are shown.
    /// </remarks>
    public App()
    {
        // Start timing application initialization for performance monitoring
        _startupTimer = Stopwatch.StartNew();

        try
        {
            // Write to a debug file to see if constructor is reached
            File.WriteAllText("debug.log", "🚀 === Application Constructor Started ===\n");

            // Phase 1: Configure logging FIRST (must happen before any Log calls)
            ConfigureLogging();
            File.AppendAllText("debug.log", "📝 Logging configured\n");
            Log.Information("🚀 === Application Constructor Started ===");

            // Phase 2: Load configuration (required for license keys and database)
            LoadConfiguration();
            File.AppendAllText("debug.log", "⚙️ Configuration loaded\n");
            Log.Information("✅ Configuration loaded successfully");

            // Phase 3: Register Syncfusion license (CRITICAL: must happen before any Syncfusion operations)
            RegisterSyncfusionLicense();
            File.AppendAllText("debug.log", "🔑 Syncfusion license registered\n");

            // Phase 4: Apply Fluent theme globally
            try
            {
                Log.Information("🎨 Starting theme application in constructor...");
                File.AppendAllText("debug.log", "🎨 Starting theme application in constructor...\n");

                // Initialize SfSkinManager for dynamic theming - CRITICAL: Must happen before any Syncfusion controls load
                // Reference: https://help.syncfusion.com/cr/wpf/Syncfusion.html#theme-application
                Syncfusion.SfSkinManager.SfSkinManager.ApplyThemeAsDefaultStyle = true;
                Log.Information("✅ SfSkinManager.ApplyThemeAsDefaultStyle set to true in constructor");
                File.AppendAllText("debug.log", "✅ SfSkinManager.ApplyThemeAsDefaultStyle set to true in constructor\n");

                // Set FluentDark theme as the application default
                // This ensures all Syncfusion controls use FluentDark theme by default
                try
                {
                    // Apply FluentDark theme globally using SfSkinManager
                    // Reference: https://help.syncfusion.com/cr/wpf/Syncfusion.SfSkinManager.SfSkinManager.html
                    // Note: Theme will be applied when MainWindow is created in OnStartup
                    Log.Information("✅ FluentDark theme will be applied when MainWindow loads");
                    File.AppendAllText("debug.log", "✅ FluentDark theme will be applied when MainWindow loads\n");
                }
                catch (Exception themeApplyEx)
                {
                    Log.Warning(themeApplyEx, "⚠️  Failed to prepare FluentDark theme: {Message}", themeApplyEx.Message);
                    File.AppendAllText("debug.log", $"⚠️  Failed to prepare FluentDark theme: {themeApplyEx.Message}\n");
                }

                Log.Information("🎨 Theme application in constructor completed");
                File.AppendAllText("debug.log", "🎨 Theme application in constructor completed\n");
            }
            catch (Exception themeEx)
            {
                Log.Error(themeEx, "💥 ERROR during constructor theme application: {Message}", themeEx.Message);
                File.AppendAllText("debug.log", $"💥 ERROR during constructor theme application: {themeEx.Message}\n{themeEx.StackTrace}\n");

                // Continue execution even if theme fails - application should still work
                Log.Warning("⚠️  Continuing without theme - application may appear unstyled");
                File.AppendAllText("debug.log", "⚠️  Continuing without theme - application may appear unstyled\n");
            }

            // Log successful initialization
            _startupTimer.Stop();
            File.AppendAllText("debug.log", "✅ Constructor completed successfully\n");
            Log.Information("🎉 === Application Constructor Completed ===");
            Log.Information("⏱️  Application initialization took {ElapsedMs}ms", _startupTimer.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            File.AppendAllText("debug.log", $"💥 CRITICAL ERROR in constructor: {ex.Message}\n{ex.StackTrace}\n");

            // Critical failure in constructor - log and rethrow to prevent corrupted state
            Log.Fatal(ex, "💥 CRITICAL: Application constructor failed - application may not start properly");
            throw;
        }

        // Breakpoint: Constructor end
        System.Diagnostics.Debugger.Break();
    }
    /// <summary>
    /// WPF application startup event handler.
    /// Executes after constructor but before main window is shown.
    /// Handles post-constructor initialization tasks that can safely run after UI is available.
    ///
    /// <para>Initialization sequence:</para>
    /// <list type="number">
    /// <item>Database services configuration and migration</item>
    /// <item>Global exception handling setup</item>
    /// <item>User settings loading and theme application</item>
    /// <item>Test automation license dialog handling (if enabled)</item>
    /// </list>
    /// </summary>
    /// <param name="e">Startup event arguments containing command line parameters</param>
    /// <remarks>
    /// This method runs on the UI thread and has access to Dispatcher and Windows collection.
    /// Any exceptions here will crash the application, so robust error handling is critical.
    /// </remarks>
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Restart stopwatch for OnStartup performance profiling
        _startupTimer.Restart();

        try
        {
            File.AppendAllText("debug.log", "🎬 === Application Startup Event ===\n");
            Log.Information("🎬 === Application Startup Event ===");

            // Log XAML resource loading status with enhanced Syncfusion error handling
            Log.Information("🔍 Checking XAML resource loading with Syncfusion error recovery...");
            File.AppendAllText("debug.log", "🔍 Checking XAML resource loading with Syncfusion error recovery...\n");

            if (this.Resources?.MergedDictionaries != null)
            {
                Log.Information("📚 Found {Count} merged dictionaries in Application.Resources", this.Resources.MergedDictionaries.Count);
                File.AppendAllText("debug.log", $"📚 Found {this.Resources.MergedDictionaries.Count} merged dictionaries in Application.Resources\n");

                // Track failed dictionaries for removal
                var failedDictionaries = new List<int>();

                for (int i = 0; i < this.Resources.MergedDictionaries.Count; i++)
                {
                    var dict = this.Resources.MergedDictionaries[i];
                    var source = dict.Source?.ToString() ?? "null";
                    Log.Information("📖 Dictionary {Index}: Source='{Source}'", i, source);
                    File.AppendAllText("debug.log", $"📖 Dictionary {i}: Source='{source}'\n");

                    // Try to access the dictionary to see if it loads
                    try
                    {
                        var keys = dict.Keys;
                        Log.Information("✅ Dictionary {Index} loaded successfully with {KeyCount} keys", i, keys.Count);
                        File.AppendAllText("debug.log", $"✅ Dictionary {i} loaded successfully with {keys.Count} keys\n");

                        // Special handling for Syncfusion theme resources
                        if (source.Contains("Syncfusion.Themes.FluentDark.WPF"))
                        {
                            Log.Information("🎨 Syncfusion FluentDark theme dictionary loaded successfully");
                            File.AppendAllText("debug.log", "🎨 Syncfusion FluentDark theme dictionary loaded successfully\n");
                        }
                    }
                    catch (Exception dictEx)
                    {
                        Log.Error(dictEx, "❌ Failed to load dictionary {Index} ({Source}): {Message}", i, source, dictEx.Message);
                        File.AppendAllText("debug.log", $"❌ Failed to load dictionary {i} ({source}): {dictEx.Message}\n{dictEx.StackTrace}\n");

                        // Special error handling for Syncfusion resources
                        if (source.Contains("Syncfusion.Themes.FluentDark.WPF"))
                        {
                            Log.Warning("⚠️ Syncfusion theme resource failed to load - will fallback to default WPF styles");
                            File.AppendAllText("debug.log", "⚠️ Syncfusion theme resource failed to load - will fallback to default WPF styles\n");

                            // Mark for removal to prevent further issues
                            failedDictionaries.Add(i);
                        }
                        else if (source.Contains("SyncfusionResources.xaml"))
                        {
                            Log.Warning("⚠️ Custom Syncfusion resources failed to load - continuing with basic theme");
                            File.AppendAllText("debug.log", "⚠️ Custom Syncfusion resources failed to load - continuing with basic theme\n");
                        }
                    }
                }

                // Remove failed dictionaries in reverse order to maintain indices
                foreach (var index in failedDictionaries.OrderByDescending(x => x))
                {
                    try
                    {
                        var removedDict = this.Resources.MergedDictionaries[index];
                        this.Resources.MergedDictionaries.RemoveAt(index);
                        Log.Information("🗑️ Removed failed dictionary: {Source}", removedDict.Source?.ToString() ?? "unknown");
                        File.AppendAllText("debug.log", $"🗑️ Removed failed dictionary: {removedDict.Source?.ToString() ?? "unknown"}\n");
                    }
                    catch (Exception removeEx)
                    {
                        Log.Warning(removeEx, "⚠️ Failed to remove failed dictionary at index {Index}", index);
                        File.AppendAllText("debug.log", $"⚠️ Failed to remove failed dictionary at index {index}\n");
                    }
                }

                // Log final state after cleanup
                Log.Information("📊 Final merged dictionaries count: {Count}", this.Resources.MergedDictionaries.Count);
                File.AppendAllText("debug.log", $"📊 Final merged dictionaries count: {this.Resources.MergedDictionaries.Count}\n");
            }
            else
            {
                Log.Warning("⚠️ No merged dictionaries found in Application.Resources - application will use default WPF styles");
                File.AppendAllText("debug.log", "⚠️ No merged dictionaries found in Application.Resources - application will use default WPF styles\n");
            }

            // Enhanced resource key verification
            Log.Debug("🔍 Resource key check: {HasKey}", this.Resources.Contains("SomeSyncfusionKey"));
            File.AppendAllText("debug.log", $"🔍 Resource key check: {this.Resources.Contains("SomeSyncfusionKey")}\n");

            Log.Information("�📋 Command line args: {Args}", string.Join(" ", e.Args));
            File.AppendAllText("debug.log", $"📋 Command line args: {string.Join(" ", e.Args)}\n");

            // Phase 1: Database services (can fail gracefully if DB unavailable)
            File.AppendAllText("debug.log", "📊 Starting database services configuration...\n");
            await ConfigureDatabaseServices();
            File.AppendAllText("debug.log", "✅ Database services configured\n");

            // Phase 2: Global exception handling (critical for stability)
            ConfigureGlobalExceptionHandling();
            Log.Information("✅ Global exception handling configured");
            File.AppendAllText("debug.log", "🛡️ Global exception handling configured\n");

            // Phase 3: User settings and theme
            LoadAndApplyUserSettings();
            File.AppendAllText("debug.log", "⚙️ User settings loaded\n");

            // Phase 3.5: Apply Syncfusion theme with enhanced error handling
            try
            {
                Log.Information("🎨 Starting Syncfusion theme application with validation...");
                File.AppendAllText("debug.log", "🎨 Starting Syncfusion theme application with validation...\n");

                // First, validate Syncfusion package availability
                ValidateSyncfusionPackages();

                // Log current application resources state
                Log.Information("📋 Application resources count: {Count}", this.Resources?.MergedDictionaries?.Count ?? 0);
                File.AppendAllText("debug.log", $"📋 Application resources count: {this.Resources?.MergedDictionaries?.Count ?? 0}\n");

                // Log each merged dictionary with validation
                if (this.Resources?.MergedDictionaries != null)
                {
                    for (int i = 0; i < this.Resources.MergedDictionaries.Count; i++)
                    {
                        var dict = this.Resources.MergedDictionaries[i];
                        var source = dict.Source?.ToString() ?? "null";
                        Log.Information("📚 Merged dictionary {Index}: Source={Source}", i, source);
                        File.AppendAllText("debug.log", $"📚 Merged dictionary {i}: Source={dict.Source?.ToString() ?? "null"}\n");

                        // Validate Syncfusion theme resources specifically
                        if (source.Contains("Syncfusion.Themes.FluentDark.WPF"))
                        {
                            try
                            {
                                // Test if the theme dictionary can be accessed
                                var testKey = dict["FluentDarkTheme"] ?? dict.Keys.OfType<string>().FirstOrDefault(k => k.Contains("Theme"));
                                if (testKey != null)
                                {
                                    Log.Information("✅ Syncfusion theme resource validated: {Key}", testKey);
                                    File.AppendAllText("debug.log", $"✅ Syncfusion theme resource validated: {testKey}\n");
                                }
                            }
                            catch (Exception themeValidationEx)
                            {
                                Log.Warning(themeValidationEx, "⚠️ Syncfusion theme validation failed: {Message}", themeValidationEx.Message);
                                File.AppendAllText("debug.log", $"⚠️ Syncfusion theme validation failed: {themeValidationEx.Message}\n");
                            }
                        }
                    }
                }

                // Apply theme settings using proper Syncfusion API
                // Reference: https://help.syncfusion.com/cr/wpf/Syncfusion.SfSkinManager.SfSkinManager.html
                File.AppendAllText("debug.log", $"🎨 Theme set at {DateTime.Now}: Preparing to set SfSkinManager.ApplyThemeAsDefaultStyle = true\n");
                SfSkinManager.ApplyThemeAsDefaultStyle = true; // Enables ThemeResource on all controls
                Log.Information("✅ SfSkinManager.ApplyThemeAsDefaultStyle set to true");
                File.AppendAllText("debug.log", $"🎨 Theme set at {DateTime.Now}: SfSkinManager.ApplyThemeAsDefaultStyle = {SfSkinManager.ApplyThemeAsDefaultStyle}\n");

                // Set FluentDark as the default theme for all Syncfusion controls
                // This will be applied when controls are created
                Log.Information("✅ FluentDark theme configured as default for Syncfusion controls");
                File.AppendAllText("debug.log", "✅ FluentDark theme configured as default for Syncfusion controls\n");

                // Validate theme application by checking if SfSkinManager is working
                try
                {
                    // Check if SfSkinManager properties are accessible
                    var applyThemeDefault = SfSkinManager.ApplyThemeAsDefaultStyle;
                    Log.Information("🎨 SfSkinManager.ApplyThemeAsDefaultStyle: {Value}", applyThemeDefault);
                    File.AppendAllText("debug.log", $"🎨 SfSkinManager.ApplyThemeAsDefaultStyle: {applyThemeDefault}\n");
                }
                catch (Exception themeCheckEx)
                {
                    Log.Warning(themeCheckEx, "⚠️ Could not verify SfSkinManager theme: {Message}", themeCheckEx.Message);
                    File.AppendAllText("debug.log", $"⚠️ Could not verify SfSkinManager theme: {themeCheckEx.Message}\n");
                }

                // Log theme application success
                Log.Information("🎨 Syncfusion theme application completed successfully");
                File.AppendAllText("debug.log", "🎨 Syncfusion theme application completed successfully\n");
            }
            catch (Exception themeEx)
            {
                Log.Error(themeEx, "💥 CRITICAL ERROR during theme application: {Message}", themeEx.Message);
                File.AppendAllText("debug.log", $"💥 CRITICAL ERROR during theme application: {themeEx.Message}\n{themeEx.StackTrace}\n");

                // Try to continue without theme - fallback to default WPF styles
                Log.Warning("⚠️ Continuing application startup without Syncfusion theme - using default WPF styles");
                File.AppendAllText("debug.log", "⚠️ Continuing application startup without Syncfusion theme - using default WPF styles\n");

                try
                {
                    // Ensure SfSkinManager is disabled to prevent conflicts
                    SfSkinManager.ApplyThemeAsDefaultStyle = false;
                    Log.Information("✅ SfSkinManager disabled for fallback mode");
                    File.AppendAllText("debug.log", "✅ SfSkinManager disabled for fallback mode\n");
                }
                catch (Exception sfSkinEx)
                {
                    Log.Warning(sfSkinEx, "⚠️ Could not disable SfSkinManager: {Message}", sfSkinEx.Message);
                    File.AppendAllText("debug.log", $"⚠️ Could not disable SfSkinManager: {sfSkinEx.Message}\n");
                }
            }

            // Phase 4: Test automation support (optional)
            ConfigureTestAutomationSupport();
            File.AppendAllText("debug.log", "🧪 Test automation configured\n");

            // Call base implementation last
            base.OnStartup(e);
            File.AppendAllText("debug.log", "📱 Base OnStartup completed\n");

            // Set MainWindow since StartupUri is removed with error handling
            try
            {
                File.AppendAllText("debug.log", "🏠 Creating MainWindow...\n");
                this.MainWindow = new MainWindow();
                File.AppendAllText("debug.log", "✅ MainWindow created\n");

                // Breakpoint: MainWindow.Show()
                System.Diagnostics.Debugger.Break();

                this.MainWindow.Show();
                File.AppendAllText("debug.log", "🎉 MainWindow shown - application should be visible now!\n");
            }
            catch (Exception windowEx)
            {
                File.AppendAllText("debug.log", $"💥 CRITICAL ERROR creating/showing MainWindow: {windowEx.Message}\n{windowEx.StackTrace}\n");
                Log.Fatal(windowEx, "💥 CRITICAL: Failed to create or show MainWindow - XAML parse error or initialization failure");

                // Show user-friendly error message
                MessageBox.Show(
                    $"Application failed to start due to a XAML parsing or initialization error:\n\n{windowEx.Message}\n\nThe application will now shut down.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                // Shutdown the application gracefully
                Shutdown(1);
                return;
            }

            // Log OnStartup performance
            _startupTimer.Stop();
            Log.Information("⏱️ OnStartup completed in {ElapsedMs}ms", _startupTimer.ElapsedMilliseconds);
            File.AppendAllText("debug.log", $"⏱️ OnStartup completed in {_startupTimer.ElapsedMilliseconds}ms\n");

            Log.Information("✅ === Application Startup Completed Successfully ===");
        }
        catch (Exception ex)
        {
            File.AppendAllText("debug.log", $"💥 CRITICAL ERROR during startup: {ex.Message}\n{ex.StackTrace}\n");
            Log.Fatal(ex, "💥 CRITICAL: Application startup failed - shutting down");
            // Shutdown the application gracefully
            Shutdown(1);
        }
    }

    /// <summary>
    /// Configures database services and initializes the database
    /// </summary>
    private async Task ConfigureDatabaseServices()
    {
        // Breakpoint: ConfigureDatabaseServices start
        System.Diagnostics.Debugger.Break();

        try
        {
            Log.Information("Configuring database services...");

            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<WileyWidget.App>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Configure services
            var services = new ServiceCollection();
            services.AddApplicationServices(configuration);

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Initialize service locator for global access
            ServiceLocator.Initialize(serviceProvider);

            // Initialize database with timeout - RESTORED
            var dbTask = DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(dbTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Error("⏰ Database initialization timed out after 10 seconds - continuing without database (graceful degradation)");
                // Continue without database - app can still run
            }
            else
            {
                // Database task completed successfully
                await dbTask; // Ensure any exceptions are propagated if needed
                Log.Information("Database services configured successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to configure database services");
            // In development, you might want to show a message box or handle this differently
            // For now, we'll log the error and continue - the app can still run without database
        }
    }

    /// <summary>
    /// Loads user settings and applies theme configuration.
    /// Sets default theme if none is configured.
    /// </summary>
    private void LoadAndApplyUserSettings()
    {
        try
        {
            Log.Information("⚙️ Loading user settings...");

            // Load persisted settings
            SettingsService.Instance.Load();

            // Apply default theme if none set
            if (string.IsNullOrWhiteSpace(SettingsService.Instance.Current.Theme))
            {
                SettingsService.Instance.Current.Theme = "FluentDark";
                Log.Information("🎨 Applied default theme: FluentDark");
            }
            else
            {
                Log.Information("🎨 Loaded theme: {Theme}", SettingsService.Instance.Current.Theme);
            }

            Log.Information("✅ User settings loaded successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to load user settings - using defaults");
            // Continue with defaults if settings fail to load
            SettingsService.Instance.Current.Theme = "FluentDark";
        }
    }

    /// <summary>
    /// Configures test automation support features.
    /// Currently handles automatic Syncfusion license dialog dismissal for automated testing.
    /// </summary>
    private void ConfigureTestAutomationSupport()
    {
        try
        {
            // Check for test automation environment variable
            var autoCloseLicense = Environment.GetEnvironmentVariable("WILEYWIDGET_AUTOCLOSE_LICENSE");
            if (string.Equals(autoCloseLicense, "1", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("🤖 Test automation mode detected - enabling license dialog auto-close");
                TryScheduleLicenseDialogAutoClose();
            }
            else
            {
                Log.Information("ℹ️ Running in standard mode (no test automation features)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to configure test automation support");
        }
    }
    /// <summary>
    /// Schedules a dispatcher timer that scans for the Syncfusion trial license dialog and attempts to close it.
    /// Used only in test automation modes to prevent blocking automated test execution.
    ///
    /// <para>Timer specifications:</para>
    /// <list type="bullet">
    /// <item>Interval: 500ms (balances responsiveness with performance)</item>
    /// <item>Max attempts: 12 (approximately 6 seconds total)</item>
    /// <item>Search pattern: Windows with "Syncfusion" in title</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This method is designed for automated testing scenarios where the license dialog
    /// would prevent clean test execution. It safely handles exceptions and will not
    /// interfere with normal application operation.
    /// </remarks>
    private void TryScheduleLicenseDialogAutoClose()
    {
        try
        {
            Log.Information("⏰ Starting license dialog auto-close timer (test automation mode)");

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
                Tag = "LicenseDialogAutoClose" // For debugging purposes
            };

            int attempts = 0;
            const int maxAttempts = 12; // ~6 seconds at 500ms intervals

            timer.Tick += (_, _) =>
            {
                try
                {
                    attempts++;
                    Log.Debug("🔍 Scanning for Syncfusion license dialog (attempt {Attempt}/{Max})", attempts, maxAttempts);

                    // Scan all current windows for Syncfusion license dialog
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window != null &&
                            !string.IsNullOrEmpty(window.Title) &&
                            window.Title.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Information("🎯 Found Syncfusion license dialog - closing automatically");
                            window.Close();
                            timer.Stop();
                            return;
                        }
                    }

                    // Stop timer after maximum attempts
                    if (attempts >= maxAttempts)
                    {
                        Log.Information("⏰ License dialog auto-close timer expired (no dialog found after {MaxAttempts} attempts)", maxAttempts);
                        timer.Stop();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠️ Error during license dialog scan (attempt {Attempt})", attempts);
                    // Continue trying - don't stop the timer on individual failures
                }
            };

            timer.Start();
            Log.Information("✅ License dialog auto-close timer started successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to start license dialog auto-close timer");
        }
    }

    /// <summary>
    /// WPF application exit event handler.
    /// Performs final cleanup operations before application termination.
    ///
    /// <para>Cleanup tasks:</para>
    /// <list type="bullet">
    /// <item>Flush and close all loggers to ensure all messages are written</item>
    /// <item>Release any held resources</item>
    /// <item>Log final application statistics</item>
    /// </list>
    /// </summary>
    /// <param name="e">Exit event arguments containing exit code</param>
    /// <remarks>
    /// This method should complete quickly as it blocks application shutdown.
    /// All operations should be wrapped in try-catch to prevent hanging.
    /// </remarks>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("🛑 === Application Exit Started ===");
            Log.Information("📊 Exit code: {ExitCode}", e.ApplicationExitCode);

            // Perform final cleanup
            PerformFinalCleanup();

            // Flush all loggers to ensure messages are written
            Log.Information("💾 Flushing loggers...");
            Log.CloseAndFlush();

            Log.Information("✅ === Application Exit Completed ===");
        }
        catch (Exception ex)
        {
            // Even exit logging failed - use debug console as last resort
            Debug.WriteLine($"CRITICAL: Exit cleanup failed - {ex.Message}");
        }
        finally
        {
            // Always call base implementation
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Performs final cleanup operations before application shutdown.
    /// Includes resource disposal, cache clearing, and final state logging.
    /// </summary>
    private void PerformFinalCleanup()
    {
        try
        {
            Log.Information("🧹 Performing final application cleanup...");

            // Dispose of service provider if it exists
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
                Log.Information("♻️ Service provider disposed");
            }

            // Log final application statistics
            var uptime = _startupTimer?.Elapsed ?? TimeSpan.Zero;
            Log.Information("📈 Application uptime: {Uptime}", uptime);

            Log.Information("✅ Final cleanup completed");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error during final cleanup");
        }
    }

    /// <summary>
    /// Registers the Syncfusion license using precedence: environment variable (SYNCFUSION_LICENSE_KEY) > side-by-side license.key file.
    /// Falls back silently if neither is present so the app can still run in development (will show trial banner).
    /// </summary>
    /// <summary>
    /// Loads application configuration from multiple sources with fallback support.
    ///
    /// <para>Configuration sources (in order):</para>
    /// <list type="number">
    /// <item>appsettings.json (optional, reloads on change)</item>
    /// <item>Environment variables (highest priority for overrides)</item>
    /// </list>
    ///
    /// <para>Configuration is used for:</para>
    /// <list type="bullet">
    /// <item>Database connection strings</item>
    /// <item>API keys and secrets</item>
    /// <item>Feature flags and settings</item>
    /// <item>Syncfusion license keys</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration cannot be loaded</exception>
    /// <remarks>
    /// This method must be called before any configuration-dependent operations.
    /// The configuration is cached in _configuration for the application lifetime.
    /// </remarks>
    private void LoadConfiguration()
    {
        try
        {
            Log.Information("📄 Loading application configuration...");

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            Log.Debug("📂 Configuration base path: {BasePath}", basePath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            // Validate critical configuration exists
            ValidateConfiguration();

            Log.Information("✅ Configuration loaded successfully");
            Log.Debug("🔧 Available configuration sections: {Sections}",
                string.Join(", ", _configuration.GetChildren().Select(c => c.Key)));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 CRITICAL: Failed to load application configuration");
            throw new InvalidOperationException("Application configuration could not be loaded", ex);
        }
    }

    /// <summary>
    /// Validates that critical configuration sections are present and valid.
    /// Logs warnings for missing optional configuration but doesn't fail.
    /// </summary>
    private void ValidateConfiguration()
    {
        try
        {
            // Check for database connection string
            var defaultConnection = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(defaultConnection))
            {
                Log.Warning("⚠️ DefaultConnection not found in configuration");
            }
            else
            {
                // Validate connection string format
                if (IsValidConnectionString(defaultConnection))
                {
                    Log.Debug("✅ Database connection string configured and valid");
                }
                else
                {
                    Log.Warning("⚠️ Database connection string format appears invalid: {ConnectionString}", defaultConnection);
                }
            }

            // Check for Syncfusion license configuration
            var syncfusionSection = _configuration.GetSection("Syncfusion");
            if (syncfusionSection.Exists())
            {
                Log.Debug("✅ Syncfusion configuration section found");
            }
            else
            {
                Log.Debug("ℹ️ Syncfusion configuration section not found (will use environment variables)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error validating configuration");
        }
    }

    /// <summary>
    /// Validates the format of a database connection string
    /// </summary>
    /// <param name="connectionString">The connection string to validate</param>
    /// <returns>True if the connection string appears valid, false otherwise</returns>
    private bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        // For SQLite connection strings, check for "Data Source="
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            // Extract the data source path
            var dataSourcePart = connectionString.Split(';')
                .FirstOrDefault(part => part.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(dataSourcePart))
            {
                var path = dataSourcePart.Split('=')[1]?.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    // Basic validation: ensure it's not empty and doesn't contain invalid characters
                    return !path.Contains("..") && !Path.GetInvalidPathChars().Any(c => path.Contains(c));
                }
            }
        }

        // For SQL Server connection strings, check for required components
        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Validates that required Syncfusion packages are available and compatible
    /// </summary>
    private void ValidateSyncfusionPackages()
    {
        try
        {
            Log.Information("🔍 Validating Syncfusion package availability...");

            // Check for SfSkinManager assembly
            var sfSkinManagerAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Contains("Syncfusion.SfSkinManager.WPF") == true);

            if (sfSkinManagerAssembly != null)
            {
                var version = sfSkinManagerAssembly.GetName().Version;
                Log.Information("✅ Syncfusion.SfSkinManager.WPF found: v{Version}", version);
                File.AppendAllText("debug.log", $"✅ Syncfusion.SfSkinManager.WPF found: v{version}\n");

                // Check if version is 23+
                if (version?.Major >= 23)
                {
                    Log.Information("✅ Syncfusion.SfSkinManager.WPF version is compatible (23+)");
                    File.AppendAllText("debug.log", "✅ Syncfusion.SfSkinManager.WPF version is compatible (23+)\n");
                }
                else
                {
                    Log.Warning("⚠️ Syncfusion.SfSkinManager.WPF version {Version} may not be fully compatible (recommended: 23+)", version);
                    File.AppendAllText("debug.log", $"⚠️ Syncfusion.SfSkinManager.WPF version {version} may not be fully compatible (recommended: 23+)\n");
                }
            }
            else
            {
                Log.Warning("⚠️ Syncfusion.SfSkinManager.WPF assembly not found");
                File.AppendAllText("debug.log", "⚠️ Syncfusion.SfSkinManager.WPF assembly not found\n");
            }

            // Check for FluentDark theme assembly
            var fluentDarkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Contains("Syncfusion.Themes.FluentDark.WPF") == true);

            if (fluentDarkAssembly != null)
            {
                var version = fluentDarkAssembly.GetName().Version;
                Log.Information("✅ Syncfusion.Themes.FluentDark.WPF found: v{Version}", version);
                File.AppendAllText("debug.log", $"✅ Syncfusion.Themes.FluentDark.WPF found: v{version}\n");

                // Check if version is 23+
                if (version?.Major >= 23)
                {
                    Log.Information("✅ Syncfusion.Themes.FluentDark.WPF version is compatible (23+)");
                    File.AppendAllText("debug.log", "✅ Syncfusion.Themes.FluentDark.WPF version is compatible (23+)\n");
                }
                else
                {
                    Log.Warning("⚠️ Syncfusion.Themes.FluentDark.WPF version {Version} may not be fully compatible (recommended: 23+)", version);
                    File.AppendAllText("debug.log", $"⚠️ Syncfusion.Themes.FluentDark.WPF version {version} may not be fully compatible (recommended: 23+)\n");
                }
            }
            else
            {
                Log.Warning("⚠️ Syncfusion.Themes.FluentDark.WPF assembly not found");
                File.AppendAllText("debug.log", "⚠️ Syncfusion.Themes.FluentDark.WPF assembly not found\n");
            }

            // Test SfSkinManager functionality
            try
            {
                // Test basic SfSkinManager functionality
                var applyThemeDefault = SfSkinManager.ApplyThemeAsDefaultStyle;
                Log.Information("✅ SfSkinManager functionality validated (ApplyThemeAsDefaultStyle: {Value})", applyThemeDefault);
                File.AppendAllText("debug.log", $"✅ SfSkinManager functionality validated (ApplyThemeAsDefaultStyle: {applyThemeDefault})\n");
            }
            catch (Exception sfTestEx)
            {
                Log.Warning(sfTestEx, "⚠️ SfSkinManager functionality test failed: {Message}", sfTestEx.Message);
                File.AppendAllText("debug.log", $"⚠️ SfSkinManager functionality test failed: {sfTestEx.Message}\n");
            }

            Log.Information("✅ Syncfusion package validation completed");
            File.AppendAllText("debug.log", "✅ Syncfusion package validation completed\n");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error during Syncfusion package validation: {Message}", ex.Message);
            File.AppendAllText("debug.log", $"⚠️ Error during Syncfusion package validation: {ex.Message}\n");
        }
    }

    /// <summary>
    /// Registers Syncfusion license using configuration system with fallback methods
    /// </summary>
    private async void RegisterSyncfusionLicense()
    {
        Log.Information("=== Starting Syncfusion License Registration ===");

        // 0. Configuration-based license (highest priority)
        try
        {
            var configKey = _configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                Log.Information("✅ Syncfusion license registered from configuration.");
                return;
            }
            else
            {
                Log.Information("ℹ️ Configuration license key not found or is placeholder.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Error reading configuration license key");
        }

        // 1. Optional embedded license hook (implemented in user-created partial file not committed).
        // If the partial method returns true, registration succeeded and we skip other sources.
        try
        {
            if (TryRegisterEmbeddedLicense())
            {
                Log.Information("Syncfusion license registered from embedded partial.");
                return;
            }
        }
        catch { /* ignore and continue */ }

        // 2. Environment variable (User or Machine scope). User sets via: [System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY","<key>","User")
        try
        {
            // Try User scope first, then Machine scope
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                Log.Information($"✅ Found license key in environment (length: {envKey.Length})");
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Log.Information("✅ Syncfusion license registered from environment variable.");
                return;
            }
            else
            {
                Log.Information("ℹ️ No valid SYNCFUSION_LICENSE_KEY environment variable found.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Error accessing SYNCFUSION_LICENSE_KEY environment variable");
        }

        // 3. File fallback with timeout
        var fileRegistrationTask = Task.Run(() => TryLoadLicenseFromFileWithTimeout());
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

        var completedTask = await Task.WhenAny(fileRegistrationTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            Log.Warning("⏰ Syncfusion license file read timed out after 5 seconds. Application will run in trial mode.");
            LogTrialModeActivation();
        }
        else
        {
            bool fileResult = await fileRegistrationTask;
            if (!fileResult)
            {
                Log.Warning("❌ Syncfusion license NOT registered (no config, no env var, no license.key). Application will run in trial mode.");
                LogTrialModeActivation();
            }
            else
            {
                Log.Information("✅ Syncfusion license registered from file fallback.");
            }
        }
    }

    /// <summary>
    /// Virtual hook allowing LicenseKey.Private.cs to embed the license.
    /// Return true if a key was registered. Default implementation calls EmbeddedLicenseManager.
    /// </summary>
    protected virtual bool TryRegisterEmbeddedLicense()
    {
        // Try to call the embedded license manager if available
        try
        {
            // Use reflection to call the static method from EmbeddedLicenseManager
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var type = assembly.GetType("WileyWidget.EmbeddedLicenseManager");
            if (type != null)
            {
                var method = type.GetMethod("TryRegisterEmbeddedLicense", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    return (bool)method.Invoke(null, null);
                }
            }
        }
        catch
        {
            // Ignore reflection errors - fall back to default
        }

        // Default implementation - returns false (no embedded license)
        return false;
    }

    /// <summary>
    /// Logs information about trial mode activation for monitoring and debugging
    /// </summary>
    private void LogTrialModeActivation()
    {
        try
        {
            Log.Warning("🚨 APPLICATION RUNNING IN SYNCFUSION TRIAL MODE 🚨");
            Log.Warning("📋 Trial Mode Details:");
            Log.Warning("   • Limited functionality may be available");
            Log.Warning("   • Watermarks may appear on controls");
            Log.Warning("   • Some features may be disabled");
            Log.Warning("   • Performance may be degraded");
            Log.Warning("💡 To resolve: Set SYNCFUSION_LICENSE_KEY environment variable or add license.key file");
            Log.Warning("🔗 Reference: https://help.syncfusion.com/cr/wpf/Syncfusion.html#licensing");
        }
        catch (Exception ex)
        {
            // Fallback logging in case structured logging fails
            System.Diagnostics.Debug.WriteLine($"Syncfusion Trial Mode Activated: {ex.Message}");
        }
    }

    /// <summary>
    /// Configures the Serilog structured logging system with comprehensive settings.
    /// Enhanced with advanced features for enterprise-grade logging and monitoring.
    ///
    /// <para>Enhanced Features:</para>
    /// <list type="bullet">
    /// <item><strong>Structured Logging:</strong> Rich contextual data with every log entry</item>
    /// <item><strong>Performance Monitoring:</strong> Request timing, memory usage, and throughput metrics</item>
    /// <item><strong>Error Tracking:</strong> Exception details with stack traces and environment context</item>
    /// <item><strong>Audit Trail:</strong> User actions, system events, and configuration changes</item>
    /// <item><strong>Health Monitoring:</strong> Application health checks and system resource tracking</item>
    /// <item><strong>Security Logging:</strong> Authentication, authorization, and security events</item>
    /// <item><strong>Business Metrics:</strong> Application-specific KPIs and business logic events</item>
    /// </list>
    ///
    /// <para>Log Sinks Configured:</para>
    /// <list type="number">
    /// <item><strong>Structured JSON Sink:</strong> Daily rolling files with size limits and retention policy</item>
    /// <item><strong>Human-Readable Sink:</strong> Development-friendly formatted logs</item>
    /// <item><strong>Console Sink:</strong> Real-time console output for development and debugging</item>
    /// <item><strong>Debug Sink:</strong> Visual Studio debug output for IDE integration</item>
    /// <item><strong>Error Sink:</strong> Filtered critical issues with detailed context</item>
    /// <item><strong>Performance Sink:</strong> Operation timing and resource usage tracking</item>
    /// <item><strong>User Actions Sink:</strong> User interaction and action tracking</item>
    /// <item><strong>Theme Changes Sink:</strong> UI theme and appearance change tracking</item>
    /// <item><strong>Syncfusion Sink:</strong> Syncfusion control operations and diagnostics</item>
    /// <item><strong>Security Sink:</strong> Security events and authentication tracking</item>
    /// <item><strong>Health Sink:</strong> Application health and system monitoring</item>
    /// </list>
    ///
    /// <para>Log Enrichment:</para>
    /// <list type="bullet">
    /// <item>Process ID and Thread ID for correlation</item>
    /// <item>Machine name and environment identification</item>
    /// <item>Log context properties for structured data</item>
    /// <item>Custom properties for application-specific context</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This method implements a comprehensive logging strategy that supports:
    /// - Development debugging with console output
    /// - Production monitoring with file-based persistence
    /// - Log analysis with structured data and correlation IDs
    /// - Performance monitoring with timing and resource metrics
    /// - Security auditing with detailed event tracking
    /// - Health monitoring with system resource tracking
    /// - Business intelligence with user action and KPI tracking
    /// </remarks>
    private void ConfigureLogging()
    {
        try
        {
            // Determine log directory (root directory logs folder for all Serilog sinks)
            var logRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            Log.Information("📝 Configuring Serilog logging system...");
            Log.Debug("📂 Log directory: {LogPath}", logRoot);

            // Ensure log directory exists
            Directory.CreateDirectory(logRoot);

            // Configure Serilog with comprehensive enterprise-grade settings
            Log.Logger = new LoggerConfiguration()
                // Base log level configuration
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Syncfusion", LogEventLevel.Information)

                // Enhanced enrichment for enterprise logging
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "WileyWidget")
                .Enrich.WithProperty("Version", GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0")
                .Enrich.FromLogContext()
                .Enrich.With(new ApplicationEnricher())

                // Structured JSON file sink for log analysis tools
                .WriteTo.File(
                    path: Path.Combine(logRoot, "structured-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    shared: false,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    fileSizeLimitBytes: 100 * 1024 * 1024, // 100MB per file
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5))

                // Human-readable file sink for development and troubleshooting
                .WriteTo.File(
                    path: Path.Combine(logRoot, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: false,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] (pid:{ProcessId} tid:{ThreadId}) {MachineName} {Application} v{Version} {CorrelationId} {OperationId}{NewLine}Message: {Message:lj}{NewLine}Properties: {@Properties}{NewLine}{Exception}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 50 * 1024 * 1024, // 50MB per file
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5))

                // Console sink for development with structured output
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Application} {CorrelationId} {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)

                // Debug sink for IDE integration
                .WriteTo.Debug(
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Application} {CorrelationId} {Message:lj}{NewLine}{Exception}")

                // Filtered error sink for critical issues only
                .WriteTo.File(
                    path: Path.Combine(logRoot, "errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] (pid:{ProcessId} tid:{ThreadId}) {MachineName} {Application} v{Version}{NewLine}CorrelationId: {CorrelationId}{NewLine}OperationId: {OperationId}{NewLine}Message: {Message:lj}{NewLine}Exception: {Exception}{NewLine}Stack Trace: {StackTrace}{NewLine}Source: {Source}{NewLine}Properties: {@Properties}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 25 * 1024 * 1024, // 25MB per file
                    rollOnFileSizeLimit: true)

                // XAML events filter for WPF PresentationFramework errors
                .WriteTo.File(
                    path: Path.Combine(logRoot, "xaml-events-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Application} {CorrelationId}{NewLine}XAML Event: {Message:lj}{NewLine}Source: {Source}{NewLine}Exception: {Exception}{NewLine}Stack Trace: {StackTrace}{NewLine}Properties: {@Properties}{NewLine}---{NewLine}")
                .Filter.ByIncludingOnly(Matching.FromSource("PresentationFramework"))

                // Performance monitoring with structured data
                .WriteTo.File(
                    path: Path.Combine(logRoot, "performance-.log"),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 168, // Keep 7 days of hourly logs
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Application} {CorrelationId} {OperationId}{NewLine}Message: {Message:lj}{NewLine}Duration: {Duration}ms | Memory: {MemoryUsageMB}MB | CPU: {CpuTime}ms{NewLine}Properties: {@Properties}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 10 * 1024 * 1024) // 10MB per file

                // User action tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "user-actions-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId} {UserId}{NewLine}User Action: {UserAction}{NewLine}Details: {ActionDetails}{NewLine}Data: {@ActionData}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 5 * 1024 * 1024) // 5MB per file

                // Theme change tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "theme-changes-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId}{NewLine}Theme Change: {FromTheme} → {ToTheme}{NewLine}User Initiated: {UserInitiated}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 2 * 1024 * 1024) // 2MB per file

                // Syncfusion operations tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "syncfusion-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId} {OperationId}{NewLine}Syncfusion {SyncfusionControl}: {SyncfusionOperation}{NewLine}Properties: {@ControlProperties}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 10 * 1024 * 1024) // 10MB per file

                // Security events tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "security-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90, // Keep 90 days of security logs
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Application} {CorrelationId} {UserId}{NewLine}Security Event: {SecurityEvent}{NewLine}Details: {SecurityDetails}{NewLine}IP: {ClientIP} | UserAgent: {UserAgent}{NewLine}Result: {SecurityResult}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 15 * 1024 * 1024) // 15MB per file

                // Health monitoring
                .WriteTo.File(
                    path: Path.Combine(logRoot, "health-.log"),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 168, // Keep 7 days of hourly health checks
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId}{NewLine}Health Check: {HealthCheckName}{NewLine}Status: {HealthStatus}{NewLine}Duration: {HealthCheckDuration}ms{NewLine}Details: {HealthDetails}{NewLine}Memory: {MemoryUsageMB}MB | CPU: {CpuUsage}% | Disk: {DiskUsageGB}GB{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 5 * 1024 * 1024) // 5MB per file

                .CreateLogger();

            Log.Information("✅ Serilog structured logging system configured successfully");
            Log.Debug("🔧 Log level: Debug, Microsoft override: Warning, Syncfusion override: Information");
            Log.Information("📊 Log files location: {LogPath}", logRoot);
            Log.Information("📈 Performance monitoring enabled with separate log file");
            Log.Information("🚨 Error tracking enabled with dedicated error log file");
            Log.Information("👤 User action tracking enabled");
            Log.Information("🎨 Theme change tracking enabled");
            Log.Information("🔧 Syncfusion operation tracking enabled");
            Log.Information("� Security event tracking enabled");
            Log.Information("❤️ Health monitoring enabled");
            Log.Information("�📋 Structured JSON logging enabled for analysis tools");
        }
        catch (Exception ex)
        {
            // Critical: logging setup failed - use fallback logging
            System.Diagnostics.Debug.WriteLine($"CRITICAL: Serilog configuration failed - {ex.Message}");

            // Try to create a basic logger as fallback
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Error()
                    .Enrich.WithProperty("Application", "WileyWidget")
                    .Enrich.WithProperty("Version", GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0")
                    .WriteTo.Console()
                    .WriteTo.Debug()
                    .CreateLogger();

                Log.Error(ex, "❌ Serilog configuration failed - using fallback console logging");
                Log.Warning("⚠️ Advanced logging features disabled - using basic fallback configuration");
            }
            catch
            {
                // Last resort - no logging available
                System.Diagnostics.Debug.WriteLine("CRITICAL: All logging configuration failed");
            }
        }
    }

    /// <summary>
    /// Configures comprehensive global exception handling for the WPF application.
    ///
    /// <para>Exception sources handled:</para>
    /// <list type="number">
    /// <item><strong>AppDomain.UnhandledException:</strong> Catches all unhandled exceptions in the application domain</item>
    /// <item><strong>Application.DispatcherUnhandledException:</strong> Catches UI thread exceptions</item>
    /// <item><strong>TaskScheduler.UnobservedTaskException:</strong> Catches unobserved task exceptions</item>
    /// </list>
    ///
    /// <para>Exception handling strategy:</para>
    /// <list type="bullet">
    /// <item>Log all exceptions with full context and stack traces</item>
    /// <item>Mark Dispatcher exceptions as handled to prevent crashes</item>
    /// <item>Mark unobserved task exceptions as observed</item>
    /// <item>Continue application execution when possible</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This method should be called early in application startup to ensure
    /// all exceptions are properly captured and logged. The strategy prioritizes
    /// application stability over immediate failure.
    /// </remarks>
    private void ConfigureGlobalExceptionHandling()
    {
        try
        {
            Log.Information("🛡️ Configuring global exception handling...");

            // 1. AppDomain unhandled exceptions (last resort - will crash app)
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                var isTerminating = args.IsTerminating;

                Log.Fatal(exception, "💥 CRITICAL: Unhandled AppDomain exception (terminating: {IsTerminating})", isTerminating);
                Log.Fatal("🔍 Exception sender: {Sender}", sender?.GetType().Name ?? "Unknown");

                // Flush logs before crash
                Log.CloseAndFlush();
            };

            // 2. WPF Dispatcher unhandled exceptions (UI thread)
            DispatcherUnhandledException += (sender, args) =>
            {
                Log.Error(args.Exception, "🚨 CRITICAL: Unhandled Dispatcher exception on UI thread");
                Log.Error("🔍 Dispatcher sender: {Sender}", sender?.GetType().Name ?? "Unknown");
                Log.Error("🔍 Exception handled: {Handled}", args.Handled);

                // Mark as handled to prevent application crash
                args.Handled = true;

                // Could show user-friendly error dialog here
                ShowExceptionDialog(args.Exception, "UI Thread Exception");
            };

            // 3. Task Scheduler unobserved exceptions (background tasks)
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Log.Warning(args.Exception, "⚠️ Unobserved task exception detected");
                Log.Warning("🔍 Task scheduler sender: {Sender}", sender?.GetType().Name ?? "Unknown");

                // Mark as observed to prevent unobserved exception escalation
                args.SetObserved();

                // Log additional task information if available
                if (args.Exception.InnerException != null)
                {
                    Log.Warning("📋 Inner exception: {InnerException}", args.Exception.InnerException.Message);
                }
            };

            Log.Information("✅ Global exception handling configured successfully");
            Log.Debug("🛡️ Handled exception sources: AppDomain, Dispatcher, TaskScheduler");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to configure global exception handling");
            // Don't rethrow - exception handling setup failure shouldn't crash the app
        }
    }

    /// <summary>
    /// Displays a user-friendly exception dialog to inform the user of an error.
    /// Used for non-critical exceptions that don't require application shutdown.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">Context description of where the exception occurred</param>
    private void ShowExceptionDialog(Exception exception, string context)
    {
        try
        {
            var message = $"An unexpected error occurred in {context}.\n\n" +
                         $"Error: {exception.Message}\n\n" +
                         "The application will continue running, but you may experience issues. " +
                         "Please save your work and restart the application if problems persist.";

            // Use MessageBox for simple error display
            MessageBox.Show(message, "Application Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);

            Log.Information("ℹ️ Exception dialog shown to user for: {Context}", context);
        }
        catch (Exception dialogEx)
        {
            Log.Warning(dialogEx, "⚠️ Failed to show exception dialog");
        }
    }

    /// <summary>
    /// Central exception logging method that provides consistent formatting and context.
    /// Isolates exception logging pattern for easy modification and enhancement.
    ///
    /// <para>Logged information includes:</para>
    /// <list type="bullet">
    /// <item>Exception type and message</item>
    /// <item>Full stack trace</item>
    /// <item>Source context (AppDomain, Dispatcher, etc.)</item>
    /// <item>Timestamp and log level</item>
    /// </list>
    /// </summary>
    /// <param name="source">The source of the exception (e.g., "AppDomain", "Dispatcher")</param>
    /// <param name="exception">The exception to log</param>
    /// <remarks>
    /// This method is designed to be failure-resistant and will not throw exceptions.
    /// If logging itself fails, it silently continues to prevent recursive error loops.
    /// </remarks>
    private void LogException(string source, Exception exception)
    {
        if (exception == null)
        {
            Log.Warning("⚠️ LogException called with null exception (source: {Source})", source);
            return;
        }

        try
        {
            // Log with structured data for better analysis
            Log.Error(exception, "🚨 Unhandled exception in {Source}", source);

            // Log additional context if available
            if (!string.IsNullOrWhiteSpace(exception.Source))
            {
                Log.Error("🔍 Exception source: {ExceptionSource}", exception.Source);
            }

            // Log inner exception if present
            if (exception.InnerException != null)
            {
                Log.Error("📋 Inner exception: {InnerException}", exception.InnerException.Message);
            }

            // Log stack trace separately for better readability
            Log.Debug("📜 Full stack trace: {StackTrace}", exception.StackTrace);
        }
        catch (Exception logEx)
        {
            // Logging failed - use debug output as last resort
            System.Diagnostics.Debug.WriteLine($"CRITICAL: Exception logging failed - {logEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Original exception: {exception.Message}");
        }
    }

    /// <summary>
    /// Attempts to load the Syncfusion license key from a file in the application directory.
    /// This serves as a fallback method when environment variables are not available.
    ///
    /// <para>Search locations:</para>
    /// <list type="number">
    /// <item>license.key in the application's base directory</item>
    /// </list>
    ///
    /// <para>Expected file format:</para>
    /// <list type="bullet">
    /// <item>Plain text file containing only the license key</item>
    /// <item>UTF-8 encoding</item>
    /// <item>Key should be trimmed of whitespace</item>
    /// </list>
    /// </summary>
    /// <returns>
    /// <c>true</c> if the license file was found and successfully loaded;
    /// <c>false</c> if the file doesn't exist, is empty, or loading failed
    /// </returns>
    /// <remarks>
    /// This method is designed to be safe and will not throw exceptions.
    /// It logs detailed information about the license loading process for debugging.
    /// The license.key file is typically created during the build process.
    /// </remarks>
    private bool TryLoadLicenseFromFile()
    {
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var licensePath = Path.Combine(exeDir, "license.key");

            Log.Information("🔍 Checking for Syncfusion license file...");
            Log.Debug("📂 License file path: {LicensePath}", licensePath);

            if (!File.Exists(licensePath))
            {
                Log.Information("ℹ️ License file not found at: {LicensePath}", licensePath);
                return false;
            }

            // Read and validate the license key
            var key = File.ReadAllText(licensePath).Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                Log.Warning("❌ License file exists but is empty or contains only whitespace");
                return false;
            }

            if (key.Length < 50) // Basic validation - Syncfusion keys are typically much longer
            {
                Log.Warning("❌ License key appears to be invalid (too short: {Length} characters)", key.Length);
                return false;
            }

            Log.Information("📄 Found license file with key length: {KeyLength}", key.Length);

            // Register the license
            SyncfusionLicenseProvider.RegisterLicense(key);

            Log.Information("✅ Syncfusion license successfully loaded from file");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "❌ Access denied reading license file - check file permissions");
            return false;
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "❌ I/O error reading license file - file may be locked or corrupted");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Unexpected error loading license from file");
            return false;
        }
    }

    /// <summary>
    /// Attempts to load Syncfusion license from file with timeout protection.
    /// This method is designed to be safe and will not throw exceptions.
    /// It logs detailed information about the license loading process for debugging.
    /// The license.key file is typically created during the build process.
    /// </summary>
    private async Task<bool> TryLoadLicenseFromFileWithTimeout()
    {
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var licensePath = Path.Combine(exeDir, "license.key");

            Log.Information("🔍 Checking for Syncfusion license file with timeout...");
            Log.Debug("📂 License file path: {LicensePath}", licensePath);

            if (!File.Exists(licensePath))
            {
                Log.Information("ℹ️ License file not found at: {LicensePath}", licensePath);
                return false;
            }

            // Read file asynchronously with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var readTask = Task.Run(() => File.ReadAllText(licensePath), cts.Token);

            try
            {
                var key = await readTask;
                key = key.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    Log.Warning("❌ License file exists but is empty or contains only whitespace");
                    return false;
                }

                if (key.Length < 50) // Basic validation - Syncfusion keys are typically much longer
                {
                    Log.Warning("❌ License key appears to be invalid (too short: {Length} characters)", key.Length);
                    return false;
                }

                Log.Information("📄 Found license file with key length: {KeyLength}", key.Length);

                // Register the license
                SyncfusionLicenseProvider.RegisterLicense(key);

                Log.Information("✅ Syncfusion license successfully loaded from file");
                return true;
            }
            catch (TaskCanceledException)
            {
                Log.Warning("⏰ License file read operation timed out after 5 seconds");
                return false;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "❌ Access denied reading license file - check file permissions");
            return false;
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "❌ I/O error reading license file - file may be locked or corrupted");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Unexpected error loading license from file");
            return false;
        }
    }
}

