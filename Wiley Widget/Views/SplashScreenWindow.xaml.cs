using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shapes;
using Serilog;

namespace WileyWidget;

/// <summary>
/// Enhanced splash screen window with progress tracking and professional appearance.
/// Provides visual feedback during application startup with animated elements.
/// </summary>
public partial class SplashScreenWindow : Window
{
    private readonly DispatcherTimer _animationTimer;
    private int _dotIndex = 0;
    private readonly StartupProgressTracker _progressTracker;

    /// <summary>
    /// Initializes the splash screen with animations and progress tracking.
    /// </summary>
    public SplashScreenWindow(StartupProgressTracker progressTracker = null)
    {
        InitializeComponent();

        _progressTracker = progressTracker;

        // Start fade-in animation
        BeginAnimation(OpacityProperty, null);
        var fadeIn = (Storyboard)FindResource("FadeInStoryboard");
        fadeIn.Begin();

        // Setup loading dots animation
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _animationTimer.Tick += AnimateLoadingDots;
        _animationTimer.Start();

        // Set version info
        VersionText.Text = $"Version {GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"}";

        // Subscribe to progress updates if tracker provided
        if (_progressTracker != null)
        {
            UpdateProgress(0, "Starting application...");
        }

        Log.Information("🖼️ Enhanced splash screen window initialized");
    }

    /// <summary>
    /// Updates the progress bar and status text.
    /// </summary>
    public void UpdateProgress(double progress, string statusText)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = progress;
            ProgressText.Text = $"{progress:F0}%";
            StatusText.Text = statusText;

            Log.Debug("📊 Splash screen progress: {Progress:F1}% - {Status}", progress, statusText);
        });
    }

    /// <summary>
    /// Animates the loading dots to show activity.
    /// </summary>
    private void AnimateLoadingDots(object sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Reset all dots
            Dot1.Opacity = 0.3;
            Dot2.Opacity = 0.3;
            Dot3.Opacity = 0.3;

            // Highlight current dot
            switch (_dotIndex)
            {
                case 0:
                    Dot1.Opacity = 1.0;
                    break;
                case 1:
                    Dot2.Opacity = 1.0;
                    break;
                case 2:
                    Dot3.Opacity = 1.0;
                    break;
            }

            _dotIndex = (_dotIndex + 1) % 3;
        });
    }

    /// <summary>
    /// Shows the splash screen with a fade-in effect.
    /// </summary>
    public new void Show()
    {
        base.Show();
        Log.Information("🖼️ Splash screen displayed");
    }

    /// <summary>
    /// Closes the splash screen with a fade-out effect.
    /// </summary>
    public async Task CloseAsync()
    {
        // Stop animations
        _animationTimer.Stop();

        // Fade out animation
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
        BeginAnimation(OpacityProperty, fadeOut);

        // Wait for animation to complete
        await Task.Delay(500);

        // Close the window
        Dispatcher.Invoke(() => base.Close());

        Log.Information("🖼️ Splash screen closed");
    }

    /// <summary>
    /// Handles window loaded event to ensure proper display.
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Ensure window is properly positioned and visible
        Activate();
        Focus();
    }

    /// <summary>
    /// Handles window closing to clean up resources.
    /// </summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _animationTimer.Stop();
        Log.Debug("🖼️ Splash screen cleaning up resources");
    }
}
