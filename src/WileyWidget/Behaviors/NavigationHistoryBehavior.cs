using System.Collections.Generic;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// WPF Region behavior that maintains navigation history for back/forward functionality
    /// </summary>
    public class NavigationHistoryBehavior : RegionBehavior
    {
        public const string BehaviorKey = "NavigationHistory";

        private readonly Stack<string> _backStack = new();
        private readonly Stack<string> _forwardStack = new();

        public NavigationHistoryBehavior()
        {
        }

        protected override void OnAttach()
        {
            Log.Debug("NavigationHistoryBehavior attached to region '{RegionName}'", Region.Name);

            Region.NavigationService.Navigated += Region_NavigationService_Navigated;
        }

        private void Region_NavigationService_Navigated(object sender, RegionNavigationEventArgs e)
        {
            // Add current navigation to history
            _backStack.Push(e.Uri.ToString());
            _forwardStack.Clear(); // Clear forward stack when navigating to new location

            Log.Debug("Navigation history updated for region '{RegionName}': {Uri}", Region.Name, e.Uri);
        }

        public bool CanGoBack => _backStack.Count > 1;
        public bool CanGoForward => _forwardStack.Count > 0;

        public string? GetPreviousUri()
        {
            return CanGoBack ? _backStack.Peek() : null;
        }

        public void GoBack()
        {
            if (CanGoBack)
            {
                var currentUri = _backStack.Pop();
                _forwardStack.Push(currentUri);
                Log.Debug("Navigated back in region '{RegionName}'", Region.Name);
            }
        }

        public void GoForward()
        {
            if (CanGoForward)
            {
                var uri = _forwardStack.Pop();
                _backStack.Push(uri);
                Log.Debug("Navigated forward in region '{RegionName}'", Region.Name);
            }
        }
    }
}