using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;

namespace WileyWidget.Diagnostics;

/// <summary>
/// Monitors and reports application startup performance metrics.
/// Based on Microsoft WPF startup time best practices:
/// https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/application-startup-time
/// </summary>
public sealed class StartupPerformanceMonitor : IDisposable
{
    private readonly Stopwatch _totalTimer;
    private readonly Dictionary<string, Stopwatch> _phaseTimers;
    private readonly Dictionary<string, TimeSpan> _completedPhases;
    private readonly ILogger _logger;

    /// <summary>
    /// Total elapsed time since startup began
    /// </summary>
    public TimeSpan TotalElapsed => _totalTimer.Elapsed;

    /// <summary>
    /// All completed startup phases with their durations
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> CompletedPhases => _completedPhases;

    public StartupPerformanceMonitor()
    {
        _logger = Log.ForContext<StartupPerformanceMonitor>();
        _totalTimer = Stopwatch.StartNew();
        _phaseTimers = new Dictionary<string, Stopwatch>();
        _completedPhases = new Dictionary<string, TimeSpan>();

        _logger.Information("Startup performance monitoring initialized");
    }

    /// <summary>
    /// Begins timing a startup phase
    /// </summary>
    public void BeginPhase(string phaseName)
    {
        if (_phaseTimers.ContainsKey(phaseName))
        {
            _logger.Warning("Phase {PhaseName} already started, restarting timer", phaseName);
            _phaseTimers[phaseName].Restart();
        }
        else
        {
            _phaseTimers[phaseName] = Stopwatch.StartNew();
            _logger.Debug("Started phase: {PhaseName}", phaseName);
        }
    }

    /// <summary>
    /// Ends timing a startup phase and records the duration
    /// </summary>
    public TimeSpan EndPhase(string phaseName)
    {
        if (!_phaseTimers.TryGetValue(phaseName, out var timer))
        {
            _logger.Warning("Attempted to end phase {PhaseName} that was never started", phaseName);
            return TimeSpan.Zero;
        }

        timer.Stop();
        var duration = timer.Elapsed;
        _completedPhases[phaseName] = duration;

        _logger.Information("Completed phase: {PhaseName} in {Duration:N2}ms",
            phaseName, duration.TotalMilliseconds);

        return duration;
    }

    /// <summary>
    /// Measures execution time of a synchronous action
    /// </summary>
    public TimeSpan Measure(string phaseName, Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        BeginPhase(phaseName);
        try
        {
            action();
            return EndPhase(phaseName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during phase: {PhaseName}", phaseName);
            EndPhase(phaseName);
            throw;
        }
    }

    /// <summary>
    /// Measures execution time of an asynchronous function
    /// </summary>
    public async Task<TimeSpan> MeasureAsync(string phaseName, Func<Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        BeginPhase(phaseName);
        try
        {
            await action();
            return EndPhase(phaseName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during async phase: {PhaseName}", phaseName);
            EndPhase(phaseName);
            throw;
        }
    }

    /// <summary>
    /// Generates a summary report of all startup phases
    /// </summary>
    public StartupPerformanceReport GenerateReport()
    {
        _totalTimer.Stop();

        var report = new StartupPerformanceReport
        {
            TotalStartupTime = _totalTimer.Elapsed,
            Phases = _completedPhases.Select(kvp => new StartupPhase
            {
                Name = kvp.Key,
                Duration = kvp.Value,
                PercentageOfTotal = (kvp.Value.TotalMilliseconds / _totalTimer.Elapsed.TotalMilliseconds) * 100
            }).OrderByDescending(p => p.Duration).ToList(),
            Timestamp = DateTime.UtcNow
        };

        _logger.Information("Startup Performance Report:\n{Report}", report.ToString());

        return report;
    }

    public void Dispose()
    {
        foreach (var timer in _phaseTimers.Values)
        {
            timer.Stop();
        }
        _totalTimer.Stop();
    }
}

/// <summary>
/// Report containing startup performance metrics
/// </summary>
public sealed class StartupPerformanceReport
{
    public TimeSpan TotalStartupTime { get; set; }
    public List<StartupPhase> Phases { get; set; } = new();
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Identifies the slowest startup phase
    /// </summary>
    public StartupPhase? SlowestPhase => Phases.OrderByDescending(p => p.Duration).FirstOrDefault();

    /// <summary>
    /// Calculates percentage of time spent in I/O-related phases
    /// </summary>
    public double IoPercentage => Phases
        .Where(p => p.Name.Contains("Database", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Config", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("KeyVault", StringComparison.OrdinalIgnoreCase))
        .Sum(p => p.PercentageOfTotal);

    public override string ToString()
    {
        var lines = new List<string>
        {
            "═══════════════════════════════════════════════════════",
            "           STARTUP PERFORMANCE REPORT",
            "═══════════════════════════════════════════════════════",
            $"Total Startup Time: {TotalStartupTime.TotalMilliseconds:N2}ms",
            $"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC",
            "",
            "Phases (sorted by duration):",
            "─────────────────────────────────────────────────────"
        };

        foreach (var phase in Phases)
        {
            lines.Add($"  {phase.Name,-40} {phase.Duration.TotalMilliseconds,8:N2}ms ({phase.PercentageOfTotal,5:N1}%)");
        }

        lines.Add("─────────────────────────────────────────────────────");
        lines.Add($"Slowest Phase: {SlowestPhase?.Name ?? "N/A"}");
        lines.Add($"I/O Operations: {IoPercentage:N1}% of total time");
        lines.Add("═══════════════════════════════════════════════════════");

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Represents a single startup phase with timing information
/// </summary>
public sealed class StartupPhase
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public double PercentageOfTotal { get; set; }
}
