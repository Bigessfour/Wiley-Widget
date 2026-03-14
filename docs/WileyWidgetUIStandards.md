# WileyWidget Canonical Panel UX Checklist

**Version:** 2026-03-10

This document is the canonical single source of truth for evaluating the design quality of WileyWidget WinForms panels.

It is grounded in Microsoft Windows app design guidance and adapted for WinForms + Syncfusion surfaces. The goal is not novelty. The goal is a panel that feels obvious, trustworthy, balanced, readable, accessible, and professionally designed.

## Source Foundation

Primary Microsoft guidance used to build this checklist:

- Windows apps user interface hub: <https://learn.microsoft.com/en-us/windows/apps/develop/user-interface>
- Forms: <https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/forms>
- Content layout and spacing: <https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics>
- Typography in Windows: <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography>
- Color in Windows: <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/color>
- Theming in Windows apps: <https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming>
- Commanding basics: <https://learn.microsoft.com/en-us/windows/apps/design/basics/commanding-basics>
- Navigation basics: <https://learn.microsoft.com/en-us/windows/apps/design/basics/navigation-basics>
- Accessibility checklist: <https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist>
- Keyboard accessibility: <https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility>
- Accessible text requirements: <https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements>
- Desktop layout guidance for scan path, focus, and resizability: <https://learn.microsoft.com/en-us/windows/win32/uxguide/vis-layout>

## How To Use This Checklist

Audit every user-facing panel from top to bottom.

- Mark each item `Pass`, `Fail`, or `N/A`.
- Any failure in theme, accessibility, resize behavior, or critical task clarity blocks release.
- Treat this as an evaluation standard first and an implementation guide second.
- If Microsoft guidance conflicts with a legacy panel, change the panel unless there is a documented business reason not to.

## What Professional Looks Like

A panel feels professionally designed when all of the following are true:

- The user knows what this panel is for within 3 seconds.
- There is one obvious focal point and one obvious next action.
- The layout feels calm, aligned, and intentional instead of crowded or empty.
- Text is readable without squinting and without decorative noise.
- Important actions are easy to find, but secondary actions do not fight for attention.
- The panel survives light theme, dark theme, high contrast, resize, DPI scaling, and keyboard-only use.
- Errors, loading, empty states, and confirmations feel deliberate rather than bolted on.

## Measurement Baseline

Use these Microsoft-derived values as the default visual rhythm unless a control's built-in chrome requires a close equivalent.

### Spacing

- `8 px` between peer buttons.
- `8 px` between a control and its immediate header when using compact command/header layouts.
- `12 px` between a label and its input control.
- `12 px` between adjacent content areas or cards.
- `16 px` minimum interior gutter between surface edge and content.
- `24 px` vertical spacing between individual form inputs.
- `48 px` vertical spacing between groups of related form inputs.
- `5 px` minimum gap between interactive controls if they are not touching.

### Typography

- Caption: `12/16`
- Body: `14/20`
- Body Strong: `14/20` semibold
- Body Large: `18/24`
- Subtitle: `20/28` semibold
- Title: `28/36` semibold

### Text Rules

- Default to left alignment.
- Use sentence case for titles, labels, and buttons.
- Do not go smaller than `12 px` regular or `14 px` semibold for meaningful UI text.
- Keep long reading lines around `50-60` characters when possible.

### Input Size And Hit Targets

- Treat `16 x 16` as the minimum practical interactive target.
- Microsoft form examples use `80 px` tall input rows. In WinForms, preserve the same roomy rhythm even if the native or Syncfusion control chrome is shorter.

## Step-By-Step Panel Audit Checklist

### 1. Purpose And First Impression

- [ ] The panel has one clearly stated job.
- [ ] The panel title tells the user what they are viewing, not just the feature name.
- [ ] The top section communicates the current context, such as entity, period, account, or record scope.
- [ ] The user can identify the primary action without scanning the whole surface.
- [ ] The panel does not open with multiple competing focal points.
- [ ] The initial state is useful above the fold and gives the user a reason to continue.

### 2. Visual Hierarchy And Scan Path

- [ ] The most important interactive element is in the upper-left, upper-center, or primary scan path.
- [ ] Commit actions are placed in the lower-right area or other clearly conventional completion zone.
- [ ] Frequently used actions appear before infrequently used actions.
- [ ] Required workflow steps are in the main visual flow.
- [ ] Optional or advanced settings are outside the primary flow or progressively disclosed.
- [ ] The panel avoids putting critical information in the lower-left corner or below long scroll regions.
- [ ] There is one obvious focal point, not several equally emphasized regions.

### 3. Layout, Alignment, And Spacing

- [ ] The root surface is aligned to a clear grid or layout container.
- [ ] Controls line up cleanly across rows and columns.
- [ ] Labels, inputs, helper text, and validation messages follow a consistent alignment rule.
- [ ] Text fields and selectors are not staggered without purpose.
- [ ] Related controls are visually grouped.
- [ ] Unrelated controls are clearly separated.
- [ ] Spacing is generous enough to feel calm but not so loose that the panel feels unfinished.
- [ ] There are no accidental dead zones between closely related controls.
- [ ] Numeric data is right-aligned where comparison matters.
- [ ] Standard buttons on the same surface use one or two consistent widths instead of random sizes.
- [ ] The surface looks balanced, with no large awkward voids and no cramped islands of UI.

### 4. Typography And Copy

- [ ] The panel uses one primary UI font family, aligned with Windows defaults.
- [ ] Page title, section headings, labels, body text, and captions use a consistent type ramp.
- [ ] Headings are semibold, not oversized for the sake of drama.
- [ ] Body text is readable at normal viewing distance.
- [ ] Labels are concise and specific.
- [ ] Buttons use action verbs such as `Save`, `Refresh`, `Apply`, `Export`, or `Cancel`.
- [ ] Copy is in sentence case, not title case shouting or all caps.
- [ ] Static explanatory text is short and skimmable.
- [ ] Long instructions are broken into short chunks instead of walls of text.
- [ ] Truncation is controlled. Use wrapping first where appropriate, then clipping or ellipsis only when necessary.
- [ ] Critical text is never clipped where it must be read to complete a task.

### 5. Form Design And Field Choice

- [ ] Each form field has a visible label.
- [ ] Labels are placed above inputs by default.
- [ ] Placeholder text is used only as a hint, never as the only label.
- [ ] Required fields are explicitly marked.
- [ ] The panel uses the correct control for the data type instead of forcing text entry everywhere.
- [ ] Dates use date pickers when format accuracy matters.
- [ ] Times use time pickers when format accuracy matters.
- [ ] Bounded choice uses a selector, not freeform text.
- [ ] Binary choice uses a checkbox or toggle, not a drop-down.
- [ ] Exclusive small choice sets use radio buttons.
- [ ] Longer choice sets use a combo box, list, or picker.
- [ ] The submit action is disabled or guarded until required data is valid.
- [ ] Invalid fields are clearly identified in context.
- [ ] Validation feedback appears near the offending field.
- [ ] Form groups are separated by visible spacing and heading structure, not by guesswork.
- [ ] Multi-column forms are used only when the screen size and content density support them.
- [ ] If the panel would overwhelm the user as a single form, it is broken into sections, steps, tabs, or pages.

### 6. Commanding And Action Placement

- [ ] Primary actions are always visible.
- [ ] Secondary actions are available without overpowering the primary path.
- [ ] Dangerous actions are visually separated from normal actions.
- [ ] Reversible actions prefer undo over intrusive confirmation.
- [ ] Irreversible or high-consequence actions require confirmation.
- [ ] Common commands are placed on the canvas near the content they affect when that improves clarity.
- [ ] Overflow, context menus, or flyouts are used for secondary actions that would otherwise clutter the surface.
- [ ] Commands that act on selected rows or selected items are disabled when no valid selection exists.
- [ ] Command names describe outcome, not internal implementation.
- [ ] The panel never relies on hidden hover-only actions for core workflows.

### 7. Navigation And Wayfinding

- [ ] The user can tell where they are in the app and within the panel.
- [ ] Navigation labels are clear and distinct.
- [ ] Peer navigation groups do not contain too many equally weighted destinations.
- [ ] If there are more than about 7 peer destinations, the structure is regrouped or made hierarchical.
- [ ] The panel avoids deep nesting without breadcrumbs or obvious orientation cues.
- [ ] The user does not need to pogo-stick up and down the hierarchy to reach related content.
- [ ] Tab sets, accordions, or secondary navigation are used only when they simplify the experience.
- [ ] List/detail layouts are used when users frequently switch between records while keeping details visible.

### 8. Feedback, State, And Recovery

- [ ] The panel gives immediate feedback when a command is invoked.
- [ ] Long-running operations show progress or at least an active busy state.
- [ ] Loading does not leave the user guessing whether the app froze.
- [ ] Success feedback is lightweight unless the outcome is critical.
- [ ] Error messages explain what happened and what the user can do next.
- [ ] Network or service failures are shown as actionable errors, not silent failures.
- [ ] Empty states explain why the surface is empty and what to do next.
- [ ] No-data states are visually intentional, not just blank white space.
- [ ] Disabled states explain why the action is unavailable when it is not obvious.
- [ ] Notifications are not overused for routine operations.
- [ ] Modal dialogs are used sparingly.

### 9. Theme, Color, And Visual Restraint

- [ ] The panel follows the active Windows or application theme rather than fighting it.
- [ ] The surface is tested in both light and dark themes.
- [ ] The surface remains legible in high contrast.
- [ ] Color is used to establish hierarchy and state, not to decorate empty design decisions.
- [ ] Accent color is used sparingly to indicate important interactive or stateful elements.
- [ ] Color is never the only cue for error, success, warning, or selection.
- [ ] Text contrast meets at least `4.5:1` against its background.
- [ ] Backgrounds, borders, and foreground text still read correctly when theme changes.
- [ ] The panel does not introduce a competing theme system, hard-coded palette, or ad hoc color rules.
- [ ] Brand color, if used, still preserves accessibility and state clarity.

### 10. Accessibility And Keyboard Use

- [ ] Every interactive element exposes a meaningful accessible name.
- [ ] Helpful descriptions are provided where the control purpose is not obvious from the label alone.
- [ ] Labels are programmatically associated with their fields.
- [ ] The tab order matches the visual and logical flow.
- [ ] Noninteractive decorative elements are not inserted into the tab order.
- [ ] Composite controls support arrow-key navigation where appropriate.
- [ ] Enter and Space invoke controls that behave like buttons.
- [ ] Keyboard shortcuts exist for important or frequent actions where appropriate.
- [ ] Shortcut keys are discoverable through tooltips, documentation, or accessible metadata.
- [ ] If the panel has multiple major panes, `F6` and `Shift+F6` navigation is considered.
- [ ] Focus indicators are visible and consistent.
- [ ] Screen readers are not misled by static text hosted in editable controls.
- [ ] Text scaling or display scaling does not break the layout.
- [ ] The panel remains understandable without relying on color vision.

### 11. Resizing, DPI, And Responsive Behavior

- [ ] The panel is usable at its minimum supported size.
- [ ] The panel becomes more useful when resized larger.
- [ ] The panel does not require the user to resize it immediately just to make it workable.
- [ ] There are no clipped controls at common DPI settings.
- [ ] There are no unnecessary horizontal scrollbars.
- [ ] Vertical scrolling is present only when content genuinely exceeds the viewport.
- [ ] Important actions remain reachable as the panel shrinks.
- [ ] The reading width for explanatory text does not become excessively wide.
- [ ] Content areas that benefit from expansion actually expand.
- [ ] Minimum sizes are defined for complex panels or panes that cannot collapse safely.

### 12. Data-Dense Panels, Lists, And Grids

- [ ] Text columns and numeric columns are aligned by meaning.
- [ ] Critical columns are visible without horizontal scrolling at the intended working size.
- [ ] Dense data views still preserve hierarchy through typography, spacing, and grouping.
- [ ] Multi-line list items use body and caption styles intentionally.
- [ ] Section headers within dense content use stronger type, not random color.
- [ ] The surface does not mix too many unrelated data visualizations in one view.
- [ ] Filters, search, and sort controls are close to the data they affect.
- [ ] Row actions are obvious and consistent.
- [ ] Selection state is visible in all supported themes.
- [ ] Empty grids, filtered grids, and error grids each have intentional messaging.

### 13. Wiley Widget WinForms Translation Rules

These are the repo-specific implementation rules that translate Microsoft guidance into this codebase.

- [ ] Root user-facing panels inherit from `ScopedPanelBase` unless there is a documented exception.
- [ ] Every user-facing panel has a clear header region, normally through `PanelHeader`.
- [ ] Root panels use `DockStyle.Fill`.
- [ ] Root panels use `AutoScaleMode.Dpi`.
- [ ] Root panels define a sensible minimum size.
- [ ] Prefer `TableLayoutPanel` and `FlowLayoutPanel` over manual absolute positioning for full-surface composition.
- [ ] Syncfusion controls are created through `SyncfusionControlFactory` unless an approved exception applies.
- [ ] `SfSkinManager` remains the single source of truth for theming.
- [ ] Dynamically created Syncfusion controls receive the active `ThemeName`.
- [ ] Manual color assignments do not compete with theme management except for semantic status colors where justified.
- [ ] Loading, no-data, and error overlays are part of the design, not afterthoughts.
- [ ] Event wiring, focus order, and disposal are treated as part of UX quality, not just code correctness.

### 14. Red Flags That Trigger Immediate Rework

- [ ] The panel opens as a wall of controls with no focal point.
- [ ] Labels are beside controls in a way that creates jagged reading flow.
- [ ] Multiple button styles compete for primary emphasis.
- [ ] Typography uses arbitrary sizes without a type ramp.
- [ ] The panel depends on manual color styling that breaks light/dark theme behavior.
- [ ] Critical actions are hidden in menus while low-value actions are always visible.
- [ ] The user must scroll before understanding what the panel is for.
- [ ] Validation appears only after a failing submit with no field-level guidance.
- [ ] Keyboard-only use is frustrating or impossible.
- [ ] Resize or DPI scaling causes clipping, overlap, or disappearing actions.
- [ ] Empty states look like missing content instead of intentional UX.
- [ ] The design relies on color alone to communicate status.

## Practical Panel Review Order

Use this order during implementation and review:

1. Confirm the panel's job, primary user, and primary task.
2. Confirm the top section communicates purpose, context, and primary action.
3. Check visual hierarchy, grouping, and scan path.
4. Check layout alignment, spacing rhythm, and control sizing.
5. Check typography, copy, and label quality.
6. Check field selection, validation, and form submission behavior.
7. Check command placement, confirmation, and undo strategy.
8. Check navigation and orientation.
9. Check loading, empty, success, warning, and error states.
10. Check theme, color, contrast, and high-contrast behavior.
11. Check keyboard, focus, tab order, and accessible names.
12. Check resize, DPI, and data-density behavior.

## Release Gate

A panel is ready only when all of the following are true:

- The panel passes every applicable checklist item in Sections 1 through 13.
- The panel has no critical failures in accessibility, theming, resize behavior, or task clarity.
- The panel feels balanced and readable at normal working size without special explanation.
- The panel looks like it belongs to the same product family as every other WileyWidget surface.

## Notes On Accuracy And Translation

Microsoft's current public guidance is written primarily for Windows apps using WinUI and XAML. This checklist preserves Microsoft's intent and concrete measurements while translating them to WinForms and Syncfusion.

That means:

- Use Microsoft spacing, type, accessibility, command, and navigation guidance as the benchmark.
- Adapt control-specific implementation details to WinForms without losing the visual and behavioral outcome.
- When a native or Syncfusion control cannot exactly match a WinUI metric, preserve the hierarchy, spacing rhythm, readability, focus behavior, and theme correctness.

If a future panel meets this checklist, it should not feel homemade. It should feel like a deliberate Windows desktop surface built by a team that understands hierarchy, restraint, accessibility, and task flow.
