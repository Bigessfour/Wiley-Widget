#nullable enable

using Prism.Events;

namespace WileyWidget.ViewModels.Messages
{
    /// <summary>
    /// Published when municipal accounts data has been updated/loaded so other modules can react.
    /// </summary>
    public class AccountsUpdatedEvent : PubSubEvent<AccountsUpdatedEvent>
    {
        public int Count { get; set; }
        public string Source { get; set; } = "repository"; // e.g., seeded, loaded, synced
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }
}
