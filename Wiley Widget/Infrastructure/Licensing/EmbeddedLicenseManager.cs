// PRIVATE FILE - DO NOT COMMIT TO VERSION CONTROL
// This file contains sensitive license information and should remain untracked.
// Ensure .gitignore excludes LicenseKey.Private.cs to prevent accidental commit.

using System;
using System.IO;
using System.Reflection;
using Syncfusion.Licensing;
using Serilog;

namespace WileyWidget.Infrastructure.Licensing
{
    /// <summary>
    /// Static class containing embedded license logic for Syncfusion
    /// </summary>
    public static class EmbeddedLicenseManager
    {
        /// <summary>
        /// Tries to register an embedded Syncfusion license from embedded resource, environment variable, or hardcoded fallback.
        /// This method provides multiple fallback mechanisms for license registration.
        /// Return true if registration succeeded.
        /// </summary>
        public static bool TryRegisterEmbeddedLicense()
        {
            try
            {
                // First priority: Try to load from embedded resource
                var key = LoadLicenseFromEmbeddedResource();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    SyncfusionLicenseProvider.RegisterLicense(key.Trim());
                    Log.Information("✅ Syncfusion license registered from embedded resource.");
                    return true;
                }

                // Second priority: Try to get key from environment variable (for development)
                key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY_EMBEDDED") ??
                      Environment.GetEnvironmentVariable("SYNCFUSION_EMBEDDED_LICENSE_KEY") ??
                      Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

                if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    SyncfusionLicenseProvider.RegisterLicense(key.Trim());
                    Log.Information("✅ Syncfusion license registered from embedded environment variable.");
                    return true;
                }

                // Third priority: Hardcoded license key (replace with your actual key)
                // NOTE: This is a placeholder - replace with your real Syncfusion license key
                // For production, use environment variables or embedded resources instead
                string hardcodedKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ??
                                     Environment.GetEnvironmentVariable("SYNCFUSION_EMBEDDED_LICENSE_KEY");

                if (!string.IsNullOrWhiteSpace(hardcodedKey) && hardcodedKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    SyncfusionLicenseProvider.RegisterLicense(hardcodedKey.Trim());
                    Log.Information("✅ Syncfusion license registered from hardcoded fallback.");
                    return true;
                }

                Log.Information("ℹ️ No embedded license key found in resources or environment.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to register embedded Syncfusion license");
                return false;
            }
        }

        /// <summary>
        /// Loads license key from embedded resource
        /// </summary>
        private static string LoadLicenseFromEmbeddedResource()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "WileyWidget.Resources.license.key";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Log.Debug("Embedded license resource not found: {ResourceName}", resourceName);
                    return null;
                }

                using var reader = new StreamReader(stream);
                var key = reader.ReadToEnd();
                return string.IsNullOrWhiteSpace(key) ? null : key;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load license from embedded resource");
                return null;
            }
        }
    }
}
