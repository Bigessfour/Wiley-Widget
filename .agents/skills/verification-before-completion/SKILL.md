---
name: verification-before-completion
description: Use before claiming a fix is complete. Requires fresh build/test evidence and explicit reporting of what passed and what was not run.
---

# Verification Before Completion (Wiley Widget)

## Rule

Never claim completion without fresh verification evidence from this session.

## Required Steps

1. Pick the smallest command that proves the claim.
2. Run it now (not relying on previous output).
3. Confirm exit status and result details.
4. Report what passed, what failed, and what was not run.

## Minimum Validation Defaults

- Code edit default:
- Run `shell: build: fast` task.

- Behavior fix:
- Run focused test task(s) when available.
- If no focused tests exist, run nearest scope test task.

- UI-sensitive changes:
- Run relevant WinForms/Blazor integration tests if available.

## Reporting Contract

Always include:

- Command or task run
- Pass/fail result
- Key output summary
- Remaining risk or untested areas

## Wiley-Specific Notes

- Prefer VS Code tasks over ad-hoc shell commands for build/test.
- Do not run parallel builds/tests in separate terminals.
- If verification cannot be run, state why and provide the exact next command.
