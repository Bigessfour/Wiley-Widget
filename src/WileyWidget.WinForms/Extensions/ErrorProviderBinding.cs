using System.Windows.Forms;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Provides binding support for ErrorProvider control.
    /// </summary>
    /// <summary>
    /// Represents a class for errorproviderbinding.
    /// </summary>
    /// <summary>
    /// Represents a class for errorproviderbinding.
    /// </summary>
    /// <summary>
    /// Represents a class for errorproviderbinding.
    /// </summary>
    /// <summary>
    /// Represents a class for errorproviderbinding.
    /// </summary>
    public class ErrorProviderBinding
    {
        /// <summary>
        /// Represents the _errorprovider.
        /// </summary>
        private readonly ErrorProvider _errorProvider;

        public ErrorProviderBinding(ErrorProvider errorProvider)
        {
            _errorProvider = errorProvider;
        }
        /// <summary>
        /// Performs seterror. Parameters: control, message.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: control, message.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: control, message.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="message">The message.</param>
        /// <summary>
        /// Performs seterror. Parameters: control, message.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="message">The message.</param>

        public void SetError(Control control, string message)
        {
            _errorProvider.SetError(control, message);
        }
        /// <summary>
        /// Performs clear.
        /// </summary>
        /// <summary>
        /// Performs clear.
        /// </summary>
        /// <summary>
        /// Performs clear.
        /// </summary>
        /// <summary>
        /// Performs clear.
        /// </summary>
        /// <summary>
        /// Performs clear.
        /// </summary>

        public void Clear()
        {
            _errorProvider.Clear();
        }
    }
}
