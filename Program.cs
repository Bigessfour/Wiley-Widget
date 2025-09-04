using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Context;
using Syncfusion.Licensing;

namespace WileyWidget;

/// <summary>
/// Application entry point implementing Phase 0 (Bootstrap) from the startup refactor plan.
/// Responsible ONLY for: minimal logger, early license registration attempt, correlation id seeding,
/// and guarded launch of WPF <see cref="App"/>.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
    var processStart = Stopwatch.StartNew();
    ILogger bootstrapLogger = null; // assigned after configuration
        string correlationId = Guid.NewGuid().ToString();
        bool diagStartup = false;
        bool ttfwExit = false;
        bool debugConhost = false;
        if (args != null)
        {
            foreach (var a in args)
            {
                if (string.Equals(a, "--diag-startup", StringComparison.OrdinalIgnoreCase)) diagStartup = true;
                else if (string.Equals(a, "--ttfw-exit", StringComparison.OrdinalIgnoreCase)) ttfwExit = true;
                else if (string.Equals(a, "--debug-conhost", StringComparison.OrdinalIgnoreCase)) debugConhost = true;
            }
        }

        // DEBUG: Early debugger attachment point for conhost.exe
        if (debugConhost)
        {
            Console.WriteLine("🔍 EARLY DEBUG MODE: Bootstrap phase - conhost.exe debugging enabled");
            Console.WriteLine("📋 Process Info:");
            Console.WriteLine($"   Process ID: {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"   Process Name: {Process.GetCurrentProcess().ProcessName}");
            Console.WriteLine($"   Main Module: {Process.GetCurrentProcess().MainModule?.FileName}");
            Console.WriteLine("💡 In Visual Studio: Debug → Attach to Process");
            Console.WriteLine("   Look for: conhost.exe (Console Host) or WileyWidget.exe");
            Console.WriteLine("🔴 Press ENTER to continue bootstrap or attach debugger now...");
            Console.ReadLine();
        }

        if (diagStartup) WileyWidget.Diagnostics.StartupDiagnostics.EnableVerbose();
        if (ttfwExit) WileyWidget.Diagnostics.StartupDiagnostics.EnableTtfwExit();
        WileyWidget.Diagnostics.StartupDiagnostics.RecordProcessStart();
        WileyWidget.Diagnostics.StartupDiagnostics.RecordBootstrapBegin();
        try
        {
            // Ensure logs reside in repository root (not bin/) per requirement
            var repoRoot = Directory.GetCurrentDirectory();
            var logsRoot = Path.Combine(repoRoot, "logs");
            Directory.CreateDirectory(logsRoot);
            var bootstrapPath = Path.Combine(logsRoot, "bootstrap.log");

            bootstrapLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("Application", "WileyWidget")
                .Enrich.WithProperty("StartupPhase", "Bootstrap")
                .Enrich.WithProperty("CorrelationId", correlationId)
                .WriteTo.File(bootstrapPath, restrictedToMinimumLevel: LogEventLevel.Debug, rollingInterval: RollingInterval.Infinite, shared: true)
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information, outputTemplate: "[BOOT][{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Logger = bootstrapLogger; // Assign so downstream code can use Log.* safely.

            Log.Information("StartupPhase=Bootstrap:Begin Args={Args} Diag={Diag} TtfwExit={Ttfw}", string.Join(' ', args ?? Array.Empty<string>()), diagStartup, ttfwExit);
            Log.Information("Environment Information: OS={OS} 64BitProcess={Is64} Framework={Fx}", Environment.OSVersion, Environment.Is64BitProcess, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

            // Provision correlation id into ambient context for any early code
            LogContext.PushProperty("CorrelationId", correlationId);

            TryEarlySyncfusionLicense();

            // Set debug flag for App constructor if debugging conhost
            if (debugConhost)
            {
                Environment.SetEnvironmentVariable("WILEY_WIDGET_DEBUG_CONHOST", "true");
            }

            // Launch WPF application (Phase 1 will continue in App.xaml.cs OnStartup)
            #pragma warning disable CA2000 // WPF Application lifetime == process lifetime; disposal handled by framework
            var app = new App();
            #pragma warning restore CA2000
            var exit = app.Run();

            Log.Information("StartupPhase=Bootstrap:Complete ElapsedMs={Elapsed}", processStart.ElapsedMilliseconds);
            WileyWidget.Diagnostics.StartupDiagnostics.RecordBootstrapComplete();
            return exit;
        }
        catch (Exception ex)
        {
            try
            {
                bootstrapLogger?.Fatal(ex, "Fatal exception during bootstrap startup phase");
            }
            catch { /* ignored */ }
            // As a last resort also write to Debug output
            Debug.WriteLine($"BOOTSTRAP FATAL: {ex}");
            return -1;
        }
        finally
        {
            // Flush immediately to capture earliest events
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Attempt an extremely small-footprint Syncfusion license registration without relying on configuration.
    /// Order in bootstrap: Environment variable then local file (license.key). Failing silently is acceptable;
    /// trial mode will be re-evaluated in Phase 1 with full configuration.
    /// </summary>
    private static void TryEarlySyncfusionLicense()
    {
        WileyWidget.Infrastructure.LicenseRegistrar.RegisterEarlyLicenses();
    }
}
