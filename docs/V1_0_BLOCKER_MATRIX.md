# Wiley Widget v1.0 Blocker Matrix

Use this document as the working release board for v1.0.

The purpose of the matrix is to convert the v1.0 promise into explicit blockers with clear owner lanes, clear proof requirements, and clear stop-ship severity.

## How To Use This Matrix

1. Assign one named owner to each blocker row.
2. Keep status current.
3. Do not close a blocker without the listed proof.
4. If a blocker changes shared shell behavior, add regression proof for the previously working path.

## Status Values

| Status      | Meaning                                                                                |
| ----------- | -------------------------------------------------------------------------------------- |
| Open        | Release blocker is known and not yet proven closed                                     |
| In Progress | Active work is underway and proof is being prepared                                    |
| Blocked     | Cannot proceed because another blocker or missing environment dependency is in the way |
| Proven      | Required proof exists and the blocker is cleared                                       |
| Deferred    | Explicitly moved out of v1.0 scope                                                     |

## Severity Values

| Severity            | Meaning                                                                                                |
| ------------------- | ------------------------------------------------------------------------------------------------------ |
| P0 Stop-Ship        | Must be fixed or formally removed from scope before v1.0                                               |
| P1 Release-Critical | Must be resolved for RC1 unless the scope is changed                                                   |
| P2 Important        | Important to confidence and polish, but only blocks v1.0 if it affects an in-scope workflow materially |

## Owner Lanes

| Owner Lane          | Responsibility                                                                      |
| ------------------- | ----------------------------------------------------------------------------------- |
| Shell & Navigation  | Startup, MainForm, ribbon, panel registry, right dock, theme, docking, layout       |
| Panel Certification | Panel speed, readability, clipping, control wiring, panel checklist compliance      |
| Data & Reliability  | Database runtime path, repository behavior, CRUD reliability, docs alignment        |
| JARVIS & AI         | JARVIS rendering, shell integration, prompt/response usefulness, enterprise context |
| Release & Docs      | Release checklist, scope control, packaging guidance, truthfulness of documentation |

## Blocker Matrix

| ID     | Area                          | Owner Lane          | Severity            | Blocker                                                                                                          | Required Proof                                                                                                                        | Initial Status |
| ------ | ----------------------------- | ------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | -------------- |
| WW-100 | Startup                       | Shell & Navigation  | P0 Stop-Ship        | Main shell does not start cleanly and leave the app usable                                                       | `MainFormStartupIntegrationTests.FullStartup_NormalConfig_SucceedsWithoutExceptions` plus focused MainForm startup validation         | Open           |
| WW-101 | Navigation                    | Shell & Navigation  | P0 Stop-Ship        | Production navigation cannot reliably open every in-scope panel from the release slice                           | `PanelRegistryNavigationProofTests` plus focused shell smoke for in-scope panels                                                      | Open           |
| WW-102 | JARVIS Docking                | Shell & Navigation  | P0 Stop-Ship        | JARVIS does not reliably appear in the intended right-dock experience                                            | focused MainForm navigation proof plus `JarvisChatFlaUiTests`                                                                         | Open           |
| WW-103 | Shell Regression              | Shell & Navigation  | P1 Release-Critical | Changes to theme, docking, layout, or right-dock behavior regress existing shell methods                         | targeted MainForm tests, startup proof, and rerun of panel-registry shell smoke                                                       | Open           |
| WW-200 | Enterprise Vital Signs        | Panel Certification | P1 Release-Critical | Enterprise Vital Signs does not clearly present enterprise financial state                                       | in-scope panel certification using `Done_Checklist.md` plus hard-fail control proof                                                   | Open           |
| WW-201 | Municipal Accounts            | Panel Certification | P0 Stop-Ship        | Municipal Accounts is not reliable enough for enterprise financial understanding                                 | panel certification plus `AccountsPanelIntegrationTests` and panel navigation proof                                                   | Open           |
| WW-202 | Budget Analysis               | Panel Certification | P1 Release-Critical | Budget Management & Analysis does not provide readable, unclipped, working controls                              | panel certification plus focused panel smoke or integration proof                                                                     | Open           |
| WW-203 | Rates Guidance                | Panel Certification | P1 Release-Critical | Recommended Monthly Charge does not support understandable rate guidance for the town                            | panel certification plus dedicated recommended-monthly-charge proof task                                                              | Open           |
| WW-204 | Utility Operations            | Panel Certification | P1 Release-Critical | Customers, Utility Bills, and Payments surfaces are not all usable, readable, and wired correctly                | panel certification plus focused panel smoke or FlaUI/integration checks for each surface                                             | Open           |
| WW-205 | Layout Quality                | Panel Certification | P1 Release-Critical | In-scope panels contain clipped, overlapping, or unevenly spaced Syncfusion controls at normal DPI settings      | panel certification, `docs/WileyWidgetUIStandards.md`, and UI/layout proof including MCP EvalCSharp or focused FlaUI where applicable | Open           |
| WW-206 | Panel Responsiveness          | Panel Certification | P2 Important        | In-scope panels appear too slowly or feel unresponsive during common actions                                     | timed smoke validation for panel load and refresh actions plus loader feedback verification                                           | Open           |
| WW-300 | Database Truth                | Data & Reliability  | P0 Stop-Ship        | Current database setup and runtime behavior are not documented truthfully                                        | docs review plus validated runtime path and maintainer reproduction steps                                                             | Open           |
| WW-301 | Repository Reliability        | Data & Reliability  | P0 Stop-Ship        | Repository and CRUD behavior for in-scope enterprise workflows is unreliable                                     | `SqlRepositoryProofTests` plus targeted integration proof for enterprise-facing repos                                                 | Open           |
| WW-302 | Enterprise Data Read Path     | Data & Reliability  | P1 Release-Critical | Core enterprise data cannot be loaded, refreshed, and re-read reliably in the UI                                 | targeted integration proof plus panel-level reload verification for in-scope financial panels                                         | Open           |
| WW-303 | Failure Visibility            | Data & Reliability  | P1 Release-Critical | Database or data-load failures leave the user with silent or misleading financial state                          | focused failure-path validation plus clear user-facing error behavior                                                                 | Open           |
| WW-400 | JARVIS Render                 | JARVIS & AI         | P0 Stop-Ship        | JARVIS does not render reliably inside the v1.0 shell experience                                                 | `JarvisChatFlaUiTests` and startup/navigation proof                                                                                   | Open           |
| WW-401 | Everyday Language             | JARVIS & AI         | P1 Release-Critical | JARVIS answers are too technical, too generic, or not useful to a town clerk                                     | defined everyday-question baseline with reproducible prompt/response proof                                                            | Open           |
| WW-402 | Enterprise Context            | JARVIS & AI         | P1 Release-Critical | JARVIS is not grounded enough in enterprise context to help users understand Water, Sewer, Trash, and Apartments | focused enterprise prompt suite and observable context-sensitive answer review                                                        | Open           |
| WW-403 | Financial Guidance Usefulness | JARVIS & AI         | P1 Release-Critical | JARVIS does not help steer ordinary financial understanding or decision support                                  | curated prompt set showing useful, plain-language financial guidance for the release slice                                            | Open           |
| WW-500 | Release Scope Control         | Release & Docs      | P0 Stop-Ship        | The release slice is not frozen, so work keeps expanding and blockers never stabilize                            | accepted `docs/V1_0_RELEASE_SCOPE.md` plus explicit in-scope and deferred lists                                                       | Open           |
| WW-501 | Docs Match Reality            | Release & Docs      | P1 Release-Critical | Setup, release, and reliability docs drift away from the current repository truth                                | doc review against runtime behavior and current workflows                                                                             | Open           |
| WW-502 | Release Packaging             | Release & Docs      | P1 Release-Critical | Release artifact path, packaging instructions, and launch validation are not yet trustworthy                     | successful publish path plus clean-machine launch validation                                                                          | Open           |

## Suggested First Pass Ownership

If you need to start assigning work immediately, start in this order:

1. WW-500 Release Scope Control
2. WW-100 Startup
3. WW-101 Navigation
4. WW-102 JARVIS Docking
5. WW-300 Database Truth
6. WW-301 Repository Reliability
7. WW-201 Municipal Accounts
8. WW-204 Utility Operations
9. WW-400 JARVIS Render
10. WW-401 Everyday Language

## Recommended Proof Hooks Already In The Repo

These are the most relevant current proof hooks for the first pass.

- `test: panel registry proof (incremental)`
- `test: panel registry shell smoke (incremental)`
- `test: ribbon mainform focused`
- `test: jarvis chat flaui (final)`
- `temp: sql repository proof`
- `run integration tests`

Use these as starting points, not as a substitute for blocker-specific proof design.

## Rule For Closing A Blocker

Do not close a blocker because the UI looks better or because a developer believes it is fixed.

Close it only when:

1. The blocker condition is no longer true.
2. The required proof has been run successfully.
3. Adjacent existing behavior that was at risk has also been proven.
