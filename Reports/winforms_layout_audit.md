# WinForms Layout Conformance Audit

Generated: 2026-03-10T01:37:54.775783+00:00

## Summary

- Total findings: **20**
- Conformance score: **30 / 100**
- Critical: **0**
- High: **0**
- Medium: **15**
- Low: **5**
- Size literals scanned: **139**
- MinimumSize assignments scanned: **22**

## Sizing Inventory

- DockStyle.Fill assignments: **427**
- MaximumSize assignments: **7**
- SplitterWidth assignments: **3**

## MainForm Host Coverage

- MainForm partials scanned: **13**
- MainForm minimum size assignments: **1**
- Right dock minimum size assignments: **1**

## Top Recommendations

### Most Sizing-Dense Files

- `src/WileyWidget.WinForms/Extensions/SyncfusionThemingExtensions.cs`: sizeLiterals=17, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Dialogs/CustomerEditDialog.cs`: sizeLiterals=12, minimumSize=11, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/AccountEditPanel.cs`: sizeLiterals=11, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Dialogs/AccountCreateDialog.cs`: sizeLiterals=7, minimumSize=6, splitterWidth=0
- `src/WileyWidget.WinForms/Dialogs/BudgetEntryEditDialog.cs`: sizeLiterals=7, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs`: sizeLiterals=7, minimumSize=0, splitterWidth=1
- `src/WileyWidget.WinForms/Controls/Panels/SettingsPanel.cs`: sizeLiterals=6, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/PaymentsPanel.cs`: sizeLiterals=4, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/RecommendedMonthlyChargePanel.cs`: sizeLiterals=4, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/RevenueTrendsPanel.cs`: sizeLiterals=4, minimumSize=0, splitterWidth=1

### MainForm Baseline Detected

- Location: `src\WileyWidget.WinForms\Forms\MainForm\MainForm.Navigation.cs:300`
- Size: `350x0`

### [MEDIUM] Panel does not set a minimum size baseline (x12)

- Rule: `panel-minimum-size`
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Form sets Location without StartPosition.Manual (x1)

- Rule: `form-location-without-manual-startposition`
- Recommendation: Add StartPosition = FormStartPosition.Manual where Location is explicitly assigned.
- Source: https://www.syncfusion.com/faq/windowsforms/layout/how-can-i-programmatically-set-the-initial-position-of-a-form-so-that-it-is-displayed

### [MEDIUM] MainForm minimum size is below baseline (x1)

- Rule: `mainform-minimum-size-below-baseline`
- Recommendation: Raise MainForm MinimumSize to at least 1280x800 unless a deliberate smaller baseline is documented.
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel relies heavily on manual pixel layout (x1)

- Rule: `manual-pixel-layout-heavy`
- Recommendation: Move primary composition to TableLayoutPanel/FlowLayoutPanel and keep only exceptional absolute placement.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll (x4)

- Rule: `fixed-layout-without-autoscroll`
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Form does not explicitly set AutoScaleMode.Dpi (x1)

- Rule: `form-autoscale-dpi-missing`
- Recommendation: Set AutoScaleMode = AutoScaleMode.Dpi in form initialization if not designer-managed.
- Source: docs/WileyWidgetUIStandards.md

## Findings

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AccountEditPanel.cs:1`
- Evidence: `class AccountEditPanel : ScopedPanelBase<AccountsViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ActivityLogPanel.cs:1`
- Evidence: `class ActivityLogPanel : ScopedPanelBase<ActivityLogViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:1`
- Evidence: `class AnalyticsHubPanel : ScopedPanelBase<AnalyticsHubViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:1`
- Evidence: `class BudgetOverviewPanel : ScopedPanelBase<BudgetOverviewViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel relies heavily on manual pixel layout

- Rule: `manual-pixel-layout-heavy`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetPanel.cs:1`
- Evidence: `manualLocation=0, manualSize=43, hasLayoutContainer=False`
- Detail: Large volumes of point/size assignments without layout containers increase DPI and resize fragility.
- Recommendation: Move primary composition to TableLayoutPanel/FlowLayoutPanel and keep only exceptional absolute placement.
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\CustomersPanel.cs:1`
- Evidence: `class CustomersPanel : ScopedPanelBase<CustomersViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\DepartmentSummaryPanel.cs:1`
- Evidence: `class DepartmentSummaryPanel : ScopedPanelBase<DepartmentSummaryViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:1`
- Evidence: `class PaymentsPanel : ScopedPanelBase<PaymentsViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:1`
- Evidence: `class RecommendedMonthlyChargePanel : ScopedPanelBase<RecommendedMonthlyChargeViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ReportsPanel.cs:1`
- Evidence: `class ReportsPanel : ScopedPanelBase<ReportsViewModel>, IParameterizedPanel`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\SettingsPanel.cs:1`
- Evidence: `class SettingsPanel : ScopedPanelBase<SettingsViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\UtilityBillPanel.cs:1`
- Evidence: `class UtilityBillPanel : ScopedPanelBase<UtilityBillViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel does not set a minimum size baseline

- Rule: `panel-minimum-size`
- Location: `src\WileyWidget.WinForms\Controls\Panels\WarRoomPanel.cs:1`
- Evidence: `class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>, ICompletablePanel`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] MainForm minimum size is below baseline

- Rule: `mainform-minimum-size-below-baseline`
- Location: `src\WileyWidget.WinForms\Forms\MainForm\MainForm.Navigation.cs:300`
- Evidence: `_rightDockPanel.MinimumSize = new Size(350, 0);`
- Detail: MainForm host sizing baseline should preserve ribbon/content host docking and avoid clipped MDI content.
- Recommendation: Raise MainForm MinimumSize to at least 1280x800 unless a deliberate smaller baseline is documented.
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Form sets Location without StartPosition.Manual

- Rule: `form-location-without-manual-startposition`
- Location: `src\WileyWidget.WinForms\Forms\MsalSignInHostForm.cs:144`
- Evidence: `cancelButton.Location = new Point(footerPanel.Width - cancelButton.Width - 20, 7);`
- Detail: Syncfusion FAQ notes that dialog/form placement should set StartPosition to Manual when Location is set programmatically.
- Recommendation: Add StartPosition = FormStartPosition.Manual where Location is explicitly assigned.
- Source: https://www.syncfusion.com/faq/windowsforms/layout/how-can-i-programmatically-set-the-initial-position-of-a-form-so-that-it-is-displayed

### [LOW] Fixed layout regions detected without AutoScroll

- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:146`
- Evidence: `absoluteStyles=2, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll

- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:704`
- Evidence: `absoluteStyles=3, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll

- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RevenueTrendsPanel.cs:203`
- Evidence: `absoluteStyles=4, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll

- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\WarRoomPanel.cs:108`
- Evidence: `absoluteStyles=3, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Form does not explicitly set AutoScaleMode.Dpi

- Rule: `form-autoscale-dpi-missing`
- Location: `src\WileyWidget.WinForms\Forms\MsalSignInHostForm.cs:1`
- Evidence: `class MsalSignInHostForm : Form`
- Detail: UI standards prefer DPI-aware scaling for modern displays. This may already be set in a designer file.
- Recommendation: Set AutoScaleMode = AutoScaleMode.Dpi in form initialization if not designer-managed.
- Source: docs/WileyWidgetUIStandards.md
