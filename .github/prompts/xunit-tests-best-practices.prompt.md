---
mode: agent
description: Generate or refactor xUnit tests using Microsoft .NET unit-testing best practices and the xunit-test-best-practices skill.
---

Generate xUnit tests for the requested target using the `xunit-test-best-practices` skill.

Follow these requirements:

- Use test names in `MethodName_Scenario_ExpectedBehavior` format.
- Use explicit Arrange, Act, Assert sections.
- Keep one behavior per test and one Act phase per test.
- Use `[Theory]` and `[InlineData]` for multiple input permutations instead of loops.
- Avoid logic in tests (`if`, `for`, `while`, `switch`) unless absolutely necessary.
- Avoid magic strings and values when intent is unclear; use named constants.
- Test public behavior only; do not target private methods directly.
- Keep tests deterministic and isolated from infrastructure (file system, network, DB, clock, environment).
- Introduce seams/interfaces for static or time-based dependencies.

Execution steps:

1. Identify target behavior(s) and nearest existing test file.
2. Add or update tests with minimal, focused assertions.
3. Reuse existing builders/helpers where possible; add concise helpers when needed.
4. Run targeted tests first, then broader suite only if required.
5. Report what changed, what best-practice rules were applied, validation commands/results, and any remaining gaps.

Input to provide when using this prompt:

- Target file/class/method.
- Behavior(s) to validate.
- Whether to add new tests, refactor existing tests, or both.
