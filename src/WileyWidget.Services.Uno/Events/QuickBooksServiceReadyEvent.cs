namespace WileyWidget.Services.Events
{
    /// <summary>
    /// Message published when the real QuickBooksService is ready.
    /// LazyQuickBooksService subscribes to this message to swap from stub to real implementation.
    /// </summary>
    public class QuickBooksServiceReadyMessage
    {
        public IQuickBooksService Service { get; }
        
        public QuickBooksServiceReadyMessage(IQuickBooksService service)
        {
            Service = service;
        }
    }
}
