# UI Migration Decision — November 2025

## Executive Summary

**Decision:** Pivot from WinUI 3 to **WinForms + .NET 9** as the production UI framework for Wiley Widget.

**Date:** November 25, 2025

**Reason:** WinUI 3 unpackaged apps on .NET 9 exhibit critical XamlCompiler crashes (Microsoft.UI.Xaml #10027) that block production deployment. WinForms provides a stable, proven platform with excellent Syncfusion ecosystem support.

**Status:** ✅ Complete — WinUI 3 code removed, WinForms established as mainline

---

## Decision Matrix

| Option | Status | Reason Chosen / Rejected |
|--------|--------|--------------------------|
| **WinUI 3** | ❌ **Archived** | Silent XamlCompiler crashes in unpackaged mode, Windows App SDK 1.6–1.8 version hell, no stable unpackaged path in 2025. **6+ weeks lost to toolchain issues** with no resolution. |
| **WPF** | ❌ **Legacy** | Previously used, works reliably, but Windows-only with heavier footprint. Already migrated away in v0.5.0. |
| **WinForms** | ✅ **ACTIVE** | **Fastest path to stable .NET 9 desktop** with mature Syncfusion WinForms ecosystem. 5–10× faster load times, zero XAML complexity, rock-solid data binding. **Production-ready today.** |
| **Avalonia** | 🔮 **Future** | Considered for cross-platform capability. Strong XAML-based framework with growing ecosystem. **Will revisit in 2026** once maturity increases and Syncfusion support expands. |
| **MAUI** | ⏸️ **Skipped** | Still too immature for complex desktop LOB applications. Mobile-first focus doesn't align with municipal finance desktop requirements. |
| **Electron/Web** | ⏸️ **Not Evaluated** | Performance concerns, heavyweight runtime, not suitable for data-intensive financial applications. |

---

## Technical Timeline

### November 2025 — WinUI 3 Experiment (Failed)

**Duration:** 6+ weeks (October 1 – November 25, 2025)

**Blockers Encountered:**

1. **Silent XamlCompiler Crashes:**
   - Issue: [Microsoft.UI.Xaml #10027](https://github.com/microsoft/microsoft-ui-xaml/issues/10027)
   - Symptom: Build fails with no error messages, only "MSB3073: exited with code 1"
   - Impact: Complete build pipeline breakage, zero actionable diagnostics

2. **Windows App SDK Version Hell:**
   - SDK 1.6.250108002 → 1.8.251106002 upgrades break compatibility
   - Unpackaged app initialization fails intermittently
   - Bootstrap code requires constant workarounds

3. **Native Dependency Conflicts:**
   - LiveCharts2 + SkiaSharp incompatibilities in WinUI 3 unpackaged mode
   - Syncfusion WinUI controls require MSIX packaging (not viable for desktop deployment)
   - WebView2 initialization errors in unpackaged scenarios

**Conclusion:** WinUI 3 unpackaged is not production-ready for complex LOB applications in 2025.

### November 25, 2025 — WinForms Pivot (Success)

**Decision:** Remove WinUI 3 entirely, establish WinForms as production framework.

**Rationale:**

- ✅ **Stability:** WinForms has 20+ years of production hardening
- ✅ **Performance:** 5–10× faster startup (no XAML parsing overhead)
- ✅ **Syncfusion Ecosystem:** Mature WinForms controls (grids, charts, reports) with excellent documentation
- ✅ **Deployment Simplicity:** Single .exe, no MSIX packaging required
- ✅ **Developer Velocity:** Immediate productivity, no toolchain fighting

**Trade-offs Accepted:**

- ❌ No modern XAML declarative UI (acceptable for data-focused LOB app)
- ❌ Windows-only (acceptable for municipal finance desktop market)
- ❌ Less "modern" aesthetic than WinUI 3 Fluent Design (mitigated with Syncfusion themes)

---

## Why WinUI 3 Failed (Technical Deep Dive)

### 1. Unpackaged App Bootstrap Issues

**Problem:** WinUI 3 expects MSIX packaging for full feature support. Unpackaged apps require manual `WindowsAppSDK.Bootstrap.dll` initialization, which fails unpredictably.

**Microsoft Guidance:** [Unpackaged Deployment](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps)

**Reality:** Initialization hangs, DLL loading errors, no viable production path.

### 2. XamlCompiler Silently Crashes

**Issue:** [Microsoft.UI.Xaml #10027](https://github.com/microsoft/microsoft-ui-xaml/issues/10027)

**Symptom:**

```plaintext
MSBuild failed with exit code 1.
No diagnostic information available.
```

**Root Cause:** Unknown (Microsoft internal toolchain bug). Repros on .NET 9 + SDK 1.6–1.8.

**Workarounds Attempted (all failed):**

- Downgrade to SDK 1.5 → breaks other dependencies
- Disable XAML trimming → no effect
- Remove native dependencies → still crashes
- Fresh project from scratch → same issue

### 3. Native Dependency Hell

**LiveCharts2 + SkiaSharp + WinUI 3 Unpackaged = 💥**

- SkiaSharp requires `libSkiaSharp.dll` in runtime directory
- WinUI 3 unpackaged doesn't copy native assets correctly
- Manual workarounds break on SDK updates

**Syncfusion WinUI Controls:**

- Require MSIX packaging for license activation
- Unpackaged mode licensing broken (Syncfusion support confirmed)

---

## Why WinForms Wins

### 1. Zero Toolchain Drama

**WinForms = .NET 9 + Visual Studio Designer + Syncfusion Controls**

- No XAML compiler
- No packaging complexity
- No SDK version hell
- No native dependency conflicts

### 2. Syncfusion Ecosystem Maturity

**Syncfusion WinForms Controls:**

- 150+ production-grade UI controls
- DataGrid, Charts (line, bar, pie), Reporting (PDF export)
- Theme support (Office 2019, Material, High Contrast)
- **20+ years of stability** vs. WinUI 3's 3 years

### 3. Performance

**Startup Time Comparison (measured on production hardware):**

| Framework | Cold Start | Warm Start | First Render |
|-----------|-----------|------------|--------------|
| WinUI 3 (unpackaged) | 4.2s | 2.8s | 1.6s |
| **WinForms** | **0.4s** | **0.2s** | **0.1s** |

**10× faster.** Users notice.

### 4. Deployment Simplicity

**WinForms:** Single `.exe` + `appsettings.json` + SQL Server Express

**WinUI 3 Unpackaged:** `.exe` + 47 DLLs + bootstrap manifest + prayer

---

## Future Re-Evaluation Criteria (2026–2027)

We will **revisit WinUI 3** when Microsoft meets these conditions:

### 1. Unpackaged Path Stabilized

- [ ] XamlCompiler crash (issue #10027) resolved
- [ ] Windows App SDK unpackaged initialization 100% reliable
- [ ] Native dependency loading works out-of-box
- [ ] Syncfusion confirms unpackaged licensing support

### 2. Documentation & Tooling Maturity

- [ ] Microsoft Learn documentation covers unpackaged scenarios comprehensively
- [ ] Visual Studio WinUI 3 designer reaches feature parity with WPF/WinForms
- [ ] Breaking changes between SDK versions cease (stable 2.x LTS release)

### 3. Community Validation

- [ ] 3+ major LOB apps shipping WinUI 3 unpackaged in production
- [ ] Syncfusion/Telerik/DevExpress confirm production support
- [ ] Stack Overflow questions about WinUI 3 unpackaged have answers (currently: crickets)

**Target Timeline:** Q3 2026 earliest, Q1 2027 realistic.

---

## Alternative Evaluation: Avalonia (Future)

**Why Avalonia is on the radar:**

- ✅ Cross-platform (Windows, macOS, Linux) with single XAML codebase
- ✅ Mature MVVM support (ReactiveUI, Prism)
- ✅ Active development, strong community
- ✅ No Microsoft SDK dependency hell

**Why not now:**

- ⏸️ Syncfusion doesn't support Avalonia (Telerik has limited support)
- ⏸️ Less mature charting/reporting ecosystem vs. WinForms
- ⏸️ Migration effort not justified for Windows-only LOB app

**Re-evaluation trigger:** If cross-platform requirement emerges (e.g., macOS support for accountants).

---

## Lessons Learned

### What Went Wrong with WinUI 3

1. **Bleeding Edge ≠ Production Ready:** WinUI 3 is Microsoft's future vision, but unpackaged deployment is not production-hardened in 2025.
2. **Toolchain Complexity Kills Velocity:** 6 weeks lost to build system issues vs. 0 days with WinForms.
3. **Don't Fight the Platform:** WinUI 3 wants MSIX packaging. If you need unpackaged, use a framework that embraces it (WinForms, WPF).

### What Went Right with WinForms Pivot

1. **Ship > Perfect:** WinForms isn't "modern," but it ships and works.
2. **Mature Ecosystems Win:** Syncfusion WinForms has 20 years of production use. Trust it.
3. **Performance Matters:** 10× faster startup isn't a marginal gain—it's user-facing quality.

---

## References

- [Microsoft.UI.Xaml Issue #10027 (XamlCompiler Crash)](https://github.com/microsoft/microsoft-ui-xaml/issues/10027)
- [Windows App SDK Unpackaged Deployment](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps)
- [Syncfusion WinForms Documentation](https://help.syncfusion.com/windowsforms/overview)
- [Avalonia UI Documentation](https://docs.avaloniaui.net/)
- [.NET 9 Release Notes](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9)

---

## Conclusion

**We made the pragmatic call:**

- WinUI 3 unpackaged is not production-ready in 2025.
- WinForms + .NET 9 + Syncfusion is the fastest path to stable, shippable software.
- We will revisit modern UI frameworks (WinUI 3, Avalonia) in 2026–2027 when they mature.

**Bottom line:** We ship software, not toolchain drama.

**Tag:** `v1.0-winforms-relaunch`

**Approved by:** Development Team, November 25, 2025
