**WileyWidget Panel Definition Of Done Checklist**
**Syncfusion WinForms v32.2.3 + Sacred Panel Skeleton Standard**

Use this checklist for each in-scope panel before calling that panel complete.

### 1. Construction And Sacred Panel Skeleton

- [ ] Inherits `ScopedPanelBase<TViewModel>` or `ScopedPanelBase` for host panels
- [ ] Constructor is `public XXXPanel(XXXViewModel vm, SyncfusionControlFactory factory)`
- [ ] Uses `SafeSuspendAndLayout(InitializeLayout)`
- [ ] `PanelHeader` created via `_factory` with title, refresh, and close wiring
- [ ] Root `_content = new TableLayoutPanel { Dock = DockStyle.Fill }`
- [ ] `_loader = _factory.CreateLoadingOverlay()` with `Dock = Fill` and `Visible = false`
- [ ] All Syncfusion controls are created via `_factory.CreateXXX(...)`
- [ ] No manual `.ThemeName`, no child-level `SfSkinManager.SetVisualStyle`, and no hard-coded colors
- [ ] `AutoScaleMode = AutoScaleMode.Dpi` and `MinimumSize = RecommendedDockedPanelMinimumLogicalSize`
- [ ] `OnHandleCreated` enforces minimum size and calls `PerformLayout()`

### 2. Data Loading And Display

- [ ] `LoadAsync(CancellationToken ct)` calls the view model command and shows `_loader`
- [ ] All intended records for the fiscal year are loaded
- [ ] `SfDataGrid` binds to the full filtered collection with `EnableDataVirtualization = true`
- [ ] Grid columns use auto-sizing and sensible minimum widths so resizing does not clip content
- [ ] Full data appears within the expected panel-load budget
- [ ] `NoDataOverlay` appears when the collection is empty

### 3. CRUD Behaviors

- [ ] Add, edit, and delete operations update the grid immediately
- [ ] `ApplyFiltersAsync()` runs automatically after changes where needed
- [ ] Grid refreshes and collection notifications keep the visible state consistent
- [ ] New rows appear immediately in the expected location
- [ ] Delete confirmation removes the row without a manual refresh
- [ ] Bulk operations refresh visible results immediately

### 4. UI Responsiveness

- [ ] First paint and data load meet the panel budget
- [ ] Refresh actions meet the expected response budget
- [ ] Filter and search interactions stay within the expected debounce budget
- [ ] Resize, dock, and undock operations do not clip controls
- [ ] Loading overlay appears promptly for long-running work
- [ ] Interactive controls remain responsive during background work

### 5. Visual And UX Compliance

- [ ] Layout spacing is consistent
- [ ] Standard control sizing is respected
- [ ] Actions and summary surfaces are balanced and readable
- [ ] Helpful tooltips exist where users need them
- [ ] Semantic colors are only used where they carry meaning
- [ ] Theme behavior works through `SfSkinManager` cascade only
- [ ] No manual `BackColor`, `ForeColor`, or `Font` assignments beyond approved exceptions

### 6. Lifecycle And ICompletablePanel

- [ ] `ValidateAsync()` returns a meaningful result
- [ ] `SaveAsync()` is either a safe no-op or delegates correctly
- [ ] `FocusFirstError()` focuses the correct control
- [ ] Dispose cleanup unsubscribes events and disposes bindings or grids safely
- [ ] Supports delayed load patterns where applicable

### 7. Theme Switchability

- [ ] Panel responds correctly to global theme changes
- [ ] Controls refresh through theme cascade without restart
- [ ] Open panels visually refresh within the expected budget
- [ ] Hosted content also respects global theme changes

### 8. Error Handling And UX

- [ ] User-facing error messages are clear
- [ ] Keyboard shortcuts work where expected
- [ ] Interactive controls provide plain-language affordances
- [ ] Export and import flows provide progress feedback
- [ ] Panel can be closed cleanly during loading when cancellation is expected

### 9. Host-Control Exception Path

- [ ] Applies only to panels whose primary content is hosted rather than a standard Syncfusion CRUD surface
- [ ] Any checklist exemptions are explicit and justified
- [ ] Host-side WinForms layout still uses the required base patterns
- [ ] Async initialization supports cancellation without blocking calls
- [ ] Hosted control participates in global theme behavior
- [ ] Error fallback UI and disposal behavior remain accessible and clean

## Release Use

When all applicable items are checked, mark the panel as Certified.

For release planning, every in-scope panel should be in one of three states:

- Certified
- Known Limitation
- Deferred

Do not use one panel passing this checklist as proof that the entire product is release-ready.
