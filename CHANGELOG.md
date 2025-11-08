# Changelog
All notable changes to this project will be documented in this file.

## [0.2.0] - 2025-10-28
### Major Architecture Cleanup & Standardization

#### Removed
- **CommunityToolkit.Mvvm package** - Conflicted with Prism patterns, removed to standardize on pure Prism
- **7 duplicate/legacy theme files** - Generic.xaml, WileyTheme.xaml, WileyMergedTheme.xaml, Brushes_FluentLight.xaml, FluentDark.xaml, and .txt backups
- **3 obsolete service files** - InteractionRequestService, IInteractionRequestService, Class1.cs stub
- **15 backup files** - All .bak, .old, .backup files from WileyWidget.UI
- **17 stale documentation files** - Completed milestone reports, legacy narratives, outdated plans
- **60+ obsolete scripts** (71% reduction: 84→24 scripts):
  - Syncfusion migration scripts (7 files)
  - Architecture migration scripts (5 files)
  - XAML conversion scripts (6 files)
  - Build error scripts (4 files)
  - Cloudflared/infrastructure scripts (9 files)
  - Production DB scripts (4 files)
  - Test/debug scripts (12 files)
  - Environment setup scripts (8 files)
  - Profile optimization scripts (6 files)
  - Misc utility scripts (10+ files)
- **3 script directories** - archived-scripts/, diagnostics/, testing/

#### Standardized
- **Pure Prism MVVM** - All ViewModels use `BindableBase`, all commands use `DelegateCommand`
- **Single theme source** - `WileyTheme-Syncfusion.xaml` only (FluentDark/FluentLight via SfSkinManager)
- **Prism navigation** - All navigation uses `IRegionManager.RequestNavigate()` exclusively
- **Prism dialogs** - Direct use of `IDialogService` (removed wrapper services)

#### Added
- **LEGACY_CLEANUP_REPORT.md** - Comprehensive report of all code cleanup (28 files deleted)
- **SCRIPTS_CLEANUP_REPORT.md** - Detailed report of scripts cleanup (60+ files deleted)

#### Cleaned
- **Shell.xaml** - Removed commented-out theme references
- **docs/ folder** - Removed 17 obsolete/completed documentation files (51 active docs remain)
- **scripts/ folder** - 71% reduction in script count, only active automation remains

#### Impact
- ✅ Cleaner, more maintainable codebase
- ✅ Single architectural pattern (Prism only)
- ✅ No conflicting MVVM toolkits
- ✅ Reduced confusion from duplicate files
- ✅ Easier onboarding for new developers
- ✅ Faster script/tool discovery

---

## 2025-10-22

### Navigation Refactor: Removed Legacy Navigation Services
- **Removed**: `INavigationService`, `NavigationService`, `IRegionNavigationService`, `RegionNavigationService`, `IScopedRegionService`, `ScopedRegionService`
- **Standardized**: All navigation now uses Prism `IRegionManager.RequestNavigate()` exclusively per Prism documentation
- **Updated**: ViewModels implement `INavigationAware` for lifecycle management (OnNavigatedTo/From)
- **Updated**: Shell.xaml.cs refactored to use `IRegionManager` instead of legacy navigation service
- **Validated**: Region manager properly injected in Bootstrapper; no custom navigation wrappers remain
- **Result**: Cleaner architecture aligned with Prism best practices; eliminated tight coupling and legacy code

### Threading Cleanup Across UI
- Replaced Task.Run wrappers around UI mutations with DispatcherHelper.Invoke/InvokeAsync
- Ensured ObservableCollection updates and selection changes occur on the UI thread
- Removed blocking .Wait()/.Result on UI paths; switched to async/await where applicable
- Retained Task.Run only for CPU/disk-bound export operations
- ViewModels touched: Dashboard, Budget, BudgetAnalysis, AIAssist, Enterprise
- Result: Improved UI responsiveness and reduced deadlock risk; cleaner async patterns end-to-end

---

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
