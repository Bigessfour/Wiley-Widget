namespace WileyWidget.Models;

/// <summary>
/// Sync status for QuickBooks Online integration
/// </summary>
public enum QboSyncStatus
{
    /// <summary>
    /// Not yet synced or needs sync
    /// </summary>
    Pending,

    /// <summary>
    /// Successfully synced
    /// </summary>
    Synced,

    /// <summary>
    /// Sync failed
    /// </summary>
    Failed
}
