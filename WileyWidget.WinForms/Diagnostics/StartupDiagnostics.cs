using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Data;

namespace WileyWidget.WinForms.Diagnostics;

/// <summary>
/// Diagnostic service that verifies all critical services can be resolved at startup.
/// Use this to catch DI configuration errors before they cause runtime failures.
/// </summary>
public interface IStartupDiagnostics
{
    /// <summary>
    /// Run all diagnostic checks and return results
    /// </summary>
    Task<StartupDiagnosticsReport> RunDiagnosticsAsync();

    /// <summary>
    /// Get diagnostics summary as human-readable string
    /// </summary>
    string GetSummary();
}

/// <summary>
/// Result of a single diagnostic check
/// </summary>
public class DiagnosticCheckResult
{
    public string ServiceName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public TimeSpan Duration { get; set; }

    public override string ToString()
    {
        var icon = IsSuccess ? "✓" : "✗";
        return $"{icon} {ServiceName}: {Message} ({Duration.TotalMilliseconds:F1}ms)";
    }
}

/// <summary>
/// Complete diagnostics report from startup verification
/// </summary>
public class StartupDiagnosticsReport
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<DiagnosticCheckResult> Results { get; set; } = new();
    public bool AllChecksPassed => Results.All(r => r.IsSuccess);
    public int SuccessCount => Results.Count(r => r.IsSuccess);
    public int FailureCount => Results.Count(r => !r.IsSuccess);
    public TimeSpan TotalDuration => TimeSpan.FromMilliseconds(Results.Sum(r => r.Duration.TotalMilliseconds));

    public override string ToString()
    {
        var lines = new List<string>
        {
            "\n╔════════════════════════════════════════════════════════════╗",
            "║         WILEY WIDGET STARTUP DIAGNOSTICS REPORT            ║",
            "╚════════════════════════════════════════════════════════════╝",
            $"\nTimestamp: {Timestamp:yyyy-MM-dd HH:mm:ss UTC}",
            $"Status: {(AllChecksPassed ? "✓ ALL PASSED" : "✗ FAILURES DETECTED")}",
            $"Summary: {SuccessCount} passed, {FailureCount} failed",
            $"Total Time: {TotalDuration.TotalMilliseconds:F1}ms\n"
        };

        if (Results.Any())
        {
            lines.Add("─ Service Resolution Checks:");
            foreach (var result in Results)
            {
                lines.Add($"  {result}");
            }
        }

        lines.Add("\n" + new string('═', 62));
        return string.Join("\n", lines);
    }
}

/// <summary>
/// Implementation of startup diagnostics
/// </summary>
public class StartupDiagnostics : IStartupDiagnostics
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupDiagnostics> _logger;
    private StartupDiagnosticsReport? _lastReport;

    public StartupDiagnostics(IServiceProvider serviceProvider, ILogger<StartupDiagnostics> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StartupDiagnosticsReport> RunDiagnosticsAsync()
    {
        var report = new StartupDiagnosticsReport();

        _logger.LogInformation("Starting startup diagnostics...");

        // Define critical services to check
        var servicesToCheck = new Dictionary<string, Type>
        {
            // Infrastructure services
            { "ILoggerFactory", typeof(ILoggerFactory) },
            { "IHttpClientFactory", typeof(IHttpClientFactory) },
            { "IMemoryCache", typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache) },
            { "ICacheService", typeof(WileyWidget.Abstractions.ICacheService) },
            // NOTE: AppDbContext is NOT registered as a service (only DbContextFactory is registered)
            // Do NOT attempt to resolve AppDbContext directly; use the factory pattern instead
            { "IDbContextFactory<AppDbContext>", typeof(Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>) },

            // Core services
            { "ISettingsService", typeof(ISettingsService) },
            { "ISecretVaultService", typeof(ISecretVaultService) },
            { "HealthCheckService", typeof(HealthCheckService) },
            { "IWileyWidgetContextService", typeof(WileyWidget.Services.IWileyWidgetContextService) },

            // Data services
            { "IQuickBooksApiClient", typeof(WileyWidget.Services.IQuickBooksApiClient) },
            { "IQuickBooksService", typeof(WileyWidget.Services.IQuickBooksService) },

            // Feature services
            { "IAIService", typeof(IAIService) },
            { "IAILoggingService", typeof(IAILoggingService) },
            { "IAuditService", typeof(IAuditService) },
            { "IReportExportService", typeof(WileyWidget.Services.IReportExportService) },
            { "IExcelReaderService", typeof(WileyWidget.Services.Excel.IExcelReaderService) },
            { "IExcelExportService", typeof(WileyWidget.Services.Export.IExcelExportService) },
            { "IDiValidationService", typeof(WileyWidget.Services.Abstractions.IDiValidationService) },
        };

        // Check each service
        foreach (var kvp in servicesToCheck)
        {
            var result = await CheckServiceResolutionAsync(kvp.Key, kvp.Value);
            report.Results.Add(result);
        }

        _lastReport = report;

        // Log summary
        _logger.LogInformation("Diagnostics complete: {SuccessCount} passed, {FailureCount} failed",
            report.SuccessCount, report.FailureCount);

        if (!report.AllChecksPassed)
        {
            _logger.LogError("⚠ Startup diagnostics detected issues. See details below:");
            foreach (var failure in report.Results.Where(r => !r.IsSuccess))
            {
                _logger.LogError("  - {Service}: {Message}", failure.ServiceName, failure.Message);
            }
        }

        return report;
    }

    public string GetSummary()
    {
        return _lastReport?.ToString() ?? "No diagnostics report available. Run RunDiagnosticsAsync() first.";
    }

    private async Task<DiagnosticCheckResult> CheckServiceResolutionAsync(string serviceName, Type serviceType)
    {
        var startTime = DateTime.UtcNow;
        var result = new DiagnosticCheckResult { ServiceName = serviceName };

        try
        {
            object? service;

            // Check if this is a scoped service by attempting resolution with a scope
            // Scoped services cannot be resolved directly from the root provider
            try
            {
                // Try resolving from root provider first (for singleton/transient services)
                service = _serviceProvider.GetService(serviceType);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("scoped service", StringComparison.Ordinal))
            {
                // This is a scoped service — create a scope and retry
                using (var scope = _serviceProvider.CreateScope())
                {
                    service = scope.ServiceProvider.GetService(serviceType);
                }
            }

            if (service == null)
            {
                result.IsSuccess = false;
                result.Message = "Service resolved to null (not registered or resolve returned null)";
            }
            else
            {
                result.IsSuccess = true;
                result.Message = $"Successfully resolved {serviceType.Name}";
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Message = ex.Message;
            result.Exception = ex;
            _logger.LogError(ex, "Failed to resolve {ServiceName}", serviceName);
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }

        await Task.Delay(0); // Simulate async work for future expansion
        return result;
    }
}
