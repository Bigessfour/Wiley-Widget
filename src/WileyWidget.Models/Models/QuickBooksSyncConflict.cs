#nullable enable
using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a QuickBooks sync conflict that requires manual resolution.
    /// </summary>
    public class QuickBooksSyncConflict : IAuditable
    {
        public int Id { get; set; }

        /// <summary>
        /// QuickBooks invoice identifier (string because QBO uses string IDs)
        /// </summary>
        public string QuickBooksInvoiceId { get; set; } = string.Empty;

        /// <summary>
        /// Local transaction id (if any) that corresponds to the QuickBooks invoice
        /// </summary>
        public int? LocalTransactionId { get; set; }

        /// <summary>
        /// Amount as reported by QuickBooks
        /// </summary>
        public decimal RemoteAmount { get; set; }

        /// <summary>
        /// Local amount in the WileyWidget system
        /// </summary>
        public decimal LocalAmount { get; set; }

        /// <summary>
        /// Configured conflict policy in effect when the discrepancy was detected
        /// </summary>
        public QuickBooksConflictPolicy Policy { get; set; } = QuickBooksConflictPolicy.PreferQBO;

        /// <summary>
        /// Current status (Pending, Resolved, Ignored)
        /// </summary>
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Optional resolution note or action
        /// </summary>
        public string? ResolutionNote { get; set; }

        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
