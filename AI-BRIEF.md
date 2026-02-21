# WileyWidget — AI Briefing

> Generated: 2026-02-21 12:04 | Branch: `fix/validation/panels-warroom-customers-20260210` | Commit: `072fa868b5`

## Project Purpose

WileyWidget is a Windows Forms (.NET) application built with the Syncfusion component suite and an MVVM-inspired architecture using ScopedPanelBase panels, ViewModels, and a Syncfusion Ribbon/Docking navigation surface.

## Architecture Patterns

- **MVVM** — ViewModels bind to Panels; panels inherit from `ScopedPanelBase`
- **Syncfusion WinForms** — `SfSkinManager` is the SOLE theme authority (no manual `BackColor`/`ForeColor`)
- **Docking** — `DockingManager` controls panel layout
- **DI** — `Microsoft.Extensions.DependencyInjection` wires all services
- **Ribbon** — `RibbonControlAdv` is the primary navigation surface when `UI:ShowRibbon = true`
- **Async init** — Heavy startup runs via `IAsyncInitializable.InitializeAsync` after `MainForm` is shown

## How to Navigate the Codebase

1. `src/WileyWidget.WinForms/Forms/MainForm.cs` — UI entry point
2. Each panel in `src/WileyWidget.WinForms/` has a matching `*ViewModel.cs`
3. Services live in `src/WileyWidget.Services/`
4. DI wiring is in `Program.cs` and `*ServiceCollectionExtensions.cs` files
5. Syncfusion controls must be created via `SyncfusionControlFactory`

## Critical Files (read these first)

- `src/WileyWidget.WinForms/Controls/Base/ICompletablePanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Base/ScopedPanelBase.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/AccountEditPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/AccountsPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/ActivityLogPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/AnalyticsHubPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.Layout.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/BudgetOverviewPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/BudgetPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/CustomersPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/DepartmentSummaryPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/FormHostPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/InsightFeedPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/PaymentEditPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/ProactiveInsightsPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.Designer.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/RecommendedMonthlyChargePanel.cs` — Panel — priority 100

## Recommended Reading Order

1. `src/WileyWidget.WinForms/Controls/Base/ICompletablePanel.cs`
2. `src/WileyWidget.WinForms/Controls/Base/ScopedPanelBase.cs`
3. `src/WileyWidget.WinForms/Controls/Panels/AccountEditPanel.cs`
4. `src/WileyWidget.WinForms/Controls/Panels/AccountsPanel.cs`
5. `src/WileyWidget.WinForms/Controls/Panels/ActivityLogPanel.cs`
6. `src/WileyWidget.WinForms/Controls/Panels/AnalyticsHubPanel.cs`
7. `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.cs`
8. `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.Layout.cs`
9. `src/WileyWidget.WinForms/Controls/Panels/BudgetOverviewPanel.cs`
10. `src/WileyWidget.WinForms/Controls/Panels/BudgetPanel.cs`
11. `src/WileyWidget.WinForms/Controls/Panels/CustomersPanel.cs`
12. `src/WileyWidget.WinForms/Controls/Panels/DepartmentSummaryPanel.cs`
13. `src/WileyWidget.WinForms/Controls/Panels/FormHostPanel.cs`
14. `src/WileyWidget.WinForms/Controls/Panels/InsightFeedPanel.cs`
15. `src/WileyWidget.WinForms/Controls/Panels/PaymentEditPanel.cs`
16. `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs`
17. `src/WileyWidget.WinForms/Controls/Panels/ProactiveInsightsPanel.cs`
18. `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`
19. `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.Designer.cs`
20. `src/WileyWidget.WinForms/Controls/Panels/RecommendedMonthlyChargePanel.cs`

## Architecture Summary

| Component    | Count |
| ------------ | ----- |
| Views        | 20    |
| Viewmodels   | 48    |
| Panels       | 34    |
| Services     | 118   |
| Controls     | 14    |
| Repositories | 31    |
| Factories    | 7     |

## ViewModels

- `src/WileyWidget.WinForms/Forms/MainViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/AccountsViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/ActivityLogViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/AdvancedScenariosTabViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/AnalyticsHubViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/AnalyticsViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/AuditLogViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/BudgetAnalyticsViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/BudgetOverviewViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/BudgetViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/ChartViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/CustomersViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/DashboardViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/DepartmentSummaryViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IAccountsViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IAdvancedScenariosTabViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IAnalyticsHubViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IAnalyticsViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IAuditLogViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IBudgetAnalyticsViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IBudgetOverviewViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IBudgetViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IChartViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/ICustomersViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IDashboardViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IDepartmentSummaryViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IInsightFeedViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/ILazyLoadViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/InsightFeedViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IQuickBooksViewModel.cs`

## Panels

- `src/WileyWidget.WinForms/Controls/Base/ICompletablePanel.cs`
- `src/WileyWidget.WinForms/Controls/Base/ScopedPanelBase.cs`
- `src/WileyWidget.WinForms/Controls/Panels/AccountEditPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/AccountsPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/ActivityLogPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/AnalyticsHubPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.Layout.cs`
- `src/WileyWidget.WinForms/Controls/Panels/BudgetOverviewPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/BudgetPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/CustomersPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/DepartmentSummaryPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/FormHostPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/InsightFeedPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/PaymentEditPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/ProactiveInsightsPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.Designer.cs`
- `src/WileyWidget.WinForms/Controls/Panels/RecommendedMonthlyChargePanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/ReportsPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/RevenueTrendsPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/SettingsPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/UtilityBillPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/WarRoomPanel.cs`
- `src/WileyWidget.WinForms/Controls/Panels/WarRoomPanel.Designer.cs`
- `src/WileyWidget.WinForms/Controls/Supporting/CsvMappingWizardPanel.cs`
- `src/WileyWidget.WinForms/Controls/Supporting/PanelHeader.cs`
- `src/WileyWidget.WinForms/Forms/RightDockPanelFactory.cs`
- `src/WileyWidget.Models/Models/PanelItem.cs`

## Services

- `src/WileyWidget.Services/ActivityFallbackDataService.cs`
- `src/WileyWidget.Services/AdaptiveTimeoutService.cs`
- `src/WileyWidget.Services/AICacheWarmingService.cs`
- `src/WileyWidget.Services/AILoggingService.cs`
- `src/WileyWidget.Services/AnalyticsService.cs`
- `src/WileyWidget.Services/AnomalyDetectionService.cs`
- `src/WileyWidget.Services/ApplicationMetricsService.cs`
- `src/WileyWidget.Services/AuditService.cs`
- `src/WileyWidget.Services/CacheServiceCollectionExtensions.cs`
- `src/WileyWidget.Services/ChatBridgeService.cs`
- `src/WileyWidget.Services/CorrelationIdService.cs`
- `src/WileyWidget.Services/CsvExcelImportService.cs`
- `src/WileyWidget.Services/DashboardService.cs`
- `src/WileyWidget.Services/DataAnonymizerService.cs`
- `src/WileyWidget.Services/DataPrefetchService.cs`
- `src/WileyWidget.Services/DependencyInjection/WileyWidgetServicesExtensions.cs`
- `src/WileyWidget.Services/DistributedCacheService.cs`
- `src/WileyWidget.Services/DiValidationService.cs`
- `src/WileyWidget.Services/EncryptedLocalSecretVaultService.cs`
- `src/WileyWidget.Services/ErrorReportingService.cs`
- `src/WileyWidget.Services/Excel/ExcelReaderService.cs`
- `src/WileyWidget.Services/Excel/IExcelReaderService.cs`
- `src/WileyWidget.Services/Export/ExcelExportService.cs`
- `src/WileyWidget.Services/FallbackDataService.cs`
- `src/WileyWidget.Services/FastReportService.cs`
- `src/WileyWidget.Services/GlobalSearchService.cs`
- `src/WileyWidget.Services/HealthBasedRoutingService.cs`
- `src/WileyWidget.Services/HealthCheckService.cs`
- `src/WileyWidget.Services/InMemoryCacheService.cs`
- `src/WileyWidget.Services/IntuitDataServiceAdapter.cs`

## Controls

- `src/WileyWidget.WinForms/Controls/Panels/AdvancedScenariosTabControl.cs`
- `src/WileyWidget.WinForms/Controls/Panels/JARVISChatUserControl.cs`
- `src/WileyWidget.WinForms/Controls/Panels/KpiCardControl.cs`
- `src/WileyWidget.WinForms/Controls/Panels/OverviewTabControl.cs`
- `src/WileyWidget.WinForms/Controls/Panels/ScenariosTabControl.cs`
- `src/WileyWidget.WinForms/Controls/Panels/TrendsTabControl.cs`
- `src/WileyWidget.WinForms/Controls/Panels/VariancesTabControl.cs`
- `src/WileyWidget.WinForms/Extensions/ChartControlDefaults.cs`
- `src/WileyWidget.WinForms/Extensions/ChartControlPrinting.cs`
- `src/WileyWidget.WinForms/Extensions/ChartControlRegionEventWiring.cs`
- `src/WileyWidget.WinForms/Extensions/ControlSafeExtensions.cs`
- `src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs`
- `src/WileyWidget.WinForms/Services/RoleBasedAccessControl.cs`
- `src/WileyWidget.WinForms/Utilities/SafeControlSizeValidator.cs`

## Key NuGet Dependencies

## Manifest Stats

- Total files indexed: **1722**
- Files with embedded content: **400**
- Total source size: **42,648 KB**
- Manifest mode: **full-context**

---

> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.
