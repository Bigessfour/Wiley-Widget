using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Validators;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IAccountService for managing municipal account operations.
    /// Extracts business logic from AccountsViewModel for better separation of concerns.
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly ILogger<AccountService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAccountMapper _mapper;

        public AccountService(
            ILogger<AccountService> logger,
            IServiceScopeFactory scopeFactory,
            IAccountMapper mapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<AccountsResult> LoadAccountsAsync(
            MunicipalFundType? fundType = null,
            AccountType? accountType = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Loading municipal accounts with filters {@Filters}",
                    new { Fund = fundType?.ToString() ?? "(all)", AccountType = accountType?.ToString() ?? "(all)", SearchText = searchText ?? "(none)" });

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Build query with filters
                var accountsQuery = context.MunicipalAccounts
                    .Include(a => a.Department)
                    .Include(a => a.BudgetPeriod)
                    .Include(a => a.ParentAccount)
                    .Where(a => a.IsActive)
                    .AsNoTracking();

                // Apply optional filters using LINQ
                if (fundType.HasValue)
                {
                    accountsQuery = accountsQuery.Where(a => a.Fund == fundType.Value);
                }

                if (accountType.HasValue)
                {
                    accountsQuery = accountsQuery.Where(a => a.Type == accountType.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchTerm = searchText.Trim();
                    accountsQuery = accountsQuery.Where(a =>
                        (a.Name != null && a.Name.Contains(searchTerm)) ||
                        (a.AccountNumber != null && a.AccountNumber.Value.Contains(searchTerm)));
                }

                var accountsList = await accountsQuery
                    .OrderBy(a => a.AccountNumber != null ? a.AccountNumber.Value : string.Empty)
                    .ToListAsync(cancellationToken);

                // Map domain entities to display DTOs
                var mapped = _mapper.MapToDisplay(accountsList).ToList();
                var totalBalance = mapped.Sum(a => a.Balance);
                var activeCount = mapped.Count;

                _logger.LogInformation("Municipal accounts loaded successfully: {Count} accounts, Total Balance: {Balance:C}",
                    activeCount, totalBalance);

                return new AccountsResult(mapped, totalBalance, activeCount);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Loading accounts operation was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load municipal accounts");
                throw;
            }
        }

        public IEnumerable<string> ValidateAccount(MunicipalAccount account)
        {
            if (account == null)
            {
                yield return "Account cannot be null.";
                yield break;
            }

            // Use FluentValidation extension method from domain model
            foreach (var error in account.Validate())
            {
                yield return error;
            }
        }

        public async Task<SaveResult> SaveAccountAsync(MunicipalAccount account, CancellationToken cancellationToken = default)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            // Validate first
            var errors = ValidateAccount(account).ToList();
            if (errors.Count > 0)
            {
                _logger.LogWarning("Account validation failed with {ErrorCount} errors", errors.Count);
                return new SaveResult(false, errors);
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (account.Id == 0)
                {
                    context.MunicipalAccounts.Add(account);
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Created account {@AccountDetails}",
                        new
                        {
                            AccountNumber = account.AccountNumber?.Value,
                            Name = account.Name,
                            DepartmentId = account.DepartmentId,
                            Fund = account.Fund.ToString(),
                            Type = account.Type.ToString()
                        });
                }
                else
                {
                    context.MunicipalAccounts.Update(account);
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Updated account {@AccountDetails}",
                        new { Id = account.Id, AccountNumber = account.AccountNumber?.Value, Name = account.Name, Balance = account.Balance });
                }

                return new SaveResult(true, Array.Empty<string>());
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Save account operation was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save account {AccountNumber}", account.AccountNumber?.Value);
                throw;
            }
        }

        public async Task<bool> DeleteAccountAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var account = await context.MunicipalAccounts.FindAsync(new object[] { id }, cancellationToken);
                if (account == null)
                {
                    _logger.LogWarning("Account {Id} not found for deletion", id);
                    return false;
                }

                // Soft delete
                account.IsActive = false;
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Deleted (deactivated) account {@AccountDetails}",
                    new { Id = id, AccountNumber = account.AccountNumber?.Value, Name = account.Name });

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Delete account operation was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete account {Id}", id);
                throw;
            }
        }
    }
}
