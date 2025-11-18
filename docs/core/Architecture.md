# Architecture Summary

Wiley Widget is a .NET 9 WinUI 3 desktop application that layers Prism 9 on top of the MVVM pattern. The application shell lives in `src/WileyWidget.WinUI` and composes feature modules from sibling projects under `src/`.

## Key Points

- **Frameworks**: Prism 9, DryIoc container, WinUI 3 with Syncfusion WinUI components (https://help.syncfusion.com/winui/overview)
- **Structure**: UI composition happens through Prism regions; modules register their views and services during bootstrapping
- **Layering**: UI (`WileyWidget.UI`), services (`WileyWidget.Services` + `.Abstractions`), domain models (`WileyWidget.Models`), persistence (`WileyWidget.Data`), and cross-cutting business logic (`WileyWidget.Business`)
- **Configuration**: JSON-based settings loaded via `SettingsService`; secrets supplied via environment variables or external configuration
- **Navigation**: Region navigation and dialogs use `IRegionManager` and `IDialogService`

Refer to the [full architecture reference](../reference/ARCHITECTURE.md) for diagrams, module breakdowns, and advanced topics such as performance tuning and dependency injection details.
