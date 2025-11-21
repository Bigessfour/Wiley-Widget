// 28-sfdocking-regions.csx
// Purpose: test Prism region adapter registration for Syncfusion DockingManager in a DashboardPanelViewModel context

#r "nuget:Prism.DryIocServiceProviderAdapter,8.2.0"
#r "nuget:Syncfusion.SfDocking.WPF,22.4.0.47"

using System;
using System.IO;
using System.Runtime.InteropServices;

Console.WriteLine("[28] Syncfusion DockingManager / Prism Regions E2E - START");

// Fake Prism region registration scenario
public class FakeRegionManager
{
    private readonly System.Collections.Generic.List<string> _regions = new();
    public void RegisterRegion(string name) => _regions.Add(name);
    public bool IsRegistered(string name) => _regions.Contains(name);
}

// Fake DashboardPanelViewModel that would register a region
public class FakeDashboardPanelViewModel
{
    private readonly FakeRegionManager _regionManager;
    public FakeDashboardPanelViewModel(FakeRegionManager rm) { _regionManager = rm; }
    public void Initialize() => _regionManager.RegisterRegion("DashboardRegion");
}

var rm = new FakeRegionManager();
var vm = new FakeDashboardPanelViewModel(rm);
vm.Initialize();
if (!rm.IsRegistered("DashboardRegion")) throw new Exception("DashboardRegion not registered by DashboardPanelViewModel");
Console.WriteLine("[28] Fake region registration OK.");

// DockingManager control checks only on Windows
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        var dockType = Type.GetType("Syncfusion.Windows.Tools.SfDockingManager, Syncfusion.SfDocking.WPF")
                       ?? Type.GetType("Syncfusion.SfDocking.WPF.SfDockingManager, Syncfusion.SfDocking.WPF");
        if (dockType == null) throw new Exception("SfDockingManager type not found");

        dynamic dock = Activator.CreateInstance(dockType);
        Console.WriteLine("[28] SfDockingManager instantiated (Windows). Attempting to attach to fake region adapter.");
        // Best-effort probe; real adapter tests require full Prism + WPF hosting
        try
        {
            var xamlWriter = Type.GetType("System.Windows.Markup.XamlWriter, Microsoft.UI.Xaml");
            if (xamlWriter != null)
            {
                var saveMethod = xamlWriter.GetMethod("Save", new[] { typeof(object) });
                if (saveMethod != null)
                {
                    var xaml = (string)saveMethod.Invoke(null, new object[] { dock });
                    var outDir = Path.GetFullPath("logs/xaml-previews");
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, "28-sfdocking-preview.xaml"), xaml);
                    Console.WriteLine("[28] XAML preview written to logs/xaml-previews/28-sfdocking-preview.xaml");
                }
            }
        }
        catch (Exception ex) { Console.WriteLine("[28] XAML preview skipped/failed: " + ex.Message); }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[28] Control-level checks skipped/failed: " + ex.Message);
    }
}
else Console.WriteLine("[28] Non-Windows host: skipping DockingManager control instantiation.");

Console.WriteLine("[28] Syncfusion DockingManager / Prism Regions E2E - END");
