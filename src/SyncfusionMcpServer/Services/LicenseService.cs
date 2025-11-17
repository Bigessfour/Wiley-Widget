using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;

namespace SyncfusionMcpServer.Services;

/// <summary>
/// Service for validating Syncfusion license
/// </summary>
public class LicenseService
{
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(ILogger<LicenseService> logger)
    {
        _logger = logger;
    }

    public async Task<LicenseValidationResult> ValidateLicenseAsync(string? licenseKey, string? expectedVersion)
    {
        var result = new LicenseValidationResult { IsValid = true };

        try
        {
            // Get license key from environment if not provided
            if (string.IsNullOrEmpty(licenseKey))
            {
                licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            }

            if (string.IsNullOrEmpty(licenseKey))
            {
                result.Warnings.Add("No license key provided or found in environment");
                result.IsRegistered = false;
                return result;
            }

            // Register license (this will validate it)
            try
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                result.IsRegistered = true;
                _logger.LogInformation("Syncfusion license registered successfully");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"License registration failed: {ex.Message}");
                result.IsRegistered = false;
                return result;
            }

            // Parse license info (basic validation)
            var licenseInfo = ParseLicenseKey(licenseKey);
            result.RegisteredVersion = licenseInfo.Version ?? expectedVersion;

            // Check version match
            if (!string.IsNullOrEmpty(expectedVersion) &&
                !string.IsNullOrEmpty(result.RegisteredVersion) &&
                result.RegisteredVersion != expectedVersion)
            {
                result.Warnings.Add(
                    $"License version mismatch: expected {expectedVersion}, got {result.RegisteredVersion}");
            }

            // Set default licensed components for WinUI
            result.LicensedComponents = new List<string>
            {
                "WinUI", "DataGrid", "Charts", "Editors", "Gauges",
                "TreeView", "Calendar", "Core"
            };

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating license");
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    private (string? Version, DateTime? ExpirationDate) ParseLicenseKey(string licenseKey)
    {
        // Syncfusion license keys have a specific format
        // This is a simplified parser - actual format is proprietary

        string? version = null;
        DateTime? expirationDate = null;

        try
        {
            // License keys often contain version info
            var parts = licenseKey.Split(';', '@');
            foreach (var part in parts)
            {
                // Look for version patterns like "27.2.5" or "v27.2"
                var versionMatch = System.Text.RegularExpressions.Regex.Match(
                    part, @"(\d+\.\d+(?:\.\d+)?)");
                if (versionMatch.Success)
                {
                    version = versionMatch.Groups[1].Value;
                    break;
                }
            }

            // Estimate expiration (typically 1 year from generation)
            // This is approximate since we can't decode the actual expiration without Syncfusion's internal logic
            if (!string.IsNullOrEmpty(version))
            {
                expirationDate = DateTime.UtcNow.AddYears(1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse license key details");
        }

        return (version, expirationDate);
    }

    public bool CheckLicenseEnvironmentVariable()
    {
        var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        return !string.IsNullOrEmpty(licenseKey);
    }
}
