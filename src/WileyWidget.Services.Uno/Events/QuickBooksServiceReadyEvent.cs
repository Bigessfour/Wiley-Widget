using Prism.Events;

namespace WileyWidget.Services.Events
{
    /// <summary>
    /// Event published when the real QuickBooksService is ready.
    /// LazyQuickBooksService subscribes to this event to swap from stub to real implementation.
    /// </summary>
    public class QuickBooksServiceReadyEvent : PubSubEvent<IQuickBooksService>
    {
    }
}
