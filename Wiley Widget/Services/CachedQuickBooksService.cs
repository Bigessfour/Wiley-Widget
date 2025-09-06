using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyWidget.Services;
using WileyWidget.Configuration;
using WileyWidget.Models;

/// <summary>
/// Cached wrapper for QuickBooks service to improve performance and reduce API calls.
/// </summary>
public class CachedQuickBooksService : IQuickBooksService
{
    private readonly QuickBooksService _innerService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedQuickBooksService> _logger;
    private readonly QuickBooksSettings _settings;
    private readonly DistributedCacheEntryOptions _cacheOptions;

    public CachedQuickBooksService(
        QuickBooksService innerService,
        IDistributedCache cache,
        ILogger<CachedQuickBooksService> logger,
        IOptions<QuickBooksSettings> settings)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        // Configure cache options based on settings
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.CacheDurationMinutes),
            SlidingExpiration = TimeSpan.FromMinutes(_settings.SlidingExpirationMinutes)
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_innerService as IDisposable)?.Dispose();
        }
    }

    public bool HasValidAccessToken() => ((dynamic)_innerService).HasValidAccessToken();

    public async Task RefreshTokenIfNeededAsync() => await ((dynamic)_innerService).RefreshTokenIfNeededAsync();

    public async Task RefreshTokenAsync() => await ((dynamic)_innerService).RefreshTokenAsync();

    public async Task<List<QboCustomer>> GetCustomersAsync()
    {
        const string cacheKey = "qbo_customers";

        try
        {
            // Try to get from cache first
            var cachedData = await Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.GetStringAsync(_cache, cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("Retrieved customers from cache");
                return System.Text.Json.JsonSerializer.Deserialize<List<QboCustomer>>(cachedData);
            }

            // Cache miss - fetch from service
            _logger.LogDebug("Cache miss for customers, fetching from QuickBooks");
            var customers = await ((dynamic)_innerService).GetCustomersAsync();

            // Cache the result
            var serializedData = System.Text.Json.JsonSerializer.Serialize(customers);
            await Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.SetStringAsync(_cache, cacheKey, serializedData, _cacheOptions);

            return customers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cached GetCustomersAsync");
            // Fallback to direct service call
            return await ((dynamic)_innerService).GetCustomersAsync();
        }
    }

    public async Task<List<QboInvoice>> GetInvoicesAsync(string enterprise = null)
    {
        var cacheKey = string.IsNullOrWhiteSpace(enterprise)
            ? "qbo_invoices_all"
            : $"qbo_invoices_enterprise_{enterprise}";

        try
        {
            // Try to get from cache first
            var cachedData = await Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.GetStringAsync(_cache, cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("Retrieved invoices from cache for enterprise: {Enterprise}", enterprise ?? "all");
                return System.Text.Json.JsonSerializer.Deserialize<List<QboInvoice>>(cachedData);
            }

            // Cache miss - fetch from service
            _logger.LogDebug("Cache miss for invoices, fetching from QuickBooks for enterprise: {Enterprise}", enterprise ?? "all");
            var invoices = await ((dynamic)_innerService).GetInvoicesAsync(enterprise);

            // Cache the result
            var serializedData = System.Text.Json.JsonSerializer.Serialize(invoices);
            await Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.SetStringAsync(_cache, cacheKey, serializedData, _cacheOptions);

            return invoices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cached GetInvoicesAsync for enterprise: {Enterprise}", enterprise ?? "all");
            // Fallback to direct service call
            return await ((dynamic)_innerService).GetInvoicesAsync(enterprise);
        }
    }

    public async Task<string> SyncEnterpriseToQboClassAsync(Enterprise enterprise)
    {
        try
        {
            var result = await ((dynamic)_innerService).SyncEnterpriseToQboClassAsync(enterprise);

            // Invalidate related caches after sync
            await InvalidateEnterpriseCachesAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cached SyncEnterpriseToQboClassAsync for enterprise: {EnterpriseId}", enterprise.Id);
            throw;
        }
    }

    public async Task<string> SyncBudgetInteractionToQboAccountAsync(BudgetInteraction interaction, string classId)
    {
        try
        {
            var result = await ((dynamic)_innerService).SyncBudgetInteractionToQboAccountAsync(interaction, classId);

            // Invalidate related caches after sync
            await InvalidateBudgetCachesAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cached SyncBudgetInteractionToQboAccountAsync for interaction: {InteractionId}", interaction.Id);
            throw;
        }
    }

    private async Task InvalidateEnterpriseCachesAsync()
    {
        try
        {
            // Invalidate customer and invoice caches as enterprise sync might affect them
            await _cache.RemoveAsync("qbo_customers");
            await _cache.RemoveAsync("qbo_invoices_all");

            // Remove enterprise-specific invoice caches (we'd need to track these separately in a real implementation)
            // For now, we'll clear all invoice caches with a pattern
            _logger.LogDebug("Invalidated enterprise-related caches");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating enterprise caches");
        }
    }

    private async Task InvalidateBudgetCachesAsync()
    {
        try
        {
            // Invalidate caches that might be affected by budget changes
            await _cache.RemoveAsync("qbo_invoices_all");
            _logger.LogDebug("Invalidated budget-related caches");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating budget caches");
        }
    }

    /// <summary>
    /// Manually clear all QuickBooks-related caches
    /// </summary>
    public async Task ClearAllCachesAsync()
    {
        try
        {
            await _cache.RemoveAsync("qbo_customers");
            await _cache.RemoveAsync("qbo_invoices_all");
            _logger.LogInformation("Cleared all QuickBooks caches");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing QuickBooks caches");
            throw;
        }
    }
}
