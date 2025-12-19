using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Phase configuration defining expected startup order for Syncfusion WinForms apps.
/// </summary>
public record PhaseConfig(int ExpectedOrder, bool IsUiCritical, string? DependsOn = null)
{
    /// <summary>
    /// Canonical phase order for Wiley-Widget Syncfusion WinForms application.
    /// Based on Syncfusion best practices: License → Theme → Controls → Data.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PhaseConfig> ExpectedPhases = new Dictionary<string, PhaseConfig>
    {
        // Phase 1: License (before any Syncfusion usage)
        { "License Registration", new(1, false, null) },

        // Phase 2: Theme CRITICAL - must be early, on UI thread, before any controls
        { "Theme Initialization", new(2, true, "License Registration") },

        // Phase 3: WinForms Setup (after theme, before form creation)
        { "WinForms Initialization", new(3, true, "Theme Initialization") },

        // Phase 4: DI Container (can be parallel/async - no UI dependency)
        { "DI Container Build", new(4, false, null) },

        // Phase 5: DI Validation (depends on container)
        { "DI Validation", new(5, false, "DI Container Build") },

        // Phase 6: Database (can be async, after DI)
        { "Database Health Check", new(6, false, "DI Container Build") },

        // Phase 7: MainForm Creation (depends on theme + DI)
        { "MainForm Creation", new(7, true, "Theme Initialization") },

        // Phase 8: Chrome/Ribbon (after MainForm, on UI thread)
        { "Chrome Initialization", new(8, true, "MainForm Creation") },

        // Phase 9: MDI/Docking (after Chrome)
        { "MDI Support Initialization", new(9, true, "Chrome Initialization") },

        // Phase 10: Data Prefetch (background, non-blocking)
        { "Data Prefetch", new(10, false, "MainForm Creation") },

        // Phase 11: Splash Hide (after data ready)
        { "Splash Screen Hide", new(11, true, "Data Prefetch") },

        // Phase 12: UI Message Loop (final phase)
        { "UI Message Loop", new(12, true, "MainForm Creation") }
    };

    /// <summary>
    /// Gets phase config by name, or returns default config.
    /// </summary>
    public static PhaseConfig GetOrDefault(string phaseName) =>
        ExpectedPhases.TryGetValue(phaseName, out var config) ? config : new PhaseConfig(0, false);
}

/// <summary>
/// Monitors application startup timeline and detects out-of-sync operations.
/// Tracks phase timing, thread affinity, dependency violations, and identifies initialization order issues.
/// Optimized for Syncfusion WinForms apps with theme/DI/module initialization.
/// </summary>
public interface IStartupTimelineService
{
    /// <summary>
    /// Records the start of a startup phase.
    /// If phase exists in PhaseConfig.ExpectedPhases, auto-applies order and UI-criticality.
    /// </summary>
    /// <param name="phaseName">Name of the phase (e.g., "Theme Initialization")</param>
    /// <param name="expectedOrder">Expected sequential order (1-based). 0 = auto-detect from config.</param>
    /// <param name="isUiCritical">True if phase must run on UI thread. Null = auto-detect from config.</param>
    void RecordPhaseStart(string phaseName, int expectedOrder = 0, bool? isUiCritical = null);

    /// <summary>
    /// Records the end of a startup phase.
    /// </summary>
    void RecordPhaseEnd(string phaseName);

    /// <summary>
    /// Records a critical operation within a phase with optional duration.
    /// </summary>
    void RecordOperation(string operationName, string phaseName, double? durationMs = null);

    /// <summary>
    /// Begins a phase scope that auto-ends on disposal (RAII pattern).
    /// Recommended for ensuring phases are properly closed.
    /// </summary>
    IDisposable BeginPhaseScope(string phaseName, int expectedOrder = 0, bool? isUiCritical = null);

    /// <summary>
    /// Records a WinForms lifecycle event (Load, Shown, Activated) as a checkpoint.
    /// </summary>
    void RecordFormLifecycleEvent(string formName, string eventName);

    /// <summary>
    /// Detects and logs any out-of-order operations, dependency violations, or timing issues.
    /// Generates comprehensive report with actionable insights.
    /// Only runs if DEBUG build or WILEYWIDGET_TRACK_STARTUP_TIMELINE=true.
    /// </summary>
    StartupTimelineReport GenerateReport();

    /// <summary>
    /// Gets the current phase name.
    /// </summary>
    string? CurrentPhase { get; }

    /// <summary>
    /// True if timeline tracking is enabled (DEBUG or env var).
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Represents a recorded startup phase or operation.
/// </summary>
public class StartupEvent
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Phase"; // Phase, Operation, Checkpoint
    public int ExpectedOrder { get; set; }
    public int ActualOrder { get; set; }
    public int ChronologicalOrder { get; set; } // Based purely on start time for parallel-safe ordering
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    public int ThreadId { get; set; }
    public string? ParentPhase { get; set; }
    public string? DependsOn { get; set; } // Dependency phase name
    public bool IsAsync { get; set; }
    public bool IsUiCritical { get; set; }
    public double? MeasuredDurationMs { get; set; }
}

/// <summary>
/// Timeline analysis report with detected issues.
/// </summary>
public class StartupTimelineReport
{
    public List<StartupEvent> Events { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int UiThreadId { get; set; }

    /// <summary>
    /// Detects operations that happened out of expected order.
    /// </summary>
    public List<string> GetOrderViolations()
    {
        var violations = new List<string>();
        var phases = Events.Where(e => e.Type == "Phase" && e.ExpectedOrder > 0)
            .OrderBy(e => e.ChronologicalOrder)
            .ToList();

        var previousExpectedOrder = 0;
        foreach (var phase in phases)
        {
            if (phase.ExpectedOrder <= previousExpectedOrder)
            {
                violations.Add($"Phase '{phase.Name}' (expected order {phase.ExpectedOrder}) started before phase with expected order {previousExpectedOrder}");
            }
            previousExpectedOrder = phase.ExpectedOrder;
        }

        return violations;
    }

    /// <summary>
    /// Detects dependency violations (phase started before dependency completed).
    /// </summary>
    public List<string> GetDependencyViolations()
    {
        var violations = new List<string>();
        var completedPhases = new HashSet<string>();

        foreach (var evt in Events.Where(e => e.Type == "Phase").OrderBy(e => e.StartTime))
        {
            if (!string.IsNullOrEmpty(evt.DependsOn))
            {
                var dependency = Events.FirstOrDefault(e =>
                    e.Type == "Phase" &&
                    e.Name == evt.DependsOn &&
                    e.EndTime.HasValue &&
                    e.EndTime.Value <= evt.StartTime);

                if (dependency == null)
                {
                    violations.Add($"Phase '{evt.Name}' started without dependency '{evt.DependsOn}' completing first");
                }
            }

            if (evt.EndTime.HasValue)
            {
                completedPhases.Add(evt.Name);
            }
        }

        return violations;
    }

    /// <summary>
    /// Detects phases that ran on wrong thread.
    /// </summary>
    public List<string> GetThreadAffinityIssues()
    {
        var issues = new List<string>();

        foreach (var evt in Events)
        {
            if (evt.IsUiCritical && evt.ThreadId != UiThreadId)
            {
                issues.Add($"UI-critical phase '{evt.Name}' ran on thread {evt.ThreadId} instead of UI thread {UiThreadId}");
            }

            if (!evt.IsAsync && evt.Duration.HasValue && evt.Duration.Value.TotalMilliseconds > 300 && evt.ThreadId == UiThreadId)
            {
                issues.Add($"Blocking operation '{evt.Name}' took {evt.Duration.Value.TotalMilliseconds:F0}ms on UI thread (>300ms threshold)");
            }
        }

        return issues;
    }

    /// <summary>
    /// Gets summary statistics for the report.
    /// </summary>
    public (double LongestUiPhaseMs, string LongestUiPhaseName, double TotalUiBlockedMs, int PotentialFreezes) GetSummaryStats()
    {
        var uiPhases = Events.Where(e => e.ThreadId == UiThreadId && e.Duration.HasValue).ToList();

        var longestPhase = uiPhases.OrderByDescending(e => e.Duration!.Value.TotalMilliseconds).FirstOrDefault();
        var longestMs = longestPhase?.Duration?.TotalMilliseconds ?? 0;
        var longestName = longestPhase?.Name ?? "N/A";

        var totalUiBlocked = uiPhases.Sum(e => e.Duration!.Value.TotalMilliseconds);
        var potentialFreezes = uiPhases.Count(e => e.Duration!.Value.TotalMilliseconds > 500);

        return (longestMs, longestName, totalUiBlocked, potentialFreezes);
    }

    /// <summary>
    /// Generates human-readable timeline report with nested operations and summary stats.
    /// </summary>
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║         STARTUP TIMELINE ANALYSIS REPORT (Syncfusion)          ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Start Time:      {StartTime:HH:mm:ss.fff}                                  ║");
        if (EndTime.HasValue)
        {
            sb.AppendLine($"║ End Time:        {EndTime.Value:HH:mm:ss.fff}                                  ║");
            sb.AppendLine($"║ Total Duration:  {TotalDuration.TotalMilliseconds,6:F0}ms                                     ║");
        }
        sb.AppendLine($"║ UI Thread ID:    {UiThreadId,4}                                           ║");
        sb.AppendLine($"║ Total Events:    {Events.Count,4}                                           ║");

        // Summary stats
        var (longestMs, longestName, totalBlocked, freezes) = GetSummaryStats();
        sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
        sb.AppendLine("║ SUMMARY STATISTICS:                                            ║");
        sb.AppendLine($"║ Longest UI Phase: {TruncateString(longestName, 30),-30} {longestMs,6:F0}ms ║");
        sb.AppendLine($"║ Total UI Blocked: {totalBlocked,6:F0}ms                                     ║");
        sb.AppendLine($"║ Potential Freezes (>500ms): {freezes,2}                                   ║");

        sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
        sb.AppendLine("║ TIMELINE (chronological, 🔒=UI-critical, ⚡=async):             ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");

        var sortedEvents = Events.OrderBy(e => e.StartTime).ToList();
        var baseTime = sortedEvents.FirstOrDefault()?.StartTime ?? StartTime;

        foreach (var evt in sortedEvents)
        {
            var offset = (evt.StartTime - baseTime).TotalMilliseconds;
            var duration = evt.MeasuredDurationMs ?? evt.Duration?.TotalMilliseconds ?? 0;
            var threadMarker = evt.ThreadId == UiThreadId ? "UI" : $"T{evt.ThreadId}";
            var asyncMarker = evt.IsAsync ? "⚡" : " ";
            var criticalMarker = evt.IsUiCritical ? "🔒" : " ";

            string line;
            if (evt.Type == "Phase")
            {
                line = $"║ [{offset,6:F0}ms] {asyncMarker}{criticalMarker}[{threadMarker,3}] ▶ {TruncateString(evt.Name, 26),-26} {duration,6:F0}ms ║";
            }
            else // Operation - indent under phase
            {
                line = $"║ [{offset,6:F0}ms] {asyncMarker}{criticalMarker}[{threadMarker,3}]   → {TruncateString(evt.Name, 24),-24} {duration,6:F0}ms ║";
            }

            sb.AppendLine(line.Length > 68 ? line.Substring(0, 68) + "║" : line);
        }

        // Dependency violations
        var dependencyViolations = GetDependencyViolations();
        if (dependencyViolations.Any())
        {
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ ✗ DEPENDENCY VIOLATIONS DETECTED:                              ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            foreach (var violation in dependencyViolations)
            {
                var lines = WrapText(violation, 60);
                foreach (var line in lines)
                {
                    sb.AppendLine($"║ ✗ {line,-59} ║");
                }
            }
        }

        // Order violations
        var orderViolations = GetOrderViolations();
        if (orderViolations.Any())
        {
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ ⚠ ORDER VIOLATIONS DETECTED:                                   ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            foreach (var violation in orderViolations)
            {
                var lines = WrapText(violation, 60);
                foreach (var line in lines)
                {
                    sb.AppendLine($"║ ⚠ {line,-59} ║");
                }
            }
        }

        // Thread affinity issues
        var threadIssues = GetThreadAffinityIssues();
        if (threadIssues.Any())
        {
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ ⚠ THREAD AFFINITY ISSUES DETECTED:                             ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            foreach (var issue in threadIssues)
            {
                var lines = WrapText(issue, 60);
                foreach (var line in lines)
                {
                    sb.AppendLine($"║ ⚠ {line,-59} ║");
                }
            }
        }

        // Errors
        if (Errors.Any())
        {
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ ✗ ERRORS DETECTED:                                             ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            foreach (var error in Errors)
            {
                var lines = WrapText(error, 60);
                foreach (var line in lines)
                {
                    sb.AppendLine($"║ ✗ {line,-59} ║");
                }
            }
        }

        // Warnings
        if (Warnings.Any())
        {
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ ⚠ WARNINGS:                                                    ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            foreach (var warning in Warnings)
            {
                var lines = WrapText(warning, 60);
                foreach (var line in lines)
                {
                    sb.AppendLine($"║ ⚠ {line,-59} ║");
                }
            }
        }

        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

    private static string TruncateString(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    private static List<string> WrapText(string text, int maxLength)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 <= maxLength)
            {
                currentLine += (currentLine.Length > 0 ? " " : "") + word;
            }
            else
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.PadRight(maxLength));
                    currentLine = word;
                }
                else
                {
                    lines.Add(TruncateString(word, maxLength).PadRight(maxLength));
                }
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.PadRight(maxLength));
        }

        return lines;
    }
}

/// <summary>
/// Implementation of startup timeline monitoring service.
/// Thread-safe, designed for concurrent access during multi-threaded startup.
/// Optimized for Syncfusion WinForms with pre-defined critical phases.
/// </summary>
public class StartupTimelineService : IStartupTimelineService
{
    private readonly ConcurrentBag<StartupEvent> _events = new();
    private readonly ConcurrentDictionary<string, StartupEvent> _activePhases = new();
    private readonly ILogger<StartupTimelineService> _logger;
    private readonly Stopwatch _stopwatch;
    private readonly DateTime _startTime;
    private readonly int _uiThreadId;
    private int _eventCounter = 0;
    private int _chronologicalCounter = 0;
    private string? _currentPhase;
    private readonly bool _isEnabled;

    public string? CurrentPhase => _currentPhase;
    public bool IsEnabled => _isEnabled;

    public StartupTimelineService(ILogger<StartupTimelineService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stopwatch = Stopwatch.StartNew();
        _startTime = DateTime.Now;
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;

        // Enable in debug mode or if env var set (conditional reporting)
        _isEnabled = IsDebugBuild() || IsTimelineTrackingEnabled();

        if (_isEnabled)
        {
            _logger.LogInformation("StartupTimelineService initialized - tracking startup on UI thread {ThreadId}", _uiThreadId);
            _logger.LogInformation("Canonical phase order loaded: {PhaseCount} expected phases defined", PhaseConfig.ExpectedPhases.Count);
        }
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static bool IsTimelineTrackingEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("WILEYWIDGET_TRACK_STARTUP_TIMELINE"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public void RecordPhaseStart(string phaseName, int expectedOrder = 0, bool? isUiCritical = null)
    {
        if (!_isEnabled) return;

        // Auto-detect from pre-defined phases if not explicitly specified
        var config = PhaseConfig.GetOrDefault(phaseName);
        if (expectedOrder == 0) expectedOrder = config.ExpectedOrder;
        if (!isUiCritical.HasValue) isUiCritical = config.IsUiCritical;

        var evt = new StartupEvent
        {
            Name = phaseName,
            Type = "Phase",
            ExpectedOrder = expectedOrder,
            ActualOrder = Interlocked.Increment(ref _eventCounter),
            ChronologicalOrder = Interlocked.Increment(ref _chronologicalCounter),
            StartTime = DateTime.Now,
            ThreadId = Thread.CurrentThread.ManagedThreadId,
            IsAsync = Thread.CurrentThread.ManagedThreadId != _uiThreadId,
            IsUiCritical = isUiCritical ?? false,
            DependsOn = config.DependsOn
        };

        _events.Add(evt);
        _activePhases[phaseName] = evt;
        _currentPhase = phaseName;

        var threadMarker = evt.ThreadId == _uiThreadId ? "UI" : $"T{evt.ThreadId}";
        var criticalMarker = evt.IsUiCritical ? " [UI-CRITICAL]" : "";
        _logger.LogInformation("[TIMELINE] Phase START [{ThreadMarker}]{CriticalMarker}: {PhaseName} (expected order: {Expected}, chronological: {Chrono})",
            threadMarker, criticalMarker, phaseName, expectedOrder, evt.ChronologicalOrder);

        // Immediate validation: UI-critical on wrong thread
        if (evt.IsUiCritical && evt.ThreadId != _uiThreadId)
        {
            _logger.LogError("[TIMELINE] ✗ CRITICAL: UI-critical phase '{PhaseName}' started on non-UI thread {ThreadId}!",
                phaseName, evt.ThreadId);
        }

        // Check dependency immediately
        if (!string.IsNullOrEmpty(config.DependsOn))
        {
            var dependency = _events.FirstOrDefault(e =>
                e.Name == config.DependsOn &&
                e.EndTime.HasValue &&
                e.EndTime.Value <= evt.StartTime);

            if (dependency == null)
            {
                _logger.LogWarning("[TIMELINE] ⚠ DEPENDENCY WARNING: Phase '{PhaseName}' started but dependency '{Dependency}' not completed",
                    phaseName, config.DependsOn);
            }
        }
    }

    public void RecordPhaseEnd(string phaseName)
    {
        if (!_isEnabled) return;

        if (_activePhases.TryRemove(phaseName, out var evt))
        {
            evt.EndTime = DateTime.Now;

            var threadMarker = evt.ThreadId == _uiThreadId ? "UI" : $"T{evt.ThreadId}";
            _logger.LogInformation("[TIMELINE] Phase END [{ThreadMarker}]: {PhaseName} (duration: {Duration}ms)",
                threadMarker, phaseName, evt.Duration?.TotalMilliseconds ?? 0);

            // Warn about long-running phases on UI thread (>300ms = potential freeze)
            if (evt.ThreadId == _uiThreadId && evt.Duration?.TotalMilliseconds > 300)
            {
                _logger.LogWarning("[TIMELINE] ⚠ BLOCKING PHASE: '{PhaseName}' took {Duration}ms on UI thread (>300ms threshold)",
                    phaseName, evt.Duration?.TotalMilliseconds);
            }

            // Special warning for Syncfusion theme if too late (must be order ≤4)
            if (phaseName.Contains("Theme", StringComparison.OrdinalIgnoreCase) && evt.ChronologicalOrder > 4)
            {
                _logger.LogWarning("[TIMELINE] ⚠ SYNCFUSION WARNING: Theme initialization happened at chronological order {Order} - should be early (order ≤4) before any control creation",
                    evt.ChronologicalOrder);
            }
        }
        else
        {
            _logger.LogWarning("[TIMELINE] ⚠ Phase END called for '{PhaseName}' but no matching START found", phaseName);
        }

        _currentPhase = null;
    }

    public void RecordOperation(string operationName, string phaseName, double? durationMs = null)
    {
        if (!_isEnabled) return;

        var evt = new StartupEvent
        {
            Name = operationName,
            Type = "Operation",
            ExpectedOrder = 0,
            ActualOrder = Interlocked.Increment(ref _eventCounter),
            ChronologicalOrder = Interlocked.Increment(ref _chronologicalCounter),
            StartTime = DateTime.Now,
            EndTime = DateTime.Now,
            ThreadId = Thread.CurrentThread.ManagedThreadId,
            ParentPhase = phaseName,
            IsAsync = Thread.CurrentThread.ManagedThreadId != _uiThreadId,
            MeasuredDurationMs = durationMs
        };

        _events.Add(evt);

        var threadMarker = evt.ThreadId == _uiThreadId ? "UI" : $"T{evt.ThreadId}";
        var durationText = durationMs.HasValue ? $" ({durationMs.Value:F0}ms)" : "";
        _logger.LogDebug("[TIMELINE] Operation [{ThreadMarker}]: {OperationName} in '{PhaseName}'{Duration}",
            threadMarker, operationName, phaseName, durationText);
    }

    /// <summary>
    /// Helper method to record WinForms lifecycle events (Load, Shown, Activated).
    /// </summary>
    public void RecordFormLifecycleEvent(string formName, string eventName)
    {
        if (!_isEnabled) return;

        var checkpointName = $"{formName}.{eventName}";
        RecordOperation(checkpointName, _currentPhase ?? "UI Message Loop", null);

        _logger.LogDebug("[TIMELINE] WinForms Lifecycle: {Checkpoint}", checkpointName);
    }

    /// <summary>
    /// Disposable scope for RAII pattern - ensures RecordPhaseEnd is always called.
    /// </summary>
    private class PhaseScope : IDisposable
    {
        private readonly StartupTimelineService _service;
        private readonly string _phaseName;
        private bool _disposed;

        public PhaseScope(StartupTimelineService service, string phaseName)
        {
            _service = service;
            _phaseName = phaseName;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _service.RecordPhaseEnd(_phaseName);
                _disposed = true;
            }
        }
    }

    public IDisposable BeginPhaseScope(string phaseName, int expectedOrder = 0, bool? isUiCritical = null)
    {
        RecordPhaseStart(phaseName, expectedOrder, isUiCritical);
        return new PhaseScope(this, phaseName);
    }

    public StartupTimelineReport GenerateReport()
    {
        if (!_isEnabled)
        {
            return new StartupTimelineReport
            {
                StartTime = _startTime,
                EndTime = DateTime.Now,
                UiThreadId = _uiThreadId
            };
        }

        _stopwatch.Stop();

        var report = new StartupTimelineReport
        {
            Events = _events.ToList(),
            StartTime = _startTime,
            EndTime = DateTime.Now,
            TotalDuration = _stopwatch.Elapsed,
            UiThreadId = _uiThreadId
        };

        // Detect all types of violations
        var orderViolations = report.GetOrderViolations();
        var dependencyViolations = report.GetDependencyViolations();
        var threadIssues = report.GetThreadAffinityIssues();

        report.Warnings.AddRange(orderViolations);
        report.Errors.AddRange(dependencyViolations); // Dependencies are errors, not warnings
        report.Warnings.AddRange(threadIssues);

        // Check for incomplete phases
        foreach (var activePhase in _activePhases.Values)
        {
            report.Errors.Add($"Phase '{activePhase.Name}' was started but never completed");
        }

        // Log the formatted report
        var formattedReport = report.ToFormattedString();
        Log.Information(formattedReport);

        // Also log individual issues with structured logging for querying
        foreach (var error in report.Errors)
        {
            _logger.LogError("[TIMELINE] ✗ {Error}", error);
        }

        foreach (var warning in report.Warnings)
        {
            _logger.LogWarning("[TIMELINE] ⚠ {Warning}", warning);
        }

        if (report.Errors.Count == 0 && report.Warnings.Count == 0)
        {
            _logger.LogInformation("[TIMELINE] ✓ No timing or ordering issues detected - startup sequence optimal");
        }
        else
        {
            var summary = $"Startup analysis: {report.Errors.Count} errors, {report.Warnings.Count} warnings";
            _logger.LogWarning("[TIMELINE] {Summary}", summary);
        }

        return report;
    }
}
