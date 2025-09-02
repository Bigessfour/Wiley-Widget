// PRIVATE FILE - DO NOT COMMIT TO VERSION CONTROL
// This file contains sensitive license information and should remain untracked.
// Ensure .gitignore excludes LicenseKey.Private.cs to prevent accidental commit.

using System;
using System.IO;
using System.Reflection;
using Syncfusion.Licensing;

namespace WileyWidget
{
    /// <summary>
    /// Static class containing embedded license logic for Syncfusion
    /// </summary>
    internal static class EmbeddedLicenseManager
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
                    return true;
                }

                // Second priority: Try to get key from environment variable (for development)
                key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY_EMBEDDED") ??
                      Environment.GetEnvironmentVariable("SYNCFUSION_EMBEDDED_LICENSE_KEY") ??
                      Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

                if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    SyncfusionLicenseProvider.RegisterLicense(key.Trim());
                    return true;
                }

                // Third priority: Hardcoded license key (replace with your actual key)
                // NOTE: This is a placeholder - replace with your real Syncfusion license key
                // For production, use environment variables or embedded resources instead
                string hardcodedKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ??
                                     Environment.GetEnvironmentVariable("SYNCFUSION_EMBEDDED_LICENSE_KEY");

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

        /// <summary>
        /// Loads license key from embedded resource.
        /// The resource should be named "WileyWidget.license.key" and embedded in the assembly.
        /// </summary>
        private static string LoadLicenseFromEmbeddedResource()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "WileyWidget.license.key";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Embedded license resource '{resourceName}' not found.");
                        return null;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        var key = reader.ReadToEnd().Trim();
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            System.Diagnostics.Debug.WriteLine("Embedded license resource exists but is empty.");
                            return null;
                        }

                        System.Diagnostics.Debug.WriteLine($"Found embedded license key (length: {key.Length})");
                        return key;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading license from embedded resource: {ex.Message}");
                return null;
            }
        }
    }
}
