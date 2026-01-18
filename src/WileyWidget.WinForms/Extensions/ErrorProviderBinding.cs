using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Robust error management for ErrorProvider control.
    /// Tracks error state per control and supports clearing specific or all errors.
    /// </summary>
    public class ErrorProviderBinding : IDisposable
    {
        private readonly ErrorProvider _errorProvider;
        private readonly Dictionary<Control, string> _errorTracker = new();
        private bool _disposed;

        public ErrorProviderBinding(ErrorProvider errorProvider)
        {
            _errorProvider = errorProvider ?? throw new ArgumentNullException(nameof(errorProvider));
        }

        /// <summary>
        /// Set error on a specific control. Null or empty message clears the error.
        /// </summary>
        public void SetError(Control control, string? message)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (_disposed) throw new ObjectDisposedException(GetType().Name);

            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    _errorProvider.SetError(control, "");
                    _errorTracker.Remove(control);
                }
                else
                {
                    _errorProvider.SetError(control, message);
                    _errorTracker[control] = message;
                }
            }
            catch (ObjectDisposedException)
            {
                // Control disposed
                _errorTracker.Remove(control);
            }
        }

        /// <summary>
        /// Clears error on a specific control.
        /// </summary>
        public void ClearError(Control control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            SetError(control, null);
        }

        /// <summary>
        /// Clears all errors on all controls tracked by this provider.
        /// </summary>
        public void ClearAllErrors()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);

            try
            {
                _errorProvider.Clear();
                _errorTracker.Clear();
            }
            catch (ObjectDisposedException)
            {
                // Provider disposed
                _errorTracker.Clear();
            }
        }

        /// <summary>
        /// Get current error message for a control (null if no error).
        /// </summary>
        public string? GetError(Control control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            return _errorTracker.TryGetValue(control, out var error) ? error : null;
        }

        /// <summary>
        /// Check if there are any active errors.
        /// </summary>
        public bool HasErrors => _errorTracker.Count > 0;

        /// <summary>
        /// Get count of controls with errors.
        /// </summary>
        public int ErrorCount => _errorTracker.Count;

        /// <summary>
        /// Properly implements IDisposable pattern with finalizer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for proper IDisposable pattern.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                try { _errorProvider?.Dispose(); } catch { }
                _errorTracker.Clear();
            }
        }

        /// <summary>
        /// Finalizer ensures cleanup if Dispose wasn't called.
        /// </summary>
        ~ErrorProviderBinding()
        {
            Dispose(false);
        }
    }
}
