#nullable enable

using Prism.Events;
using WileyWidget.Services;

namespace WileyWidget.ViewModels.Messages
{
    /// <summary>
    /// Event published by QuickBooksModule.OnInitialized() when the real IQuickBooksService
    /// implementation is registered and ready to use.
    ///
    /// LazyQuickBooksService subscribes to this event to swap from stub to real implementation.
    ///
    /// Pattern: Service swap pattern using EventAggregator for late-binding service resolution.
    /// Based on Prism-Samples-Wpf patterns for module-specific service initialization.
    ///
    /// Reference: https://github.com/PrismLibrary/Prism-Samples-Wpf
    /// </summary>
    public class QuickBooksServiceReadyEvent : PubSubEvent<IQuickBooksService>
    {
    }
}
