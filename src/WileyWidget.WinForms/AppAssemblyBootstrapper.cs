using System;
using System.Runtime.CompilerServices;

namespace WileyWidget.WinForms;

internal static class AppAssemblyBootstrapper
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            Program.RegisterSyncfusionLicense(configuration: null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppAssemblyBootstrapper] Syncfusion license bootstrap warning: {ex.Message}");
        }
    }
}
