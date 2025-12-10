using System.Windows.Forms;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Provides binding support for ErrorProvider control.
    /// </summary>
    public class ErrorProviderBinding
    {
        private readonly ErrorProvider _errorProvider;

        public ErrorProviderBinding(ErrorProvider errorProvider)
        {
            _errorProvider = errorProvider;
        }

        public void SetError(Control control, string message)
        {
            _errorProvider.SetError(control, message);
        }

        public void Clear()
        {
            _errorProvider.Clear();
        }
    }
}
