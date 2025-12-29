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

            // Don't block startup waiting for splash; just wait briefly so early reports can be displayed.
            _uiReady.Wait(TimeSpan.FromMilliseconds(500));
        }

        public void InvokeOnUiThread(Action action)
        {
            if (action == null) return;

            if (_isHeadless)
            {
                try { action(); } catch { }
                return;
            }

            var form = _form;
            if (form == null || form.IsDisposed) return;

            try
            {
                if (form.InvokeRequired) form.BeginInvoke((Action)action);
                else action();
            }
            catch { }
        }

        /// <summary>
        /// Updates the splash UI. Must be invoked on the splash UI thread.
        /// Use <see cref="InvokeOnUiThread"/> from other threads.
        /// </summary>
        public void Report(double progress, string message, bool isIndeterminate = false)
        {
            ProgressChanged?.Invoke(this, new SplashProgressChangedEventArgs(progress, message, isIndeterminate));

            if (_isHeadless)
            {
                try { Log.Debug("{Message} ({Percent}%)", message, (int)(progress * 100)); } catch { }
                return;
            }

            if (_cts.IsCancellationRequested) return;
            var form = _form;
            if (form == null || form.IsDisposed) return;

            if (form.InvokeRequired)
                throw new InvalidOperationException("SplashForm.Report must be invoked on the splash UI thread. Use InvokeOnUiThread().");

            try
            {
                if (_messageLabel != null) _messageLabel.Text = message ?? string.Empty;

                if (_progressBar != null)
                {
                    if (isIndeterminate)
                    {
                        _progressBar.Style = ProgressBarStyle.Marquee;
                        _progressBar.MarqueeAnimationSpeed = 30;
                    }
                    else
                    {
                        _progressBar.Style = ProgressBarStyle.Continuous;
                        var percent = (int)Math.Round(progress * 100.0);
                        percent = Math.Max(0, Math.Min(100, percent));
                        _progressBar.Value = percent;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Finalizes the splash UI. Must be invoked on the splash UI thread.
        /// Use <see cref="InvokeOnUiThread"/> from other threads.
        /// </summary>
        public void Complete(string finalMessage)
        {
            _cts.Cancel();

            if (_isHeadless)
            {
                if (!string.IsNullOrEmpty(finalMessage)) Log.Debug(finalMessage);
                return;
            }

            var form = _form;
            if (form == null || form.IsDisposed) return;

            if (form.InvokeRequired)
                throw new InvalidOperationException("SplashForm.Complete must be invoked on the splash UI thread. Use InvokeOnUiThread().");

            try
            {
                if (_messageLabel != null) _messageLabel.Text = finalMessage ?? string.Empty;
                if (_progressBar != null)
                {
                    _progressBar.Style = ProgressBarStyle.Continuous;
                    _progressBar.Value = _progressBar.Maximum;
                }
            }
            catch { }
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
                    try { _uiReady.Set(); } catch { }
                };

                _form.FormClosed += (_, _) =>
                {
                    try { _cts.Cancel(); } catch { }
                    try { Application.ExitThread(); } catch { }
                };

                Application.Run(_form);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SPLASH] Splash UI thread failed");
                try { _uiReady.Set(); } catch { }
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _cts.Dispose();

                var form = _form;
                if (form != null && !form.IsDisposed)
                {
                    try
                    {
                        if (form.InvokeRequired) form.BeginInvoke((Action)(() => form.Close()));
                        else form.Close();
                    }
                    catch { }
                }

                try
                {
                    if (_uiThread != null && _uiThread.IsAlive)
                        _uiThread.Join(TimeSpan.FromSeconds(2));
                }
                catch { }
            }
            catch { }
        }
    }
}
