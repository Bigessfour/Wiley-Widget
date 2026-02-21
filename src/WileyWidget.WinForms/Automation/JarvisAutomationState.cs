using System;

namespace WileyWidget.WinForms.Automation
{
    public sealed class JarvisAutomationState
    {
        private readonly object _lock = new();
        private JarvisAutomationSnapshot _snapshot = JarvisAutomationSnapshot.Empty;

        public event EventHandler<JarvisAutomationStateChangedEventArgs>? Changed;

        public JarvisAutomationSnapshot Snapshot
        {
            get
            {
                lock (_lock)
                {
                    return _snapshot;
                }
            }
        }

        public void Reset()
        {
            UpdateState(_ => JarvisAutomationSnapshot.Empty);
        }

        public void MarkBlazorReady(bool assistViewReady)
        {
            UpdateState(current => current with
            {
                BlazorReady = true,
                AssistViewReady = current.AssistViewReady || assistViewReady
            });
        }

        public void MarkAssistViewReady()
        {
            UpdateState(current => current with { AssistViewReady = true });
        }

        public void MarkDiagnosticsCompleted()
        {
            UpdateState(current => current with { DiagnosticsReady = true });
        }

        public void NotifyPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            UpdateState(current => current with
            {
                PromptCount = current.PromptCount + 1,
                LastPrompt = prompt
            });
        }

        public void NotifyResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return;
            }

            UpdateState(current => current with
            {
                ResponseCount = current.ResponseCount + 1,
                LastResponse = response
            });
        }

        private void UpdateState(Func<JarvisAutomationSnapshot, JarvisAutomationSnapshot> update)
        {
            JarvisAutomationSnapshot snapshot;
            lock (_lock)
            {
                snapshot = update(_snapshot) with { LastUpdatedUtc = DateTime.UtcNow };
                _snapshot = snapshot;
            }

            Changed?.Invoke(this, new JarvisAutomationStateChangedEventArgs(snapshot));
        }
    }

    public sealed record JarvisAutomationSnapshot(
        bool BlazorReady,
        bool AssistViewReady,
        bool DiagnosticsReady,
        int PromptCount,
        int ResponseCount,
        string? LastPrompt,
        string? LastResponse,
        DateTime LastUpdatedUtc)
    {
        public static JarvisAutomationSnapshot Empty { get; } = new(
            BlazorReady: false,
            AssistViewReady: false,
            DiagnosticsReady: false,
            PromptCount: 0,
            ResponseCount: 0,
            LastPrompt: null,
            LastResponse: null,
            LastUpdatedUtc: DateTime.MinValue);

        public string ToStatusString()
        {
            return string.Join(";",
                $"BlazorReady={BlazorReady}",
                $"AssistViewReady={AssistViewReady}",
                $"DiagnosticsReady={DiagnosticsReady}",
                $"PromptCount={PromptCount}",
                $"ResponseCount={ResponseCount}",
                $"LastPrompt={LastPrompt ?? string.Empty}",
                $"LastResponse={LastResponse ?? string.Empty}",
                $"LastUpdatedUtc={LastUpdatedUtc:o}");
        }
    }

    public sealed class JarvisAutomationStateChangedEventArgs : EventArgs
    {
        public JarvisAutomationStateChangedEventArgs(JarvisAutomationSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public JarvisAutomationSnapshot Snapshot { get; }
    }
}
