using Prism.Events;

namespace WileyWidget.Services.Events
{
    /// <summary>
    /// Event published when the QuickBooks module is loaded.
    /// Used to trigger lazy initialization of QuickBooks services.
    /// </summary>
    public class QuickBooksModuleLoadedEvent : PubSubEvent<object>
    {
    }
}
