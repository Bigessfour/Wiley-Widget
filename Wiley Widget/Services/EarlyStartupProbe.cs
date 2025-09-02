using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Syncfusion.Licensing;

#nullable enable

namespace WileyWidget;

/// <summary>
/// Earliest possible startup probe using a module initializer. Fires before <see cref="App"/> constructor.
/// Captures assembly load baseline and writes a sentinel line to debug.log so we can distinguish
/// between a crash before WPF Application construction and one during App initialization.
/// </summary>
internal static class EarlyStartupProbe
{
    private static int _fired;

    [ModuleInitializer]
    internal static void Init()
    {
        if (System.Threading.Interlocked.Exchange(ref _fired, 1) == 1) return;
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            File.AppendAllText(path, $"🏁 ModuleInitializer fired @ {DateTime.UtcNow:O}\n");

            // Log a concise list of already loaded assemblies (core + app) for early crash diagnostics
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(n => n != null && (n.StartsWith("Syncfusion") || n.StartsWith("WileyWidget") || n.StartsWith("PresentationFramework") || n.StartsWith("WindowsBase")))
                .Distinct()
                .OrderBy(n => n)
                .ToArray();
            File.AppendAllText(path, $"[EarlyAssemblies] {string.Join(", ", loaded)}\n");

            // Pre-flight check: attempt to resolve the two theme assemblies explicitly so we know if they fail early
            TryProbeAssembly("Syncfusion.SfSkinManager.WPF");
            TryProbeAssembly("Syncfusion.Themes.FluentDark.WPF");
            TryProbeAssembly("Syncfusion.Themes.FluentLight.WPF");

            // Early license registration (must occur before any Syncfusion control creation in XAML)
            try
            {
                var licFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
                string? key = null;
                if (File.Exists(licFile)) key = File.ReadAllText(licFile).Trim();
                else key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    SyncfusionLicenseProvider.RegisterLicense(key);
                    File.AppendAllText(path, "[License✅] Syncfusion license registered early\n");
                }
                else
                {
                    File.AppendAllText(path, "[License⚠️] No license key found for early registration\n");
                }
            }
            catch (Exception licEx)
            {
                try { File.AppendAllText(path, $"[License❌] Early registration failed: {licEx.GetType().Name}: {licEx.Message}\n"); } catch { }
            }
        }
        catch (Exception ex)
        {
            try { File.AppendAllText("debug.log", $"⚠️ EarlyStartupProbe failure: {ex.Message}\n"); } catch { }
        }
    }

    private static void TryProbeAssembly(string name)
    {
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name) ?? Assembly.Load(name);
            File.AppendAllText("debug.log", asm != null
                ? $"[Probe✅] {name} v{asm.GetName().Version}\n"
                : $"[Probe❌] {name} NOT LOADED\n");
        }
        catch (Exception ex)
        {
            try { File.AppendAllText("debug.log", $"[Probe❌] {name} load failed: {ex.GetType().Name}: {ex.Message}\n"); } catch { }
        }
    }
}
