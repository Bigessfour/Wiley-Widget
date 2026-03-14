---
name: test-driven-development
description: Use for implementing or fixing behavior with red-green-refactor discipline and targeted, reproducible tests.
---

# Test-Driven Development (Wiley Widget)

## Goal

Implement changes through red-green-refactor with minimal scope and strong regression coverage.

## Workflow

1. Red

- Write or identify a failing test for the intended behavior.
- Confirm the test fails for the right reason.

2. Green

- Implement the smallest change to make the test pass.
- Avoid unrelated refactors in this step.

3. Refactor

- Improve clarity and maintainability while keeping tests green.

## Test Selection Strategy

- Prefer focused test runs first.
- Then run nearest-scope suite.
- Finish with a build validation task.

## Wiley-Specific Focus Areas

- WinForms panel lifecycle and initialization timing.
- Syncfusion theming and control setup invariants.
- Jarvis chat bridge event flow and streaming completion behavior.
- Startup orchestration and async initialization contracts.

## Required Output

Report:

- New/updated tests
- Red -> Green evidence
- Final validation commands and results
- Any known gaps not covered by tests

## Guardrails

- No brittle sleep-based timing tests when condition-based waiting is possible.
- Keep tests deterministic and isolated.
- If UI tests are expensive, start with narrow filters before broad suites.
