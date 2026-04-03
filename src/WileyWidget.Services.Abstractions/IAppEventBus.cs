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

    /// <summary>
    /// Event published when a QuickBooks Desktop import completes successfully.
    /// </summary>
    public record QuickBooksDesktopImportCompletedEvent
    {
        public string FilePath { get; init; }
        public string? ImportEntityType { get; init; }
        public int RecordsImported { get; init; }
        public int RecordsUpdated { get; init; }
        public int RecordsSkipped { get; init; }
        public TimeSpan Duration { get; init; }

        public QuickBooksDesktopImportCompletedEvent(
            string filePath,
            string? importEntityType,
            int recordsImported,
            int recordsUpdated,
            int recordsSkipped,
            TimeSpan duration)
        {
            FilePath = filePath;
            ImportEntityType = importEntityType;
            RecordsImported = recordsImported;
            RecordsUpdated = recordsUpdated;
            RecordsSkipped = recordsSkipped;
            Duration = duration;
        }
    }
}
