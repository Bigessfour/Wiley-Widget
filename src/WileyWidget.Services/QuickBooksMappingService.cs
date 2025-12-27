#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
// Intuit types referenced fully-qualified to avoid type name collisions (e.g., Task, Invoice)

namespace WileyWidget.Services;

/// <summary>
/// Service for managing QuickBooks to WileyWidget mapping configurations
/// </summary>
public class QuickBooksMappingService : IQuickBooksMappingService
{
    private readonly IQBMappingConfigurationRepository _repository;
    private readonly ILogger<QuickBooksMappingService> _logger;

    public QuickBooksMappingService(
        IQBMappingConfigurationRepository repository,
        ILogger<QuickBooksMappingService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<QBMappingConfiguration>> GetAllMappingsAsync()
    {
        try
        {
            return await _repository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all QB mapping configurations");
            throw;
        }
    }

    public async Task<QBMappingConfiguration?> GetMappingByIdAsync(int id)
    {
        try
        {
            return await _repository.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QB mapping configuration with ID {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<QBMappingConfiguration>> GetMappingsByQBEntityAsync(string entityType, string entityId)
    {
        try
        {
            return await _repository.GetByQBEntityAsync(entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QB mappings for entity {EntityType}:{EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<int?> GetBudgetEntryIdForQBEntityAsync(string entityType, string entityId)
    {
        try
        {
            var mappings = await _repository.GetByQBEntityAsync(entityType, entityId);
            var activeMapping = mappings.FirstOrDefault(m => m.IsActive);

            if (activeMapping != null)
            {
                return activeMapping.BudgetEntryId;
            }

            _logger.LogDebug("No active mapping found for QB entity {EntityType}:{EntityId}", entityType, entityId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding budget entry for QB entity {EntityType}:{EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<QBMappingConfiguration> CreateMappingAsync(QBMappingConfiguration mapping)
    {
        try
        {
            if (!await ValidateMappingAsync(mapping))
            {
                throw new InvalidOperationException("Invalid mapping configuration");
            }

            await _repository.AddAsync(mapping);
            _logger.LogInformation("Created QB mapping configuration: {EntityType}:{EntityId} -> BudgetEntry {BudgetEntryId}",
                mapping.QBEntityType, mapping.QBEntityId, mapping.BudgetEntryId);

            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating QB mapping configuration");
            throw;
        }
    }

    public async Task UpdateMappingAsync(QBMappingConfiguration mapping)
    {
        try
        {
            if (!await ValidateMappingAsync(mapping))
            {
                throw new InvalidOperationException("Invalid mapping configuration");
            }

            await _repository.UpdateAsync(mapping);
            _logger.LogInformation("Updated QB mapping configuration {Id}: {EntityType}:{EntityId} -> BudgetEntry {BudgetEntryId}",
                mapping.Id, mapping.QBEntityType, mapping.QBEntityId, mapping.BudgetEntryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating QB mapping configuration {Id}", mapping.Id);
            throw;
        }
    }

    public async Task DeleteMappingAsync(int id)
    {
        try
        {
            await _repository.DeleteAsync(id);
            _logger.LogInformation("Deleted QB mapping configuration {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting QB mapping configuration {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<QBMappingConfiguration>> GetMappingsByBudgetEntryAsync(int budgetEntryId)
    {
        try
        {
            return await _repository.GetByBudgetEntryIdAsync(budgetEntryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QB mappings for budget entry {BudgetEntryId}", budgetEntryId);
            throw;
        }
    }

    public async Task<bool> ValidateMappingAsync(QBMappingConfiguration mapping)
    {
        if (mapping == null)
            return false;

        if (string.IsNullOrWhiteSpace(mapping.QBEntityType))
            return false;

        if (string.IsNullOrWhiteSpace(mapping.QBEntityId))
            return false;

        if (mapping.BudgetEntryId <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(mapping.MappingStrategy))
            return false;

        // Check for duplicate active mappings
        var existingMappings = await GetMappingsByQBEntityAsync(mapping.QBEntityType, mapping.QBEntityId);
        var duplicate = existingMappings.FirstOrDefault(m =>
            m.Id != mapping.Id &&
            m.IsActive &&
            m.MappingStrategy == mapping.MappingStrategy);

        if (duplicate != null)
        {
            _logger.LogWarning("Duplicate active mapping found for {EntityType}:{EntityId} with strategy {Strategy}",
                mapping.QBEntityType, mapping.QBEntityId, mapping.MappingStrategy);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolve a BudgetEntryId for an Intuit Invoice using configured mappings.
    /// This centralizes mapping strategies (CustomField, Class, Department, Customer, Item, RuleBased).
    /// </summary>
    public async System.Threading.Tasks.Task<int?> ResolveBudgetEntryIdForInvoiceAsync(Intuit.Ipp.Data.Invoice invoice)
    {
        if (invoice == null) return null;

        // 1) Custom fields
        try
        {
            if (invoice.CustomField != null)
            {
                foreach (var cf in invoice.CustomField)
                {
                    var value = cf?.AnyIntuitObject?.ToString();
                    var name = cf?.Name ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var mapped = await GetBudgetEntryIdForQBEntityAsync("CustomField", value).ConfigureAwait(false);
                        if (mapped != null) return mapped;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            mapped = await GetBudgetEntryIdForQBEntityAsync($"CustomField:{name}", value).ConfigureAwait(false);
                            if (mapped != null) return mapped;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CustomField mapping attempt failed for invoice {InvoiceId}", invoice?.Id);
        }

        // 2) ClassRef
        try
        {
            var classId = invoice.ClassRef?.Value;
            if (!string.IsNullOrWhiteSpace(classId))
            {
                var mapped = await GetBudgetEntryIdForQBEntityAsync("Class", classId).ConfigureAwait(false);
                if (mapped != null) return mapped;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ClassRef mapping attempt failed for invoice {InvoiceId}", invoice?.Id);
        }

        // 3) DepartmentRef
        try
        {
            var deptId = invoice.DepartmentRef?.Value;
            if (!string.IsNullOrWhiteSpace(deptId))
            {
                var mapped = await GetBudgetEntryIdForQBEntityAsync("Department", deptId).ConfigureAwait(false);
                if (mapped != null) return mapped;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DepartmentRef mapping attempt failed for invoice {InvoiceId}", invoice?.Id);
        }

        // 4) CustomerRef
        try
        {
            var custId = invoice.CustomerRef?.Value;
            if (!string.IsNullOrWhiteSpace(custId))
            {
                var mapped = await GetBudgetEntryIdForQBEntityAsync("Customer", custId).ConfigureAwait(false);
                if (mapped != null) return mapped;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CustomerRef mapping attempt failed for invoice {InvoiceId}", invoice?.Id);
        }

        // 5) Line item ItemRef
        try
        {
            if (invoice.Line != null)
            {
                foreach (var line in invoice.Line)
                {
                    if (line.DetailType == Intuit.Ipp.Data.LineDetailTypeEnum.SalesItemLineDetail)
                    {
                        var detail = line.AnyIntuitObject as Intuit.Ipp.Data.SalesItemLineDetail;
                        var itemRef = detail?.ItemRef?.Value;
                        if (!string.IsNullOrWhiteSpace(itemRef))
                        {
                            var mapped = await GetBudgetEntryIdForQBEntityAsync("Item", itemRef).ConfigureAwait(false);
                            if (mapped != null) return mapped;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ItemRef mapping attempt failed for invoice {InvoiceId}", invoice?.Id);
        }

        // 6) Rule-based mappings (regex against invoice/line descriptions)
        try
        {
            var allMappings = await GetAllMappingsAsync().ConfigureAwait(false);
            var rules = allMappings.Where(m => m.IsActive && m.MappingStrategy == "RuleBased").OrderByDescending(m => m.Priority);
            foreach (var rule in rules)
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(rule.QBEntityId, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var invoiceText = (invoice.PrivateNote ?? string.Empty) + " " + (invoice.TxnDate.ToString() ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(invoiceText) && regex.IsMatch(invoiceText))
                    {
                        return rule.BudgetEntryId;
                    }

                    if (invoice.Line != null)
                    {
                        foreach (var line in invoice.Line)
                        {
                            if (!string.IsNullOrWhiteSpace(line?.Description) && regex.IsMatch(line.Description))
                                return rule.BudgetEntryId;
                        }
                    }
                }
                catch (System.ArgumentException ex)
                {
                    _logger.LogWarning(ex, "Invalid regex in RuleBased mapping {MappingId}", rule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Rule-based mapping attempt failed for invoice {InvoiceId}", invoice?.Id);
        }

        return null;
    }
}
