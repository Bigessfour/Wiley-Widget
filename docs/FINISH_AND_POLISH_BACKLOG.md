# Finish And Polish Status

Status: complete

Completed: March 15, 2026

The finish-and-polish backlog has been closed and converted into this completion record so the repository no longer carries a stale open-work list.

## Completed Areas

- Reports were aligned to the shipped template inventory and the supported PDF-first workflow.
- Utility Bill reporting was finalized on the shipped UI path by keeping the real PDF and Excel export actions and removing the dead internal report command path.
- QuickBooks budget support remains intentionally deferred, and the non-shipping budget sync injection path was removed from the panel view model so the shipped surface no longer implies active budget support.
- Analytics rate scenarios now source current-rate baselines from enterprise data instead of returning placeholder zero values.
- The dead alternate tabbed-layout factory path is no longer present.
- Webhooks companion pages are served from static content files through the production route structure.

## Validation Summary

- Fast solution build passed via `shell: build: fast`.
- Focused service diagnostics for the edited files reported no compile errors.
- A new analytics regression test file was added for portfolio baseline rate behavior and missing-rate failure behavior.

## Known Repository-Wide Validation Issue

The WinForms test project still has unrelated pre-existing compile failures outside this finish pass:

- `tests/WileyWidget.WinForms.Tests/Integration/Forms/JarvisPanelIntegrationTests.cs`
- `tests/WileyWidget.WinForms.Tests/Integration/Forms/RightDockPanelFactoryIntegrationTests.cs`
- `tests/WileyWidget.WinForms.Tests/Unit/Forms/RightPanelTests.cs`

Those failures are tuple-deconstruction errors in existing tests and were not introduced by this work.

## Closure Note

This document is retained only as a completion marker. If a new polish sweep is needed later, start a new backlog from the current code state rather than reopening the outdated item list that was previously here.
