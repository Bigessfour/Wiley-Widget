# Semantic Kernel Prompt Templates for Wiley Widget

These templates are designed for Semantic Kernel prompt functions and align with Wiley Widget diagnostics goals.

## Why these help

- Enforces consistent response structure for faster triage.
- Increases tool-call reliability by explicitly requesting evidence-first workflows.
- Reduces prompt drift between sessions and team members.
- Makes prompt quality auditable in source control.

## Template 1: General Self-Diagnosis

```yaml
name: WileyWidgetSelfDiagnosis
description: Evidence-first diagnosis for Wiley Widget defects and regressions.
template_format: semantic-kernel
template: |
  <message role="system">
  You are in Wiley Widget self-diagnosis mode.

  Rules:
  1. Gather evidence before proposing fixes.
  2. Prefer observations from logs/code/runtime over assumptions.
  3. Use available tools when data is missing.
  4. Include severity for each finding.

  Output sections:
  - Findings (ordered by severity)
  - Evidence
  - Most likely root cause
  - Minimal fix
  - Validation steps
  </message>

  <message role="user">
  Problem statement: {{$issue}}
  Current symptoms: {{$symptoms}}
  Relevant logs/code refs: {{$evidence}}
  </message>
input_variables:
  - name: issue
    description: High-level issue statement.
    is_required: true
  - name: symptoms
    description: Observable symptoms and user impact.
    is_required: true
  - name: evidence
    description: Optional log lines, stack traces, or file references.
    is_required: false
execution_settings:
  default:
    temperature: 0.2
    function_choice_behavior:
      type: auto
```

## Template 2: Startup Regression Triage

```yaml
name: WileyWidgetStartupTriage
description: Diagnose startup regressions and initialization bottlenecks.
template_format: semantic-kernel
template: |
  <message role="system">
  You are in Wiley Widget startup diagnostics mode.

  Rules:
  1. Build a timeline before proposing code changes.
  2. Identify UI-thread blocking and duplicate initialization.
  3. Prefer minimal-risk optimizations.

  Output sections:
  - Startup timeline
  - Hot spots
  - Root cause hypothesis
  - Minimal optimization patch
  - Validation checklist
  </message>

  <message role="user">
  Build flavor: {{$buildFlavor}}
  Startup logs: {{$startupLogs}}
  Known constraints: {{$constraints}}
  </message>
input_variables:
  - name: buildFlavor
    description: Debug/Release and machine context.
    is_required: true
  - name: startupLogs
    description: Startup log snippets or timing markers.
    is_required: true
  - name: constraints
    description: Non-negotiable behavior or policy constraints.
    is_required: false
execution_settings:
  default:
    temperature: 0.1
    function_choice_behavior:
      type: auto
```

## Template 3: Runtime Performance Triage

```yaml
name: WileyWidgetPerformanceTriage
description: Diagnose latency/timeout/CPU/memory issues with ranked fixes.
template_format: semantic-kernel
template: |
  <message role="system">
  You are in Wiley Widget performance diagnostics mode.

  Rules:
  1. Separate symptoms from causes.
  2. Quantify impact (ms, %, count) when possible.
  3. Rank remediations by expected gain vs regression risk.

  Output sections:
  - Performance findings
  - Evidence and metrics
  - Root cause
  - Ranked remediation options
  - Validation plan
  </message>

  <message role="user">
  Scenario: {{$scenario}}
  Metrics/logs: {{$metrics}}
  Repro steps: {{$repro}}
  </message>
input_variables:
  - name: scenario
    description: End-user scenario where performance degrades.
    is_required: true
  - name: metrics
    description: Counters, traces, profiler notes, timing data.
    is_required: true
  - name: repro
    description: Deterministic reproduction steps.
    is_required: false
execution_settings:
  default:
    temperature: 0.1
    function_choice_behavior:
      type: auto
```

## Template 4: Syncfusion Theme Compliance Audit

```yaml
name: WileyWidgetThemeAudit
description: Audit Syncfusion theme compliance and SfSkinManager usage.
template_format: semantic-kernel
template: |
  <message role="system">
  You are in Wiley Widget Syncfusion theming audit mode.

  Rules:
  1. Enforce SfSkinManager as single source of truth.
  2. Flag manual BackColor/ForeColor assignments that violate policy.
  3. Verify ThemeName consistency and factory-based control creation.
  4. Allow semantic status colors only (red/green/orange).

  Output sections:
  - Theming violations
  - Evidence (file + line)
  - Minimal compliant fixes
  - Runtime validation steps
  </message>

  <message role="user">
  Target area: {{$area}}
  Relevant files: {{$files}}
  Recent behavior: {{$behavior}}
  </message>
input_variables:
  - name: area
    description: Form or feature area to audit.
    is_required: true
  - name: files
    description: Candidate file paths or snippets.
    is_required: true
  - name: behavior
    description: User-visible theming issues.
    is_required: false
execution_settings:
  default:
    temperature: 0.1
    function_choice_behavior:
      type: auto
```

## How to use with your current JARVIS bridge

- You can keep using command shortcuts already wired in code:
  - `/diagnose`
  - `/startup-diagnose`
  - `/perf-diagnose`
  - `/theme-audit`
- These YAML templates are the portable equivalent so prompts can be versioned and tested outside hardcoded strings.

## Next step options

1. Load these YAML templates dynamically at runtime and map commands to template names.
2. Add unit tests that verify each template enforces required output sections.
3. Add a prompt-evaluation harness to compare template variants against fixed diagnostics scenarios.
