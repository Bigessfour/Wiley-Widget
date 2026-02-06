# Syncfusion WinForms Skill Agent

This extension exposes a chat participant named `@syncfusion` that applies the workspace skill file located at `.vscode/skills/syncfusion-skill.md`.

## Prerequisites
- VS Code 1.86+ with Copilot Chat enabled.
- This workspace open so the skill file is available.

## Build
```powershell
Set-Location tools/SyncfusionSkillExtension
npm install
npm run compile
```

## Run (Extension Development Host)
1. Open this folder in a new VS Code window: `tools/SyncfusionSkillExtension`.
2. Press F5 to start the Extension Development Host.
3. In the new window, open Chat and use `@syncfusion`.

## Notes
- If the skill file is missing, the agent responds with a helpful error message.
- The participant uses the currently selected chat model in VS Code.
