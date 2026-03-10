# Town-Scale Release Playbook for Wiley Widget

This project does not need a heavyweight commercial release process.

It does need a repeatable way to prove that the app works, the important screens behave correctly, and the release artifact runs outside the development machine.

## Release Goal

Wiley Widget is release-ready when a town user can:

- Launch the app without errors.
- Navigate to the core financial panels without crashes or dead ends.
- Trust the P/L-related numbers shown on the main workflows.
- Use the app without obvious layout breakage, unreadable controls, or theme problems.
- Run the published executable on a second Windows environment.

That is the standard. Everything in this playbook is designed to prove those points.

## Working Definition of Done

A release candidate is ready only if all of the following are true:

- The app starts normally.
- The main form loads with the expected Syncfusion theme.
- Ribbon and panel navigation work.
- The core financial screens open and render correctly.
- Known sample data or expected real data produces believable P/L results.
- Reports and exports needed for this release work.
- External integration points fail gracefully when unavailable.
- The release executable runs outside the dev environment.
- No major UI clipping, unreadable text, overlapping controls, or broken dialogs are visible in the core workflows.

## Core Release Surface

For the town-focused release, the app should stay centered on the original mission instead of exposing every supporting or experimental panel.

### Keep in the release surface

- Enterprise Vital Signs
- Budget Management & Analysis
- Municipal Accounts
- Rates
- QuickBooks
- Reports
- Revenue Trends
- Recommended Monthly Charge
- JARVIS Chat
- Settings

### Hide from the release surface for now

- Account Editor
- Activity Log
- Analytics Hub
- Audit Log & Activity
- Customers
- Data Mapper
- Department Summary
- Insight Feed
- Payment Editor
- Payments
- Proactive AI Insights
- Utility Bills
- War Room

These are not deleted from the project. They are treated as out of scope for the town release and kept out of the main navigation and search surface unless they become necessary again.

## Scope Freeze for a Release Candidate

When preparing a release candidate, only work on these categories:

- Startup failures
- Panel navigation failures
- P/L math or data correctness issues
- Report or export failures
- QuickBooks or external integration failures that are in scope for this release
- Major usability or UI polish issues on core screens
- Packaging and release artifact issues

Do not mix in refactors, architecture cleanups, or low-value cosmetic changes once the candidate cycle starts.

## Must-Work Flows

For Wiley Widget, use this as the minimum proof set for each release candidate.

### 1. Startup

- Launch the app.
- Confirm the main window appears cleanly.
- Confirm the selected theme applies correctly.
- Confirm no blocking exception dialogs appear.

### 2. Core Navigation

- Open Enterprise Vital Signs.
- Open Budget Management & Analysis.
- Open Municipal Accounts.
- Open Reports.
- Open Settings.
- Confirm each panel appears without docking or layout failures.
- If Enterprise Vital Signs shows a no-data overlay, verify whether `dbo.TownOfWileyBudgetData` in the live app database contains rows before treating it as a UI defect.

### 3. Core Financial Proof

- Load a known dataset or representative working data.
- For Enterprise Vital Signs, the required source dataset is `dbo.TownOfWileyBudgetData` in the same SQL Server instance the WinForms app uses.
- Verify at least one known-good P/L scenario by eye.
- Confirm totals, labels, and trend visuals look credible.
- Confirm empty-state or missing-data behavior is understandable.

### 4. Reporting / Export

- Open the reporting flow used by real users.
- Run the report or export action used in normal operation.
- Confirm success messaging, output generation, or graceful failure.
- If no report has ever been displayed successfully, treat Reports as a current release blocker.
- For this candidate, reporting proof must be completed before publish.
- For this candidate, PDF exporbet and Excel export must also be proven end-to-end before publish.
- Installed Syncfusion packages do not count as proof. The actual user-facing export commands must produce working files.

### 5. QuickBooks / Integration Proof

- Open QuickBooks-related UI if included in this release.
- Confirm the screen loads.
- Run the QuickBooks diagnostics action before attempting Connect.
- Confirm the redirect URI is a localhost callback for Wiley Widget itself, for example `http://localhost:5000/callback/`, not the Intuit OAuth playground redirect.
- Confirm connection success or failure messaging is understandable.
- Confirm Connect returns to the app, shows a company name, and leaves the panel in a connected state.
- Confirm the app does not hang or crash when QuickBooks is unavailable.
- Treat missing client credentials, missing URL ACL registration, or a sandbox callback that never returns to the app as release blockers.
- If sandbox connection has never been proven end-to-end, treat QuickBooks integration as a current release blocker.
- For this candidate, sandbox proof must be completed before publish.
- Use [docs/QUICKBOOKS_SANDBOX_CONNECT_AND_SEED_GUIDE.md](docs/QUICKBOOKS_SANDBOX_CONNECT_AND_SEED_GUIDE.md) before the first sandbox connect-and-seed attempt.

### 5A. QuickBooks Production Cutover Gate

- Do not move Wiley Widget to production QuickBooks data until sandbox proof is stable and repeatable.
- Production credentials are separate from development credentials in the Intuit Developer Portal.
- Intuit requires a production questionnaire and approval before production Client ID and Client Secret are available.
- Production redirect URIs must be configured under the Production tab in Intuit and must use HTTPS.
- Production redirect URIs cannot use an IP address.
- If Wiley Widget uses a loopback callback in production, it must use HTTPS and a locally trusted TLS certificate with the required HTTP.SYS binding on that port.
- Before enabling production, confirm the Production host domain, launch URL, disconnect URL, privacy policy URL, and terms URL are set correctly in Intuit.
- Before enabling production, confirm the requested scopes still match the app's actual needs. If scopes change, users must reauthorize.
- Do not point production at sandbox credentials, sandbox realm IDs, or sandbox redirect URIs.
- Treat missing production keys, missing production redirect URI registration, or missing HTTPS callback infrastructure as release blockers.
- Use [docs/QUICKBOOKS_PRODUCTION_CUTOVER_GUIDE.md](docs/QUICKBOOKS_PRODUCTION_CUTOVER_GUIDE.md) as the step-by-step cutover checklist.

### 6. UI Polish Pass

- Check the main header areas, buttons, grids, overlays, and dialogs.
- Look specifically for clipping, odd spacing, unreadable text, tooltips that never appear, and broken status messaging.
- Fix only issues that make the product look unreliable or hard to use.

### 7. Release Artifact Proof

- Publish the release candidate executable.
- Run that executable on a second Windows machine, VM, or clean user profile.
- Confirm startup, navigation, and one core financial flow work there too.

## Recommended Release-Candidate Cycle

Use the same sequence every time.

## Local-To-CI Bridge

The workspace now includes explicit local tasks that mirror the main GitHub Actions paths.

- `ci-local: build-release`
  Runs the same kind of Release build that GitHub runs on its Windows runner.
- `ci-local: startup-smoke`
  Runs the same filtered startup smoke test used by the WinForms CI workflow in its default configuration.
- `ci-local: startup-smoke matrix`
  Runs the three startup variants used in CI: default, no-docking, and no-dashboard.
- `ci-local: test-release`
  Runs the broader full solution test pass in Release mode. Use it as an audit pass, not as the minimum release gate.
- `ci-local: test-release-coverage`
  Runs the same style of coverage-enabled test pass used by the coverage workflow.
- `ci-local: publish-release-candidate`
  Produces the same style of self-contained single-file candidate artifact as the current GitHub release workflow.
- `ci-local: release-proof`
  Chains the practical local release gate in sequence: Release build, then startup smoke matrix.

### How To Think About It

- Local tasks answer: did my changes work on my machine using the same commands CI expects?
- GitHub Actions answers: do those same commands still work on a clean Windows runner from scratch?
- The release workflow answers: can we package the exact artifact we intend to hand to users?

That is the model to remember.

### Recommended Everyday Use

- While developing: use faster local tasks when you need speed.
- Before calling something release-ready: run `ci-local: release-proof`.
- When you want a broader backlog-quality sweep beyond the candidate gate: run `ci-local: test-release`.
- Before tagging: run `ci-local: publish-release-candidate` and test that artifact.

If a local `ci-local` task passes and GitHub still fails, the usual causes are environment differences, missing secrets, restore issues, or a clean-machine dependency problem.

### Step 1. Pick the Candidate Commit

- Stop feature work.
- Choose the exact commit or release branch that will be evaluated.
- Treat that commit as the only thing under test.

### Step 2. Run the Standard Validation Tasks

Run the canonical local proof set:

- `ci-local: release-proof`

If you want a broader local test sweep beyond the release gate, run `ci-local: test-release` separately.

If you also want the local equivalent of the coverage workflow, run `ci-local: test-release-coverage`.

If a release affects a narrower area, targeted tests can supplement these, but this baseline should stay stable.

### Step 3. Produce the Candidate Artifact

Publish the app using `ci-local: publish-release-candidate` so the local artifact follows the same packaging path as the current GitHub release workflow.

Important note for Wiley Widget:

- The current GitHub release workflow publishes a self-contained single-file WinForms executable.
- The release packaging path is intentionally untrimmed.
- Microsoft documents that Windows Forms trimming is currently incompatible/unsupported for reliable use.
- Keep the candidate and tag-based release artifacts untrimmed unless trim safety is explicitly proven.

### Step 4. Do a Manual Smoke Pass

Run the must-work flows in order.

Do not wander around the app looking for random issues. Follow the same path every time so results are comparable.

### Step 5. Record Only Real Blockers

A release blocker is something that breaks trust or normal operation:

- App does not launch
- Crash or hang during normal use
- Panel cannot open or renders incorrectly
- Wrong financial result
- Save/export/report path fails
- Integration path crashes or misleads the user
- UI is visibly broken on a core screen
- Published executable fails outside the dev box

Everything else goes to the backlog.

### Step 6. Fix Blockers Only

- Apply the smallest safe fix.
- Re-run the same candidate cycle.
- Do not expand scope during this pass.

### Step 7. Require Two Clean Passes

Before release, get two consecutive clean candidate passes on the same checklist.

That is a simple, practical confidence rule for a small real-world desktop tool.

### Step 8. Tag and Release

Only after the candidate passes cleanly should the release tag be created.

Suggested sequence:

- Merge the approved candidate commit.
- Create the release tag from that exact commit.
- Let GitHub Actions package the release artifact.
- Verify the GitHub release contains the expected executable.

## Minimal GitHub Actions Strategy

Keep CI lightweight and useful.

### Use CI to prove technical health

- `build-winforms.yml` should stay the primary technical gate.
- `test-coverage.yml` is useful as supporting evidence, but not the main release decision maker.
- `security-scan.yml` is still worth keeping, but it should not overshadow functional readiness.

### Use release workflow for packaging, not truth

- `release.yml` should package the approved release candidate.
- Do not treat a successful tag-triggered build as proof that the app is ready.
- Readiness should be established before tagging.

## Suggested Evidence to Keep

Keep the evidence lightweight:

- Completed release checklist
- Test task results
- One short note listing blockers fixed for the release
- Screenshots only if they help explain a UI issue
- GitHub release artifact link

That is enough for this project.

## First Practical Release Run

If starting now, use this exact order:

1. Decide which features are in scope for the next release.
2. Freeze new feature work until the release candidate is proven.
3. Run `ci-local: release-proof`.
4. Optionally run `ci-local: test-release` if you want the broader full-suite audit pass.
5. Optionally run `ci-local: test-release-coverage` if you want the local equivalent of the coverage workflow.
6. Run `ci-local: publish-release-candidate`.
7. Run the must-work flows locally.
8. Run the same candidate on a second Windows environment.
9. Fix only blockers.
10. Repeat until two clean passes are complete.
11. Create the tag and publish the release.

## Practical Standard

The goal is not to prove Wiley Widget is enterprise software.

The goal is to prove that the town can depend on it for the workflows that matter.

If the app launches, core financial screens work, the numbers are credible, the UI looks solid enough, and the release executable behaves outside the dev machine, that is a valid release standard for this project.
