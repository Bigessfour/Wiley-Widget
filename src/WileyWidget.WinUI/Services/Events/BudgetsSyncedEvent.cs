using Prism.Events;

namespace WileyWidget.WinUI.Services.Events
{
    /// <summary>
    /// Event published when budgets are successfully synced to QuickBooks Online.
    /// Payload is the count of budgets synced.
    /// Subscribers (e.g., SettingsView) can refresh their UI grids upon receiving this event.
    /// </summary>
    public class BudgetsSyncedEvent : PubSubEvent<int>
    {
    }
}