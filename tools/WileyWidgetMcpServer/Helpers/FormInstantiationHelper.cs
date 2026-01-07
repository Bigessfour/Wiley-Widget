using System.Windows.Forms;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    /// Supports DI-style constructors with automatic mock parameter injection.
    /// </summary>
    public static Form InstantiateForm(Type formType, MainForm mockMainForm)
    {
        if (formType == null)
            throw new ArgumentNullException(nameof(formType));
        if (mockMainForm == null)
            throw new ArgumentNullException(nameof(mockMainForm));

        // Get all public constructors ordered by parameter count (prefer simpler constructors)
        var constructors = formType.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .ToArray();

        Exception? lastEx = null;
        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();

            // Try to create mock parameters for all constructor parameters
            var args = new object?[parameters.Length];
            bool canInstantiate = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                // Provide MainForm mock
                if (paramType == typeof(MainForm) || paramType.IsAssignableFrom(typeof(MainForm)))
                {
                    args[i] = mockMainForm;
                }
                // Provide ILogger<T> via Mock.Of<T>() where possible, fallback to NullLogger
                else if (paramType.IsGenericType &&
                         paramType.GetGenericTypeDefinition() == typeof(ILogger<>))
                {
                    try
                    {
                        var ofMethod = typeof(Mock).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "Of" && m.IsGenericMethod && m.GetParameters().Length == 0);
                        if (ofMethod != null)
                        {
                            args[i] = ofMethod.MakeGenericMethod(paramType).Invoke(null, null);
                        }
                        else
                        {
                            args[i] = NullLogger.Instance;
                        }
                    }
                    catch
                    {
                        args[i] = NullLogger.Instance;
                    }
                }
                // Provide null for non-generic ILogger
                else if (paramType == typeof(ILogger))
                {
                    args[i] = NullLogger.Instance;
                }
                // Provide default non-empty string for string params (file/report paths etc.)
                else if (paramType == typeof(string))
                {
                    var pname = parameters[i].Name ?? string.Empty;
                    if (pname.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pname.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pname.IndexOf("report", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            args[i] = Path.GetTempFileName();
                        }
                        catch
                        {
                            args[i] = "dummy-report-path";
                        }
                    }
                    else
                    {
                        args[i] = $"dummy-{i}";
                    }
                }
                else if (paramType == typeof(IServiceProvider))
                {
                    args[i] = MockFactory.CreateTestServiceProvider();
                }
                // Try to create mock ViewModel with constructor parameter mocking
                else if (paramType.Name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Try parameterless constructor first
                        var parameterlessCtor = paramType.GetConstructor(Type.EmptyTypes);
                        if (parameterlessCtor != null)
                        {
                            args[i] = Activator.CreateInstance(paramType);
                        }
                        else
                        {
                            // ViewModel has dependencies - create it with mocked dependencies
                            var viewModelCtor = paramType.GetConstructors().OrderBy(c => c.GetParameters().Length).FirstOrDefault();
                            if (viewModelCtor != null)
                            {
                                var viewModelParams = viewModelCtor.GetParameters();
                                var viewModelArgs = new object?[viewModelParams.Length];

                                for (int j = 0; j < viewModelParams.Length; j++)
                                {
                                    var vpType = viewModelParams[j].ParameterType;

                                    // Create NullLogger for ILogger<T>
                                    if (vpType.IsGenericType && vpType.GetGenericTypeDefinition() == typeof(ILogger<>))
                                    {
                                        var loggerType = typeof(NullLogger<>).MakeGenericType(vpType.GetGenericArguments()[0]);
                                        var instanceProperty = loggerType.GetProperty("Instance",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                        viewModelArgs[j] = instanceProperty?.GetValue(null);
                                    }
                                    // Mock other interfaces (repositories, services, etc.)
                                    else if (vpType.IsInterface)
                                    {
                                        try
                                        {
                                            // Special-case IServiceProvider to provide a test provider that returns mocks for services
                                            if (vpType == typeof(IServiceProvider))
                                            {
                                                viewModelArgs[j] = MockFactory.CreateTestServiceProvider();
                                            }
                                            else
                                            {
                                                // Prefer Mock.Of<T>() to avoid AmbiguousMatch issues
                                                var ofMethod = typeof(Mock).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                                    .FirstOrDefault(m => m.Name == "Of" && m.IsGenericMethod && m.GetParameters().Length == 0);
                                                if (ofMethod != null)
                                                {
                                                    viewModelArgs[j] = ofMethod.MakeGenericMethod(vpType).Invoke(null, null);
                                                }
                                                else
                                                {
                                                    var mockType = typeof(Mock<>).MakeGenericType(vpType);
                                                    var mock = Activator.CreateInstance(mockType);
                                                    var objectProperty = mockType.GetProperty("Object");
                                                    viewModelArgs[j] = objectProperty?.GetValue(mock);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            viewModelArgs[j] = null;
                                        }
                                    }
                                    else
                                    {
                                        viewModelArgs[j] = null;
                                    }
                                }

                                args[i] = viewModelCtor.Invoke(viewModelArgs);
                            }
                            else
                            {
                                args[i] = null;
                            }
                        }
                    }
                    catch
                    {
                        args[i] = null;
                    }
                }
                // Use Moq for other interfaces/repositories
                else if (paramType.IsInterface)
                {
                    try
                    {
                        // Prefer Mock.Of<T>() to avoid AmbiguousMatch issues
                        var ofMethod = typeof(Mock).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "Of" && m.IsGenericMethod && m.GetParameters().Length == 0);
                        if (ofMethod != null)
                        {
                            args[i] = ofMethod.MakeGenericMethod(paramType).Invoke(null, null);
                        }
                        else
                        {
                            var mockType = typeof(Mock<>).MakeGenericType(paramType);
                            var mock = Activator.CreateInstance(mockType);
                            var objectProperty = mockType.GetProperty("Object");
                            args[i] = objectProperty?.GetValue(mock);
                        }
                    }
                    catch
                    {
                        args[i] = null;
                    }
                }
                // Can't mock this parameter type
                else
                {
                    canInstantiate = false;
                    break;
                }
            }

            if (canInstantiate)
            {
                try
                {
                    return (Form)ctor.Invoke(args);
                }
                catch (Exception ex)
                {
                    // Constructor threw exception with mock parameters - record and try next constructor
                    lastEx = ex;
                    continue;
                }
            }
        }

        // No suitable constructor found
        throw new InvalidOperationException(
            $"Form type '{formType.FullName}' does not have a suitable constructor. " +
            $"Tried {constructors.Length} constructor(s) but none could be instantiated with mock parameters. Last error: {(lastEx == null ? "(no details)" : lastEx.ToString())}");
    }

    /// <summary>
    /// Safely disposes a form and its associated mock MainForm.
    /// Handles cleanup errors gracefully.
    /// </summary>
    public static void SafeDispose(Form? form, MainForm? mockMainForm)
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

        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }
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
                Syncfusion.Windows.Forms.SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            }
            catch
            {
                // Assembly already loaded, ignore
            }

            // Apply theme to form (cascades to all children)
            try
            {
                Syncfusion.Windows.Forms.SkinManager.SetVisualStyle(form, themeName);
            }
            catch
            {
                // Theme application may fail in headless mode, continue anyway
            }

            // Show/hide to trigger component initialization
            form.Show();

            // Poll for initialization with event pumping, up to waitMs
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var maxWait = TimeSpan.FromMilliseconds(Math.Max(50, waitMs));
                while (sw.Elapsed < maxWait)
                {
                    try
                    {
                        Application.DoEvents();
                        if (form.IsHandleCreated)
                        {
                            var sfControls = SyncfusionTestHelper.GetAllSyncfusionControls(form);
                            if ((sfControls != null && sfControls.Count > 0) || (form.Controls != null && form.Controls.Count > 0))
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore transient errors while querying control tree
                    }

                    // No Thread.Sleep; rely on event pumping and stopwatch for polling
                }
            }
            form.Hide();

            return true;
        }
        catch
        {
            return false;
        }
    }
}
