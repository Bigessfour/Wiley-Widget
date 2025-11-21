# Prism Usage — Wiley-Widget (Ready Reference)

## Overview

This is the authoritative quick reference for Prism usage in the Wiley-Widget WPF application. It documents the Prism version used, correct namespaces, common patterns (MVVM, regions, modules), Syncfusion integration notes, common pitfalls (including Copilot hallucinations), and recommended practices specific to this repository.

Keep this file in `docs/PRISM_USAGE.md` as the single source of truth for Prism-related code and Copilot prompts.

## Prism version

- Target Prism: Prism 9.0.x
  - NuGet: `Prism.Uno.WinUI` (example pinned version: `9.0.537`)
  - DI adapter: `Prism.DryIoc` (use `Prism.DryIoc` / `Prism.DryIoc.Extensions` as needed)
- Supported frameworks: .NET 6.0+ and compatibility with .NET 8.0 when project SDKs align.
- Recommendation: Pin package versions in csproj files and update through CI-validated PRs.

Example package install (run in project folder):

```powershell
# Run in PowerShell (pwsh)
dotnet add package Prism.Uno.WinUI --version 9.0.537
dotnet add package Prism.DryIoc --version 9.0.537
```

## Correct namespaces (do not use legacy ones)

### C# (use these)

- Prism.Ioc (IContainerRegistry, IContainerProvider)
- Prism.Modularity (IModule, IModuleCatalog)
- Prism.Mvvm (BindableBase, ViewModelLocator, DelegateCommand)
- Prism.Navigation.Regions (IRegionManager, RegionManager, IRegionBehaviorFactory)
- Prism.Events (PubSubEvent<T>, IEventAggregator)
- Prism.DryIoc (DryIoc container extension types)
- Prism.Navigation (INavigationAware, IRegionNavigationService)

Avoid legacy/CodePlex-era names such as `Microsoft.Practices.Prism` or XAML URIs like `http://www.codeplex.com/prism` — they will cause resolution and runtime issues.

### XAML

Use the modern Prism XAML URI:

```xml

```

Example: `<ContentControl prism:RegionManager.RegionName="MainRegion" />`

## Bootstrapping (App / PrismApplication)

- The App class should inherit from the Prism-provided `PrismApplication` base type (no hard-coded namespace in your class declaration).
  When using DryIoc as the container adapter, override `CreateContainerExtension()` to return a `DryIoc` container extension (example below).
- Override key methods to register types, configure modules, and add region adapter mappings.

Note: to avoid XAML markup-compile problems during the temporary wpftmp project step, use the standard WPF `<Application>` root in `App.xaml` instead of trying to reference a Prism-specific XAML tag. The code-behind may still inherit `PrismApplication` so runtime Prism behavior remains unchanged.

Minimal `App.xaml.cs` style example:

```csharp
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Prism.DryIoc;

public partial class App : PrismApplication
{
    protected override IContainerExtension CreateContainerExtension()
    {
        return new DryIocContainerExtension(new DryIoc.Container());
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // Register services
        containerRegistry.RegisterSingleton<ISettingsService, SettingsService>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // Register modules
        moduleCatalog.AddModule<CoreModule>();
    }

    protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings mappings)
    {
        base.ConfigureRegionAdapterMappings(mappings);
        // Register custom Syncfusion adapters here, e.g. DockingManagerRegionAdapter
        // mappings.RegisterMapping(typeof(Syncfusion.Windows.Tools.Controls.DockingManager), container.Resolve<DockingManagerRegionAdapter>());
    }
}
```

Notes:

- Register Syncfusion license keys and call ThemeUtility before creating any controls (see Bootstrapping & Syncfusion section below).

## Modularity

- Implement modules by creating classes that implement `IModule` (or deriving from `IModule` helpers).
- Register modules in `ConfigureModuleCatalog` or use a DirectoryModuleCatalog when appropriate.

Example module skeleton:

```csharp
using Prism.Ioc;
using Prism.Modularity;

public class BudgetModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<BudgetView, BudgetViewModel>("BudgetView");
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // Optional initialization logic
    }
}
```

## Regions and navigation

- Use `IRegionManager.RequestNavigate("RegionName", "ViewName")` for navigation.
- Prefer view-model-first navigation patterns with `RegisterForNavigation<TView, TViewModel>(name)`.
- Add custom region behaviors in `ConfigureDefaultRegionBehaviors` where needed (e.g., `AutoActivateBehavior`).

Example:

```csharp
_regionManager.RequestNavigate("MainRegion", "BudgetView");
```

### Region adapters (Syncfusion)

- For Syncfusion controls (DockingManager, SfDataGrid), register custom region adapters in `ConfigureRegionAdapterMappings`.
- Adapter responsibilities:
  - Map Prism view registration to the control's intended API (e.g., ItemsSource for data hosts).
  - Avoid injecting raw UIElements into data-host controls.
  - Clean up collection event handlers to avoid memory leaks (unsubscribe or use weak handlers).
- Approved pattern examples:
  - `SfDataGridRegionAdapter` should map a collection of viewmodels to the grid's `ItemsSource`.
  - `DockingManagerRegionAdapter` should place views into appropriate docking slots by RegionName on ContentControls.

## MVVM patterns

- ViewModels inherit from `Prism.Mvvm.BindableBase`.
- Use `DelegateCommand`/`DelegateCommand<T>` for commands.
- Enable locator in XAML when appropriate:

```xml
<UserControl prism:ViewModelLocator.AutoWireViewModel="True" ... />
```

- Keep UI logic in views, business/presentation logic in viewmodels.

## Event aggregation

- Use `IEventAggregator` for decoupled pub/sub across modules.
- Define events as `PubSubEvent<T>` and subscribe/unsubscribe carefully to prevent leaks.

Example:

```csharp
public class AccountUpdatedEvent : PubSubEvent<Account> { }

// Publish
_eventAggregator.GetEvent<AccountUpdatedEvent>().Publish(account);

// Subscribe
_eventAggregator.GetEvent<AccountUpdatedEvent>().Subscribe(OnAccountUpdated, ThreadOption.PublisherThread, false);
```

## Dependency injection (DI)

- Register services through `IContainerRegistry` in `RegisterTypes`.
- Prefer `RegisterSingleton<TInterface, TImpl>()` for long-lived services and `Register<TInterface, TImpl>()` for per-resolve instances.
- Resolve services by constructor injection in ViewModels and modules.

Example registration:

```csharp
containerRegistry.RegisterSingleton<IAIService, XAIService>();
```

## Bootstrapping & Syncfusion (important project rules)

- Register Syncfusion license(s) in `App()` constructor before any Syncfusion control is constructed.
- For global application theme initialization in App.xaml.cs OnStartup/OnInitialized, use SfSkinManager directly (as recommended by Syncfusion for bootstrap before controls load).
- For all other theme applications (window constructors, runtime changes), use the project's `ThemeUtility`.
- Ensure `ThemeUtility.TryApplyTheme(window, themeName)` is used in window constructors and theme switching code.

Example global theme initialization (in App.xaml.cs OnStartup):

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    SfSkinManager.ApplyThemeAsDefaultStyle = true;
    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
}
```

Example theme application (in window constructor):

```csharp
public MyWindow()
{
    InitializeComponent();
    ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);
}
```

## Common Copilot pitfalls and fixes

- Copilot may suggest legacy namespace URIs or `Microsoft.Practices.Prism`. Always replace with modern `Prism.*` namespaces.
- Copilot may suggest injecting UIElements into an ItemsControl; prefer mapping collections to `ItemsSource` instead.
- To avoid repeated bad suggestions, add explicit using directives and XML comments in modules and register the required adapters in bootstrap code.

## Best practices (project-specific)

- Pin Prism packages in project files and update with CI checks.
- Add unit tests for module initialization using mocking for `IContainerRegistry`.
- For region adapters, add integration/unit tests to validate mapping behavior and ensure no memory leaks.
- Keep Syncfusion adapter code in `WileyWidget.Regions` or a similarly named folder and namespace.
- Add Copilot guidance comments in frequently-scaffolded files to steer generated code.

## Migration tips (legacy Prism → Prism 9)

- Replace old XAML URIs with `http://prismlibrary.com/`.
- Replace `Microsoft.Practices.Prism` namespaces with `Prism.*` equivalents.
- Validate module registration APIs (IModule/IModuleCatalog) and adapt any DryIoc-specific extension usage.

## Quick reference snippets

Register view for navigation:

```csharp
containerRegistry.RegisterForNavigation<BudgetView, BudgetViewModel>("BudgetView");
```

Request navigation:

```csharp
_regionManager.RequestNavigate("MainRegion", "BudgetView");
```

Register a singleton service:

```csharp
containerRegistry.RegisterSingleton<ISettingsService, SettingsService>();
```

## Troubleshooting

- If XAML fails: Ensure Prism.Uno.WinUI is referenced and using: namespaces are correct. to the project.
- If region navigation throws casting errors when using Syncfusion controls, verify your region adapter maps to the control API rather than inserting UIElements.
- If memory isn't released after view removal, ensure collection handlers are unsubscribed.

## References

- Prism docs: https://prismlibrary.com/docs/wpf/
- Prism GitHub: https://github.com/PrismLibrary/Prism
- Syncfusion + Prism integration patterns: (internal project adapters and official Syncfusion docs)

---

Notes about validation: this ready-reference was created to follow the project's local Copilot guidance and the `docs/.copilot-instructions.md` file. It uses only modern Prism namespaces, recommends pinned versions, and enforces the ThemeUtility rule and region adapter constraints specified by project policy.

## Repository findings (Prism 9.0.537)

These notes were added after inspecting this repository and the Prism 9.0.537 sources/release notes for traceability.

- Confirmed Prism version in this repository: 9.0.537 (see PackageReference / project.assets.json entries). Use this version string when searching the Prism source or pinning packages.
- Prism WPF core types live under a few related namespaces in 9.0.537:
  - Prism (core types and application base classes, e.g. PrismApplicationBase)
  - Prism.Mvvm (ViewModelLocationProvider, BindableBase)
  - Prism.Events (IEventAggregator, PubSubEvent)
  - Prism.Navigation.Regions (RegionAdapterMappings, IRegionAdapter, IRegionBehaviorFactory) — region APIs are scoped under Navigation.Regions in the WPF package
  - Prism.Dialogs (IDialogService, DialogService)

- DryIoc adapter/package notes:
  - NuGet package identifiers may use names like `Prism.Container.DryIoc` while runtime namespaces and types appear under `Prism.DryIoc` / `Prism.DryIoc.Wpf` in the Prism source tree. This can look like a mismatch when updating using directives; prefer to follow the compile-time assemblies referenced by the csproj.

- XAML URI and App bootstrapping guidance (confirmed):
  - Use the modern Prism XAML URI `` in view XAML files.
  - To avoid markup compile / wpftmp issues, prefer the standard WPF `<Application>` root in `App.xaml` while keeping the code-behind inheriting `PrismApplication` (this repository follows that pattern).

- References discovered while validating:
  - Prism GitHub release for the exact tag used: https://github.com/PrismLibrary/Prism/releases/tag/9.0.537
  - Prism WPF source (9.0.537) — notable files: PrismApplicationBase.cs, RegionAdapterMappings.cs, Prism.DryIoc adapter sources under `src/Wpf/Prism.DryIoc.Wpf`

These repository findings are added here so future contributors have a short trace from the code in this repo to the authoritative Prism source for the exact version we depend on.
