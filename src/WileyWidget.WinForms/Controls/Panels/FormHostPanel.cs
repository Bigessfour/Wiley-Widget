using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;

namespace WileyWidget.WinForms.Controls.Panels
{
    public class FormHostPanel : UserControl, IAsyncInitializable
    {
        private Form? _hostedForm;

        public FormHostPanel()
        {
            Dock = DockStyle.Fill;
        }

        public void HostForm(Form form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));
            if (_hostedForm != null)
            {
                UnhostForm();
            }

            _hostedForm = form;
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock = DockStyle.Fill;
            Controls.Add(form);
            form.Show();
        }

        public void UnhostForm()
        {
            if (_hostedForm == null) return;
            try
            {
                _hostedForm.Hide();
                Controls.Remove(_hostedForm);
                _hostedForm.Dispose();
            }
            finally
            {
                _hostedForm = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnhostForm();
            }
            base.Dispose(disposing);
        }

        // Lightweight lifecycle surface to mirror panels where convenient
        public virtual Task LoadAsync(CancellationToken ct) => Task.CompletedTask;
        public virtual Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public virtual Task<bool> ValidateAsync(CancellationToken ct) => Task.FromResult(true);

        /// <summary>
        /// Forward IAsyncInitializable.InitializeAsync to the hosted form if it implements the interface.
        /// This allows PanelNavigationService to perform async initialization for form-hosted dashboards.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_hostedForm is IAsyncInitializable asyncInit)
            {
                await asyncInit.InitializeAsync(cancellationToken).ConfigureAwait(true);
            }
        }
    }
}
