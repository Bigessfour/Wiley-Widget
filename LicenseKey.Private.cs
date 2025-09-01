// PRIVATE FILE - DO NOT COMMIT TO VERSION CONTROL
// This file contains sensitive license information and should remain untracked.
// Ensure .gitignore excludes LicenseKey.Private.cs to prevent accidental commit.

using System;
using Syncfusion.Licensing;

namespace WileyWidget
{
    /// <summary>
    /// Static class containing embedded license logic for Syncfusion
    /// </summary>
    internal static class EmbeddedLicenseManager
    {
        /// <summary>
        /// Tries to register an embedded Syncfusion license with hardcoded fallback.
        /// This method provides a last-resort license key if environment/config fails.
        /// Return true if registration succeeded.
        /// </summary>
        public static bool TryRegisterEmbeddedLicense()
        {
            try
            {
                // Primary: Try to get key from environment variable (for development)
                var embeddedKey = Environment.GetEnvironmentVariable("SYNCFUSION_EMBEDDED_LICENSE_KEY");

                if (!string.IsNullOrWhiteSpace(embeddedKey) && embeddedKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    SyncfusionLicenseProvider.RegisterLicense(embeddedKey.Trim());
                    return true;
                }

                // Fallback: Hardcoded license key (replace with your actual key)
                // NOTE: This is a placeholder - replace with your real Syncfusion license key
                const string hardcodedKey = "YOUR_ACTUAL_SYNCFUSION_LICENSE_KEY_HERE";

                if (!string.IsNullOrWhiteSpace(hardcodedKey) && hardcodedKey != "YOUR_ACTUAL_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    SyncfusionLicenseProvider.RegisterLicense(hardcodedKey.Trim());
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Log to debug output since we don't have access to the logger here
                System.Diagnostics.Debug.WriteLine($"Error in TryRegisterEmbeddedLicense: {ex.Message}");
                return false;
            }
        }
    }
}
