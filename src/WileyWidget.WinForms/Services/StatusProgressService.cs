using System;

namespace WileyWidget.WinForms.Services
{
    public interface IStatusProgressService
    {
        event EventHandler<StatusProgressUpdate>? ProgressChanged;

        void Start(string operation, string? message = null, bool isIndeterminate = true);

        void Report(string operation, double percent, string? message = null);

        void Complete(string operation, string? message = null);
    }

    public sealed record StatusProgressUpdate(
        string Operation,
        string? Message,
        double? Percent,
        bool IsIndeterminate,
        bool IsActive);

    public sealed class StatusProgressService : IStatusProgressService
    {
        private readonly object _lock = new();
        private string? _currentOperation;

        public event EventHandler<StatusProgressUpdate>? ProgressChanged;

        public void Start(string operation, string? message = null, bool isIndeterminate = true)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                return;
            }

            lock (_lock)
            {
                _currentOperation = operation;
                double? percent = isIndeterminate ? null : 0d;
                RaiseProgressChanged(new StatusProgressUpdate(operation, message, percent, IsIndeterminate: isIndeterminate, IsActive: true));
            }
        }

        public void Report(string operation, double percent, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                return;
            }

            var clamped = Math.Max(0, Math.Min(100, percent));
            lock (_lock)
            {
                _currentOperation = operation;
                RaiseProgressChanged(new StatusProgressUpdate(operation, message, clamped, IsIndeterminate: false, IsActive: true));
            }
        }

        public void Complete(string operation, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                return;
            }

            lock (_lock)
            {
                if (string.Equals(_currentOperation, operation, StringComparison.OrdinalIgnoreCase))
                {
                    _currentOperation = null;
                }

                RaiseProgressChanged(new StatusProgressUpdate(operation, message, 100, IsIndeterminate: false, IsActive: false));
            }
        }

        private void RaiseProgressChanged(StatusProgressUpdate update)
        {
            ProgressChanged?.Invoke(this, update);
        }
    }
}
