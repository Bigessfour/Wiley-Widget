using System;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Helpers
{
    public static class UIThreadHelper
    {
        /// <summary>
        /// Marshals the action to the UI thread if necessary.
        /// Safe to call even if control is disposing/closed.
        /// Uses BeginInvoke (non-blocking) — suitable for fire-and-forget UI updates.
        /// </summary>
        public static void SafeInvoke(this Control? control, Action action)
        {
            if (control == null || control.IsDisposed || action == null)
                return;

            try
            {
                if (!control.IsHandleCreated)
                    return;

                if (control.InvokeRequired)
                {
                    control.BeginInvoke((Action)(() =>
                    {
                        // Re-check after marshalling (race condition protection)
                        if (control.IsDisposed || !control.IsHandleCreated)
                            return;
                        action();
                    }));
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // Silently ignore — form is closing
            }
            catch (InvalidOperationException)
            {
                // Handle not created or cross-thread race — ignore in most UI-update scenarios
            }
        }
    }
}
