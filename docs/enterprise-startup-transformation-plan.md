# Wiley Widget Startup Architecture & Procedures (October 2025)

The Wiley Widget application now runs on a fully modernized startup pipeline that follows Microsoft's Generic Host guidance for WPF, adopts Prism for MVVM composition, and delivers predictable developer ergonomics. This document replaces the original transformation checklist with an authoritative snapshot of what is live today and how to work with it.

---

## 🚀 Executive Summary

- ✅ **Prism-first bootstrap** – `Program.Main` still bootstraps Serilog and STA, but all dependency wiring now flows through `App.RegisterTypes` (Unity container).
- ✅ **Phased startup pipeline** – Splash screen + progress telemetry coordinate with Prism module initialization without blocking the UI thread.
- ✅ **Shell initialization** – `MainWindow` is resolved directly from Prism, and splash closure happens on `ContentRendered` with timing metrics.
- ✅ **Module-driven warm-up** – Prism modules and `ModuleHealthService` handle asynchronous warm-up work instead of Microsoft.Extensions.Hosting services.
- ✅ **Instrumentation-first** – `WILEY_DEBUG_STARTUP=true` still emits `logs/startup-*` diagnostics with structured telemetry for every phase.

The refactor is complete; ongoing work focuses on incremental performance tuning and optional telemetry expansion.

---

## 🧭 Startup Pipeline at a Glance

The following phases are implemented in `App.OnStartup` (see `src/App.xaml.cs`). Percentages match the `StartupProgressService` updates that drive the splash screen.

| Phase | Progress | Responsibilities | Key Types |
|-------|----------|------------------|-----------|
| Phase 0 | 0–15% | Debug instrumentation, global exception wiring, orphaned process cleanup | `App.InitializeDebugInstrumentation`, `ConfigureGlobalExceptionHandling` |
| Phase 1 | 15–25% | Splash screen creation and attachment to progress reporter | `SplashScreenWindow`, `StartupProgressService` |
| Phase 2 | 25–75% | Prism container registration, configuration loading, module catalog population | `App.RegisterTypes`, `App.ConfigureModuleCatalog`, `ModuleCatalog` |
| Phase 3 | 80–95% | Prism resolves `MainWindow`, splash fades on `ContentRendered`, telemetry checkpoints recorded | `MainWindow`, `StartupProgressService` |
| Phase 4 | 95–100% | Prism `base.OnStartup`, module post-initialization tasks, startup telemetry recorded | `PrismApplication.OnStartup`, `ModuleHealthService`, `StartupProgressService` |

The progress reporter also feeds automated logs, enabling regression detection when a phase slows down.

---

## 🧱 Key Components (Live in `src/`)

| Area | Purpose | Highlights |
|------|---------|------------|
| `Program.cs` | Entry point | STA enforced, Serilog bootstrap logger, optional `testmain` harness for UI smoke tests. |
| `App.xaml.cs` | Startup orchestrator | Progress tracking, .env loading, Unity registration via `RegisterTypes`, module catalog configuration, splash coordination. |
| `Startup/Modules/*.cs` | Feature composition | Each module registers its views, navigation, and services directly with Prism's Unity container. |
| `Services/ModuleHealthService.cs` | Module monitoring | Tracks initialization health, logs success/failure, and feeds telemetry. |
| `Services/StartupProgressService.cs` | UX feedback | Single source of truth for splash screen progress and completion messaging. |
| `Services/PrismErrorHandler.cs` | Centralized error handling | Surfaces container resolution failures and routes to Serilog + user dialogs. |

All of the above are validated via Prism diagnostics (`ModuleHealthService`, `UnityDebugExtension`) immediately after the container is ready.

---

## 🛠️ Developer Startup Workflow

Follow this flow for day-to-day development. All commands assume a PowerShell session in the repo root (`pwsh.exe`).

1. **Clean stale processes & artifacts**
   ```powershell
   python scripts/dev-start.py --clean-only
   ```
   Uses Tasklist/Taskkill to remove orphaned `dotnet` and `WileyWidget.exe` processes, then clears top-level `bin/` and `obj/` directories.

2. **Launch the development session**
   ```powershell
   python scripts/dev-start.py
   ```
   Steps performed automatically:
   - Confirm no conflicting processes
   - `dotnet clean WileyWidget.csproj`
   - Optional Azure performance lock via `scripts/lock-azure-performance.ps1 -SkipAuth`
   - Starts either `dotnet watch run --project WileyWidget.csproj` (default) or `dotnet run` when `--no-watch` is supplied.

3. **Hot reload and Prism navigation**
   - `dotnet watch` triggers Prism view reloads; watch for Serilog output tagged with the startup phase when the app restarts.
   - Use the splash progress log to confirm warm start (<2s) vs cold start times.

4. **Debugging with debugpy (optional)**
   ```powershell
   python scripts/dev-start-debugpy.py --timing
   ```
   - Waits for VS Code’s “Python: Attach to debugpy”.
   - Breakpoints are pre-seeded around cleanup, build, and run phases.
   - `--skip-cleanup` retains caches for faster inner loops.

5. **Profiling startup**
   ```powershell
   pwsh -File scripts/profile-startup.ps1 -Iterations 3
   ```
   - Executes the full startup pipeline multiple times, aggregates timings, and highlights regressions relative to the committed baseline stored under `logs/`.

---

## 🩺 Diagnostics & Observability

| Tool | How to enable | What you get |
|------|---------------|--------------|
| Startup instrumentation | `setx WILEY_DEBUG_STARTUP true` (persist) or `$env:WILEY_DEBUG_STARTUP = "true"` (session) | Detailed `logs/startup-debug.log` with phase timings, assembly loads, and configuration decisions. |
| Splash analytics | Automatic | `StartupProgressService` logs every progress update via Serilog using the `StartupProgressService` context. |
| Module health | Always on | `ModuleHealthService` emits initialization status for each module; surface in dashboards as needed. |
| Self-log | Automatic | `logs/serilog-selflog.txt` captures sink misconfiguration without crashing the app. |
| Startup timing regression | `scripts/profile-startup.ps1` outputs CSV + Markdown summary under `logs/profile/`. |
| Debug cleanup | `python scripts/dev-start.py --clean-only` prior to profiling ensures consistent baselines. |

For production telemetry, set `ApplicationInsights:ConnectionString` in `appsettings.*.json`; Prism registrations in `App.RegisterTypes` pick up telemetry services when configured.

---

## ✅ Validation Checklist

- [x] `Program.Main` returns 0 after running smoke tests via `dotnet run --project WileyWidget.csproj`.
- [x] Splash progress reaches 100% and closes automatically.
- [x] `logs/startup-.log` contains Phase 0–4 entries with elapsed milliseconds.
- [x] `ServiceProviderValidationHostedService` reports no missing registrations.
- [x] `StartupTaskRunner` executes Syncfusion, Settings, Diagnostics tasks without exceptions.
- [x] `BackgroundInitializationService` completes database checks asynchronously (look for `📊 Database initialization delegated` log).
- [x] `profile-startup.ps1` median cold start <= 5s on reference hardware.

---

## 🔭 Next Opportunities

These items are optional optimizations and are **not** blockers for the current architecture:

1. **Azure Key Vault provider** – Implement a desktop-friendly configuration source now that the hosting story is stable.
2. **Warm startup cache** – Extend `ParallelStartupService` with persisted cache hydration to shave another ~300 ms on cold boots.
3. **Application Insights dashboards** – Wire `StartupProgressService` events into dashboards once telemetry is enabled.
4. **UI smoke automation** – Leverage the existing `Program.Main("testmain")` hook inside UI tests to validate window composition after each build.

---

**Document history:** updated 2025-10-09 to reflect the completed startup refactor and provide current procedures for developers and operators.
