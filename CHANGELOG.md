# Changelog
All notable changes to this project will be documented in this file.

## [0.3.0] - 2025-11-08
### Added
- AI manifest schema (`schemas/ai-manifest-schema.json`) with validation guidance
- `.ai-manifest-config.json.example` for customizable manifest generation
- Reference guide `docs/reference/AI_FETCHABLE_MANIFEST_ENHANCEMENTS.md`
- **Comprehensive NuGet Package Resolution System** - Three-layer defense-in-depth approach:
  - **Build Layer**: `CopyLocalLockFileAssemblies=true` ensures all NuGet assemblies copy to output directory
  - **MSBuild Layer**: `GeneratePathProperty=true` on Prism packages provides MSBuild path variables
  - **Runtime Layer**: `AppDomain.AssemblyResolve` event handler with intelligent probing for fallback resolution
  - Assembly resolution cache using `ConcurrentDictionary` for performance
  - Whitelisted package resolution (Prism, DryIoc, Syncfusion, etc.) to avoid interfering with system assemblies
  - Multi-path probing: bin folder → probe paths → NuGet global cache
  - Comprehensive logging for troubleshooting assembly load issues

### Changed
- Upgraded Syncfusion WPF controls to 31.2.5 and BoldReports WPF to 11.1.18
- Updated Serilog.Sinks.File to 7.0.0 and FluentValidation to 12.1.0
- Raised Microsoft.Extensions.Http.Resilience to 9.10.0 and QuickBooks SDK to 14.7.0.2
- Bumped OpenTelemetry packages to the 1.13.x line for instrumentation parity
- Expanded `generate_repo_urls.py` with dependency graphs, git history, license detection, security scans, and metrics

### Fixed
- **Resolved NuGet Package Reference Issues**: Eliminated CS0246/CS0115 errors from wpftmp compilation
- **Assembly Loading Reliability**: Prevented runtime `FileNotFoundException` and `TypeLoadException` for NuGet packages
- **Prism Type Resolution**: Fixed PrismApplication and DryIoc container resolution in various environments

### Documentation
- Refreshed README with November 2025 release highlights and AI manifest usage
- Documented manifest workflow in reference docs and linked sample configuration
- Added comprehensive NuGet resolution documentation with implementation details and troubleshooting guide

---

## [0.2.0] - 2025-10-28
### Major Architecture Cleanup & Standardization

#### Removed
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

#### Standardized

#### Added

#### Cleaned

#### Impact

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
