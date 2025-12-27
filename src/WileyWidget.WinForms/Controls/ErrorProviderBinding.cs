using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Lightweight binding helper that maps view model properties to controls for ErrorProvider usage.
    /// </summary>
    public sealed class ErrorProviderBinding : IDisposable
    {
        /// <summary>
        /// Represents the _errorprovider.
        /// </summary>
        /// <summary>
        /// Represents the _errorprovider.
        /// </summary>
        private readonly ErrorProvider _errorProvider;
        private readonly Dictionary<string, Control> _controlMap = new();

        public ErrorProviderBinding(ErrorProvider errorProvider, object viewModel)
        {
            _errorProvider = errorProvider ?? throw new ArgumentNullException(nameof(errorProvider));
            _ = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
        /// <summary>
        /// Performs mapcontrol. Parameters: propertyName, control.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs mapcontrol. Parameters: propertyName, control.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs mapcontrol. Parameters: propertyName, control.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs mapcontrol. Parameters: propertyName, control.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="control">The control.</param>

        public void MapControl(string propertyName, Control control)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(propertyName));
            if (control == null) throw new ArgumentNullException(nameof(control));

            _controlMap[propertyName] = control;
        }
        /// <summary>
        /// Performs seterror. Parameters: propertyName, message.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: propertyName, message.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: propertyName, message.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: propertyName, message.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: propertyName, message.
        /// </summary>
        /// <param name="propertyName">The propertyName.</param>
        /// <param name="message">The message.</param>

        public void SetError(string propertyName, string? message)
        {
            if (_controlMap.TryGetValue(propertyName, out var control))
            {
                _errorProvider.SetError(control, message ?? string.Empty);
            }
        }
        /// <summary>
        /// Performs refreshallerrors.
        /// </summary>
        /// <summary>
        /// Performs refreshallerrors.
        /// </summary>
        /// <summary>
        /// Performs refreshallerrors.
        /// </summary>
        /// <summary>
        /// Performs refreshallerrors.
        /// </summary>
        /// <summary>
        /// Performs refreshallerrors.
        /// </summary>

        public void RefreshAllErrors()
        {
            foreach (var control in _controlMap.Values)
            {
                // Force the ErrorProvider to repaint current error state
                var current = _errorProvider.GetError(control);
                _errorProvider.SetError(control, current);
            }
        }
        /// <summary>
        /// Performs clearerrors.
        /// </summary>
        /// <summary>
        /// Performs clearerrors.
        /// </summary>
        /// <summary>
        /// Performs clearerrors.
        /// </summary>
        /// <summary>
        /// Performs clearerrors.
        /// </summary>
        /// <summary>
        /// Performs clearerrors.
        /// </summary>

        public void ClearErrors()
        {
            foreach (var control in _controlMap.Values)
            {
                _errorProvider.SetError(control, string.Empty);
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            ClearErrors();
            _controlMap.Clear();
        }
    }
}
