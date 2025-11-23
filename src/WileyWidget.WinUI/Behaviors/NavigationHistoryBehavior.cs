using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.Behaviors
{
    /// <summary>
    /// Region behavior that maintains navigation history for back/forward functionality
    /// </summary>
    public class NavigationHistoryBehavior : RegionBehavior
    {
        public const string BehaviorKey = "NavigationHistory";

        private readonly ILogger<NavigationHistoryBehavior> _logger;
        private readonly Stack<Uri> _backStack = new();
        private readonly Stack<Uri> _forwardStack = new();

        public NavigationHistoryBehavior(ILogger<NavigationHistoryBehavior> logger)
        {
            _logger = logger;
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigated += NavigationService_Navigated;
            _logger.LogDebug("NavigationHistoryBehavior attached to region '{RegionName}'", Region.Name);
        }

        private void NavigationService_Navigated(object? sender, RegionNavigationEventArgs e)
        {
            // Add current navigation to back stack
            if (Region.NavigationService.Journal?.CurrentEntry?.Uri is { } uri)
            {
                _backStack.Push(uri);
                _forwardStack.Clear(); // Clear forward stack when navigating to new location

                _logger.LogDebug("Navigation history updated for region '{RegionName}': Back={BackCount}, Forward={ForwardCount}",
                    Region.Name, _backStack.Count, _forwardStack.Count);
            }
        }

        public bool CanGoBack => _backStack.Count > 1; // More than 1 because current is also in stack

        public bool CanGoForward => _forwardStack.Count > 0;

        public void GoBack()
        {
            if (!CanGoBack) return;

            // Current location goes to forward stack
            var current = _backStack.Pop();
            _forwardStack.Push(current);

            // Navigate to previous location
            var previous = _backStack.Peek();
            Region.NavigationService.Journal.GoBack();

            _logger.LogInformation("Navigated back in region '{RegionName}' to '{Uri}'",
                Region.Name, previous);
        }

        public void GoForward()
        {
            if (!CanGoForward) return;

            var next = _forwardStack.Pop();
            _backStack.Push(next);

            Region.NavigationService.Journal.GoForward();

            _logger.LogInformation("Navigated forward in region '{RegionName}' to '{Uri}'",
                Region.Name, next);
        }

        public void ClearHistory()
        {
            _backStack.Clear();
            _forwardStack.Clear();
            _logger.LogDebug("Navigation history cleared for region '{RegionName}'", Region.Name);
        }
    }
}