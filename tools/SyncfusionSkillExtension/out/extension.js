"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const SKILL_RELATIVE_PATH = [".vscode", "skills", "syncfusion-skill.md"];
async function loadSkillText() {
    const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
    if (!workspaceFolder) {
        return "No workspace folder is open. Open the Wiley-Widget workspace to load the Syncfusion skill.";
    }
    const skillUri = vscode.Uri.joinPath(workspaceFolder.uri, ...SKILL_RELATIVE_PATH);
    try {
        const bytes = await vscode.workspace.fs.readFile(skillUri);
        return new TextDecoder("utf-8").decode(bytes);
    }
    catch (error) {
        return `Syncfusion skill file not found at ${skillUri.fsPath}.`;
    }
}
function renderModelError(stream, error) {
    if (error && typeof error === "object" && "message" in error) {
        stream.markdown(`Language model error: ${error.message}`);
        return;
    }
    stream.markdown("Unexpected error while contacting the language model.");
}
function activate(context) {
    const participant = vscode.chat.createChatParticipant("syncfusion-agent.syncfusion", async (request, _chatContext, stream, token) => {
        stream.progress("Using Syncfusion WinForms skill");
        if (!context.languageModelAccessInformation.canSendRequest) {
            stream.markdown("Language model access is not available. Ensure Copilot chat access is enabled for this extension.");
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
        }
        catch (error) {
            renderModelError(stream, error);
        }
    });
    context.subscriptions.push(participant);
}
function deactivate() {
    // No-op
}
//# sourceMappingURL=extension.js.map