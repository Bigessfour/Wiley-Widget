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

        public JarvisGrokBridgeHandler(
            IChatBridgeService bridge,
            IAIService aiService,
            IJARVISPersonalityService personalityService,
            IAILoggingService aiLoggingService,
            ILogger<JarvisGrokBridgeHandler> logger)
        {
            _bridge = bridge;
            _aiService = aiService;
            _personalityService = personalityService;
            _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
            _logger = logger;

            _bridge.ExternalPromptRequested += OnExternalPromptRequested;
            _logger.LogInformation("[JARVIS-GROK] Bridge handler subscribed");
        }

        private async void OnExternalPromptRequested(object? sender, ChatExternalPromptEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Prompt)) return;
            await HandlePromptAsync(e.Prompt);
        }

        private async Task HandlePromptAsync(string prompt)
        {
            _logger.LogInformation("[JARVIS-GROK] Received prompt ({Length} chars)", prompt.Length);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var responseBuilder = new System.Text.StringBuilder();

            try
            {
                // Log the query
                _aiLoggingService.LogQuery(prompt, "JARVIS Chat", "grok-2-1212");

                var systemPrompt = _personalityService.GetSystemPrompt();

                await foreach (var delta in _aiService.StreamResponseAsync(prompt, systemPrompt))
                {
                    if (!string.IsNullOrWhiteSpace(delta))
                    {
                        responseBuilder.Append(delta);
                        await _bridge.SendResponseChunkAsync(delta);
                    }
                }

                sw.Stop();
                await _bridge.NotifyResponseCompletedAsync();

                // Log the successful response
                var fullResponse = responseBuilder.ToString();
                _aiLoggingService.LogResponse(prompt, fullResponse, sw.ElapsedMilliseconds, tokensUsed: 0);
                _logger.LogInformation("[JARVIS-GROK] Response completed in {Ms}ms, {CharCount} chars", sw.ElapsedMilliseconds, fullResponse.Length);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[JARVIS-GROK] Streaming failed");

                // Log the error
                _aiLoggingService.LogError(prompt, ex.Message, "JARVIS Chat");

                await _bridge.SendResponseChunkAsync($"\n\n**Error:** {ex.Message}");
                await _bridge.NotifyResponseCompletedAsync();
            }
        }

        public void Dispose()
        {
            _bridge.ExternalPromptRequested -= OnExternalPromptRequested;
        }
    }
}
