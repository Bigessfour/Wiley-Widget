using System.Reflection;
using System.Runtime.CompilerServices;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Registers an <see cref="AppDomain.AssemblyResolve"/> handler via a module initializer so it
/// runs before any test type is JIT-compiled.
///
/// Background: <c>WileyWidget.WinForms.dll</c> was compiled with a reference to
/// <c>Microsoft.WinForms.Utilities.Shared, Version=1.6.0.0</c> (from the
/// <c>microsoft.winforms.designer.sdk</c> NuGet package, which ships only a ref assembly).
/// That package's ref assembly is rejected at runtime by the CLR with
/// <see cref="BadImageFormatException"/>.  The only runtime-loadable copy lives inside the
/// Visual Studio installation at a different version (18.x).  Because .NET's default probe
/// path will fail to satisfy the 1.6.0 request, the <see cref="AppDomain.AssemblyResolve"/>
/// event fires and this handler redirects it to the VS runtime DLL.
/// </summary>
internal static class AssemblyResolver
{
    private static readonly string[] _candidatePaths =
    [
        @"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\Windows.Forms\Microsoft.WinForms.Utilities.Shared.dll",
        @"C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\Windows.Forms\Microsoft.WinForms.Utilities.Shared.dll",
        @"C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\Windows.Forms\Microsoft.WinForms.Utilities.Shared.dll",
        @"C:\Program Files\Microsoft Visual Studio\17\Community\Common7\IDE\CommonExtensions\Microsoft\Windows.Forms\Microsoft.WinForms.Utilities.Shared.dll",
        @"C:\Program Files\Microsoft Visual Studio\17\Enterprise\Common7\IDE\CommonExtensions\Microsoft\Windows.Forms\Microsoft.WinForms.Utilities.Shared.dll",
        @"C:\Program Files\Microsoft Visual Studio\17\Professional\Common7\IDE\CommonExtensions\Microsoft\Windows.Forms\Microsoft.WinForms.Utilities.Shared.dll",
    ];

    // Cached so we only probe the filesystem once per process.
    private static Assembly? _resolved;

    // CA2255: ModuleInitializer in a non-application assembly is intentional here —
    // we need the handler registered before any test type from WileyWidget.WinForms.dll
    // is JIT-compiled.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize()
#pragma warning restore CA2255
    {
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name);
        if (!string.Equals(name.Name, "Microsoft.WinForms.Utilities.Shared", StringComparison.OrdinalIgnoreCase))
            return null;

        if (_resolved is not null)
            return _resolved;

        foreach (var path in _candidatePaths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                _resolved = Assembly.LoadFrom(path);
                return _resolved;
            }
            catch (BadImageFormatException)
            {
                // This candidate is a reference assembly — skip it.
            }
            catch (Exception)
            {
                // Any other load failure — try next candidate.
            }
        }

        return null;
    }
}
