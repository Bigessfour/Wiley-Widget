using System;
using System.Linq;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Service for validating that required assemblies are loaded.
/// Specifically handles Syncfusion assembly validation.
/// </summary>
public class AssemblyValidationService
{
    private readonly string[] _requiredSyncfusionAssemblies = new[]
    {
        "Syncfusion.SfSkinManager.WPF",
        "Syncfusion.Shared.WPF",
        "Syncfusion.Tools.WPF",
        "Syncfusion.SfInput.WPF"
    };

    /// <summary>
    /// Validates that required Syncfusion assemblies are loaded.
    /// </summary>
    public void ValidateSyncfusionAssemblies()
    {
        Log.Information("🔍 Validating Syncfusion assemblies...");

        foreach (var assemblyName in _requiredSyncfusionAssemblies)
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

        Log.Information("🔍 Syncfusion assembly validation completed");
    }

    /// <summary>
    /// Gets the list of required Syncfusion assemblies.
    /// </summary>
    public string[] RequiredSyncfusionAssemblies => _requiredSyncfusionAssemblies;
}
