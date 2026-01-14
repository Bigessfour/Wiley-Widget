using System;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Simple application event bus for cross-scope notifications.
    /// </summary>
    public interface IAppEventBus
    {
        void Publish<TEvent>(TEvent evt);
        void Subscribe<TEvent>(Action<TEvent> handler);
        void Unsubscribe<TEvent>(Action<TEvent> handler);
    }

    /// <summary>
    /// Event published when QuickBooks fiscal-year actuals have been applied to BudgetEntry rows.
    /// </summary>
    public record BudgetActualsUpdatedEvent
    {
        public int FiscalYear { get; init; }
        public int UpdatedCount { get; init; }

        public BudgetActualsUpdatedEvent(int fiscalYear, int updatedCount) => (FiscalYear, UpdatedCount) = (fiscalYear, updatedCount);
    }
}
