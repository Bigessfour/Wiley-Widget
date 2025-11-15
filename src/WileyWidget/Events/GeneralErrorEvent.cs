using Prism.Events;

namespace WileyWidget.Events
{
    /// <summary>
    /// Event for general application errors.
    /// </summary>
    public class GeneralErrorEvent : PubSubEvent<GeneralErrorEventArgs>
    {
    }

    /// <summary>
    /// Arguments for general error events.
    /// </summary>
    public class GeneralErrorEventArgs
    {
        public bool IsHandled { get; set; }
        public Exception Error { get; set; }
        public string Source { get; set; }
        public string Operation { get; set; }
        public string ErrorMessage { get; set; }
    }
}