using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Panel for hosting a Form as a child control in docking/panel layouts.
    /// Provides thread-safe hosting, cleanup, theme integration, and async lifecycle forwarding.
    /// </summary>
    public class FormHostPanel : UserControl, IAsyncInitializable
    {
        private Form? _hostedForm;

        /// <summary>
        /// Gets the currently hosted form, if any.
        /// </summary>
        public Form? HostedForm => _hostedForm;

        /// <summary>
        /// Event raised when the hosted form changes.
        /// </summary>
        public event EventHandler? HostedFormChanged;

        /// <summary>
        /// Initializes a new instance of the FormHostPanel class.
        /// </summary>
        public FormHostPanel()
        {
            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Size = new Size(560, 420);
            MinimumSize = new Size(420, 360);
            Dock = DockStyle.Fill;

            // Enable double buffering to reduce flicker during resize/repaint
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// Hosts a form as a child control in this panel.
        /// Thread-safe - can be called from any thread.
        /// </summary>
        /// <param name="form">The form to host.</param>
        /// <exception cref="ArgumentNullException">Thrown if form is null.</exception>
        public void HostForm(Form form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            void HostInternal()
            {
                if (_hostedForm != null)
                {
                    UnhostFormInternal();
                }

                _hostedForm = form;
                form.TopLevel = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.Dock = DockStyle.Fill;
                Controls.Add(form);
                form.Show();

                // Propagate size constraints from hosted form (with reasonable defaults)
                this.MinimumSize = new Size(
                    Math.Max(420, form.MinimumSize.Width > 0 ? form.MinimumSize.Width : 420),
                    Math.Max(360, form.MinimumSize.Height > 0 ? form.MinimumSize.Height : 360));

                if (form.MaximumSize.Width > 0 && form.MaximumSize.Height > 0)
                {
                    this.MaximumSize = form.MaximumSize;
                }

                // Apply current Syncfusion theme to ensure visual consistency
                ApplyCurrentTheme(form);

                // Notify observers
                HostedFormChanged?.Invoke(this, EventArgs.Empty);
            }

            if (InvokeRequired)
            {
                Invoke(new Action(HostInternal));
            }
            else
            {
                HostInternal();
            }
        }

        /// <summary>
        /// Removes and disposes the currently hosted form.
        /// Thread-safe - can be called from any thread.
        /// </summary>
        public void UnhostForm()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UnhostFormInternal));
            }
            else
            {
                UnhostFormInternal();
            }
        }

        /// <summary>
        /// Internal method to remove and dispose the hosted form.
        /// Must be called on UI thread.
        /// </summary>
        private void UnhostFormInternal()
        {
            if (_hostedForm == null) return;
            try
            {
                _hostedForm.Hide();
                Controls.Remove(_hostedForm);
                _hostedForm.Close(); // Standard form cleanup pattern
                _hostedForm.Dispose();
            }
            finally
            {
                _hostedForm = null;
                HostedFormChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Applies the current Syncfusion theme to the hosted form.
        /// </summary>
        /// <param name="form">The form to apply the theme to.</param>
        private void ApplyCurrentTheme(Form form)
        {
            try
            {
                var currentTheme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrEmpty(currentTheme))
                {
                    SfSkinManager.SetVisualStyle(form, currentTheme);
                }
            }
            catch
            {
                // Fail silently if theme application fails (e.g., form doesn't support theming)
            }
        }

        /// <summary>
        /// Disposes the panel and unhosts any hosted form.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Call internal method directly (no Invoke needed in Dispose)
                UnhostFormInternal();
            }
            base.Dispose(disposing);
        }

        #region Lifecycle Methods

        /// <summary>
        /// Loads the panel asynchronously.
        /// Forwards to hosted form if it implements IAsyncInitializable.
        /// </summary>
        public virtual async Task LoadAsync(CancellationToken ct)
        {
            if (_hostedForm is IAsyncInitializable asyncInit)
            {
                await asyncInit.InitializeAsync(ct).ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Saves the panel asynchronously.
        /// Forwards save requests to hosted forms that implement save-capable contracts.
        /// </summary>
        public virtual async Task SaveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_hostedForm == null)
            {
                return;
            }

            if (_hostedForm is ICompletablePanel completablePanel)
            {
                await completablePanel.SaveAsync(ct).ConfigureAwait(true);
                return;
            }

            var saveMethod = _hostedForm.GetType().GetMethod(nameof(SaveAsync), new[] { typeof(CancellationToken) });
            if (saveMethod == null)
            {
                return;
            }

            if (saveMethod.Invoke(_hostedForm, new object[] { ct }) is Task saveTask)
            {
                await saveTask.ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Validates the panel asynchronously.
        /// Returns true as hosted forms typically handle their own validation.
        /// </summary>
        public virtual Task<bool> ValidateAsync(CancellationToken ct) => Task.FromResult(true);

        /// <summary>
        /// Initializes the panel asynchronously.
        /// Forwards to hosted form if it implements IAsyncInitializable.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }

        #endregion
    }
}
