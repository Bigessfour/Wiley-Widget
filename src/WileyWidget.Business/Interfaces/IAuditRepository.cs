using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for audit trail operations
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Gets audit trail entries within a date range
    /// </summary>
    Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets audit trail entries for a specific entity type
    /// </summary>
    Task<IEnumerable<AuditEntry>> GetAuditTrailForEntityAsync(string entityType, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets audit trail entries for a specific entity
    /// </summary>
    Task<IEnumerable<AuditEntry>> GetAuditTrailForEntityAsync(string entityType, int entityId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Adds a new audit entry
    /// </summary>
    Task AddAuditEntryAsync(AuditEntry auditEntry);
}