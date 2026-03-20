# Wiley Widget

[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![WinForms](https://img.shields.io/badge/UI-WinForms-blue.svg)](https://learn.microsoft.com/dotnet/desktop/winforms/)
[![Syncfusion](https://img.shields.io/badge/Syncfusion-33.1.44-orange.svg)](https://www.syncfusion.com/products/communitylicense)
[![Build](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/build-winforms.yml/badge.svg)](https://github.com/Bigessfour/Wiley-Widget/actions)
[![Tests](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/test-coverage.yml/badge.svg)](https://github.com/Bigessfour/Wiley-Widget/actions)
[![Security](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/security-scan.yml/badge.svg)](https://github.com/Bigessfour/Wiley-Widget/actions)

Wiley Widget is a local-first Windows desktop application for municipal finance work. The product is built on .NET 10, WinForms, and Syncfusion, with a large shared shell around panel navigation, theming, reporting, and optional integrations such as QuickBooks.

## Current Status

- Status: release stabilization
- Platform: Windows only
- UI stack: WinForms + Syncfusion 33.1.44
- Architecture: layered services and repositories with MVVM-style panels
- Release posture: a green build is necessary, but release sign-off depends on targeted proof of startup, navigation, layout, theming, and critical workflows

This repository has gone through several architectural and workflow shifts. The goal of the current documentation set is to reflect the repository as it exists now, not as it looked in earlier phases.

Generated files such as `AI-BRIEF.md` and `ai-fetchable-manifest.json` can still be useful snapshots, but they are not the source of truth for release readiness.

## What Matters For Release

- Protect existing behavior while fixing or polishing adjacent code.
- Prefer focused regression proof over broad but low-signal test counts.
- Treat shell changes, shared methods, panel infrastructure, theme handling, and layout as high-blast-radius areas.
- Do not count stale reports, filtered zero-test runs, or `dotnet test WileyWidget.sln` by itself as release evidence.

The release-oriented workflow lives in `.vscode/approved-workflow.md` and the release checklist lives in `docs/PRE_RELEASE_CHECKLIST.md`.

## Repository Map

```text
src/
  WileyWidget.WinForms/        Main desktop application and shell
  WileyWidget.Business/        Domain logic
  WileyWidget.Data/            Data access and EF Core
  WileyWidget.Models/          Shared models
  WileyWidget.Services/        Application and integration services
tests/
  WileyWidget.WinForms.Tests/  Primary proof lane for shell and panel behavior
  WileyWidget.UiTests/         Exploratory and focused UI investigation
docs/                          Release, testing, UI, and operations guidance
scripts/                       Local automation and diagnostics
.vscode/                       Workspace instructions, tasks, and agent guidance
```

## Local Development

1. Restore packages.

   ```powershell
   dotnet restore WileyWidget.sln
   ```

2. Configure a Syncfusion license if you need to run the UI.

   ```powershell
   pwsh ./scripts/setup-license.ps1
   ```

3. Build the solution.

   ```powershell
   dotnet build WileyWidget.sln -m:2
   ```

   For quicker local iteration, use the VS Code `build: fast` task.

4. Run the WinForms application.

   ```powershell
   dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
   ```

5. Use focused proof for the area you changed. Start with `docs/TESTING_STRATEGY.md` instead of defaulting to a whole-solution test command.

## Documentation Map

- `QUICK_START.md`: current developer setup and day-to-day workflow
- `CONTRIBUTING.md`: contribution rules for stabilization work
- `docs/TESTING_STRATEGY.md`: what counts as proof and which test lanes matter
- `docs/V1_0_RELEASE_SCOPE.md`: proposed v1.0 release slice and stop-ship criteria
- `docs/V1_0_BLOCKER_MATRIX.md`: working release blocker board for v1.0
- `docs/PRE_RELEASE_CHECKLIST.md`: release sign-off checklist
- `Done_Checklist.md`: panel certification checklist for in-scope panels
- `.vscode/approved-workflow.md`: workspace workflow for making safe, proven changes
- `.vscode/copilot-instructions.md`: workspace instruction set for agents and contributors using Copilot

## Working Agreement

- If a doc is wrong, fix it close to the code change that made it wrong.
- If a fix changes shared behavior, prove both the new behavior and the behavior that must remain intact.
- If a workflow is not release-critical, do not document it as if it were.
