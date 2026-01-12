using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace WileyWidget.Services.HealthChecks;

/// <summary>
/// Health check to validate Syncfusion license status and version compatibility
/// </summary>
public class SyncfusionLicenseHealthCheck : IHealthCheck
{
    private readonly ILogger<SyncfusionLicenseHealthCheck> _logger;
    private static string? _cachedVersion;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static HealthCheckResult? _cachedResult;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly object _lock = new();

    public SyncfusionLicenseHealthCheck(ILogger<SyncfusionLicenseHealthCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                // Use cached result if recent
                if (_cachedResult != null && DateTime.UtcNow - _lastCheck < CacheDuration)
                {
                    return Task.FromResult(_cachedResult.Value);
                }
            }

            var data = new Dictionary<string, object>();

            // Check environment variable for license key
            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
                           ?? Environment.GetEnvironmentVariable("Syncfusion__LicenseKey");

            var hasLicense = !string.IsNullOrWhiteSpace(licenseKey);
            data["LicenseKeyPresent"] = hasLicense;

            if (hasLicense)
            {
                data["LicenseKeyLength"] = licenseKey!.Length;
                data["LicenseKeyHash"] = ComputeShortHash(licenseKey);
            }

            // Detect Syncfusion version from loaded assemblies
            var syncfusionVersion = GetSyncfusionVersion();
            data["SyncfusionVersion"] = syncfusionVersion ?? "Unknown";
            _cachedVersion = syncfusionVersion;

            // Determine health status
            HealthStatus status;
            string description;

            if (!hasLicense)
            {
                status = HealthStatus.Degraded;
                description = "Syncfusion license key not found in environment. UI may show evaluation watermarks.";
                _logger.LogWarning("Health check: {Description}", description);
            }
            else if (licenseKey!.Length < 80)
            {
                status = HealthStatus.Degraded;
                description = "Syncfusion license key appears invalid (too short). Expected 80+ characters.";
                _logger.LogWarning("Health check: {Description} Length: {Length}", description, licenseKey.Length);
            }
            else if (string.IsNullOrEmpty(syncfusionVersion))
            {
                status = HealthStatus.Degraded;
                description = "Syncfusion assemblies loaded but version could not be detected.";
                _logger.LogWarning("Health check: {Description}", description);
            }
            else
            {
                status = HealthStatus.Healthy;
                description = $"Syncfusion license key present and appears valid. Version: {syncfusionVersion}";
                _logger.LogDebug("Health check: {Description}", description);
            }

            data["CheckedAt"] = DateTime.UtcNow;

            var result = new HealthCheckResult(status, description, data: data);

            // Cache result
            lock (_lock)
            {
                _cachedResult = result;
                _lastCheck = DateTime.UtcNow;
                _cachedVersion = syncfusionVersion;
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Syncfusion license health check");
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Exception during license health check",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["ExceptionType"] = ex.GetType().Name,
                        ["ExceptionMessage"] = ex.Message
                    }));
        }
    }

    /// <summary>
    /// Attempts to detect Syncfusion version from loaded assemblies
    /// </summary>
    private static string? GetSyncfusionVersion()
    {
        try
        {
            // Try to find Syncfusion.Licensing assembly
            var licensingAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Syncfusion.Licensing");

            if (licensingAssembly != null)
            {
                var version = licensingAssembly.GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : null;
            }

            // Fallback: try any Syncfusion assembly
            var syncfusionAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.StartsWith("Syncfusion.", StringComparison.OrdinalIgnoreCase) == true);

            if (syncfusionAssembly != null)
            {
                var version = syncfusionAssembly.GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : null;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Computes a short hash of the license key for logging (not the full key)
    /// </summary>
    private static string ComputeShortHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = sha.ComputeHash(bytes);
        var hex = BitConverter.ToString(hash).Replace("-", "", StringComparison.Ordinal);
        return hex[..Math.Min(8, hex.Length)].ToLowerInvariant();
    }

    /// <summary>
    /// Gets the cached Syncfusion version if available
    /// </summary>
    public static string? GetCachedVersion() => _cachedVersion;
}
