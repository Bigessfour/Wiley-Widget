using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Provides methods to bind an <see cref="ErrorProvider"/> to a ViewModel implementing
    /// <see cref="INotifyDataErrorInfo"/> for automatic error display on WinForms controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This helper enables MVVM-style validation in WinForms by subscribing to the ViewModel's
    /// <see cref="INotifyDataErrorInfo.ErrorsChanged"/> event and updating the ErrorProvider
    /// for mapped controls automatically.
    /// </para>
    /// <para>
    /// Usage:
    /// <code>
    /// var binding = new ErrorProviderBinding(_errorProvider, _viewModel);
    /// binding.MapControl(nameof(vm.ConnectionString), txtConnectionString);
    /// binding.MapControl(nameof(vm.AutoSaveIntervalMinutes), numAutoSave);
    /// // On form close: binding.Dispose();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class ErrorProviderBinding : IDisposable
    {
        private readonly ErrorProvider _errorProvider;
        private readonly INotifyDataErrorInfo _dataErrorInfo;
        private readonly Dictionary<string, Control> _propertyControlMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ToolStripItem> _propertyToolStripMap = new(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>
        /// Gets or sets a value indicating whether to show only the first error per property.
        /// Default is <c>true</c>.
        /// </summary>
        public bool ShowFirstErrorOnly { get; set; } = true;

        /// <summary>
        /// Gets or sets the error blink style. Default is <see cref="ErrorBlinkStyle.BlinkIfDifferentError"/>.
        /// </summary>
        public ErrorBlinkStyle BlinkStyle
        {
            get => _errorProvider.BlinkStyle;
            set => _errorProvider.BlinkStyle = value;
        }

        /// <summary>
        /// Initializes a new binding between an ErrorProvider and a ViewModel.
        /// </summary>
        /// <param name="errorProvider">The ErrorProvider to update with validation errors.</param>
        /// <param name="dataErrorInfo">The ViewModel implementing INotifyDataErrorInfo.</param>
        /// <exception cref="ArgumentNullException">If any argument is null.</exception>
        public ErrorProviderBinding(ErrorProvider errorProvider, INotifyDataErrorInfo dataErrorInfo)
        {
            _errorProvider = errorProvider ?? throw new ArgumentNullException(nameof(errorProvider));
            _dataErrorInfo = dataErrorInfo ?? throw new ArgumentNullException(nameof(dataErrorInfo));

            // Subscribe to errors changed event
            _dataErrorInfo.ErrorsChanged += OnErrorsChanged;

            // Configure error provider for smoother UX
            _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        }

        /// <summary>
        /// Maps a ViewModel property to a WinForms control for error display.
        /// </summary>
        /// <param name="propertyName">The ViewModel property name.</param>
        /// <param name="control">The control to show errors on.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public ErrorProviderBinding MapControl(string propertyName, Control control)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (control is null)
                throw new ArgumentNullException(nameof(control));

            _propertyControlMap[propertyName] = control;

            // Show any existing errors immediately
            UpdateControlError(propertyName);

            return this;
        }

        /// <summary>
        /// Maps multiple ViewModel properties to controls at once.
        /// </summary>
        /// <param name="mappings">Dictionary of property name to control mappings.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public ErrorProviderBinding MapControls(IDictionary<string, Control> mappings)
        {
            if (mappings is null)
                throw new ArgumentNullException(nameof(mappings));

            foreach (var kvp in mappings)
            {
                MapControl(kvp.Key, kvp.Value);
            }

            return this;
        }

        /// <summary>
        /// Maps a ViewModel property to a ToolStripItem for error display (via ToolTip).
        /// </summary>
        /// <param name="propertyName">The ViewModel property name.</param>
        /// <param name="item">The ToolStripItem to show errors on.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public ErrorProviderBinding MapToolStripItem(string propertyName, ToolStripItem item)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (item is null)
                throw new ArgumentNullException(nameof(item));

            _propertyToolStripMap[propertyName] = item;

            // Show any existing errors immediately
            UpdateToolStripError(propertyName);

            return this;
        }

        /// <summary>
        /// Removes a property-to-control mapping.
        /// </summary>
        /// <param name="propertyName">The property name to unmap.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public ErrorProviderBinding UnmapControl(string propertyName)
        {
            if (_propertyControlMap.TryGetValue(propertyName, out var control))
            {
                _errorProvider.SetError(control, string.Empty);
                _propertyControlMap.Remove(propertyName);
            }
            return this;
        }

        /// <summary>
        /// Clears all errors and mappings.
        /// </summary>
        public void ClearAll()
        {
            foreach (var control in _propertyControlMap.Values)
            {
                try { _errorProvider.SetError(control, string.Empty); } catch { }
            }
            foreach (var item in _propertyToolStripMap.Values)
            {
                try { item.ToolTipText = string.Empty; } catch { }
            }
            _propertyControlMap.Clear();
            _propertyToolStripMap.Clear();
        }

        /// <summary>
        /// Forces a refresh of all error states from the ViewModel.
        /// </summary>
        public void RefreshAllErrors()
        {
            foreach (var propertyName in _propertyControlMap.Keys.ToList())
            {
                UpdateControlError(propertyName);
            }
            foreach (var propertyName in _propertyToolStripMap.Keys.ToList())
            {
                UpdateToolStripError(propertyName);
            }
        }

        /// <summary>
        /// Gets whether any mapped control currently has a validation error.
        /// </summary>
        public bool HasErrors => _dataErrorInfo.HasErrors;

        /// <summary>
        /// Gets all current error messages as a concatenated string.
        /// </summary>
        public string GetAllErrorsText(string separator = "\n")
        {
            var allErrors = new List<string>();
            foreach (var propertyName in _propertyControlMap.Keys)
            {
                var errors = GetErrorMessages(propertyName);
                allErrors.AddRange(errors);
            }
            return string.Join(separator, allErrors.Distinct());
        }

        private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
        {
            if (_disposed) return;

            var propertyName = e.PropertyName;
            if (string.IsNullOrEmpty(propertyName))
            {
                // All errors changed - refresh everything
                RefreshAllErrors();
                return;
            }

            UpdateControlError(propertyName);
            UpdateToolStripError(propertyName);
        }

        private void UpdateControlError(string propertyName)
        {
            if (!_propertyControlMap.TryGetValue(propertyName, out var control))
                return;

            try
            {
                var errorMessages = GetErrorMessages(propertyName);
                var errorText = ShowFirstErrorOnly
                    ? errorMessages.FirstOrDefault() ?? string.Empty
                    : string.Join(Environment.NewLine, errorMessages);

                // Marshal to UI thread if needed
                if (control.InvokeRequired)
                {
                    control.BeginInvoke(new Action(() => _errorProvider.SetError(control, errorText)));
                }
                else
                {
                    _errorProvider.SetError(control, errorText);
                }
            }
            catch (ObjectDisposedException)
            {
                // Control was disposed - remove mapping
                _propertyControlMap.Remove(propertyName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ErrorProviderBinding: Failed to update error for {propertyName}: {ex.Message}");
            }
        }

        private void UpdateToolStripError(string propertyName)
        {
            if (!_propertyToolStripMap.TryGetValue(propertyName, out var item))
                return;

            try
            {
                var errorMessages = GetErrorMessages(propertyName);
                var errorText = ShowFirstErrorOnly
                    ? errorMessages.FirstOrDefault() ?? string.Empty
                    : string.Join(Environment.NewLine, errorMessages);

                // ToolStripItems don't have InvokeRequired, access parent
                var parent = item.GetCurrentParent();
                if (parent != null && parent.InvokeRequired)
                {
                    parent.BeginInvoke(new Action(() => item.ToolTipText = errorText));
                }
                else
                {
                    item.ToolTipText = errorText;
                }
            }
            catch (ObjectDisposedException)
            {
                _propertyToolStripMap.Remove(propertyName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ErrorProviderBinding: Failed to update ToolStrip error for {propertyName}: {ex.Message}");
            }
        }

        private IReadOnlyList<string> GetErrorMessages(string propertyName)
        {
            var errors = _dataErrorInfo.GetErrors(propertyName);
            if (errors is null)
                return Array.Empty<string>();

            var messages = new List<string>();
            foreach (var error in errors)
            {
                switch (error)
                {
                    case ValidationResult vr when vr.ErrorMessage is not null:
                        messages.Add(vr.ErrorMessage);
                        break;
                    case string s when !string.IsNullOrWhiteSpace(s):
                        messages.Add(s);
                        break;
                    case not null:
                        var str = error.ToString();
                        if (!string.IsNullOrWhiteSpace(str))
                            messages.Add(str);
                        break;
                }
            }
            return messages;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _dataErrorInfo.ErrorsChanged -= OnErrorsChanged;
            ClearAll();
        }
    }

    /// <summary>
    /// Extension methods for easy ErrorProviderBinding setup.
    /// </summary>
    public static class ErrorProviderBindingExtensions
    {
        /// <summary>
        /// Creates an ErrorProviderBinding for a ViewModel and returns it for configuration.
        /// </summary>
        /// <param name="errorProvider">The ErrorProvider to bind.</param>
        /// <param name="viewModel">The ViewModel implementing INotifyDataErrorInfo.</param>
        /// <returns>The created binding for fluent configuration.</returns>
        public static ErrorProviderBinding BindToViewModel(
            this ErrorProvider errorProvider,
            INotifyDataErrorInfo viewModel)
        {
            return new ErrorProviderBinding(errorProvider, viewModel);
        }
    }
}
