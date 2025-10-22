using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Prism.Ioc;
using Unity;
using WileyWidget;
using WileyWidget.Configuration;
using WileyWidget.Services;
using Xunit;

// Tests that push on private App methods and DI registration behavior without touching DB/UI
public class HarderPrismUnityTests
{
    [Fact]
    public void ValidatePrismInfrastructure_NullRegistry_Throws()
    {
        // Create an App instance without running its constructor to avoid WPF Application creation
        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));
        var mi = typeof(WileyWidget.App).GetMethod("ValidatePrismInfrastructure", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);
        var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(app, new object?[] { null }));
        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    [Fact]
    public void ValidatePrismInfrastructure_DummyRegistry_NoUnity_ThrowsInvalidOperation()
    {
        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));
        var mi = typeof(WileyWidget.App).GetMethod("ValidatePrismInfrastructure", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);

        var fake = new TestContainerRegistry();

    var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(app, new object?[] { fake }));
    Assert.NotNull(ex.InnerException);
    // Some versions of Prism/Unity surface a cast error when GetContainer extension is unavailable.
    // Accept either InvalidOperationException or InvalidCastException, but ensure the message references Unity/container.
    Assert.True(ex.InnerException is InvalidOperationException || ex.InnerException is InvalidCastException, "Expected InvalidOperationException or InvalidCastException");
    Assert.Contains("unity", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegisterAppOptions_RegistersOptionsInstances_WhenConfiguratorMissing()
    {
        var fake = new TestContainerRegistry();

        var inMemory = new Dictionary<string, string?>
        {
            ["App:SomeSetting"] = "SomeValue"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var unity = new UnityContainer(); // no AppOptionsConfigurator registered - triggers configurator catch

        var app = (WileyWidget.App)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WileyWidget.App));
        var mi = typeof(WileyWidget.App).GetMethod("RegisterAppOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);

        // Should not throw
        mi.Invoke(app, new object?[] { fake, config, unity });

        // Verify that IOptions<AppOptions> and IOptionsMonitor<AppOptions> were registered in our fake registry
        Assert.Contains(typeof(IOptions<AppOptions>), fake.RecordedInstances.Keys);
        Assert.Contains(typeof(IOptionsMonitor<AppOptions>), fake.RecordedInstances.Keys);
    }

    [Fact]
    public void UpdateLatestHealthReport_SetsStartupProgress_ForEnumerableAndSingle()
    {
        // Enumerable
        var modules = new List<ModuleHealthInfo>
        {
            new ModuleHealthInfo { ModuleName = "A", Status = ModuleHealthStatus.Healthy, RegistrationTime = DateTime.UtcNow },
            new ModuleHealthInfo { ModuleName = "B", Status = ModuleHealthStatus.Failed, RegistrationTime = DateTime.UtcNow }
        };

        WileyWidget.App.UpdateLatestHealthReport(modules);
        Assert.Equal(modules, WileyWidget.App.StartupProgress);
        Assert.NotNull(WileyWidget.App.LastHealthReportUpdate);

        // Single
        var single = new ModuleHealthInfo { ModuleName = "C", Status = ModuleHealthStatus.Registered, RegistrationTime = DateTime.UtcNow };
        WileyWidget.App.UpdateLatestHealthReport(single);
        Assert.Equal(single, WileyWidget.App.StartupProgress);
    }

    // Minimal IContainerRegistry implementation sufficient for RegisterAppOptions and basic tests
    private class TestContainerRegistry : IContainerRegistry
    {
        public Dictionary<Type, object> RecordedInstances { get; } = new Dictionary<Type, object>();

        public IUnityContainer? GetContainerInstance { get; set; }

        // RegisterInstance overloads
        public IContainerRegistry RegisterInstance<T>(T instance) where T : class
        {
            RecordedInstances[typeof(T)] = instance!;
            return this;
        }

        public IContainerRegistry RegisterInstance(Type type, object instance)
        {
            RecordedInstances[type] = instance;
            return this;
        }

        // The IContainerRegistry interface defines many methods; provide minimal no-op implementations
        public IContainerRegistry RegisterSingleton(Type from, Type to) { return this; }
        public IContainerRegistry RegisterSingleton(Type from, Type to, string name) { return this; }
        public IContainerRegistry RegisterSingleton<TFrom, TTo>() where TTo : TFrom { return this; }
        public IContainerRegistry RegisterSingleton<TFrom>(Func<IContainerProvider, TFrom> factory) { return this; }
    public IContainerRegistry RegisterSingleton(Type from, Func<IContainerProvider, object> factory) { return this; }
    public IContainerRegistry RegisterSingleton(Type from, Func<object> factory) { return this; }
        public IContainerRegistry RegisterManySingleton(Type from, params Type[] to) { return this; }
        public IContainerRegistry Register(Type from, Type to) { return this; }
        public IContainerRegistry Register(Type from, Type to, string name) { return this; }
    public IContainerRegistry Register(Type from, Func<IContainerProvider, object> factory) { return this; }
    public IContainerRegistry Register(Type from, Func<object> factory) { return this; }
        public IContainerRegistry Register(Type from, Func<IContainerProvider, object> factory, string name) { return this; }
        public IContainerRegistry Register<TFrom, TTo>() where TTo : TFrom { return this; }
        public IContainerRegistry Register<TFrom, TTo>(string name) where TTo : TFrom { return this; }
        public IContainerRegistry Register<T>(Func<IContainerProvider, T> factory) { return this; }
        public IContainerRegistry Register<T>(Func<IContainerProvider, T> factory, string name) { return this; }
        public IContainerRegistry RegisterScoped(Type from, Type to) { return this; }
    public IContainerRegistry RegisterScoped(Type from, Func<IContainerProvider, object> factory) { return this; }
    public IContainerRegistry RegisterScoped(Type from, Func<object> factory) { return this; }
        public IContainerRegistry RegisterScoped(Type from, Func<IContainerProvider, object> factory, string name) { return this; }
        public IContainerRegistry RegisterScoped<TFrom, TTo>() where TTo : TFrom { return this; }
        public IContainerRegistry RegisterScoped<TFrom, TTo>(string name) where TTo : TFrom { return this; }
        public IContainerRegistry RegisterForNavigation(Type view, string name, params Type[] viewModel) => this;
        public IContainerRegistry RegisterForNavigation<T>() where T : class => this;
        public IContainerRegistry RegisterDialog<TView, TViewModel>(string name) where TView : class where TViewModel : class => this;
        public IContainerRegistry RegisterInstance(Type type, object instance, string name) => this;
        public IContainerRegistry RegisterInstance<T>(T instance, string name) where T : class { RecordedInstances[typeof(T)] = instance!; return this; }
        public IContainerRegistry RegisterSingletonInstance(Type type, object instance) { RecordedInstances[type] = instance; return this; }
        public IContainerRegistry RegisterMany(Type from, params Type[] to) { return this; }
        public IContainerRegistry RegisterMany(Type from, IEnumerable<Type> to) { return this; }
        public bool IsRegistered(Type type) { return RecordedInstances.ContainsKey(type); }
        public bool IsRegistered(Type type, string name) { return RecordedInstances.ContainsKey(type); }
    }

    // Helper subclass to expose private instance methods via reflection when needed
    private class TestableApp : WileyWidget.App
    {
    }
}
