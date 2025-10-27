// This file provides minimal shim types used only during the temporary
// WPF markup compilation (the *_wpftmp.csproj). The wpftmp project in some
// environments cannot resolve NuGet package references, which leads to
// CS0246/CS0115 errors for types like PrismApplication and related Prism
// infrastructure that App.xaml.cs overrides. To avoid touching runtime
// behavior, this file is included conditionally into the temporary project
// only (see WileyWidget.csproj). Do NOT rely on these types at runtime.

using System;
using System.Windows;

namespace Prism
{
    // Minimal placeholder interfaces used by App.xaml.cs overrides.
    public interface IContainerExtension { }
    public interface IContainerRegistry { }
    public interface IModuleCatalog { }

    namespace Regions
    {
        public class RegionAdapterMappings { }
        public interface IRegionBehaviorFactory { }
    }

    // A very small PrismApplication stub that mirrors the surface area
    // required by App.xaml.cs. Methods are virtual so App can override them.
    public abstract class PrismApplication : Application
    {
        protected virtual void ConfigureRegionAdapterMappings(Regions.RegionAdapterMappings mappings) { }
        protected virtual void ConfigureDefaultRegionBehaviors(Regions.IRegionBehaviorFactory factory) { }
        protected virtual Window CreateShell() => null;
        protected virtual void InitializeShell(Window shell) { }
        protected virtual void RegisterTypes(IContainerRegistry registry) { }
        protected virtual void ConfigureModuleCatalog(IModuleCatalog catalog) { }
        protected virtual void InitializeModules() { }
        protected virtual void OnInitialized() { }
        protected virtual IContainerExtension CreateContainerExtension() => null;
    }
}
