using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Prism.Ioc;
using Unity;
using Xunit;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using Serilog;

public class ResolveAllRegistrationsTests
{
    [Fact]
    public void AllRegistrations_CanBeResolved()
    {
        // Set test mode to enable in-memory DB
        Environment.SetEnvironmentVariable("WILEY_WIDGET_TESTMODE", "1");
        var testMode = true;

        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));

        var createMi = typeof(WileyWidget.App).GetMethod("CreateContainerExtension", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(createMi);
        var containerExtObj = createMi!.Invoke(app, null);

        // Ensure Application.Current points to our test App instance so ValidatePrismInfrastructure recognizes the test path
        var wpfAppType = typeof(System.Windows.Application);
        var staticAppField = wpfAppType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(f => f.FieldType == wpfAppType);
        if (staticAppField != null)
        {
            staticAppField.SetValue(null, app);
        }

        Assert.NotNull(containerExtObj);
        var containerExt = (IContainerExtension)containerExtObj!;

        // Extract inner Unity container (reuse same heuristics as AssertRegistrationsTests)
        IUnityContainer? unity = null;
        var candidateField = containerExt.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => typeof(IUnityContainer).IsAssignableFrom(f.FieldType) || f.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0);

        if (candidateField != null)
        {
            unity = candidateField.GetValue(containerExt) as IUnityContainer;
        }

        if (unity == null)
        {
            var candidateProp = containerExt.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => typeof(IUnityContainer).IsAssignableFrom(p.PropertyType) || p.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0);
            if (candidateProp != null)
            {
                unity = candidateProp.GetValue(containerExt) as IUnityContainer;
            }
        }

        if (unity == null)
        {
            var getContainerMethod = containerExt.GetType().GetMethod("GetContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getContainerMethod != null)
            {
                unity = getContainerMethod.Invoke(containerExt, null) as IUnityContainer;
            }
        }

        Assert.NotNull(unity);

        // Set backing field on app to the container provider if present (search base types too)
        var containerProvider = containerExt as IContainerProvider ?? containerExt;
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
            try
            {
                if (f.FieldType.IsAssignableFrom(containerProvider.GetType()) || f.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetField = f;
                    break;
                }
            }
            catch { }
        }

        if (targetField != null)
        {
            targetField.SetValue(app, containerProvider);
        }

        // Call RegisterTypes to populate registrations
        typeof(WileyWidget.App).GetMethod("RegisterTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, new object[] { containerExt });

        // In test mode, initialize the in-memory database to ensure repositories can be resolved
        if (testMode)
        {
            try
            {
                var factory = unity!.Resolve<IDbContextFactory<AppDbContext>>();
                using var context = factory.CreateDbContext();
                context.Database.EnsureCreated();
                Log.Debug("In-memory database initialized for test mode");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize in-memory database, repository resolution may fail");
            }
        }

        // Build a list of registrations to try to resolve.
        var registrations = unity!.Registrations.ToList();

        // Opt-outs: registrations we expect to fail in test environment (runtime-only, require parameters, open generics, etc.)
        var optOutFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // add known runtime-only types here if necessary, e.g. types that require a database connection
            // UI, platform or external-library types that are not available in test environment:
            // REMOVED: Legacy navigation services deleted
            // "WileyWidget.Services.INavigationService",
            // "WileyWidget.Services.NavigationService",
            // "WileyWidget.Services.IScopedRegionService",
            // "WileyWidget.Services.ScopedRegionService",
            "WileyWidget.Services.IPrismErrorHandler",
            "WileyWidget.Services.PrismErrorHandler",
            "WileyWidget.Views.MainWindow",
            "WileyWidget.ViewModels.MainViewModel",
            "WileyWidget.ViewModels.EnterpriseViewModel",
            "WileyWidget.Regions.DockingManagerRegionAdapter",
            // Syncfusion and other third-party controls/adapters
            "Syncfusion.Windows.Tools.Controls.DockingManager",
            // Navigation service internals
            // REMOVED: Legacy navigation services deleted
            // "WileyWidget.Services.NavigationService+*",
        };

        var failures = new List<string>();

        foreach (var reg in registrations)
        {
            try
            {
                var regType = reg.RegisteredType;

                // skip open generics and interfaces that are not concrete
                if (regType.IsGenericTypeDefinition) continue;

                // Skip common framework types or ones in opt-out list
                if (regType.IsInterface && reg.MappedToType == null) {
                    // try resolving as the registered type (common for services registered by interface)
                }

                var mapped = reg.MappedToType ?? reg.RegisteredType;

                // Skip if mapped type is null or open generic
                if (mapped == null || mapped.IsGenericTypeDefinition) continue;

                if (optOutFullNames.Contains(mapped.FullName ?? string.Empty) || optOutFullNames.Contains(reg.RegisteredType.FullName ?? string.Empty))
                {
                    continue;
                }

                // Try resolve by RegisteredType (preferred) then by MappedToType
                object? resolved = null;
                try { resolved = unity.Resolve(reg.RegisteredType); } catch { }
                if (resolved == null && reg.MappedToType != null)
                {
                    try { resolved = unity.Resolve(reg.MappedToType); } catch { }
                }

                if (resolved == null)
                {
                    // If it's an interface, try named resolution variations
                    // Finally record failure
                    failures.Add($"Could not resolve: RegisteredType={reg.RegisteredType.FullName}, MappedTo={reg.MappedToType?.FullName}, Name={reg.Name}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Exception while resolving registration {reg.RegisteredType.FullName}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (failures.Any())
        {
            var msg = "Resolve failures:\n" + string.Join("\n", failures.Take(200));
            Assert.True(false, msg);
        }
    }
}
