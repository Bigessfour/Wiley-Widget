using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Action = System.Action;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms
{
    internal sealed class SplashProgressChangedEventArgs : EventArgs
    {
        public SplashProgressChangedEventArgs(double progress, string message, bool isIndeterminate)
        {
            Progress = progress;
            Message = message ?? string.Empty;
            IsIndeterminate = isIndeterminate;
        }

        public double Progress { get; }
        public string Message { get; }
        public bool IsIndeterminate { get; }
    }

    /// <summary>
    /// Lightweight splash screen for startup progress.
    /// Runs in its own STA thread so startup work can proceed without blocking splash UI.
    /// </summary>
    internal sealed class SplashForm : IDisposable
    {
        private readonly bool _isHeadless;
        private readonly string _themeName;
        private readonly ManualResetEventSlim _uiReady = new(false);
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;
        private bool _ctsDisposed;
        private Thread? _uiThread;

        private Form? _form;
        private Label? _messageLabel;
        private ProgressBarAdv? _progressBar;

        public event EventHandler<SplashProgressChangedEventArgs>? ProgressChanged;

        public SplashForm(string? themeName = null)
        {
            _themeName = string.IsNullOrWhiteSpace(themeName)
                ? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme
                : themeName;
            Log.Debug("[SPLASH] SplashForm constructor started with theme: {Theme}", _themeName);

            _isHeadless = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                          || !Environment.UserInteractive;

            if (_isHeadless)
            {
                Log.Debug("[SPLASH] Headless mode - no UI splash will be shown");
                return;
            }

            _uiThread = new Thread(SplashThreadMain)
            {
                IsBackground = true,
                Name = "WileyWidget.Splash"
            };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();

            // Wait for splash UI to be ready so early progress reports can be displayed.
            // Increased timeout to ensure splash appears before first progress report.
            _uiReady.Wait(TimeSpan.FromSeconds(2));
        }

        public void InvokeOnUiThread(Action action)
        {
            if (action == null) return;

            if (_isHeadless)
            {
                // Offload to thread pool in headless mode to simulate non-blocking BeginInvoke
                _ = Task.Run(() =>
                {
                    try { action(); }
                    catch (Exception ex) { Log.Debug(ex, "[SPLASH] Headless action failed (non-critical)"); }
                });
                return;
            }

            var form = _form;
            if (form == null || form.IsDisposed) return;

            try
            {
                if (form.InvokeRequired) form.BeginInvoke((Action)action);
                else action();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[SPLASH] InvokeOnUiThread failed (form may be closing)");
            }
        }

        /// <summary>
        /// Updates the splash UI with progress percentage. Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="progress">Progress value from 0.0 to 1.0</param>
        /// <param name="message">Progress message to display</param>
        /// <param name="isIndeterminate">If true, shows marquee-style indeterminate progress</param>
        public void Report(double progress, string message, bool isIndeterminate = false)
        {
            try
            {
                ProgressChanged?.Invoke(this, new SplashProgressChangedEventArgs(progress, message, isIndeterminate));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SPLASH] ProgressChanged event invocation failed");
            }

            if (_isHeadless)
            {
                // Offload logging to thread pool in headless mode
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (isIndeterminate)
                            Log.Debug("{Message} (indeterminate)", message);
                        else
                            Log.Debug("{Message} ({Percent}%)", message, (int)(progress * 100));
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "[SPLASH] Headless logging failed");
                    }
                });
                return;
            }

            if (_cts.IsCancellationRequested) return;

            InvokeOnUiThread(() =>
            {
                var form = _form;
                if (form == null || form.IsDisposed) return;

                try
                {
                    if (_messageLabel != null && !_messageLabel.IsDisposed)
                        _messageLabel.Text = message ?? string.Empty;

                    if (_progressBar != null && !_progressBar.IsDisposed)
                    {
                        if (isIndeterminate)
                        {
                            // ProgressBarAdv doesn't support marquee, set to 50% for indeterminate
                            _progressBar.Value = 50;
                        }
                        else
                        {
                            var percent = Math.Max(0, Math.Min(100, (int)Math.Round(progress * 100.0)));
                            _progressBar.Value = percent;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Control disposed during update - expected during shutdown
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[SPLASH] Failed to update controls during Report");
                }
            });
        }

        /// <summary>
        /// Updates the splash UI with an indeterminate progress message (marquee style).
        /// Convenience overload for long-running operations where progress cannot be measured.
        /// Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="message">Progress message to display</param>
        public void Report(string message)
        {
            Report(0.0, message, isIndeterminate: true);
        }

        /// <summary>
        /// Finalizes the splash UI with completion message and full progress bar.
        /// Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="finalMessage">Final message to display before closing</param>
        public void Complete(string finalMessage)
        {
            if (_disposed) return;
            if (_ctsDisposed) return;

            var completionMessage = finalMessage ?? string.Empty;

            // Always emit a deterministic final progress update so observers
            // can reliably detect completion in both UI and headless modes.
            Report(1.0, completionMessage, isIndeterminate: false);

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                _ctsDisposed = true;
                return;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[SPLASH] CTS cancellation failed during Complete");
                _ctsDisposed = true;
                return;
            }

            if (_isHeadless)
            {
                // Offload completion log to thread pool in headless mode
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(completionMessage))
                            Log.Debug("[SPLASH] Complete: {Message}", completionMessage);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "[SPLASH] Headless Complete logging failed");
                    }
                });
                return;
            }

            InvokeOnUiThread(() =>
            {
                var form = _form;
                if (form == null || form.IsDisposed) return;

                try
                {
                    if (_messageLabel != null && !_messageLabel.IsDisposed)
                        _messageLabel.Text = completionMessage;

                    if (_progressBar != null && !_progressBar.IsDisposed)
                    {
                        _progressBar.Value = _progressBar.Maximum;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Control disposed during update - expected during shutdown
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[SPLASH] Failed to update controls during Complete");
                }
            });
        }

        private void SplashThreadMain()
        {
            try
            {
                Log.Debug("[SPLASH] Splash thread started");

                _form = new Form
                {
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    ShowInTaskbar = false,
                    Width = 520,
                    Height = 160,
                    MinimumSize = new System.Drawing.Size(520, 160),
                    Text = "Wiley Widget - Loading...",
                    ControlBox = false,
                    AccessibleName = "Startup splash"
                };

                // Apply Syncfusion theme to splash form (using theme passed from Program.Main)
                try
                {
                    WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(_form, _themeName);
                    Log.Debug("[SPLASH] Applied theme '{Theme}' to splash form", _themeName);
                }
                catch (Exception themeEx)
                {
                    Log.Debug(themeEx, "[SPLASH] Failed to apply theme '{Theme}' to splash form (non-critical)", _themeName);
                }

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(12),
                    RowCount = 2,
                    ColumnCount = 1
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

                _messageLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    Text = "Initializing...",
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    AccessibleName = "Splash status message",
                    AccessibleDescription = "Current startup progress message"
                };

                _progressBar = new ProgressBarAdv
                {
                    Dock = DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    AccessibleName = "Splash progress",
                    AccessibleDescription = "Startup progress indicator"
                };

                layout.Controls.Add(_messageLabel, 0, 0);
                layout.Controls.Add(_progressBar, 0, 1);
                _form.Controls.Add(layout);

                _form.Shown += (_, _) =>
                {
                    try { _uiReady.Set(); }
                    catch (Exception ex) { Log.Debug(ex, "[SPLASH] Failed to set _uiReady event"); }
                };

                _form.FormClosed += (_, _) =>
                {
                    if (!_ctsDisposed)
                    {
                        try { _cts.Cancel(); }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "[SPLASH] Failed to cancel CTS on form close");
                            _ctsDisposed = true;
                        }
                    }
                    try { Application.ExitThread(); }
                    catch (Exception ex) { Log.Debug(ex, "[SPLASH] Failed to exit thread on form close"); }
                };

                // Subscribe to CancellationToken to gracefully exit the UI thread loop
                // This allows Dispose() to signal shutdown without blocking
                using (_cts.Token.Register(() =>
                {
                    try
                    {
                        if (_form != null && !_form.IsDisposed && _form.InvokeRequired)
                        {
                            _form.BeginInvoke((Action)(() =>
                            {
                                try
                                {
                                    _form?.Close();
                                    Log.Debug("[SPLASH] Splash form closed via CancellationToken");
                                }
                                catch (Exception ex)
                                {
                                    Log.Debug(ex, "[SPLASH] Failed to close form via CancellationToken");
                                }
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "[SPLASH] CancellationToken callback failed");
                    }
                }))
                {
                    Application.Run(_form);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SPLASH] Splash UI thread failed");
                try { _uiReady.Set(); }
                catch (Exception setEx) { Log.Debug(setEx, "[SPLASH] Failed to set _uiReady after thread failure"); }
            }
        }

        /// <summary>
        /// Gracefully disposes the splash form and its background thread.
        /// Uses CancellationToken to signal the splash thread to exit, avoiding blocking Thread.Join calls.
        /// Does not block the UI thread - relies on CancellationToken callback to close the form asynchronously.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Signal the splash thread to exit via CancellationToken
                // The thread's CancellationToken.Register callback will close the form gracefully
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    _ctsDisposed = true;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[SPLASH] CTS cancellation failed during Dispose");
                }

                // Close the form if it's still open (redundant, but safe fallback)
                var form = _form;
                if (form != null && !form.IsDisposed)
                {
                    try
                    {
                        if (form.IsHandleCreated && !form.IsDisposed)
                        {
                            if (form.InvokeRequired)
                            {
                                // Fire-and-forget invoke to avoid blocking or re-throwing if form disposes mid-call
                                try
                                {
                                    form.BeginInvoke((Action)(() =>
                                    {
                                        if (form.IsDisposed) return;
                                        try { form.Close(); } catch { }
                                        try { Application.ExitThread(); } catch { }
                                    }));
                                }
                                catch (ObjectDisposedException) { /* Already gone */ }
                            }
                            else
                            {
                                form.Close();
                                Application.ExitThread();
                            }
                        }
                        else
                        {
                            form.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "[SPLASH] Failed to close form during dispose");
                    }
                }

                // Dispose CancellationTokenSource
                try
                {
                    _cts.Dispose();
                    _ctsDisposed = true;
                }
                catch (ObjectDisposedException)
                {
                    _ctsDisposed = true;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[SPLASH] Failed to dispose CTS");
                }

                // Background thread cleanup: join briefly to encourage timely shutdown
                // Avoid indefinite blocking to keep Dispose responsive.
                if (_uiThread != null && _uiThread.IsAlive)
                {
                    try { _uiThread.Join(TimeSpan.FromSeconds(1)); } catch { }
                }
                if (_uiThread != null && _uiThread.IsAlive)
                {
                    Log.Debug("[SPLASH] Splash thread will exit via CancellationToken callback (non-blocking)");
                    // Allow the thread callback time to run
                    // In normal cases, the thread exits within ~10-50ms
                    // This is merely logged for diagnostics if needed
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[SPLASH] Dispose failed (non-critical)");
            }
        }
    }
}
