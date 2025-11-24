using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WileyWidget.WinUI.Services
{
    /// <summary>
    /// Navigation service interface for WinUI 3 Frame-based navigation.
    /// Replaces legacy Prism RegionManager with pure WinUI navigation.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Gets the current Frame instance used for navigation.
        /// </summary>
        Frame? CurrentFrame { get; }

        /// <summary>
        /// Navigates to a page by name with optional parameter.
        /// </summary>
        /// <param name="pageName">The page name (e.g., "BudgetOverview", "Settings")</param>
        /// <param name="parameter">Optional navigation parameter</param>
        /// <returns>True if navigation succeeded, false otherwise</returns>
        Task<bool> NavigateToAsync(string pageName, object? parameter = null);

        /// <summary>
        /// Navigates to a page by type with optional parameter.
        /// </summary>
        /// <typeparam name="TPage">The page type</typeparam>
        /// <param name="parameter">Optional navigation parameter</param>
        /// <returns>True if navigation succeeded, false otherwise</returns>
        Task<bool> NavigateToAsync<TPage>(object? parameter = null) where TPage : Page;

        /// <summary>
        /// Navigates back if possible.
        /// </summary>
        /// <returns>True if back navigation occurred, false if not possible</returns>
        bool GoBack();

        /// <summary>
        /// Checks if back navigation is possible.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Clears navigation history.
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Gets the current page content.
        /// </summary>
        object? CurrentContent { get; }
    }

    /// <summary>
    /// Default implementation of INavigationService for WinUI 3.
    /// Thread-safe, logger-enabled, with error handling.
    /// </summary>
    public class DefaultNavigationService : INavigationService
    {
        private readonly Frame _frame;
        private readonly ILogger<DefaultNavigationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public Frame? CurrentFrame => _frame;

        public bool CanGoBack => _frame?.CanGoBack ?? false;

        public object? CurrentContent => _frame?.Content;

        public DefaultNavigationService(
            Frame frame, 
            ILogger<DefaultNavigationService> logger,
            IServiceProvider serviceProvider)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _logger.LogInformation("NavigationService initialized with Frame");
        }

        public async Task<bool> NavigateToAsync(string pageName, object? parameter = null)
        {
            try
            {
                var pageType = ResolvePageType(pageName);
                if (pageType == null)
                {
                    _logger.LogWarning("Failed to resolve page type for: {PageName}", pageName);
                    return false;
                }

                return await NavigateToAsync(pageType, parameter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation to {PageName} failed", pageName);
                return false;
            }
        }

        public async Task<bool> NavigateToAsync<TPage>(object? parameter = null) where TPage : Page
        {
            return await NavigateToAsync(typeof(TPage), parameter);
        }

        private async Task<bool> NavigateToAsync(Type pageType, object? parameter = null)
        {
            if (_frame == null)
            {
                _logger.LogError("Frame is null, cannot navigate");
                return false;
            }

            try
            {
                _logger.LogInformation("Navigating to {PageType} with parameter: {Parameter}", 
                    pageType.Name, parameter?.ToString() ?? "<none>");

                // Use dispatcher to ensure UI thread
                var result = false;
                await _frame.DispatcherQueue.EnqueueAsync(() =>
                {
                    result = _frame.Navigate(pageType, parameter);
                });

                if (result)
                {
                    _logger.LogInformation("Successfully navigated to {PageType}", pageType.Name);
                }
                else
                {
                    _logger.LogWarning("Navigation to {PageType} returned false", pageType.Name);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation to {PageType} threw exception", pageType.Name);
                return false;
            }
        }

        public bool GoBack()
        {
            if (!CanGoBack)
            {
                _logger.LogDebug("Cannot go back - no history");
                return false;
            }

            try
            {
                _frame.GoBack();
                _logger.LogInformation("Navigated back to previous page");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoBack failed");
                return false;
            }
        }

        public void ClearHistory()
        {
            try
            {
                while (_frame?.CanGoBack == true)
                {
                    _frame.BackStack.Clear();
                }
                _logger.LogInformation("Navigation history cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear navigation history");
            }
        }

        private Type? ResolvePageType(string pageName)
        {
            // Map page names to types
            return pageName switch
            {
                "BudgetOverview" => typeof(Views.BudgetOverviewPage),
                "Dashboard" => typeof(Views.DashboardView),
                "Settings" => typeof(Views.SettingsPage),
                "Chart" => typeof(Views.ChartView),
                "Data" => typeof(Views.DataView),
                _ => null
            };
        }
    }

    /// <summary>
    /// Extension methods for DispatcherQueue to support async/await.
    /// </summary>
    public static class DispatcherQueueExtensions
    {
        public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}
