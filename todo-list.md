# Wiley Widget Development Tasks

## Phase 1: JARVIS Migration Cleanup ✅
- [x] Delete `AIChatControl` and `ChatPanel` (WinForms)
- [x] Redirect Ribbon navigation to `JARVISChatHostForm` (Blazor-based)
- [x] Verify `IAsyncEnumerable` streaming in `XAIService`
- [x] Verify `JARVISPersonalityService` integration

## Phase 2: Documentation Scrub ✅
- [x] Scrub legacy `AIChatControl` and `ChatPanel` references from `docs/integration/AI_Services_Integration_Verification.md`
- [x] Update `docs/reference/DI-AUDIT-2025-12-16.md` to remove deleted control audits
- [x] Update `docs/reference/INTEGRATION-TESTING.md`

## Phase 3: UX Stabilization ✅
- [x] Implement improved typing indicator in `JARVISAssist.razor`
- [x] Ensure `JARVISChatHostForm` maintains state across sessions
- [x] Add error handling to Blazor-to-C# bridge for network timeouts

## Phase 4: Roadmap & Documentation Update ✅
- [x] Add Future Roadmap to `docs/JARVIS_PERSONALITY_IMPLEMENTATION.md`
- [x] Document Database Persistence and Semantic Kernel integration
- [x] Propose Screen Reading features

## Phase 5: Build Fix (In Progress) 🛠️
- [ ] Investigate and fix `IAIService` build error in `NullAIService.cs`
- [ ] Verify if `IAIService` was accidentally deleted or should be replaced by `IXAIService`
- [x] Clamp `SplitterDistance` after layout in `AuditLogPanel`
- [x] Clamp `SplitterDistance` after layout in `WarRoomPanel`
- [x] Defer `BeginInvoke` usage in `BudgetPanel` until handle created
- [x] Register `ChartViewModel` in DI
- [x] Add MCP UI layout tests for SplitContainer panels

## Phase 6: Final Validation 🏁
- [ ] Run `WileyWidget: Build` task
- [ ] Run `test: viewmodels` task
- [ ] Verify no "ghost" references in Problems panel
