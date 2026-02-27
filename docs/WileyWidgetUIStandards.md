# WileyWidget Canonical UI Standardization Guide v2026

## What Right Looks Like — Municipal Finance Edition

Built on Microsoft Windows User Interface Principles + Syncfusion WinForms + ScopedPanelBase.

## Core Philosophy (Microsoft + Wiley)

We follow Microsoft’s foundational UX principles and adapt them to municipal finance workflows where speed, trust, and consistency matter.

| Microsoft Principle | Wiley Widget Translation                                           |
| ------------------- | ------------------------------------------------------------------ |
| Consistency         | Every panel looks and behaves identically                          |
| Directness          | One click to the most important action                             |
| Forgiveness         | Undo-oriented flows, explicit confirmation for destructive actions |
| Feedback            | Loading overlay + progress + non-disruptive notifications          |
| Aesthetics          | Syncfusion theme + PanelHeader + clean spacing                     |
| Simplicity          | No fluff panels, clear headers, conversational text                |

## 1) The Sacred Panel Skeleton

Copy this pattern for every new panel.

```csharp
public partial class MyFinancePanel : ScopedPanelBase, ICompletablePanel
{
    private readonly MyViewModel _vm;
    private readonly SyncfusionControlFactory _factory;
    private PanelHeader _header = null!;
    private TableLayoutPanel _content = null!;
    private LoadingOverlay _loader = null!;

    public MyFinancePanel(MyViewModel vm, SyncfusionControlFactory factory) : base()
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        SafeSuspendAndLayout(InitializeLayout);
        BindViewModel();
    }

    private void InitializeLayout()
    {
        Dock = DockStyle.Fill;
        MinimumSize = new Size(1024, 720);

        _header = new PanelHeader(_factory) { Title = "Municipal Accounts — FY 2026" };
        _header.RefreshClicked += (s, e) => _vm.RefreshCommand.Execute(null);

        _content = new TableLayoutPanel { Dock = DockStyle.Fill };
        _loader = new LoadingOverlay { Dock = DockStyle.Fill, Visible = false };

        Controls.Add(_header);
        Controls.Add(_content);
        Controls.Add(_loader);
    }
}
```

## 2) Mandatory Rules (Microsoft → Wiley Mapping)

1. **Use Standards** → Always inherit `ScopedPanelBase`, use `PanelHeader`, `SyncfusionControlFactory`, and `DockStyle.Fill` for hero panels.
2. **Draw Attention to Important Buttons** → Default focus on primary action (for example, Save Changes). Non-critical actions can use links.
3. **Simplify Recognition with Icons** → All action buttons should use approved resource icons.
4. **Simplify Recognition with Headers** → `PanelHeader` is required on every user-facing panel.
5. **Use Custom Message Boxes** → Standardize dialogs through a shared dialog service (`CustomDialogService` target).
6. **Include Alternate Commands** → Add keyboard shortcuts and right-click context menus for grids/lists.
7. **Handle Critical Actions** → Explicit confirmation + safe default + cancellation-first behavior.
8. **RadioButtons vs ComboBox** → For <=4 exclusive options, prefer radio buttons.
9. **Never Disrupt the User** → Favor non-blocking notifications during normal workflows.
10. **Provide Progress Status** → Loading overlay + status text + marquee for unknown-duration operations.
11. **Simplify Complex Steps with Wizards** → Use wizard-style flow for high-control-count scenarios.
12. **Get the Tone of Your Text Right** → Conversational, plain language copy.
13. **Sometimes a ListView is Better** → Prefer structured data controls (for example, `SfDataGrid`) over ambiguous list controls.
14. **Use Pretty Graphics** → Use only approved Syncfusion/FlatIcons resources.
15. **Provide Resizable Forms** → All panels/forms must have sensible minimum size and resize behavior.
16. **Provide More Functionality with Sidebars** → Support right-side utility/task surfaces where appropriate.
17. **Give a Notification Choice** → Frequent informational notifications should support user suppression.
18. **Provide Tooltips** → Every primary interactive control should have tooltip guidance.
19. **Do Not Forget the Little Things** → Consistent spacing, anchoring, keyboard flow, and no clipped controls.

## 3) Layout & Rendering Rules

```csharp
protected override void OnHandleCreated(EventArgs e)
{
    base.OnHandleCreated(e);
    MinimumSize = new Size(1024, 720);
    PerformLayout();
    Invalidate(true);
}

protected void SafeSuspendAndLayout(Action build)
{
    SuspendLayout();
    try { build(); }
    finally
    {
        ResumeLayout(false);
        PerformLayout();
    }
}
```

### Canonical Sizing Properties (All Panels)

Use this property baseline on every panel/control surface to prevent clipped controls and layout drift:

- `Dock = DockStyle.Fill` for root panel containers.
- `AutoScaleMode = AutoScaleMode.Dpi` for DPI-safe rendering.
- `MinimumSize` set to a role-based floor:
  - Docked/root panels: `1024x720` logical
  - Embedded/tab panels: `960x600` logical
  - Dialog-hosted panels: `760x640` logical (or larger when required by content)
- `AutoScroll = true` for embedded/tab surfaces that can host fixed-width/fixed-column content.
- Prefer layout containers (`TableLayoutPanel`, `FlowLayoutPanel`) over manual pixel positioning for actions.
- Avoid all-absolute row/column sizing in complex layouts; keep at least one `Percent` stretch region.

Implementation note: these defaults are centralized in `ScopedPanelBase` via
`RecommendedDockedPanelMinimumLogicalSize`, `RecommendedEmbeddedPanelMinimumLogicalSize`,
and `RecommendedDialogPanelMinimumLogicalSize`.

## 4) Ribbon & Navigation Rules

- Home tab: Enterprise Vital Signs as primary surface; JARVIS Chat as right-side companion.
- Other panels default to right dock unless explicitly documented otherwise.
- Button text must lead with action verbs (for example, Refresh Data, Export to PDF).

## 5) Text & Tone Standards

- Buttons: Save Changes, Cancel, Delete Selected.
- Labels: Total Revenue This Quarter.
- Tooltips: Exports current view as PDF for council packet.

## 6) Syncfusion-Specific Enforcement

- Create Syncfusion controls via `SyncfusionControlFactory` unless explicit checklist exceptions are met.
- Keep `SfSkinManager` as the single source of truth for theming.
- Set `ThemeName` consistently for dynamically created Syncfusion controls.
- When changing Syncfusion control behavior/configuration, validate API usage against current Syncfusion documentation and local Essential Studio samples.

## 7) Team Enforcement Workflow

1. **Design pass**: map panel requirements to this guide before coding.
2. **Implementation pass**: use panel skeleton and mandatory rules.
3. **Review pass**: verify against checklist (header, sizing, keyboard, feedback, tooltips, tone).
4. **Validation pass**: run build/tests and resolve panel-level regressions before merge.

## 8) Definition of Done for Any User-Facing Panel

A panel is done only when all are true:

- Uses `ScopedPanelBase` lifecycle and `SafeSuspendAndLayout`.
- Includes `PanelHeader` with consistent action behavior.
- Uses approved spacing and non-clipping resize behavior.
- Implements user feedback for loading/progress/error states.
- Provides keyboard alternatives and tooltips.
- Aligns text/tone with plain-language standards.
- Uses Syncfusion theme/factory rules with no competing styling system.

---

This document is the canonical reference for WileyWidget UI consistency across user-facing surfaces.
