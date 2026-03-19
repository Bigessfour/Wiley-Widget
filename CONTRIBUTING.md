# Contributing to Wiley Widget

Wiley Widget is in release stabilization. The priority is no longer feature accumulation. The priority is finishing safely: fix what matters, preserve working behavior, and prove that changes did not break adjacent workflows.

## Read These First

- `.vscode/approved-workflow.md`
- `.vscode/copilot-instructions.md`
- `docs/TESTING_STRATEGY.md`
- `docs/PRE_RELEASE_CHECKLIST.md`
- `Done_Checklist.md` when the change affects an in-scope panel

## Core Rules

1. Keep scope small.
2. Do not mix unrelated cleanup with a stabilization fix.
3. When you fix or polish one method, identify the existing methods and workflows that could break as a side effect.
4. Add or update proof for the behaviors that matter.
5. Update docs when behavior, workflow, or setup expectations change.

## Meaningful Proof

Meaningful tests are required. They should prove a real contract, workflow, or regression risk.

Good proof:

- startup completes without exceptions
- panel navigation still works through the production path
- a shared method still serves existing callers after a polish or refactor
- a theme, layout, or docking fix does not regress other shell behavior
- a business workflow still produces the expected observable result

Weak proof:

- tests added only to increase count
- brittle assertions on internal implementation details
- mock-heavy tests that do not protect real behavior
- filtered runs that execute zero tests
- old reports or screenshots treated as current evidence

If automated proof is impractical, run a focused manual smoke path and record exactly what was validated.

## Recommended Change Flow

1. Read the affected code and nearby tests.
2. Identify shared methods, shell surfaces, and user-visible contracts at risk.
3. Define the smallest safe change.
4. Implement it without opportunistic refactors.
5. Add or update meaningful proof.
6. Run the smallest relevant build and validation tasks.
7. Update docs if the repo truth changed.

## Validation Expectations

- Use focused proof from `docs/TESTING_STRATEGY.md`.
- Prefer targeted WinForms and smoke coverage over whole-solution test noise.
- Treat `dotnet test WileyWidget.sln` as incomplete evidence, not release proof.
- For release-facing work, validate both the changed behavior and the unchanged behavior that users still depend on.

## Documentation Expectations

- `README.md` should describe the current product and release posture.
- `QUICK_START.md` should only include commands and scripts that exist now.
- `docs/PRE_RELEASE_CHECKLIST.md` should reflect actual sign-off criteria.
- Internal instruction files under `.vscode/` and `.github/` should not point at missing documents.

## Branching and PRs

- Keep branches short-lived and purpose-driven.
- Use one PR per cohesive fix or workflow slice.
- In the PR description, state:
  - what changed
  - what existing behavior was at risk
  - what proof was added or rerun
  - what was not proven, if anything

## Release Mindset

Do not ask whether a change is small enough to skip proof. Ask what the user-visible or maintainer-visible contract is, and prove that contract still holds.
