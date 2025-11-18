# Focus-On-Load Behavior Rollout

This checklist tracks enabling consistent keyboard focus on view load using `WileyWidget.Behaviors.FocusOnLoadBehavior`.

How to enable:

- Add xmlns:behaviors="clr-namespace:WileyWidget.Behaviors" to the view XAML root.
- Add behaviors:FocusOnLoadBehavior.IsEnabled="True" to the appropriate root element.
- Optionally set behaviors:FocusOnLoadBehavior.TargetName="<ElementName>" to focus a specific control.
- Add a hidden `<Border x:Name="<ElementName>" Focusable="True" Visibility="Collapsed" />` as a stable anchor if needed.

Status

- [x] DashboardView.xaml
- [x] ReportsView.xaml
- [x] EnterpriseView.xaml
- [x] MunicipalAccountView.xaml
- [x] BudgetView.xaml
- [x] AnalyticsView.xaml
- [x] SettingsView.xaml
- [x] UtilityCustomerView.xaml
- [x] ExcelImportView.xaml
- [x] ProgressView.xaml
- [ ] Dialogs (ConfirmationDialogView, ErrorDialogView, NotificationDialogView)
- [ ] Panels (DashboardPanelView, EnterprisePanelView, MunicipalAccountPanelView, SettingsPanelView, ToolsPanelView)

Notes

- Behavior also attempts Window.Activate() to bring the window forward when allowed by the OS.
- Prefer focusing the first interactive element in the view when obvious (e.g., a search box).
