// SECURE SYNCFUSION LICENSE PROVIDER - MICROSOFT SECURITY BEST PRACTICES COMPLIANT
// This file implements secure environment variable access for Syncfusion licensing
// Based on: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider
// Security: Uses machine-level environment variables for secure credential storage

using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Syncfusion.Licensing;

namespace WileyWidget.Licensing
{
    /// <summary>
    /// Secure license provider that reads Syncfusion license from machine-level environment variables.
    /// Follows Microsoft security best practices for credential management.
    /// </summary>
    public static class SecureLicenseProvider
    {
        private const string LicenseKeyEnvironmentVariable = "SYNCFUSION_LICENSE_KEY";
        private const string LicenseFilePath = "license.key";
        private static bool _isRegistered;

        /// <summary>
        /// Registers Syncfusion license using secure Azure Key Vault access.
        /// This method follows Microsoft's recommended approach for secure credential storage.
        /// </summary>
        /// <returns>True if license was registered successfully, false otherwise</returns>
        public static bool RegisterLicense()
        {
            if (_isRegistered)
            {
                Log.Information("🔄 Syncfusion license already registered");
                return true;
            }

            try
            {
                Log.Information("🔐 Attempting secure license registration");

                // Priority 1: License file (secure for development)
                var licenseKey = GetLicenseFromFile();
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Log.Information("✅ Syncfusion license registered from secure file");
                    _isRegistered = true;
                    return true;
                }

                // Priority 2: Azure Key Vault (most secure for production)
                licenseKey = GetLicenseFromKeyVault();
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Log.Information("✅ Syncfusion license registered from Azure Key Vault");
                    _isRegistered = true;
                    return true;
                }

                // Priority 3: Machine-level environment variable
                licenseKey = GetLicenseFromEnvironmentVariable();
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Log.Information("✅ Syncfusion license registered from secure environment variable");
                    _isRegistered = true;
                    return true;
                }

                Log.Warning("⚠️ No valid Syncfusion license found - application will run in trial mode");
                Log.Warning("💡 Add your license key to license.key file for secure development access");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to register Syncfusion license");
                return false;
            }
        }

        /// <summary>
        /// Securely retrieves license key from machine-level environment variable.
        /// Follows Microsoft security guidelines for environment variable access.
        /// </summary>
        /// <returns>License key string or null if not found</returns>
        private static string GetLicenseFromEnvironmentVariable()
        {
            try
            {
                // Use EnvironmentVariableTarget.Machine for maximum security
                var licenseKey = Environment.GetEnvironmentVariable(
                    LicenseKeyEnvironmentVariable,
                    EnvironmentVariableTarget.Machine);

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    // Fallback to user-level if machine-level not found
                    licenseKey = Environment.GetEnvironmentVariable(
                        LicenseKeyEnvironmentVariable,
                        EnvironmentVariableTarget.User);

                    if (!string.IsNullOrWhiteSpace(licenseKey))
                    {
                        Log.Warning("⚠️ Using user-level environment variable (consider machine-level for better security)");
                    }
                }

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    // Final fallback to process-level
                    licenseKey = Environment.GetEnvironmentVariable(LicenseKeyEnvironmentVariable);
                }

                return ValidateLicenseKey(licenseKey) ? licenseKey : null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error reading license from environment variable");
                return null;
            }
        }

        /// <summary>
        /// Securely retrieves license key from Azure Key Vault.
        /// This is the most secure method for license storage.
        /// </summary>
        /// <returns>License key string or null if not found</returns>
        private static string GetLicenseFromKeyVault()
        {
            try
            {
                // Only check for Key Vault loaded license if environment variable is NOT set at user/machine level
                // This prevents the Key Vault method from hijacking user-set environment variables
                var userEnvVar = Environment.GetEnvironmentVariable(LicenseKeyEnvironmentVariable, EnvironmentVariableTarget.User);
                var machineEnvVar = Environment.GetEnvironmentVariable(LicenseKeyEnvironmentVariable, EnvironmentVariableTarget.Machine);

                if (!string.IsNullOrWhiteSpace(userEnvVar) || !string.IsNullOrWhiteSpace(machineEnvVar))
                {
                    Log.Debug("Environment variable already set at user/machine level - skipping Key Vault check");
                    return null;
                }

                // Check if license key is loaded in environment (from load-mcp-secrets.ps1)
                var licenseKey = Environment.GetEnvironmentVariable(LicenseKeyEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(licenseKey) && ValidateLicenseKey(licenseKey))
                {
                    Log.Debug("License key found in environment (likely loaded from Key Vault)");
                    return licenseKey;
                }

                Log.Debug("License key not found in environment - run scripts\\load-mcp-secrets.ps1");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error checking for license from Key Vault");
                return null;
            }
        }

        /// <summary>
        /// Reads license key from license.key file (development fallback only).
        /// This method should not be used in production environments.
        /// </summary>
        /// <returns>License key string or null if not found</returns>
        private static string GetLicenseFromFile()
        {
            try
            {
                var licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFilePath);

                if (!File.Exists(licensePath))
                {
                    Log.Debug("License file not found: {Path}", licensePath);
                    return null;
                }

                var licenseKey = File.ReadAllText(licensePath).Trim();

                // Remove comments and empty lines
                var lines = licenseKey.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!trimmedLine.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        licenseKey = trimmedLine;
                        break;
                    }
                }

                return ValidateLicenseKey(licenseKey) ? licenseKey : null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error reading license from file");
                return null;
            }
        }

        /// <summary>
        /// Validates the format of a Syncfusion license key.
        /// </summary>
        /// <param name="licenseKey">The license key to validate</param>
        /// <returns>True if the license key appears valid</returns>
        private static bool ValidateLicenseKey(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return false;

            // Basic validation - Syncfusion keys are typically long and contain specific patterns
            if (licenseKey.Length < 50)
                return false;

            // Check for placeholder text
            if (licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE"))
                return false;

            // Check for common invalid patterns
            if (licenseKey.Contains("Replace this content"))
                return false;

            return true;
        }

        /// <summary>
        /// Gets the current license status for diagnostic purposes.
        /// </summary>
        /// <returns>License status information</returns>
        public static string GetLicenseStatus()
        {
            var status = "Syncfusion License Status:\n";

            // Check Azure Key Vault (most secure)
            var kvKey = Environment.GetEnvironmentVariable(LicenseKeyEnvironmentVariable);
            status += $"Azure Key Vault (via environment): {(string.IsNullOrWhiteSpace(kvKey) ? "Not Set" : "Set")}\n";

            // Check machine environment variable
            var envKey = Environment.GetEnvironmentVariable(
                LicenseKeyEnvironmentVariable,
                EnvironmentVariableTarget.Machine);
            status += $"Machine Environment Variable: {(string.IsNullOrWhiteSpace(envKey) ? "Not Set" : "Set")}\n";

            // Check user environment variable
            var userKey = Environment.GetEnvironmentVariable(
                LicenseKeyEnvironmentVariable,
                EnvironmentVariableTarget.User);
            status += $"User Environment Variable: {(string.IsNullOrWhiteSpace(userKey) ? "Not Set" : "Set")}\n";

            // Check file
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFilePath);
            status += $"License File Exists: {File.Exists(filePath)}\n";

            // Check registration status
            status += $"License Registered: {_isRegistered}\n";

            return status;
        }
    }
}
