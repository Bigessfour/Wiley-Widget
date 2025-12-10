# .NET 10 Upgrade — Compatibility & Migration Checklist

This guide is a high-level checklist and set of recommendations to help upgrade Wiley Widget from .NET 9 -> .NET 10.
It focuses on components that commonly cause friction in WinForms apps: Syncfusion controls, third-party libraries, and runtime behavior.

⚠️ Note: This repository is large and contains UI components, native interop, third-party licensed libraries (Syncfusion/BoldReports), and CI pipelines. Upgrade work should be done on a feature branch and validated thoroughly in CI and a test environment before merging.

---

## 1. Quick assessment

- Confirm current TargetFramework for all projects (most projects currently target `net9.0(-windows)`).
- Identify packages that directly target Windows (e.g., Syncfusion.WinForms packages, BoldReports WPF) and note their current versions.

## 2. Prepare your environment

- Install .NET 10 SDK on developer machines and in CI agents.
- Update `global.json` (if present) to pin .NET 10 SDK for deterministic builds.
- Clone a fresh branch: `git checkout -b feat/upgrade/dotnet-10`.

## 3. Automatic project updates (fast path)

- Update project files: change `<TargetFramework>net9.0-windows</TargetFramework>` → `<TargetFramework>net10.0-windows</TargetFramework>` for each relevant project.
- Run `dotnet restore` → `dotnet build` and collect all compilation errors.

## 4. Package compatibility

- Syncfusion: Verify Syncfusion WinForms packages have releases for .NET 10. If not, identify the minimum supported version, and upgrade packages accordingly.
  - If packages require a newer API surface, incrementally update the Syncfusion package versions in `.csproj` and recompile.
  - Confirm licensing registration and runtime behavior (Syncfusion licensing code must run early in `Program.Main`).

- BoldReports/WPF: Validate WPF-hosted components and ElementHost interop under .NET 10.

- Polly / Resilience: This repo already contains modern Polly references and docs. Confirm any code using older Polly v7 API is modernized (see docs/reference/POLLY_ENHANCEMENT_RECOMMENDATIONS.md).

- Third-party SDKs (QuickBooks SDKs, Intuit): Verify compatibility or upgrade to new SDK versions.

## 5. WinForms & UI changes

- Ensure `ApplicationConfiguration.Initialize()` behavior remains unchanged in .NET 10 (this is platform policy code; verify to catch any breaking changes).
- Validate thread/STA behaviors for WinForms controls (FlaUI/UI Tests must still create STA threads properly).
- Re-run UI tests (WinForms integration tests, FlaUI tests) and iterate on UI issues.

## 6. Prism & Syncfusion integration

- If your app references Prism and Syncfusion integration layers, audit for direct Prism dependencies in the WinForms code.
- This repository already contains helper scripts and guidance to remove Prism/Syncfusion coupling (see `scripts/tools/remove-syncfusion-prism.ps1`). Use those scripts in a local branch to see what needs to be updated.

## 7. Database & EF Core

- Verify EF Core package versions are compatible with .NET 10.
- Ensure any provider (Microsoft.Data.SqlClient) is upgraded if required.
- Run DB-driven integration tests and local migrations to catch runtime data provider issues.

## 8. Resilience & Telemetry

- Ensure Polly pipelines and rate-limiters are configured (this repo contains POLLY_ENHANCEMENT_RECOMMENDATIONS).
- Confirm OpenTelemetry / ApplicationInsights SDKs support .NET 10 and update versions as needed.

## 9. Build & CI updates

- Update CI images and runner versions to include .NET 10 SDKs.
- Update `global.json` in repo root to pin .NET 10 for all contributors.
- Add a temporary compatibility gate in CI that builds/boots a smoke test for the app.

## 10. Runtime validation & QA

- Run the entire test suite (unit + integration + UI). Expect iterative fixes.
- Validate logging, performance, and memory profile — .NET 10 may have minor GC/runtime improvements.
- Verify licensing/packer steps for Syncfusion (packaging and deployment may need updates).

## 11. Rollout plan

- Merge to `develop` or a release branch only after CI is green on .NET 10.
- Use canary or staged rollout, especially for UI-heavy releases with new runtime.

---

## Appendix: Common compatibility flags and checks

- Search code for platform-specific APIs (e.g., System.Management, COM interop). These can be a source of regressions.
- Validate 3rd-party native dependencies (if present) for .NET 10.
- If you need to avoid a full repo upgrade at once, consider a per-project target change and teaser PRs to incrementally migrate.

If you'd like, I can create a branch that performs a staged migration (project-by-project) and run the unit & UI test matrix to find concrete failures to fix.
