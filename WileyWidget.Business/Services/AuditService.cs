using System;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Business.Services
{
    public class AuditService
    {
        public AuditService() { }

        public void LogAudit(string user, string action, string entity, string? details = null)
        {
            Log.Information("AUDIT: User={User}, Action={Action}, Entity={Entity}, Details={Details}",
                user, action, entity, details ?? "N/A");
        }

        public void LogFinancialOperation(string user, string operation, decimal amount, string account)
        {
            LogAudit(user, operation, "Financial", $"Amount: {amount}, Account: {account}");
        }
    }
}
