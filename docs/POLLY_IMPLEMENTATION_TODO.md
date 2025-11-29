# Polly Resilience Implementation — Project TODO

Generated: 2025-11-28
Author: Copilot — repository tracker

This document enumerates the prioritized tasks required to bring Polly resilience
patterns to a consistent, production-ready state across the Wiley Widget codebase.
Use this file as the authoritative worklist when implementing changes.

Status key: ✅ completed | 🔁 in-progress | ⏳ not-started

## 1. 🔁 Decide and lock central Polly package versions

- File: `Directory.Packages.props`
- Goal: Confirm whether Polly 8.6.5 is intentional. If not, align to verified stable (recommended: 8.2.4).
- Acceptance: CI builds clean, no runtime mismatch for Microsoft.Extensions.Http.Resilience.

## 2. ⏳ Add centralized DI-registered resilience pipelines (HTTP / QuickBooks / DB / Export)

- Files to add/modify: `src/Program.cs` (or `Startup.cs`), `src/WileyWidget/Startup/ResiliencePolicyConfiguration.cs`
- Goal: Provide named ResiliencePipeline(s) via DI to be reused by services.

## 3. ⏳ Refactor QuickBooks clients to use DI-resilience pipelines

- Files: `src/WileyWidget.Services/QuickBooksApiClient.cs`, `src/WileyWidget.Services/QuickBooksService.cs`
- Goal: Replace ad-hoc retry logic with a shared `quickbooks` pipeline; add token refresh resilience.

## 4. ⏳ Refactor Export (PDF/Excel) services to use file I/O resilience pipeline

- Files: `src/WileyWidget.Services/Export/*` (QuestPdfExportService.cs, ClosedXmlExportService.cs, ReportExportService.cs)
- Goal: Wrap disk/network I/O operations with Retry + Timeout + Fallback and queueing as fallback.

## 5. ⏳ Ensure DatabaseResiliencePolicy is used consistently across repositories

- Files: `src/WileyWidget.Data/*` repos and service classes
- Goal: All DB writes/reads use `DatabaseResiliencePolicy.ExecuteWriteAsync` / `ExecuteReadAsync`.

## 6. ⏳ Add telemetry/logging hooks for policy events (Serilog / OpenTelemetry)

- Files: telemetry setup (Program.cs or TelemetryService), add `onRetry`/`onBreak` logs and metrics.
- Goal: Record retries, circuit-breaker transitions, and timeouts in telemetry.

## 7. ⏳ Add unit tests & integration tests for resilience behavior

- Files: tests/WileyWidget.\*.Tests — add tests for retries, circuit breaker opens, timeout cancellations
- Goal: CI asserts policy behavior; add mocks/fault injections.

## 8. ⏳ CI validation and chaos testing harness

- Add tests to CI to validate resilience and optionally run chaos/fault-injection runs.

## 9. ⏳ Documentation updates

- Update `README.md` Resilience section, `docs/reference/POLLY_ENHANCEMENT_RECOMMENDATIONS.md`, and changelog entries.

## 10. ⏳ Prepare PR and run full builds/tests

- Ensure Roslyn analyzers, whitespace rules, and problems panel are clean before PR.

---

Notes

- Follow `.vscode/approved-workflow.md` when making edits that touch three or more files (create an MCP PR branch and ensure CI passes before merging).
- This file is the canonical runbook for the Polly improvements — update statuses here as work progresses.
