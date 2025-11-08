// App.Wpftmp.cs - Wpftmp Compilation Support
//
// This file provides minimal implementations of abstract Prism methods required
// to complete the App partial class during WPF temporary markup compilation (wpftmp).
//
// CONTEXT:
// - During wpftmp, App.g.cs is generated from App.xaml, creating a partial class App
// - App.xaml.cs (the main implementation) is excluded from wpftmp to avoid dependency issues
// - This file fills the gap by providing stub implementations ONLY during wpftmp builds
//
// WHEN ACTIVE:
// - Only compiled when WPFTMP preprocessor constant is defined (during wpftmp build)
// - Completely inactive during normal Debug/Release builds (App.xaml.cs is used instead)
//
// WHY NEEDED:
// - Prism.DryIoc.PrismApplication has abstract methods: CreateShell() and RegisterTypes()
// - Without implementations, wpftmp compilation fails with CS0534 errors
// - These stub methods allow XAML markup compilation to succeed
//
// RELATED FILES:
// - App.xaml: XAML definition declaring App : PrismApplication
// - App.xaml.cs: Real runtime implementation (excluded from wpftmp)
// - App.g.cs: Generated partial class from App.xaml (wpftmp creates this)
// - PrismWpftmpShim.cs: Type shims for when NuGet packages aren't resolved (legacy fallback)
//
// See: docs/reference/NUGET_PACKAGE_RESOLUTION.md for complete architecture

#if WPFTMP

using System.Windows;
using Prism.Ioc;

// Type alias to match App.xaml.cs conventions
using IContainerRegistry = Prism.Ioc.IContainerRegistry;

namespace WileyWidget
{
    /// <summary>
    /// Wpftmp-specific partial class completion for App.
    /// Provides minimal implementations of abstract Prism methods required for XAML compilation.
    /// This code is NEVER executed at runtime - it exists only to satisfy the compiler during wpftmp build.
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Wpftmp stub: Creates the main application shell window.
        /// Returns null because this is never executed - wpftmp is compile-time only.
        /// </summary>
        protected override Window CreateShell()
        {
            // Wpftmp compilation stub - never executed at runtime
            return null!;
        }

        /// <summary>
        /// Wpftmp stub: Registers types with the DI container.
        /// Empty implementation because this is never executed - wpftmp is compile-time only.
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry registry)
        {
            // Wpftmp compilation stub - never executed at runtime
        }
    }
}

#endif // WPFTMP
