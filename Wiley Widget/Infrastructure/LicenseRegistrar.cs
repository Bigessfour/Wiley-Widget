using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Serilog;
using Syncfusion.Licensing;

namespace WileyWidget.Infrastructure;

/// <summary>
/// Centralized license registration service for third-party components.
/// Handles Syncfusion license registration with fallback hierarchy and proper error handling.
/// </summary>
public static class LicenseRegistrar
{
    private static bool _isRegistered;

    /// <summary>
    /// Registers all required licenses using the specified configuration.
    /// Should be called early in application startup.
    /// </summary>
    /// <param name="configuration">Application configuration containing license keys</param>
    /// <returns>True if all licenses were registered successfully, false otherwise</returns>
    public static bool RegisterLicenses(IConfiguration configuration)
    {
        if (_isRegistered)
        {
            Log.Warning("⚠️ Licenses already registered - skipping duplicate registration");
            return true;
        }

        try
        {
            Log.Information("🔑 Starting centralized license registration");

            var success = RegisterSyncfusionLicense(configuration);

            if (success)
            {
                _isRegistered = true;
                Log.Information("✅ All licenses registered successfully");
            }
            else
            {
                Log.Warning("⚠️ Some licenses failed to register - application may run in trial mode");
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Critical error during license registration");
            return false;
        }
    }

    /// <summary>
    /// Registers Syncfusion license using priority-based fallback hierarchy.
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <returns>True if license was registered successfully</returns>
    private static bool RegisterSyncfusionLicense(IConfiguration configuration)
    {
        try
        {
            // Priority 1: Configuration-based license (highest priority)
            var configKey = configuration?["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                Log.Information("✅ Syncfusion license registered from configuration");
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
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Log.Information("✅ Syncfusion license registered from environment variable");
                return true;
            }

            // Priority 4: File-based license (fallback, single read operation)
            var licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
            if (File.Exists(licensePath))
            {
                var key = File.ReadAllText(licensePath).Trim();
                if (!string.IsNullOrWhiteSpace(key) && key.Length > 50)
                {
                    SyncfusionLicenseProvider.RegisterLicense(key);
                    Log.Information("✅ Syncfusion license registered from file");
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
    /// Attempts to register an embedded license from the assembly.
    /// </summary>
    /// <returns>True if embedded license was found and registered</returns>
    private static bool TryRegisterEmbeddedLicense()
    {
        try
        {
            // Try to call the embedded license manager if available
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetType("WileyWidget.EmbeddedLicenseManager");
            if (type != null)
            {
                var method = type.GetMethod("TryRegisterEmbeddedLicense", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    return (bool)method.Invoke(null, null);
                }
            }
        }
        catch
        {
            // Ignore reflection errors - fall back to default
        }

        // Default implementation - returns false (no embedded license)
        return false;
    }

    /// <summary>
    /// Performs early license registration without configuration dependency.
    /// Used during bootstrap phase before full configuration is available.
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

            // Try file-based license
            var licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
            if (File.Exists(licensePath))
            {
                var key = File.ReadAllText(licensePath).Trim();
                if (!string.IsNullOrWhiteSpace(key) && key.Length > 50)
                {
                    SyncfusionLicenseProvider.RegisterLicense(key);
                    Console.WriteLine("✅ Early Syncfusion license registered from file");
                    return;
                }
            }

            Console.WriteLine("⚠️ No early license found - will retry with full configuration");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Early license registration failed: {ex.Message}");
        }
    }
}
