using System;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Business.Services
{
    /// <summary>
    /// Represents a class for auditservice.
    /// </summary>
    public class AuditService
    {
        public AuditService() { }
        /// <summary>
        /// Performs logaudit. Parameters: user, action, entity, null.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="null">The null.</param>

        public void LogAudit(string user, string action, string entity, string? details = null)
        {
            Log.Information("AUDIT: User={User}, Action={Action}, Entity={Entity}, Details={Details}",
                user, action, entity, details ?? "N/A");
        }
        /// <summary>
        /// Performs logfinancialoperation. Parameters: user, operation, amount, account.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="account">The account.</param>

        public void LogFinancialOperation(string user, string operation, decimal amount, string account)
        {
            LogAudit(user, operation, "Financial", $"Amount: {amount}, Account: {account}");
        }
    }
}
