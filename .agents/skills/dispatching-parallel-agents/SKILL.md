---
name: dispatching-parallel-agents
description: Use when 2+ independent problem domains can be investigated concurrently without shared-state conflict.
---

# Dispatching Parallel Agents (Wiley Widget)

## Purpose

Speed up multi-issue work by splitting independent investigations/fixes.

## Use When

- Multiple failing tests in unrelated files
- Separate subsystems fail independently (for example: auth flow, UI rendering, data layer)
- Work can proceed without editing same files

## Do Not Use When

- Failures likely share one root cause
- Issues depend on common mutable state
- Work requires strict sequencing

## Process

1. Partition by domain.

- Define 2-4 independent scopes with no overlapping files.

2. Create focused tasks.

- One clear objective per scope.
- Include constraints (for example, no cross-scope edits).

3. Run parallel investigation/fixes.

- Execute only independent tracks in parallel.

4. Integrate and verify.

- Merge results.
- Resolve collisions.
- Run full relevant validation once integrated.

## Prompt Template

- Scope: [exact subsystem/test file]
- Goal: [specific expected outcome]
- Constraints: [what must not be changed]
- Deliverables: [root cause, exact edits, validation output]

## Wiley-Specific Guardrails

- Never run concurrent `dotnet build` or `dotnet test` jobs in this workspace.
- Parallelize code search/reading and independent editing only.
- Final validation is serialized.
