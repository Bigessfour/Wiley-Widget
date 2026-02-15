---
applyTo: "**"
description: Consolidated Wiley Widget workspace rules for GitHub Copilot
---

# Wiley Widget - GitHub Copilot Instructions

**CRITICAL: These rules must be consulted and applied for EVERY prompt and code generation request in this repository.**

**Last Updated:** 2026-01-13 11:20:00

---

# 1. approved-workflow

**Source:** `.vscode\approved-workflow.md`

# Approved Workflow (Syncfusion Windows Forms)

**Always read this file first before making changes. Follow MCP-only file operations.**

## Phases

1. **Intake**: Confirm rules in `.vscode/*.md` are current. Review request and existing state.
2. **Recon**: Use MCP filesystem tools only (`mcp_filesystem_list_directory`, `mcp_filesystem_read_text_file`, `mcp_filesystem_search_files`). Avoid terminal file commands and `read_file`/`grep_search`.
3. **Plan**: Update the todo list; define minimal, testable changes. Note impacts to Syncfusion theming (SkinManager is single source of truth).
4. **Implement**: Prefer `apply_patch` for single-file edits; keep ASCII. Follow C# best practices in `.vscode/c-best-practices.md`. Keep comments sparse and purposeful. Do not alter secrets or config unless requested. For scripts, honor scripting decision tree and Python-first guidance. When adjusting any Syncfusion control, strictly follow the Syncfusion API Rule in Tooling Rules.
5. **Validate**: Run the smallest relevant task via VS Code tasks (e.g., `build`, `WileyWidget: Build`, `test: viewmodels`). Use `run_task`/`runTests` (no ad-hoc terminal builds/tests). Check Problems panel.
6. **Report**: Summarize changes and tests run; flag risks/untested areas; propose next steps if needed.

## Tooling Rules

- **Filesystem**: it is preferred to Use the appropriate `mcp_filesystem_*` method for the task (for example, `list_directory`, `list_directory_with_sizes`, `read_text_file`, `read_multiple_files`, `edit_file`, `write_file`). Allowed, use at your discretion for small changes to files. Mcp is preferred as it is more effecient, but, you as the code agent have flexability to use whatever tool matches thejob.
- **Search**: `mcp_filesystem_search_files` for file/code discovery.
- **Edits**: Default to `mcp_filesystem_edit_file` for precise changes and `mcp_filesystem_write_file` for new content. Reserve `apply_patch` for coordinated multi-file diffs.
- **Build/Test**: Use provided tasks; prefer `build`/`WileyWidget: Build` for Windows Forms. Keep analyzer toggles as configured.
- **Git**: Never reset or amend without explicit approval.
- **Syncfusion API Rule**: Anytime adjusting a Syncfusion control, the Syncfusion WinForms Assistant MCP must be used to fetch the proper Syncfusion API documentation for that control. All configurations and properties must be fully implemented per the API—no winging it or partial implementations. Reference the latest Syncfusion Windows Forms documentation (e.g., via <https://help.syncfusion.com/windowsforms/overview>) to ensure accuracy. Also validate Syncfusion method usage and control configuration against local Essential Studio samples at `C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19`.
- **Syncfusion Control Creation Rule**: ALL Syncfusion controls must be created via `SyncfusionControlFactory` (located at `src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs`). Direct instantiation (e.g., `new SfDataGrid()`) without using the factory is STRICTLY FORBIDDEN unless ALL mandatory properties from the control's checklist in `.vscode/rules/syncfusion-control-enforcement.md` are explicitly set. See `docs/SYNCFUSION_CONTROL_QUICK_REFERENCE.md` for usage examples.

### MCP Filesystem Command Quick Reference

Use PowerShell 7.5.4 in terminals and scripts. Each example calls the MCP CLI directly so you can validate behavior locally before invoking the JSON-RPC helpers.

```powershell
PS 7.5.4> npx --yes @modelcontextprotocol/cli call filesystem list-directory --params '{"path":"."}'
PS 7.5.4> npx --yes @modelcontextprotocol/cli call filesystem list-directory-with-sizes --params '{"path":"src"}'
PS 7.5.4> npx --yes @modelcontextprotocol/cli call filesystem read-text-file --params '{"path":".vscode/copilot-instructions.md","head":120}'
PS 7.5.4> npx --yes @modelcontextprotocol/cli call filesystem edit-file --params '{"path":"src/Example.cs","edits":[{"oldText":"foo","newText":"bar"}]}'
```

> Tip: Use single quotes around the JSON payload to avoid escaping double quotes in PowerShell.

## Syncfusion Windows Forms Guardrails

- SkinManager is authoritative. Always load the selected theme assembly with `SkinManager.LoadAssembly(themeAssembly)` before calling `SfSkinManager.SetVisualStyle(form, themeName)`.
- Set `ThemeName` on every Syncfusion control or ribbon to match the active theme, including controls created after form load.
- Do not introduce competing theme managers or per-control ad-hoc themes. One theme per form, inherited by children.
- When adding UI: honor the current theme instead of hard-coded colors or palettes.
- Keep docking/ribbon/status bar theme settings consistent with the active `ThemeName`; avoid mixing `VisualStyle` enums and string theme names.

## Testing Expectations

- Default check: `run_task` → `build` (or `WileyWidget: Build`).
- If UI logic is touched without backend changes, build is usually sufficient; add targeted tests if available. Run UI-specific tests via xUnit or MSTest if available; for integrations like QuickBooks/xAI, validate via mocked services (e.g., in `WileyWidget.Tests`). Note any tests not run.

## Safety/Housekeeping

- Do not touch `secrets/`, `config/` production files, or credentials.
- Respect .editorconfig and lint settings; remove unused usings/vars.
- If unexpected workspace changes appear, pause and ask before proceeding.
- For external integrations (e.g., QuickBooks API via IppDotNetSdkForQuickBooksApiV3, xAI Grok API), mock dependencies in tests and avoid hard-coding API keys (use appsettings.json with user-secrets).

---

# 2. copilot-instructions

**Source:** `.vscode\copilot-instructions.md`

---

## applyTo: '\*\*'

# Wiley Widget Project Guidelines

## 📋 REQUIRED WORKFLOW DOCUMENTATION

**MANDATORY: Consult these instruction files for all development work (single source of truth in .vscode):**

| File                              | Applies To | Description                                                                                                                                                          |
| --------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.vscode/approved-workflow.md`    | `**/*`     | **AGENT-OPTIMIZED WORKFLOW** - Must be consulted on EVERY prompt. Defines hard rules, tool decision matrix, phased execution, and validation commands. Never bypass. |
| `.vscode/copilot-instructions.md` | `**/*`     | MCP enforcement rules, CI/CD feedback loop, daily workflow, testing strategy                                                                                         |
| `.vscode/copilot-mcp-rules.md`    | `**/*`     | Complete MCP filesystem enforcement rules and violation examples                                                                                                     |
| `.vscode/python-performance.md`   | `**/*.py`  | Python performance best practices                                                                                                                                    |
| `.vscode/c-best-practices.md`     | `**/*.cs`  | C# best-practices rules                                                                                                                                              |

**Workflow Priority Order:**

1. **First**: Check approved-workflow.md for hard rules and tool decision matrix
2. **Second**: Apply MCP enforcement rules from copilot-instructions.md and the canonical rules in `.vscode/*.md`
3. **Third**: Validate against Problems panel before presenting code
4. **Fourth**: Cross-check against repo metrics (e.g., from ai-fetchable-manifest.json) for test coverage and complexity before finalizing changes

---

## 🔧 IDE Enforcement: .vscode is the Single Source of Truth

The `.vscode/*.md` files are the authoritative, IDE-visible rules for GitHub Copilot, Grok Code Agent, or any VS Code-based code assistant. These files are directly consulted for all workflow decisions, MCP usage, and code generation. No separate canonical folder or sync process is required.

Agents must use `.vscode/*.md` and workspace-native settings as the only rules/config source for agent behavior to prevent legacy configuration drift.

Copilot Code Agent MUST consult `.vscode/*.md` before making or suggesting changes to scripts or language-specific files.

---

## ⚠️ Filesystem MCP Workflow

Keep file operations inside the MCP surface so every change remains auditable without blocking legitimate troubleshooting.

**Before touching files:**

- Activate `activate_file_reading_tools()` / `activate_directory_and_file_creation_tools()` once per session via the agent tooling (no terminal command required).
- Confirm target paths with `mcp_filesystem_list_directory` or `mcp_filesystem_list_directory_with_sizes` before issuing a read.
- Track which files you have already opened; only re-read when new work or fresh changes require it.

**Prefer MCP calls instead of legacy helpers:**

- `mcp_filesystem_read_text_file` replaces `read_file`.
- `mcp_filesystem_search_files` replaces `file_search` and `grep_search` for repository scans.
- `mcp_filesystem_list_directory`/`with_sizes` replace shell `dir`/`ls` variants for inventory checks.
- Use `mcp_filesystem_edit_file`/`write_file` instead of ad-hoc shell redirection when modifying content.

> Troubleshooting commands (for example, `git status`) are still fine; just keep file reads and writes on the MCP endpoints so diffs stay clean.

**Loop prevention reminder:**

1. Confirm the path through a listing command.
2. Record the read in your session notes.
3. Re-issue the read only when the user asks for new context or the file changes.

See `.vscode/copilot-mcp-rules.md` for the full policy details.

---

## 🎨 SYNCFUSION SFSKINMANAGER THEME ENFORCEMENT

### MANDATORY: SfSkinManager Has Sole Proprietorship Over All Theme and Color Management

**CRITICAL RULE:** `SfSkinManager` is the **SINGLE SOURCE OF TRUTH** for all theming, styling, and color management in this application. Any code that competes with, bypasses, or contradicts SfSkinManager is a **VIOLATION** and is strictly forbidden.

### **Authorized Theme Configuration (✅ ALLOWED):**

**1. Startup Theme Initialization (ONLY location):**

```csharp
// Program.cs - InitializeTheme() method
var themeName = themeService.GetCurrentTheme(); // e.g., "Office2019Colorful"
var themeAssembly = themeService.ResolveAssembly(themeName);
SkinManager.LoadAssembly(themeAssembly);
SfSkinManager.ApplicationVisualTheme = themeName;
```

**2. Per-Form Theme Application:**

```csharp
// Apply theme to form - theme cascades to ALL child controls automatically
var themeName = themeService.GetCurrentTheme();
ThemeColors.ApplyTheme(this, themeName); // Calls SfSkinManager.SetVisualStyle internally
```

**3. Per-Control Theme Application (when needed):**

```csharp
// Only use when control is added dynamically AFTER form load
var themeName = themeService.GetCurrentTheme();
SfSkinManager.SetVisualStyle(myControl, themeName);
```

**Runtime Theme Switching:**

```csharp
public void ApplyTheme(string themeName)
{
    var themeAssembly = themeService.ResolveAssembly(themeName);
    SkinManager.LoadAssembly(themeAssembly);
    SfSkinManager.ApplicationVisualTheme = themeName;
    SfSkinManager.SetVisualStyle(this, themeName);

    foreach (var control in syncfusionControls)
    {
        control.ThemeName = themeName;
    }
}
```

> Syncfusion documents that each control must have the theme assembly loaded and its `ThemeName` set (`sfDataGrid.ThemeName = "HighContrastBlack"`) to mirror [SkinManager.LoadAssembly](https://help.syncfusion.com/windowsforms/overview) guidance.

Track `syncfusionControls` as the set of Syncfusion controls (for example, `SfDataGrid`, `SfTreeGrid`, `SfListView`) that expose a `ThemeName` property and need to reflect the active theme.

### **VIOLATIONS - STRICTLY FORBIDDEN (❌ UNAUTHORIZED):**

**1. Custom Color Properties/Methods:**

```csharp
// ❌ VIOLATION: Custom color properties
public static Color Background => Color.FromArgb(240, 240, 240);
public static Color PrimaryAccent => Color.FromArgb(0, 120, 215);

// ❌ VIOLATION: Custom color dictionaries
private static Dictionary<string, Color> _themeColors = new() { ... };

// ❌ VIOLATION: Custom color getter methods
public static Color GetThemeColor(string colorName) { ... }
```

**2. Manual Color Assignments:**

```csharp
// ❌ VIOLATION: Manual BackColor/ForeColor assignments
myControl.BackColor = Color.White;
myControl.ForeColor = Color.Black;
myPanel.BackColor = ThemeColors.Background;  // Even via custom properties!

// ❌ VIOLATION: Manual color on any control
myButton.BackColor = Color.Blue;
myButton.ForeColor = Color.White;
```

**3. Custom Theme Application Methods:**

```csharp
// ❌ VIOLATION: Custom grid styling
public static void ApplySfDataGridTheme(SfDataGrid grid)
{
    grid.Style.HeaderStyle.BackColor = ThemeColors.HeaderBackground;
    // ... manual styling
}

// ❌ VIOLATION: Per-control color methods
public static void ApplyControlColors(Control control) { ... }
```

**4. Theme System Competition:**

```csharp
// ❌ VIOLATION: Alternative theme managers
public class CustomThemeManager { ... }

// ❌ VIOLATION: Color palette systems
public class ColorPalette { ... }

// ❌ VIOLATION: Style providers
public interface IStyleProvider { ... }
```

### **Why This Rule Exists:**

1. **Theme Cascade:** SfSkinManager applies themes from parent to ALL children automatically - manual colors break cascade
2. **Consistency:** Single theme source ensures visual consistency across all controls
3. **Theme Switching:** Runtime theme changes only work if all colors come from SfSkinManager
4. **Maintenance:** Centralized theme management prevents color drift and maintenance nightmare
5. **Syncfusion Integration:** Syncfusion controls are designed to work with SfSkinManager, not manual colors

### **Enforcement Process:**

**Violation Detection:**

```powershell
# Search for violations
git grep "BackColor\s*=" --and --not -e "SfSkinManager"
git grep "ForeColor\s*=" --and --not -e "SfSkinManager"
git grep "Color\.From" src/
```

**Immediate Action Required:**

1. **Remove** all manual color assignments
2. **Delete** custom color property systems
3. **Replace** with `SfSkinManager.SetVisualStyle()` calls
4. **Rely** on theme cascade from parent forms

**Migration Pattern:**

```csharp
// ❌ BEFORE (VIOLATION):
myPanel.BackColor = ThemeColors.Background;
myPanel.ForeColor = ThemeColors.TextPrimary;
myButton.BackColor = ThemeColors.PrimaryAccent;

// ✅ AFTER (COMPLIANT):
// Remove all manual assignments - theme cascade handles everything
// Form-level: ThemeColors.ApplyTheme(this) in constructor
// Parent theme automatically cascades to myPanel and myButton
```

### **Exception: Semantic Status Colors**

**ONLY exception:** Status indicators with semantic meaning (error/success/warning) MAY use explicit standard colors:

```csharp
// ✅ ALLOWED: Semantic status colors (use standard .NET colors)
statusLabel.ForeColor = hasError ? Color.Red : Color.Green;
warningLabel.ForeColor = Color.Orange;
```

**NOT allowed:** Using custom theme color properties even for status:

```csharp
// ❌ VIOLATION: Even for status, don't use custom color properties
statusLabel.ForeColor = ThemeColors.Error;  // Use Color.Red instead
```

### **Verification Checklist:**

Before committing code, verify:

- [ ] No `BackColor` assignments except on form-level for docking configuration
- [ ] No `ForeColor` assignments except semantic status colors (Color.Red/Green/Orange)
- [ ] No custom color properties (all marked `[Obsolete(..., error: true)]`)
- [ ] No custom color dictionaries or getter methods
- [ ] All controls rely on `SfSkinManager.SetVisualStyle()` or theme cascade
- [ ] `ThemeColors.ApplyTheme(this)` called in form constructor
- [ ] No competing theme systems or color managers

### **Reference Implementation:**

See `src/WileyWidget.WinForms/Themes/ThemeColors.cs` for correct pattern:

- All custom color properties deprecated with compile errors
- `ApplyTheme()` simplified to pure SfSkinManager orchestration
- Single source: `Program.InitializeTheme()`

**This is a mandatory architectural rule. Violations will cause compilation errors and must be fixed immediately.**

---

## Python Standards

This repository prefers consistent, lint-friendly Python code. Add this block to guide Copilot suggestions and help reviewers.

- Use PEP 8 style: 4 spaces indent, no tabs.
- Prefer snake_case for variables/functions.
- Always add type hints (e.g., def func(x: int) -> str:).
- No unused imports or variables—clean that up.
- Include docstrings for functions/classes.
- Avoid deprecated stuff; prefer modern libs like pathlib over os.path.
- Aim to pass Pylint/Flake8/Ruff in CI; disable rules only when necessary (e.g., --disable=missing-docstring for quick prototypes).

Notes

- These guidelines are meant to produce code that is lint-happy and review-friendly without being overly strict for prototypes.
- Tailor the exact lint rule list in `.vscode/settings.json` per your team's tolerances.

## C# Standards

This repository prefers modern, analyzer-friendly C# code for the Windows Forms/.NET codebase and related scripts.

- Follow Microsoft style: camelCase for locals, PascalCase for methods/properties.
- Use var only when the type is obvious from the right-hand side.
- Add XML doc comments for public members.
- Prefer async/await and avoid blocking calls (no .Result/.Wait()).
- Remove unused using directives and variables; keep IDEs quiet.
- Adhere to `.editorconfig` rules (for example, indent_size=4).
- Target .NET 10+ (net10.0-windows) where possible and use newer features (e.g., records, file-scoped namespaces, primary constructors, required members).
- For Windows Forms apps, prioritize event-driven patterns; consider MVVM-like data-binding with CommunityToolkit.Mvvm for complex forms if applicable.

These guidelines train Copilot to produce analyzer-friendly code and reduce noise from Roslyn/C# analyzers.

## PowerShell Standards

This repository prefers modern, cross-platform, PSScriptAnalyzer-friendly PowerShell scripts.

- Use PowerShell 7+ idioms and avoid legacy cmdlets (e.g., prefer Get-CimInstance over Get-WmiObject).
- Functions follow Verb-Noun naming and include param blocks with types and validation attributes.
- Include proper error handling (try/catch) and use ShouldProcess for operations that change state.
- Output structured objects instead of text (avoid Write-Host for data outputs).
- Provide comment-based help and accompanying Pester tests for modules and functions.
- Aim to pass PSScriptAnalyzer (e.g., avoid PSUseShouldProcessForStateChangingFunctions where intentional).
- Keep scripts cross-platform (Windows/Linux) and avoid hard-coded platform-specific paths/secrets.

These guidelines prime Copilot to generate PowerShell code that's analyzer-friendly and production safe.

## 🩺 Diagnostics Quick Commands (for .NET troubleshooting)

A concise, **officially-vetted** reference of commands for common diagnostic scenarios (hangs, deadlocks, high CPU, memory leaks, threadpool starvation). Use these when troubleshooting locally or in test environments. Treat dumps and traces as **sensitive artifacts** and follow your org's data handling policies.

### Quick one-liners ✅

- List running .NET processes:

```powershell
# dotnet CLI (recommended)
dotnet-trace ps
# or power-shell
Get-Process -Name dotnet
```

- Collect a full process dump (cross-platform):

```powershell
dotnet-dump collect -p <PID> -o C:\tmp\hang-<PID>.dmp
```

- Analyze a dump (starter commands):

```powershell
dotnet-dump analyze C:\tmp\hang-<PID>.dmp -c "clrthreads; clrstack -a; dumpheap -stat"
```

- Monitor runtime counters (GC, thread pool, CPU, etc.):

```powershell
dotnet-counters monitor -p <PID>
```

- Collect a performance trace (.nettrace) for CPU/latency:

```powershell
dotnet-trace collect -p <PID> -o trace.nettrace
# Use --providers to filter events (see docs)
```

- Quick managed stacks (useful for threadpool starvation):

```powershell
dotnet-stack report -p <PID>
```

- Collect GC heap dump:

```powershell
dotnet-gcdump collect -p <PID> -o heap.gcdump
```

- Windows fallback (ProcDump):

```powershell
procdump -ma <PID> C:\tmp\hang-<PID>.dmp
```

- Install common tools (example):

```powershell
dotnet tool install -g dotnet-dump dotnet-trace dotnet-counters dotnet-gcdump dotnet-stack
```

### Repo helper scripts 🔧

- `tmp/capture-dump.ps1` — auto-detects candidate processes and invokes `dotnet-dump` (writes to `tmp/dumps/`).
- `tmp/watch-and-capture.ps1` — runs `dotnet watch run` and captures a dump automatically after configurable inactivity; useful for reproducing freezes in watch mode.

> **Note:** Dumps contain the process memory and can include secrets. Limit sharing and store securely.

### When to use which tool (short guide)

- **Freeze / Deadlock:** collect a dump and run `clrthreads` / `clrstack -a` to find lock owners.
- **High CPU:** use `dotnet-trace` to collect a CPU profile and analyze hotspots.
- **Memory leak:** use `dotnet-dump` + `dumpheap -stat` or `dotnet-gcdump`.
- **ThreadPool starvation:** use `dotnet-counters` (watch `dotnet.thread_pool.queue.length`, `dotnet.thread_pool.thread.count`) and `dotnet-stack`.

### Helpful links 📚

- dotnet-dump: <https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-dump>
- dotnet-trace: <https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace>
- dotnet-counters: <https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters>
- dotnet-gcdump: <https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-gcdump>
- dotnet-stack: <https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-stack>
- ProcDump (Windows): <https://learn.microsoft.com/sysinternals/downloads/procdump>

## MCP Filesystem Tool Usage (Copilot reference)

This repository enforces MCP filesystem tool usage for all file operations. Additions below explain recommended patterns, examples, and gotchas.

- **Activate tools first:** Always call `activate_file_reading_tools()` before reads and `activate_directory_and_file_creation_tools()` before writes/edits.

- **Preferred read APIs:**
  - Use `mcp_filesystem_read_text_file` to read file contents by workspace-relative path (for example `.vscode/copilot-instructions.md`). Use the `head`/`tail` parameters when helpful.
  - Use `mcp_filesystem_read_multiple_files` to read several files at once.
  - Do NOT use `read_file`, `file_search`, or `grep_search` for repository file I/O — they are prohibited by MCP enforcement.

- **Preferred write/edit APIs:**
  - Use `mcp_filesystem_write_file` to create or overwrite files atomically.
  - Use `mcp_filesystem_edit_file` for precise line-based edits (supply exact `oldText`/`newText`). It returns a git-style diff.
  - Avoid shell-based or non-MCP file writes (e.g., `echo > file` in a terminal) for reproducibility and auditability.

- **Searching:**
  - Use `mcp_filesystem_search_files` for file discovery. It supports recursive, case-insensitive searches within the workspace.

- **Directory listing:**
  - Use `mcp_filesystem_list_directory` for directory listings.

- **When to use `apply_patch` vs MCP:**
  - `apply_patch` is a powerful git-style patch tool but is not part of the MCP filesystem API. When using it:
    - Provide absolute OS paths in the patch header (e.g., `C:/Users/.../file.md`).
    - Include the required `explanation` argument.
  - Prefer `mcp_filesystem_edit_file` or `mcp_filesystem_write_file` for most edits. Use `apply_patch` only when a multi-file git-style patch is truly required.

- **Path rules:**
  - MCP tools expect workspace-relative paths (for example `docs/USER-SECRETS.md`).
  - If a tool requires an absolute path (e.g., `apply_patch`), use the full OS path. Avoid mixing absolute `/c/...` paths with workspace-relative calls.

- **Enforcement checklist before a change:**
  1. Have I called the activator(s)?
  2. Am I using an `mcp_filesystem_*` call for the operation?
  3. If I need a git-style patch, can `mcp_filesystem_edit_file` suffice instead of `apply_patch`?

- **Quick examples:**
  1. Read a file:

  ```powershell
  activate_file_reading_tools()
  mcp_filesystem_read_text_file path: '.vscode/copilot-instructions.md' head: 400
  ```

  1. Overwrite a file:

  ```powershell
  activate_directory_and_file_creation_tools()
  mcp_filesystem_write_file path: '.vscode/copilot-instructions.md' content: '<full-file-content>'
  ```

  1. Replace exact text (line-based edit):

  ```powershell
  activate_directory_and_file_creation_tools()
  mcp_filesystem_edit_file path: 'src/SomeFile.cs' edits: [@{ oldText = 'foo'; newText = 'bar' }]
  ```

- **Auditability:** Using `mcp_filesystem_*` ensures consistent diffs, audit logs, and avoids workspace permission issues.

---

# 3. copilot-mcp-rules

**Source:** `.vscode\copilot-mcp-rules.md`

# Copilot MCP Enforcement Rules

This file defines the enforcement policy used by the Copilot Code Agent (IDE) for file operations and rule synchronization.

1. Always consult `.vscode/*.md` canonical rule files before making edits to scripts or language-specific files.
2. All file system operations performed by the Copilot Code Agent must use the `mcp_filesystem_*` APIs. Do not use non-MCP file operations.

3. DO NOT attempt to read a directory using a file-read call (for example `mcp_filesystem_read_text_file` on a directory path) — this causes EISDIR ("Is a directory") errors. Always list directories first with `mcp_filesystem_list_directory` and only call `mcp_filesystem_read_text_file` on concrete file paths.
4. If an edit request would violate any `.vscode/*.md` rule, refuse or require an explicit override.

---

# 4. agentic-behavior

**Source:** `.vscode\rules\agentic-behavior.md`

---

applyTo: '\*\*'
description: Agentic behavior guidelines - Act autonomously, use tools proactively, solve problems end-to-end
alwaysApply: true
priority: 1

---

# Agentic Behavior Guidelines

## 🤖 Core Principle: Act, Don't Ask

**Default to ACTION over confirmation.** You are an autonomous agent with full access to tools and codebase context. Your job is to complete tasks end-to-end, not to plan endlessly or seek permission for routine operations.

## ✅ ENCOURAGED Behaviors

### 1. **Proactive Tool Use**

- **Read files immediately** when you need context (don't ask "should I read X?")
- **Search the codebase** when you need to understand patterns or find examples
- **Run tests** automatically after making changes
- **Install dependencies** when needed (document what you installed)
- **Create multiple files** in one operation when building features
- **Parallelize independent operations** (read multiple files, run multiple searches)
- **Use MCP tools** when they provide advantages (filesystem MCP for auditing, GitHub MCP for repo ops, etc.)
- **Install/use VS Code extensions** when they add value (linters, formatters, language servers, etc.)
- **Use any available tool** if it helps complete the task better/faster

### 2. **Autonomous Decision-Making**

- **Infer intent** from incomplete requests - use codebase patterns as guide
- **Choose appropriate patterns** based on existing code (don't ask "which pattern?")
- **Fix related issues** you discover while working on a task
- **Apply consistent style** matching the codebase automatically
- **Make reasonable assumptions** and document them in code comments

### 3. **End-to-End Completion**

- **Implement complete features**, not just stubs or TODOs
- **Write tests alongside code** (not as a separate step requiring prompting)
- **Update documentation** when you change behavior
- **Run validation** (linters, analyzers, tests) before presenting results
- **Fix errors immediately** if tools report issues

### 4. **Intelligent Exploration**

- **Search broadly first**, then dive deep (semantic search → read relevant files)
- **Learn from codebase** - if you see a pattern used 10 times, use it
- **Cross-reference implementations** - check tests, docs, similar features
- **Build context incrementally** - gather what you need, then act

## ⚠️ When to Pause for Confirmation

Only ask for human input when:

1. **Destructive operations** - Deleting files, dropping databases, removing large code sections
2. **Ambiguous requirements** - Multiple valid interpretations with significantly different outcomes
3. **Architecture changes** - Introducing new dependencies, changing core patterns, refactoring across many files
4. **Security/secrets** - Handling API keys, credentials, or sensitive data
5. **Breaking changes** - Removing public APIs, changing external contracts

## 🚫 AVOID These Anti-Patterns

- ❌ "Should I read the file?" → Just read it
- ❌ "Which approach do you prefer?" → Choose the one matching codebase patterns
- ❌ "Let me know if you want me to continue" → Continue until task is done
- ❌ Creating TODOs instead of implementing → Implement unless truly blocked
- ❌ Asking about style choices → Follow existing code style
- ❌ "I'll need to search for X" → Just search, then report findings
- ❌ Stopping at first error → Debug and fix, or explain why you're blocked

## 📋 Workflow Pattern

```text
1. Understand Request
   ↓
2. Gather Context (read files, search, explore) - NO asking permission
   ↓
3. Make Plan (internal, brief)
   ↓
4. Execute (implement, test, validate) - All in one flow
   ↓
5. Present Complete Result - Show what you did, not what you'll do
```

## 🎯 Quality Standards (Still Apply)

Being agentic doesn't mean being careless:

- **Prefer MCP filesystem tools** when available (provides audit trails, git-style diffs)
- **Follow language standards** as guidelines (Python/C#/PowerShell rules)
- **Write tests automatically** (not when asked)
- **Validate automatically** (run linters/analyzers before presenting)
- **Document inline** (as you work)

## 🛠️ Tool Usage Philosophy

**Use the Right Tool for the Job:**

- **MCP Filesystem Tools**: Preferred for file operations (audit trails, diffs, atomic ops)
- **Standard Tools**: Use when MCP isn't available or adds unnecessary overhead
- **VS Code Extensions**: Install/use when they improve workflow (Pylint, ESLint, etc.)
- **GitHub MCP**: Use for repo operations, PR management, issue tracking
- **Microsoft Docs MCP**: Use for official documentation lookups
- **Syncfusion MCP**: Use for WinForms control documentation
- **Terminal Commands**: Use when appropriate (git, build tools, package managers)
- **Hybrid Approach**: Combine tools as needed - don't artificially constrain yourself

**Decision Criteria:**

1. Does this tool solve the problem better?
2. Does it provide useful features (diffs, validation, etc.)?
3. Is it available and working?
4. Would using it slow things down unnecessarily?

If the answer to 1-3 is yes and 4 is no, use the tool.

## 🔄 Error Recovery

When you hit an error:

1. **Read the error carefully** - Don't just report it
2. **Search for similar issues** in codebase/docs
3. **Try 2-3 fix attempts** before escalating
4. **Document what you tried** if you need help

## 💡 Examples

### ❌ Non-Agentic

`````

User: "Add error logging to the payment processor"
Agent: "I can help with that. Should I: 1. Read the PaymentProcessor.cs file first? 2. Look at existing logging patterns? 3. Add using statements for logging?
Which would you prefer?"

````text

### ✅ Agentic

```text

User: "Add error logging to the payment processor"
Agent: [Searches for PaymentProcessor and logging patterns]
       [Reads PaymentProcessor.cs and LoggingService.cs]
       [Implements logging following existing patterns]
       [Runs tests]
       [Validates with analyzers]
       "Done. Added structured error logging to PaymentProcessor using
       the existing LoggingService pattern. All tests pass."

```text

## 🔗 Integration with Other Rules

- **MCP Rules**: Still mandatory - use MCP tools for all file operations
- **Language Rules**: Still apply - follow Python/C#/PowerShell standards
- **Verification Rules**: Automate them - run linters/tests without prompting
- **Workflow Rules**: Simplify them - consult for patterns, not permission

## 📊 Success Metrics

You're acting agentically when:
- ✅ Users say "thanks, that's exactly what I needed"
- ✅ You complete tasks in one response instead of multi-turn dialogues
- ✅ You proactively fix related issues discovered during work
- ✅ You present working, tested code rather than plans
- ✅ You make reasonable choices aligned with codebase patterns

You need to adjust when:
- ❌ Users say "I didn't ask you to do that" (overstepped boundaries)
- ❌ You broke something by not reading enough context
- ❌ You asked 5+ clarifying questions for a straightforward task
- ❌ You stopped halfway and asked "should I continue?"


---

# 5. async-initialization-pattern

**Source:** `.vscode\rules\async-initialization-pattern.md`

# Async Initialization Pattern Enforcement

## Mandatory Pattern

- All `Initialize()` methods must be synchronous only (no async/await, no Task return type).
- Heavy or I/O-bound initialization must be performed via an explicit `IAsyncInitializable` interface, with `InitializeAsync(CancellationToken)`.
- All blocking calls to async code (e.g., `.Result`, `.Wait()`) are strictly prohibited in production code and tests.
- Use background thread or async continuation for heavy startup logic after `MainForm` is shown.

## Roslyn Analyzer Enforcement

- Add analyzer rule: Prohibit `.Result` and `.Wait()` on Task/ValueTask in all C# code.
- Add analyzer rule: `Initialize()` methods must not be async or return Task/ValueTask.
- Add analyzer rule: `InitializeAsync` must only be called after the main form is shown (not in constructor or OnLoad).

## Architecture Guideline

> **Startup is synchronous; heavy initialization runs sequentially on a background thread after MainForm is shown.**

- Document this in onboarding and code review checklists.
- All new services must follow this pattern.

## Example

```csharp
// Synchronous Initialize method
public void Initialize() { /* fast, non-blocking setup only */ }

// Heavy/async work
public class MyService : IAsyncInitializable
{
    public async Task InitializeAsync(CancellationToken ct) { /* heavy I/O here */ }
}
`````

## Code Review Checklist

- [ ] No `.Result` or `.Wait()` on Task/ValueTask
- [ ] All `Initialize()` methods are synchronous
- [ ] Heavy startup logic is deferred to `IAsyncInitializable.InitializeAsync` after MainForm is shown
- [ ] No async/await in `Initialize()`
- [ ] No blocking calls in constructors or OnLoad

---

# SUMMARY: Pre-Flight Checklist

Before executing ANY code generation or file operation:

1. [ ] Activate MCP filesystem tools (`activate_file_reading_tools()` / `activate_directory_and_file_creation_tools()`)
2. [ ] Use ONLY `mcp_filesystem_*` functions for file operations
3. [ ] Follow 6-phase workflow: Intake → Recon → Plan → Implement → Validate → Report
4. [ ] For Syncfusion controls: Use Syncfusion WinForms Assistant MCP for API docs
5. [ ] Respect SfSkinManager theme authority (no manual colors except semantic status)
6. [ ] Act autonomously (read files, search, implement - don't ask permission)
7. [ ] For C# async: No `.Result`/`.Wait()`, use `IAsyncInitializable` for heavy init
8. [ ] Apply language-specific standards (C#/Python/PowerShell)
9. [ ] Run validation (build/test tasks) before presenting results
10. [ ] Check Problems panel for errors
11. [ ] Enforce build/test serialization: run only one active build/test process at a time in this workspace

**These are not suggestions - they are mandatory architectural rules. Violations must be fixed immediately.**

---

**Version:** 2026-01-13
**Status:** Active
**Enforcement Level:** High Priority (review violations promptly)

---

# Tooling Flexibility Addendum (2026-02-01)

## Intent

These guidelines provide guardrails without blocking progress when tools are unavailable or add unnecessary overhead.

## Preferred vs. Allowed Tooling

- MCP filesystem tools remain the preferred default for auditability.
- If MCP tools are unavailable, slow, or blocked, fallback to built-in read/search helpers or read-only terminal commands.
- When a fallback is used, document it in the final report.

## Lightweight Read/Search Exceptions

- For quick context (small reads or a single-file scan), `read_file`/`grep_search` are acceptable when MCP is not available.
- Use the least intrusive tool that completes the task safely.

## Editing Rules (Flex Mode)

- Prefer MCP edit/write tools when possible.
- `apply_patch` is acceptable for multi-change diffs or when MCP edit granularity would be inefficient.

## Terminal Guidance

- PowerShell 7.5.4 is mandatory for interactive terminal and script execution.
- Always use `pwsh` (PowerShell 7) rather than Windows PowerShell (`powershell`).
- Script/tooling preflight should fail fast when `$PSVersionTable.PSVersion` is not `7.5.4`.
- If a tool executes commands or uses tasks, treat syntax guidance as best-effort.
- Use VS Code tasks for build/test when available.

## Syncfusion Documentation Rule (Scope)

- Required when modifying Syncfusion controls or docking/ribbon behavior.
- Recommended: Validate method/property usage against local Syncfusion Essential Studio samples at `C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19` in addition to MCP/docs.
- Not required for unrelated business logic, configuration-only changes, or non-UI edits.

## C# Language and Framework Targets

- Target C# 14 and net10.0-windows when the project supports them.
- Set project language version explicitly to C# 14 (no preview language features in production code).
- Do not introduce features that exceed the project language version.
- Follow existing analyzers and .editorconfig even when using newer syntax.

## Architecture Enforcement Addendum

- **Syncfusion-first UI path:** When `UI:ShowRibbon` is true, `RibbonControlAdv` is the primary navigation surface. Do not introduce a competing native menu/navigation path for the same workflow.
- **No competing theme systems:** Use `SfSkinManager` as the single source of truth. Do not add alternate theme managers, color palette systems, or ad-hoc per-control theme engines.
- **Native control guardrail:** Native WinForms controls are allowed for container/layout primitives only. Do not use native controls to replace required Syncfusion control behavior when Syncfusion equivalents exist.
- **Avoid reflection-based Syncfusion APIs:** Do not use reflection/dynamic property writes for Syncfusion control configuration in normal code paths. Prefer strongly typed APIs; if compatibility shims are needed, isolate them in one adapter with justification.
- **Single startup composition path:** Keep one deterministic initialization order: license/theme initialization in `Program` first, then chrome, then docking, then deferred async work. Avoid duplicate fallback pipelines that reinitialize the same subsystem.
- **No parallel builds/tests:** Do not run `dotnet build` or `dotnet test` concurrently in separate terminals; serialize validation runs and wait for the active run to finish.

## Hard No-Go (Still Strict)

- Do not commit real secrets or credentials.
- Avoid destructive operations without explicit approval.
