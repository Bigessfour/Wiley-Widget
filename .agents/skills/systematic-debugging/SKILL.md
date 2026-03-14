---
name: systematic-debugging
description: Use for bugs, regressions, runtime crashes, test failures, startup issues, and unexpected behavior. Requires evidence-first root-cause analysis before fixes.
---

# Systematic Debugging (Wiley Widget)

## Purpose

Use an evidence-first process to find root cause before implementing fixes.
Do not guess. Do not stack unrelated changes.

## When To Use

- Failing tests
- Runtime exceptions
- Build or startup failures
- Performance regressions
- Integration failures (Syncfusion, BlazorWebView, Grok/xAI, EF/SQL)

## Required Workflow

1. Reproduce exactly.

- Capture exact trigger steps.
- Capture the precise exception, stack trace, or failing assertion.

2. Collect evidence.

- Read relevant code paths end-to-end.
- Gather logs and recent changes.
- Identify where expected vs actual behavior diverges.

3. Compare against working pattern.

- Find a similar, known-good implementation in this repo.
- List concrete differences.

4. Form one hypothesis.

- State one root-cause hypothesis.
- Make the smallest change that tests it.

5. Verify and iterate.

- Run targeted validation.
- If it fails, revise hypothesis.
- If three attempts fail, pause and reassess architecture.

## Wiley-Specific Checks

- Syncfusion and theming:
- Verify `SfSkinManager` is the single theme authority.
- Avoid manual `BackColor`/`ForeColor` unless semantic status colors.
- For Syncfusion controls, ensure creation through `SyncfusionControlFactory` where required.

- Startup and async init:
- Keep `Initialize()` synchronous.
- Defer heavy work to `IAsyncInitializable.InitializeAsync` after form shown.
- Avoid `.Result` or `.Wait()`.

- Jarvis/Grok path:
- Trace from UI event -> bridge -> handler -> AI service.
- Confirm degraded/offline behavior is user-safe.

## Output Format

Provide:

- Findings (severity ordered)
- Evidence (file and line references)
- Root cause
- Minimal fix
- Validation run and outcome

## Guardrails

- No destructive git operations.
- No broad refactors while triaging a single issue.
- Keep edits minimal and reversible.
