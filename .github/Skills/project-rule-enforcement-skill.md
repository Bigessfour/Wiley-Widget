---
name: project-rule-enforcement
description: Always apply this skill when suggesting or making changes in the Wiley-Widget repo.
---
You must follow these canonical rules (single source of truth in .vscode/*.md):
- Filesystem: ONLY mcp_filesystem_* tools. Never read_file, terminal I/O, or list_directory_with_sizes.
- Workflow: Intake → Recon → Plan → Implement (apply_patch) → Validate (build task) → Report.
- Syncfusion: Direct API only, reference https://help.syncfusion.com/windowsforms/overview.
- C#: Modern practices (records, init, no unused usings) per c-best-practices.md.
- Never touch secrets or production config.

Before any edit: Check if .vscode rules are synced.