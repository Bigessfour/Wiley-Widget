using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Controls.Supporting
{
    /// <summary>
    /// Lightweight binding helper that maps view model properties to controls for ErrorProvider usage.
    /// </summary>
    public sealed class ErrorProviderBinding : IDisposable
    {
        private readonly ErrorProvider _errorProvider;
        private readonly Dictionary<string, Control> _controlMap = new();

        public ErrorProviderBinding(ErrorProvider errorProvider, object viewModel)
        {
            _errorProvider = errorProvider ?? throw new ArgumentNullException(nameof(errorProvider));
            _ = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void MapControl(string propertyName, Control control)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(propertyName));
            if (control == null) throw new ArgumentNullException(nameof(control));

            _controlMap[propertyName] = control;
        }

        public void SetError(string propertyName, string? message)
        {
            if (_controlMap.TryGetValue(propertyName, out var control))
            {
                _errorProvider.SetError(control, message ?? string.Empty);
#if DEBUG
                Debug.WriteLine($"[ERRORPROVIDER] SetError | Property={propertyName} | MessageLength={(message ?? string.Empty).Length} | Control={control.Name} | HostControlSize={control.Parent?.Size}");
#endif
            }
        }

        public void RefreshAllErrors()
        {
            foreach (var control in _controlMap.Values)
            {
                // Force the ErrorProvider to repaint current error state
                var current = _errorProvider.GetError(control);
                _errorProvider.SetError(control, current);
            }

#if DEBUG
            Debug.WriteLine($"[ERRORPROVIDER] RefreshAllErrors | ControlsMapped={_controlMap.Count} | HostControlSize={_controlMap.Values.FirstOrDefault()?.Parent?.Size}");
#endif
        }

        public void ClearErrors()
        {
            foreach (var control in _controlMap.Values)
            {
                _errorProvider.SetError(control, string.Empty);
            }
        }

        public void Dispose()
        {
            ClearErrors();
            _controlMap.Clear();
        }
    }
}
