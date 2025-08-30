#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Database integration service for GrokSupercomputer
/// Handles all database operations needed by the AI service
/// </summary>
public class GrokDatabaseService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GrokDatabaseService> _logger;

    public GrokDatabaseService(AppDbContext context, ILogger<GrokDatabaseService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Saves AI analysis results to the database
    /// </summary>
    public async Task SaveAnalysisResultAsync(AiAnalysisResult result)
    {
        try
        {
            _context.AiAnalysisResults.Add(result);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AI analysis result saved with ID {ResultId}", result.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI analysis result");
            throw;
        }
    }

    /// <summary>
    /// Retrieves cached AI response by input hash
    /// </summary>
    public async Task<AiResponseCache?> GetCachedResponseAsync(string cacheKey)
    {
        try
        {
            var cacheEntry = await _context.AiResponseCache
                .FirstOrDefaultAsync(c => c.CacheKey == cacheKey && !c.IsExpired);

            if (cacheEntry != null)
            {
                // Update access statistics
                cacheEntry.AccessCount++;
                cacheEntry.LastAccessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogDebug("Retrieved cached response for key {CacheKey}", cacheKey);
            }

            return cacheEntry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve cached response for key {CacheKey}", cacheKey);
            return null;
        }
    }

    /// <summary>
    /// Saves AI response to cache
    /// </summary>
    public async Task SaveResponseToCacheAsync(string cacheKey, string response, TimeSpan expiration)
    {
        try
        {
            var cacheEntry = new AiResponseCache
            {
                CacheKey = cacheKey,
                Response = response,
                ExpiresAt = DateTime.UtcNow.Add(expiration)
            };

            _context.AiResponseCache.Add(cacheEntry);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Saved response to cache with key {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save response to cache for key {CacheKey}", cacheKey);
            throw;
        }
    }

    /// <summary>
    /// Saves AI recommendations for enterprises
    /// </summary>
    public async Task SaveRecommendationsAsync(IEnumerable<AiRecommendation> recommendations)
    {
        try
        {
            _context.AiRecommendations.AddRange(recommendations);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved {Count} AI recommendations", recommendations.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI recommendations");
            throw;
        }
    }

    /// <summary>
    /// Logs an audit entry for AI operations
    /// </summary>
    public async Task LogAuditEntryAsync(AiAnalysisAudit auditEntry)
    {
        try
        {
            _context.AiAnalysisAudits.Add(auditEntry);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Audit entry logged: {OperationType}", auditEntry.OperationType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry");
            // Don't throw for audit failures to avoid breaking main flow
        }
    }

    /// <summary>
    /// Retrieves historical budget data for trend analysis
    /// </summary>
    public async Task<List<OverallBudget>> GetHistoricalBudgetsAsync(int count = 10)
    {
        try
        {
            var budgets = await _context.OverallBudgets
                .OrderByDescending(ob => ob.SnapshotDate)
                .Take(count)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} historical budget snapshots", budgets.Count);
            return budgets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve historical budgets");
            return new List<OverallBudget>();
        }
    }

    /// <summary>
    /// Updates enterprise data with AI results
    /// </summary>
    public async Task UpdateEnterprisesWithAiResultsAsync(IEnumerable<Enterprise> enterprises)
    {
        try
        {
            _context.Enterprises.UpdateRange(enterprises);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated {Count} enterprises with AI results", enterprises.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update enterprises with AI results");
            throw;
        }
    }

    /// <summary>
    /// Creates a new budget snapshot
    /// </summary>
    public async Task<OverallBudget> CreateBudgetSnapshotAsync(
        decimal totalRevenue,
        decimal totalExpenses,
        int totalCitizens,
        string notes = "")
    {
        try
        {
            // Mark existing current budget as not current
            var currentBudgets = await _context.OverallBudgets
                .Where(ob => ob.IsCurrent)
                .ToListAsync();

            foreach (var budget in currentBudgets)
            {
                budget.IsCurrent = false;
            }

            // Create new budget snapshot
            var newBudget = new OverallBudget
            {
                TotalMonthlyRevenue = totalRevenue,
                TotalMonthlyExpenses = totalExpenses,
                TotalMonthlyBalance = totalRevenue - totalExpenses,
                TotalCitizensServed = totalCitizens,
                AverageRatePerCitizen = totalCitizens > 0 ? totalRevenue / totalCitizens : 0,
                Notes = notes,
                IsCurrent = true
            };

            _context.OverallBudgets.Add(newBudget);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new budget snapshot with ID {BudgetId}", newBudget.Id);
            return newBudget;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create budget snapshot");
            throw;
        }
    }

    /// <summary>
    /// Gets AI recommendations for a specific enterprise
    /// </summary>
    public async Task<List<AiRecommendation>> GetRecommendationsForEnterpriseAsync(int enterpriseId)
    {
        try
        {
            var recommendations = await _context.AiRecommendations
                .Where(ar => ar.EnterpriseId == enterpriseId)
                .OrderByDescending(ar => ar.GeneratedDate)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} recommendations for enterprise {EnterpriseId}",
                           recommendations.Count, enterpriseId);
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recommendations for enterprise {EnterpriseId}", enterpriseId);
            return new List<AiRecommendation>();
        }
    }

    /// <summary>
    /// Cleans up expired cache entries
    /// </summary>
    public async Task<int> CleanupExpiredCacheAsync()
    {
        try
        {
            var expiredEntries = await _context.AiResponseCache
                .Where(c => c.IsExpired)
                .ToListAsync();

            if (expiredEntries.Any())
            {
                _context.AiResponseCache.RemoveRange(expiredEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} expired cache entries", expiredEntries.Count);
                return expiredEntries.Count;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired cache");
            return 0;
        }
    }

    /// <summary>
    /// Gets analysis statistics
    /// </summary>
    public async Task<AnalysisStatistics> GetAnalysisStatisticsAsync()
    {
        try
        {
            var stats = new AnalysisStatistics
            {
                TotalAnalyses = await _context.AiAnalysisResults.CountAsync(),
                SuccessfulAnalyses = await _context.AiAnalysisResults.CountAsync(aar => aar.IsSuccessful),
                FailedAnalyses = await _context.AiAnalysisResults.CountAsync(aar => !aar.IsSuccessful),
                TotalRecommendations = await _context.AiRecommendations.CountAsync(),
                PendingRecommendations = await _context.AiRecommendations.CountAsync(ar => ar.Status == "Pending"),
                CacheEntries = await _context.AiResponseCache.CountAsync(),
                AuditEntries = await _context.AiAnalysisAudits.CountAsync()
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve analysis statistics");
            return new AnalysisStatistics();
        }
    }
}

/// <summary>
/// Statistics about AI analysis operations
/// </summary>
public class AnalysisStatistics
{
    public int TotalAnalyses { get; set; }
    public int SuccessfulAnalyses { get; set; }
    public int FailedAnalyses { get; set; }
    public int TotalRecommendations { get; set; }
    public int PendingRecommendations { get; set; }
    public int CacheEntries { get; set; }
    public int AuditEntries { get; set; }

    public double SuccessRate => TotalAnalyses > 0 ? (double)SuccessfulAnalyses / TotalAnalyses * 100 : 0;
}
