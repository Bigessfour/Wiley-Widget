// SAMPLE ONLY - DO NOT COMMIT REAL LICENSE
// Copy this file to LicenseKey.Private.cs (which should stay untracked) and insert your real key.
// Ensure .gitignore excludes LicenseKey.Private.cs (add if missing) to prevent accidental commit.
// NOTE: This sample file should NOT be included in the build - it contains no implementation.

using System;
using System.IO;
using System.Reflection;
using Serilog;

namespace WileyWidget
{
    /// <summary>
    /// Embedded License Manager for loading Syncfusion license keys from embedded resources or environment variables.
    /// This class is called by the virtual TryRegisterEmbeddedLicense() method in App.xaml.cs.
    /// </summary>
    public static class EmbeddedLicenseManager
    {
        /// <summary>
        /// Tries to register an embedded Syncfusion license from embedded resource or environment variable.
        /// Return true if registration succeeded.
        /// </summary>
        public static bool TryRegisterEmbeddedLicense()
        {
            try
            {
                // First, try to load from embedded resource
                var key = LoadLicenseFromEmbeddedResource();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key.Trim());
                    Log.Information("✅ Syncfusion license registered from embedded resource.");
                    return true;
                }

                // Fallback: try environment variable
                key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY_EMBEDDED") ??
                      Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

                if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key.Trim());
                    Log.Information("✅ Syncfusion license registered from embedded environment variable.");
                    return true;
                }

                Log.Information("ℹ️ No embedded license key found in resources or environment.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "❌ Error registering embedded Syncfusion license");
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
                        Log.Debug("Embedded license resource '{ResourceName}' not found.", resourceName);
                        return null;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        var key = reader.ReadToEnd().Trim();
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            Log.Warning("Embedded license resource exists but is empty.");
                            return null;
                        }

                        Log.Information("📄 Found embedded license key (length: {Length})", key.Length);
                        return key;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "❌ Error loading license from embedded resource");
                return null;
            }
        }
    }
}
