#nullable enable

using Prism.Events;

namespace WileyWidget.ViewModels.Messages;

/// <summary>
/// Message sent when an enterprise is changed (created, updated, deleted)
/// Used for cross-ViewModel communication via Prism EventAggregator
/// </summary>
public class EnterpriseChangedMessage : PubSubEvent<EnterpriseChangedMessage>
{
    public int EnterpriseId { get; set; }
    public string EnterpriseName { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when a budget is updated
/// </summary>
public class BudgetUpdatedMessage : PubSubEvent<BudgetUpdatedMessage>
{
    public string Context { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when dashboard data has been loaded successfully
/// Used for cross-ViewModel communication to notify other components
/// </summary>
public class DataLoadedEvent : PubSubEvent<DataLoadedEvent>
{
    public string ViewModelName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when data refresh is needed
/// </summary>
public class RefreshDataMessage : PubSubEvent<RefreshDataMessage>
{
    public string ViewName { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent to navigate to a specific view
/// </summary>
public class NavigationMessage : PubSubEvent<NavigationMessage>
{
    public string TargetView { get; set; } = string.Empty;
    public object? Parameter { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when grouping should be changed in the data grid
/// </summary>
public class GroupingMessage : PubSubEvent<GroupingMessage>
{
    public GroupingOperation Operation { get; set; }
    public string? ColumnName { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when a navigation error occurs
/// Used for centralized error handling and logging via Prism EventAggregator
/// </summary>
public class NavigationErrorEvent : PubSubEvent<NavigationErrorEvent>
{
    public string RegionName { get; set; } = string.Empty;
    public string TargetView { get; set; } = string.Empty;
    public Exception? Error { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when a general application error occurs
/// Used for centralized error handling and logging via Prism EventAggregator
/// </summary>
public class GeneralErrorEvent : PubSubEvent<GeneralErrorEvent>
{
    public string Source { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public Exception? Error { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsHandled { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when municipal accounts have been loaded/ seeded
/// Used for cross-ViewModel communication to notify dashboard refresh
/// </summary>
public class AccountsLoadedEvent : PubSubEvent<AccountsLoadedEvent>
{
    public int AccountCount { get; set; }
    public string LoadType { get; set; } = string.Empty; // "seeded", "loaded", "synced"
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Type of change made to an entity
/// </summary>
public enum ChangeType
{
    Created,
    Updated,
    Deleted,
    Restored
}

/// <summary>
/// Type of grouping operation
/// </summary>
public enum GroupingOperation
{
    Clear,
    GroupByColumn,
    AddGroupByColumn
}
