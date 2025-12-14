using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.Data.Interceptors
{
    /// <summary>
    /// Enhanced EF Core SaveChanges interceptor that provides comprehensive audit logging,
    /// user context tracking, and automatic rollback on failures.
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly ILogger<AuditInterceptor> _logger;
        private readonly IUserContext _userContext;
        private readonly IAuditService _auditService;

        public AuditInterceptor(
            ILogger<AuditInterceptor> logger,
            IUserContext userContext,
            IAuditService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);
            var context = eventData.Context;
            if (context == null)
            {
                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            // Capture changes before save
            var auditEntries = PrepareAuditEntries(context);

            try
            {
                // Log the operation start
                var userId = _userContext.UserId ?? "System";
                var userName = _userContext.DisplayName ?? "System";

                if (auditEntries.Count > 0)
                {
                    _logger.LogInformation(
                        "AuditInterceptor: Starting SaveChanges with {Count} changes by user {UserId} ({UserName})",
                        auditEntries.Count, userId, userName);

                    await _auditService.AuditAsync("SaveChanges.Started", new
                    {
                        UserId = userId,
                        UserName = userName,
                        ChangeCount = auditEntries.Count,
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Call the base implementation
                var saveResult = await base.SavingChangesAsync(eventData, result, cancellationToken);

                // If save was successful, persist audit entries
                if (saveResult.HasResult && auditEntries.Count > 0)
                {
                    await PersistAuditEntriesAsync(context, auditEntries, cancellationToken);

                    _logger.LogInformation(
                        "AuditInterceptor: SaveChanges completed successfully with {AffectedRows} rows affected",
                        saveResult.Result);

                    await _auditService.AuditAsync("SaveChanges.Completed", new
                    {
                        UserId = userId,
                        UserName = userName,
                        AffectedRows = saveResult.Result,
                        AuditEntriesCreated = auditEntries.Count,
                        Timestamp = DateTime.UtcNow
                    });
                }

                return saveResult;
            }
            catch (Exception ex)
            {
                // Log the failure
                _logger.LogError(ex, "AuditInterceptor: SaveChanges failed, initiating rollback");

                var userId = _userContext.UserId ?? "System";
                await _auditService.AuditAsync("SaveChanges.Failed", new
                {
                    UserId = userId,
                    Error = ex.Message,
                    ChangeCount = auditEntries.Count,
                    Timestamp = DateTime.UtcNow
                });

                // Attempt to rollback by clearing the change tracker
                try
                {
                    context.ChangeTracker.Clear();
                    _logger.LogInformation("AuditInterceptor: Change tracker cleared for rollback");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "AuditInterceptor: Failed to clear change tracker during rollback");
                }

                // Re-throw the original exception
                throw;
            }
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ArgumentNullException.ThrowIfNull(eventData);
            var context = eventData.Context;
            if (context == null)
            {
                return base.SavingChanges(eventData, result);
            }

            // Capture changes before save
            var auditEntries = PrepareAuditEntries(context);

            try
            {
                // Log the operation start
                var userId = _userContext.UserId ?? "System";
                var userName = _userContext.DisplayName ?? "System";

                if (auditEntries.Count > 0)
                {
                    _logger.LogInformation(
                        "AuditInterceptor: Starting SaveChanges (sync) with {Count} changes by user {UserId} ({UserName})",
                        auditEntries.Count, userId, userName);

                    _auditService.AuditAsync("SaveChanges.Started", new
                    {
                        UserId = userId,
                        UserName = userName,
                        ChangeCount = auditEntries.Count,
                        Timestamp = DateTime.UtcNow
                    }).GetAwaiter().GetResult();
                }

                // Call the base implementation
                var saveResult = base.SavingChanges(eventData, result);

                // If save was successful, persist audit entries
                if (saveResult.HasResult && auditEntries.Count > 0)
                {
                    PersistAuditEntriesAsync(context, auditEntries, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    _logger.LogInformation(
                        "AuditInterceptor: SaveChanges (sync) completed successfully with {AffectedRows} rows affected",
                        saveResult.Result);

                    _auditService.AuditAsync("SaveChanges.Completed", new
                    {
                        UserId = userId,
                        UserName = userName,
                        AffectedRows = saveResult.Result,
                        AuditEntriesCreated = auditEntries.Count,
                        Timestamp = DateTime.UtcNow
                    }).GetAwaiter().GetResult();
                }

                return saveResult;
            }
            catch (Exception ex)
            {
                // Log the failure
                _logger.LogError(ex, "AuditInterceptor: SaveChanges (sync) failed, initiating rollback");

                var userId = _userContext.UserId ?? "System";
                _auditService.AuditAsync("SaveChanges.Failed", new
                {
                    UserId = userId,
                    Error = ex.Message,
                    ChangeCount = auditEntries.Count,
                    Timestamp = DateTime.UtcNow
                }).GetAwaiter().GetResult();

                // Attempt to rollback by clearing the change tracker
                try
                {
                    context.ChangeTracker.Clear();
                    _logger.LogInformation("AuditInterceptor: Change tracker cleared for rollback");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "AuditInterceptor: Failed to clear change tracker during rollback");
                }

                // Re-throw the original exception
                throw;
            }
        }

        private List<AuditEntry> PrepareAuditEntries(DbContext context)
        {
            var auditEntries = new List<AuditEntry>();
            var userId = _userContext.UserId ?? "System";
            var userName = _userContext.DisplayName ?? "System";

            foreach (var entry in context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                           e.State == EntityState.Modified ||
                           e.State == EntityState.Deleted))
            {
                var auditEntry = new AuditEntry
                {
                    EntityType = entry.Entity.GetType().Name,
                    EntityId = GetEntityId(entry),
                    Action = entry.State.ToString(),
                    User = userId,
                    Timestamp = DateTime.UtcNow
                };

                // Capture old and new values for Modified entries
                if (entry.State == EntityState.Modified)
                {
                    var oldValues = new Dictionary<string, object?>();
                    var newValues = new Dictionary<string, object?>();
                    var changes = new List<string>();

                    foreach (var property in entry.Properties.Where(p => p.IsModified))
                    {
                        oldValues[property.Metadata.Name] = property.OriginalValue;
                        newValues[property.Metadata.Name] = property.CurrentValue;
                        changes.Add($"{property.Metadata.Name}: '{property.OriginalValue}' -> '{property.CurrentValue}'");
                    }

                    auditEntry.OldValues = JsonSerializer.Serialize(oldValues);
                    auditEntry.NewValues = JsonSerializer.Serialize(newValues);
                    auditEntry.Changes = string.Join("; ", changes);
                }
                else if (entry.State == EntityState.Added)
                {
                    var newValues = entry.Properties.ToDictionary(
                        p => p.Metadata.Name,
                        p => p.CurrentValue);
                    auditEntry.NewValues = JsonSerializer.Serialize(newValues);
                    auditEntry.Changes = "Entity created";
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var oldValues = entry.Properties.ToDictionary(
                        p => p.Metadata.Name,
                        p => p.OriginalValue);
                    auditEntry.OldValues = JsonSerializer.Serialize(oldValues);
                    auditEntry.Changes = "Entity deleted";
                }

                auditEntries.Add(auditEntry);
            }

            return auditEntries;
        }

        private async Task PersistAuditEntriesAsync(DbContext context, List<AuditEntry> auditEntries, CancellationToken cancellationToken)
        {
            try
            {
                if (context is AppDbContext appContext)
                {
                    await appContext.AuditEntries.AddRangeAsync(auditEntries, cancellationToken);
                    await appContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("AuditInterceptor: Persisted {Count} audit entries", auditEntries.Count);
                }
                else
                {
                    _logger.LogWarning("AuditInterceptor: Cannot persist audit entries - context is not AppDbContext");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuditInterceptor: Failed to persist audit entries");
                // Don't throw here - audit persistence failure shouldn't fail the original operation
            }
        }

        private int GetEntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            // Try to get the primary key value
            var key = entry.Metadata.FindPrimaryKey();
            if (key != null && key.Properties.Count == 1)
            {
                var keyProperty = key.Properties[0];
                var keyValue = entry.Property(keyProperty.Name).CurrentValue;
                if (keyValue is int intValue)
                {
                    return intValue;
                }
            }

            // Fallback - try common ID patterns
            var idProperty = entry.Properties.FirstOrDefault(p =>
                p.Metadata.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                p.Metadata.Name.Equals("EntityId", StringComparison.OrdinalIgnoreCase));

            if (idProperty != null && idProperty.CurrentValue is int idValue)
            {
                return idValue;
            }

            // If we can't determine the ID, return 0
            return 0;
        }
    }
}
