**WileyWidget — Migration Specification: BlazorWebView → Native SfAIAssistView**
**Document ID**: `docs/MigrateToSfAIAssistView.md`
**Version**: 1.0
**Target Syncfusion Version**: 33.1.44 (2026 Volume 1)
**Current Branch**: `main` (commit `c3bb154b0b`)
**Author**: Grok (Syncfusion WinForms v32.2.3 → v33.1.44 Expert)
**Date**: 2026-03-18
**Status**: Ready for Copilot + manual review

---

### 1. Purpose & Business Justification

Replace the current BlazorWebView + `jarvis.html` / `app.js` workaround in `JARVISChatUserControl.cs` with the **native SfAIAssistView** control introduced in Syncfusion WinForms v33.1.44.

**Benefits** (aligned with Microsoft Windows Apps Best Practices):

- Full `SfSkinManager` theming (no manual colors, no CSS conflicts)
- Native `DockingManager` + layout persistence support (`MainForm.LayoutPersistence.cs`)
- Zero WebView2/Blazor overhead (faster startup, lower memory)
- Built-in suggestion chips, typing indicator, message bubbles, and prompt history
- Keeps 100% of existing backend (`JarvisGrokBridgeHandler.cs`, `GrokAgentService`, `KernelPluginRegistrar.cs`, `GrokApiKeyProvider.cs`)
- Removes ~15 files/folder from `wwwroot/` (cleanup win)

**Risk**: Very low — only one control swap in one panel.

---

### 2. Official References (Documentation-First)

- Syncfusion WinForms v33.1.44 Release Notes: https://help.syncfusion.com/common/essential-studio/release-notes/v33.1.44
- What’s New in WinForms 2026 Volume 1: https://www.syncfusion.com/products/whatsnew/winforms
- **Local Sample / Installed Example Root** (use this installed path during the upgrade work):
  - `C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\33.1.44`
  - Inspect the local demo/example tree under that root for `SfAIAssistView` usage and event wiring before replacing `JARVISChatUserControl.cs`.
- SyncfusionControlFactory pattern (your existing rule)
- Microsoft Best Practices: https://learn.microsoft.com/en-us/windows/apps/get-started/best-practices (async UI, DI, responsive docking)

---

### 3. Scope & Out of Scope

**In Scope**:

- Replace control in `JARVISChatUserControl.cs`
- Wire `PromptRequested` / `ResponseReceived` events to your existing Grok bridge
- Preserve docking, floating, layout save/restore
- Update `MainForm.QuickAccessToolbar.cs` (optional dedicated JARVIS button)
- Clean up BlazorWebView + wwwroot files
- Update `AI-BRIEF.md` reading order

**Out of Scope**:

- Changing any ViewModel or service logic
- QuickBooks Desktop Import work
- Any other panels

---

### 4. Acceptance Criteria (Must Pass Before Merge)

1. Control renders with full `SfSkinManager` theme (no Blazor styling leaks)
2. Prompt → Grok → Response flow works exactly as today
3. Typing indicator + suggestion chips visible
4. Panel docks/floats/saves layout correctly (`MainForm.LayoutPersistence.cs`)
5. `WinFormsDiValidator` + `SafeControlSizeValidator` pass
6. No WebView2 dependency left in project
7. Startup time improved (measurable via `StartupOrchestrator.cs`)

---

### 5. Copilot-Ready Implementation Plan (Copy-Paste Prompts)

**Step 1 – NuGet Upgrade (5 min)**

```plaintext
// Copilot prompt:
Update WileyWidget.WinForms.csproj to use Syncfusion.SfAIAssistView.WinForms 33.1.44 (and all other Syncfusion packages to 33.1.44). Keep SfSkinManager as the sole theme authority.
Run WinFormsDiValidator after update.
```

Implementation note for this repository:

- Native WinForms package ID confirmed from NuGet: `Syncfusion.SfAIAssistView.WinForms` version `33.1.44`.
- Use the installed Syncfusion example root at `C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\33.1.44` as the local reference path during implementation.

**Step 2 – Create Migration Spec (already done — you are here)**

**Step 3 – Replace the Control (Copilot prompt — 10 min)**

```plaintext
In JARVISChatUserControl.cs:
- Remove all BlazorWebView, WebView2, and HTML/JS references.
- Use SyncfusionControlFactory.CreateAIAssistView() to instantiate SfAIAssistView.
- Wire:
  - PromptRequested event → call _grokBridge.SendPromptAsync
  - ResponseReceived → update conversation history
- Inherit ScopedPanelBase + ICompletablePanel exactly as before.
- Apply SfSkinManager.ApplyStyles(this) in constructor (no BackColor).
Reference the local assistview sample for event wiring.
```

**Step 4 – Event Wiring to Grok Bridge (Copilot prompt — 15 min)**

```plaintext
Keep JarvisGrokBridgeHandler.cs unchanged.
In JARVISChatUserControl:
private async void OnPromptRequested(object sender, AIAssistViewPromptRequestedEventArgs e)
{
    var response = await _grokBridge.SendPromptAsync(e.Prompt);
    aiAssistView.AddResponse(response); // or equivalent API from sample
}
```

**Step 5 – Cleanup (Copilot prompt — 5 min)**

```plaintext
Remove from WileyWidget.WinForms.csproj:
- wwwroot/jarvis.html
- wwwroot/app.js
- All BlazorWebView NuGet references
Delete the entire wwwroot folder if no longer used.
Update MainForm.QuickAccessToolbar.cs if you want a dedicated JARVIS ribbon button using existing FlatIcons.
```

**Step 6 – Update Documentation (Copilot prompt)**

```plaintext
Update AI-BRIEF.md:
- Add SfAIAssistView to Controls section
- Update Recommended Reading Order to include new JARVISChatUserControl.cs
- Add "JARVIS now uses native SfAIAssistView (v33.1.44)" to Architecture Patterns
```

---

### 6. Test Plan (Run After Each Step)

1. Build & run → JARVIS panel opens docked
2. Float the panel → close/reopen → layout restores
3. Send prompt → Grok responds with typing indicator
4. Switch themes → everything updates via SfSkinManager
5. Run full `WinFormsDiValidator`

---

### 7. Rollback Plan

- Revert NuGet to 32.2.3
- Restore BlazorWebView code from Git (previous commit)

---

**Next Action for You (or Copilot)**

1. Save this entire document as `docs/MigrateToSfAIAssistView.md`
2. Tell Copilot: “Follow MigrateToSfAIAssistView.md step by step”
3. Reply here with “Start Step 1” when you want me to generate the exact code diff for any step.

This spec guarantees zero workarounds and full compliance with your existing architecture (`ScopedPanelBase`, `SyncfusionControlFactory`, `SfSkinManager`, `DockingManager`).

You now have everything Copilot needs to complete the migration in **one focused session**. Ready?
