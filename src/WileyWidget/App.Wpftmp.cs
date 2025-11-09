// Wpftmp stub for App class
// This file is ONLY included in wpftmp temporary projects to provide
// dummy implementations of abstract Prism methods.
// The real implementations are in App.DependencyInjection.cs and App.Lifecycle.cs

#if WPFTMP

using Prism.Ioc;
using System.Windows;
using Prism.DryIoc;

namespace WileyWidget
{
    // Stub implementations for wpftmp compilation
    // wpftmp only needs these to satisfy abstract method requirements
    // Real implementations are in the main project
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            // Stub implementation - never called in wpftmp
            return null!;
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Stub implementation - never called in wpftmp
        }

        protected override IContainerExtension CreateContainerExtension()
        {
            // Stub implementation - never called in wpftmp
            return null!;
        }
    }
}

#endif
