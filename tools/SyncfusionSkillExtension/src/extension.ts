import * as vscode from "vscode";

const SKILL_RELATIVE_PATH = [".vscode", "skills", "syncfusion-skill.md"];

async function loadSkillText(): Promise<string> {
  const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
  if (!workspaceFolder) {
    return "No workspace folder is open. Open the Wiley-Widget workspace to load the Syncfusion skill.";
  }

  const skillUri = vscode.Uri.joinPath(workspaceFolder.uri, ...SKILL_RELATIVE_PATH);

  try {
    const bytes = await vscode.workspace.fs.readFile(skillUri);
    return new TextDecoder("utf-8").decode(bytes);
  } catch (error) {
    return `Syncfusion skill file not found at ${skillUri.fsPath}.`;
  }
}

function renderModelError(stream: vscode.ChatResponseStream, error: unknown): void {
  if (error && typeof error === "object" && "message" in error) {
    stream.markdown(`Language model error: ${(error as Error).message}`);
    return;
  }

  stream.markdown("Unexpected error while contacting the language model.");
}

export function activate(context: vscode.ExtensionContext): void {
  const participant = vscode.chat.createChatParticipant(
    "syncfusion-agent.syncfusion",
    async (
      request: vscode.ChatRequest,
      _chatContext: vscode.ChatContext,
      stream: vscode.ChatResponseStream,
      token: vscode.CancellationToken
    ) => {
      stream.progress("Using Syncfusion WinForms skill");

      if (!context.languageModelAccessInformation.canSendRequest) {
        stream.markdown(
          "Language model access is not available. Ensure Copilot chat access is enabled for this extension."
        );
        return;
      }

      const skillText = await loadSkillText();

      const messages = [
        vscode.LanguageModelChatMessage.User(skillText),
        vscode.LanguageModelChatMessage.User(request.prompt)
      ];

      try {
        const response = await request.model.sendRequest(messages, {}, token);
        for await (const text of response.text) {
          stream.markdown(text);
        }
      } catch (error) {
        renderModelError(stream, error);
      }
    }
  );

  context.subscriptions.push(participant);
}

export function deactivate(): void {
  // No-op
}
