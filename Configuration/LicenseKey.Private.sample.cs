// SAMPLE ONLY - DO NOT COMMIT REAL LICENSE
// Copy this file to LicenseKey.Private.cs (which should stay untracked) and insert your real key.
// Ensure .gitignore excludes LicenseKey.Private.cs (add if missing) to prevent accidental commit.

namespace WileyWidget;

public partial class App
{
    /// <summary>
    /// Tries to register an embedded Syncfusion license. Replace SAMPLE_KEY with your real key.
    /// Return true if registration succeeded.
    /// </summary>
    private partial bool TryRegisterEmbeddedLicense()
    {
        // string key = "YOUR_REAL_KEY"; // e.g. from environment variable or secret manager during local dev
        // if (!string.IsNullOrWhiteSpace(key))
        // {
        //     Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key.Trim());
        //     return true;
        // }
        return false; // no key embedded
    }
}
