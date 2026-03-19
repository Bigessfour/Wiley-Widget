# WileyWidget — AI Briefing

> Generated: 2026-03-18 08:33 | Branch: `main` | Commit: `c3bb154b0b`

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
- `src/WileyWidget.WinForms/Controls/Panels/EnterpriseVitalSignsPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/FormHostPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/InsightFeedPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/PaymentEditPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/ProactiveInsightsPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs` — Panel — priority 100
- `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.Designer.cs` — Panel — priority 100

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
13. `src/WileyWidget.WinForms/Controls/Panels/EnterpriseVitalSignsPanel.cs`
14. `src/WileyWidget.WinForms/Controls/Panels/FormHostPanel.cs`
15. `src/WileyWidget.WinForms/Controls/Panels/InsightFeedPanel.cs`
16. `src/WileyWidget.WinForms/Controls/Panels/PaymentEditPanel.cs`
17. `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs`
18. `src/WileyWidget.WinForms/Controls/Panels/ProactiveInsightsPanel.cs`
19. `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`
20. `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.Designer.cs`

## Architecture Summary

| Component    | Count |
| ------------ | ----- |
| Views        | 19    |
| Viewmodels   | 48    |
| Panels       | 35    |
| Services     | 121   |
| Controls     | 14    |
| Repositories | 31    |
| Factories    | 5     |

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
- `src/WileyWidget.WinForms/ViewModels/DepartmentSummaryViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/EnterpriseVitalSignsViewModel.cs`
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
- `src/WileyWidget.WinForms/ViewModels/IDepartmentSummaryViewModel.cs`
- `src/WileyWidget.WinForms/ViewModels/IEnterpriseVitalSignsViewModel.cs`
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
- `src/WileyWidget.WinForms/Controls/Panels/EnterpriseVitalSignsPanel.cs`
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

- Total files indexed: **8374**
- Files with embedded content: **400**
- Total source size: **466,768 KB**
- Manifest mode: **full-context**

## QuickBooks Desktop Import (Local File)

### Why This Exists

- The production clerk workflow uses a local QuickBooks Desktop company file rather than QuickBooks Online cloud APIs.
- The cloud OAuth path in `src/WileyWidget.Services/QuickBooksService.cs` remains valid for online tenants, but Wiley Widget now also needs a local file import path for desktop exports.

### Official Intuit References

- CSV export/import overview: https://quickbooks.intuit.com/learn-support/en-us/help-article/manage-lists/import-export-csv-files/L9AiGRdT9_US_en_US
- IIF export/import overview: https://quickbooks.intuit.com/learn-support/en-us/help-article/import-export-data-files/export-import-edit-iif-files/L56LT9Z0Q_US_en_US
- IIF format, headers, and sample kit: https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/iif-overview-import-kit-sample-files-headers/L5CZIpJne_US_en_US
- Excel export/import overview: https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/import-export-ms-excel-files/L9BDPsTTX_US_en_US

### Export Steps Summary

- CSV: QuickBooks Desktop can export customer/vendor lists, item lists, and reports to `.csv` from the built-in Excel/export menus.
- IIF: QuickBooks Desktop can export lists and transactions as tab-delimited `.iif` files; Intuit treats IIF as an advanced format with limited validation.
- Excel: QuickBooks Desktop can export lists, reports, and certain transaction sets to Excel workbooks; Wiley Widget treats Excel as a clerk-friendly fallback when CSV is unavailable.
- For exact wording, current menu labels, and screenshots, use the linked Intuit articles instead of this summary.

### Supported Formats

| Format                 | Priority  | Clerk Use                                                   | Wiley Widget Path                                        |
| ---------------------- | --------- | ----------------------------------------------------------- | -------------------------------------------------------- |
| CSV                    | Primary   | Lists and report exports saved from QuickBooks Desktop      | `CsvMappingWizardPanel` + `CsvExcelImportService`        |
| IIF                    | Secondary | Lists or transactions exported in Intuit Interchange Format | New parser feeding the same `DataTable`/mapping pipeline |
| Excel (`.xlsx`/`.xls`) | Fallback  | Clerk exports when Excel is the easiest available route     | Existing `CsvExcelImportService` / `ExcelReaderService`  |

### Wiley Widget Component Mapping

| Need                                   | Existing Surface                                                                                                      | Planned Use                                                                                             |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Clerk file selection and import action | `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`                                                         | Desktop import runs from the existing QuickBooks panel even when the cloud connection is unavailable    |
| Auto-detection and routing             | `src/WileyWidget.Services/QuickBooksDesktopImportService.cs`                                                          | Detect chart-of-accounts, customer, vendor, and payment exports and route them to existing repositories |
| CSV and Excel ingestion                | `src/WileyWidget.Services/CsvExcelImportService.cs`                                                                   | Reuse for local desktop files                                                                           |
| IIF ingestion                          | `src/WileyWidget.Services/QuickBooksDesktopIifParser.cs`                                                              | Normalize QuickBooks Desktop IIF rows into the same table-driven import path                            |
| Import routing into app data           | `src/WileyWidget.Business.Interfaces/*Repository.cs` and `src/WileyWidget.Services/QuickBooksDesktopImportService.cs` | Persist normalized records into existing municipal account, customer, vendor, and payment targets       |
| Progress and status                    | `src/WileyWidget.WinForms/Services/StatusProgressService.cs`                                                          | Surface import progress without blocking the shell                                                      |

### Current Support Boundary

- Implemented local targets: chart of accounts, customers, vendors, payments/check exports.
- Explicitly deferred: QuickBooks item-list imports, because the current codebase does not expose an item repository or item panel target.

### Reading Order Addendum

1. `docs/QuickBooksDesktopImportSpecification.md`
2. `docs/QuickBooksDesktopImport-UserGuide.md`
3. `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`
4. `src/WileyWidget.WinForms/ViewModels/QuickBooksViewModel.cs`
5. `src/WileyWidget.Services/QuickBooksService.cs`
6. `src/WileyWidget.WinForms/Controls/Supporting/CsvMappingWizardPanel.cs`
7. `src/WileyWidget.Services/CsvExcelImportService.cs`

---

> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.
