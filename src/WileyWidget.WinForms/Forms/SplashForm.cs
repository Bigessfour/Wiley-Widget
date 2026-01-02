using Serilog;
using System;
using System.Threading;
using System.Windows.Forms;
using Action = System.Action;

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
        private readonly ManualResetEventSlim _uiReady = new(false);
        private readonly CancellationTokenSource _cts = new();
        private Thread? _uiThread;

        private Form? _form;
        private Label? _messageLabel;
        private ProgressBar? _progressBar;

        public event EventHandler<SplashProgressChangedEventArgs>? ProgressChanged;

        public SplashForm()
        {
            Log.Debug("[SPLASH] SplashForm constructor started");

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
            Log.Information("[SPLASH] UI thread initialized (ready signal set)");
        }

        public void InvokeOnUiThread(Action action)
        {
            if (action == null) return;

            if (_isHeadless)
            {
                try { action(); }
                catch (Exception ex) { Log.Debug(ex, "[SPLASH] Headless action failed (non-critical)"); }
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
            if (_isHeadless || _form == null || _cts.IsCancellationRequested) return;

            try
            {
                if (_form.InvokeRequired)
                {
                    _form.BeginInvoke(new Action(() => ReportInternal(progress, message, isIndeterminate)));
                }
                else
                {
                    ReportInternal(progress, message, isIndeterminate);
                }
            }
            catch (ObjectDisposedException)
            {
                // Swallow - form already disposed
            }
        }

        private void ReportInternal(double progress, string message, bool isIndeterminate)
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
                return;
            }

            if (_cts.IsCancellationRequested) return;

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
                        _progressBar.Style = ProgressBarStyle.Marquee;
                        _progressBar.MarqueeAnimationSpeed = 30;
                    }
                    else
                    {
                        _progressBar.Style = ProgressBarStyle.Continuous;
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
            _cts.Cancel(); // Stop further updates
            // ...rest of completion logic (fade/hide form)...
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
                    Text = "Wiley Widget - Loading...",
                    ControlBox = false
                };

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
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                _progressBar = new ProgressBar
                {
                    Dock = DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Style = ProgressBarStyle.Continuous
                };

                layout.Controls.Add(_messageLabel, 0, 0);
                layout.Controls.Add(_progressBar, 0, 1);
                _form.Controls.Add(layout);

                _form.Shown += (_, _) =>
                {
                    try { _uiReady.Set(); }
                    catch (Exception ex) { Log.Debug(ex, "[SPLASH] Failed to set _uiReady event"); }
                    Log.Information("[SPLASH] Splash form shown (Size={Width}x{Height})", _form.Width, _form.Height);
                };

                _form.FormClosed += (_, _) =>
                {
                    try { _cts.Cancel(); }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "[SPLASH] Failed to cancel CTS on form close");
                    }
                    try { Application.ExitThread(); }
                    catch (Exception ex) { Log.Debug(ex, "[SPLASH] Failed to exit thread on form close"); }
                    Log.Information("[SPLASH] Splash form closed");
                };

                Application.Run(_form);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SPLASH] Splash UI thread failed");
                try { _uiReady.Set(); }
                catch (Exception setEx) { Log.Debug(setEx, "[SPLASH] Failed to set _uiReady after thread failure"); }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _form?.Dispose();
        }
    }
}
