using Prism.Dialogs;
using Prism.Mvvm;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Base class for dialog ViewModels that implement IDialogAware.
    /// Provides proper disposal pattern to prevent memory leaks and shutdown exceptions.
    /// </summary>
    public abstract class DialogViewModelBase : BindableBase, IDialogAware, IDisposable
    {
        private bool _disposed;

        public string Title { get; set; } = string.Empty;
        public DialogCloseListener RequestClose { get; set; }

        public virtual bool CanCloseDialog() => true;

        /// <summary>
        /// Called when the dialog is closed. Override to perform cleanup operations.
        /// Always calls Dispose to ensure proper resource cleanup.
        /// </summary>
        public virtual void OnDialogClosed()
        {
            // Ensure disposal happens when dialog closes
            Dispose();
        }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (parameters.TryGetValue("Title", out string title))
            {
                Title = title;
            }
        }

        protected virtual void CloseDialog(ButtonResult result)
        {
            // DialogCloseListener is a value type in Prism, safe to invoke directly
            RequestClose.Invoke(new DialogResult(result));
        }

        /// <summary>
        /// Disposes resources used by this dialog ViewModel.
        /// Override DisposeCore for custom cleanup logic.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources. Override DisposeCore for custom cleanup.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Call virtual cleanup method for derived classes
                DisposeCore();
            }

            _disposed = true;
        }

        /// <summary>
        /// Override this method in derived classes to perform custom cleanup.
        /// This is called automatically during disposal.
        /// </summary>
        protected virtual void DisposeCore()
        {
            // Derived classes override this to cleanup their resources
        }

        /// <summary>
        /// Throws ObjectDisposedException if the object has been disposed.
        /// Call this at the start of methods that shouldn't run after disposal.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
