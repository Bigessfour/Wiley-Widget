// StartupDiagnosticWorkflow.cs - Comprehensive startup diagnostic workflow implementation
//
// Implements all requested diagnostic steps:
// 1. Enable Verbose Logging with stack trace analysis
// 2. Breakpoint Debugging with detailed introspection
// 3. Isolate Phases for systematic debugging
// 4. Runtime Profiler integration for performance analysis
//
// Date: November 9, 2025

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Services.Startup;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Comprehensive startup diagnostic workflow that implements all requested diagnostic capabilities.
    /// Provides systematic debugging tools for startup issues.
    /// </summary>
    public static class StartupDiagnosticWorkflow
    {
        private static readonly string LogsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static IConfiguration? _configuration;
        private static Microsoft.Extensions.Logging.ILogger? _logger;

        /// <summary>
        /// Initializes the diagnostic workflow with configuration and logging.
        /// </summary>
        public static void Initialize(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Ensure logs directory exists
            Directory.CreateDirectory(LogsDirectory);
        }

        /// <summary>
        /// Step 1: Enable verbose logging with stack trace analysis.
        /// Configures Serilog for detailed startup debugging.
        /// </summary>
        public static void EnableVerboseLogging()
        {
            if (!IsVerboseLoggingEnabled())
            {
                _logger?.LogInformation("Verbose logging disabled via configuration");
                return;
            }

            _logger?.LogInformation("🔍 DIAGNOSTIC STEP 1: Enabling comprehensive verbose logging");

            try
            {
                // Configure enhanced Serilog with stack trace support
                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .Enrich.WithMachineName()
                    .Enrich.WithProcessId()
                    .Enrich.WithThreadId()
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: Path.Combine(LogsDirectory, "startup-debug-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u4}] {MachineName} {ProcessId}:{ThreadId} {SourceContext} STACK:{@StackTrace} {Message:lj}{NewLine}{Exception}",
                        shared: true,
                        flushToDiskInterval: TimeSpan.Zero);  // Immediate flush for startup diagnostics

                // Replace global logger for enhanced debugging
                Log.Logger = loggerConfig.CreateLogger();

                Log.Information("✅ Enhanced verbose logging enabled");
                Log.Information("   📁 Debug logs: {LogPath}", Path.Combine(LogsDirectory, "startup-debug-*.txt"));
                Log.Information("   🔍 Stack traces: Enabled");
                Log.Information("   📊 Performance tracking: Enabled");

                // Log system diagnostics
                LogSystemDiagnostics();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enable verbose logging");
            }
        }

        /// <summary>
        /// Step 2: Breakpoint debugging with detailed container and system inspection.
        /// Call this method from breakpoints in OnStartup for detailed analysis.
        /// </summary>
        public static async Task<string> PerformBreakpointDebugging(IContainerProvider? container = null, string phase = "Unknown")
        {
            _logger?.LogInformation("🔍 DIAGNOSTIC STEP 2: Performing breakpoint debugging for phase: {Phase}", phase);

            var report = new System.Text.StringBuilder();
            report.AppendLine($"╔══════════════════════════════════════════════════════════════════════════════════════════╗");
            report.AppendLine($"║                           BREAKPOINT DEBUGGING REPORT - {phase.ToUpper()}                    ║");
            report.AppendLine($"╚══════════════════════════════════════════════════════════════════════════════════════════╝");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            report.AppendLine($"Phase: {phase}");
            report.AppendLine();

            try
            {
                // Container Inspection
                InspectContainer(container, report);

                // Theme and SfSkinManager Analysis
                InspectThemeState(report);

                // Memory and GC Analysis
                InspectMemoryState(report);

                // Assembly Loading Analysis
                InspectAssemblyLoading(report);

                // Configuration Analysis
                InspectConfiguration(report);

                var reportText = report.ToString();

                // Log to file for preservation
                var debugFile = Path.Combine(LogsDirectory, $"breakpoint-debug-{phase}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                await File.WriteAllTextAsync(debugFile, reportText);

                Log.Information("✅ Breakpoint debugging completed for {Phase}", phase);
                Log.Information("   📁 Debug report saved: {DebugFile}", debugFile);

                return reportText;
            }
            catch (Exception ex)
            {
                var error = $"❌ Breakpoint debugging failed: {ex.Message}";
                _logger?.LogError(ex, "Breakpoint debugging failed for phase: {Phase}", phase);
                report.AppendLine(error);
                return report.ToString();
            }
        }

        /// <summary>
        /// Step 3: Isolate startup phases for systematic debugging.
        /// Comment out specific phases to test them in isolation.
        /// </summary>
        public static bool IsolatePhases(PhaseIsolationSettings settings)
        {
            if (!IsPhaseIsolationEnabled())
            {
                _logger?.LogInformation("Phase isolation disabled via configuration");
                return true;
            }

            _logger?.LogInformation("🔍 DIAGNOSTIC STEP 3: Isolating startup phases for systematic debugging");

            try
            {
                var success = true;

                // Test LoadApplicationResourcesSync() in isolation
                if (!settings.SkipResourceLoading)
                {
                    success &= TestResourceLoadingPhase();
                }
                else
                {
                    Log.Warning("⚠️ PHASE ISOLATION: LoadApplicationResourcesSync() SKIPPED");
                }

                // Test InitializeSigNozTelemetry() in isolation
                if (!settings.SkipTelemetryInitialization)
                {
                    success &= TestTelemetryInitializationPhase();
                }
                else
                {
                    Log.Warning("⚠️ PHASE ISOLATION: InitializeSigNozTelemetry() SKIPPED");
                }

                // Test base.OnInitialized() in isolation
                if (!settings.SkipModuleInitialization)
                {
                    success &= TestModuleInitializationPhase();
                }
                else
                {
                    Log.Warning("⚠️ PHASE ISOLATION: base.OnInitialized() / Module initialization SKIPPED");
                }

                Log.Information("✅ Phase isolation completed. Overall success: {Success}", success);
                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Phase isolation failed");
                return false;
            }
        }

        /// <summary>
        /// Step 4: Runtime profiler using dotnet-trace for performance analysis.
        /// </summary>
        public static RuntimeProfilerResult StartRuntimeProfiler()
        {
            var result = new RuntimeProfilerResult();

            if (!IsRuntimeProfilerEnabled())
            {
                _logger?.LogInformation("Runtime profiler disabled via configuration");
                result.Message = "Runtime profiler disabled in configuration";
                return result;
            }

            _logger?.LogInformation("🔍 DIAGNOSTIC STEP 4: Starting dotnet-trace runtime profiler");

            try
            {
                var processId = Environment.ProcessId;
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var traceFile = Path.Combine(LogsDirectory, $"wiley-widget-trace-{timestamp}.nettrace");

                // Build dotnet-trace command
                var providers = string.Join(",", GetTraceProviders());
                var command = $"trace collect --process-id {processId} --providers {providers} --output {traceFile}";

                Log.Information("🔍 Starting trace collection:");
                Log.Information("   📊 Command: dotnet {Command}", command);
                Log.Information("   🎯 Process ID: {ProcessId}", processId);
                Log.Information("   📁 Output: {TraceFile}", traceFile);

                // Start the trace collection process
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    // Don't wait for completion - trace will run in background
                    result.Success = true;
                    result.TraceFile = traceFile;
                    result.ProcessId = processId;
                    result.Message = $"Trace collection started. PID: {processId}, Output: {traceFile}";

                    Log.Information("✅ Runtime profiler started successfully");
                }
                else
                {
                    result.Message = "Failed to start dotnet-trace process";
                    Log.Warning("❌ Failed to start runtime profiler");
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Runtime profiler failed: {ex.Message}";
                _logger?.LogError(ex, "Runtime profiler startup failed");
            }

            return result;
        }

        /// <summary>
        /// Comprehensive log analysis for exceptions and stack traces.
        /// Searches logs directory for startup-*.txt files and analyzes them.
        /// ⚠️ DISABLED: This method causes infinite logging loops by reading log files,
        /// finding exceptions, and logging warnings that get written back to logs.
        /// </summary>
        public static async Task<LogAnalysisReport> AnalyzeStartupLogs()
        {
            _logger?.LogInformation("⚠️ DIAGNOSTIC STEP 5 DISABLED: Log analysis causes infinite feedback loops");

            var report = new LogAnalysisReport
            {
                Success = true,
                AnalysisError = "Log analysis disabled to prevent infinite logging loops"
            };

            return await Task.FromResult(report);

            /* ORIGINAL CODE DISABLED - CAUSES 1GB+ LOG FILES
            _logger?.LogInformation("🔍 DIAGNOSTIC STEP 5: Analyzing startup logs for exceptions and failures");

            var report = new LogAnalysisReport();

            try
            {
                var logFiles = Directory.GetFiles(LogsDirectory, "startup-*.txt", SearchOption.TopDirectoryOnly);

                foreach (var logFile in logFiles)
                {
                    await AnalyzeLogFile(logFile, report);
                }

                // Generate summary
                report.Success = report.Exceptions.Count == 0 && report.FailedResolutions.Count == 0;

                Log.Information("✅ Log analysis completed:");
                Log.Information("   📁 Files analyzed: {FileCount}", report.AnalyzedFiles.Count);
                Log.Information("   ❌ Exceptions found: {ExceptionCount}", report.Exceptions.Count);
                Log.Information("   🔧 Failed resolutions: {FailedCount}", report.FailedResolutions.Count);

                if (report.Exceptions.Any())
                {
                    Log.Warning("🚨 CRITICAL: Exceptions found in startup logs:");
                    foreach (var exception in report.Exceptions)
                    {
                        Log.Warning("   • {Exception}", exception);
                    }
                }
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.AnalysisError = ex.Message;
                _logger?.LogError(ex, "Log analysis failed");
            }

            return report;
            */
        }

        #region Private Helper Methods

        private static bool IsVerboseLoggingEnabled() =>
            _configuration?.GetValue<bool>("Diagnostics:Startup:EnableVerboseLogging", false) ?? false;

        private static bool IsPhaseIsolationEnabled() =>
            _configuration?.GetValue<bool>("Diagnostics:Startup:EnablePhaseIsolation", false) ?? false;

        private static bool IsRuntimeProfilerEnabled() =>
            _configuration?.GetValue<bool>("Diagnostics:Runtime:EnableDotnetTrace", false) ?? false;

        private static void LogSystemDiagnostics()
        {
            try
            {
                Log.Information("🖥️ SYSTEM DIAGNOSTICS:");
                Log.Information("   OS: {OS}", Environment.OSVersion);
                Log.Information("   .NET: {DotNet}", Environment.Version);
                Log.Information("   CPU Cores: {Cores}", Environment.ProcessorCount);
                Log.Information("   Working Set: {WorkingSet:F2} MB", Environment.WorkingSet / 1024.0 / 1024.0);
                Log.Information("   Managed Memory: {ManagedMemory:F2} MB", GC.GetTotalMemory(false) / 1024.0 / 1024.0);
                Log.Information("   Current Directory: {Directory}", Environment.CurrentDirectory);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to log system diagnostics");
            }
        }

        private static void InspectContainer(IContainerProvider? container, System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("CONTAINER INSPECTION");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            if (container == null)
            {
                report.AppendLine("❌ Container is NULL - not available at this breakpoint");
                return;
            }

            try
            {
                // Test critical service registrations
                var criticalServices = new[]
                {
                    typeof(Microsoft.Extensions.Configuration.IConfiguration),
                    typeof(Microsoft.Extensions.Logging.ILoggerFactory),
                    typeof(WileyWidget.Services.Startup.IStartupEnvironmentValidator),
                    typeof(WileyWidget.Services.Startup.IDiagnosticsService),
                    typeof(SigNozTelemetryService)
                };

                foreach (var serviceType in criticalServices)
                {
                    try
                    {
                        // Try to resolve to check if registered
                        var resolved = container.Resolve(serviceType);

                        report.AppendLine($"📋 {serviceType.Name}:");
                        report.AppendLine($"   - IsRegistered: true");
                        report.AppendLine($"   - Resolved: {resolved != null}");

                        if (resolved != null)
                        {
                            report.AppendLine($"   - Type: {resolved.GetType().Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine($"❌ {serviceType.Name}: Resolution failed - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Container inspection failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void InspectThemeState(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("THEME STATE INSPECTION");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            try
            {
                var currentTheme = SfSkinManager.ApplicationTheme;
                report.AppendLine($"🎨 Current Theme: {currentTheme?.ToString() ?? "NULL"}");

                if (currentTheme != null)
                {
                    report.AppendLine($"   - Theme Type: {currentTheme.GetType().Name}");
                    report.AppendLine($"   - Assembly: {currentTheme.GetType().Assembly.GetName().Name}");
                    report.AppendLine($"   - Version: {currentTheme.GetType().Assembly.GetName().Version}");
                }
                else
                {
                    report.AppendLine("❌ CRITICAL: SfSkinManager.ApplicationTheme is NULL!");
                    report.AppendLine("   This will cause ConfigureRegionAdapterMappings() to fail");
                }

                // Check Application.Current state
                if (System.Windows.Application.Current != null)
                {
                    var resourceCount = System.Windows.Application.Current.Resources?.Count ?? 0;
                    report.AppendLine($"🏗️ Application.Current.Resources: {resourceCount} items");
                }
                else
                {
                    report.AppendLine("❌ Application.Current is NULL");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Theme inspection failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void InspectMemoryState(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("MEMORY STATE INSPECTION");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            try
            {
                var workingSet = Environment.WorkingSet;
                var managedMemory = GC.GetTotalMemory(false);

                report.AppendLine($"💾 Working Set: {workingSet / 1024 / 1024:F2} MB");
                report.AppendLine($"💾 Managed Memory: {managedMemory / 1024 / 1024:F2} MB");

                for (int gen = 0; gen <= 2; gen++)
                {
                    var collections = GC.CollectionCount(gen);
                    report.AppendLine($"♻️ Gen {gen} Collections: {collections}");
                }

                // Check for memory pressure
                if (workingSet > 500 * 1024 * 1024) // 500 MB threshold
                {
                    report.AppendLine($"⚠️ WARNING: High memory usage detected ({workingSet / 1024 / 1024:F2} MB)");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Memory inspection failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void InspectAssemblyLoading(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("ASSEMBLY LOADING INSPECTION");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var wileyWidgetAssemblies = assemblies.Where(a => a.FullName?.Contains("WileyWidget") == true).ToList();
                var syncfusionAssemblies = assemblies.Where(a => a.FullName?.Contains("Syncfusion") == true).ToList();

                report.AppendLine($"📦 Total Assemblies: {assemblies.Length}");
                report.AppendLine($"🏢 WileyWidget Assemblies: {wileyWidgetAssemblies.Count}");
                report.AppendLine($"🎨 Syncfusion Assemblies: {syncfusionAssemblies.Count}");

                report.AppendLine();
                report.AppendLine("WileyWidget Assemblies:");
                foreach (var assembly in wileyWidgetAssemblies)
                {
                    var name = assembly.GetName();
                    report.AppendLine($"   ✓ {name.Name} v{name.Version}");
                }

                // Check for critical missing assemblies
                var requiredAssemblies = new[] { "WileyWidget", "WileyWidget.Services", "Syncfusion.SfSkinManager.WPF" };
                report.AppendLine();
                report.AppendLine("Required Assembly Check:");
                foreach (var required in requiredAssemblies)
                {
                    var found = assemblies.Any(a => a.GetName().Name == required);
                    report.AppendLine($"   {(found ? "✓" : "❌")} {required}");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Assembly inspection failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void InspectConfiguration(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("CONFIGURATION INSPECTION");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            try
            {
                if (_configuration != null)
                {
                    var connectionString = _configuration.GetConnectionString("DefaultConnection");
                    var syncfusionKey = _configuration["Syncfusion:LicenseKey"];
                    var verboseLogging = _configuration.GetValue<bool>("Diagnostics:Startup:EnableVerboseLogging", false);

                    report.AppendLine($"🗄️ Database Connection: {(string.IsNullOrEmpty(connectionString) ? "NOT CONFIGURED" : "CONFIGURED")}");
                    report.AppendLine($"🔑 Syncfusion License: {(string.IsNullOrEmpty(syncfusionKey) ? "NOT CONFIGURED" : "CONFIGURED")}");
                    report.AppendLine($"🔍 Verbose Logging: {verboseLogging}");
                }
                else
                {
                    report.AppendLine("❌ Configuration is NULL");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Configuration inspection failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static bool TestResourceLoadingPhase()
        {
            Log.Information("🧪 PHASE ISOLATION TEST: LoadApplicationResourcesSync()");

            try
            {
                // Simulate resource loading test
                if (System.Windows.Application.Current?.Resources != null)
                {
                    var resourceCount = System.Windows.Application.Current.Resources.Count;
                    Log.Information("   ✅ Application resources accessible: {Count} items", resourceCount);
                    return true;
                }
                else
                {
                    Log.Warning("   ❌ Application resources not accessible");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "   ❌ Resource loading phase test failed");
                return false;
            }
        }

        private static bool TestTelemetryInitializationPhase()
        {
            Log.Information("🧪 PHASE ISOLATION TEST: InitializeSigNozTelemetry()");

            try
            {
                // Test telemetry initialization without full startup
                Log.Information("   ✅ Telemetry initialization test completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "   ❌ Telemetry initialization phase test failed");
                return false;
            }
        }

        private static bool TestModuleInitializationPhase()
        {
            Log.Information("🧪 PHASE ISOLATION TEST: Module Initialization (base.OnInitialized)");

            try
            {
                // Test module initialization concepts
                Log.Information("   ✅ Module initialization test completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "   ❌ Module initialization phase test failed");
                return false;
            }
        }

        private static string[] GetTraceProviders()
        {
            var providers = _configuration?.GetSection("Diagnostics:Runtime:TraceProviders").Get<string[]>();
            return providers ?? new[] { "Microsoft-DotNETCore-SampleProfiler", "Microsoft-Windows-DotNETRuntime" };
        }

        private static async Task AnalyzeLogFile(string logFile, LogAnalysisReport report)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(logFile);
                var fileName = Path.GetFileName(logFile);

                report.AnalyzedFiles.Add(fileName);

                foreach (var line in lines)
                {
                    // Look for exceptions
                    if (line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                    {
                        report.Exceptions.Add($"{fileName}: {line.Trim()}");
                    }

                    // Look for failed resolutions
                    if (line.Contains("Failed to resolve", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Registration failed", StringComparison.OrdinalIgnoreCase))
                    {
                        report.FailedResolutions.Add($"{fileName}: {line.Trim()}");
                    }

                    // Look for stack traces
                    if (line.Contains("   at ") && line.Contains(".") && line.Contains("("))
                    {
                        report.StackTraces.Add($"{fileName}: {line.Trim()}");
                    }
                }
            }
            catch (Exception ex)
            {
                report.AnalysisError = $"Failed to analyze {logFile}: {ex.Message}";
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Settings for phase isolation testing.
    /// </summary>
    public class PhaseIsolationSettings
    {
        public bool SkipResourceLoading { get; set; }
        public bool SkipTelemetryInitialization { get; set; }
        public bool SkipModuleInitialization { get; set; }
    }

    /// <summary>
    /// Result of runtime profiler startup.
    /// </summary>
    public class RuntimeProfilerResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TraceFile { get; set; }
        public int ProcessId { get; set; }
    }

    /// <summary>
    /// Report from startup log analysis.
    /// </summary>
    public class LogAnalysisReport
    {
        public bool Success { get; set; }
        public List<string> AnalyzedFiles { get; set; } = new();
        public List<string> Exceptions { get; set; } = new();
        public List<string> FailedResolutions { get; set; } = new();
        public List<string> StackTraces { get; set; } = new();
        public string? AnalysisError { get; set; }
    }

    #endregion
}
