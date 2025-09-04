using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WileyWidget.Diagnostics;

/// <summary>
/// Lightweight startup diagnostics helper capturing key milestones and time-to-first-window (TTFW).
/// Activated with --diag-startup. Supports --ttfw-exit to terminate after first window display for SLA harness.
/// Writes to logs/startup-metrics.log without relying on Serilog.
/// </summary>
internal static class StartupDiagnostics
{
    private static readonly object _lock = new();
    private static readonly string _metricsPath;
    private static readonly Stopwatch _processStopwatch = new();
    private static bool _firstWindowRecorded;

    static StartupDiagnostics()
    {
        try
        {
            var root = Directory.GetCurrentDirectory();
            var logs = Path.Combine(root, "logs");
            Directory.CreateDirectory(logs);
            _metricsPath = Path.Combine(logs, "startup-metrics.log");
        }
        catch { }
    }

    public static bool VerboseEnabled { get; private set; }
    public static bool TtfwExitAfterFirstWindow { get; private set; }

    public static void EnableVerbose() => VerboseEnabled = true;
    public static void EnableTtfwExit() => TtfwExitAfterFirstWindow = true;

    public static void RecordProcessStart()
    {
        if (_processStopwatch.IsRunning) return;
        _processStopwatch.Start();
        WriteLine("EVENT=ProcessStart Utc={0:o}", DateTime.UtcNow);
    }

    public static void RecordBootstrapBegin() => WriteMilestone("BootstrapBegin");
    public static void RecordBootstrapComplete() => WriteMilestone("BootstrapComplete");
    public static void RecordCoreStartupBegin() => WriteMilestone("CoreStartupBegin");
    public static void RecordCoreStartupComplete() => WriteMilestone("CoreStartupComplete");

    public static void RecordFirstWindowShown()
    {
        if (_firstWindowRecorded) return;
        _firstWindowRecorded = true;
        var elapsed = _processStopwatch.ElapsedMilliseconds;
        WriteLine("EVENT=FirstWindowShown TTFW_MS={0} Utc={1:o}", elapsed, DateTime.UtcNow);
        if (TtfwExitAfterFirstWindow)
        {
            new Thread(static () => { Thread.Sleep(150); System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown(0)); })
            { IsBackground = true }.Start();
        }
    }

    private static void WriteMilestone(string name)
    {
        if (!VerboseEnabled) return;
        WriteLine("EVENT={0} ElapsedMs={1} Utc={2:o}", name, _processStopwatch.ElapsedMilliseconds, DateTime.UtcNow);
    }

    private static void WriteLine(string fmt, params object[] args)
    {
        try
        {
            var line = string.Format(fmt, args);
            lock (_lock)
            {
                if (_metricsPath != null)
                    File.AppendAllText(_metricsPath, line + Environment.NewLine);
            }
        }
        catch { }
    }
}
