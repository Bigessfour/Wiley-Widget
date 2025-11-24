using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Composition.SystemBackdrops;
using WileyWidget.Views;
using WileyWidget.Services;
using System;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget
{
    public sealed partial class MainWindow : Window
    {
        private readonly IServiceProvider? _serviceProvider;
        private INavigationService? _navigationService;

        public MainWindow()
        {
            this.InitializeComponent();

            _serviceProvider = App.Services;

            Log.Information("MainWindow constructor started");

            try
            {
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

                // Defer initialization until after activation
                this.Activated += OnWindowActivated;

                Log.Information("MainWindow initialized successfully (initialization deferred)");
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
            Log.Information("Window activated; performing initialization");
            
            InitializeNavigation();
            NavigateToInitialPage();
        }

        private void InitializeNavigation()
        {
            try
            {
                // Create NavigationService with ContentFrame
                var logger = _serviceProvider?.GetService<Microsoft.Extensions.Logging.ILogger<DefaultNavigationService>>();
                
                if (logger != null && _serviceProvider != null)
                {
                    _navigationService = new DefaultNavigationService(
                        ContentFrame, 
                        logger,
                        _serviceProvider);
                    
                    Log.Information("NavigationService initialized");
                }
                else
                {
                    Log.Warning("Failed to initialize NavigationService - missing dependencies");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize NavigationService");
            }
        }

        private async void NavigateToInitialPage()
        {
            if (ContentFrame == null)
            {
                Log.Error("ContentFrame is null; cannot navigate");
                ShowFallbackContent("Frame not initialized");
                return;
            }

            try
            {
                // Allow tests to opt-out of UI navigation
                var testNoNav = Environment.GetEnvironmentVariable("TEST_NO_NAV");
                if (!string.IsNullOrEmpty(testNoNav) && testNoNav == "1")
                {
                    Log.Information("Test mode detected: skipping UI navigation");
                    return;
                }

                // Navigate to BudgetOverviewPage as default
                if (_navigationService != null)
                {
                    var navigated = await _navigationService.NavigateToAsync("BudgetOverview");
                    if (navigated)
                    {
                        Log.Information("Successfully navigated to BudgetOverview");
                        
                        // Select the first nav item
                        NavView.SelectedItem = NavView.MenuItems[0];
                    }
                    else
                    {
                        Log.Warning("Navigation to BudgetOverview failed");
                        ShowFallbackContent("Navigation failed");
                    }
                }
                else
                {
                    // Fallback to direct navigation if service not available
                    Log.Warning("NavigationService not available, using direct navigation");
                    var navigated = ContentFrame.Navigate(typeof(BudgetOverviewPage));
                    
                    if (navigated)
                    {
                        Log.Information("Successfully navigated to BudgetOverviewPage (direct)");
                        NavView.SelectedItem = NavView.MenuItems[0];
                    }
                    else
                    {
                        ShowFallbackContent("Navigation failed");
                    }
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

                ContentFrame.Content = textBlock;
                Log.Information("Fallback content displayed: {Message}", message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show fallback content");
            }
        }

        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                Log.Information("Settings navigation invoked");
                if (_navigationService != null)
                {
                    await _navigationService.NavigateToAsync("Settings");
                }
                else
                {
                    ContentFrame.Navigate(typeof(SettingsPage));
                }
            }
            else if (args.InvokedItemContainer is NavigationViewItem item)
            {
                var tag = item.Tag as string;
                Log.Information("Navigation invoked: {Tag}", tag);

                if (!string.IsNullOrEmpty(tag) && _navigationService != null)
                {
                    await _navigationService.NavigateToAsync(tag);
                }
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (_navigationService?.CanGoBack == true)
            {
                _navigationService.GoBack();
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Update back button state
            NavView.IsBackEnabled = ContentFrame.CanGoBack;
            
            Log.Debug("Navigated to: {PageType}", e.SourcePageType?.Name ?? "<unknown>");
        }
    }
}
