using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    public interface IAuditService
    {
        Task AuditAsync(string eventName, object payload);

        Task<IEnumerable<AuditEntry>> GetAuditEntriesAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? actionType = null,
            string? user = null,
            int? skip = null,
            int? take = null);

        Task<int> GetAuditEntriesCountAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? actionType = null,
            string? user = null);
    }
}
