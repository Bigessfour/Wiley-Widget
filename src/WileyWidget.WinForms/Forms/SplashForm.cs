using Serilog;
using System;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
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
    /// Lightweight splash screen for startup progress running on the primary UI thread.
    /// </summary>
    internal sealed class SplashForm : IDisposable
    {
        private readonly object _disposeLock = new();
        private readonly bool _isHeadless;
        private bool _disposed;
        private bool _closed;

        private Form? _form;
        private Label? _messageLabel;
        private ProgressBarAdv? _progressBar;

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

            InitializeForm();
        }

        private void InitializeForm()
        {
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

            _progressBar = new ProgressBarAdv
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                ProgressStyle = ProgressBarStyles.Tube
            };

            layout.Controls.Add(_messageLabel, 0, 0);
            layout.Controls.Add(_progressBar, 0, 1);
            _form.Controls.Add(layout);

            _form.Shown += (_, _) => Log.Information("[SPLASH] Splash form shown (Size={Width}x{Height})", _form.Width, _form.Height);
            _form.FormClosed += (_, _) => _closed = true;
        }

        public void ShowSplash()
        {
            if (_isHeadless || _disposed) return;

            var form = _form;
            if (form == null || form.IsDisposed) return;

            form.Show();
            form.BringToFront();
            form.Refresh();
            Application.DoEvents();
        }

        /// <summary>
        /// Updates the splash UI with progress percentage. Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="progress">Progress value from 0.0 to 1.0</param>
        /// <param name="message">Progress message to display</param>
        /// <param name="isIndeterminate">If true, shows marquee-style indeterminate progress</param>
        public void Report(double progress, string message, bool isIndeterminate = false)
        {
            if (_disposed) return;

            try
            {
                ProgressChanged?.Invoke(this, new SplashProgressChangedEventArgs(progress, message, isIndeterminate));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SPLASH] ProgressChanged event invocation failed");
                throw;
            }

            if (_isHeadless)
            {
                if (isIndeterminate)
                    Log.Debug("{Message} (indeterminate)", message);
                else
                    Log.Debug("{Message} ({Percent}%)", message, (int)(progress * 100));
                return;
            }

            UpdateUi(progress, message ?? string.Empty, isIndeterminate);
        }

        private void UpdateUi(double progress, string message, bool isIndeterminate)
        {
            var form = _form;
            if (form == null || form.IsDisposed || _closed) return;

            void Apply()
            {
                if (_messageLabel != null && !_messageLabel.IsDisposed)
                {
                    _messageLabel.Text = message;
                }

                if (_progressBar != null && !_progressBar.IsDisposed)
                {
                    if (isIndeterminate)
                    {
                        _progressBar.ProgressStyle = ProgressBarStyles.WaitingGradient;
                    }
                    else
                    {
                        _progressBar.ProgressStyle = ProgressBarStyles.Tube;
                        var percent = Math.Max(0, Math.Min(100, (int)Math.Round(progress * 100.0)));
                        _progressBar.Value = percent;
                    }
                }
            }

            if (form.InvokeRequired)
            {
                form.Invoke((Action)(() => Apply()));
            }
            else
            {
                Apply();
            }

            Application.DoEvents();
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

            Report(1.0, finalMessage ?? "Ready", isIndeterminate: false);
            CloseSplash();
        }

        public void CloseSplash()
        {
            if (_isHeadless) return;

            lock (_disposeLock)
            {
                if (_disposed || _closed) return;
                _closed = true;

                var form = _form;
                if (form != null && !form.IsDisposed)
                {
                    form.Hide();
                    form.Close();
                    form.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;
                CloseSplash();
                _messageLabel = null;
                _progressBar = null;
                _form = null;
            }
        }
    }
}
