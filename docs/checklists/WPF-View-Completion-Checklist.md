# WPF View Completion Checklist (Prism + Syncfusion)

This checklist consolidates Microsoft WPF guidance, Prism patterns, and Syncfusion WPF control practices into a single, practical template you can use to judge when a view is truly “complete.” Each item is intentionally concrete and verifiable. Use it during development, code review, and QA sign‑off.

Sources (selected):
- Microsoft WPF: Data binding overview, data templating, validation, commanding, layout
  - https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview
  - https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview
- Prism: Event Aggregator (and related WPF docs)
  - https://prismlibrary.github.io/docs/event-aggregator.html
- Syncfusion WPF (example control docs; use the control’s official documentation for specifics)
  - DataGrid Getting Started: https://help.syncfusion.com/wpf/datagrid/getting-started
  - Themes overview and licensing: refer to the official Syncfusion WPF docs for your version

> Note: For each Syncfusion control you use (SfDataGrid, Ribbon, SfAIAssistView, etc.), review that control’s “Getting Started,” API reference, and theming pages in the official docs and validate usage against them.

---

## How to use this checklist

- Work top‑down. Mark each checkbox and attach “Evidence” (file/line, PR link, screenshot) where applicable.
- If an item is N/A, mark it as such and add a one‑liner why.
- Use the scoring rubric at the end to compute a completion percentage per view.
- In PRs, paste the per‑view template, compute and include the score, and attach evidence links. CI can parse this
  section and enforce thresholds automatically.
- Avoid double‑counting: when an item appears to span multiple sections (e.g., EventAggregator cleanup and Lifecycle),
  score it once in the most relevant section and reference it from others as needed.
- Version pinning: when citing control documentation, reference the version‑specific page for your Syncfusion/WPF/Prism
  versions to ensure API parity.

---

## 1) Architecture & MVVM wiring (Prism)

- [ ] View and ViewModel exist as a pair with MVVM separation (no business logic in code‑behind)  
  Evidence:
- [ ] ViewModel is resolved by Prism ViewModelLocator (AutoWireViewModel=True or explicit registration)  
  Evidence:
- [ ] All dependencies of the ViewModel are container‑resolvable (IContainerRegistry registrations exist)  
  Evidence:
- [ ] Module registers the view for navigation/region usage (RegisterForNavigation or explicit RegionManager registration)  
  Evidence:
- [ ] View placed into the correct region by RegionManager (RegionName constant, no typos)  
  Evidence:
- [ ] Uses INavigationAware/IConfirmNavigationRequest as needed to load/unload data and guard navigation  
  Evidence:
- [ ] EventAggregator usage: subscriptions are scoped appropriately, use ThreadOption.UIThread when updating UI, and are unsubscribed/cleaned up  
  Evidence:
- [ ] Long‑lived resources (timers, streams, file watchers) are owned by the ViewModel and disposed deterministically  
  Evidence:

## 2) Data Binding fundamentals

- [ ] DataContext flows via ViewModelLocator or is explicitly set; no conflicting DataContext on inner elements  
  Evidence:
- [ ] Bindings use correct Mode (OneWay/TwoWay/OneTime/OneWayToSource) per scenario  
  Evidence:
- [ ] UpdateSourceTrigger is appropriate (e.g., LostFocus for TextBox unless immediate updates are needed)  
  Evidence:
- [ ] ViewModel implements INotifyPropertyChanged; collections implement ObservableCollection<T> or INotifyCollectionChanged  
  Evidence:
- [ ] Binding paths are valid; binding errors do not appear at runtime (verify via Output window or PresentationTraceSources when needed)  
  Evidence:
- [ ] Value converters are used when default type conversion is insufficient; converters are unit tested  
  Evidence:
- [ ] Data templates are used to shape complex items; no ToString() fallback visuals  
  Evidence:
- [ ] Collection views (CollectionViewSource) are used for sort/filter/group when needed; current item behavior is correct  
  Evidence:

## 3) Commands, input, and gestures

- [ ] All user actions are ICommand‑backed (e.g., Prism DelegateCommand) with CanExecute and dynamic requerying  
  Evidence:
- [ ] Keyboard gestures or InputBindings are provided for high‑value actions (e.g., Ctrl+Enter to send)  
  Evidence:
- [ ] CommandTargets are set when needed (for RoutedCommand); otherwise ICommand is bound to the VM  
  Evidence:
- [ ] Access keys and accelerator text provided in menus/buttons where applicable  
  Evidence:
- [ ] Focus management on open/close/navigation (initial focus, returning focus after dialogs)  
  Evidence:

## 4) Validation and error UX

- [ ] Input validation implemented using one of: ValidationRules, IDataErrorInfo, or INotifyDataErrorInfo  
  Evidence:
- [ ] ErrorTemplate or visual cues for invalid inputs; tooltips or inline messages show Validation.Errors content  
  Evidence:
- [ ] UpdateSourceTrigger selected to match validation strategy (PropertyChanged vs LostFocus vs Explicit)  
  Evidence:
- [ ] Cross‑field/business‑rule validation (e.g., date ranges) implemented and surfaced to the user  
  Evidence:
- [ ] Async validation (if applicable) debounced and cancellable; does not freeze the UI  
  Evidence:
- [ ] Primary action (Save/Submit) disabled when invalid  
  Evidence:

## 5) UX, design, and theming

- [ ] View aligns with app style guide (colors, typography, spacing, component usage) via ResourceDictionaries  
  Evidence:
- [ ] Theme applied consistently (Syncfusion SfSkinManager or global theme) and is discoverable by users if theme switching is supported  
  Evidence:
- [ ] Clear visual hierarchy; empty states, loading states, and error states are designed  
  Evidence:
- [ ] Busy/Progress indicators are used during long operations; prevents double‑submit  
  Evidence:
- [ ] Microcopy is concise and consistent with the domain (terms, labels)  
  Evidence:

## 6) Accessibility (A11y)

- [ ] Tab order is logical; full keyboard operation (no traps); Esc/Enter behave consistently  
  Evidence:
- [ ] Focus visuals present and visible; programmatic focus set on critical flows  
  Evidence:
- [ ] AutomationProperties.Name/HelpText/LabeledBy set for interactive and important content for screen readers  
  Evidence:
- [ ] Color contrast meets WCAG AA; does not rely solely on color to convey meaning  
  Evidence:
- [ ] High contrast and text scaling work; no clipped or unreadable content  
  Evidence:
- [ ] Controls expose correct roles and states to UI Automation  
  Evidence:

## 7) Layout and responsiveness

- [ ] Uses Grid with star sizing and sensible MinWidth/MinHeight to scale with window size and DPI  
  Evidence:
- [ ] Content scrolls appropriately; virtualization enabled for large item lists  
  Evidence:
- [ ] Resize behavior tested (small, medium, large) and on high‑DPI monitors  
  Evidence:

## 8) Async, threading, and cancellation

- [ ] Async operations are truly asynchronous (no blocking of UI thread)  
  Evidence:
- [ ] CancellationToken supported for long‑running calls; cancellation path cleans up state  
  Evidence:
- [ ] Errors surfaced to the UI with actionable messages; rethrow/log balance is appropriate  
  Evidence:
- [ ] UI updates occur on UI thread (Dispatcher/ThreadOption.UIThread)  
  Evidence:

## 9) Performance

- [ ] Startup and key interactions measured; no obvious hot paths (avoid unnecessary bindings in tight loops)  
  Evidence:
- [ ] Virtualization for ItemsControls (EnableRowVirtualization/ColumnVirtualization or control‑specific flags)  
  Evidence:
- [ ] Avoids memory leaks: unsubscribes from events/EventAggregator, disposes timers/streams, no static captures  
  Evidence:
- [ ] Images and heavy visuals are cached or deferred; no oversized bitmaps  
  Evidence:

## 10) Syncfusion specifics (per control)

- [ ] Syncfusion license key is registered at startup per official guidance for the used version  
  Evidence:
- [ ] Required assemblies referenced; using documented APIs only (no invented members)  
  Evidence:
- [ ] Control‑level performance features enabled (e.g., SfDataGrid virtualization, column generation strategy)  
  Evidence:
- [ ] Theme applied via SfSkinManager; control styles unified with rest of app  
  Evidence:
- [ ] Control events used as documented (e.g., AutoGeneratingColumn, SelectionChanged) and handlers are thin/VM‑based  
  Evidence:
- [ ] Per‑control matrix validated (Ribbon, Scheduler, Chart, SfAIAssistView, etc.) using each control’s “Getting
      Started” and API reference; evidence includes exact doc links pinned to the specific library version.  
  Evidence:
- [ ] No undocumented/assumed APIs used; any advanced behavior is backed by an official sample or API reference.  
  Evidence:

## 11) Navigation & region management (Prism)

- [ ] View registered for navigation; RegionManager requests inject the correct view  
  Evidence:
- [ ] INavigationAware implemented where needed: OnNavigatedTo loads minimal data; OnNavigatedFrom saves state/cleans up  
  Evidence:
- [ ] No duplicate load patterns (avoid view Loaded for data fetch when navigation already triggers it)  
  Evidence:
- [ ] Back/forward journaling behaves as intended (or is explicitly disabled)  
  Evidence:

## 12) Logging, diagnostics, and telemetry

- [ ] Key actions and failures logged (e.g., Serilog) with context (user, route, correlation id)  
  Evidence:
- [ ] Binding and navigation issues traceable (enable PresentationTraceSources as needed in dev builds)  
  Evidence:
- [ ] Non‑PII telemetry only; opt‑in/consent respected  
  Evidence:

## 13) Security and privacy

- [ ] No secrets in code or XAML; configuration pulled from secure sources  
  Evidence:
- [ ] User input sanitized where applicable; file/URL access constrained  
  Evidence:
- [ ] Clipboard and file export guarded; sensitive info masked/redacted  
  Evidence:

## 14) Testing (unit, integration, UI)

- [ ] ViewModel unit tests cover core logic, commands, validation, and state transitions  
  Evidence:
- [ ] Converter tests (for each non‑trivial converter)  
  Evidence:
- [ ] Integration tests cover navigation, region injection, and data flows  
  Evidence:
- [ ] UI/Automation tests for critical flows (smoke/E2E) run in CI  
  Evidence:

## 15) Documentation & discoverability

- [ ] XML doc comments for public VM members; inline comments where intent is non‑obvious  
  Evidence:
- [ ] README or view‑level notes describing purpose, data contract, and known constraints  
  Evidence:
- [ ] Support links to relevant control docs (Syncfusion) and Microsoft guidance  
  Evidence:

## 16) Build & CI quality gates

- [ ] Build passes locally and in CI; warnings reviewed (treat as errors where feasible)  
  Evidence:
- [ ] Static analysis/linters (style, analyzers) pass  
  Evidence:
- [ ] Test tasks (unit/UI) wired into CI; coverage thresholds met  
  Evidence:
- [ ] Checklist automation: CI parses the per‑view template in PRs and enforces thresholds (e.g., fail if < configured
      completion %, or if any critical item fails).  
  Evidence:
- [ ] Evidence links (files/lines, screenshots) included in PR description for auditability.  
  Evidence:

## 17) Resource lifecycle & cleanup

- [ ] Disposables disposed (IDisposable/IAsyncDisposable) and CancellationTokenSource canceled  
  Evidence:
- [ ] EventAggregator subscriptions removed (or rely on weak refs with conscious tradeoff)  
  Evidence:
- [ ] Region clean‑up on navigation away (detach heavy child controls, stop timers, clear large collections if needed)  
  Evidence:

## 18) Configuration & environment

- [ ] Settings read via configuration service/Options; no hard‑coded environment switches  
  Evidence:
- [ ] Feature flags/toggles supported where applicable  
  Evidence:

## 19) Error handling & resiliency

- [ ] Network/service errors surfaced with retry/backoff when appropriate  
  Evidence:
- [ ] Fallback UI for unavailable services; user clear next steps  
  Evidence:

---

## 20) Custom controls & third‑party integrations

- [ ] For non‑Syncfusion/custom controls, a mini‑matrix is provided with links to official docs (or internal design
      docs) covering API usage, theming, and accessibility.  
  Evidence:
- [ ] Theming integration verified: resources, styles, and behaviors align with app dictionaries; dynamic resources are
      used where live updates are expected.  
  Evidence:
- [ ] Accessibility verified: roles, names, and states exposed via UIA; keyboard navigation and focus behavior tested.  
  Evidence:
- [ ] Performance characteristics documented and enabled (virtualization, deferred loading, caching) where applicable.  
  Evidence:

## Syncfusion control checklist (example: SfDataGrid)

- [ ] Assemblies referenced: Syncfusion.Data.WPF, Syncfusion.SfGrid.WPF, Syncfusion.Shared.WPF (and converters for export if used)  
  Evidence:
- [ ] ItemsSource bound to IEnumerable/ObservableCollection; auto‑generated columns reviewed (or defined explicitly)  
  Evidence:
- [ ] Performance: virtualization on; sorting/grouping/filtering configured with event handlers only when needed  
  Evidence:
- [ ] Editing: AllowEditing/Deleting/AddNewRowPosition configured per requirements; validation integrated with VM  
  Evidence:
- [ ] Theme: applied via SfSkinManager; consistent with app  
  Evidence:

> Replace with the specific Syncfusion control(s) present in the view (Ribbon, Scheduler, Chart, SfAIAssistView, etc.) and validate against their official docs.

---

## Scoring rubric

- Critical (Architecture, Binding, Validation, Navigation, Performance, Accessibility): 5 pts each item
- Important (UX/Design, Commands, Async/Cancellation, Logging, Security, Testing, Cleanup): 3 pts each item
- Nice‑to‑have: standardize as follows for consistency:
  - Documentation & discoverability basics: 1 pt per item
  - Advanced UX polish (e.g., gestures, micro‑interactions) and optional per‑control enhancements: 2 pts per item

Compute: (Points achieved) / (Points applicable) = Completion %.  
Gate suggestions: 
- Ship‑ready ≥ 90%  
- Beta ≥ 80% (with no critical item failing)  
- Dev‑only < 80%

Notes:
- Do not double‑count overlapping items—score once in the most relevant section.
- CI gating is recommended: parse the per‑view template in PRs and enforce project‑specific thresholds.

---

## Wiley Widget “icing on the cake” (repo‑specific checks)

These polish items reflect patterns already present in this codebase. Use them to keep views consistent with our theming, resources, behaviors, and Prism region conventions.

1) Theming and resources (Syncfusion + app dictionaries)
- [ ] Global theme is respected: avoid hard‑coding `syncfusionskin:SfSkinManager.VisualStyle="..."` in XAML unless an intentional override is documented. Prefer the app‑wide `SfSkinManager.ApplicationTheme` managed by `ThemeManager`.  
  Evidence:
- [ ] If a per‑control override is truly required, bind or apply through `ThemeManager.ApplyThemeToControl(...)` or use `ThemeUtility.ToVisualStyle(...)` to avoid drift.  
  Evidence:
- [ ] View styles/colors come from `Themes/WileyTheme.xaml` and `Themes/Generic.xaml` via (Dynamic)Resource, not hard‑coded brushes; use `DynamicResource` for anything that should live‑update on theme change.  
  Evidence:
- [ ] Converters are sourced from `Themes/Generic.xaml` (no duplicate local declarations); keys and pack URIs are correct.  
  Evidence:
- [ ] If the view depends on theme at runtime (e.g., cached visuals), it listens to `ThemeManager.ThemeChanged` and updates accordingly (or uses only dynamic resources).  
  Evidence:
- [ ] Syncfusion license registration verified as per version guidance in app startup.  
  Evidence:

2) Behaviors and focus UX
- [ ] Initial focus is intentional using `FocusOnLoadBehavior` (root or a specific `TargetName`); avoid competing focus behaviors on the same control.  
  Evidence:
- [ ] `MouseFocusBehavior` is used where click‑to‑prime keyboard input improves UX, but does not steal focus from more important flows.  
  Evidence:
- [ ] No redundant event handlers for focus if the behavior already provides it (keep code‑behind thin).  
  Evidence:

3) Prism regions and docking
- [ ] Region name used in XAML matches the canonical name (no typos); only one instance per intended region.  
  Evidence:
- [ ] If using Docking‑based layouts, `RegionManagerBehavior` is attached where appropriate so regions are initialized and added to `DockingManager` when missing.  
  Evidence:
- [ ] Navigation responsibilities are not split across `Loaded` and `INavigationAware` (no double loads).  
  Evidence:

4) Accessibility and text/UI polish
- [ ] `AutomationProperties.Name`/`HelpText` assigned to key inputs and buttons; labels use `LabeledBy` where appropriate (especially for custom/Syncfusion controls).  
  Evidence:
- [ ] Microcopy uses consistent terminology; headers/subheaders/buttons align with WileyTheme text styles (Header/Subheader/Body).  
  Evidence:
- [ ] Card/Panel visuals use the shared styles (e.g., Card Border style) rather than ad‑hoc borders.  
  Evidence:

5) Performance and cleanup niceties
- [ ] Long‑running work is cancellable; tokens are disposed on navigation away.  
  Evidence:
- [ ] EventAggregator subscriptions are cleaned up (or intentionally weak); timers/streams are disposed.  
  Evidence:
- [ ] ItemsControls use virtualization where applicable; avoid unnecessary bindings in item templates.  
  Evidence:

Notes:
- Central theming: `Services/ThemeManager.cs` and `Services/ThemeUtility.cs` govern theme selection, persistence, and conversion to Syncfusion `Theme`/`VisualStyles`. Prefer these over per‑view theme logic.  
- App resources: `App.xaml` merges `Themes/Generic.xaml` and `Themes/WileyTheme.xaml`; use these dictionaries for shared colors/typography/converters.  
- Region initialization: `Behaviors/RegionManagerBehavior.cs` coordinates Prism regions with `DockingManager` when needed.

---

## Per‑view evaluation template (copy/paste)

View name:  
Owner:  
Date:  
Module/Region:

- Architecture & MVVM: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Data binding: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Commands/Input: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Validation/Error UX: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- UX/Design/Theming: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Accessibility: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Layout/Responsive: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Async/Cancellation: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Performance: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Syncfusion specifics: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Navigation/Regions: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Logging/Telemetry: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Security/Privacy: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Testing: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Documentation: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Cleanup/Lifecycle: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Config/Environment: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:
- Error handling/Resiliency: [ ] Pass | [ ] Needs work | [ ] N/A  
  Notes/Evidence:

Summary score:  
Critical items failing:  
Top 3 gaps:

---

## Quick‑pass examples for this repo (initial)

- AIAssistPanelView (Syncfusion AI Assist UI)  
  - Architecture & MVVM: ViewModel autowiring enabled; module registration points to panel view.  
  - Data binding: Messages/CurrentUser bindings in place; suggestions and typing indicator bound.  
  - Validation: Mostly command‑driven chat; input box should include basic rules (non‑empty, max length).  
  - Accessibility: Needs explicit AutomationProperties and tab order verification.  
  - Navigation/Regions: Registered to AI region; verify INavigationAware usage and cleanup when navigating away.  
  - Syncfusion specifics: Ensure license registration and control properties match official docs for the control.  
  - Cleanup: Ensure timers/background operations cancel on dispose/navigate.  
  Action: Run this checklist fully and record evidence per item.

- Dashboard views  
  - Timer ownership centralized in ViewModel and disposed; duplicate loads reduced.  
  - Follow through on remaining Loaded‑to‑VM lifecycle migrations and add cancellation for any async data loads.

> Treat the above as starting notes; complete the full template per view with evidence and scores.

---

Maintainers: When introducing a new view, paste the template into the PR description and attach evidence links (file ranges, screenshots). CI can gate on the declared score if desired.

---

## Evaluated views (running list)

Maintain a lightweight ledger of views that have been evaluated with this checklist. Keep this list scoped to current release cycle; archive prior cycles in `docs/history/` if it grows too large.

- View: AIAssistPanelView  
  Module/Region: AIAssistModule / AIAssistRegion  
  Date: 2025-10-19  
  Result: Ship-ready (≈92–94%)  
  Evidence: 
  - View: `src/Views/AIAssistPanelView.xaml`  
  - ViewModel: `src/ViewModels/AIAssistViewModel.cs`  
  - Tests: 
    - `tests/WileyWidget.LifecycleTests/AIAssistLifecycleTests.cs`  
    - `tests/WileyWidget.LifecycleTests/AIAssistViewThemeAndBehaviorTests.cs`  
  Notes: Theming refactor to DynamicResource, access keys and keyboard shortcuts added, DEBUG perf timing instrumented.

- **Modern WPF/Prism Updates**: References are solid but static. Input: Add a maintenance note to periodically validate against evolving docs (e.g., Prism 8+ changes to IContainerRegistry or WPF .NET 9 previews). Include emerging topics like MAUI/WPF interop if relevant to the repo.
