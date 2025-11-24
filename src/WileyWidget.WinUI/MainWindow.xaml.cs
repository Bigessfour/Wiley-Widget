// MainWindow.xaml.cs - WinUI 3 Main Window
//
// Standard WinUI 3 window with Frame-based navigation (no Prism regions)
// Uses Frame.Navigate() pattern per Microsoft guidance

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Serilog;
using WinRT.Interop;
using Microsoft.UI;

namespace WileyWidget.WinUI;

/// <summary>
/// Main window for WinUI 3 application using Frame-based navigation.
/// </summary>
public sealed partial class MainWindow : Window
{
    private AppWindow? _appWindow;

    public MainWindow()
    {
        Log.Information("[MAINWINDOW] Initializing main window");

        try
        {
            this.InitializeComponent();
            Title = "Wiley Widget - WinUI 3";

            // Set window size and position
            ConfigureWindow();

            // Show welcome content
            ShowWelcomePage();

            Log.Information("[MAINWINDOW] Main window initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MAINWINDOW] Exception during InitializeComponent - attempting minimal fallback UI");
            try
            {
                var grid = new Grid();
                var tb = new TextBlock 
                { 
                    Text = "UI failed to load - check logs", 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Center 
                };
                grid.Children.Add(tb);
                this.Content = grid;
            }
            catch (Exception inner)
            {
                Log.Error(inner, "[MAINWINDOW] Failed to apply fallback UI");
            }
        }
    }


    private void ConfigureWindow()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // Set window size: 1200x800
                _appWindow.Resize(new SizeInt32(1200, 800));

                // Center window on screen
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    var x = (workArea.Width - 1200) / 2;
                    var y = (workArea.Height - 800) / 2;
                    _appWindow.Move(new PointInt32(x, y));
                }

                Log.Information("[MAINWINDOW] Window configured: 1200x800");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[MAINWINDOW] Could not configure window size/position");
        }
    }

    private void ShowWelcomePage()
    {
        try
        {
            if (ContentFrame != null)
            {
                // Create welcome page content inline
                var page = new Page();
                var grid = new Grid { Padding = new Thickness(48) };
                
                var stack = new StackPanel 
                { 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 24
                };

                var title = new TextBlock
                {
                    Text = "🎯 Wiley Widget",
                    FontSize = 48,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var subtitle = new TextBlock
                {
                    Text = "WinUI 3 Application",
                    FontSize = 24,
                    Opacity = 0.7,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var version = new TextBlock
                {
                    Text = "Version 1.0 • .NET 9.0 • Windows App SDK 1.8",
                    FontSize = 14,
                    Opacity = 0.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 24, 0, 0)
                };

                stack.Children.Add(title);
                stack.Children.Add(subtitle);
                stack.Children.Add(version);
                grid.Children.Add(stack);
                page.Content = grid;

                ContentFrame.Content = page;
                Log.Information("[MAINWINDOW] Welcome page displayed");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MAINWINDOW] Failed to show welcome page");
        }
    }
}
