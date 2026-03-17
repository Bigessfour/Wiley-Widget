# Ship Blocker Audit

Generated: 2026-03-15T20:48:03.647559+00:00

## Summary
- Total findings: **77**
- Critical: **0**
- High: **69**
- Medium: **8**
- Low: **0**
- Ship blocked: **True**

## Findings

### [HIGH] Placeholder implementation in production code
- Category: `implementation-completeness`
- Location: `src\WileyWidget.Services\QuickBooksService.cs:1042`
- Evidence: `// This is a placeholder implementation`
- Detail: Placeholder logic indicates partially developed behavior that may not satisfy intended runtime outcomes.
- Needed development item: Replace placeholder behavior with production implementation and add integration validation for the affected workflow.

### [HIGH] Dummy/simulated external integration call detected
- Category: `integration-readiness`
- Location: `src\WileyWidget.Services\QuickBooksService.cs:1047`
- Evidence: `// Simulate HTTP calls for each budget`
- Detail: Simulation or dummy endpoints in production services indicate incomplete external integration paths.
- Needed development item: Replace simulated HTTP calls with real provider integration, resilient error handling, and verified contract tests.

### [HIGH] Dummy/simulated external integration call detected
- Category: `integration-readiness`
- Location: `src\WileyWidget.Services\QuickBooksService.cs:1055`
- Evidence: `var response = await _httpClient.GetAsync(new Uri("http://dummy"), cancellationToken).ConfigureAwait(false);`
- Detail: Simulation or dummy endpoints in production services indicate incomplete external integration paths.
- Needed development item: Replace simulated HTTP calls with real provider integration, resilient error handling, and verified contract tests.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AccountEditPanel.cs:52`
- Evidence: `public partial class AccountEditPanel : ScopedPanelBase<AccountsViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AccountsPanel.cs:36`
- Evidence: `public partial class AccountsPanel : ScopedPanelBase<AccountsViewModel>, ICompletablePanel`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ActivityLogPanel.cs:42`
- Evidence: `public partial class ActivityLogPanel : ScopedPanelBase<ActivityLogViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:36`
- Evidence: `public partial class AnalyticsHubPanel : ScopedPanelBase<AnalyticsHubViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:526`
- Evidence: `if (_fiscalYearComboBox != null) _fiscalYearComboBox.ThemeName = themeName;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:528`
- Evidence: `if (_searchTextBox != null) _searchTextBox.ThemeName = themeName;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:530`
- Evidence: `if (_refreshButton != null) _refreshButton.ThemeName = themeName;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AnalyticsHubPanel.cs:532`
- Evidence: `if (_exportButton != null) _exportButton.ThemeName = themeName;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\AuditLogPanel.cs:36`
- Evidence: `public partial class AuditLogPanel : ScopedPanelBase<AuditLogViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:250`
- Evidence: `ThemeName = currentTheme,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:269`
- Evidence: `ThemeName = currentTheme`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:288`
- Evidence: `ThemeName = currentTheme`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:377`
- Evidence: `ThemeName = currentTheme,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:58`
- Evidence: `public partial class BudgetOverviewPanel : ScopedPanelBase<BudgetOverviewViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetOverviewPanel.cs:685`
- Evidence: `_lblVariance.ForeColor = ViewModel.TotalVariance >= 0 ? ThemeColors.Error : ThemeColors.Success;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\BudgetPanel.cs:53`
- Evidence: `public partial class BudgetPanel : ScopedPanelBase<BudgetViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\CustomersPanel.cs:46`
- Evidence: `public partial class CustomersPanel : ScopedPanelBase<CustomersViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\DepartmentSummaryPanel.cs:29`
- Evidence: `public partial class DepartmentSummaryPanel : ScopedPanelBase<DepartmentSummaryViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\DepartmentSummaryPanel.cs:465`
- Evidence: `_lblVarianceValue.ForeColor = ViewModel.Variance >= 0 ? ThemeColors.Success : ThemeColors.Error;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\DepartmentSummaryPanel.cs:471`
- Evidence: `_lblOverBudgetCountValue.ForeColor = ThemeColors.Error;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\DepartmentSummaryPanel.cs:477`
- Evidence: `_lblUnderBudgetCountValue.ForeColor = ThemeColors.Success;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\EnterpriseVitalSignsPanel.cs:26`
- Evidence: `public partial class EnterpriseVitalSignsPanel : ScopedPanelBase<EnterpriseVitalSignsViewModel>, ICompletablePanel`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\JARVISChatUserControl.cs:337`
- Evidence: `ForeColor = ThemeColors.Error,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\OverviewTabControl.cs:138`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:31`
- Evidence: `public partial class PaymentEditPanel : ScopedPanelBase<PaymentsViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:316`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:328`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:360`
- Evidence: `combo.ThemeName = themeName;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:374`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:393`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:411`
- Evidence: `combo.ThemeName = themeName;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:428`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:442`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:458`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:471`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:498`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:512`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentEditPanel.cs:526`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:175`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:30`
- Evidence: `public partial class PaymentsPanel : ScopedPanelBase<PaymentsViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:387`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:402`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:418`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:434`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs:449`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ProactiveInsightsPanel.cs:237`
- Evidence: `_btnRefresh.ThemeName = currentTheme;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ProactiveInsightsPanel.cs:255`
- Evidence: `_btnClear.ThemeName = currentTheme;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\QuickBooksPanel.cs:2053`
- Evidence: `_statusLabel.ForeColor = isError ? ThemeColors.Error : Color.Empty;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\QuickBooksPanel.cs:2351`
- Evidence: `_connectionStatusLabel.ForeColor = isConnected ? ThemeColors.Success : ThemeColors.Error;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\QuickBooksPanel.cs:57`
- Evidence: `public partial class QuickBooksPanel : ScopedPanelBase<QuickBooksViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:41`
- Evidence: `public partial class RecommendedMonthlyChargePanel : ScopedPanelBase<RecommendedMonthlyChargeViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:822`
- Evidence: `_overallStatusLabel.ForeColor = ThemeColors.Error;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:826`
- Evidence: `_overallStatusLabel.ForeColor = ThemeColors.Warning;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:830`
- Evidence: `_overallStatusLabel.ForeColor = ThemeColors.Success;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RecommendedMonthlyChargePanel.cs:834`
- Evidence: `_overallStatusLabel.ForeColor = ThemeColors.Warning;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ReportsPanel.cs:47`
- Evidence: `public partial class ReportsPanel : ScopedPanelBase<ReportsViewModel>, IParameterizedPanel`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RevenueTrendsPanel.cs:649`
- Evidence: `_lblGrowthRateValue.ForeColor = ViewModel.GrowthRate >= 0 ? ThemeColors.Success : ThemeColors.Error;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\RevenueTrendsPanel.cs:68`
- Evidence: `public partial class RevenueTrendsPanel : ScopedPanelBase<RevenueTrendsViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ScenariosTabControl.cs:183`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ScenariosTabControl.cs:196`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\ScenariosTabControl.cs:227`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\SettingsPanel.cs:1509`
- Evidence: `_statusLabel.ForeColor = isError ? ThemeColors.Error : Color.Empty;`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\SettingsPanel.cs:66`
- Evidence: `public partial class SettingsPanel : ScopedPanelBase<SettingsViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\UtilityBillPanel.cs:45`
- Evidence: `public partial class UtilityBillPanel : ScopedPanelBase<UtilityBillViewModel>`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [HIGH] Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)
- Category: `theming-compliance`
- Location: `src\WileyWidget.WinForms\Controls\Panels\VariancesTabControl.cs:152`
- Evidence: `ThemeName = themeName,`
- Detail: Syncfusion best practice requires SfSkinManager to remain the primary theming authority.
- Needed development item: Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.

### [HIGH] Panel missing AccessibleName or Role (Microsoft accessibility requirement)
- Category: `accessibility`
- Location: `src\WileyWidget.WinForms\Controls\Panels\WarRoomPanel.cs:23`
- Evidence: `public partial class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>, ICompletablePanel`
- Detail: All controls must expose automation properties. Detected missing metadata: AccessibleRole
- Needed development item: Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Configuration\UIConfiguration.cs:144`
- Evidence: `UseSyncfusionDocking = GetBooleanWithAliases(configuration, true, "UI:UseSyncfusionDocking", "UI:UseDockingManager"),`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Extensions\SyncfusionExtensions.cs:16`
- Evidence: `this DockingManager? dockingManager,`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Extensions\SyncfusionThemingExtensions.cs:104`
- Evidence: `this DockingManager? dockingManager,`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Factories\SyncfusionControlFactory.cs:416`
- Evidence: `#region DockingManager`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Forms\MainForm\MainForm.DocumentManagement.cs:51`
- Evidence: `_tabbedMdi = new TabbedMDIManager`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Initialization\StartupInstrumentation.cs:201`
- Evidence: `public static bool ValidateDockingManagerCreation(`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Program.cs:285`
- Evidence: `e.Exception.StackTrace?.Contains("Syncfusion.Windows.Forms.Tools.DockingManager.HostControl_Paint", StringComparison.OrdinalIgnoreCase) == true)`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.

### [MEDIUM] Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)
- Category: `layout-stability`
- Location: `src\WileyWidget.WinForms\Services\TestDockingManagerStub.cs:13`
- Evidence: `public class TestDockingManagerStub : DockingManager`
- Detail: This file uses docking manager patterns but no explicit layout synchronization call was found.
- Needed development item: Add PerformLayout or TriggerForceFullLayout after major docking state changes.
