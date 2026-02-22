# WileyWidget UI Consistency Rollout Checklist

This checklist operationalizes `docs/WileyWidgetUIStandards.md` across all user-facing WinForms surfaces.

## Rollout Method

1. **Baseline Audit**: Review each panel/form against the canonical standards.
2. **Standardization Pass**: Apply panel skeleton, header, sizing, feedback, tooltips, and keyboard rules.
3. **Syncfusion Compliance Pass**: Validate factory usage, `SfSkinManager` authority, and `ThemeName` consistency.
4. **Validation Pass**: Run build + targeted tests + UI layout checks.
5. **Sign-off**: Mark the item complete only after all checks pass.

## Required Checks per Surface

- [ ] Inherits/uses `ScopedPanelBase` lifecycle pattern and `SafeSuspendAndLayout`.
- [ ] Includes `PanelHeader` with consistent refresh/help/close behavior where applicable.
- [ ] Uses sensible `MinimumSize` and resize-safe layout (no clipping/cutoff).
- [ ] Uses non-disruptive feedback (`IsBusy`, overlay/status text, no surprise modal interruptions).
- [ ] Primary actions are clear and first-class (verb-first labels, keyboard shortcuts).
- [ ] All primary controls include tooltips and accessible names.
- [ ] Uses approved icons/resources only.
- [ ] No competing color/theme systems; `SfSkinManager` remains authoritative.
- [ ] Syncfusion controls are created via `SyncfusionControlFactory` unless documented exception applies.
- [ ] Syncfusion API usage validated against current docs/samples for modified controls.

## User-Facing Panels Audit Queue

### High Priority (Core operations)

- [x] `src/WileyWidget.WinForms/Controls/Panels/EnterpriseVitalSignsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/WarRoomPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/PaymentEditPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/BudgetPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/BudgetOverviewPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/AccountsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/AccountEditPanel.cs`

### Medium Priority (Navigation and analytics)

- [x] `src/WileyWidget.WinForms/Controls/Panels/AnalyticsHubPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/ProactiveInsightsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/InsightFeedPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/RevenueTrendsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/ReportsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/CustomersPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/SettingsPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`

### Remaining Panels / Supporting UI

- [x] `src/WileyWidget.WinForms/Controls/Panels/ActivityLogPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/AuditLogPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/DepartmentSummaryPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/UtilityBillPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/JARVISChatUserControl.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/FormHostPanel.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/KpiCardControl.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/RecommendedMonthlyChargePanel.cs`

### Tab/Composite Surfaces

- [x] `src/WileyWidget.WinForms/Controls/Panels/OverviewTabControl.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/TrendsTabControl.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/VariancesTabControl.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/ScenariosTabControl.cs`
- [x] `src/WileyWidget.WinForms/Controls/Panels/AdvancedScenariosTabControl.cs`

## Forms / Shell Surfaces

- [x] `src/WileyWidget.WinForms/Forms/MainForm.Helpers.cs`
- [x] `src/WileyWidget.WinForms/Forms/RatesPage.cs`
- [x] `src/WileyWidget.WinForms/Forms/SplashForm.cs`

## Governance Notes

- Treat `docs/WileyWidgetUIStandards.md` as the source of truth for UI design and behavior.
- When a rule cannot be applied due to technical constraints, document the exception inline in PR notes.
- For each merged UI change, update this checklist with completion status.
