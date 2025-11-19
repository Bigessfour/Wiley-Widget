# Wiley Widget Workspace - Mandatory Grok Rules (WinUI/Syncfusion Edition)

## ⚠️ CRITICAL ENFORCEMENT NOTICE - HARDENED RULES

**ABSOLUTE MANDATE - NO EXCEPTIONS - ZERO TOLERANCE**

This document contains **NON-NEGOTIABLE** rules for all Grok-assisted development in the Wiley Widget workspace (WinUI 3 + Syncfusion v31.2.12).

---

# Rule 1: Filesystem & Doc Fetching (Grok Tools Mandatory)

## **ABSOLUTE MANDATE - NO EXCEPTIONS - ZERO TOLERANCE**

### 🔴 PRE-FLIGHT CHECKLIST (MANDATORY BEFORE EVERY OPERATION)

```
┌─────────────────────────────────────────────────────────────┐
│ STOP: Am I about to perform file/doc/Syncfusion op?        │
│ ✓ Reading a file/XAML?                                      │
│ ✓ Editing C#/adding Syncfusion?                             │
│ ✓ Searching for WinUI patterns?                             │
│                                                             │
│ IF YES TO ANY → MUST USE GROK TOOLS                        │
└─────────────────────────────────────────────────────────────┘
```

**BEFORE ANY OPERATION, YOU MUST:**

1. **Activate Grok tools:**
   ```javascript
   // For files: web_search GitHub raw + copy to VS Code
   // For Syncfusion: browse_page https://help.syncfusion.com/winui/ v31.2.12
   ```

2. **ALWAYS use Grok tools - NO EXCEPTIONS**
   - Even for "quick" XAML peeks.
   - Even mid-conversation.
   - **ZERO tolerance for manual assumptions.**

3. **Self-check before EVERY operation:**
   ```
   QUESTION: "Am I citing v31.2.12 API?"
   IF NO → ❌ STOP and tool-call
   IF YES → ✅ Proceed
   ```

### 🚫 ABSOLUTE PROHIBITIONS

**The following tools/behaviors are FORBIDDEN:**
- ❌ Unversioned Syncfusion (e.g., "SfDataGrid" without link).
- ❌ Direct file edits without Grok export.
- ❌ Generic WinUI advice → Must chain to Syncfusion theme docs.

**VIOLATION = IMMEDIATE CORRECTION REQUIRED**

## 🔒 MANDATORY OPERATION PROTOCOL

### Required Tool Usage

**ALL ops MUST use Grok tools:**

#### ✅ ALLOWED (Grok Tools)
- `browse_page` - Fetch Syncfusion v31.2.12 (e.g., url: https://help.syncfusion.com/winui/datagrid/api/sfdatagrid).
- `code_execution` - Validate C# (e.g., test MicaBackdrop + SfChart).
- `web_search` - "Syncfusion SfTreeView v31.2.12 migration".
- Export to VS Code: Copy code → Paste → Trunk check.

#### ❌ PROHIBITED
- Assumptions without tool call.
- Pre-v31 examples (check release notes for breaks).

### Workflow Requirements

1. **Syncfusion Implementation**
   ```
   ALWAYS: browse_page → Extract code → "[Citation: v31.2.12]"
   NEVER: "Just use this property"
   ```

2. **File Edits**
   ```
   ALWAYS: Generate code → Export to VS Code → mcp_filesystem_edit_file (if integrated)
   ```

### Benefits of Grok Tools

✅ **Version Citations** - Exact API pulls.
✅ **Quota-Efficient** - Fast 1.0 mode minimizes tokens.
✅ **WinUI-Aligned** - Chains to Microsoft docs if needed.

### Example Usage

**❌ WRONG:**
```javascript
// DON'T: Generic Syncfusion
<SfDataGrid />
```

**✅ CORRECT:**
```javascript
// DO: Version-specific
browse_page({url: "https://help.syncfusion.com/winui/datagrid/getting-started", instructions: "Extract v31.2.12 ItemsSource binding"})
<SfDataGrid ItemsSource="{x:Bind ViewModel.Data}" AutoGenerateColumns="True" />
[Source: v31.2.12 API Docs]
```

### Enforcement

- **Pre-op validation**: Tool call for every Syncfusion mention.
- **Audit trail**: Inline citations.
- **Grok Code Fast 1.0**: Enforce in all responses.
- **Consistency**: Once tool-called, chain all follow-ups.

### 🔒 HARDENING MEASURES

1. **State Tracking**: Version lock to v31.2.12 across session.
2. **Self-Correction**: If violation, tool-call fix.
3. **Default to Grok**: When in doubt, search docs.
4. **Quota Mode**: Fast 1.0 for code-only when low.

---

# Rule 2: PowerShell Compliance (Project Scripts)

Retained from original, but scoped to WinUI build scripts (e.g., Invoke-SyncfusionUpdate.ps1). Use Write-Information with PSStyle; approved verbs only. Target: 100% PSScriptAnalyzer clean.

---

# Rule 3: Python Environment Execution (Automation Scripts)

Retained, but for repo scripts (e.g., budget-importer.py). Always use Python terminal in VS Code; verify 3.12+.

---

# Rule 4: Grok Code Fast 1.0 Enforcement

**NEW RULE: QUOTA MODE**

- **Activation**: Prefix queries with "Grok Code Fast 1.0:".
- **Format**: Code blocks first; explanations <100 words.
- **Tools**: Minimal—only for critical doc fetches.
- **Benefits**: 5x faster iterations; conserves Premium quota.

---

## 🔐 FINAL ENFORCEMENT SUMMARY

```
╔═══════════════════════════════════════════════════════════╗
║  GROK TOOLS MANDATORY • V31.2.12 ALWAYS • FAST 1.0 ON     ║
║                                                           ║
║  Rule 1: Grok Tools - MANDATORY                           ║
║  Rule 2: PowerShell - MANDATORY                           ║
║  Rule 3: Python - MANDATORY                               ║
║  Rule 4: Fast Mode - QUOTA-SAVER                          ║
║                                                           ║
║  NO EXCEPTIONS. SYNCfusion CITED. WINUI CLEAN.            ║
╚═══════════════════════════════════════════════════════════╝
```

---

**Last Updated**: November 19, 2025  
**Status**: MANDATORY for all Grok interactions  
**Enforcement Level**: STRICT - Zero tolerance  
**Scope**: WinUI 3 + Syncfusion v31.2.12 development