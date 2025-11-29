Archived projects (2025-11-29)

This folder contains projects archived on 2025-11-29 per repository cleanup.

- WileyWidget.Facade — moved from src/WileyWidget.Facade. This project referenced a missing `src/WileyWidget.UI` project and appeared unused by the solution.
- WileyWidget.Webhooks — moved from src/WileyWidget.Webhooks. This project was not referenced by the solution and contains only webhook-related sources/docs.

These moves are reversible (git history preserved). If you want to fully delete these projects later, run CI and ensure no downstream references remain.
