# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0-winforms] - 2025-11-25

### Repository Consolidation (November 25, 2025)

- **Branch Merge**: Merged `upgrade-to-NET10` branch into `main` - consolidated .NET 9 work
- **Build Artifact Cleanup**: Removed diagnostic logs (67MB+) and build artifacts
- **CI/CD Enhancement**: Updated GitHub Actions workflow with path filtering and unique artifact naming
- **Tagged Release**: Created `v1.0-winforms-stable` tag marking stable WinForms production baseline

#### Removed

- **Legacy Folders**: Cleaned up `archive/`, `temp/`, `temp_test/`, `src/WileyWidget.Legacy/`

#### Changed

- **Solution Structure**: Updated `WileyWidget.sln` to focus on WinForms mainline + shared libraries
- **README.md**: Updated with WinForms implementation details
- **Package Management**: Maintained .NET 9 packages, prepared for Syncfusion WinForms integration
- **GitHub Workflow**: Enhanced `build-winforms.yml` with smart path filtering (skip docs-only changes)

#### Documentation

- **Migration Plan**: See `docs/syncfusion-winforms-migration.md` for WinForms implementation roadmap


**Tag:** `v1.0-winforms-relaunch`

---

## [0.4.0] - 2025-11-09

### Major Bootstrapper Refactor - Phase 0-1 Complete

#### Removed (Phase 0: Dead Code Cleanup)

- **11 Dead Modules**: Deleted DashboardModule, MunicipalAccountModule, PanelModule, ReportsModule, ToolsModule, ThrowingModule, UtilityCustomerModule, AIAssistModule, BudgetModule, EnterpriseModule, SettingsModule
- **Active Modules**: Only CoreModule and QuickBooksModule remain (2 of original 16)
- **Dead Services**: Removed XamlDiagnosticsService, HealthCheckHostedService
- **Dead Files**: Deleted Bootstrapper.cs (825 LOC), App.Tmp.cs, HttpClientFactory.cs, CustomModuleManager.cs
- **TMP Support**: Removed all conditional compilation blocks (#if TMP)
- **IUnitOfWork Pattern**: Completely removed (zero references) - repositories now injected directly
- **Test Directory**: Deleted entire tests/ directory (811 files, 0% coverage) for fresh rebuild
- **Unused Methods**: Removed LoadApplicationResourcesEnterpriseAsync, GetModuleInitializationMode, NavigateToMinimalViewFallback
- **LOC Reduction**: ~12,000+ lines removed

#### Added (Phase 1: Critical Fixes & Partial Class Split)

- **Partial Class Structure**: Split App.xaml.cs into 6 maintainable files (~2,000 LOC total):
  - `App.xaml.cs` (555 LOC): Main entry, assembly resolution, helpers
  - `App.DependencyInjection.cs` (749 LOC): DI container, config, registration methods
  - `App.Lifecycle.cs` (656 LOC): OnStartup, OnInitialized, OnExit, shell creation
  - `App.Telemetry.cs`: SigNoz telemetry, metrics, distributed tracing
  - `App.Resources.cs`: Resource loading, theme management (VerifyAndApplyTheme)
  - `App.ExceptionHandling.cs`: Global exception handlers, error reporting
- **Full Stub Implementations**:
  - RegisterConventionTypes: Infrastructure, repositories, services, ViewModels
  - RegisterLazyAIServices: AI service registration with XAI/NullAIService fallback
  - ValidateAndRegisterViewModels: SettingsViewModel dependency validation
  - RegisterCoreInfrastructure: IConfiguration, IMemoryCache, ILoggerFactory, IHttpClientFactory
- **Theme Race Condition Fix**:
  - VerifyAndApplyTheme() called in OnStartup BEFORE base.OnStartup() (Phase 1)
  - Fail-fast exception in ConfigureRegionAdapterMappings if theme null
  - Memory check (128MB minimum) before theme application
- **Configuration Caching**: BuildConfiguration() now caches IConfiguration to eliminate duplicate calls

#### Updated

- **Module Registration**: Hardcoded CoreModule and QuickBooksModule in ConfigureModuleCatalog (no longer config-driven)
- **Configuration Cleanup**: appsettings.json Modules:Order and Modules:Regions reduced to 2 active modules
- **Bootstrapper Inlining**: Moved config/logging/HTTP setup from Bootstrapper.cs into App.DependencyInjection.cs
- **LOC Reduction**: App.xaml.cs reduced from 1,835 LOC (monolithic) to 555 LOC (main partial) + 5 supporting partials
- **Module Count**: 16 → 2 active modules (87.5% reduction)
- **ViewModel Count**: Only SettingsViewModel active per manifest analysis

#### Issues Fixed

- **Stale Module References**: Removed 10 deleted module names from appsettings.json (DashboardModule, etc.)
- **Unused Config Loading**: Eliminated ModuleOrder/ModuleRegionMap config loading (modules now hardcoded)
- **DI Registration Issues**: Fixed HealthCheckHostedService stale registration (2nd order effect)
- **Theme Timing**: Syncfusion theme now applied before region adapter registration (eliminates race condition)
- **Build Errors**: Resolved TMP-related compilation errors through complete removal of conditional support

#### Docs Updated

- **Partial Class Navigation**: Added comprehensive structure map in App.xaml.cs header (developer reference)
- **Audit Report**: Created BOOTSTRAPPER_AUDIT_2025-11-09.md with phase completion status
- **README Update**: Scrubbed legacy items, documented new bootstrapper architecture

#### Validation Results

- ✅ Zero compilation errors in core App files
- ✅ All Phase 0-1 acceptance criteria met
- ✅ Theme registration aligned with Syncfusion docs
- ✅ auto-catalog pattern implemented for 2 active modules
- ✅ DI container resolution validated via C# MCP testing
- ✅ SettingsViewModel fully resolves with all dependencies

#### Performance Impact

- **Startup Simplification**: Reduced from 12-module to 2-module initialization
- **Memory Footprint**: Smaller surface area from deleted services/modules
- **Configuration Overhead**: Eliminated duplicate BuildConfiguration() calls via caching

---

## [0.3.0] - 2025-11-08
### Added
- AI manifest schema (`schemas/ai-manifest-schema.json`) with validation guidance
- `.ai-manifest-config.json.example` for customizable manifest generation
- Reference guide `docs/reference/AI_FETCHABLE_MANIFEST_ENHANCEMENTS.md`
- **Comprehensive NuGet Package Resolution System** - Three-layer defense-in-depth approach:
  - **Build Layer**: `CopyLocalLockFileAssemblies=true` ensures all NuGet assemblies copy to output directory
  - **MSBuild Layer**: `GeneratePathProperty=true` on packages provides MSBuild path variables
  - **Runtime Layer**: `AppDomain.AssemblyResolve` event handler with intelligent probing for fallback resolution
  - Assembly resolution cache using `ConcurrentDictionary` for performance
  - Whitelisted package resolution (DryIoc, Syncfusion, etc.) to avoid interfering with system assemblies
  - Multi-path probing: bin folder → probe paths → NuGet global cache
  - Comprehensive logging for troubleshooting assembly load issues

### Updated

- Upgraded Syncfusion controls to 31.2.5 and BoldReports WPF to 11.1.18
- Updated Serilog.Sinks.File to 7.0.0 and FluentValidation to 12.1.0
- Raised Microsoft.Extensions.Http.Resilience to 9.10.0 and QuickBooks SDK to 14.7.0.2
- Bumped OpenTelemetry packages to the 1.13.x line for instrumentation parity
- Expanded `generate_repo_urls.py` with dependency graphs, git history, license detection, security scans, and metrics

### Package Issues Fixed

- **Resolved NuGet Package Reference Issues**: Eliminated CS0246/CS0115 errors from tmp compilation
- **Assembly Loading Reliability**: Prevented runtime `FileNotFoundException` and `TypeLoadException` for NuGet packages
- **Type Resolution**: Fixed Application and DryIoc container resolution in various environments

### Documentation Updates

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
- **Standardized**: All navigation now uses `IRegionManager.RequestNavigate()` exclusively per documentation
- **Updated**: ViewModels implement `INavigationAware` for lifecycle management (OnNavigatedTo/From)
- **Updated**: Shell.xaml.cs refactored to use `IRegionManager` instead of legacy navigation service
- **Validated**: Region manager properly injected in Bootstrapper; no custom navigation wrappers remain
- **Result**: Cleaner architecture aligned with best practices; eliminated tight coupling and legacy code

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

- Initial scaffold with Syncfusion controls
- MVVM integration
- Unit tests + coverage
- CI workflow & release workflow
- Global exception logging
- Build script (scripts/build.ps1)
