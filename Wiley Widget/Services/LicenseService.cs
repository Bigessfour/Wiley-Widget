using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Syncfusion.Licensing;

namespace WileyWidget.Services;

/// <summary>
/// Service for managing Syncfusion license registration.
/// </summary>
public class LicenseService
{
    private readonly IConfiguration _configuration;

    public LicenseService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Registers Syncfusion licenses with priority hierarchy.
    /// </summary>
    public bool RegisterLicenses()
    {
        try
        {
            Log.Information("🔑 Starting centralized Syncfusion license registration");

            // Priority 1: Configuration-based license (highest priority)
            var configKey = _configuration?["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                // SECURITY: Don't log license key content
                Log.Information("✅ Syncfusion license registered from configuration");
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                return true;
            }

            // Priority 2: Embedded license (if available)
            if (TryRegisterEmbeddedLicense())
            {
                Log.Information("✅ Syncfusion license registered from embedded source");
                return true;
            }

            // Priority 3: Environment variable (User → Machine → Process scope)
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                // SECURITY: Don't log license key content
                Log.Information("✅ Syncfusion license registered from environment variable");
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                return true;
            }

            // Priority 4: File-based license (fallback, single read operation)
            var licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
            if (File.Exists(licensePath))
            {
                var key = File.ReadAllText(licensePath).Trim();
                if (!string.IsNullOrWhiteSpace(key) && key.Length > 50)
                {
                    // SECURITY: Don't log license key content
                    Log.Information("✅ Syncfusion license registered from file");
                    SyncfusionLicenseProvider.RegisterLicense(key);
                    return true;
                }
                else
                {
                    Log.Warning("⚠️ License file found but content invalid (length: {Length})", key.Length);
                }
            }

            Log.Warning("⚠️ No valid Syncfusion license found - application will run in trial mode");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error during Syncfusion license registration");
            return false;
        }
    }

    /// <summary>
    /// Registers early licenses in the constructor.
    /// </summary>
    public static void RegisterEarlyLicenses()
    {
        try
        {
            // Try environment variable first
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Console.WriteLine("✅ Early Syncfusion license registered from environment");
                return;
            }

            // Try embedded license
            // This would be implemented in a derived class or separate file
            Console.WriteLine("ℹ️ Early license registration completed (environment checked)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Early license registration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that required Syncfusion assemblies are loaded.
    /// </summary>
    public void ValidateSyncfusionAssemblies()
    {
        var requiredAssemblies = new[]
        {
            "Syncfusion.SfSkinManager.WPF",
            "Syncfusion.Shared.WPF",
            "Syncfusion.Tools.WPF",
            "Syncfusion.SfInput.WPF"
        };

        foreach (var assemblyName in requiredAssemblies)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly != null)
                {
                    Log.Information("✅ Syncfusion assembly loaded: {AssemblyName} v{Version}",
                        assemblyName, assembly.GetName().Version);
                }
                else
                {
                    Log.Warning("⚠️ Syncfusion assembly not found: {AssemblyName}", assemblyName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Error validating assembly: {AssemblyName}", assemblyName);
            }
        }
    }

    /// <summary>
    /// Logs information about trial mode activation.
    /// </summary>
    public void LogTrialModeActivation()
    {
        try
        {
            Log.Warning("🚨 APPLICATION RUNNING IN SYNCFUSION TRIAL MODE 🚨");
            Log.Warning("📋 Trial Mode Details:");
            Log.Warning("   • Limited functionality may be available");
            Log.Warning("   • Watermarks may appear on controls");
            Log.Warning("   • Some features may be disabled");
            Log.Warning("   • Performance may be degraded");
            Log.Warning("💡 To resolve: Set SYNCFUSION_LICENSE_KEY environment variable or add license.key file");
            Log.Warning("🔗 Reference: https://help.syncfusion.com/cr/wpf/Syncfusion.html#licensing");
        }
        catch (Exception ex)
        {
            // Fallback logging in case structured logging fails
            System.Diagnostics.Debug.WriteLine($"Syncfusion Trial Mode Activated: {ex.Message}");
        }
    }

    /// <summary>
    /// Virtual hook allowing LicenseKey.Private.cs to embed the license.
    /// </summary>
    protected virtual bool TryRegisterEmbeddedLicense()
    {
        // Default implementation - returns false (no embedded license)
        // Override in derived classes or use explicit interface implementation
        return false;
    }
}
