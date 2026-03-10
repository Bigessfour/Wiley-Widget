# Release Panel Scope for Wiley Widget

This document defines the recommended panel scope for the town-focused Wiley Widget release.

The goal is not to delete every non-core panel immediately.

The safe approach is:

- Keep only the panels that directly support the original product goal visible in the release surface.
- Hide the rest from ribbon navigation, panel gallery navigation, and global search.
- Leave non-core code in place until the release is stable.

## Original Product Goal

Wiley Widget exists to:

- bring in QuickBooks data,
- align expenses into four enterprises: Water, Sewer, Trash, and Apartments,
- show whether each enterprise is paying its own way,
- show whether one enterprise is subsidizing another,
- support what-if planning for rates, reserves, equipment, and compensation,
- support plain-language financial questions through the AI/chat layer,
- avoid recreating systems QuickBooks already handles.

That goal is the basis for the scope decision.

## Keep Panels

These panels directly support the town release and should stay visible.

| Panel                        | Keep | Why                                                                                 |
| ---------------------------- | ---- | ----------------------------------------------------------------------------------- |
| Enterprise Vital Signs       | Yes  | Best top-level answer to whether each enterprise is healthy or subsidizing another. |
| Budget Management & Analysis | Yes  | Core budget vs actual workflow for enterprise financial review.                     |
| Municipal Accounts           | Yes  | Needed to align accounts and expenses into the correct enterprise buckets.          |
| Rates                        | Yes  | Supports enterprise rate-planning decisions.                                        |
| QuickBooks                   | Yes  | Required integration point for importing and understanding source data.             |
| Reports                      | Yes  | Needed to produce usable outputs for review and planning.                           |
| Revenue Trends               | Yes  | Supports rate and financial trend review over time.                                 |
| Recommended Monthly Charge   | Yes  | Directly supports the question of whether rates need adjustment.                    |
| JARVIS Chat                  | Yes  | Supports the plain-language Q&A goal using xAI and Semantic Kernel.                 |
| Settings                     | Yes  | Required operational and configuration surface.                                     |

## Hide for This Release

These panels can be safely removed from the release surface for now because they are either redundant, experimental, operationally secondary, or too close to recreating non-core workflows.

| Panel                 | Release Surface | Why                                                                                     |
| --------------------- | --------------- | --------------------------------------------------------------------------------------- |
| Account Editor        | Hide            | Supporting admin detail, not part of the core town release workflow.                    |
| Activity Log          | Hide            | Internal diagnostics, not needed for normal release use.                                |
| Analytics Hub         | Hide            | Broad analytics surface that overlaps with more targeted retained panels.               |
| Audit Log & Activity  | Hide            | Internal diagnostics and admin feature, not part of the core goal.                      |
| Customers             | Hide            | Moves the app toward utility and customer operations instead of enterprise P/L.         |
| Data Mapper           | Hide            | Useful support tool, but not a primary end-user release surface.                        |
| Department Summary    | Hide            | Department-level analysis is less central than enterprise-level analysis.               |
| Insight Feed          | Hide            | Nice-to-have AI and analytics surface, but not needed if JARVIS Chat remains.           |
| Payment Editor        | Hide            | Detail editor that moves toward recreating accounting operations.                       |
| Payments              | Hide            | Too close to recreating QuickBooks workflows that are not central to the app's purpose. |
| Proactive AI Insights | Hide            | Secondary AI surface; plain-language chat is the simpler retained AI entry point.       |
| Utility Bills         | Hide            | Utility operations and billing detail are out of scope for the current release goal.    |
| War Room              | Hide            | Exploratory operational surface, not essential to the base enterprise P/L mission.      |

## Implementation Rule

For this release cycle:

- Hidden panels remain in code.
- Hidden panels are removed from the main ribbon and panel navigation surface.
- Hidden panels are removed from panel-oriented global search suggestions and results.
- Hidden panels can be reconsidered after the town release is stable.

This gives a smaller, clearer product without forcing risky code deletion during release prep.
