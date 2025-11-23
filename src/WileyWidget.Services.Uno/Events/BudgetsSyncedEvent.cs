namespace WileyWidget.Services.Events
{
    /// <summary>
    /// Message published when budgets are successfully synced to QuickBooks Online.
    /// Payload is the count of budgets synced.
    /// Subscribers (e.g., SettingsView) can refresh their UI grids upon receiving this message.
    /// </summary>
    public class BudgetsSyncedMessage
    {
        public int Count { get; }
        
        public BudgetsSyncedMessage(int count)
        {
            Count = count;
        }
    }
}
