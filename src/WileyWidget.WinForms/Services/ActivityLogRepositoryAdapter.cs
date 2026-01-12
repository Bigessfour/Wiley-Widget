using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Adapter that implements the legacy Services.Abstractions.IActivityLogRepository
    /// by delegating to the Business layer's IActivityLogRepository.
    /// This preserves binary compatibility for consumers of the Abstractions package.
    /// </summary>
    public class ActivityLogRepositoryAdapter : WileyWidget.Services.Abstractions.IActivityLogRepository
    {
        private readonly IActivityLogRepository _businessRepository;
        private readonly ILogger<ActivityLogRepositoryAdapter> _logger;

        public ActivityLogRepositoryAdapter(IActivityLogRepository businessRepository, ILogger<ActivityLogRepositoryAdapter>? logger = null)
        {
            _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ActivityLogRepositoryAdapter>.Instance;
        }

        public async Task LogActivityAsync(string activity, string details)
        {
            var model = new ActivityLog
            {
                Activity = activity,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            await _businessRepository.LogActivityAsync(model).ConfigureAwait(false);
        }

        public async Task LogActivityAsync(WileyWidget.Services.Abstractions.ActivityLog activityLog)
        {
            if (activityLog == null) throw new ArgumentNullException(nameof(activityLog));

            var model = new ActivityLog
            {
                ActivityType = activityLog.ActivityType,
                Activity = activityLog.Activity,
                Details = activityLog.Details,
                User = activityLog.User,
                Status = activityLog.Status,
                EntityType = activityLog.EntityType,
                EntityId = activityLog.EntityId ?? string.Empty,
                Severity = activityLog.Severity,
                Timestamp = activityLog.Timestamp
            };

            await _businessRepository.LogActivityAsync(model).ConfigureAwait(false);
        }
    }
}
