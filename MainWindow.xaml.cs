using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using WileyWidget.Views;
using System;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget
{
    public sealed partial class MainWindow : Window
    {
        private Frame? ContentFrame;
        private readonly IServiceProvider? _serviceProvider;

        public MainWindow()
        {
            this.InitializeComponent(); // Load XAML - this sets Content to RootGrid with ContentFrame

            _serviceProvider = App.Services;
            
            Log.Information("MainWindow constructor started");
            
            try
            {
                // Now Content should be Grid from XAML
                if (this.Content is Grid existingGrid)
                {
                    ContentFrame = existingGrid.FindName("ContentFrame") as Frame ?? throw new InvalidOperationException("ContentFrame not found in XAML");
                    Log.Debug("Using existing Grid and Frame from XAML");
                }
                else
                {
                    // Fallback if XAML still not loaded (unlikely now)
                    Log.Warning("Content is not Grid after InitializeComponent; creating programmatically");
                    var rootGrid = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                    ContentFrame = new Frame();
                    rootGrid.Children.Add(ContentFrame);
                    this.Content = rootGrid;
                }

                // Set Mica backdrop
                try
                {
                    this.SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
                    Log.Debug("Mica backdrop applied successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to apply Mica backdrop, using default");
                }

                // Defer navigation until after activation to ensure visual tree is ready
                this.Activated += OnWindowActivated;
                
                Log.Information("MainWindow initialized successfully (navigation deferred)");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error in MainWindow constructor");
                throw;
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= OnWindowActivated; // One-time
            Log.Information("Window activated; performing initial navigation");
            NavigateToInitialPage();
        }
        
        private void NavigateToInitialPage()
        {
            if (ContentFrame == null)
            {
                Log.Error("ContentFrame is null; cannot navigate");
                ShowFallbackContent("Frame not initialized");
                return;
            }

            try
            {
                // Allow tests to opt-out of UI navigation to avoid WinUI issues in headless environments
                var testNoNav = Environment.GetEnvironmentVariable("TEST_NO_NAV");
                if (!string.IsNullOrEmpty(testNoNav) && testNoNav == "1")
                {
                    Log.Information("Test mode detected: skipping UI navigation");
                    return;
                }

                // Try to navigate to BudgetOverviewPage (registered Page type)
                var pageType = typeof(BudgetOverviewPage);
                
                if (typeof(Page).IsAssignableFrom(pageType))
                {
                    Log.Information("Attempting navigation to {PageType}", pageType.Name);
                    
                    var navigated = ContentFrame.Navigate(pageType);
                    
                    if (navigated)
                    {
                        Log.Information("Successfully navigated to {PageType}", pageType.Name);
                    }
                    else
                    {
                        Log.Warning("Navigation to {PageType} returned false", pageType.Name);
                        ShowFallbackContent("Navigation failed");
                    }
                }
                else
                {
                    Log.Error("Invalid page type for navigation: {PageType}", pageType.Name);
                    ShowFallbackContent("Invalid page type");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Navigation failed in NavigateToInitialPage");
                ShowFallbackContent($"Navigation error: {ex.Message}");
            }
        }
        
        private void ShowFallbackContent(string message)
        {
            try
            {
                var textBlock = new TextBlock
                {
                    Text = $"Welcome to Wiley Widget\n\n{message}",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                
                ContentFrame!.Content = textBlock;
                Log.Information("Fallback content displayed: {Message}", message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show fallback content");
            }
        }

        private void Nav_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is Microsoft.UI.Xaml.Controls.NavigationViewItem item && ContentFrame != null)
            {
                var tag = item.Tag as string;
                if (string.Equals(tag, "BudgetOverview", StringComparison.OrdinalIgnoreCase))
                {
                    // Navigate using the Frame
                    ContentFrame.Navigate(typeof(BudgetOverviewPage));
                }
                else if (string.Equals(tag, "Dashboard", StringComparison.OrdinalIgnoreCase))
                {
                    ContentFrame.Navigate(typeof(Views.DashboardView));
                }
            }
        }
    }
}
