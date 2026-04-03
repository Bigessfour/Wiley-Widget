**WileyWidget Panel Definition Of Done Checklist**
**Syncfusion WinForms v33.1.44 + Sacred Panel Skeleton Standard**

Use this checklist for each in-scope panel before calling that panel complete.

### 1. Construction And Sacred Panel Skeleton

- [x] Inherits `ScopedPanelBase<TViewModel>` or `ScopedPanelBase` for host panels
- [x] Constructor is `public XXXPanel(XXXViewModel vm, SyncfusionControlFactory factory)`
- [x] Uses `SafeSuspendAndLayout(InitializeLayout)`
- [x] `PanelHeader` created via `_factory` with title, refresh, and close wiring
- [x] Root `_content = new TableLayoutPanel { Dock = DockStyle.Fill }`
- [x] `_loader = _factory.CreateLoadingOverlay()` with `Dock = Fill` and `Visible = false`
- [x] All Syncfusion controls are created via `_factory.CreateXXX(...)`
- [x] No manual `.ThemeName`, no child-level `SfSkinManager.SetVisualStyle`, and no hard-coded colors
- [x] `AutoScaleMode = AutoScaleMode.Dpi` and `MinimumSize = RecommendedDockedPanelMinimumLogicalSize`
- [x] `OnHandleCreated` enforces minimum size and calls `PerformLayout()`

### 2. Data Loading And Display

- [x] `LoadAsync(CancellationToken ct)` calls the view model command and shows `_loader`
- [x] All intended records for the fiscal year are loaded
- [x] `SfDataGrid` binds to the full filtered collection with `EnableDataVirtualization = true` (N/A for this read-only dashboard panel)
- [x] Grid columns use auto-sizing and sensible minimum widths so resizing does not clip content (N/A for this read-only dashboard panel)
- [x] Full data appears within the expected panel-load budget
- [x] `NoDataOverlay` appears when the collection is empty

### 3. CRUD Behaviors

- [x] Add, edit, and delete operations update the grid immediately (N/A for this read-only dashboard panel)
- [x] `ApplyFiltersAsync()` runs automatically after changes where needed (N/A for this read-only dashboard panel)
- [x] Grid refreshes and collection notifications keep the visible state consistent (N/A for this read-only dashboard panel)
- [x] New rows appear immediately in the expected location (N/A for this read-only dashboard panel)
- [x] Delete confirmation removes the row without a manual refresh (N/A for this read-only dashboard panel)
- [x] Bulk operations refresh visible results immediately (N/A for this read-only dashboard panel)

### 4. UI Responsiveness

### 5. Visual And UX Compliance

[ ] Resize, dock, and undock operations do not clip controls
[ ] Loading overlay appears promptly for long-running work

- [x] Standard control sizing is respected
- [x] Actions and summary surfaces are balanced and readable

### 6. Lifecycle And ICompletablePanel

- [x] `ValidateAsync()` returns a meaningful result
- [x] `SaveAsync()` is either a safe no-op or delegates correctly
- [x] `FocusFirstError()` focuses the correct control
- [x] Dispose cleanup unsubscribes events and disposes bindings or grids safely
- [x] Supports delayed load patterns where applicable

### 7. Theme Switchability

- [x] Panel responds correctly to global theme changes
- [x] Controls refresh through theme cascade without restart
- [x] Open panels visually refresh within the expected budget
- [x] Hosted content also respects global theme changes

### 8. Error Handling And UX

- [x] User-facing error messages are clear
- [x] Keyboard shortcuts work where expected (N/A for this read-only dashboard panel)
- [x] Interactive controls provide plain-language affordances
- [x] Export and import flows provide progress feedback (N/A for this read-only dashboard panel)
- [ ] Panel can be closed cleanly during loading when cancellation is expected

### 9. Host-Control Exception Path

FormHostPanel and RatesPage are intentional exceptions to the standard `ScopedPanelBase` panel skeleton because they host existing Forms rather than compose a normal panel surface. The exemption is documented in the DI registration and related navigation tests.

- [x] Applies only to panels whose primary content is hosted rather than a standard Syncfusion CRUD surface
- [x] Any checklist exemptions are explicit and justified
- [x] Host-side WinForms layout still uses the required base patterns
- [x] Async initialization supports cancellation without blocking calls
- [x] Hosted control participates in global theme behavior
- [ ] Error fallback UI and disposal behavior remain accessible and clean

## Release Use

When all applicable items are checked, mark the panel as Certified.

For release planning, every in-scope panel should be in one of three states:

- Certified
- Known Limitation
- Deferred

Do not use one panel passing this checklist as proof that the entire product is release-ready.
