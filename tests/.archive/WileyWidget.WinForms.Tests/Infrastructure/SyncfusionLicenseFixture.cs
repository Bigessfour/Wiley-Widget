#nullable enable

using System;
using Syncfusion.Licensing;
using Serilog;

namespace WileyWidget.WinForms.Tests.Infrastructure
{
    /// <summary>
    /// Fixture that initializes Syncfusion licensing for all xUnit tests.
    /// Must be referenced as a Collection fixture to ensure it runs before any tests.
    /// </summary>
    public class SyncfusionLicenseFixture : IDisposable
    {
        private static bool _licenseInitialized = false;
        private static readonly object _lock = new object();

        public SyncfusionLicenseFixture()
        {
            lock (_lock)
            {
                if (_licenseInitialized)
                    return;

                InitializeSyncfusionLicense();
                _licenseInitialized = true;
            }
        }

        private static void InitializeSyncfusionLicense()
        {
            try
            {
                // Try to get license from environment variable first
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
                               ?? Environment.GetEnvironmentVariable("Syncfusion__LicenseKey");

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    Log.Warning("Syncfusion license key not found in environment. Syncfusion controls will show trial watermark.");
                    return;
                }

                // Remove quotes and whitespace if present (common when pasted from config)
                licenseKey = licenseKey.Trim('"', '\'');
                licenseKey = string.Concat(System.Linq.Enumerable.Where(licenseKey, c => !char.IsWhiteSpace(c)));

                // Register the license with Syncfusion
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Information("Syncfusion license registered successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register Syncfusion license. Controls will show trial watermark.");
                // Don't throw - allow tests to continue with trial watermark
            }
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    /// <summary>
    /// xUnit collection fixture that ensures SyncfusionLicenseFixture is created once per test session
    /// and runs before all tests in the [Collection] marked tests.
    /// </summary>
    [Xunit.CollectionDefinition("Syncfusion License Collection")]
    public class SyncfusionLicenseCollection : Xunit.ICollectionFixture<SyncfusionLicenseFixture>
    {
        // This class has no code, and never creates an instance of itself.
        // It's just used to define and register the SyncfusionLicenseFixture.
    }
}
