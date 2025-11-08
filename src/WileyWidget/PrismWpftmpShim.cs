// This file provides minimal shim types used only during the temporary
// WPF markup compilation (the *_wpftmp.csproj). The wpftmp project in some
// environments cannot resolve NuGet package references, which leads to
// CS0246/CS0115 errors for types like PrismApplication and related Prism
// infrastructure that App.xaml.cs overrides. To avoid touching runtime
// behavior, this file is included conditionally into the temporary project
// only (see WileyWidget.csproj). Do NOT rely on these types at runtime.

using System;
using System.Windows;
using System.Windows.Markup;

[assembly: XmlnsDefinition("http://prismlibrary.com/", "Prism")]
[assembly: XmlnsPrefix("http://prismlibrary.com/", "prism")]

namespace Prism.Container.DryIoc
{
    // Shim for DryIocContainerExtension
    public class DryIocContainerExtension : Prism.Ioc.IContainerExtension, Prism.Ioc.IContainerRegistry
    {
        public DryIocContainerExtension(object container) { }
        public T Resolve<T>() => default;
        public object Resolve(Type type) => null;
        public T Resolve<T>(string name) => default;
        public Prism.Ioc.IContainerRegistry Register(Type from, Type to) => this;
        public Prism.Ioc.IContainerRegistry Register(Type from, Type to, string name) => this;
        public Prism.Ioc.IContainerRegistry RegisterInstance(Type type, object instance) => this;
        public Prism.Ioc.IContainerRegistry RegisterSingleton(Type from, Type to) => this;
    }
}

namespace Prism.Wpf
{
    // Empty namespace shim to allow "using Prism.Wpf;" to compile in wpftmp
}

namespace Prism
{
    namespace Ioc
    {
        // Minimal placeholder interfaces matching Prism namespaces
        public interface IContainerExtension {
            T Resolve<T>();
            object Resolve(Type type);
            T Resolve<T>(string name);
        }
        public interface IContainerRegistry {
            IContainerRegistry Register(Type from, Type to);
            IContainerRegistry Register(Type from, Type to, string name);
            IContainerRegistry RegisterInstance(Type type, object instance);
            IContainerRegistry RegisterSingleton(Type from, Type to);
        }
        public interface IContainerProvider {
            T Resolve<T>();
            object Resolve(Type type);
        }

        // Extension methods for generic registration
        public static class ContainerRegistryExtensions
        {
            public static IContainerRegistry Register<T>(this IContainerRegistry registry) => registry;
            public static IContainerRegistry Register<TFrom, TTo>(this IContainerRegistry registry) where TTo : TFrom => registry;
            public static IContainerRegistry RegisterSingleton<T>(this IContainerRegistry registry) => registry;
            public static IContainerRegistry RegisterSingleton<TFrom, TTo>(this IContainerRegistry registry) where TTo : TFrom => registry;
            public static IContainerRegistry RegisterInstance<T>(this IContainerRegistry registry, T instance) => registry;
        }
    }

    namespace Modularity
    {
        public interface IModuleCatalog { }
    }

    // A very small PrismApplicationBase stub that mirrors the surface area
    // required by App.xaml.cs. Methods are virtual so App can override them.
    // This is the base class that Prism.Wpf actually exports at runtime.
    public abstract class PrismApplicationBase : Application
    {
    protected virtual void ConfigureRegionAdapterMappings(Prism.Navigation.Regions.RegionAdapterMappings mappings) { }
    protected virtual void ConfigureDefaultRegionBehaviors(Prism.Navigation.Regions.IRegionBehaviorFactory factory) { }
        protected virtual Window CreateShell() => null;
        protected virtual void InitializeShell(Window shell) { }
        protected virtual void RegisterTypes(Prism.Ioc.IContainerRegistry registry) { }
        protected virtual void ConfigureModuleCatalog(Prism.Modularity.IModuleCatalog catalog) { }
        protected virtual void InitializeModules() { }
        protected virtual void OnInitialized() { }
        protected virtual Prism.Ioc.IContainerExtension CreateContainerExtension() => null;

        // Provide a minimal Container property to satisfy references in App.xaml.cs during wpftmp
        protected Prism.Ioc.IContainerExtension Container { get; private set; }

        protected PrismApplicationBase()
        {
            // Leave Container null in shim context; App code should compile against the property
            Container = null;
        }
    }

    // Concrete PrismApplication class for XAML instantiation in wpftmp
    // CRITICAL: Must be non-abstract to match XAML-generated partial class (App.g.cs)
    // This aligns with runtime Prism.Wpf.PrismApplication structure
    public class PrismApplication : PrismApplicationBase
    {
        // Concrete class - no abstract methods, allows XAML instantiation
    }
}

// Bold.Licensing shim namespace for WPFTMP
namespace Bold.Licensing
{
    public static class BoldLicenseProvider
    {
        public static void RegisterLicense(string licenseKey) { }
    }
}
