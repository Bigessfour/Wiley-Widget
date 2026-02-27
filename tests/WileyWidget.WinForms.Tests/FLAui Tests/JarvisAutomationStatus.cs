using System;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    public sealed class JarvisAutomationStatus
    {
        public static JarvisAutomationStatus Empty { get; } = new(false, false, false, 0, 0);

        public JarvisAutomationStatus() : this(false, false, false, 0, 0) { }

        private JarvisAutomationStatus(
            bool blazorReady,
            bool assistViewReady,
            bool diagnosticsReady,
            int promptCount,
            int responseCount)
        {
            BlazorReady = blazorReady;
            AssistViewReady = assistViewReady;
            DiagnosticsReady = diagnosticsReady;
            PromptCount = promptCount;
            ResponseCount = responseCount;
        }

        public bool BlazorReady { get; }
        public bool AssistViewReady { get; }
        public bool DiagnosticsReady { get; }
        public int PromptCount { get; }
        public int ResponseCount { get; }

        public string ToStatusString()
        {
            return string.Join(";",
                $"BlazorReady={BlazorReady}",
                $"AssistViewReady={AssistViewReady}",
                $"DiagnosticsReady={DiagnosticsReady}",
                $"PromptCount={PromptCount}",
                $"ResponseCount={ResponseCount}");
        }

        public static bool TryParse(string? statusText, out JarvisAutomationStatus status)
        {
            status = Empty;
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return false;
            }

            var parts = statusText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var blazorReady = false;
            var assistViewReady = false;
            var diagnosticsReady = false;
            var promptCount = 0;
            var responseCount = 0;

            foreach (var part in parts)
            {
                var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2)
                {
                    continue;
                }

                var key = pair[0];
                var value = pair[1];

                if (key.Equals("BlazorReady", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out var parsed))
                    {
                        blazorReady = parsed;
                    }
                }
                else if (key.Equals("AssistViewReady", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out var parsed))
                    {
                        assistViewReady = parsed;
                    }
                }
                else if (key.Equals("DiagnosticsReady", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out var parsed))
                    {
                        diagnosticsReady = parsed;
                    }
                }
                else if (key.Equals("PromptCount", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var parsed))
                    {
                        promptCount = parsed;
                    }
                }
                else if (key.Equals("ResponseCount", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var parsed))
                    {
                        responseCount = parsed;
                    }
                }
            }

            status = new JarvisAutomationStatus(blazorReady, assistViewReady, diagnosticsReady, promptCount, responseCount);
            return true;
        }
    }
}
