using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Core;  // Added for ILogEventEnricher support

namespace WileyWidget.Services;

/// <summary>
/// Service for configuring and managing application logging.
/// </summary>
public class LoggingService
{
    /// <summary>
    /// Configures the Serilog structured logging system.
    /// </summary>
    public void ConfigureSerilogLogger()
    {
        try
        {
            var logRoot = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            Directory.CreateDirectory(logRoot);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.WithProperty("Application", "WileyWidget")
                .Enrich.WithProperty("StartupPhase", "CoreStartup")
                .Enrich.FromLogContext()
                // Structured JSON sink
                .WriteTo.File(
                    path: Path.Combine(logRoot, "structured-.log"),
                    rollingInterval: RollingInterval.Day,
                    formatter: new Serilog.Formatting.Json.JsonFormatter())
                // Human-readable sink
                .WriteTo.File(
                    path: Path.Combine(logRoot, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Application} {CorrelationId} {Message:lj}{NewLine}{Exception}")
                // Error sink
                .WriteTo.File(
                    path: Path.Combine(logRoot, "errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Error)
                .CreateLogger();

            Log.Information("✅ Serilog logger configured successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to configure Serilog logger");
            throw;
        }
    }

    /// <summary>
    /// Enables Serilog SelfLog to logs/selflog-{timestamp}-{processId}.txt to avoid file locking issues.
    /// Uses process ID and timestamp to ensure unique file names across multiple instances.
    /// </summary>
    public void EnableSerilogSelfLog()
    {
        try
        {
            // Ensure logs directory exists
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            Directory.CreateDirectory(logsDir);

            // Create unique filename with timestamp and process ID to prevent conflicts
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var processId = Environment.ProcessId;
            var selfLogPath = Path.Combine(logsDir, $"selflog-{timestamp}-{processId}.txt");

            // Clean up old selflog files (keep only last 10)
            CleanUpOldSelfLogFiles(logsDir);

#pragma warning disable CA2000 // Call System.IDisposable.Dispose on object created - SelfLog manages the TextWriter lifetime
            var selfLogWriter = new StreamWriter(selfLogPath, append: false, encoding: System.Text.Encoding.UTF8)
            {
                AutoFlush = true // Ensure immediate writes to avoid buffering issues
            };
#pragma warning restore CA2000
            
            Serilog.Debugging.SelfLog.Enable(TextWriter.Synchronized(selfLogWriter));
            Log.Information("✅ Serilog SelfLog enabled to {Path} (Process: {ProcessId})", selfLogPath, processId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to enable Serilog SelfLog");
        }
    }

    /// <summary>
    /// Cleans up old selflog files to prevent accumulation, keeping only the most recent 10 files.
    /// </summary>
    private static void CleanUpOldSelfLogFiles(string logsDirectory)
    {
        try
        {
            var selfLogFiles = Directory.GetFiles(logsDirectory, "selflog-*.txt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(10) // Keep the 10 most recent files
                .ToList();

            foreach (var file in selfLogFiles)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Ignore deletion failures for individual files
                }
            }
        }
        catch
        {
            // Ignore cleanup failures - not critical
        }
    }

    /// <summary>
    /// Configures global exception handling.
    /// </summary>
    public void ConfigureGlobalExceptionHandling()
    {
        // Configure unhandled exception handling
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "💥 CRITICAL: Unhandled exception in AppDomain");
        };

        // Configure dispatcher unhandled exception handling
        System.Windows.Application.Current.DispatcherUnhandledException += (sender, e) =>
        {
            Log.Error(e.Exception, "💥 CRITICAL: Unhandled exception in Dispatcher");
            e.Handled = true; // Prevent application crash
        };

        // Configure task unhandled exception handling
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Warning(e.Exception, "⚠️ Unobserved task exception");
            e.SetObserved(); // Prevent exception escalation
        };

        Log.Information("✅ Global exception handling configured");
    }

    /// <summary>
    /// Logs comprehensive system diagnostics.
    /// </summary>
    public void LogSystemDiagnostics()
    {
        try
        {
            Log.Information("🔍 === System Diagnostic Information ===");

            // Environment information
            Log.Information("🖥️ OS: {OS} {Version}", Environment.OSVersion.Platform, Environment.OSVersion.Version);
            Log.Information("⚡ .NET Runtime: {Runtime}", Environment.Version);
            Log.Information("🧵 Processor Count: {Processors}", Environment.ProcessorCount);
            Log.Information("💾 Working Set: {Memory:F2}MB", Environment.WorkingSet / 1024.0 / 1024.0);

            // Assembly information
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                Log.Information("📦 Assembly: {Name} v{Version}",
                    entryAssembly.GetName().Name,
                    entryAssembly.GetName().Version);
            }

            // Syncfusion assemblies
            LogSyncfusionAssemblyInfo();

            Log.Information("🔍 === End System Diagnostics ===");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to log system diagnostics");
        }
    }

    /// <summary>
    /// Logs Syncfusion assembly information and license status.
    /// </summary>
    private void LogSyncfusionAssemblyInfo()
    {
        try
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.Contains("Syncfusion"))
                .ToList();

            Log.Information("🔧 Syncfusion Assemblies Loaded: {Count}", loadedAssemblies.Count);

            foreach (var assembly in loadedAssemblies.Take(5)) // Log first 5 to avoid spam
            {
                var name = assembly.GetName();
                Log.Information("   📦 {Name} v{Version}", name.Name, name.Version);
            }

            if (loadedAssemblies.Count > 5)
            {
                Log.Information("   ... and {More} more assemblies", loadedAssemblies.Count - 5);
            }

            // Log license registration status
            LogSyncfusionLicenseStatus();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to log Syncfusion assembly info");
        }
    }

    /// <summary>
    /// Logs the current Syncfusion license registration status for diagnostic purposes.
    /// </summary>
    public void LogSyncfusionLicenseStatus()
    {
        try
        {
            Log.Information("🔑 === Syncfusion License Status Check ===");

            // Check license via SecureLicenseProvider if available
            try
            {
                var licenseStatus = WileyWidget.Licensing.SecureLicenseProvider.GetLicenseStatus();
                Log.Information("📋 License Sources:\n{Status}", licenseStatus);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not get detailed license status from SecureLicenseProvider");
            }

            // Attempt to validate license registration by checking for known Syncfusion behavior
            ValidateSyncfusionLicenseRegistration();

            Log.Information("🔑 === End License Status Check ===");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to log Syncfusion license status");
        }
    }

    /// <summary>
    /// Validates Syncfusion license registration by checking runtime behavior.
    /// </summary>
    private void ValidateSyncfusionLicenseRegistration()
    {
        try
        {
            // Check if license is properly registered by examining Syncfusion's internal state
            // This is a diagnostic approach since Syncfusion doesn't provide a direct API
            
            var licenseAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName?.Contains("Syncfusion.Licensing") == true);
                
            if (licenseAssembly != null)
            {
                Log.Information("✅ Syncfusion.Licensing assembly found: v{Version}", 
                    licenseAssembly.GetName().Version);
                    
                // Check for environment variables as indicator
                var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var hasEnvKey = !string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE";
                
                // Check for license file
                var licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
                var hasLicenseFile = File.Exists(licenseFile);
                
                Log.Information("🔍 License Source Analysis:");
                Log.Information("   Environment Variable: {HasEnv}", hasEnvKey ? "✅ Set" : "❌ Not Set");
                Log.Information("   License File: {HasFile}", hasLicenseFile ? "✅ Found" : "❌ Not Found");
                
                if (!hasEnvKey && !hasLicenseFile)
                {
                    Log.Warning("⚠️ No license sources detected - application likely running in trial mode");
                    Log.Warning("💡 Expected behavior: Syncfusion controls may show trial watermarks");
                }
                else
                {
                    Log.Information("✅ License source(s) available - registration should have succeeded");
                }
            }
            else
            {
                Log.Warning("⚠️ Syncfusion.Licensing assembly not found - license registration may not be working");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to validate Syncfusion license registration");
        }
    }

    /// <summary>
    /// Performs a comprehensive license bootstrap analysis to help diagnose registration issues.
    /// Call this method after license registration attempts to validate success.
    /// </summary>
    public void DiagnoseLicenseBootstrap()
    {
        try
        {
            Log.Information("🔬 === LICENSE BOOTSTRAP DIAGNOSTIC ===");
            
            // 1. Check all potential license sources
            Log.Information("📋 Scanning License Sources:");
            
            var envKeyUser = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User);
            var envKeyMachine = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
            var envKeyProcess = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            
            Log.Information("   User Environment: {Status}", 
                !string.IsNullOrWhiteSpace(envKeyUser) ? "✅ SET" : "❌ NOT SET");
            Log.Information("   Machine Environment: {Status}", 
                !string.IsNullOrWhiteSpace(envKeyMachine) ? "✅ SET" : "❌ NOT SET");
            Log.Information("   Process Environment: {Status}", 
                !string.IsNullOrWhiteSpace(envKeyProcess) ? "✅ SET" : "❌ NOT SET");
            
            // 2. Check license file
            var licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
            if (File.Exists(licenseFile))
            {
                var content = File.ReadAllText(licenseFile).Trim();
                var isValid = !string.IsNullOrWhiteSpace(content) && 
                             content.Length > 50 && 
                             !content.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE");
                Log.Information("   License File: ✅ EXISTS ({Length} chars, Valid: {IsValid})", 
                    content.Length, isValid);
            }
            else
            {
                Log.Information("   License File: ❌ NOT FOUND");
            }
            
            // 3. Check if Syncfusion assemblies are loaded
            var syncfusionAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName?.Contains("Syncfusion") == true)
                .ToList();
            
            Log.Information("📦 Syncfusion Runtime Status:");
            Log.Information("   Loaded Assemblies: {Count}", syncfusionAssemblies.Count);
            
            var licenseAssembly = syncfusionAssemblies.FirstOrDefault(a => 
                a.FullName?.Contains("Syncfusion.Licensing") == true);
            
            if (licenseAssembly != null)
            {
                Log.Information("   Licensing Assembly: ✅ LOADED v{Version}", 
                    licenseAssembly.GetName().Version);
            }
            else
            {
                Log.Warning("   Licensing Assembly: ❌ NOT LOADED");
            }
            
            // 4. Provide diagnostic recommendations
            Log.Information("💡 Diagnostic Recommendations:");
            
            var hasAnyLicense = !string.IsNullOrWhiteSpace(envKeyUser) || 
                               !string.IsNullOrWhiteSpace(envKeyMachine) || 
                               !string.IsNullOrWhiteSpace(envKeyProcess) ||
                               (File.Exists(licenseFile) && File.ReadAllText(licenseFile).Trim().Length > 50);
            
            if (!hasAnyLicense)
            {
                Log.Warning("   🚨 NO LICENSE DETECTED - App will run in trial mode with watermarks");
                Log.Information("   📝 To fix: Set SYNCFUSION_LICENSE_KEY environment variable or add license.key file");
            }
            else if (licenseAssembly == null)
            {
                Log.Warning("   🚨 LICENSE FOUND but Syncfusion.Licensing not loaded - Check assembly references");
            }
            else
            {
                Log.Information("   ✅ LICENSE DETECTED and Licensing assembly loaded - Should be working");
                Log.Information("   📝 If watermarks still appear, check for license key validity or registration timing");
            }
            
            Log.Information("🔬 === END BOOTSTRAP DIAGNOSTIC ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to run license bootstrap diagnostic");
        }
    }
}
