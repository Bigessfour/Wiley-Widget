using System;
using System.Runtime.CompilerServices;
using WileyWidget.WinForms;

namespace WileyWidget.WinForms.Tests.Infrastructure;

internal static class TestAssemblyBootstrapper
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "true");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_TEST", "true");

        try
        {
            Program.RegisterSyncfusionLicense(configuration: null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestAssemblyBootstrapper] Syncfusion license bootstrap warning: {ex.Message}");
        }
    }
}
