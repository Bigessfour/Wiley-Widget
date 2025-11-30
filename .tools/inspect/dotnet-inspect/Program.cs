using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
// Note: MetadataLoadContext types come from the System.Reflection.MetadataLoadContext assembly
// and are referenced via the package; avoid adding a using directive that conflicts with type names.

// Small utility to inspect Syncfusion.Tools.Windows (dock-related) types either from the
// runtime or from a NuGet package on disk (via MetadataLoadContext).

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Inspecting Syncfusion.Tools.Windows types (Docking-related)\n");

        bool printed = false;

        // Try to find a runtime-loaded assembly first
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name?.StartsWith("Syncfusion.Tools.Windows", StringComparison.OrdinalIgnoreCase) == true);

        if (asm == null)
        {
            try
            {
                asm = Assembly.Load("Syncfusion.Tools.Windows");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load Syncfusion.Tools.Windows by name: " + ex.Message);
            }
        }

        if (asm != null)
        {
            try
            {
                printed = PrintMatchingTypes(asm);
            }
            catch (ReflectionTypeLoadException rtlx)
            {
                Console.WriteLine("ReflectionTypeLoadException when enumerating runtime assembly: " + rtlx.Message);
                // fall back to metadata-only inspection
            }
            catch (FileNotFoundException fnf)
            {
                Console.WriteLine("Missing dependency while reflecting runtime assembly: " + fnf.Message);
            }
        }

        if (!printed)
        {
            var worked = TryInspectFromNugetCache();
            if (!worked)
            {
                Console.WriteLine("Unable to inspect Syncfusion.Tools.Windows types — no results found.");
            }
        }

        Console.WriteLine("Done.");

        return 0;
    }

    static bool TryInspectFromNugetCache()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
                return false;

            var pkgRoot = Path.Combine(userProfile, ".nuget", "packages", "syncfusion.tools.windows");
            if (!Directory.Exists(pkgRoot))
            {
                Console.WriteLine($"NuGet package folder not found at: {pkgRoot}");
                return false;
            }

            // Pick the highest-version folder (if multiple)
            var versionDirs = Directory.GetDirectories(pkgRoot);
            if (versionDirs.Length == 0)
            {
                Console.WriteLine("No versions found under the Syncfusion.Tools.Windows package folder.");
                return false;
            }

            // Try to pick folder that parses as a version, otherwise use the newest modified
            string chosenVersionDir = null;
            var parsed = versionDirs.Select(d => new { Path = d, Name = Path.GetFileName(d), Version = Version.TryParse(Path.GetFileName(d), out var v) ? v : null }).ToArray();
            var withVersions = parsed.Where(p => p.Version != null).OrderByDescending(p => p.Version).ToArray();
            if (withVersions.Length > 0)
                chosenVersionDir = withVersions.First().Path;
            else
                chosenVersionDir = parsed.OrderByDescending(p => File.GetLastWriteTimeUtc(p.Path)).First().Path;

            Console.WriteLine("Inspecting package folder: " + chosenVersionDir);

            // Find the DLL file inside the selected version
            var candidateDlls = Directory.GetFiles(chosenVersionDir, "*.dll", SearchOption.AllDirectories)
                .Where(p => Path.GetFileName(p).IndexOf("Syncfusion.Tools.Windows", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p)
                .ToArray();

            if (candidateDlls.Length == 0)
            {
                Console.WriteLine("Could not find Syncfusion.Tools.Windows assembly (.dll) inside the NuGet package folder.");
                return false;
            }

            var dllPath = candidateDlls.First();
            Console.WriteLine("Found assembly: " + dllPath);

            // Collect resolver paths: include runtime core and all dlls from the assembly folder
            var assemblyDir = Path.GetDirectoryName(dllPath) ?? chosenVersionDir;
            var dllsInFolder = Directory.GetFiles(assemblyDir, "*.dll", SearchOption.TopDirectoryOnly);
            var resolverPaths = new List<string>(dllsInFolder);

            // Also include the runtime's core assembly
            var core = typeof(object).Assembly.Location;
            if (!string.IsNullOrEmpty(core) && !resolverPaths.Contains(core))
                resolverPaths.Add(core);

            Console.WriteLine("Using MetadataLoadContext with the following resolver entries (count=" + resolverPaths.Count + ")");

            var resolver = new PathAssemblyResolver(resolverPaths);
            using var mlc = new MetadataLoadContext(resolver);

            var assembly = mlc.LoadFromAssemblyPath(dllPath);
            Console.WriteLine("Loaded metadata assembly: " + assembly.FullName);

            // Use the same printing logic but for metadata-loaded types
            var types = assembly.GetTypes()
                .Where(t => t?.FullName != null && (t.FullName.IndexOf("Dock", StringComparison.OrdinalIgnoreCase) >= 0 || t.FullName.IndexOf("Docking", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(t => t.FullName)
                .ToArray();

            if (!types.Any())
            {
                Console.WriteLine("No Docking-related types found via MetadataLoadContext.");
                return false;
            }

            foreach (var t in types)
            {
                Console.WriteLine("---- " + t.FullName);

                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                             .Select(p => p.Name)
                             .OrderBy(n => n)
                             .ToArray();
                var events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                             .Select(e => e.Name)
                             .OrderBy(n => n)
                             .ToArray();
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                               .Where(m => !m.IsSpecialName)
                               .Select(m => m.Name)
                               .Distinct()
                               .OrderBy(n => n)
                               .ToArray();

                if (props.Length > 0) Console.WriteLine("  Properties: " + string.Join(", ", props));
                if (events.Length > 0) Console.WriteLine("  Events: " + string.Join(", ", events));
                if (methods.Length > 0) Console.WriteLine("  Methods: " + string.Join(", ", methods.Take(200)));

                Console.WriteLine();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Metadata fallback failed: " + ex.Message);
            return false;
        }
    }

    static bool PrintMatchingTypes(Assembly asm)
    {
        var types = asm.GetTypes()
            .Where(t => t?.FullName != null && (t.FullName.IndexOf("Dock", StringComparison.OrdinalIgnoreCase) >= 0 || t.FullName.IndexOf("Docking", StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(t => t.FullName)
            .ToArray();

        if (!types.Any())
            return false;

        foreach (var t in types)
        {
            Console.WriteLine("---- " + t.FullName);

            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .Select(p => p.Name)
                         .OrderBy(n => n)
                         .ToArray();
            var events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .Select(e => e.Name)
                         .OrderBy(n => n)
                         .ToArray();
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                           .Where(m => !m.IsSpecialName)
                           .Select(m => m.Name)
                           .Distinct()
                           .OrderBy(n => n)
                           .ToArray();

            if (props.Length > 0) Console.WriteLine("  Properties: " + string.Join(", ", props));
            if (events.Length > 0) Console.WriteLine("  Events: " + string.Join(", ", events));
            if (methods.Length > 0) Console.WriteLine("  Methods: " + string.Join(", ", methods.Take(80)));

            Console.WriteLine();
        }

        return true;
    }
}
