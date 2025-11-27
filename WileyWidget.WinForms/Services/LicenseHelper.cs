using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using System;

namespace WileyWidget.WinForms.Services
{
    internal static class LicenseHelper
    {
        /// <summary>
        /// Find a Syncfusion license key from configuration or environment variables.
        /// Order: IConfiguration["Syncfusion:LicenseKey"] -> SYNCFUSION_LICENSE_KEY env var
        /// Returns null when not found.
        /// </summary>
        public static string? GetSyncfusionLicenseKey(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var licenseKey = configuration["Syncfusion:LicenseKey"];
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
                return null;

            return licenseKey;
        }

        /// <summary>
        /// Try to register Syncfusion license if a key is present. Returns true when a license was registered.
        /// If no key is present, method logs a warning and returns false.
        /// </summary>
        public static bool TryRegisterSyncfusionLicense(IConfiguration configuration, ILogger? logger = null)
        {
            var key = GetSyncfusionLicenseKey(configuration);
            if (string.IsNullOrWhiteSpace(key))
            {
                logger?.LogWarning("Syncfusion LicenseKey missing. Continuing without license (trial mode). Set Syncfusion:LicenseKey or SYNCFUSION_LICENSE_KEY to register.");
                return false;
            }

            try
            {
                SyncfusionLicenseProvider.RegisterLicense(key);
                logger?.LogInformation("Syncfusion License registered from configuration/environment variable.");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to register Syncfusion license");
                // Swallow and continue - registration is best-effort
                return false;
            }
        }
    }
}
