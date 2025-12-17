using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for municipal account data operations.
/// Encapsulates data access logic for MunicipalAccount entities.
/// </summary>
public class AccountsRepository : IAccountsRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AccountsRepository> _logger;

    public AccountsRepository(
        AppDbContext dbContext,
        ILogger<AccountsRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all municipal accounts");

        return await _dbContext.Set<MunicipalAccount>()
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAsync(
        MunicipalFundType fundType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving accounts for fund type: {FundType}", fundType);

        return await _dbContext.Set<MunicipalAccount>()
            .Where(a => a.Fund == fundType)
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MunicipalAccount>> GetAccountsByTypeAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving accounts for account type: {AccountType}", accountType);

        return await _dbContext.Set<MunicipalAccount>()
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

        return await _dbContext.Set<MunicipalAccount>()
            .Where(a => a.Fund == fundType && a.Type == accountType)
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<MunicipalAccount?> GetAccountByIdAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving account with ID: {AccountId}", accountId);

        return await _dbContext.Set<MunicipalAccount>()
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

        return await _dbContext.Set<MunicipalAccount>()
            .Where(a =>
                a.Name.ToLower().Contains(normalizedSearch) ||
                (a.AccountNumber_Value != null && a.AccountNumber_Value.ToLower().Contains(normalizedSearch)) ||
                (a.FundDescription != null && a.FundDescription.ToLower().Contains(normalizedSearch)))
            .OrderBy(a => a.AccountNumber_Value)
            .ToListAsync(cancellationToken);
    }
}
