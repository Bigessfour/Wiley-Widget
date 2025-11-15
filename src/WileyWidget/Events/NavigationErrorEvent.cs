using Prism.Events;

namespace WileyWidget.Events
{
    /// <summary>
    /// Event for navigation errors in the application.
    /// </summary>
    public class NavigationErrorEvent : PubSubEvent<NavigationErrorEventArgs>
    {
    }

    /// <summary>
    /// Arguments for navigation error events.
    /// </summary>
    public class NavigationErrorEventArgs
    {
        public string RegionName { get; set; }
        public string TargetView { get; set; }
        public string ErrorMessage { get; set; }
    }
}