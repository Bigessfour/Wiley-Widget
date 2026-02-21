using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services.AI
{
    public sealed class JarvisGrokBridgeHandler : IDisposable
    {
        private readonly IChatBridgeService _bridge;
        private readonly IAIService _aiService;
        private readonly IJARVISPersonalityService _personalityService;
        private readonly IAILoggingService _aiLoggingService;
        private readonly ILogger<JarvisGrokBridgeHandler> _logger;
        private readonly WileyWidget.WinForms.Automation.JarvisAutomationState? _automationState;

        public JarvisGrokBridgeHandler(
            IChatBridgeService bridge,
            IAIService aiService,
            IJARVISPersonalityService personalityService,
            IAILoggingService aiLoggingService,
            ILogger<JarvisGrokBridgeHandler> logger,
            WileyWidget.WinForms.Automation.JarvisAutomationState? automationState = null)
        {
            _bridge = bridge;
            _aiService = aiService;
            _personalityService = personalityService;
            _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
            _logger = logger;
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
            await HandlePromptAsync(e.Prompt);
        }

        private void OnResponseChunkReceived(object? sender, ChatResponseChunkEventArgs e)
        {
            // Automation state is updated when response completes
        }

        private void OnResponseCompleted(object? sender, EventArgs e)
        {
            _automationState?.MarkDiagnosticsCompleted();
        }

        private async Task HandlePromptAsync(string prompt)
        {
            _logger.LogInformation("[JARVIS-GROK] Received prompt ({Length} chars)", prompt.Length);

            // Enhanced message parsing: detect commands and structured input
            var parsedPrompt = ParseIncomingMessage(prompt);
            var actualPrompt = parsedPrompt.Content;
            var metadata = parsedPrompt.Metadata;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var responseBuilder = new System.Text.StringBuilder();

            try
            {
                // Log the query with metadata
                _aiLoggingService.LogQuery(actualPrompt, "JARVIS Chat", "grok-4-1-fast-reasoning");

                var systemPrompt = _personalityService.GetSystemPrompt();

                await foreach (var delta in _aiService.StreamResponseAsync(actualPrompt, systemPrompt))
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
                    metadata["command"] = parts[0].Substring(1);
                    if (parts.Length > 1)
                    {
                        metadata["command_param"] = parts[1];
                        content = parts[1]; // Use param as content
                    }
                    else
                    {
                        content = "";
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

            return new ParsedMessage { Content = content, Metadata = metadata };
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

        public void Dispose()
        {
            _bridge.ExternalPromptRequested -= OnExternalPromptRequested;
            _bridge.ResponseChunkReceived -= OnResponseChunkReceived;
            _bridge.ResponseCompleted -= OnResponseCompleted;
        }
    }
}
