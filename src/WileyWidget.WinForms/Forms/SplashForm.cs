using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Themes;
using Timer = System.Windows.Forms.Timer;
using Action = System.Action;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Production-grade splash screen with animations, progress reporting, and polished UX.
    /// Implements IStartupProgressReporter for integration with startup sequence.
    /// Features: Fade in/out with cubic easing, smooth progress bar animations, minimum display time.
    /// CRITICAL: NO custom colors - SfSkinManager controls all theme and color decisions.
    /// Reference: https://help.syncfusion.com/windowsforms/splash-panel/overview
    /// </summary>
    internal sealed class SplashForm : SfForm, IStartupProgressReporter
    {
        private readonly ILogger<SplashForm>? _logger;
        private Panel? _contentPanel;
        private Label? _titleLabel;
        private Label? _subtitleLabel;
        private Label? _loadingLabel;
        private ProgressBarAdv? _progressBar;
        private Label? _versionLabel;
        private PictureBox? _logoBox;

        private readonly Timer _fadeInTimer;
        private readonly Timer _fadeOutTimer;
        private readonly Timer _minimumDisplayTimer;

        private bool _isClosing;
        private bool _canClose;
        private DateTime _showTime;
        private int _fadeStep;

        private const int FADE_DURATION_MS = 400; // Increased to 400ms for smoother appearance
        private const int FADE_TIMER_INTERVAL_MS = 15; // ~60 FPS
        private const int MINIMUM_DISPLAY_MS = 1500; // Minimum 1.5 seconds display
        private const int FADE_STEPS = FADE_DURATION_MS / FADE_TIMER_INTERVAL_MS;

        public SplashForm() : this(null)
        {
        }

        public SplashForm(ILogger<SplashForm>? logger)
        {
            _logger = logger;
            _logger?.LogDebug("SplashForm constructor started");

            // Initialize timers first
            _fadeInTimer = new Timer { Interval = FADE_TIMER_INTERVAL_MS };
            _fadeOutTimer = new Timer { Interval = FADE_TIMER_INTERVAL_MS };
            _minimumDisplayTimer = new Timer { Interval = MINIMUM_DISPLAY_MS, Enabled = false };

            InitializeForm();
            InitializeControls();
            ApplySfSkinManagerTheme(); // Apply theme AFTER controls are created so cascade works
            WireEvents();

            _logger?.LogInformation("SplashForm initialized successfully");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _logger?.LogInformation("SplashForm loaded");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _logger?.LogInformation("SplashForm closed");
        }

        private void InitializeForm()
        {
            Text = "Wiley Widget - Loading";
            Size = new Size(700, 450); // Larger for better visibility
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true; // Smooth rendering
            Opacity = 0; // Start invisible for fade-in

            // Rounded corners (Windows 10+ only)
            try
            {
                if (Environment.OSVersion.Version.Major >= 10)
                {
                    Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, Width, Height, 20, 20)
                    );
                }
            }
            catch
            {
                // Graceful fallback - square corners on older Windows
            }
        }

        // Win32 API for rounded corners
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        private void ApplySfSkinManagerTheme()
        {
            // CRITICAL: Use SfSkinManager for ALL theming - no custom colors
            // This ensures consistent theme across splash screen and main application
            ThemeColors.ApplyTheme(this, ThemeColors.DefaultTheme);
        }

        private void InitializeControls()
        {
            // Content panel to hold all controls
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_contentPanel);

            // Logo/branding image
            _logoBox = new PictureBox
            {
                Size = new Size(120, 120),
                Location = new Point((Width - 120) / 2, 60),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // Try to load logo from embedded resources
            try
            {
                var logoStream = typeof(SplashForm).Assembly.GetManifestResourceStream(
                    "WileyWidget.WinForms.Assets.logo.png");
                if (logoStream != null)
                {
                    _logoBox.Image = Image.FromStream(logoStream);
                }
                else
                {
                    _logoBox.Visible = false; // Hide if no logo found
                }
            }
            catch
            {
                // Fallback: Hide logo if loading fails
                _logoBox.Visible = false;
            }

            // Title label with modern typography
            _titleLabel = new Label
            {
                Text = "Wiley Widget",
                Font = new Font("Segoe UI Light", 36F, FontStyle.Regular, GraphicsUnit.Point),
                AutoSize = false,
                Size = new Size(600, 70),
                Location = new Point((Width - 600) / 2, 190),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Subtitle/tagline
            _subtitleLabel = new Label
            {
                Text = "Municipal Finance Management System",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point),
                AutoSize = false,
                Size = new Size(600, 30),
                Location = new Point((Width - 600) / 2, 260),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Loading status message
            _loadingLabel = new Label
            {
                Text = "Initializing application...",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                AutoSize = false,
                Size = new Size(600, 30),
                Location = new Point((Width - 600) / 2, 310),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Modern progress bar (Syncfusion ProgressBarAdv)
            _progressBar = new ProgressBarAdv
            {
                ProgressStyle = ProgressBarStyles.Tube, // Modern tube style
                BackSegments = false,
                SegmentWidth = 12,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Size = new Size(500, 20),
                Location = new Point((Width - 500) / 2, 360),
                BackgroundStyle = Syncfusion.Windows.Forms.Tools.ProgressBarBackgroundStyles.VerticalGradient
            };

            // Version label (bottom-right corner)
            _versionLabel = new Label
            {
                Text = $"Version {GetApplicationVersion()}",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point),
                AutoSize = true,
                Location = new Point(Width - 120, Height - 30),
                TextAlign = ContentAlignment.MiddleRight
            };

            // Add controls to content panel in Z-order (back to front)
            // SfSkinManager theme will cascade to all these controls automatically
            _contentPanel.Controls.Add(_versionLabel);
            _contentPanel.Controls.Add(_progressBar);
            _contentPanel.Controls.Add(_loadingLabel);
            _contentPanel.Controls.Add(_subtitleLabel);
            _contentPanel.Controls.Add(_titleLabel);
            _contentPanel.Controls.Add(_logoBox);
        }

        private void WireEvents()
        {
            // Form events
            Load += OnFormLoad;
            Shown += OnFormShown;
            FormClosing += OnFormClosing;

            // Timer events
            _fadeInTimer.Tick += OnFadeInTimerTick;
            _fadeOutTimer.Tick += OnFadeOutTimerTick;
            _minimumDisplayTimer.Tick += OnMinimumDisplayTimerTick;
        }

        #region Form Event Handlers

        private void OnFormLoad(object? sender, EventArgs e)
        {
            // Form is loaded but not visible yet
            _showTime = DateTime.UtcNow;
            Serilog.Log.Debug("SplashForm: Load event fired");
        }

        private void OnFormShown(object? sender, EventArgs e)
        {
            // Start fade-in animation when form is shown
            StartFadeIn();

            // Start minimum display timer
            _minimumDisplayTimer.Start();

            Serilog.Log.Debug("SplashForm: Shown event fired - starting fade-in animation");
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_isClosing && !_canClose)
            {
                // Cancel close and trigger fade-out animation
                e.Cancel = true;
                StartFadeOut();
            }
        }

        #endregion

        #region Animation Event Handlers

        private void OnFadeInTimerTick(object? sender, EventArgs e)
        {
            _fadeStep++;
            var progress = Math.Min(1.0, (double)_fadeStep / FADE_STEPS);

            // Ease-out cubic: y = 1 - (1-x)³ for smooth deceleration
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            Opacity = easedProgress;

            if (progress >= 1.0)
            {
                _fadeInTimer.Stop();
                _fadeStep = 0;
                Serilog.Log.Debug("SplashForm: Fade-in animation completed");
            }
        }

        private void OnFadeOutTimerTick(object? sender, EventArgs e)
        {
            _fadeStep++;
            var progress = Math.Min(1.0, (double)_fadeStep / FADE_STEPS);

            // Ease-in cubic: y = x³ for smooth acceleration
            var easedProgress = Math.Pow(progress, 3);
            Opacity = 1.0 - easedProgress;

            if (progress >= 1.0)
            {
                _fadeOutTimer.Stop();
                _fadeStep = 0;
                _isClosing = true;
                _canClose = true;
                Serilog.Log.Debug("SplashForm: Fade-out animation completed - closing form");
                Close(); // Now actually close
            }
        }

        private void OnMinimumDisplayTimerTick(object? sender, EventArgs e)
        {
            _minimumDisplayTimer.Stop();
            _canClose = true;
        }

        #endregion

        #region Animation Control

        private void StartFadeIn()
        {
            _fadeStep = 0;
            _fadeInTimer.Start();
        }

        private void StartFadeOut()
        {
            // Check minimum display time
            var elapsed = (DateTime.UtcNow - _showTime).TotalMilliseconds;
            if (elapsed < MINIMUM_DISPLAY_MS)
            {
                // Wait for minimum display time before fading out
                var remainingDelay = MINIMUM_DISPLAY_MS - (int)elapsed;
                var delayTimer = new Timer { Interval = remainingDelay };
                delayTimer.Tick += (s, e) =>
                {
                    delayTimer.Stop();
                    delayTimer.Dispose();
                    _fadeStep = 0;
                    _fadeOutTimer.Start();
                };
                delayTimer.Start();
            }
            else
            {
                _fadeStep = 0;
                _fadeOutTimer.Start();
            }
        }

        #endregion

        #region IStartupProgressReporter Implementation

        public void Report(double progress, string message, bool? isIndeterminate = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Report(progress, message, isIndeterminate)));
                return;
            }

            // Update message
            if (_loadingLabel != null && !_loadingLabel.IsDisposed)
            {
                _loadingLabel.Text = message;
            }

            // Update progress bar with smooth animation
            if (_progressBar != null && !_progressBar.IsDisposed)
            {
                var clampedProgress = Math.Clamp((int)(progress * 100), 0, 100);
                AnimateProgressBar(_progressBar.Value, clampedProgress);
            }

            Application.DoEvents(); // Allow UI to update
        }

        public void Complete(string? finalMessage = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Complete(finalMessage)));
                return;
            }

            if (finalMessage != null && _loadingLabel != null && !_loadingLabel.IsDisposed)
            {
                _loadingLabel.Text = finalMessage;
            }

            if (_progressBar != null && !_progressBar.IsDisposed)
            {
                AnimateProgressBar(_progressBar.Value, 100);
            }

            // Brief pause before fade-out
            var completeTimer = new Timer { Interval = 300 };
            completeTimer.Tick += (s, e) =>
            {
                completeTimer.Stop();
                completeTimer.Dispose();
                if (!IsDisposed)
                {
                    StartFadeOut();
                }
            };
            completeTimer.Start();
        }

        public void AttachSplashScreen(object? splashScreen)
        {
            // No-op: This form IS the splash screen
        }

        #endregion

        #region Helper Methods

        private void AnimateProgressBar(int fromValue, int toValue)
        {
            if (fromValue == toValue || _progressBar == null || _progressBar.IsDisposed)
                return;

            const int steps = 10;
            const int stepDelay = 20; // 20ms per step = 200ms total animation
            var currentStep = 0;
            var stepSize = (toValue - fromValue) / (double)steps;

            var animTimer = new Timer { Interval = stepDelay };
            animTimer.Tick += (s, e) =>
            {
                currentStep++;
                var newValue = fromValue + (int)(stepSize * currentStep);

                if (_progressBar != null && !_progressBar.IsDisposed)
                {
                    _progressBar.Value = Math.Clamp(newValue, 0, 100);
                }

                if (currentStep >= steps)
                {
                    if (_progressBar != null && !_progressBar.IsDisposed)
                    {
                        _progressBar.Value = toValue; // Ensure exact final value
                    }
                    animTimer.Stop();
                    animTimer.Dispose();
                }
            };
            animTimer.Start();
        }

        private static string GetApplicationVersion()
        {
            var version = typeof(SplashForm).Assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        #endregion

        #region Public API (Legacy Compatibility)

        /// <summary>
        /// Updates the loading message displayed on the splash screen.
        /// Preserved for backward compatibility with existing code.
        /// </summary>
        public void UpdateLoadingMessage(string message)
        {
            Report(0, message, isIndeterminate: true);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Stop and dispose timers
                _fadeInTimer?.Stop();
                _fadeInTimer?.Dispose();
                _fadeOutTimer?.Stop();
                _fadeOutTimer?.Dispose();
                _minimumDisplayTimer?.Stop();
                _minimumDisplayTimer?.Dispose();

                // Unsubscribe events
                Load -= OnFormLoad;
                Shown -= OnFormShown;
                FormClosing -= OnFormClosing;

                // Dispose controls
                _contentPanel?.Dispose();
                _logoBox?.Image?.Dispose();
                _logoBox?.Dispose();
                _titleLabel?.Dispose();
                _subtitleLabel?.Dispose();
                _loadingLabel?.Dispose();
                _progressBar?.Dispose();
                _versionLabel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
