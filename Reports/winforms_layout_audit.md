# WinForms Layout Conformance Audit

Generated: 2026-03-13T21:44:57.992736+00:00

## Summary
- Total findings: **16**
- Conformance score: **54 / 100**
- Critical: **0**
- High: **0**
- Medium: **7**
- Low: **9**
- Size literals scanned: **138**
- MinimumSize assignments scanned: **22**

## Sizing Inventory
- DockStyle.Fill assignments: **457**
- MaximumSize assignments: **7**
- SplitterWidth assignments: **4**

## MainForm Host Coverage
- MainForm partials scanned: **13**
- MainForm minimum size assignments: **1**
- Right dock minimum size assignments: **1**

## Top Recommendations

### Most Sizing-Dense Files

- `src/WileyWidget.WinForms/Extensions/SyncfusionThemingExtensions.cs`: sizeLiterals=17, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Dialogs/CustomerEditDialog.cs`: sizeLiterals=12, minimumSize=11, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/AccountEditPanel.cs`: sizeLiterals=11, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/RecommendedMonthlyChargePanel.cs`: sizeLiterals=8, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Dialogs/AccountCreateDialog.cs`: sizeLiterals=7, minimumSize=6, splitterWidth=0
- `src/WileyWidget.WinForms/Dialogs/BudgetEntryEditDialog.cs`: sizeLiterals=7, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs`: sizeLiterals=7, minimumSize=0, splitterWidth=1
- `src/WileyWidget.WinForms/Controls/Panels/SettingsPanel.cs`: sizeLiterals=6, minimumSize=0, splitterWidth=0
- `src/WileyWidget.WinForms/Controls/Panels/RevenueTrendsPanel.cs`: sizeLiterals=4, minimumSize=0, splitterWidth=1
- `src/WileyWidget.WinForms/Controls/Panels/WarRoomPanel.cs`: sizeLiterals=4, minimumSize=0, splitterWidth=0

### MainForm Baseline Detected

- Location: `src\WileyWidget.WinForms\Forms\MainForm\MainForm.Navigation.cs:304`
- Size: `350x0`

### [MEDIUM] Panel does not set a minimum size baseline (x4)
- Rule: `panel-minimum-size`
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] Panel relies heavily on manual pixel layout (x2)
- Rule: `manual-pixel-layout-heavy`
- Recommendation: Move primary composition to TableLayoutPanel/FlowLayoutPanel and keep only exceptional absolute placement.
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] MainForm minimum size is below baseline (x1)
- Rule: `mainform-minimum-size-below-baseline`
- Recommendation: Raise MainForm MinimumSize to at least 1280x800 unless a deliberate smaller baseline is documented.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll (x9)
- Rule: `fixed-layout-without-autoscroll`
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

## Findings

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
- Evidence: `manualLocation=0, manualSize=46, hasLayoutContainer=False`
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

### [MEDIUM] Panel relies heavily on manual pixel layout
- Rule: `manual-pixel-layout-heavy`
- Location: `src\WileyWidget.WinForms\Controls\Panels\InsightFeedPanel.cs:1`
- Evidence: `manualLocation=0, manualSize=14, hasLayoutContainer=False`
- Detail: Large volumes of point/size assignments without layout containers increase DPI and resize fragility.
- Recommendation: Move primary composition to TableLayoutPanel/FlowLayoutPanel and keep only exceptional absolute placement.
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
- Location: `src\WileyWidget.WinForms\Controls\Panels\UtilityBillPanel.cs:1`
- Evidence: `class UtilityBillPanel : ScopedPanelBase<UtilityBillViewModel>`
- Detail: Panel standards define minimum logical dimensions to reduce clipped content and maintain predictable resizing behavior.
- Recommendation: Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded).
- Source: docs/WileyWidgetUIStandards.md

### [MEDIUM] MainForm minimum size is below baseline
- Rule: `mainform-minimum-size-below-baseline`
- Location: `src\WileyWidget.WinForms\Forms\MainForm\MainForm.Navigation.cs:304`
- Evidence: `_rightDockPanel.MinimumSize = new Size(350, 0);`
- Detail: MainForm host sizing baseline should preserve ribbon/content host docking and avoid clipped MDI content.
- Recommendation: Raise MainForm MinimumSize to at least 1280x800 unless a deliberate smaller baseline is documented.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AccountsPanel.cs:191`
- Evidence: `absoluteStyles=1, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ActivityLogPanel.cs:173`
- Evidence: `absoluteStyles=1, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:167`
- Evidence: `absoluteStyles=5, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\LocalIdentityHostPanel.cs:87`
- Evidence: `absoluteStyles=2, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:173`
- Evidence: `absoluteStyles=5, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:758`
- Evidence: `absoluteStyles=3, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ReportsPanel.cs:314`
- Evidence: `absoluteStyles=1, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RevenueTrendsPanel.cs:205`
- Evidence: `absoluteStyles=4, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md

### [LOW] Fixed layout regions detected without AutoScroll
- Rule: `fixed-layout-without-autoscroll`
- Location: `src\WileyWidget.WinForms\Controls\Panels\WarRoomPanel.cs:120`
- Evidence: `absoluteStyles=3, autoScroll=False`
- Detail: Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts.
- Recommendation: Set AutoScroll = true on the hosting panel when fixed-width regions are present.
- Source: docs/WileyWidgetUIStandards.md
