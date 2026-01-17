using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    public interface IAuditService
    {
        Task AuditAsync(string eventName, object payload, CancellationToken cancellationToken = default);

        Task<IEnumerable<AuditEntry>> GetAuditEntriesAsync(DateTime? startDate = null,
            DateTime? endDate = null,
            string? actionType = null,
            string? user = null,
            int? skip = null,
            int? take = null, CancellationToken cancellationToken = default);

        Task<int> GetAuditEntriesCountAsync(DateTime? startDate = null,
            DateTime? endDate = null,
            string? actionType = null,
            string? user = null, CancellationToken cancellationToken = default);
    }
}
