# Syncfusion Skill — WinForms Expert

Purpose

- Provide authoritative guidance for Syncfusion Windows Forms controls, licensing, theming, and layout patterns.
- Ensure changes follow official Syncfusion guidance and Wiley Widget UI rules.

When to use

- Any change involving Syncfusion WinForms controls (SfDataGrid, RibbonControlAdv, DockingManager, Schedule, TreeGrid, etc.).
- Licensing, theming, SkinManager, or control initialization order questions.

Required sources

- Local Essential Studio samples (authoritative local reference): C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19
- Always consult the Syncfusion WinForms Assistant MCP before changing a Syncfusion control API.

Local implementation resource

- Syncfusion Essential Studio sample files on this machine are located at:
  - C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19
- This local resource includes control samples and recommended implementation patterns.
- It is recommended to validate all Syncfusion method usage and control configuration against this local resource in addition to official docs/MCP guidance.

Critical rules

- Register the Syncfusion license before any Syncfusion control is created or manipulated (Program.cs startup must do this first).
- Use SfSkinManager as the single source of truth for themes; avoid manual BackColor/ForeColor except semantic status colors.
- Load the theme assembly before calling SfSkinManager.SetVisualStyle, and set ThemeName on Syncfusion controls created after form load.
- Do not mix VisualStyle enums and theme name strings for the same form/control.

Behavior guidance

- Prefer concise, direct recommendations with a short code snippet when an API change is required.
- Call out required order-of-operations when initialization timing matters.
- If a Syncfusion control is mentioned, ask for the control name and file only when needed to avoid ambiguity.

VS Code agent integration (design)

- Use a VS Code extension that registers a chat participant and loads this file as the system prompt for that participant.
- Example (TypeScript):

  ```ts
  import * as vscode from "vscode";
  import { readFileSync } from "node:fs";
  import { join } from "node:path";

  export function activate(context: vscode.ExtensionContext) {
    const skillPath = join(
      vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? "",
      ".vscode",
      "skills",
      "syncfusion-skill.md"
    );
    const skillText = readFileSync(skillPath, "utf8");

    const participant = vscode.chat.createChatParticipant("syncfusion", async (request, chatContext, stream, token) => {
      stream.progress({ message: "Using Syncfusion WinForms skill" });

      const messages = [
        new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.System, skillText),
        new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.User, request.prompt),
      ];

      const response = await request.model.sendRequest(messages, {}, token);
      for await (const text of response.text) {
        stream.markdown(text);
      }
    });

    context.subscriptions.push(participant);
  }
  ```

---

_Last updated: 2026-02-13_
