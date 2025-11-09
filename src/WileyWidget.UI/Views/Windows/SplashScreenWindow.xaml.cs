using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;

namespace WileyWidget.Views.Windows;

/// <summary>
/// Custom splash screen window with progress tracking
/// </summary>
public partial class SplashScreenWindow : Window, INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Event published when splash screen close is requested
    /// </summary>
    public class SplashScreenCloseRequestedEvent : PubSubEvent
    {
    }
    private string _statusText = "Initializing application...";
    private double _progressValue = 0;
    private bool _isIndeterminate = false;
    private bool _isLoading = true;
    private string _title = "Wiley Widget";
    private string _subtitle = "Enterprise Business Solutions";
    private string _versionInfo = "Version 1.0.0 • Build 2024.09.20";
    private string _systemInfo = ".NET 9.0 • Windows 11 • Enterprise Edition";
    private string _copyrightText = "© 2024 Wiley Widget Corporation. All rights reserved.";
    private DateTime _startTime;
    private double _estimatedTotalTime = 3000; // 3 seconds default
    private readonly Dictionary<double, string> _progressMessages = new()
    {
        [0] = "Initializing core systems...",
        [20] = "Loading configuration...",
        [40] = "Connecting to database...",
        [60] = "Loading user interface...",
        [80] = "Finalizing setup...",
        [100] = "Ready to launch!"
    };

    private readonly TimeSpan _maxLifetime = TimeSpan.FromSeconds(10); // Hard timeout safeguard
    private readonly System.Threading.CancellationTokenSource _lifetimeCts = new();

    private readonly ILogger<SplashScreenWindow> _logger;
    private readonly IEventAggregator _eventAggregator;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
            // Update UI on UI thread
            Dispatcher.Invoke(() =>
            {
                if (FindName("StatusTextBlock") is TextBlock tb)
                {
                    tb.Text = value;
                }
            });
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (_progressValue != value)
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));

                // Use smooth animation instead of direct setting
                AnimateProgressTo(value);
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            _isIndeterminate = value;
            OnPropertyChanged(nameof(IsIndeterminate));
            // Update UI on UI thread
            Dispatcher.Invoke(() =>
            {
                var pb = FindName("ProgressBar") as System.Windows.Controls.ProgressBar;
                if (pb != null)
                {
                    pb.IsIndeterminate = value;
                }
            });
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public new string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            _subtitle = value;
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public string VersionInfo
    {
        get => _versionInfo;
        set
        {
            _versionInfo = value;
            OnPropertyChanged(nameof(VersionInfo));
        }
    }

    public string SystemInfo
    {
        get => _systemInfo;
        set
        {
            _systemInfo = value;
            OnPropertyChanged(nameof(SystemInfo));
        }
    }

    public string CopyrightText
    {
        get => _copyrightText;
        set
        {
            _copyrightText = value;
            OnPropertyChanged(nameof(CopyrightText));
        }
    }

    public double Progress
    {
        get => ProgressValue;
        set => ProgressValue = value;
    }

    public SplashScreenWindow(ILogger<SplashScreenWindow>? logger = null, IEventAggregator? eventAggregator = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SplashScreenWindow>.Instance;
        _eventAggregator = eventAggregator ?? new EventAggregator();
        InitializeComponent();
        // DataContext will be auto-wired by Prism ViewModelLocator

        // Apply advanced Fluent Dark theme features
        ApplyAdvancedThemeFeatures();

        // Start with indeterminate progress
        IsIndeterminate = true;
        IsLoading = true;
        StatusText = "Starting Wiley Widget...";

        // Initialize progress tracking
        StartProgressTracking();

        // Handle DPI scaling
        var dpiScale = VisualTreeHelper.GetDpi(this);
        if (dpiScale.DpiScaleX > 1 || dpiScale.DpiScaleY > 1)
        {
            Width *= dpiScale.DpiScaleX;
            Height *= dpiScale.DpiScaleY;
            // Adjust font sizes proportionally
            ScaleFonts(dpiScale);
        }

        // Schedule forced close if something hangs
        // _ = EnforceLifetimeAsync();
    }

    /// <summary>
    /// Scales fonts for high-DPI displays
    /// </summary>
    private void ScaleFonts(DpiScale dpiScale)
    {
        var scale = Math.Max(dpiScale.DpiScaleX, dpiScale.DpiScaleY);

        // Find and scale text elements
        var titleTextBlock = (TextBlock)FindName("TitleTextBlock");
        if (titleTextBlock != null)
            titleTextBlock.FontSize *= scale;

        var statusTextBlock = (TextBlock)FindName("StatusTextBlock");
        if (statusTextBlock != null)
            statusTextBlock.FontSize *= scale;

        // Scale other text elements
        var progressTextBlock = (TextBlock)FindName("ProgressTextBlock");
        if (progressTextBlock != null)
            progressTextBlock.FontSize *= scale;
    }

    // Image-based background removed; XAML now provides gradient background directly.

    /// <summary>
    /// Starts progress tracking for better time estimation
    /// </summary>
    public void StartProgressTracking()
    {
        _startTime = DateTime.Now;
        IsIndeterminate = true;
        StatusText = "Preparing application...";
    }

    /// <summary>
    /// Updates progress with smooth animation and realistic timing
    /// </summary>
    public void UpdateProgress(double actualProgress)
    {
        var elapsed = DateTime.Now - _startTime;
        var estimatedTotal = elapsed.TotalMilliseconds / Math.Max(actualProgress / 100.0, 0.01);
        _estimatedTotalTime = estimatedTotal;

        // Smooth progress animation with easing
        AnimateProgressTo(actualProgress);
        IsIndeterminate = false;
    }

    /// <summary>
    /// Animates progress bar to target value with smooth easing
    /// </summary>
    private void AnimateProgressTo(double targetProgress)
    {
        // Update UI on UI thread with smooth transition
        Dispatcher.Invoke(() =>
        {
            var pb = FindName("ProgressBar") as System.Windows.Controls.ProgressBar;
            if (pb != null)
            {
                // Calculate animation duration based on progress difference
                // Larger jumps take longer to feel more realistic
                var currentValue = pb.Value;
                var difference = Math.Abs(targetProgress - currentValue);
                var durationMs = Math.Min(1500, Math.Max(300, difference * 20)); // 300ms to 1.5s

                var animation = new DoubleAnimation(
                    currentValue,
                    Math.Min(100, Math.Max(0, targetProgress)),
                    TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                pb.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, animation);
            }
        });
    }

    /// <summary>
    /// Updates the progress and status text with enhanced messaging
    /// </summary>
    public void UpdateProgress(double progress, string statusText)
    {
        ArgumentNullException.ThrowIfNull(statusText);
        // Use smart progress estimation
        UpdateProgress(progress);

        // Use contextual message if available
        if (_progressMessages.TryGetValue(Math.Round(progress / 10) * 10, out var contextualMessage))
        {
            StatusText = contextualMessage;
        }
        else
        {
            StatusText = GetEnhancedStatusText(statusText);
        }

        IsIndeterminate = progress < 10; // Keep indeterminate for first 10%
    }

    /// <summary>
    /// Gets enhanced status text with engaging messages
    /// </summary>
    private string GetEnhancedStatusText(string baseText)
    {
        var enhancements = new[]
        {
            "Loading modules...",
            "Connecting to services...",
            "Initializing components...",
            "Preparing workspace...",
            "Almost ready..."
        };

        // Add some variety to status messages
        if (baseText.Contains("Loading", StringComparison.OrdinalIgnoreCase) || baseText.Contains("Initializing", StringComparison.OrdinalIgnoreCase))
        {
            var random = new Random();
            return enhancements[random.Next(enhancements.Length)];
        }

        return baseText;
    }

    /// <summary>
    /// Completes the loading process with enhanced messaging and celebration
    /// </summary>
    public async void Complete()
    {
        ProgressValue = 100;
        StatusText = "Welcome to Wiley Widget!";
        IsIndeterminate = false;

        // Success animation sequence
        await Task.Delay(300);

        // Quick success flash
        var successFlash = new DoubleAnimation(1, 1.1, TimeSpan.FromMilliseconds(150))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var transform = RenderTransform as ScaleTransform ?? new ScaleTransform();
        if (RenderTransform == null)
        {
            RenderTransform = transform;
        }
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, successFlash);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, successFlash);

        await Task.Delay(500);
        StatusText = "Application ready for use";
    }

    /// <summary>
    /// Gets system information for display
    /// </summary>
    private string GetSystemInfo()
    {
        try
        {
            var osVersion = Environment.OSVersion;
            var framework = RuntimeInformation.FrameworkDescription;
            var processorCount = Environment.ProcessorCount;

            return $"{framework} • Windows {osVersion.Version.Major}.{osVersion.Version.Minor} • {processorCount} CPU cores";
        }
        catch
        {
            return ".NET 9.0 • Windows 11 • Enterprise Edition";
        }
    }

    /// <summary>
    /// Applies enhanced animations. Theme is inherited from SfSkinManager.ApplicationTheme set at app startup.
    /// </summary>
    /// <remarks>
    /// Per Syncfusion documentation: When ApplicationTheme is set, individual windows automatically
    /// inherit the theme without needing explicit SetTheme calls.
    /// See: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
    /// </remarks>
    private void ApplyAdvancedThemeFeatures()
    {
        // Theme is automatically inherited from SfSkinManager.ApplicationTheme (set in App.Resources.cs)
        // No SetTheme call needed per Syncfusion best practices

        // Enhanced fade-in with smooth, professional transition
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800))
        {
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        // Add subtle scale animation with professional timing
        var scaleX = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            BeginTime = TimeSpan.FromMilliseconds(100)
        };
        var scaleY = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            BeginTime = TimeSpan.FromMilliseconds(100)
        };

        var transform = new ScaleTransform();
        RenderTransform = transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

        // Add subtle glow effect animation
        var glowAnimation = new DoubleAnimation(0.1, 0.3, TimeSpan.FromMilliseconds(1000))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var glowEffect = new DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = 20,
            Color = Color.FromRgb(0, 122, 204),
            Opacity = 0.1
        };
        Effect = glowEffect;
        glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnimation);
    }

    /// <summary>
    /// Shows the splash screen and starts the loading animation
    /// </summary>
    public new void Show()
    {
        base.Show();

        // Start the loading animation if available
        var animation = (Storyboard)Resources["LoadingAnimation"];
        if (animation != null)
        {
            animation.Begin();
        }

        // Start loading dots animation
        var dotsAnimation = (Storyboard)Resources["LoadingDotsAnimation"];
        if (dotsAnimation != null)
        {
            dotsAnimation.Begin();
        }
    }

    /// <summary>
    /// Hides the splash screen with a fade effect
    /// </summary>
    public async Task HideAsync()
    {
        // Kept for backward-compatibility; prefer RequestCloseAsync()
        await RequestCloseAsync();
    }

    /// <summary>
    /// Requests splash screen close with smooth professional transition
    /// Publishes event for Bootstrapper to handle the actual closing
    /// </summary>
    public async Task RequestCloseAsync()
    {
        try
        {
            _logger.LogInformation("Requesting splash screen close with smooth transition");

            // Publish event to notify Bootstrapper that splash screen is ready to close
            _eventAggregator.GetEvent<SplashScreenCloseRequestedEvent>().Publish();

            // Perform smooth fade-out animation
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Scale down slightly for professional effect
            var scaleDown = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(100)
            };

            var transform = RenderTransform as ScaleTransform ?? new ScaleTransform();
            if (RenderTransform == null)
            {
                RenderTransform = transform;
            }

            // Start animations
            BeginAnimation(OpacityProperty, fadeOut);
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDown);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDown);

            // Wait for animations to complete
            await Task.Delay(700);

            // Publish final close event
            _eventAggregator.GetEvent<SplashScreenCloseRequestedEvent>().Publish();

            _logger.LogInformation("Splash screen close requested with smooth transition");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request splash screen close smoothly");
            // Publish event anyway for fallback handling
            _eventAggregator.GetEvent<SplashScreenCloseRequestedEvent>().Publish();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }

    /// <summary>
    /// Handles keyboard navigation for accessibility
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            // Allow user to skip splash if needed
            OnSplashCompleted();
        }
        else if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            // Speed up loading if user is impatient
            if (ProgressValue < 90)
            {
                ProgressValue = Math.Min(90, ProgressValue + 10);
            }
        }
    }

    /// <summary>
    /// Called when splash screen should be completed (either naturally or via user interaction)
    /// </summary>
    private void OnSplashCompleted()
    {
        ProgressValue = 100;
        StatusText = "Completing setup...";
        IsIndeterminate = false;

        // Trigger completion after a short delay
        Dispatcher.Invoke(async () =>
        {
            await Task.Delay(200);
            Complete();
        });
    }

    /// <summary>
    /// Allows external callers (e.g., startup phases or health checks) to push real-time status & progress.
    /// Clamps progress; ignores if already completed.
    /// </summary>
    public void SetExternalProgress(double progress, string message)
    {
        if (progress >= 100 || ProgressValue >= 100) return;
        Dispatcher.Invoke(() =>
        {
            UpdateProgress(Math.Max(0, Math.Min(99, progress)), message);
        });
    }

    private async Task EnforceLifetimeAsync()
    {
        try
        {
            await Task.Delay(_maxLifetime, _lifetimeCts.Token);
            if (ProgressValue < 100)
            {
                // Force fast completion to avoid hanging splash
                Dispatcher.Invoke(() =>
                {
                    StatusText = "Continuing (startup taking longer)...";
                    ProgressValue = 95; // near completion
                    IsIndeterminate = true;
                });
                // Attempt close soon after
                await Task.Delay(1000, _lifetimeCts.Token);
                await RequestCloseAsync();
            }
        }
        catch (TaskCanceledException) { /* normal */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        base.OnClosed(e);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_lifetimeCts.IsCancellationRequested)
            {
                try { _lifetimeCts.Cancel(); } catch { }
            }
            _lifetimeCts.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
