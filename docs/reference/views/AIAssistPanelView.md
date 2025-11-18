# AIAssistPanelView — Documentation

Date: 2025-10-19

## Purpose

A dockable AI assistant panel for municipal utility management and financial planning. It provides chat-based interactions and quick calculators (service charge, what-if scenarios, proactive insights).

## Key files

- View: `src/Views/AIAssistPanelView.xaml`
- Code-behind: `src/Views/AIAssistPanelView.xaml.cs`
- ViewModel: `src/ViewModels/AIAssistViewModel.cs`

## Bindings

- SfAIAssistView
  - `CurrentUser` ← `AIAssistViewModel.CurrentUser`
  - `Messages` ← `AIAssistViewModel.Messages` (alias of `ChatMessages`)
- Input
  - `MessageText` (TwoWay, PropertyChanged)
  - `IsInputValid`, `InputValidationError`, `StatusMessage`, `IsTyping`, `IsProcessing`
- Financial inputs (when applicable)
  - `AnnualExpenses`, `TargetReservePercentage`, `WhatIfScenario`, `WhatIfVariable`, `WhatIfNewValue`, `ProactiveAlertThreshold`, `EnableProactiveMonitoring`

## Commands

- `SendMessageCommand`, `SendCommand`, `GenerateCommand`, `ClearChatCommand`, `ExportChatCommand`,
  `ConfigureAICommand`, `CalculateServiceChargeCommand`, `GenerateWhatIfScenarioCommand`,
  `GetProactiveAdviceCommand`, `RefreshLiveDataCommand`, `SetConversationModeCommand`, `CancelCommand`
- Keyboard shortcuts: Ctrl+Enter, Alt+S (send), Alt+R (refresh)

## Theming

- Uses DynamicResource brushes from `Themes/WileyTheme.xaml`: `PanelBackgroundBrush`, `CardBorderBrush`, `ErrorBrush`, `InfoBrush`, `SecondaryTextBrush`.
- No per-view Syncfusion `VisualStyle` override; relies on global `SfSkinManager` application theme.

## Accessibility

- AutomationProperties set for inputs, status/error, suggestions, and primary actions.
- Access keys added to primary buttons (e.g., “\_Send Message”, “Try \_Again”).

## Performance

- DEBUG-only Stopwatch instrumentation wraps the `SendMessage` pipeline and logs elapsed time.

## Tests

- Theme and behavior tests: `tests/WileyWidget.LifecycleTests/AIAssistViewThemeAndBehaviorTests.cs`
- Lifecycle/analytics test: `tests/WileyWidget.LifecycleTests/AIAssistLifecycleTests.cs`

## Pinned references

- Prism Event Aggregator/Regions: https://prismlibrary.github.io/docs/event-aggregator.html
- WPF Data Binding overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview
- WPF Commanding overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview
- Syncfusion WPF (API reference root): https://help.syncfusion.com/cr/windowsforms/Syncfusion.html
- Syncfusion WPF DataGrid getting started (example pattern): https://help.syncfusion.com/wpf/datagrid/getting-started
- Syncfusion SfBusyIndicator (WPF): https://help.syncfusion.com/wpf/busy-indicator/overview
- Syncfusion SfChat/SfAIAssistView (WPF): refer to your installed version’s docs/sample browser

## Notes

- Follow the WPF View Completion Checklist for evidence and scoring.
- Prefer documented Syncfusion APIs only; avoid invented members.
