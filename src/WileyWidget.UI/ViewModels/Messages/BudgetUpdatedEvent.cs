using Prism.Events;

namespace WileyWidget.ViewModels.Messages;

/// <summary>
/// Event published when budget data is updated
/// </summary>
public class BudgetUpdatedEvent : PubSubEvent<BudgetUpdatedEventArgs>
{
}

/// <summary>
/// Arguments for the BudgetUpdatedEvent
/// </summary>
public class BudgetUpdatedEventArgs
{
    public string? BudgetId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
