# WPF View Completion Checklist (Prism + Syncfusion)

This checklist consolidates Microsoft WPF guidance, Prism patterns, and Syncfusion WPF control practices into a single, practical template you can use to judge when a view is truly "complete." Each item is intentionally concrete, verifiable, and expanded with detailed sub-checks, examples, and enforcement notes to prevent superficial reviews. The goal is to enforce strict adherence to official documentation, avoiding assumptions or "creative" implementations that deviate from prescribed methods. For instance, if a Syncfusion control's documentation specifies a particular event handler pattern, use it exactly as described—do not improvise or rely on inferred behaviors.

Sources (expanded with version-specific pinning):

- Microsoft WPF: Always reference the exact .NET version in use (e.g., .NET 8 or 9 previews). Key topics include:
  - Data binding overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview?view=netdesktop-8.0
  - Data templating: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-templating-overview?view=netdesktop-8.0
  - Validation: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-implement-binding-validation?view=netdesktop-8.0
  - Commanding: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview?view=netdesktop-8.0
  - Layout: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/layout?view=netdesktop-8.0
- Prism: Pin to the exact version (e.g., Prism 8.x or 9.x). Key topics:
  - Event Aggregator: https://prismlibrary.com/docs/event-aggregator.html (review full API, including Publish/Subscribe overloads and thread options)
  - ViewModelLocator: https://prismlibrary.com/docs/wpf/view-model-locator.html
  - Navigation: https://prismlibrary.com/docs/wpf/navigation.html
- Syncfusion WPF: Pin to the exact version (e.g., 23.x or 24.x). For each control, review **ALL** documentation sections—not just "Getting Started." This includes:
  - API Reference: Full property/event/method listings.
  - Features: Detailed sub-sections on binding, events, styling, performance, accessibility, etc.
  - Samples: Verify against official demos.
  - Known Issues/Limitations: Ensure no workarounds are needed without documentation.
  - Example for SfDataGrid: https://help.syncfusion.com/wpf/datagrid/overview (full nav tree: Getting Started, Data Binding, Columns, Editing, Filtering, Grouping, Sorting, Exporting, Theming, Accessibility, Performance, etc.)
  - Themes and Licensing: https://help.syncfusion.com/wpf/themes/overview and https://help.syncfusion.com/common/essential-studio/licensing/overview
  - Enforcement: For every Syncfusion control property/event used, cite the exact doc page and section proving its correct usage. If a feature is undocumented, do not use it—file a support ticket with Syncfusion instead.

> Note: To combat tools like Copilot "doing it their own way," each Syncfusion item requires explicit evidence linking to the doc's exact wording. For example, if using SfDataGrid.AutoGenerateColumns, quote the doc: "Set to true to automatically generate columns based on the ItemsSource properties" and confirm no overrides contradict this.

---

## How to use this checklist (expanded)

- Work top-down **and** bottom-up: Start with architecture, end with polish, then loop back to verify cross-section consistency (e.g., does a binding in section 2 align with performance in section 9?).
- Mark each checkbox with status: [x] Complete (with evidence), [ ] Incomplete (with gap description), [N/A] Not Applicable (with justification, e.g., "No async ops in this view").
- Evidence must be verifiable: Include file paths/lines (e.g., AIAssistViewModel.cs:Line 42), PR links, screenshots (with annotations), or test case IDs. For doc adherence, quote the exact sentence from the source.
- Scoring: Use the rubric; deduct points for any deviation from docs (e.g., using an undocumented event handler = -5 pts critical).
- In PRs: Paste the full per-view template, compute score, and include evidence. CI must parse and enforce (e.g., fail if <90% or any critical fail). Use YAML or JSON in PR comments for CI readability.
- Avoid double-counting: Cross-reference (e.g., "See section 17 for EventAggregator cleanup").
- Version pinning: Always cite version-specific docs (e.g., "Prism 8.1: EventAggregator.Subscribe uses weak references by default—verified no leaks").
- Expansion principle: Each section now includes sub-checks, examples, and "common pitfalls" to force thoroughness. If a section feels "good enough," re-review the full source docs.

---

## 1) Architecture & MVVM wiring (Prism) - Critical (expanded with sub-checks for strict Prism adherence)

- [ ] View and ViewModel exist as a pair with MVVM separation (no business logic in code-behind)  
       Sub-checks: Code-behind limited to UI-only (e.g., no data fetches); all logic in VM. Example: If handling a button click, use ICommand in VM, not event in code-behind. Pitfall: Avoid Copilot-suggested "quick fixes" that add logic to Loaded/Unloaded.  
       Evidence:
- [ ] ViewModel is resolved by Prism ViewModelLocator (AutoWireViewModel=True or explicit registration)  
       Sub-checks: Verify in XAML: prism:ViewModelLocator.AutoWireViewModel="True". If explicit, check Container.RegisterType in module. Quote Prism doc: "AutoWireViewModel attaches the ViewModel to the View's DataContext."  
       Evidence:
- [ ] All dependencies of the ViewModel are container-resolvable (IContainerRegistry registrations exist)  
       Sub-checks: List all VM ctor params; confirm each registered (e.g., IEventAggregator). Test resolution in unit tests. Pitfall: No manual new()—always container.  
       Evidence:
- [ ] Module registers the view for navigation/region usage (RegisterForNavigation or explicit RegionManager registration)  
       Sub-checks: In Module.cs: container.RegisterForNavigation<ViewType>(). Quote Prism doc on RegisterForNavigation overloads.  
       Evidence:
- [ ] View placed into the correct region by RegionManager (RegionName constant, no typos)  
       Sub-checks: XAML: prism:RegionManager.RegionName="{x:Static regions:RegionNames.AIAssist}". Verify constant class for no string magic.  
       Evidence:
- [ ] Uses INavigationAware/IConfirmNavigationRequest as needed to load/unload data and guard navigation  
       Sub-checks: OnNavigatedTo: Load data minimally. OnNavigatedFrom: Save/cleanup. IConfirm: Guard unsaved changes. Quote full Prism navigation API. Pitfall: No duplicate in Loaded event.  
       Evidence:
- [ ] EventAggregator usage: subscriptions are scoped appropriately, use ThreadOption.UIThread when updating UI, and are unsubscribed/cleaned up  
       Sub-checks: Subscribe with lambda/filter; use PublisherThread/UIThread/BackgroundThread per doc. Unsubscribe in Dispose/OnNavigatedFrom. Weak refs default—test for leaks. Example: \_eventAggregator.GetEvent<MyEvent>().Subscribe(OnEvent, ThreadOption.UIThread);  
       Evidence:
- [ ] Long-lived resources (timers, streams, file watchers) are owned by the ViewModel and disposed deterministically  
       Sub-checks: Use IDisposable; dispose in OnNavigatedFrom/Dispose. For timers: System.Timers.Timer with AutoReset=false if one-shot. Pitfall: No static timers.  
       Evidence:

## 2) Data Binding fundamentals - Critical (expanded with binding mode details and error tracing)

- [ ] DataContext flows via ViewModelLocator or is explicitly set; no conflicting DataContext on inner elements  
       Sub-checks: Root DataContext from VM; inner elements inherit or RelativeSource. Check for overrides.  
       Evidence:
- [ ] Bindings use correct Mode (OneWay/TwoWay/OneTime/OneWayToSource) per scenario  
       Sub-checks: OneWay for read-only; TwoWay for edits; OneTime for static. Quote MS doc examples. Pitfall: Default TwoWay on TextBox can cause unintended updates.  
       Evidence:
- [ ] UpdateSourceTrigger is appropriate (e.g., LostFocus for TextBox unless immediate updates are needed)  
       Sub-checks: PropertyChanged for live previews; Explicit for batch. Test perf impact.  
       Evidence:
- [ ] ViewModel implements INotifyPropertyChanged; collections implement ObservableCollection<T> or INotifyCollectionChanged  
       Sub-checks: Use Prism BindableBase. For custom collections, implement full INCC. Unit test notifications.  
       Evidence:
- [ ] Binding paths are valid; binding errors do not appear at runtime (verify via Output window or PresentationTraceSources when needed)  
       Sub-checks: Enable trace: PresentationTraceSources.TraceLevel=High. Monitor debug output for "BindingExpression path error." Fix all.  
       Evidence:
- [ ] Value converters are used when default type conversion is insufficient; converters are unit tested  
       Sub-checks: Implement IValueConverter; test Convert/ConvertBack. Resource in App.xaml. Pitfall: No inline lambdas.  
       Evidence:
- [ ] Data templates are used to shape complex items; no ToString() fallback visuals  
       Sub-checks: DataTemplate in Resources; bind sub-properties. Quote MS templating doc.  
       Evidence:
- [ ] Collection views (CollectionViewSource) are used for sort/filter/group when needed; current item behavior is correct  
       Sub-checks: In XAML or code: CollectionViewSource with SortDescriptions. Sync CurrentItem. Pitfall: Manual sorting ignores view.  
       Evidence:

## 3) Commands, input, and gestures - Important (expanded with input binding examples)

- [ ] All user actions are ICommand-backed (e.g., Prism DelegateCommand) with CanExecute and dynamic requerying  
       Sub-checks: DelegateCommand.ObservesCanExecute. RaiseCanExecuteChanged on deps. Quote Prism commanding.  
       Evidence:
- [ ] Keyboard gestures or InputBindings are provided for high-value actions (e.g., Ctrl+Enter to send)  
       Sub-checks: <InputBindings> <KeyBinding Key="Enter" Modifiers="Ctrl" Command="{Binding SendCommand}"/> </InputBindings>. Test all.  
       Evidence:
- [ ] CommandTargets are set when needed (for RoutedCommand); otherwise ICommand is bound to the VM  
       Sub-checks: For Routed: CommandTarget="{Binding ElementName=foo}". For Delegate: Direct bind.  
       Evidence:
- [ ] Access keys and accelerator text provided in menus/buttons where applicable  
       Sub-checks: Button Content="\_Save" (underscore for alt key). Menus with &Save.  
       Evidence:
- [ ] Focus management on open/close/navigation (initial focus, returning focus after dialogs)  
       Sub-checks: Use FocusManager.FocusedElement. After dialog: element.Focus().  
       Evidence:

## 4) Validation and error UX - Critical (expanded with async validation details)

- [ ] Input validation implemented using one of: ValidationRules, IDataErrorInfo, or INotifyDataErrorInfo  
       Sub-checks: Prefer INDEI for async/multi-error. Quote MS: "INotifyDataErrorInfo for MVVM-friendly validation."  
       Evidence:
- [ ] ErrorTemplate or visual cues for invalid inputs; tooltips or inline messages show Validation.Errors content  
       Sub-checks: <ControlTemplate x:Key="ErrorTemplate"> with Adorner. Bind ToolTip to Validation.Errors[0].ErrorContent.  
       Evidence:
- [ ] UpdateSourceTrigger selected to match validation strategy (PropertyChanged vs LostFocus vs Explicit)  
       Sub-checks: PropertyChanged for immediate; LostFocus for perf.  
       Evidence:
- [ ] Cross-field/business-rule validation (e.g., date ranges) implemented and surfaced to the user  
       Sub-checks: In VM: Validate on property set, check others. Use ErrorsChanged event.  
       Evidence:
- [ ] Async validation (if applicable) debounced and cancellable; does not freeze the UI  
       Sub-checks: Use Debounce on input; Task.Run with CancellationToken. UI update on Dispatcher. Pitfall: No await without ConfigureAwait(false).  
       Evidence:
- [ ] Primary action (Save/Submit) disabled when invalid  
       Sub-checks: CanExecute checks HasErrors.  
       Evidence:

## 5) UX, design, and theming - Important (expanded with theme switching tests)

- [ ] View aligns with app style guide (colors, typography, spacing, component usage) via ResourceDictionaries  
       Sub-checks: Merge Themes/WileyTheme.xaml. Use {StaticResource PrimaryBrush}.  
       Evidence:
- [ ] Theme applied consistently (Syncfusion SfSkinManager or global theme) and is discoverable by users if theme switching is supported  
       Sub-checks: SfSkinManager.SetTheme(this, new Theme("MaterialDark")); Test switch: ThemeManager.ChangeTheme. Quote Syncfusion theme doc full list.  
       Evidence:
- [ ] Acrylic Effect (translucent blurred background) enabled for modern, layered UI depth in FluentDark theme  
       Sub-checks: **IMPLEMENTATION NOTE**: Use Syncfusion's `SfAcrylicPanel` wrapper control (actual API) instead of conceptual `FluentTheme()` properties. XAML Example: `<syncfusion:SfAcrylicPanel TintBrush="#202020" TintOpacity="0.25" BlurRadius="30" NoiseOpacity="0.03">`. Combine with ViewModel's `ShowAcrylicBackground` property for conditional rendering via `Visibility` binding. Limitation: Requires Windows 10+ composition APIs; test on high-DPI screens for performance. Acrylic reduces flatness by adding subtle depth—ideal for dashboards and overlays. Default is false—must explicitly enable. Pitfall: May not work in virtualized environments or older OS versions. Test all views for consistency. **See**: `docs/FLUENTDARK_ENHANCED_EFFECTS_CONFIGURATION.md` for complete implementation guide and API reality check.  
       Evidence:
- [ ] Reveal Animation (Hover and Pressed Effects) enabled for dynamic, tactile interactions in FluentDark theme  
       Sub-checks: **IMPLEMENTATION NOTE**: Hover/Pressed effects are built into FluentDark theme automatically via dynamic theme brushes (`HoverBackgroundBrush`, `PressedBackgroundBrush`). No explicit API configuration needed—already active when `SfSkinManager.ApplicationTheme = new Theme("FluentDark")` is set. Apply via XAML triggers using `{DynamicResource HoverBackgroundBrush}` and `{DynamicResource PressedBackgroundBrush}`. Use ViewModel's `HoverEffectMode` and `PressedEffectMode` properties for conditional styling or user preferences. Limitation: Effects optimized for Syncfusion controls (e.g., SfDataGrid, SfButtonAdv); native WPF controls may need extra styling. Visual Appeal Boost: Counters "stale" UIs by making hovers interactive—great for dashboard panels or navigation buttons, adding premium feel without clutter. Test all interactive controls for consistency. **See**: `docs/FLUENTDARK_ENHANCED_EFFECTS_CONFIGURATION.md` for implementation patterns.  
       Evidence:
- [ ] High-Visibility Keyboard Visuals enabled for enhanced accessibility and visual feedback in FluentDark theme  
       Sub-checks: **IMPLEMENTATION NOTE**: Apply via WPF's standard `FocusVisualStyle` property on controls, not via conceptual `SetFocusVisualKind()` API. Create custom FocusVisualStyle with thicker borders/glows (e.g., `StrokeThickness="2"`) and apply per-control or via implicit Style. Use ViewModel's `FocusVisualKind` property to conditionally apply different focus styles based on user preference or theme. Integrates seamlessly with reveal effects for smooth focus transitions. Visual Appeal Boost: Reduces flatness by emphasizing active elements—especially useful in AIAssistView for input boxes, grids, and interactive controls. Combine with acrylic + reveal for full FluentDark upgrade. Performance Note: Enable virtualization (e.g., in SfDataGrid) to avoid lag with effects. Testing: Run in system-wide dark mode to validate contrast; verify DPI awareness and theme timing in OnLoaded. Test all focusable controls for consistency. **See**: `docs/FLUENTDARK_ENHANCED_EFFECTS_CONFIGURATION.md` for code examples.  
       Evidence:
- [ ] **ALL styling elements validated—NO hardcoded colors/brushes remain in XAML (FluentDark theme compliance audit)**  
       Sub-checks: **CRITICAL VALIDATION TASK** - Perform exhaustive line-by-line XAML review to eliminate ALL hardcoded styling that breaks theme consistency. This is a **zero-tolerance** requirement for FluentDark theme integration. **Explicit validation areas**:

  **1. Top Input/Ribbon Area** (Grid.Row="0"):
  - ✅ Border: Uses `{DynamicResource ContentBackground}`, `{DynamicResource BorderAlt}` ✓
  - ✅ SfTextBoxExt: Uses `{DynamicResource ContentBackground}`, `{DynamicResource ContentForeground}` ✓
  - ✅ Watermark: Uses `{DynamicResource Gray2}` ✓
  - ✅ ButtonAdv: Inherits theme automatically ✓
  - ⚠️ **VALIDATE**: No `Background="#FFFFFF"`, `BorderBrush="#CCCCCC"`, or `Foreground="#000000"` hardcoded values

  **2. Chat Response Area** (Grid.Row="1" - ScrollViewer/ItemsControl):
  - ✅ Border: Uses `{DynamicResource ContentBackgroundAlt2}`, `{DynamicResource BorderAlt}` ✓
  - ✅ Message Bubbles: Use converters (`AuthorBackgroundConverter`, `AuthorAlignmentConverter`) ✓
  - ✅ Empty State TextBlock: Uses `{DynamicResource Gray2}` for `Foreground` ✓
  - ✅ Error Border: Uses `{DynamicResource ErrorBackgroundBrush}`, `{DynamicResource ErrorBorderBrush}`, `{DynamicResource ErrorForegroundBrush}` ✓
  - ⚠️ **VALIDATE**: No `Background="#FFEBEE"`, `BorderBrush="#E57373"`, `Foreground="#9E9E9E"` hardcoded values

  **3. Bottom Toolbar** (Grid.Row="2"):
  - ✅ Border: Uses `{DynamicResource ContentBackgroundAlt1}`, `{DynamicResource BorderAlt}` ✓
  - ✅ ButtonAdv (Mode buttons): Inherit theme automatically ✓
  - ✅ ComboBox (History): Uses `{DynamicResource ContentBackground}`, `{DynamicResource ContentForeground}`, `{DynamicResource BorderAlt}` ✓
  - ✅ ComboBoxItem hover: Uses `{DynamicResource HoverBackgroundBrush}` ✓
  - ⚠️ **VALIDATE**: No `Background="#F0F0F0"`, `Background="White"`, `BorderBrush="#CCCCCC"` hardcoded values

  **4. Resource Dictionaries & Styles**:
  - ✅ ErrorTemplate: Uses `{DynamicResource}` for all colors ✓
  - ✅ SfTextBoxExt Style: Uses `{DynamicResource HoverBackgroundBrush}` for hover trigger ✓
  - ✅ HighVisibilityFocusStyle: Uses `{DynamicResource PrimaryBrush}` ✓
  - ⚠️ **VALIDATE**: No `Background="#FFFFFF"`, `Foreground="#000000"` in any Style Setter

  **5. Syncfusion Control Theme Inheritance**:
  - ✅ SfTextBoxExt: Properly styled with theme-aware brushes ✓
  - ✅ ButtonAdv: Inherits from SfSkinManager.ApplicationTheme ✓
  - ✅ SfBusyIndicator: Inherits theme automatically ✓
  - ✅ SfAcrylicPanel: Uses `TintBrush="#202020"` (dark theme specific) ✓
  - ⚠️ **VALIDATE**: All Syncfusion controls respond to `SfSkinManager.ApplicationTheme = new Theme("FluentDark")`

  **Validation Method**:
  1. **Search XAML for patterns**: `Background="#`, `Foreground="#`, `BorderBrush="#`, `Fill="#`, `Stroke="#"` → Replace ALL with `{DynamicResource}` or `{StaticResource}` from WileyTheme.xaml
  2. **Grep for hardcoded colors**: Use regex `(Background|Foreground|BorderBrush|Fill|Stroke)="(?!{)(#|[A-Z][a-z]+)"` to find violations
  3. **Test theme switching**: Change theme at runtime (`SfSkinManager.ApplicationTheme = new Theme("MaterialLight")`) and verify ALL elements update correctly
  4. **Dark mode validation**: Run in Windows system-wide dark mode and verify no light-colored elements "bleed through"
  5. **High-DPI/scaling test**: Test at 125%, 150%, 200% DPI to ensure brushes scale correctly without artifacts

  **Common Pitfalls**:
  - ❌ Hardcoded white backgrounds on ComboBox/TextBox (breaks dark theme)
  - ❌ Light gray borders (`#CCCCCC`) that become invisible in light themes
  - ❌ Hardcoded error colors (`#F44336`) that don't adapt to theme's error palette
  - ❌ Foreground colors (`#9E9E9E`) that fail contrast ratios in different themes
  - ❌ ToolTip backgrounds/foregrounds that don't inherit theme

  **Evidence Requirements**:
  - [ ] XAML grep results showing ZERO matches for hardcoded color patterns
  - [ ] Screenshot of view in FluentDark theme (baseline)
  - [ ] Screenshot of view after theme switch to MaterialLight (validation)
  - [ ] List of all `{DynamicResource}` bindings used with corresponding WileyTheme.xaml keys
  - [ ] Line-by-line validation checklist for each Grid.Row section (see above)

  **Enforcement**: This item is **BLOCKING** for PR approval. Any hardcoded color/brush value = automatic PR rejection. Use `docs/FLUENTDARK_ENHANCED_EFFECTS_CONFIGURATION.md` as reference for proper dynamic brush usage patterns.  
  Evidence:

- [ ] **StaticResource vs DynamicResource usage validated—all theme-dependent resources use DynamicResource for runtime theme switching**  
       Sub-checks: **CRITICAL VALIDATION TASK** - Ensure proper resource reference type is used throughout XAML to support theme switching and avoid KeyNotFoundException. This is a **mandatory** requirement for theme consistency and runtime reliability.

  **Resource Reference Strategy**:

  **1. Use DynamicResource for (ALWAYS theme-dependent)**:
  - ✅ **Theme brushes**: `Background="{DynamicResource ContentBackground}"`, `Foreground="{DynamicResource ContentForeground}"`
  - ✅ **Theme colors**: `BorderBrush="{DynamicResource BorderAlt}"`, `Fill="{DynamicResource PrimaryBrush}"`
  - ✅ **Style references that may change**: `Style="{DynamicResource CustomButtonStyle}"`
  - ✅ **Syncfusion theme resources**: Any resource from `WileyTheme-Syncfusion.xaml` or Syncfusion theme packages
  - ✅ **User preference-dependent resources**: Resources that change based on settings or runtime conditions

  **2. Use StaticResource for (ONLY static, never-changing)**:
  - ✅ **Converters**: `Converter="{StaticResource BooleanToVisibilityConverter}"`
  - ✅ **DataTemplates**: `ContentTemplate="{StaticResource MyDataTemplate}"` (unless template itself needs theme-aware content)
  - ✅ **Geometry/Shapes**: Fixed path data, icon geometries
  - ✅ **Fixed configuration values**: Constant numbers, strings that never change
  - ⚠️ **CAUTION**: Even DataTemplates may need DynamicResource if they contain theme-dependent brushes internally

  **3. Common Pitfalls**:
  - ❌ **Using StaticResource for theme brushes**: `Background="{StaticResource PrimaryBrush}"` → Will not update on theme change → **USE DynamicResource**
  - ❌ **Using StaticResource for Syncfusion styles**: `Style="{StaticResource SyncfusionWPFDataGridStyle}"` → May not inherit theme → **USE DynamicResource**
  - ❌ **Mixed usage in same control**: Inconsistent Static/Dynamic mix causes confusing behavior → **Be consistent**
  - ❌ **StaticResource in custom styles**: If a Style is defined locally and uses theme brushes, those brushes MUST use DynamicResource

  **Validation Method**:
  1. **Search XAML for StaticResource pattern**:

     ```powershell
     # Find all StaticResource usages
     Select-String -Path "*.xaml" -Pattern '{StaticResource \w+}' -AllMatches

     # Verify each match:
     # - Converter? ✅ OK
     # - Brush/Color ending in "Brush", "Color", "Background", "Foreground"? ❌ MUST be DynamicResource
     # - Style reference? ❌ LIKELY needs DynamicResource
     ```

  2. **Run Convert-XamlStaticToDynamic.ps1 analysis**:
     ```powershell
     .\scripts\Convert-XamlStaticToDynamic.ps1 -Path "src/Views/MyView.xaml" -WhatIf
     # Review suggestions and apply fixes
     ```
  3. **Test theme switching at runtime**:
     - Set breakpoint in VM, change `SfSkinManager.ApplicationTheme = new Theme("MaterialLight")`
     - Verify ALL UI elements update immediately
     - Any element that doesn't update = StaticResource used incorrectly
  4. **Grep for high-risk patterns**:
     ```powershell
     # Find StaticResource usage with common brush names
     Select-String -Path "src/Views/*.xaml" -Pattern 'StaticResource.*(Background|Foreground|Border|Primary|Secondary|Content|Hover|Pressed)' -Context 0,2
     ```
  5. **Check custom resource dictionaries**:
     - Open `Themes/WileyTheme-Syncfusion.xaml`
     - Verify all Style setters use DynamicResource for theme-dependent values
     - Example: `<Setter Property="Background" Value="{DynamicResource PrimaryBrush}" />`

  **View-Specific Validation Checklist**:
  - [ ] All `Background` properties use DynamicResource (except fixed colors like Transparent)
  - [ ] All `Foreground` properties use DynamicResource
  - [ ] All `BorderBrush` properties use DynamicResource
  - [ ] All `Fill`/`Stroke` properties use DynamicResource (for shapes)
  - [ ] All Style references for themed controls use DynamicResource
  - [ ] All converters correctly use StaticResource
  - [ ] All DataTemplates analyzed—internal theme references use DynamicResource
  - [ ] Run `Convert-XamlStaticToDynamic.ps1` with `-WhatIf` flag—zero actionable warnings

  **Evidence Requirements**:
  - [ ] PowerShell script output showing StaticResource audit results
  - [ ] Screenshot of theme switch test (before/after comparison)
  - [ ] List of all DynamicResource references with corresponding resource keys
  - [ ] Confirmation that `Convert-XamlStaticToDynamic.ps1 -WhatIf` produces no critical warnings
  - [ ] Documentation reference: `docs/THEME_CONFIGURATION.md` section on StaticResource vs DynamicResource

  **Enforcement**: Any StaticResource usage for theme-dependent brushes/styles = **PR warning** (may become blocking). Review with architect if uncertain. Reference: `scripts/Convert-XamlStaticToDynamic.ps1` for automated conversion tool.  
  Evidence:

- [ ] Clear visual hierarchy; empty states, loading states, and error states are designed  
       Sub-checks: MultiDataTrigger for IsBusy/HasError/IsEmpty. Custom templates.  
       Evidence:
- [ ] Busy/Progress indicators are used during long operations; prevents double-submit  
       Sub-checks: IsBusy prop binds to ProgressBar Visibility; disable buttons during.  
       Evidence:
- [ ] Microcopy is concise and consistent with the domain (terms, labels)  
       Sub-checks: Labels: "Enter query" not "Type here". Consistent casing.  
       Evidence:

## 6) Accessibility (A11y) - Critical (expanded with UIA testing steps)

- [ ] Tab order is logical; full keyboard operation (no traps); Esc/Enter behave consistently  
       Sub-checks: TabIndex sequence; KeyboardNavigation.TabNavigation="Cycle". Test with Tab/Shift-Tab.  
       Evidence:
- [ ] Focus visuals present and visible; programmatic focus set on critical flows  
       Sub-checks: Default WPF focus rect; element.Focus() in OnNavigatedTo.  
       Evidence:
- [ ] AutomationProperties.Name/HelpText/LabeledBy set for interactive and important content for screen readers  
       Sub-checks: <Button AutomationProperties.Name="Submit query" AutomationProperties.HelpText="Sends the AI request"/>. Use LabeledBy for labels.  
       Evidence:
- [ ] Color contrast meets WCAG AA; does not rely solely on color to convey meaning  
       Sub-checks: Use tools like Color Contrast Analyzer; add icons/text for status.  
       Evidence:
- [ ] High contrast and text scaling work; no clipped or unreadable content  
       Sub-checks: Test at 200% DPI; use Auto font sizes.  
       Evidence:
- [ ] Controls expose correct roles and states to UI Automation  
       Sub-checks: Use Inspect.exe to verify UIA tree; Syncfusion controls must match doc's A11y section.  
       Evidence:

## 7) Layout and responsiveness - Important (expanded with DPI tests)

- [ ] Uses Grid with star sizing and sensible MinWidth/MinHeight to scale with window size and DPI  
       Sub-checks: <Grid.ColumnDefinitions> <ColumnDefinition Width="*" />. MinWidth=300.  
       Evidence:
- [ ] Content scrolls appropriately; virtualization enabled for large item lists  
       Sub-checks: ScrollViewer.CanContentScroll=True; ListView.VirtualizingStackPanel.IsVirtualizing=True.  
       Evidence:
- [ ] Resize behavior tested (small, medium, large) and on high-DPI monitors  
       Sub-checks: Test at 800x600, 1920x1080, 4K. No overlaps/clips.  
       Evidence:

## 8) Async, threading, and cancellation - Important (expanded with threading pitfalls)

- [ ] Async operations are truly asynchronous (no blocking of UI thread)  
       Sub-checks: Use async/await; Task.Run for CPU-bound. No .Result/.Wait.  
       Evidence:
- [ ] CancellationToken supported for long-running calls; cancellation path cleans up state  
       Sub-checks: Pass CTS.Token to Task; on cancel, reset UI. Dispose CTS.  
       Evidence:
- [ ] Errors surfaced to the UI with actionable messages; rethrow/log balance is appropriate  
       Sub-checks: Catch in VM, show MessageBox or inline; log with Serilog.  
       Evidence:
- [ ] UI updates occur on UI thread (Dispatcher/ThreadOption.UIThread)  
       Sub-checks: Application.Current.Dispatcher.Invoke. For EA: ThreadOption.UIThread.  
       Evidence:

## 9) Performance - Critical (expanded with measurement tools)

- [ ] Startup and key interactions measured; no obvious hot paths (avoid unnecessary bindings in tight loops)  
       Sub-checks: Use Stopwatch in DEBUG; aim <500ms load. Profile with VS Perf Analyzer.  
       Evidence:
- [ ] Virtualization for ItemsControls (EnableRowVirtualization/ColumnVirtualization or control-specific flags)  
       Sub-checks: For SfDataGrid: AllowRowVirtualization=True. Quote Syncfusion perf doc.  
       Evidence:
- [ ] Avoids memory leaks: unsubscribes from events/EventAggregator, disposes timers/streams, no static captures  
       Sub-checks: Use weak events if strong; dotMemory profile.  
       Evidence:
- [ ] Images and heavy visuals are cached or deferred; no oversized bitmaps  
       Sub-checks: BitmapCacheBrush; lazy load images.  
       Evidence:

## 10) Syncfusion specifics (per control) - Critical (fully expanded to require ALL doc review)

- [ ] Syncfusion license key is registered at startup per official guidance for the used version  
       Sub-checks: Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key); Quote licensing doc exact code snippet.  
       Evidence:
- [ ] Required assemblies referenced; using documented APIs only (no invented members)  
       Sub-checks: List assemblies (e.g., Syncfusion.SfGrid.WPF.dll); for each property/event, cite API ref page. Pitfall: If Copilot suggests a prop not in docs, reject it.  
       Evidence:
- [ ] Control-level performance features enabled (e.g., SfDataGrid virtualization, column generation strategy)  
       Sub-checks: Review full perf section: AllowRowVirtualization=True, AutoGenerateColumnsArgs.GenerationMode=Lazy. Quote exact options.  
       Evidence:
- [ ] Theme applied via SfSkinManager; control styles unified with rest of app  
       Sub-checks: SfSkinManager.SetTheme(control, theme); review full themes doc for supported styles. Test all themes.  
       Evidence:
- [ ] Control events used as documented (e.g., AutoGeneratingColumn, SelectionChanged) and handlers are thin/VM-based  
       Sub-checks: For each event: Quote doc description, e.g., "AutoGeneratingColumn: Raised when columns are auto-generated; use to customize." Handler delegates to VM. No thick logic.  
       Evidence:
- [ ] Per-control matrix validated (Ribbon, Scheduler, Chart, SfAIAssistView, etc.) using each control's **FULL** documentation tree: Getting Started, API Reference, Features (binding, editing, filtering, etc.), Theming, Accessibility, Performance, Known Issues. Evidence includes exact doc links pinned to the specific library version, with quotes for every used feature.  
       Sub-checks: For SfAIAssistView: Review binding to ItemsSource (quote data binding section), event handlers like QuerySubmitted (quote events section), accessibility roles (quote A11y section). List all used props/events with doc quotes. Pitfall: Do not assume defaults—verify against docs.  
       Evidence:
- [ ] No undocumented/assumed APIs used; any advanced behavior is backed by an official sample or API reference.  
       Sub-checks: If feature not in docs, stop and consult Syncfusion support. Attach ticket link as evidence.  
       Evidence:

## 11) Navigation & region management (Prism) - Critical (expanded with journaling tests)

- [ ] View registered for navigation; RegionManager requests inject the correct view  
       Sub-checks: \_regionManager.RequestNavigate(RegionNames.Foo, nameof(View));  
       Evidence:
- [ ] INavigationAware implemented where needed: OnNavigatedTo loads minimal data; OnNavigatedFrom saves state/cleans up  
       Sub-checks: Parameters via NavigationContext. Minimal load: no full data fetch if cached.  
       Evidence:
- [ ] No duplicate load patterns (avoid view Loaded for data fetch when navigation already triggers it)  
       Sub-checks: Migrate all from Loaded to OnNavigatedTo.  
       Evidence:
- [ ] Back/forward journaling behaves as intended (or is explicitly disabled)  
       Sub-checks: Journal.GoBack(); test state restoration. Disable via IRegionNavigationJournal if needed.  
       Evidence:

## 12) Logging, diagnostics, and telemetry - Important (expanded with context requirements)

- [ ] Key actions and failures logged (e.g., Serilog) with context (user, route, correlation id)  
       Sub-checks: Logger.Information("Action {Action} by {UserId}", action, userId);  
       Evidence:
- [ ] Binding and navigation issues traceable (enable PresentationTraceSources as needed in dev builds)  
       Sub-checks: #if DEBUG PresentationTraceSources.SetTraceLevel(element, High); #endif  
       Evidence:
- [ ] Non-PII telemetry only; opt-in/consent respected  
       Sub-checks: Anonymize data; check AppSettings for consent.  
       Evidence:

## 13) Security and privacy - Important (expanded with input sanitization examples)

- [ ] No secrets in code or XAML; configuration pulled from secure sources  
       Sub-checks: Use IConfiguration/Secrets Manager. No hard-coded keys.  
       Evidence:
- [ ] User input sanitized where applicable; file/URL access constrained  
       Sub-checks: HtmlEncode inputs; Path.GetFullPath for files.  
       Evidence:
- [ ] Clipboard and file export guarded; sensitive info masked/redacted  
       Sub-checks: Mask passwords; confirm export dialogs.  
       Evidence:

## 14) Testing (unit, integration, UI) - Important (expanded with coverage thresholds)

- [ ] ViewModel unit tests cover core logic, commands, validation, and state transitions  
       Sub-checks: xUnit/Moq; 80% coverage. Test CanExecute, async paths.  
       Evidence:
- [ ] Converter tests (for each non-trivial converter)  
       Sub-checks: Test Convert/ConvertBack edge cases.  
       Evidence:
- [ ] Integration tests cover navigation, region injection, and data flows  
       Sub-checks: Prism container mocks; test RequestNavigate.  
       Evidence:
- [ ] UI/Automation tests for critical flows (smoke/E2E) run in CI  
       Sub-checks: Use CodedUI or Appium; CI yaml task.  
       Evidence:

## 15) Documentation & discoverability - Nice-to-have (expanded with template requirements)

- [ ] XML doc comments for public VM members; inline comments where intent is non-obvious  
       Sub-checks: /// <summary>Loads AI suggestions.</summary>  
       Evidence:
- [ ] README or view-level notes describing purpose, data contract, and known constraints  
       Sub-checks: View.md: "Purpose: AI chat panel; Inputs: Query string; Outputs: Responses."  
       Evidence:
- [ ] Support links to relevant control docs (Syncfusion) and Microsoft guidance  
       Sub-checks: Inline links with versions.  
       Evidence:

## 16) Build & CI quality gates - Important (expanded with CI parsing details)

- [ ] Build passes locally and in CI; warnings reviewed (treat as errors where feasible)  
       Sub-checks: /warnaserror in csproj.  
       Evidence:
- [ ] Static analysis/linters (style, analyzers) pass  
       Sub-checks: StyleCop, Roslyn analyzers.  
       Evidence:
- [ ] Test tasks (unit/UI) wired into CI; coverage thresholds met  
       Sub-checks: dotnet test --collect:"XPlat Code Coverage" >80%.  
       Evidence:
- [ ] Checklist automation: CI parses the per-view template in PRs and enforces thresholds (e.g., fail if < configured completion %, or if any critical item fails).  
       Sub-checks: Use regex in CI script to extract score from PR body.  
       Evidence:
- [ ] Evidence links (files/lines, screenshots) included in PR description for auditability.  
       Evidence:

## 17) Resource lifecycle & cleanup - Critical (expanded with leak detection)

- [ ] Disposables disposed (IDisposable/IAsyncDisposable) and CancellationTokenSource canceled  
       Sub-checks: using() or explicit Dispose. CTS.Cancel() then Dispose().  
       Evidence:
- [ ] EventAggregator subscriptions removed (or rely on weak refs with conscious tradeoff)  
       Sub-checks: Keep token = Subscribe(); Unsubscribe(token). Test weak: GC.Collect and check.  
       Evidence:
- [ ] Region clean-up on navigation away (detach heavy child controls, stop timers, clear large collections if needed)  
       Sub-checks: In OnNavigatedFrom: collection.Clear(); control = null.  
       Evidence:

## 18) Configuration & environment - Nice-to-have (expanded with flag examples)

- [ ] Settings read via configuration service/Options; no hard-coded environment switches  
       Sub-checks: IOptions<AppSettings>.Value.ApiUrl.  
       Evidence:
- [ ] Feature flags/toggles supported where applicable  
       Sub-checks: FeatureManager.IsEnabledAsync("AIAssist").  
       Evidence:

## 19) Error handling & resiliency - Important (expanded with retry policies)

- [ ] Network/service errors surfaced with retry/backoff when appropriate  
       Sub-checks: Polly: Policy.Handle<HttpRequestException>().WaitAndRetry(3, \_ => TimeSpan.FromSeconds(2)).  
       Evidence:
- [ ] Fallback UI for unavailable services; user clear next steps  
       Sub-checks: Offline mode panel: "Retry or work offline."  
       Evidence:

## 20) Custom controls & third-party integrations - Important (expanded with mini-matrix template)

- [ ] For non-Syncfusion/custom controls, a mini-matrix is provided with links to official docs (or internal design docs) covering API usage, theming, and accessibility.  
       Sub-checks: Matrix: Prop: Foo (doc link: quote); Event: Bar (usage: as per sample).  
       Evidence:
- [ ] Theming integration verified: resources, styles, and behaviors align with app dictionaries; dynamic resources are used where live updates are expected.  
       Sub-checks: {DynamicResource CustomBrush}. Test theme change.  
       Evidence:
- [ ] Accessibility verified: roles, names, and states exposed via UIA; keyboard navigation and focus behavior tested.  
       Sub-checks: Inspect.exe verification.  
       Evidence:
- [ ] Performance characteristics documented and enabled (virtualization, deferred loading, caching) where applicable.  
       Sub-checks: Quote doc perf section; enable flags.  
       Evidence:

## Syncfusion control checklist (example: SfDataGrid) - Critical (expanded to full doc coverage)

Replace with specific control(s) in view. For each, create a sub-section reviewing ALL doc areas.

- [ ] Assemblies referenced: Syncfusion.Data.WPF, Syncfusion.SfGrid.WPF, Syncfusion.Shared.WPF (and converters for export if used)  
       Evidence:
- [ ] ItemsSource bound to IEnumerable/ObservableCollection; auto-generated columns reviewed (or defined explicitly)  
       Sub-checks: Quote binding section: "ItemsSource must implement IEnumerable." Customize in AutoGeneratingColumn.  
       Evidence:
- [ ] Performance: virtualization on; sorting/grouping/filtering configured with event handlers only when needed  
       Sub-checks: Review full perf doc: AllowRowVirtualization=True; QueryRowHeight for custom.  
       Evidence:
- [ ] Editing: AllowEditing/Deleting/AddNewRowPosition configured per requirements; validation integrated with VM  
       Sub-checks: Quote editing section: BeginEditCommand binds to VM. Integrate with INDEI.  
       Evidence:
- [ ] Theme: applied via SfSkinManager; consistent with app  
       Sub-checks: Quote themes: SetVisualStyle="MaterialLight".  
       Evidence:
- [ ] Full features review: Binding (all modes), Columns (types, formatting), Filtering (advanced filters), Grouping (multi-group), Sorting (custom comparers), Exporting (to Excel/PDF, options), Accessibility (ARIA roles), Known Issues (workarounds if any).  
       Sub-checks: For each used feature, quote doc and confirm exact usage. E.g., Filtering: Use FilterChanged event as per doc, not custom.  
       Evidence:

> For SfAIAssistView: Expand similarly—review ALL sections: Overview, Getting Started, Data Binding, Commands/Events (e.g., QuerySubmitted), Styling/Theming, Accessibility, Performance Optimizations, Localization, etc. Quote for each: "QuerySubmitted event: Raised when user submits; handler must be async if needed per doc."

---

## Scoring rubric (unchanged but enforced strictly)

- Critical (Architecture, Binding, Validation, Navigation, Performance, Accessibility, Syncfusion specifics): 5 pts each item
- Important (UX/Design, Commands, Async/Cancellation, Logging, Security, Testing, Cleanup): 3 pts each item
- Nice-to-have (Documentation, Config, others): 1-2 pts per item

Compute: (Points achieved) / (Points applicable) = Completion %.  
Gate: Ship-ready ≥ 90%; Beta ≥ 80% (no critical fails); Dev <80%. Deduct for any doc deviation.

---

## Wiley Widget "icing on the cake" (repo-specific checks) - Nice-to-have (expanded with code snippets)

These ensure consistency with repo patterns. Enforce exact usage from Services/Behaviors.

1. Theming and resources (Syncfusion + app dictionaries)

- [ ] Global theme is respected: avoid hard-coding syncfusionskin:SfSkinManager.VisualStyle="..." in XAML unless an intentional override is documented. Prefer the app-wide SfSkinManager.ApplicationTheme managed by ThemeManager.  
       Sub-checks: ThemeManager.SetTheme(Application.Current, "WileyDark");  
       Evidence:
- [ ] If a per-control override is truly required, bind or apply through ThemeManager.ApplyThemeToControl(...) or use ThemeUtility.ToVisualStyle(...) to avoid drift.  
       Evidence:
- [ ] View styles/colors come from Themes/WileyTheme.xaml and Themes/Generic.xaml via (Dynamic)Resource, not hard-coded brushes; use DynamicResource for anything that should live-update on theme change.  
       Sub-checks: Background="{DynamicResource PrimaryBackgroundBrush}"  
       Evidence:
- [ ] Converters are sourced from Themes/Generic.xaml (no duplicate local declarations); keys and pack URIs are correct.  
       Evidence:
- [ ] If the view depends on theme at runtime (e.g., cached visuals), it listens to ThemeManager.ThemeChanged and updates accordingly (or uses only dynamic resources).  
       Sub-checks: ThemeManager.ThemeChanged += OnThemeChanged;  
       Evidence:
- [ ] Syncfusion license registration verified as per version guidance in app startup.  
       Evidence:

2. Behaviors and focus UX

- [ ] Initial focus is intentional using FocusOnLoadBehavior (root or a specific TargetName); avoid competing focus behaviors on the same control.  
       Sub-checks: <i:Interaction.Behaviors> <behaviors:FocusOnLoadBehavior /> </i:Interaction.Behaviors>  
       Evidence:
- [ ] MouseFocusBehavior is used where click-to-prime keyboard input improves UX, but does not steal focus from more important flows.  
       Evidence:
- [ ] No redundant event handlers for focus if the behavior already provides it (keep code-behind thin).  
       Evidence:

3. Prism regions and docking

- [ ] Region name used in XAML matches the canonical name (no typos); only one instance per intended region.  
       Evidence:
- [ ] If using Docking-based layouts, RegionManagerBehavior is attached where appropriate so regions are initialized and added to DockingManager when missing.  
       Sub-checks: <DockingManager behaviors:RegionManagerBehavior.RegionName="Foo" />  
       Evidence:
- [ ] Navigation responsibilities are not split across Loaded and INavigationAware (no double loads).  
       Evidence:

4. Accessibility and text/UI polish

- [ ] AutomationProperties.Name/HelpText assigned to key inputs and buttons; labels use LabeledBy where appropriate (especially for custom/Syncfusion controls).  
       Evidence:
- [ ] Microcopy uses consistent terminology; headers/subheaders/buttons align with WileyTheme text styles (Header/Subheader/Body).  
       Sub-checks: <TextBlock Style="{StaticResource HeaderTextStyle}" />  
       Evidence:
- [ ] Card/Panel visuals use the shared styles (e.g., Card Border style) rather than ad-hoc borders.  
       Evidence:

5. Performance and cleanup niceties

- [ ] Long-running work is cancellable; tokens are disposed on navigation away.  
       Evidence:
- [ ] EventAggregator subscriptions are cleaned up (or intentionally weak); timers/streams are disposed.  
       Evidence:
- [ ] ItemsControls use virtualization where applicable; avoid unnecessary bindings in item templates.  
       Evidence:

Notes:

- Central theming: Services/ThemeManager.cs and Services/ThemeUtility.cs govern theme selection, persistence, and conversion to Syncfusion Theme/VisualStyles. Prefer these over per-view theme logic.
- App resources: App.xaml merges Themes/Generic.xaml and Themes/WileyTheme.xaml; use these dictionaries for shared colors/typography/converters.
- Region initialization: Behaviors/RegionManagerBehavior.cs coordinates Prism regions with DockingManager when needed.

---

## Per-view evaluation template (copy/paste) - Expanded for granularity

View name:  
Owner:  
Date:  
Module/Region:

For each section: [x] Pass | [ ] Needs work (detail gaps) | [N/A] (justify)  
Notes/Evidence (mandatory: links, quotes, screenshots):

- Architecture & MVVM:
- Data binding:
- Commands/Input:
- Validation/Error UX:
- UX/Design/Theming:
- Accessibility:
- Layout/Responsive:
- Async/Cancellation:
- Performance:
- Syncfusion specifics:
- Navigation/Regions:
- Logging/Telemetry:
- Security/Privacy:
- Testing:
- Documentation:
- Build & CI:
- Cleanup/Lifecycle:
- Config/Environment:
- Error handling/Resiliency:
- Custom/Third-party:

Summary score: X% (show calculation)  
Critical items failing: List  
Top 3 gaps: 1. ... 2. ... 3. ...

---

## Quick-pass examples for this repo (updated with AI Assist findings)

- AIAssistPanelView (Syncfusion AI Assist UI)
  - Architecture & MVVM: Pass - ViewModel autowiring enabled; module registration points to panel view. But expand: Verified no code-behind logic; all in VM.
  - Data binding: Needs work - Messages/CurrentUser bindings in place, but check UpdateSourceTrigger for chat input (should be PropertyChanged per live UX).
  - Validation: Needs work - Add INDEI for non-empty/max length; quote MS validation doc.
  - Accessibility: Needs work - Add AutomationProperties; test with Narrator.
  - Navigation/Regions: Pass - Registered to AI region; INavigationAware cleans up. But verify no double loads.
  - Syncfusion specifics: Needs work - For SfAIAssistView, review FULL docs: Events like QuerySubmitted handled as per API ref (quote: "async handler recommended"); performance opts like lazy loading enabled?
  - Cleanup: Pass - Timers cancel on navigate.  
    Action: Re-run full checklist; address rendering issues (e.g., if not rendering, check DataContext flow and binding errors in output).

- Dashboard views
  - Timer ownership centralized in ViewModel and disposed; duplicate loads reduced.
  - Follow through on remaining Loaded-to-VM lifecycle migrations and add cancellation for any async data loads. Expand: Test journaling for state persistence.

> Use as starting point; complete full template with evidence/scores. For rendering issues in AI Assist, specifically trace bindings (section 2) and theming (section 5/10)—likely a missed DynamicResource or undocumented Syncfusion prop.

---

## Evaluated views (running list)

- View: AIAssistPanelView  
  Module/Region: AIAssistModule / AIAssistRegion  
  Date: 2025-10-20  
  Result: Beta (≈85%) - Rendering issues indicate incomplete theming/validation.  
  Evidence:
  - View: src/Views/AIAssistPanelView.xaml (lines 10-50: bindings)
  - ViewModel: src/ViewModels/AIAssistViewModel.cs (line 100: async query)
  - Tests: tests/WileyWidget.LifecycleTests/AIAssistLifecycleTests.cs  
    Notes: Expanded review revealed missed async validation debounce; fix per section 4. Syncfusion: Quoted full events doc—no deviations.

- **Modern WPF/Prism Updates**: References are solid but static. Input: Add a maintenance note to periodically validate against evolving docs (e.g., Prism 8+ changes to IContainerRegistry or WPF .NET 9 previews). Include emerging topics like MAUI/WPF interop if relevant to the repo. Enforcement: Schedule quarterly doc refresh in CI or tasks.
