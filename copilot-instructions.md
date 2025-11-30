# Copilot / Contributor Instructions

This repository uses a mandatory "View Completeness" validation for all UI views (Forms / Dialogs / Panels) in the WinForms application.

Key rule: Any pull request that adds or modifies a view must include a completed or updated `docs/views/<view-name>.md` file and show evidence that the checklist at `docs/view-completeness.md` is satisfied for that view before the PR is merged.

What to do when changing/adding views
- Open or create `docs/views/<view-name>.md` and fill out the checklist and evidence sections.
- Re-run unit tests and UI tests where applicable; add or update tests if new behaviors are introduced.
- Include screenshots, visual-diff outputs, or a small automated test where possible to demonstrate the UI in both Fluent Dark and Fluent Light themes.

Why this is mandatory
- Ensures consistent theming, accessibility, and quality across all views.
- Makes review and CI validation fast & consistent.

Checklist location: docs/view-completeness.md

View tracking folder: docs/views/

If you are a Copilot or programmatic assistant: always reference the `docs/view-completeness.md` file when assessing view readiness and enforce that developers update the matching `docs/views/<view>.md` record when making UI changes.
