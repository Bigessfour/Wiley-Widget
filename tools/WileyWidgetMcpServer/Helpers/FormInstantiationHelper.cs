using System.Windows.Forms;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.McpServer.Helpers;

/// <summary>
/// Helper for reliable form instantiation with proper constructor handling and resource cleanup.
/// Implements best practices for headless Syncfusion WinForms testing.
/// </summary>
public static class FormInstantiationHelper
{
    /// <summary>
    /// Instantiates a form with proper constructor parameter handling.
    /// Prioritizes MainForm constructor parameter over parameterless constructor.
    /// </summary>
    public static Form InstantiateForm(Type formType, MockMainForm mockMainForm)
    {
        if (formType == null)
            throw new ArgumentNullException(nameof(formType));
        if (mockMainForm == null)
            throw new ArgumentNullException(nameof(mockMainForm));

        // Priority 1: Constructor with MainForm parameter
        var ctorWithMainForm = formType.GetConstructor(new[] { typeof(MainForm) });
        if (ctorWithMainForm != null)
        {
            return (Form)ctorWithMainForm.Invoke(new object[] { mockMainForm });
        }

        // Priority 2: Parameterless constructor (fallback)
        var parameterlessCtor = formType.GetConstructor(Type.EmptyTypes);
        if (parameterlessCtor != null)
        {
            return (Form)Activator.CreateInstance(formType)!;
        }

        // No suitable constructor found
        throw new InvalidOperationException(
            $"Form type '{formType.FullName}' does not have a suitable constructor. " +
            $"Expected: ctor(MainForm) or ctor()");
    }

    /// <summary>
    /// Safely disposes a form and its associated mock MainForm.
    /// Handles cleanup errors gracefully.
    /// </summary>
    public static void SafeDispose(Form? form, MockMainForm? mockMainForm)
    {
        if (form != null)
        {
            try
            {
                // Close and dispose on UI thread if needed
                if (form.InvokeRequired)
                {
                    form.Invoke((Action)(() =>
                    {
                        if (!form.IsDisposed)
                        {
                            form.Close();
                            form.Dispose();
                        }
                    }));
                }
                else
                {
                    if (!form.IsDisposed)
                    {
                        form.Close();
                        form.Dispose();
                    }
                }
            }
            catch
            {
                // Suppress disposal errors (common with DockingManager/Ribbon background threads)
            }

            // Suppress finalization to prevent phantom cleanup errors
            try
            {
                GC.SuppressFinalize(form);
            }
            catch
            {
                // Ignore
            }
        }

        if (mockMainForm != null)
        {
            try
            {
                if (!mockMainForm.IsDisposed)
                {
                    mockMainForm.Dispose();
                }
            }
            catch
            {
                // Suppress disposal errors
            }

            try
            {
                GC.SuppressFinalize(mockMainForm);
            }
            catch
            {
                // Ignore
            }
        }
    }

    /// <summary>
    /// Executes form instantiation and validation on an STA thread for better Syncfusion compatibility.
    /// </summary>
    public static T ExecuteOnStaThread<T>(Func<T> operation, int timeoutSeconds = 30)
    {
        T? result = default;
        Exception? thrownException = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = operation();
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var completed = thread.Join(TimeSpan.FromSeconds(timeoutSeconds));

        if (!completed)
        {
            thread.Interrupt();
            throw new TimeoutException($"Operation exceeded {timeoutSeconds} second timeout");
        }

        if (thrownException != null)
        {
            throw thrownException;
        }

        return result!;
    }

    /// <summary>
    /// Loads a form with Syncfusion theme applied.
    /// Simulates production theme initialization for accurate validation.
    /// </summary>
    public static bool LoadFormWithTheme(Form form, string themeName = "Office2019Colorful", int waitMs = 500)
    {
        ArgumentNullException.ThrowIfNull(form);
        try
        {
            // Load theme assembly (if not already loaded)
            try
            {
                Syncfusion.WinForms.Controls.SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            }
            catch
            {
                // Assembly already loaded, ignore
            }

            // Apply theme to form (cascades to all children)
            try
            {
                Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(form, themeName);
            }
            catch
            {
                // Theme application may fail in headless mode, continue anyway
            }

            // Show/hide to trigger component initialization
            form.Show();
            Application.DoEvents();
            Thread.Sleep(waitMs);
            form.Hide();

            return true;
        }
        catch
        {
            return false;
        }
    }
}
