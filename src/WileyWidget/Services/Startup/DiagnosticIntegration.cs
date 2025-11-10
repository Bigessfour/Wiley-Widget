// DiagnosticIntegration.cs - Integration layer for comprehensive startup diagnostics
//
// Provides a simple interface to integrate all diagnostic capabilities without compilation issues.
// Uses the existing diagnostic services already registered in the DI container.
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

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Integration service that provides simplified access to comprehensive diagnostic features.
    /// Uses existing registered services to avoid compilation dependencies.
    /// </summary>
    public static class DiagnosticIntegration
    {
        private static string LogsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        /// <summary>
        /// Step 1: Enable verbose logging for startup debugging.
        /// Instead of reconfiguring the logger (which causes failures), we use Serilog.Debugging
        /// and configure the logger correctly from the start in App.xaml.cs static constructor.
        /// </summary>
        public static void EnableVerboseStartupLogging(IConfiguration configuration)
        {
            try
            {
                var verboseEnabled = configuration.GetValue<bool>("Diagnostics:Startup:EnableVerboseLogging", false);
                if (!verboseEnabled)
                {
                    Log.Information("Verbose startup logging disabled via configuration");
                    return;
                }

                Log.Information("🔍 DIAGNOSTIC STEP 1: Enhanced startup logging active");

                // Enable Serilog self-diagnostics to troubleshoot logging issues
                Serilog.Debugging.SelfLog.Enable(msg =>
                {
                    Debug.WriteLine($"[SERILOG] {msg}");
                    File.AppendAllText(
                        Path.Combine(LogsDirectory, "serilog-diagnostics.txt"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}\n");
                });

                Log.Information("   ✅ Serilog self-diagnostics enabled");
                Log.Information("   📁 Main logs: {LogPath}", Path.Combine(LogsDirectory, "wiley-widget-*.log"));
                Log.Information("   📁 Self-diagnostics: {LogPath}", Path.Combine(LogsDirectory, "serilog-diagnostics.txt"));
                Log.Information("   💡 To get verbose logging, configure the logger in App.xaml.cs with:");
                Log.Information("      - .MinimumLevel.Debug()");
                Log.Information("      - .Enrich.WithThreadId()");
                Log.Information("      - .Enrich.WithProcessId()");

                LogSystemInformation();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enable verbose startup logging");
            }
        }

        /// <summary>
        /// Step 2: Perform breakpoint debugging analysis.
        /// Call this from strategic breakpoints during startup for detailed system inspection.
        /// </summary>
        public static string PerformBreakpointAnalysis(IContainerProvider? container, string phase = "Unknown")
        {
            Log.Information("🔍 DIAGNOSTIC STEP 2: Performing breakpoint analysis for phase: {Phase}", phase);

            var report = new System.Text.StringBuilder();
            report.AppendLine($"╔═══════════════════════════════════════════════════════════════════════════════════════════════╗");
            report.AppendLine($"║                           BREAKPOINT ANALYSIS REPORT - {phase.ToUpper()}                        ║");
            report.AppendLine($"╚═══════════════════════════════════════════════════════════════════════════════════════════════╝");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            report.AppendLine($"Phase: {phase}");
            report.AppendLine($"Process ID: {Environment.ProcessId}");
            report.AppendLine();

            try
            {
                // Container Analysis
                AnalyzeContainer(container, report);

                // Theme Analysis
                AnalyzeThemeState(report);

                // Memory Analysis
                AnalyzeMemoryState(report);

                // Configuration Analysis
                AnalyzeConfigurationState(report);

                var reportText = report.ToString();

                // Save report for debugging
                var reportFile = Path.Combine(LogsDirectory, $"breakpoint-{phase}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                try
                {
                    File.WriteAllText(reportFile, reportText);
                    Log.Information("✅ Breakpoint analysis completed for {Phase} - Report saved: {ReportFile}", phase, reportFile);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to save breakpoint report to file");
                }

                return reportText;
            }
            catch (Exception ex)
            {
                var error = $"❌ Breakpoint analysis failed for {phase}: {ex.Message}";
                Log.Error(ex, "Breakpoint analysis failed for phase: {Phase}", phase);
                report.AppendLine(error);
                return report.ToString();
            }
        }

        /// <summary>
        /// Step 3: Check phase isolation configuration.
        /// Returns whether specific phases should be skipped based on configuration.
        /// </summary>
        public static (bool skipResourceLoading, bool skipTelemetry, bool skipModules) CheckPhaseIsolationSettings(IConfiguration configuration)
        {
            var phaseIsolationEnabled = configuration.GetValue<bool>("Diagnostics:Startup:EnablePhaseIsolation", false);

            if (!phaseIsolationEnabled)
            {
                return (false, false, false);
            }

            Log.Information("🔍 DIAGNOSTIC STEP 3: Checking phase isolation settings");

            var skipResourceLoading = configuration.GetValue<bool>("Diagnostics:PhaseIsolation:SkipResourceLoading", false);
            var skipTelemetry = configuration.GetValue<bool>("Diagnostics:PhaseIsolation:SkipTelemetryInitialization", false);
            var skipModules = configuration.GetValue<bool>("Diagnostics:PhaseIsolation:SkipModuleInitialization", false);

            if (skipResourceLoading)
                Log.Warning("⚠️ PHASE ISOLATION: Resource loading will be SKIPPED");
            if (skipTelemetry)
                Log.Warning("⚠️ PHASE ISOLATION: Telemetry initialization will be SKIPPED");
            if (skipModules)
                Log.Warning("⚠️ PHASE ISOLATION: Module initialization will be SKIPPED");

            return (skipResourceLoading, skipTelemetry, skipModules);
        }

        /// <summary>
        /// Step 4: Start runtime profiler if enabled.
        /// Launches dotnet-trace for performance analysis.
        /// </summary>
        public static async Task<bool> TryStartRuntimeProfiler(IConfiguration configuration)
        {
            var profilerEnabled = configuration.GetValue<bool>("Diagnostics:Runtime:EnableDotnetTrace", false);

            if (!profilerEnabled)
            {
                Log.Information("Runtime profiler disabled via configuration");
                return false;
            }

            Log.Information("🔍 DIAGNOSTIC STEP 4: Attempting to start runtime profiler");

            try
            {
                var processId = Environment.ProcessId;
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var traceFile = Path.Combine(LogsDirectory, $"wiley-widget-trace-{timestamp}.nettrace");

                // Build dotnet-trace command
                var providers = configuration.GetSection("Diagnostics:Runtime:TraceProviders").Get<string[]>()
                    ?? new[] { "Microsoft-DotNETCore-SampleProfiler", "Microsoft-Windows-DotNETRuntime" };
                var providersString = string.Join(",", providers);

                var args = $"trace collect --process-id {processId} --providers {providersString} --output {traceFile}";

                Log.Information("Starting runtime profiler:");
                Log.Information("   🎯 Process ID: {ProcessId}", processId);
                Log.Information("   📊 Providers: {Providers}", providersString);
                Log.Information("   📁 Output: {TraceFile}", traceFile);
                Log.Information("   🚀 Command: dotnet {Args}", args);

                // Check if dotnet-trace is available
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "trace --help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var testProcess = Process.Start(processInfo))
                {
                    if (testProcess != null)
                    {
                        await testProcess.WaitForExitAsync();
                        if (testProcess.ExitCode != 0)
                        {
                            Log.Warning("dotnet-trace tool not available. Install with: dotnet tool install --global dotnet-trace");
                            return false;
                        }
                    }
                }

                // Start actual tracing (don't wait for completion)
                var traceProcessInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var traceProcess = Process.Start(traceProcessInfo);
                if (traceProcess != null)
                {
                    Log.Information("✅ Runtime profiler started successfully - PID: {TracePid}", traceProcess.Id);
                    return true;
                }
                else
                {
                    Log.Warning("❌ Failed to start runtime profiler process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to start runtime profiler");
                return false;
            }
        }

        /// <summary>
        /// Step 5: Analyze startup logs for exceptions and issues.
        /// Searches recent startup logs for problems.
        /// DISABLED: This method causes infinite log feedback loops.
        /// </summary>
        public static async Task<(int exceptions, int failures)> AnalyzeRecentStartupLogs()
        {
            // DISABLED: This creates infinite feedback loop
            // The method reads log files, finds exceptions, and logs warnings
            // Those warnings get written to log files, which then get read again
            // This causes exponential log growth (646MB+ files observed)
            Log.Information("⚠️ DIAGNOSTIC STEP 5 DISABLED: Log analysis causes feedback loops");
            return await Task.FromResult((0, 0));
        }

        #region Private Helper Methods

        private static void LogSystemInformation()
        {
            try
            {
                Log.Information("🖥️ SYSTEM INFORMATION:");
                Log.Information("   OS: {OS}", Environment.OSVersion);
                Log.Information("   .NET: {DotNet}", Environment.Version);
                Log.Information("   CPU Cores: {Cores}", Environment.ProcessorCount);
                Log.Information("   Working Set: {WorkingSet:F2} MB", Environment.WorkingSet / 1024.0 / 1024.0);
                Log.Information("   Managed Memory: {ManagedMemory:F2} MB", GC.GetTotalMemory(false) / 1024.0 / 1024.0);
                Log.Information("   Base Directory: {BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);
                Log.Information("   Current Directory: {CurrentDirectory}", Environment.CurrentDirectory);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to log system information");
            }
        }

        private static void AnalyzeContainer(IContainerProvider? container, System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("CONTAINER ANALYSIS");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            if (container == null)
            {
                report.AppendLine("❌ Container is NULL - not available at this phase");
                return;
            }

            try
            {
                // Test critical service registrations
                var criticalServices = new[]
                {
                    typeof(Microsoft.Extensions.Configuration.IConfiguration),
                    typeof(Microsoft.Extensions.Logging.ILoggerFactory),
                    typeof(IStartupEnvironmentValidator),
                    typeof(IDiagnosticsService)
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
                report.AppendLine($"❌ Container analysis failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void AnalyzeThemeState(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("THEME STATE ANALYSIS");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            try
            {
                var currentTheme = Syncfusion.SfSkinManager.SfSkinManager.ApplicationTheme;
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
                    report.AppendLine("   This will cause region adapter mapping failures");
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
                report.AppendLine($"❌ Theme analysis failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void AnalyzeMemoryState(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("MEMORY STATE ANALYSIS");
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

                // Memory pressure warning
                if (workingSet > 500 * 1024 * 1024) // 500 MB threshold
                {
                    report.AppendLine($"⚠️ WARNING: High memory usage detected ({workingSet / 1024 / 1024:F2} MB)");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Memory analysis failed: {ex.Message}");
            }

            report.AppendLine();
        }

        private static void AnalyzeConfigurationState(System.Text.StringBuilder report)
        {
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");
            report.AppendLine("CONFIGURATION STATE ANALYSIS");
            report.AppendLine("═══════════════════════════════════════════════════════════════════════════");

            try
            {
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                report.AppendLine($"📁 Assembly Location: {assemblyLocation}");
                report.AppendLine($"📁 Base Directory: {baseDirectory}");
                report.AppendLine($"📁 Current Directory: {Environment.CurrentDirectory}");

                // Check for appsettings files
                var configFiles = new[] { "appsettings.json", "appsettings.Development.json" };
                foreach (var configFile in configFiles)
                {
                    var configPath = Path.Combine(baseDirectory, "config", "development", configFile);
                    var exists = File.Exists(configPath);
                    report.AppendLine($"📄 {configFile}: {(exists ? "EXISTS" : "MISSING")} at {configPath}");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Configuration analysis failed: {ex.Message}");
            }

            report.AppendLine();
        }

        #endregion
    }
}
