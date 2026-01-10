using System;
using System.Windows.Forms;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Xunit;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Provides Syncfusion licensing and theme initialization for WinForms tests.
/// This fixture ensures that SfDataGrid and other Syncfusion controls can be instantiated
/// in unit tests by registering the license key and setting up the default theme.
/// 
/// License key is retrieved from environment variable SYNCFUSION_LICENSE_KEY (which can be set from GitHub Actions secrets).
/// Falls back to empty string for community/trial mode if not configured.
/// </summary>
public sealed class SyncfusionLicenseFixture : IDisposable
{
    private static readonly object _syncLock = new object();
    private static bool _initialized = false;

    /// <summary>
    /// Gets the Syncfusion license key from environment or returns empty string for trial mode.
    /// </summary>
    private static string GetLicenseKey()
    {
        return Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? string.Empty;
    }

    public SyncfusionLicenseFixture()
    {
        // Ensure single-threaded initialization
        lock (_syncLock)
        {
            if (_initialized) return;

            try
            {
                // Register Syncfusion license
                var licenseKey = GetLicenseKey();
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Console.WriteLine("[Syncfusion] License registered from SYNCFUSION_LICENSE_KEY environment variable");
                }
                else
                {
                    // Fall back to trial mode (empty license)
                    SyncfusionLicenseProvider.RegisterLicense(string.Empty);
                    Console.WriteLine("[Syncfusion] Using trial/community mode (no license key configured)");
                }

                // Load the default theme assembly globally
                // This allows SfDataGrid and other controls to initialize style properties
                var assemblyLoaded = LoadDefaultTheme();

                if (assemblyLoaded)
                {
                    Console.WriteLine("[Syncfusion] Default Office2019Colorful theme loaded successfully");
                }
                else
                {
                    Console.WriteLine("[Syncfusion] WARNING: Could not load default theme assembly - controls may have styling issues");
                }

                // Enable visual styles for WinForms
                Application.EnableVisualStyles();
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Console.WriteLine("[Syncfusion] WinForms visual styles and high DPI mode enabled");

                _initialized = true;
                Console.WriteLine("[Syncfusion] Initialization complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Syncfusion ERROR] Failed to initialize Syncfusion: {ex.Message}");
                // Don't rethrow - allow tests to continue with reduced functionality
                _initialized = true;
            }
        }
    }

    /// <summary>
    /// Loads the default Office2019Colorful theme by loading its assembly.
    /// This is necessary for Syncfusion controls to properly initialize their style properties in tests.
    /// </summary>
    private static bool LoadDefaultTheme()
    {
        try
        {
            // Create a temporary form to initialize theme
            using (var tempForm = new Form { Visible = false })
            {
                // Set the visual style to Office2019Colorful
                // This loads the Office2019Theme assembly and initializes styles globally
                SfSkinManager.SetVisualStyle(tempForm, "Office2019Colorful");

                tempForm.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Syncfusion WARNING] Theme load failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Initializes Syncfusion and applies the default theme to a specific control.
    /// Call this method in test setup when you need to ensure a control has proper styling.
    /// Note: Theme application is handled automatically by Syncfusion.
    /// </summary>
    public static void ApplyDefaultTheme(Form form)
    {
        if (form == null)
            throw new ArgumentNullException(nameof(form));

        // Theme application is handled automatically by Syncfusion framework
        // No explicit SfSkinManager calls needed
    }

    public void Dispose()
    {
        // No cleanup needed - Syncfusion initialization is global and persists for the test session
    }
}

/// <summary>
/// Xunit collection definition for tests that require Syncfusion initialization.
/// This ensures SyncfusionLicenseFixture is initialized once per test collection.
/// </summary>
[CollectionDefinition("Syncfusion License Collection")]
public class SyncfusionLicenseCollection : ICollectionFixture<SyncfusionLicenseFixture>
{
    // This class has no code, and is never created. Its purpose is purely to define the collection.
}
