using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Data;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;
using System.Net.Http;

namespace WileyWidget.WinForms.Diagnostics
{
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
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private StartupDiagnosticsReport? _lastReport;

    public StartupDiagnostics(IServiceProvider serviceProvider, ILogger<StartupDiagnostics> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

            // WinForms ViewModels (scoped) - resolve inside a created scope
            { "MainViewModel", typeof(MainViewModel) },
            { "ChartViewModel", typeof(ChartViewModel) },
            { "SettingsViewModel", typeof(SettingsViewModel) },
            { "AccountsViewModel", typeof(AccountsViewModel) },
            { "BudgetOverviewViewModel", typeof(BudgetOverviewViewModel) },
        };

        // Check each service
        foreach (var kvp in servicesToCheck)
        {
            var result = await CheckServiceResolutionAsync(kvp.Key, kvp.Value);
            report.Results.Add(result);
        }

        // === Configuration Checks ===
        // Check critical configuration values that could cause runtime exceptions
        report.Results.Add(CheckConfigurationValue("XAI:ApiKey", "XAI API Key", isRequired: false));
        report.Results.Add(CheckConfigurationValue("ConnectionStrings:DefaultConnection", "Database Connection String", isRequired: true));
        report.Results.Add(CheckSecretVaultConfiguration());

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
            object? service = null;

            // ALWAYS resolve within a scope to avoid "Cannot resolve scoped service from root provider"
            // exceptions that occur when ValidateScopes is enabled.
            // This works correctly for Singleton, Scoped, and Transient services.
            using (var scope = _serviceProvider.CreateScope())
            {
                service = scope.ServiceProvider.GetService(serviceType);
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

    /// <summary>
    /// Check if a configuration value is present and valid
    /// </summary>
    private DiagnosticCheckResult CheckConfigurationValue(string configKey, string displayName, bool isRequired)
    {
        var startTime = DateTime.UtcNow;
        var result = new DiagnosticCheckResult { ServiceName = $"Config: {displayName}" };

        try
        {
            var value = _configuration[configKey];

            // Check for placeholder values like "${VAR_NAME}"
            var isPlaceholder = !string.IsNullOrEmpty(value) &&
                               value.StartsWith("${", StringComparison.Ordinal) &&
                               value.EndsWith("}", StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(value) || isPlaceholder)
            {
                if (isRequired)
                {
                    result.IsSuccess = false;
                    result.Message = isPlaceholder
                        ? $"Configuration '{configKey}' contains unresolved placeholder: {value}"
                        : $"Required configuration '{configKey}' is not set";
                    _logger.LogWarning("Configuration check failed: {Message}", result.Message);
                }
                else
                {
                    result.IsSuccess = true;
                    result.Message = $"Optional configuration '{configKey}' is not set (feature may be disabled)";
                    _logger.LogDebug("Optional configuration '{ConfigKey}' not set", configKey);
                }
            }
            else
            {
                result.IsSuccess = true;
                // Don't log the actual value for security, just the length
                result.Message = $"Configuration '{configKey}' is set (length: {value.Length})";
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Message = $"Error checking configuration '{configKey}': {ex.Message}";
            result.Exception = ex;
            _logger.LogError(ex, "Failed to check configuration {ConfigKey}", configKey);
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }

        return result;
    }

    /// <summary>
    /// Check if secret vault can be accessed and contains expected secrets
    /// </summary>
    private DiagnosticCheckResult CheckSecretVaultConfiguration()
    {
        var startTime = DateTime.UtcNow;
        var result = new DiagnosticCheckResult { ServiceName = "Config: Secret Vault Access" };

        try
        {
            var secretVault = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ISecretVaultService>(_serviceProvider);
            if (secretVault == null)
            {
                result.IsSuccess = true; // Vault is optional
                result.Message = "Secret vault service not registered (secrets will use env vars/config)";
                return result;
            }

            // Try to check vault health
            var criticalSecrets = new[] { "XAI_API_KEY", "QBO_CLIENT_ID" };
            var presentSecrets = new List<string>();
            var missingSecrets = new List<string>();

            foreach (var secretName in criticalSecrets)
            {
                try
                {
                    var secret = secretVault.GetSecret(secretName);
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        presentSecrets.Add(secretName);
                    }
                    else
                    {
                        missingSecrets.Add(secretName);
                    }
                }
                catch
                {
                    missingSecrets.Add(secretName);
                }
            }

            result.IsSuccess = true; // Vault access works, missing secrets are warnings not failures
            result.Message = $"Vault accessible. Present: {presentSecrets.Count}, Missing/Optional: {missingSecrets.Count}";

            if (missingSecrets.Count > 0)
            {
                _logger.LogDebug("Secrets not in vault (may use env vars): {MissingSecrets}",
                    string.Join(", ", missingSecrets));
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Message = $"Error accessing secret vault: {ex.Message}";
            result.Exception = ex;
            _logger.LogError(ex, "Failed to check secret vault");
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }

        return result;
    }
}
}
