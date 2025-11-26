using System;
using System.Reflection;
using Microsoft.UI.Xaml;

namespace WileyWidget.WinUI
{
    public static class AppStartupHelper
    {
        // Helper that can be invoked by App static constructor if needed.
        public static void TryInitializeRuntime()
        {
            // Attempt to initialize the Windows App Runtime bootstrapper early on STA thread
            try
            {
                var bootstrapTypeNames = new[]
                {
                    "Microsoft.WindowsAppRuntime.Bootstrap.Net.Bootstrapper, Microsoft.WindowsAppRuntime.Bootstrap.Net",
                    "Microsoft.WindowsAppRuntime.Bootstrap.Net.Bootstrapper, Microsoft.WindowsAppRuntime",
                    "Microsoft.WindowsAppRuntime.Bootstrapper, Microsoft.WindowsAppRuntime"
                };

                Type? bootstrapType = null;
                foreach (var name in bootstrapTypeNames)
                {
                    bootstrapType = Type.GetType(name, throwOnError: false);
                    if (bootstrapType != null) break;
                }

                var initMethod = bootstrapType?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                if (initMethod is not null && initMethod.GetParameters().Length == 0)
                {
                    initMethod.Invoke(null, null);
                }
            }
            catch
            {
                // Best-effort initialization - do not block startup on failure here
            }

            // Attempt to initialize WinRT ComWrappers support if available
            try
            {
                var comWrappersType = Type.GetType("WinRT.ComWrappersSupport, WinRT.Runtime", throwOnError: false)
                                    ?? Type.GetType("WinRT.ComWrappersSupport, WinRT", throwOnError: false);
                var init = comWrappersType?.GetMethod("InitializeComWrappers", BindingFlags.Public | BindingFlags.Static);
                if (init is not null && init.GetParameters().Length == 0)
                {
                    init.Invoke(null, null);
                }
            }
            catch
            {
                // Best-effort
            }

            // DispatcherQueue guard for .NET 9 unpackaged (prevents init before thread)
            // Fix for #10027 - ensures Bootstrap.Initialize doesn't race in unpackaged
            try
            {
                var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (queue != null && queue.HasThreadAccess)
                {
                    // Re-invoke Bootstrap.Initialize only on UI thread with proper dispatcher
                    var bootstrapTypeNames = new[]
                    {
                        "Microsoft.WindowsAppRuntime.Bootstrap.Net.Bootstrapper, Microsoft.WindowsAppRuntime.Bootstrap.Net",
                        "Microsoft.WindowsAppRuntime.Bootstrap.Net.Bootstrapper, Microsoft.WindowsAppRuntime",
                        "Microsoft.WindowsAppRuntime.Bootstrapper, Microsoft.WindowsAppRuntime"
                    };

                    Type? bootstrapType = null;
                    foreach (var name in bootstrapTypeNames)
                    {
                        bootstrapType = Type.GetType(name, throwOnError: false);
                        if (bootstrapType != null) break;
                    }

                    var initMethod = bootstrapType?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                    if (initMethod is not null && initMethod.GetParameters().Length == 0)
                    {
                        initMethod.Invoke(null, null);  // Bootstrap only on UI thread
                    }
                }
            }
            catch
            {
                // Best-effort
            }

            // Now start the WinUI application on the STA thread
            // Nothing here - prefer the generated XAML Program.Main to be the process entrypoint.
        }
    }
}
