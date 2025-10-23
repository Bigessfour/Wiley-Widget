# Changelog
All notable changes to this project will be documented in this file.

## [0.1.0] - 2025-08-12
### Added
- Initial WPF scaffold with Syncfusion controls
- MVVM integration
- Unit tests + coverage
- CI workflow & release workflow
- Global exception logging
- Build script (scripts/build.ps1)

## 2025-10-22

### Navigation Refactor: Removed Legacy Navigation Services
- **Removed**: `INavigationService`, `NavigationService`, `IRegionNavigationService`, `RegionNavigationService`, `IScopedRegionService`, `ScopedRegionService`
- **Standardized**: All navigation now uses Prism `IRegionManager.RequestNavigate()` exclusively per Prism documentation
- **Updated**: ViewModels implement `INavigationAware` for lifecycle management (OnNavigatedTo/From)
- **Updated**: Shell.xaml.cs refactored to use `IRegionManager` instead of legacy navigation service
- **Validated**: Region manager properly injected in Bootstrapper; no custom navigation wrappers remain
- **Result**: Cleaner architecture aligned with Prism best practices; eliminated tight coupling and legacy code

### Threading cleanup across UI
- Replaced Task.Run wrappers around UI mutations with DispatcherHelper.Invoke/InvokeAsync.
- Ensured ObservableCollection updates and selection changes occur on the UI thread.
- Removed blocking .Wait()/.Result on UI paths; switched to async/await where applicable.
- Retained Task.Run only for CPU/disk-bound export operations.
- ViewModels touched: Dashboard, Budget, BudgetAnalysis, AIAssist, Enterprise.
- Result: Improved UI responsiveness and reduced deadlock risk; cleaner async patterns end-to-end.
