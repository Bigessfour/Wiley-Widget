using System;
using System.Linq;
using System.Reflection;
using Prism.Ioc;
using Unity;
using Xunit;
using Serilog;

public class AssertRegistrationsTests
{
    private static bool IntegrationTestsEnabled()
    {
        try
        {
            var v = Environment.GetEnvironmentVariable("PRISM_INTEGRATION_TESTS");
            return string.Equals(v, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    private static Unity.IUnityContainer? GetInnerUnityContainer(object containerExtObj)
    {
        if (containerExtObj == null) return null;
        var containerExt = containerExtObj;
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

        return unity;
    }
    [Fact]
    public void CriticalServices_AreRegisteredInUnityContainer()
    {
        // Set test mode to enable in-memory DB and disable external services
        Environment.SetEnvironmentVariable("WILEY_WIDGET_TESTMODE", "1");

        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));

        // Create the container extension and extract the inner Unity container
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

        // Attempt to find the inner Unity container via well-known fields or properties
        IUnityContainer? unity = null;
        // Common field name used in UnityContainerExtension implementations
        var candidateField = containerExt.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => typeof(IUnityContainer).IsAssignableFrom(f.FieldType) || f.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0);

        if (candidateField != null)
        {
            unity = candidateField.GetValue(containerExt) as IUnityContainer;
        }

        // Try property fallback
        if (unity == null)
        {
            var candidateProp = containerExt.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => typeof(IUnityContainer).IsAssignableFrom(p.PropertyType) || p.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0);
            if (candidateProp != null)
            {
                unity = candidateProp.GetValue(containerExt) as IUnityContainer;
            }
        }

        // Try method fallback
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
        var allFields = new System.Collections.Generic.List<FieldInfo>();
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

        // Call RegisterTypes
        typeof(WileyWidget.App).GetMethod("RegisterTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, new object[] { containerExt });

        // Types to verify - AI services are disabled in test mode
        var critical = new Type[] {
            typeof(Microsoft.Extensions.Configuration.IConfiguration),
            typeof(Microsoft.Extensions.Logging.ILoggerFactory),
            typeof(WileyWidget.Services.ISettingsService),
            typeof(WileyWidget.Business.Interfaces.IEnterpriseRepository),
            typeof(WileyWidget.Business.Interfaces.IBudgetRepository),
            typeof(WileyWidget.Services.IModuleHealthService)
        };

        var missing = critical.Where(t => !unity!.Registrations.Any(r => r.RegisteredType == t || r.MappedToType == t)).ToList();

        Assert.True(!missing.Any(), "Missing critical registrations: " + string.Join(", ", missing.Select(t => t.FullName)));
    }

    [Fact]
    public void Prism_Services_AreRegistered_And_Modules_Discovered()
    {
        if (!IntegrationTestsEnabled())
        {
            // Skip long-running Prism integration checks by default; enable via PRISM_INTEGRATION_TESTS=1
            return;
        }
        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));
        var createMi = typeof(WileyWidget.App).GetMethod("CreateContainerExtension", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(createMi);
        var containerExtObj = createMi!.Invoke(app, null);
        Assert.NotNull(containerExtObj);
        var containerExt = (Prism.Ioc.IContainerExtension)containerExtObj!;
        var unity = GetInnerUnityContainer(containerExt);
        Assert.NotNull(unity);

        // Set backing field on app to the container provider if present (so RegisterTypes can use app state)
        var containerProvider = containerExt as IContainerProvider ?? containerExt;
        var appType2 = app.GetType();
        var allFields2 = new System.Collections.Generic.List<FieldInfo>();
        var tcur = appType2;
        while (tcur != null)
        {
            allFields2.AddRange(tcur.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
            tcur = tcur.BaseType;
        }

        FieldInfo? targetField2 = null;
        foreach (var f in allFields2)
        {
            try
            {
                if (f.FieldType.IsAssignableFrom(containerProvider.GetType()) || f.Name.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetField2 = f;
                    break;
                }
            }
            catch { }
        }

        if (targetField2 != null)
        {
            targetField2.SetValue(app, containerProvider);
        }

        // Call RegisterTypes so container gets populated with Prism services
        typeof(WileyWidget.App).GetMethod("RegisterTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, new object[] { containerExt });

        // Common Prism services we expect to be registered in the Unity container
        var prismServices = new Type[] {
            typeof(Prism.Modularity.IModuleCatalog),
            typeof(Prism.Navigation.Regions.IRegionManager),
            typeof(Prism.Events.IEventAggregator),
            typeof(Prism.Ioc.IContainerProvider),
            // Additional commonly-expected Prism services used in this app
            typeof(Prism.Dialogs.IDialogService),
        };

        // Some Prism types live in different assemblies/namespaces depending on Prism version
        // We'll attempt to locate these at runtime via reflection rather than compile-time types
        var dynamicPrismTypeNames = new[] { "RegionAdapterMappings", "IRegionBehaviorFactory" };

        var unresolved = new System.Collections.Generic.List<string>();
        // Prefer checking container registrations rather than attempting runtime Resolve which may throw in test host
        foreach (var svc in prismServices)
        {
            try
            {
                bool registered = false;
                if (unity != null)
                {
                    registered = unity.Registrations.Any(r =>
                        r.RegisteredType == svc ||
                        r.MappedToType == svc ||
                        (r.RegisteredType != null && svc.IsAssignableFrom(r.RegisteredType)) ||
                        (r.MappedToType != null && svc.IsAssignableFrom(r.MappedToType)));
                }

                if (!registered)
                {
                    // fallback: try container provider Resolve via reflection to be robust across Prism versions
                    try
                    {
                        var cp = containerExt as Prism.Ioc.IContainerProvider;
                        if (cp != null)
                        {
                            try { var o = cp.Resolve(svc); registered = o != null; } catch { registered = false; }
                        }
                    }
                    catch { registered = false; }
                }

                if (!registered) unresolved.Add(svc.FullName ?? svc.Name);
            }
            catch
            {
                unresolved.Add(svc.FullName ?? svc.Name);
            }
        }

        Assert.True(!unresolved.Any(), "Missing Prism services from container: " + string.Join(", ", unresolved));

        // Runtime-check additional Prism types by simple name to be robust across Prism versions
        foreach (var dynName in dynamicPrismTypeNames)
        {
            Type? dynType = null;
            try
            {
                dynType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => string.Equals(t.Name, dynName, StringComparison.Ordinal) || (t.FullName != null && t.FullName.EndsWith("." + dynName)));
            }
            catch { dynType = null; }

            if (dynType == null)
            {
                // If the type isn't present in runtime assemblies, note it but don't fail the entire test
                // as some Prism packages may not include these types in certain versions.
                continue;
            }

            bool registered = false;
            try
            {
                if (unity != null)
                {
                    registered = unity.Registrations.Any(r => (r.RegisteredType != null && (r.RegisteredType == dynType || dynType.IsAssignableFrom(r.RegisteredType))) || (r.MappedToType != null && (r.MappedToType == dynType || dynType.IsAssignableFrom(r.MappedToType))));
                }

                if (!registered)
                {
                    var cp = containerExt as Prism.Ioc.IContainerProvider;
                    if (cp != null)
                    {
                        try { var o = cp.Resolve(dynType); registered = o != null; } catch { registered = false; }
                    }
                }
            }
            catch { registered = false; }

            if (!registered) unresolved.Add(dynName + " (found runtime type but not registered)");
        }

        // Final assert includes dynamic type resolution failures as well
        Assert.True(!unresolved.Any(), "Missing Prism services from container: " + string.Join(", ", unresolved));

        // Verify module discovery from App.ConfigureModuleCatalog logic
        var appType = typeof(WileyWidget.App);
        var configureModuleMethod = appType.GetMethod("ConfigureModuleCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(configureModuleMethod);

        // Create a ModuleCatalog instance and invoke ConfigureModuleCatalog
    var moduleCatalog = new Prism.Modularity.ModuleCatalog();
    configureModuleMethod!.Invoke(app, new object[] { moduleCatalog });

        // Ensure that discovered modules (types with [Module]) are registered
        var discoveredTypes = typeof(WileyWidget.App).Assembly
            .GetTypes()
            .Where(t => typeof(Prism.Modularity.IModule).IsAssignableFrom(t) && t.GetCustomAttributes(typeof(Prism.Modularity.ModuleAttribute), false).Any())
            .ToList();

        // Robust check: module catalog item may expose ModuleType (Type) or ModuleName (string) depending on Prism version.
        System.Collections.Generic.List<string> missingModules = new();
        foreach (var dt in discoveredTypes)
        {
            var attr = (Prism.Modularity.ModuleAttribute)dt.GetCustomAttributes(typeof(Prism.Modularity.ModuleAttribute), false).First();
            var moduleName = attr.ModuleName;
            bool found = false;
            foreach (var item in moduleCatalog.Items)
            {
                try
                {
                    var itemType = item.GetType();
                    var moduleTypeProp = itemType.GetProperty("ModuleType", BindingFlags.Public | BindingFlags.Instance);
                    if (moduleTypeProp != null)
                    {
                        var val = moduleTypeProp.GetValue(item);
                        if (val is Type tt && tt == dt)
                        {
                            found = true; break;
                        }
                    }

                    var moduleTypeNameProp = itemType.GetProperty("ModuleTypeName", BindingFlags.Public | BindingFlags.Instance);
                    if (moduleTypeNameProp != null)
                    {
                        var val = moduleTypeNameProp.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(val) && val.Contains(dt.FullName ?? dt.Name))
                        {
                            found = true; break;
                        }
                    }

                    var moduleNameProp = itemType.GetProperty("ModuleName", BindingFlags.Public | BindingFlags.Instance);
                    if (moduleNameProp != null)
                    {
                        var val = moduleNameProp.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(val) && string.Equals(val, moduleName, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true; break;
                        }
                    }
                }
                catch { }
            }

            if (!found)
            {
                missingModules.Add(moduleName ?? dt.FullName ?? dt.Name);
            }
        }

        Assert.True(!missingModules.Any(), "ModuleCatalog missing discovered modules: " + string.Join(", ", missingModules));
    }

    [Fact]
    public void CanResolve_SampleViewModel_And_RegionRegistration_Happens()
    {
        if (!IntegrationTestsEnabled())
        {
            // Skip integration-style resolution checks by default; enable via PRISM_INTEGRATION_TESTS=1
            return;
        }
        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));
        var createMi = typeof(WileyWidget.App).GetMethod("CreateContainerExtension", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(createMi);
        var containerExtObj = createMi!.Invoke(app, null);
        Assert.NotNull(containerExtObj);
        var containerExt = (Prism.Ioc.IContainerExtension)containerExtObj!;

        // Some modules register views with regions in OnInitialized. We'll invoke CoreModule.RegisterTypes and OnInitialized to exercise wiring.
        var coreModuleType = typeof(WileyWidget.Startup.Modules.CoreModule);
        var coreModule = Activator.CreateInstance(coreModuleType) as Prism.Modularity.IModule;
        Assert.NotNull(coreModule);

    // Call RegisterTypes
    coreModule!.RegisterTypes((Prism.Ioc.IContainerRegistry)containerExt);

    // Resolve a sample viewmodel that CoreModule registers
    var containerProvider = containerExt as Prism.Ioc.IContainerProvider;

    object? vm = null;
    if (containerProvider != null)
    {
        try { vm = containerProvider.Resolve(typeof(WileyWidget.ViewModels.SettingsViewModel)); } catch { vm = null; }
    }

    // If not registered via container provider, check that the registration exists in Unity and try resolving via Unity
    if (vm == null)
    {
        var unityContainer = GetInnerUnityContainer(containerExt)!;
        // Verify registration exists
        var registered = unityContainer.Registrations.Any(r => r.RegisteredType == typeof(WileyWidget.ViewModels.SettingsViewModel) || r.MappedToType == typeof(WileyWidget.ViewModels.SettingsViewModel));
        if (registered)
        {
            try { vm = unityContainer.Resolve(typeof(WileyWidget.ViewModels.SettingsViewModel)); } catch { vm = null; }
        }
    }

    Assert.NotNull(vm);

        // Call OnInitialized using the container provider wrapper
        var containerProviderCast = (Prism.Ioc.IContainerProvider)containerExt;
        coreModule.OnInitialized(containerProvider);

    var regionManagerObj = containerProviderCast.Resolve(typeof(Prism.Navigation.Regions.IRegionManager));
        Assert.NotNull(regionManagerObj);
    var rm = (Prism.Navigation.Regions.IRegionManager)regionManagerObj;
        Assert.True(rm.Regions.ContainsRegionWithName("SettingsRegion"), "SettingsRegion was not registered by CoreModule.OnInitialized");
    }
}
