using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Business.Models;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for municipal account data operations.
/// Encapsulates data access logic for MunicipalAccount entities.
/// </summary>
public class AccountsRepository : IAccountsRepository
{
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.AccountsRepository");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountsRepository> _logger;

    public AccountsRepository(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountsRepository> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AccountsRepository.GetAllAccounts");
        activity?.SetTag("operation.type", "query");

        try
        {
            _logger.LogDebug("Retrieving all municipal accounts");

            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var result = await context.Set<MunicipalAccount>()
                .AsNoTracking()
                .OrderBy(a => a.AccountNumber_Value)
                .ToListAsync(cancellationToken);

            activity?.SetTag("result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Retrieved {Count} municipal accounts", result.Count);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error retrieving all municipal accounts");
            throw;
        }
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAsync(
        MunicipalFundType fundType,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AccountsRepository.GetAccountsByFund");
        activity?.SetTag("fund_type", fundType.ToString());
        activity?.SetTag("operation.type", "query");

        try
        {
            _logger.LogDebug("Retrieving accounts for fund type: {FundType}", fundType);

            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var result = await context.Set<MunicipalAccount>()
                .AsNoTracking()
                .Where(a => a.FundType == fundType)
                .OrderBy(a => a.AccountNumber_Value)
                .ToListAsync(cancellationToken);

            activity?.SetTag("result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Retrieved {Count} accounts for fund type {FundType}", result.Count, fundType);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error retrieving accounts for fund type {FundType}", fundType);
            throw;
        }
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAccountsByTypeAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving accounts for account type: {AccountType}", accountType);

        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<MunicipalAccount>()
            .AsNoTracking()
            .Where(a => a.Type == accountType)
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAndTypeAsync(
        MunicipalFundType fundType,
        AccountType accountType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving accounts for fund type: {FundType} and account type: {AccountType}",
            fundType, accountType);

        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<MunicipalAccount>()
            .AsNoTracking()
            .Where(a => a.FundType == fundType && a.Type == accountType)
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<MunicipalAccount?> GetAccountByIdAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving account with ID: {AccountId}", accountId);

        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<MunicipalAccount>()
            .AsNoTracking()
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<MunicipalAccount>> SearchAccountsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _logger.LogDebug("Empty search term - returning all accounts");
            return await GetAllAccountsAsync(cancellationToken);
        }

        _logger.LogDebug("Searching accounts for term: {SearchTerm}", searchTerm);

        var normalizedSearch = searchTerm.ToLowerInvariant();

        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<MunicipalAccount>()
            .AsNoTracking()
            .Where(a =>
                a.Name.ToLower(CultureInfo.InvariantCulture).Contains(normalizedSearch) ||
                (a.AccountNumber_Value != null && a.AccountNumber_Value.ToLower(CultureInfo.InvariantCulture).Contains(normalizedSearch)) ||
                (a.FundDescription != null && a.FundDescription.ToLower(CultureInfo.InvariantCulture).Contains(normalizedSearch)))
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns aggregated revenue per calendar month within the supplied date range.
    /// Minimal stub implementation: returns an empty collection until a proper DB aggregation is implemented.
    /// </summary>
    public Task<IReadOnlyList<MonthlyRevenueAggregate>> GetMonthlyRevenueAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetMonthlyRevenueAsync called: returning empty aggregates until implemented.");
        return Task.FromResult<IReadOnlyList<MonthlyRevenueAggregate>>(Array.Empty<MonthlyRevenueAggregate>());
    }
}
