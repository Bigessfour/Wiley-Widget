# Enterprise WPF Startup Audit & Refactor Plan

> Reference document capturing the enterprise-grade audit of `App.xaml` / `App.xaml.cs` and the recommended phased remediation plan for resilient, observable, and maintainable startup.

## 🎯 Objectives
- Eliminate “fail before logging” blind spots.
- Reduce startup complexity & side-effects in constructor.
- Provide deterministic theming with fallback (Fluent Dark → Fluent Light → Default WPF).
- Unify Syncfusion license registration (idempotent, early, safe).
- Stage logging (bootstrap → full) for guaranteed capture.
- Defer non-critical work (DB, monitors, heavy sinks) until UI responsive.
- Extract infrastructure concerns into dedicated components.
- Introduce measurable startup metrics & validation gates.

## 🧭 Guiding Principles
| Principle | Application |
|-----------|-------------|
| Fail Fast (core) / Defer Heavy (non-core) | Only license + minimal resources before window. |
| Single Responsibility | Split giant `App.xaml.cs` into cohesive services. |
| Deterministic Order | Explicit phase boundaries with structured phase logging. |
| Observable Everywhere | Bootstrap logger before any risky call; SelfLog enabled. |
| Safe Fallbacks | Theme, license, DB, monitors degrade gracefully. |
| Idempotency | License registration & theme application safe to re-run. |

## 🚨 Current Pain Points
- Logging configured **after** calls to `Log.Information` (lost events).
- License registration depends on `_configuration` (null in ctor).
- Duplicate / divergent license registration methods.
- Theme applied 3 different ways (XAML attached property + merged dictionary + runtime service).
- Excess Serilog sinks at cold start (IO pressure, failure surface).
- `Debugger.Break()` in `ConfigureDatabaseServices()` halts automation.
- Obsolete NGEN + runtime `generatePublisherEvidence` tweak (ineffective in modern .NET).
- Giant monolithic file (~2400 lines) mixes 8+ concerns.

## 🗂️ Phase Breakdown
| Phase | Name | Timing | Purpose | MUST succeed? |
|-------|------|--------|---------|---------------|
| 0 | Bootstrap | Before `App` ctor / inside entrypoint | Minimal logging + early license (env/file) | Yes (fallback logger) |
| 1 | Core Startup | `OnStartup` (pre-Window) | Full config, rebuild logger, theme apply, basic DI | Yes (or terminate) |
| 2 | UI Activation | After MainWindow shown | Deferred heavy init (DB, monitors, extra sinks) | No (degrade) |
| 3 | Post-Ready | Background tasks | Health, resource telemetry, optional features | No |

## ✅ Target End-State (Summary)
| Concern | Current | Target |
|---------|--------|--------|
| Logging | Late + noisy | Two-stage (bootstrap + full), fewer early sinks |
| License | Duplicated logic | Single `LicenseRegistrar.TryRegister(Phase)` |
| Theme | Triple application conflict | Runtime-only, fallback cascade |
| DB Init | Blocking in startup | Deferred async with cancellation & telemetry |
| Monitors | Eager timers | Feature-flag & Phase 2 activation |
| Exceptions | Always handled dispatcher | Conditional fail-fast for fatal types |
| Structure | Monolith | Foldered services (Infrastructure/...) |

## 🛠️ Task-Oriented Implementation Checklist
Legend: [ ] Pending  [~] In Progress  [x] Done

### Phase 0 – Bootstrap (New `Program.cs`)
1. [x] Add `Program.cs` (STAThread) entrypoint (if not present) to own startup. (Added `Program.cs` with explicit `<StartupObject>` in csproj)
2. [x] Create `BootstrapLogger` (console + `logs/bootstrap.log`). (Minimal Serilog config writing to file + console)
3. [x] Log `StartupPhase=Bootstrap:Begin` & environment info. (Implemented at start of `Main` with env + OS + framework)
4. [x] Perform early license registration (env var → file) **only** (no config dependency). (Added `TryEarlySyncfusionLicense` ordering: env var then `license.key`)
5. [x] Seed correlation ID (GUID) and push into `LogContext`. (CorrelationId generated and pushed; property on bootstrap logger)
6. [x] Catch any exception → log fatal → exit with non-zero code. (Guarded try/catch around entire bootstrap phase)

### Phase 1 – Core Startup (`OnStartup`)
7. [x] Load configuration (`appsettings.json`, env vars, user secrets dev-only).
8. [x] Rebuild full Serilog logger (structured + human + errors) – keep early sink set slim.
9. [x] Enable Serilog SelfLog to `logs/selflog.txt`.
10. [x] Run idempotent license registrar again (adds config key path).
11. [x] Initialize ThemeService; apply with fallback sequence (Dark → Light → Default) + structured result log.
12. [x] Remove XAML theme attached property & redundant FluentDark resource dictionary from `App.xaml`.
13. [x] Instantiate & show MainWindow (no heavy ViewModel hydration inline).
14. [x] Emit `StartupPhase=CoreStartup:Complete` with elapsed ms.

### Phase 2 – Deferred Initialization (Post-Window Async)
15. [x] Kick off orchestrator `DeferredInitializer.StartAsync()` (fire & forget).
16. [x] Database warm-up with timeout + cancellation token + telemetry (log success/timeout/error).
17. [x] Start optional monitors (resource, health) gated by `Features:*` flags.
18. [x] Add ancillary Serilog sinks (performance, user-actions, theme-changes, syncfusion, health) AFTER stabilization.
19. [x] Log aggregated `StartupTimeline` (phases + durations) once.

### Phase 3 – Post-Ready Enhancements
20. [x] Register periodic health ping with jitter (avoid synchronized timers).
21. [x] Add structured “AppReady” event with memory snapshot & thread count.
22. [x] Wire theme switch UI to ThemeService; log structured transitions.
23. [x] Add guard for dispatcher exceptions (fatal classification: OOM, StackOverflow, AccessViolation → escalate).

### Cross-Cutting Refactors
24. [x] Extract classes into folders: `Infrastructure/Logging`, `Infrastructure/Licensing`, `UI/Theming`, `Diagnostics/Health`.
25. [x] Remove NGEN + Authenticode runtime tweak; document ReadyToRun (`PublishReadyToRun=true`).
26. [x] Consolidate performance metrics into `StartupMetrics` POCO.
27. [x] Replace `Debugger.Break()` with conditional log (`if (Debugger.IsAttached) ...`).
28. [x] Wrap resource dictionary merges with try/catch + fallback.
29. [x] Add unit test (where feasible) for license registrar idempotency.
30. [x] Document environment variables in README (SYNCFUSION_LICENSE_KEY, WILEYWIDGET_AUTOCLOSE_LICENSE, feature flags).

### Optional Hardening
31. [ ] Add `--diag-startup` CLI flag to force verbose metric logging.
32. [ ] Expose minimal `/healthz` local named pipe or loopback endpoint for external probe tools (future).
33. [ ] Introduce startup SLA test harness measuring TTFW (time-to-first-window).

## 🔐 Syncfusion License Strategy
Order of precedence: Config (Phase 1) > Embedded > Env > File. A single registrar method returns `LicenseStatus` enum (Success | Trial | Failed). Logs one structured event: `{ LicenseStatus, Source, Phase, ElapsedMs }`. Calls `LogTrialModeActivation()` only when final status not Success.

## 🎨 Theme Strategy
Runtime-only apply: `ThemeService.Apply(string desired)` → try dark; on exception try light; final fallback logs a warning and leaves default.
Maintain currently applied theme in a field to avoid re-adding dictionaries. Provide `ThemeChanged` event for UI binding.

## 🧾 Logging Strategy (Layered)
Bootstrap: Console + `bootstrap.log`.
Core: `structured-.log`, `app-.log`, `errors-.log`.
Deferred (opt-in): performance, user-actions, syncfusion, theme-changes, health, security.
Add `StartupPhase` property to all phase boundary events.
Enable SelfLog: `Serilog.Debugging.SelfLog.Enable(writer)` → rotate manually if > 1 MB.

## 🩺 Health & Resource Monitoring
Feature flags: `Features:EnableHealthMonitoring`, `Features:EnableResourceMonitoring`.
Load only in Phase 2. Each subsystem logs a single “activated” event with config snapshot.

## 🚦 Metrics & Validation
| Metric | Definition | Target |
|--------|------------|--------|
| TTFW | Time from process start to first window shown | < 1200 ms (cold) |
| EarlyFailureRate | Crashes before full logger ready | 0 |
| DeferredInitTime | Phase 2 duration | < 3000 ms |
| ThemeFallbackRate | Fallbacks / launches | < 1% |
| LicenseSuccessRate | Successful registrations / launches | 100% (non-dev) |

## 🧪 Verification Steps
1. Run with missing license → confirm trial mode logs & no crash.
2. Corrupt FluentDark dictionary (simulate) → confirm fallback to Fluent Light.
3. DB offline → startup still reaches window; timeout path logged.
4. Inject throw in Deferred initializer → failure logged, app usable.
5. Toggle feature flags off → monitors not initialized (log asserts absence).
6. Confirm bootstrap log contains earliest constructor messages.

## 🔄 Rollout Plan
1. Commit bootstrap + structural extraction (no behavior change except logging staging).
2. Add theme & license refactor.
3. Introduce deferred initializer & feature flags.
4. Remove legacy / deprecated code (NGEN, Authenticode tweak, Debugger.Break).
5. Activate reduced sink set; monitor log integrity.
6. Re-enable ancillary sinks gradually.

## 🧯 Backout Plan
- If issues appear, revert to pre-refactor branch (retain bootstrap logger if stable).
- Keep legacy `App.xaml.cs` snapshot for 1 sprint; delete after confidence builds.

## 📁 Proposed New Structure
```
Infrastructure/
  Logging/BootstrapLogging.cs
  Licensing/LicenseRegistrar.cs
  Theming/ThemeService.cs (refined)
  Diagnostics/Health/HealthMonitorHost.cs
Startup/DeferredInitializer.cs
Program.cs
```

## 🗒️ Notes / Decisions Log
| Date | Decision | Rationale |
|------|----------|-----------|
| 2025-09-03 | Two-stage logging | Capture earliest failures |
| 2025-09-03 | Remove XAML theme binding | Centralize & enable fallback |
| 2025-09-03 | Feature flag monitors | Reduce baseline startup risk |

## 📌 Open Questions (To Clarify Before Coding)
- Need acceptance threshold for removing emojis in production logs? (`AppSettings:Logging:UseEmojis`).
- Will DB be optional long-term (allow “readonly mode” banner)?
- Any compliance requirement for security log retention beyond 90 days?

## 🚀 Implementation Order (Concise)
1. Add `Program.cs` + bootstrap logger.
2. Strip theme from `App.xaml` (keep only core resources).
3. Extract license registrar & adapt ctor + OnStartup sequence.
4. Rebuild logging (phase separation) + SelfLog.
5. Add theme fallback logic + update ThemeService.
6. Introduce deferred initializer & move DB / monitors there.
7. Add feature flags + config docs.
8. Prune legacy code (NGEN, generatePublisherEvidence, Debugger.Break()).
9. Add metrics object + final startup report log.
10. Tighten dispatcher exception policy.

---
Prepared: 2025-09-03
