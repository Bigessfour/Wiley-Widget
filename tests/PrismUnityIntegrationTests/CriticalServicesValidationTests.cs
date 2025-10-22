using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Prism.Ioc;
using Xunit;

public class CriticalServicesValidationTests
{
    /// <summary>
    /// Logs the full exception chain for diagnostic purposes.
    /// </summary>
    private static void LogExceptionChain(Exception ex)
    {
        Console.WriteLine("=== Exception Chain Analysis ===");
        int depth = 0;
        Exception? current = ex;
        while (current != null && depth < 10)
        {
            Console.WriteLine($"[{depth}] {current.GetType().Name}: {current.Message}");
            if (current.StackTrace != null)
            {
                Console.WriteLine($"    Stack: {current.StackTrace.Split('\n').FirstOrDefault()?.Trim()}");
            }
            current = current.InnerException;
            depth++;
        }

        if (depth >= 10)
        {
            Console.WriteLine("... (chain truncated)");
        }
        Console.WriteLine("=== End Exception Chain ===");
    }
    [Fact]
    public void RegisterTypes_ThenValidateCriticalServices_ThrowsIfMissing()
    {
        // Set test mode to enable in-memory DB
        Environment.SetEnvironmentVariable("WILEY_WIDGET_TESTMODE", "1");

        // Create a container extension via the App.CreateContainerExtension() static behavior
        var miCreate = typeof(WileyWidget.App).GetMethod("CreateContainerExtension", BindingFlags.Instance | BindingFlags.NonPublic);
        // Use an uninitialized App instance to avoid WPF Application ctor
        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));
        var miRegister = typeof(WileyWidget.App).GetMethod("RegisterTypes", BindingFlags.Instance | BindingFlags.NonPublic);
        var miValidate = typeof(WileyWidget.App).GetMethod("ValidateCriticalServices", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Prism.Ioc.IContainerRegistry), typeof(bool) }, null);

        Assert.NotNull(miCreate);
        Assert.NotNull(miRegister);
        Assert.NotNull(miValidate);

        // Create container extension instance
        var containerExt = (IContainerExtension)typeof(WileyWidget.App).GetMethod("CreateContainerExtension", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, null)!;

        // Ensure Application.Current points to our test App instance so ValidatePrismInfrastructure recognizes the test path
        var wpfAppType = typeof(System.Windows.Application);
        var staticAppField = wpfAppType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(f => f.FieldType == wpfAppType);
        if (staticAppField != null)
        {
            staticAppField.SetValue(null, app);
        }

        // Extract an IContainerProvider from the extension (either the extension itself or the Instance property)
        object? containerProvider = null;
        if (containerExt is IContainerProvider cp)
        {
            containerProvider = cp;
        }
        else
        {
            var prop = containerExt.GetType().GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                containerProvider = prop.GetValue(containerExt);
            }
        }

        // Try to set a private backing field for the container on the App instance so Resolve() calls work.
        if (containerProvider != null)
        {
            var appType = app.GetType();
            var allFields = new List<FieldInfo>();
            var t = appType;
            while (t != null)
            {
                allFields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
                t = t.BaseType;
            }

            FieldInfo? targetField = null;
            foreach (var f in allFields)
            {
                if (f.FieldType.IsAssignableFrom(containerProvider.GetType()) || f.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetField = f;
                    break;
                }
            }

            if (targetField != null)
            {
                targetField.SetValue(app, containerProvider);
            }
        }

        // Call RegisterTypes to register services into the container
        miRegister!.Invoke(app, new object[] { containerExt });

        // Determine testMode the same way RegisterTypes does
        var testMode = (Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE") ?? "0") == "1";

        // Now call ValidateCriticalServices - if any critical services are missing, the method should throw
        var ex = Record.Exception(() => miValidate!.Invoke(app, new object[] { containerExt, testMode }));

        // If it threw a TargetInvocationException, unwrap and rethrow so xUnit reports the inner error
        if (ex is TargetInvocationException tie && tie.InnerException != null)
        {
            // Log the full exception chain for better diagnostics
            LogExceptionChain(tie);
            throw tie.InnerException;
        }

        // If no exception, assert success - otherwise the thrown inner exception indicates missing registrations
        Assert.Null(ex);
    }
}
