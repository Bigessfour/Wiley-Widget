# Development Rhythm — Slice-by-Slice Workflow

This document helps teams move quickly while staying safe and consistent. It’s tuned for the Wiley Widget repo (WinForms/.NET 9, MVVM, Syncfusion integrations) and aims to maintain a steady E2E rhythm for slicing features into production-ready increments.

## Goals
- Ship small, testable slices frequently (1–3 days per slice).
- Maintain high local validation (tests + linters) before PRs.
- Keep CI/Trunk checks fast and deterministic.
- Produce fully-validated, production-ready slices.

## Daily / Weekly Cadence
- Morning: Review overnight CI/Trunk checks & open PRs assigned to you.
- Mid-day: Work on one focused slice (2–4 hours uninterrupted), or a single spike when blocked.
- End-of-day: Run local tests + linters, update PR description, push the changes to trigger Trunk/CI.
- Weekly (Friday): Retrospective — what shipped, what was learned, update CHANGELOG and release notes.

## Slice (Single Feature) — Target: 1–3 days
1. Choose a small, discrete feature that improves an observable flow (e.g., "Accounts: Add quick filter to SfDataGrid").
2. Create a feature branch using the branch naming guide (see docs/branching.md).
3. Create a single Issue describing acceptance criteria and test plan.
4. Implement a single MVVM slice: ViewModel → Service → Model → View. Avoid cross-cutting changes.
5. Add unit tests for logic and integration tests where cross-layer behavior must be verified.
6. Write or update an E2E test for full slice if feasible (UI E2E on Windows self-hosted runner).
7. Update docs: CHANGELOG, README (if feature-level visibility), and any usage notes.
8. Create a PR with the slice checklist completed (see docs/slice-checklist.md) and request review.
9. Resolve review feedback and iterate until CI + Trunk checks pass.
10. Merge using Trunk Merge queue (or similar protected branch strategy) — keep merge commits minimal.

## Pull Request Rules (Good Hygiene)
- Small PRs. Prefer <500 lines of change. Break larger work into sub-slices.
- Include an explicit slice summary, testing, and rollback plan in the PR body.
- All unit tests must pass locally before pushing.
- Trunk + CI checks must pass before merge. If a check is flaky, fix it in a dedicated PR or open an issue.

## Testing Targets & Coverage
- Aim for 80%+ coverage on newly changed code for every slice. A global target of 85% is maintained in CI but can be adjusted per context.
- Prefer behavior-driven tests: Given/When/Then for important flows.
- Use mocks (Moq) for dependencies; keep integration tests minimal and staged.

## CI & Fast Feedback
- Keep pre-merge checks fast: linters, unit tests, trunk checks, and a smoke test of builds.
- Integration and UI E2E tests run in dedicated contexts (with secrets) and/or self-hosted runners.
- Use caching for NuGet/dotnet tools and parallelization to improve run times.

## Handling Blockers
- Create a small “spike” PR or workspace experiment for unknowns (Syncfusion behavior, QuickBooks API). Mark that PR as experimental and communicate in comments.
- Use Friday retros to adjust slice size and tooling if repeated blockers are found.

## Trunk / Merge Queue Behavior
- Prefer trunk gating for final merges. Use Trunk checks early (pre-commit or pre-push) when feasible.
- Protect `main` with branch and merge rules; require PR review + trunk/CI success.

## Metrics That Matter
- CI success rate (goal 95%+ across the last 30 runs).
- Average time from PR opened → merged for green slices (goal: 24–48 hours).
- Code coverage for new files (goal: 80%+ per slice).
- Number of flaky tests discovered — triage and quarantine immediately.

---

Docs added: docs/slice-checklist.md, docs/branching.md. Use the provided PR/Issue templates for consistent delivery.

## Grok immediate boosters
We added a small set of "immediate rhythm boosters" (see `docs/grok-suggestions.md`) that give you:
- a runnable playground for seed & query (`scripts/playground/seed-and-query.csx`)
- instructions for using the repo's MCP + Syncfusion docs servers
- a recommended 7-day micro-rhythm for focused slice delivery

If you'd like, I can prepare a staged PR with everything above and a few optional CI tweaks to improve gating and feedback speed.