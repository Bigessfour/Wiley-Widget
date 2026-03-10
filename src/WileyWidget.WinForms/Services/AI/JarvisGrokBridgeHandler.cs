using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
// using WileyWidget.Models; // This line is already present in the file
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services.AI
{
    public sealed class JarvisGrokBridgeHandler : IDisposable
    {
        private const string DiagnosticCommand = "diagnose";
        private const string StartupDiagnosticCommand = "startup-diagnose";
        private const string PerformanceDiagnosticCommand = "perf-diagnose";
        private const string ThemeAuditCommand = "theme-audit";
        private const string XaiSetupCommand = "xai-setup";
        private const string XaiCurlCommand = "xai-curl";
        private const string XaiActivateCommand = "xai-activate";
        private const string XaiRotateCommand = "xai-rotate";
        private const string PromptModeMetadataKey = "prompt_mode";
        private const string SelfDiagnosisMode = "self_diagnose";
        private const string StartupDiagnosisMode = "startup_diagnose";
        private const string PerformanceDiagnosisMode = "performance_diagnose";
        private const string ThemeAuditMode = "theme_audit";
        private const int MaxConversationTurns = 6;
        private const int MaxPromptContextCharacters = 5000;
        private const int MaxAttachmentContextCharacters = 12000;
        private const int MaxTurnTextCharacters = 1200;

        private static readonly string[] SelfDiagnosisTriggerPhrases =
        {
            "self diagnose",
            "self-diagnose",
            "diagnose",
            "debug",
            "triage",
            "root cause",
            "why is",
            "startup issue",
            "jarvis issue",
            "right dock"
        };

        private static readonly string[] StartupDiagnosisTriggerPhrases =
        {
            "startup",
            "boot",
            "launch",
            "initialize",
            "initialization",
            "cold start"
        };

        private static readonly string[] PerformanceDiagnosisTriggerPhrases =
        {
            "slow",
            "latency",
            "timeout",
            "performance",
            "throughput",
            "high cpu",
            "memory"
        };

        private static readonly string[] ThemeAuditTriggerPhrases =
        {
            "theme",
            "skin",
            "sfskinmanager",
            "syncfusion style",
            "color mismatch",
            "visual style"
        };

        private const string SelfDiagnosisSystemPrompt = @"You are in Wiley Widget self-diagnosis mode.

Your goal is to diagnose Wiley Widget issues using evidence-first reasoning and available tools.

Rules:
1. Gather evidence before proposing fixes.
2. Prefer concrete observations from code/runtime over assumptions.
3. Call tools when needed to inspect files, search code, or evaluate runtime state.
4. For every finding, include severity and evidence references.
5. If evidence is insufficient, state what is missing and the smallest next diagnostic step.

Required response sections:
- Findings (ordered by severity)
- Evidence
- Most likely root cause
- Minimal fix
- Validation steps";

        private const string StartupDiagnosisSystemPrompt = @"You are in Wiley Widget startup diagnostics mode.

Goal: identify startup regressions, blocking initialization paths, and duplicated work.

Rules:
1. Build a startup timeline from evidence before recommending changes.
2. Highlight blocking operations on the UI thread.
3. Flag duplicate initialization, repeated service construction, and unnecessary startup work.
4. Prefer minimal-risk changes that reduce startup time without behavior regressions.

Required response sections:
- Startup timeline
- Hot spots
- Root cause hypothesis
- Minimal optimization patch
- Verification checklist";

        private const string PerformanceDiagnosisSystemPrompt = @"You are in Wiley Widget performance diagnostics mode.

Goal: diagnose latency, throughput, timeout, CPU, and memory issues using evidence.

Rules:
1. Separate symptoms from root causes.
2. Quantify impact when possible (ms, %, counts).
3. Prioritize fixes by cost/benefit and regression risk.
4. Propose instrumentation if data is incomplete.

Required response sections:
- Performance findings
- Evidence and metrics
- Root cause
- Ranked remediation options
- Validation plan";

        private const string ThemeAuditSystemPrompt = @"You are in Wiley Widget Syncfusion theming audit mode.

Goal: enforce SfSkinManager as the single source of truth and find theming violations.

Rules:
1. Detect manual BackColor/ForeColor styling that conflicts with SfSkinManager.
2. Verify ThemeName consistency for Syncfusion controls.
3. Prefer SyncfusionControlFactory for control creation and consistent theming.
4. Preserve semantic status color exceptions only (red/green/orange).

Required response sections:
- Theming violations
- Evidence (file and line references)
- Minimal compliant fixes
- Runtime validation steps";

        private readonly IChatBridgeService _bridge;
        private readonly IAIService _aiService;
        private readonly IJARVISPersonalityService _personalityService;
        private readonly IAILoggingService _aiLoggingService;
        private readonly SettingsSecretsPersistenceService? _settingsSecretsPersistenceService;
        private readonly ILogger<JarvisGrokBridgeHandler> _logger;
        private readonly WileyWidget.WinForms.Automation.JarvisAutomationState? _automationState;
        private readonly List<ConversationTurn> _conversationTurns = new();
        private readonly object _conversationHistoryGate = new();

        public JarvisGrokBridgeHandler(
            IChatBridgeService bridge,
            IAIService aiService,
            IJARVISPersonalityService personalityService,
            IAILoggingService aiLoggingService,
            ILogger<JarvisGrokBridgeHandler> logger,
            IServiceProvider? serviceProvider = null,
            WileyWidget.WinForms.Automation.JarvisAutomationState? automationState = null)
        {
            _bridge = bridge;
            _aiService = aiService;
            _personalityService = personalityService;
            _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
            _logger = logger;
            _settingsSecretsPersistenceService = serviceProvider?.GetService(typeof(SettingsSecretsPersistenceService)) as SettingsSecretsPersistenceService;
            _automationState = automationState;

            _bridge.ExternalPromptRequested += OnExternalPromptRequested;
            _bridge.ResponseChunkReceived += OnResponseChunkReceived;
            _bridge.ResponseCompleted += OnResponseCompleted;
            _logger.LogInformation("[JARVIS-GROK] Bridge handler subscribed");
        }

        private async void OnExternalPromptRequested(object? sender, ChatExternalPromptEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Prompt)) return;
            _automationState?.NotifyPrompt(e.Prompt);
            await HandlePromptAsync(e.Prompt, e.Attachments);
        }

        private void OnResponseChunkReceived(object? sender, ChatResponseChunkEventArgs e)
        {
            // Automation state is updated when response completes
        }

        private void OnResponseCompleted(object? sender, EventArgs e)
        {
            _automationState?.MarkDiagnosticsCompleted();
        }

        private async Task HandlePromptAsync(string prompt, IReadOnlyList<global::WileyWidget.Models.ChatPromptAttachment>? attachments = null)
        {
            _logger.LogInformation("[JARVIS-GROK] Received prompt ({Length} chars)", prompt.Length);

            // Enhanced message parsing: detect commands and structured input
            var parsedPrompt = ParseIncomingMessage(prompt);
            var actualPrompt = parsedPrompt.Content;
            var metadata = parsedPrompt.Metadata;
            using var correlationScope = _aiLoggingService.BeginCorrelationScope($"jarvis-{Guid.NewGuid():N}");

            if (metadata.TryGetValue("command", out var commandObj) && commandObj is string command && IsApiKeyCommand(command))
            {
                await HandleApiKeyCommandAsync(command, metadata, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var responseBuilder = new System.Text.StringBuilder();

            try
            {
                // Log the query with metadata
                _aiLoggingService.LogQuery(actualPrompt, "JARVIS Chat", "grok-4-1-fast-reasoning");

                var baseSystemPrompt = _personalityService.GetSystemPrompt();
                var systemPrompt = BuildEffectiveSystemPrompt(baseSystemPrompt, actualPrompt, metadata);
                var promptForModel = BuildPromptWithConversationContext(actualPrompt, attachments);

                if (!ReferenceEquals(systemPrompt, baseSystemPrompt))
                {
                    var mode = metadata.TryGetValue(PromptModeMetadataKey, out var modeObj) ? modeObj?.ToString() : SelfDiagnosisMode;
                    _logger.LogInformation("[JARVIS-GROK] Engineered prompt mode enabled: {Mode}", mode);
                }

                await foreach (var delta in _aiService.StreamResponseAsync(promptForModel, systemPrompt))
                {
                    if (!string.IsNullOrWhiteSpace(delta))
                    {
                        responseBuilder.Append(delta);
                        await _bridge.SendResponseChunkAsync(delta);
                    }
                }

                sw.Stop();

                // Enhanced response parsing: format and enrich the response
                var fullResponse = responseBuilder.ToString();
                var parsedResponse = ParseOutgoingMessage(fullResponse);

                if (!string.IsNullOrWhiteSpace(parsedResponse.PlainText))
                {
                    AppendConversationTurn(actualPrompt, parsedResponse.PlainText);
                }

                await _bridge.NotifyResponseCompletedAsync();

                // Log the successful response with parsed metadata
                var estimatedTokens = (int)Math.Ceiling(parsedResponse.PlainText.Length / 4.0);
                _aiLoggingService.LogResponse(actualPrompt, parsedResponse.PlainText, sw.ElapsedMilliseconds, tokensUsed: estimatedTokens);
                _logger.LogInformation("[JARVIS-GROK] Response completed in {Ms}ms, {CharCount} chars (~{Tokens} tokens), {Elements} parsed elements",
                    sw.ElapsedMilliseconds, parsedResponse.PlainText.Length, estimatedTokens, parsedResponse.Elements?.Count ?? 0);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[JARVIS-GROK] Streaming failed");

                // Log the error
                _aiLoggingService.LogError(actualPrompt, ex.Message, "JARVIS Chat");

                await _bridge.SendResponseChunkAsync($"\n\n**Error:** {ex.Message}");
                await _bridge.NotifyResponseCompletedAsync();
            }
        }

        /// <summary>
        /// Parses incoming messages for commands, metadata, and structured content.
        /// </summary>
        private ParsedMessage ParseIncomingMessage(string message)
        {
            var metadata = new System.Collections.Generic.Dictionary<string, object>();
            var content = message;

            // Detect commands (e.g., /command param)
            if (message.StartsWith("/"))
            {
                var parts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var command = parts[0].Substring(1);
                    metadata["command"] = command;

                    SetPromptModeFromCommand(command, metadata);

                    if (parts.Length > 1)
                    {
                        metadata["command_param"] = parts[1];
                        content = parts[1]; // Use param as content
                    }
                    else
                    {
                        if (string.Equals(command, DiagnosticCommand, StringComparison.OrdinalIgnoreCase))
                        {
                            content = "Run a self-diagnosis for Wiley Widget and report findings with evidence and minimal fixes.";
                        }
                        else if (string.Equals(command, StartupDiagnosticCommand, StringComparison.OrdinalIgnoreCase))
                        {
                            content = "Diagnose Wiley Widget startup performance and initialization issues using evidence and a minimal patch plan.";
                        }
                        else if (string.Equals(command, PerformanceDiagnosticCommand, StringComparison.OrdinalIgnoreCase))
                        {
                            content = "Diagnose Wiley Widget runtime performance issues and provide ranked remediations with validation steps.";
                        }
                        else if (string.Equals(command, ThemeAuditCommand, StringComparison.OrdinalIgnoreCase))
                        {
                            content = "Audit Wiley Widget Syncfusion theming compliance and report minimal fixes with evidence.";
                        }
                        else if (string.Equals(command, XaiSetupCommand, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(command, XaiCurlCommand, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(command, XaiActivateCommand, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(command, XaiRotateCommand, StringComparison.OrdinalIgnoreCase))
                        {
                            content = string.Empty;
                        }
                        else
                        {
                            content = "";
                        }
                    }
                }
            }

            // Detect JSON structure
            if (message.TrimStart().StartsWith("{") && message.TrimEnd().EndsWith("}"))
            {
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(message);
                    metadata["is_json"] = true;
                    metadata["json_root"] = json.RootElement.ValueKind.ToString();
                }
                catch
                {
                    // Not valid JSON, continue
                }
            }

            // Detect code blocks
            if (message.Contains("```"))
            {
                metadata["has_code_blocks"] = true;
            }

            var promptMode = ResolvePromptMode(content, metadata);
            if (!string.IsNullOrWhiteSpace(promptMode))
            {
                metadata[PromptModeMetadataKey] = promptMode;
                metadata["self_diagnose"] = true;
            }

            return new ParsedMessage { Content = content, Metadata = metadata };
        }

        private static bool IsApiKeyCommand(string command)
        {
            return string.Equals(command, XaiSetupCommand, StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, XaiCurlCommand, StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, XaiActivateCommand, StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, XaiRotateCommand, StringComparison.OrdinalIgnoreCase);
        }

        private async Task HandleApiKeyCommandAsync(string command, IReadOnlyDictionary<string, object> metadata, CancellationToken cancellationToken)
        {
            var normalizedCommand = command.Trim().ToLowerInvariant();
            metadata.TryGetValue("command_param", out var commandParamObj);
            var commandParam = commandParamObj?.ToString();

            if (normalizedCommand == XaiSetupCommand)
            {
                var setupMessage = BuildXaiSetupInstructions();
                _aiLoggingService.LogQuery("/xai-setup", "JARVIS Chat", "n/a");
                _aiLoggingService.LogResponse("/xai-setup", setupMessage, 0, tokensUsed: 0);
                await _bridge.SendResponseChunkAsync(setupMessage, cancellationToken).ConfigureAwait(false);
                await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (normalizedCommand == XaiCurlCommand)
            {
                var curlMessage = "Use this cURL test command to validate your xAI key before activation:\n\n" + BuildCurlValidationCommand();
                _aiLoggingService.LogQuery("/xai-curl", "JARVIS Chat", "n/a");
                _aiLoggingService.LogResponse("/xai-curl", curlMessage, 0, tokensUsed: 0);
                await _bridge.SendResponseChunkAsync(curlMessage, cancellationToken).ConfigureAwait(false);
                await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (normalizedCommand == XaiActivateCommand || normalizedCommand == XaiRotateCommand)
            {
                var verb = normalizedCommand == XaiRotateCommand ? "rotate" : "activate";
                var normalizedKey = NormalizeApiKey(commandParam);

                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    var usage = $"Usage: /xai-{verb} <xai-api-key>\n\nExample:\n/xai-{verb} xai-xxxxxxxxxxxxxxxxxxxxxxxx\n\nTip: run /xai-curl first to test a key with cURL.";
                    await _bridge.SendResponseChunkAsync(usage, cancellationToken).ConfigureAwait(false);
                    await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                var maskedKey = MaskApiKey(normalizedKey);
                _aiLoggingService.LogQuery($"/xai-{verb} {maskedKey}", "JARVIS Chat", "n/a");

                AIResponseResult validation;
                try
                {
                    validation = await _aiService.ValidateApiKeyAsync(normalizedKey, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[JARVIS-GROK] xAI key validation command failed");
                    await _bridge.SendResponseChunkAsync("I could not validate that key due to an internal error. Please retry or run /xai-curl.", cancellationToken).ConfigureAwait(false);
                    await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (validation.HttpStatusCode < 200 || validation.HttpStatusCode >= 300)
                {
                    var failure = $"Key validation failed (HTTP {validation.HttpStatusCode}).\nDetails: {validation.Content}\n\nYou can verify manually with:\n{BuildCurlValidationCommand()}";
                    _aiLoggingService.LogResponse($"/xai-{verb} {maskedKey}", failure, 0, tokensUsed: 0);
                    await _bridge.SendResponseChunkAsync(failure, cancellationToken).ConfigureAwait(false);
                    await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (_settingsSecretsPersistenceService != null)
                {
                    var persistResult = await _settingsSecretsPersistenceService
                        .PersistAsync(syncfusionLicenseKey: null, xaiApiKey: normalizedKey, cancellationToken)
                        .ConfigureAwait(false);

                    if (!persistResult.Success)
                    {
                        var persistError = $"Key validated, but secure persistence failed: {persistResult.ErrorMessage ?? "unknown error"}.";
                        _aiLoggingService.LogError($"/xai-{verb} {maskedKey}", persistError, "Persistence");
                        await _bridge.SendResponseChunkAsync(persistError, cancellationToken).ConfigureAwait(false);
                        await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    if (persistResult.Warnings.Count > 0)
                    {
                        _logger.LogWarning("[JARVIS-GROK] xAI key persistence warnings: {Warnings}", string.Join(" | ", persistResult.Warnings));
                    }
                }

                await _aiService.UpdateApiKeyAsync(normalizedKey, cancellationToken).ConfigureAwait(false);

                var successMessageBuilder = new StringBuilder();
                successMessageBuilder.Append("xAI key ");
                successMessageBuilder.Append(verb == "rotate" ? "rotation" : "activation");
                successMessageBuilder.Append(" succeeded for ");
                successMessageBuilder.Append(maskedKey);
                successMessageBuilder.AppendLine(".");
                successMessageBuilder.AppendLine();
                successMessageBuilder.AppendLine("Recommended next steps:");
                successMessageBuilder.AppendLine("1. Ask a quick check prompt: `are you working?`");
                successMessageBuilder.AppendLine("2. Revoke old keys in xAI Console after confirming the new key works.");
                successMessageBuilder.AppendLine("3. Use `/xai-rotate <new-key>` whenever you rotate credentials.");

                var successMessage = successMessageBuilder.ToString();
                _aiLoggingService.LogResponse($"/xai-{verb} {maskedKey}", successMessage, 0, tokensUsed: 0);
                await _bridge.SendResponseChunkAsync(successMessage, cancellationToken).ConfigureAwait(false);
                await _bridge.NotifyResponseCompletedAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static string BuildXaiSetupInstructions()
        {
            var builder = new StringBuilder();
            builder.AppendLine("xAI API onboarding is available directly in chat.");
            builder.AppendLine();
            builder.AppendLine("Commands you can run in JARVIS chat:");
            builder.AppendLine("- `/xai-setup` -> show setup and security guidance");
            builder.AppendLine("- `/xai-curl` -> show a cURL key-validation request");
            builder.AppendLine("- `/xai-activate <xai-api-key>` -> validate and securely persist a new key");
            builder.AppendLine("- `/xai-rotate <xai-api-key>` -> same as activate, intended for key rotation");
            builder.AppendLine();
            builder.AppendLine("Security notes:");
            builder.AppendLine("- Keys are persisted through secure app storage paths (user-secrets and configured secure stores).");
            builder.AppendLine("- Raw keys are not echoed back in chat responses.");
            builder.AppendLine("- Rotate keys regularly and revoke old keys in xAI Console.");
            builder.AppendLine();
            builder.AppendLine("cURL validation template:");
            builder.Append(BuildCurlValidationCommand());
            return builder.ToString();
        }

        private static string BuildCurlValidationCommand()
        {
            return "curl.exe https://api.x.ai/v1/responses -H \"Authorization: Bearer <XAI_API_KEY>\" -H \"Content-Type: application/json\" -d \"{\\\"model\\\":\\\"grok-4-1-fast-reasoning\\\",\\\"input\\\":[{\\\"role\\\":\\\"user\\\",\\\"content\\\":\\\"Say: API key test successful.\\\"}],\\\"stream\\\":false}\"";
        }

        private static string? NormalizeApiKey(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var trimmed = rawValue.Trim().Trim('"', '\'');
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "<empty>";
            }

            if (apiKey.Length <= 8)
            {
                return "****";
            }

            return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4, 4)}";
        }

        private static void SetPromptModeFromCommand(string command, IDictionary<string, object> metadata)
        {
            if (string.Equals(command, DiagnosticCommand, StringComparison.OrdinalIgnoreCase))
            {
                metadata[PromptModeMetadataKey] = SelfDiagnosisMode;
            }
            else if (string.Equals(command, StartupDiagnosticCommand, StringComparison.OrdinalIgnoreCase))
            {
                metadata[PromptModeMetadataKey] = StartupDiagnosisMode;
            }
            else if (string.Equals(command, PerformanceDiagnosticCommand, StringComparison.OrdinalIgnoreCase))
            {
                metadata[PromptModeMetadataKey] = PerformanceDiagnosisMode;
            }
            else if (string.Equals(command, ThemeAuditCommand, StringComparison.OrdinalIgnoreCase))
            {
                metadata[PromptModeMetadataKey] = ThemeAuditMode;
            }
        }

        private static string ResolvePromptMode(string message, IReadOnlyDictionary<string, object> metadata)
        {
            if (metadata.TryGetValue(PromptModeMetadataKey, out var explicitMode)
                && explicitMode is string explicitModeValue
                && !string.IsNullOrWhiteSpace(explicitModeValue))
            {
                return explicitModeValue;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            if (StartupDiagnosisTriggerPhrases.Any(trigger =>
                message.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
            {
                return StartupDiagnosisMode;
            }

            if (PerformanceDiagnosisTriggerPhrases.Any(trigger =>
                message.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
            {
                return PerformanceDiagnosisMode;
            }

            if (ThemeAuditTriggerPhrases.Any(trigger =>
                message.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
            {
                return ThemeAuditMode;
            }

            if (SelfDiagnosisTriggerPhrases.Any(trigger =>
                message.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
            {
                return SelfDiagnosisMode;
            }

            return string.Empty;
        }

        private static string BuildEffectiveSystemPrompt(
            string basePrompt,
            string actualPrompt,
            IReadOnlyDictionary<string, object> metadata)
        {
            var promptMode = ResolvePromptMode(actualPrompt, metadata);
            if (string.IsNullOrWhiteSpace(promptMode))
            {
                return basePrompt;
            }

            var selectedTemplate = promptMode switch
            {
                StartupDiagnosisMode => StartupDiagnosisSystemPrompt,
                PerformanceDiagnosisMode => PerformanceDiagnosisSystemPrompt,
                ThemeAuditMode => ThemeAuditSystemPrompt,
                _ => SelfDiagnosisSystemPrompt
            };

            return basePrompt + Environment.NewLine + Environment.NewLine + selectedTemplate;
        }

        private string BuildPromptWithConversationContext(string actualPrompt, IReadOnlyList<global::WileyWidget.Models.ChatPromptAttachment>? attachments = null)
        {
            if (string.IsNullOrWhiteSpace(actualPrompt))
            {
                return actualPrompt;
            }

            var maxContextCharacters = MaxPromptContextCharacters + ((attachments?.Count ?? 0) > 0 ? MaxAttachmentContextCharacters : 0);

            var turns = GetConversationTurnsSnapshot();
            if (turns.Count == 0)
            {
                return BuildCurrentRequestPrompt(actualPrompt, attachments);
            }

            var contextualPrompt = BuildContextualPrompt(actualPrompt, turns, attachments);
            while (contextualPrompt.Length > maxContextCharacters && turns.Count > 1)
            {
                turns.RemoveAt(0);
                contextualPrompt = BuildContextualPrompt(actualPrompt, turns, attachments);
            }

            if (contextualPrompt.Length > maxContextCharacters)
            {
                contextualPrompt = BuildCurrentRequestPrompt(actualPrompt, attachments);
            }

            _logger.LogDebug("[JARVIS-GROK] Applied {TurnCount} prior conversation turns to prompt context", turns.Count);
            return contextualPrompt;
        }

        private static string BuildContextualPrompt(
            string actualPrompt,
            IReadOnlyList<ConversationTurn> turns,
            IReadOnlyList<global::WileyWidget.Models.ChatPromptAttachment>? attachments)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Conversation context (most recent last):");

            foreach (var turn in turns)
            {
                builder.Append("User: ");
                builder.AppendLine(TruncateConversationText(turn.UserPrompt, MaxTurnTextCharacters));
                builder.Append("Assistant: ");
                builder.AppendLine(TruncateConversationText(turn.AssistantResponse, MaxTurnTextCharacters));
            }

            builder.AppendLine();
            AppendCurrentRequest(builder, actualPrompt, attachments);
            return builder.ToString();
        }

        private static string BuildCurrentRequestPrompt(string actualPrompt, IReadOnlyList<global::WileyWidget.Models.ChatPromptAttachment>? attachments)
        {
            var builder = new StringBuilder();
            AppendCurrentRequest(builder, actualPrompt, attachments);
            return builder.ToString();
        }

        private static void AppendCurrentRequest(
            StringBuilder builder,
            string actualPrompt,
            IReadOnlyList<global::WileyWidget.Models.ChatPromptAttachment>? attachments)
        {
            builder.Append("Current user request: ");
            builder.AppendLine(actualPrompt);

            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            AppendAttachmentContext(builder, attachments);
        }

        private static void AppendAttachmentContext(
            StringBuilder builder,
            IReadOnlyList<global::WileyWidget.Models.ChatPromptAttachment>? attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            builder.AppendLine("Attached file context for the current request:");

            for (var index = 0; index < attachments.Count; index++)
            {
                var attachment = attachments[index];
                builder.Append("[Attachment ");
                builder.Append(index + 1);
                builder.AppendLine("]");
                builder.Append("FileName: ");
                builder.AppendLine(attachment.FileName);
                builder.Append("ContentType: ");
                builder.AppendLine(string.IsNullOrWhiteSpace(attachment.ContentType) ? "unknown" : attachment.ContentType);
                builder.Append("SizeBytes: ");
                builder.AppendLine(attachment.SizeBytes.ToString());
                builder.Append("Truncated: ");
                builder.AppendLine(attachment.IsTruncated ? "yes" : "no");
                builder.AppendLine("Content:");
                builder.AppendLine("```text");
                builder.AppendLine(attachment.Content);
                builder.AppendLine("```");
                builder.AppendLine();
            }
        }

        private List<ConversationTurn> GetConversationTurnsSnapshot()
        {
            lock (_conversationHistoryGate)
            {
                return _conversationTurns.ToList();
            }
        }

        private void AppendConversationTurn(string userPrompt, string assistantResponse)
        {
            var userText = TruncateConversationText(userPrompt, MaxTurnTextCharacters);
            var assistantText = TruncateConversationText(assistantResponse, MaxTurnTextCharacters);

            lock (_conversationHistoryGate)
            {
                _conversationTurns.Add(new ConversationTurn(userText, assistantText));
                while (_conversationTurns.Count > MaxConversationTurns)
                {
                    _conversationTurns.RemoveAt(0);
                }
            }
        }

        private static string TruncateConversationText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Parses outgoing messages for formatting, links, and structured elements.
        /// </summary>
        private ParsedResponse ParseOutgoingMessage(string response)
        {
            var metadata = new System.Collections.Generic.Dictionary<string, object>();
            var elements = new System.Collections.Generic.List<MessageElement>();

            // Simple markdown parsing
            var lines = response.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("# "))
                {
                    elements.Add(new MessageElement { Type = "heading", Content = line.Substring(2) });
                }
                else if (line.StartsWith("```"))
                {
                    elements.Add(new MessageElement { Type = "code_start", Content = line });
                }
                else if (line.Contains("http://") || line.Contains("https://"))
                {
                    elements.Add(new MessageElement { Type = "link", Content = line });
                    metadata["has_links"] = true;
                }
                else
                {
                    elements.Add(new MessageElement { Type = "text", Content = line });
                }
            }

            metadata["element_count"] = elements.Count;

            return new ParsedResponse
            {
                PlainText = response,
                Elements = elements,
                Metadata = metadata
            };
        }

        private class ParsedMessage
        {
            public string Content { get; set; } = "";
            public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; } = new();
        }

        private class ParsedResponse
        {
            public string PlainText { get; set; } = "";
            public System.Collections.Generic.List<MessageElement> Elements { get; set; } = new();
            public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; } = new();
        }

        private class MessageElement
        {
            public string Type { get; set; } = "";
            public string Content { get; set; } = "";
        }

        private sealed record ConversationTurn(string UserPrompt, string AssistantResponse);

        public void Dispose()
        {
            _bridge.ExternalPromptRequested -= OnExternalPromptRequested;
            _bridge.ResponseChunkReceived -= OnResponseChunkReceived;
            _bridge.ResponseCompleted -= OnResponseCompleted;
        }
    }
}
