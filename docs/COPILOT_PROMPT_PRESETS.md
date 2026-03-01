# Copilot Prompt Presets (Scope-Controlled)

Use these presets to keep Copilot focused on exactly what you asked.

---

## How to Use This File

1. Pick the closest preset.
2. Fill in the placeholders in `[brackets]`.
3. Keep **Scope Allowed** and **Out of Scope** explicit.
4. Keep each request to **one deliverable**.
5. If needed, chain requests as small phases (diagnose -> implement -> validate).

---

## Universal Scope Header (prepend to any task)

```text
Mode: Surgical
Goal: Complete exactly one task with minimal change.

Scope Allowed:
- [list exact files]

Out of Scope:
- Any file not listed above
- Refactors, renames, formatting-only edits
- Dependency/version/config changes
- UI/theme/layout changes unless explicitly listed

Constraints:
- Max files changed: [N]
- Max lines changed: [M]
- If out-of-scope edits are required, STOP and ask first.

Validation:
- Run only: [exact task/test]
- Do not run full suite.

Output:
- Show changed files and a 1-line reason per file.
```

---

## Preset 1 - Read-Only Diagnosis (No Edits)

Use when you want root cause before any changes.

```text
Read-only diagnosis only. Do NOT edit files.

Problem:
- [describe symptom]

Scope Allowed (read only):
- [file A]
- [file B]

Required output:
1) Most likely root cause
2) Exact location(s)
3) Smallest fix options (A/B)
4) Risk of each option

Do not implement anything in this step.
```

---

## Preset 2 - Single Bug Fix (Strict)

```text
Implement one bug fix only.

Bug:
- [expected]
- [actual]

Scope Allowed:
- [exact file(s)]

Out of Scope:
- Tests unrelated to this bug
- Any behavior changes outside [feature area]

Constraints:
- Minimal patch
- Max [N] files, max [M] lines
- No new abstractions unless required

Validation:
- Run only [specific build/test task]

If fix requires touching another file, STOP and ask.
```

---

## Preset 3 - Safe UI Fix (WinForms/Syncfusion)

```text
Implement a UI fix with strict boundaries.

Issue:
- [control/panel]
- [what is wrong visually/behaviorally]

Scope Allowed:
- [MainForm partial(s) or specific service file]

Hard Rules:
- Keep SfSkinManager as the single theme authority
- Do not add alternate theme/color systems
- Do not modify unrelated panel navigation behavior

Out of Scope:
- Ribbon redesign
- New controls
- Global layout refactor

Validation:
- Run [build: fast] only (or one targeted UI test)
- Report exact runtime behavior changed
```

---

## Preset 4 - Test-Only Change

```text
Change tests only. Production code is out of scope.

Task:
- [test failure or update]

Scope Allowed:
- [test file(s)]

Out of Scope:
- src/ changes
- test infrastructure changes

Constraints:
- Keep assertions aligned to existing product behavior
- No broad rewrites

Validation:
- Run only [specific test filter]
```

---

## Preset 5 - Code-Only Change (No Test Edits)

```text
Fix production code only. Do not modify tests.

Task:
- [bug/feature]

Scope Allowed:
- [src file(s)]

Out of Scope:
- tests/

Validation:
- Run [build: fast] and [single targeted test]
- If tests fail and need test changes, STOP and explain why.
```

---

## Preset 6 - Guardrail Refactor (Small)

```text
Perform a tiny refactor only in listed files.

Objective:
- [e.g., reduce duplication in method X]

Scope Allowed:
- [file(s)]

Out of Scope:
- Behavior changes
- API signature changes
- Cross-file moves

Constraints:
- Mechanical refactor only
- Max [N] files / [M] lines

Validation:
- Run [build: fast]
- Confirm zero behavior change
```

---

## Preset 7 - Multi-Phase Plan (for risky changes)

```text
Use a 3-phase flow. Do not skip phases.

Phase 1 (read-only): root cause + proposed minimal patch.
Phase 2 (implement): apply only approved minimal patch.
Phase 3 (validate): run only [task/test], summarize deltas.

Scope Allowed:
- [files]

Stop gate:
- If new files are needed, pause and ask.
```

---

## Prompt Anti-Patterns (causes drift)

Avoid phrases like:

- "Fix this whole area"
- "Clean up while you're in there"
- "Make it robust"
- "Align everything"
- "Refactor as needed"

Replace with:

- "Fix only [specific behavior] in [specific file]."
- "Do not change anything outside listed scope."

---

## Fast Escalation Prompt (when agent starts drifting)

```text
Stop. You are out of scope.
Revert to the original task only:
- [original task]
Allowed files:
- [file list]
Forbidden:
- everything else
Continue with minimal patch only.
```

---

## Real Example (JARVIS Boundaries)

```text
Task: Keep JARVIS Chat within right-side bounded host; do not open fullscreen.

Scope Allowed:
- src/WileyWidget.WinForms/Services/PanelNavigationService.cs

Out of Scope:
- MainForm.* files
- Theme code
- Activity Log behavior
- Test files

Constraints:
- Minimal patch
- Max 1 file, max 80 lines
- If another file is needed, stop and ask

Validation:
- Run only build: fast
- Report what changed in panel hosting logic
```
