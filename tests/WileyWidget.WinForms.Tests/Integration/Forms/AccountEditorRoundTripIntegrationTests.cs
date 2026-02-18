using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Input;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class AccountEditorRoundTripIntegrationTests
{
    private const string SqlConnectionString = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=WileyWidget;Integrated Security=True;Pooling=False;Encrypt=False;Trust Server Certificate=True";

    [WinFormsFact]
    public async Task AccountEditPanel_Save_PersistsAndRoundTripsBalanceBudgetAndIsActive()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        var accountNumber = $"311.{DateTime.UtcNow:HHmmss}";
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());
        services.AddMemoryCache();
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlServer(SqlConnectionString));
        services.AddScoped(sp => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IDbContextFactory<AppDbContext>>(sp)
            .CreateDbContext());
        services.AddScoped<IAccountsRepository>(sp => new TargetedAccountsRepository(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDbContextFactory<AppDbContext>>(sp),
            () => accountNumber));
        services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
        services.AddScoped<AccountsViewModel>();
        services.AddSingleton<DpiAwareImageService>();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        await SeedRequiredLookupDataAsync(provider);

        await using var editorScope = provider.CreateAsyncScope();
        var panel = CreateEditorPanel(editorScope.ServiceProvider);
        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(editorScope.ServiceProvider);
        panel.ViewModel = viewModel;

        try
        {
            SetEditorValues(
                panel,
                accountNumber: accountNumber,
                accountName: "Specific Ownership Tax",
                description: "Specific Ownership Tax",
                balance: 1250.75,
                budget: 2500.50,
                isActive: false);

            var editModel = SyncEditorAndGetModel(panel);
            var account = editModel.ToEntity();
            await viewModel.CreateAccountFromEditorAsync(account, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(30));

            viewModel.ErrorMessage.Should().BeNullOrWhiteSpace("save should complete without viewmodel errors");

            await AssertDatabaseAndViewModelRoundTripAsync(provider, accountNumber)
                .WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            panel.Dispose();
            await TryCleanupCreatedAccountAsync(provider, accountNumber);
        }
    }

    private static AccountEditPanel CreateEditorPanel(IServiceProvider serviceProvider)
    {
        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ScopedPanelBase>>(serviceProvider);
        var imageService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DpiAwareImageService>(serviceProvider);

        return new AccountEditPanel(scopeFactory, logger, imageService);
    }

    private static async Task SeedRequiredLookupDataAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider);

        var canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue("test requires SQL Server connection to WileyWidget database");

        if (!await db.Departments.AnyAsync(d => d.Id == 1))
        {
            db.Departments.Add(new Department
            {
                Id = 1,
                Name = "Finance"
            });
        }

        if (!await db.BudgetPeriods.AnyAsync(bp => bp.Id == 1))
        {
            db.BudgetPeriods.Add(new BudgetPeriod
            {
                Id = 1,
                Year = 2026,
                Name = "FY 2026",
                CreatedDate = DateTime.UtcNow,
                Status = BudgetStatus.Adopted,
                StartDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2025, 12, 31),
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }

    private static void SetEditorValues(
        Control root,
        string accountNumber,
        string accountName,
        string description,
        double balance,
        double budget,
        bool isActive)
    {
        FindControl<TextBoxExt>(root, "txtAccountNumber").Text = accountNumber;
        FindControl<TextBoxExt>(root, "txtName").Text = accountName;
        FindControl<TextBoxExt>(root, "txtDescription").Text = description;

        var departmentCombo = FindControl<Syncfusion.WinForms.ListView.SfComboBox>(root, "cmbDepartment");
        departmentCombo.DataSource = new[] { new Department { Id = 1, Name = "Finance" } };
        departmentCombo.DisplayMember = "Name";
        departmentCombo.ValueMember = "Id";
        departmentCombo.SelectedValue = 1;
        if (departmentCombo.SelectedIndex < 0)
        {
            departmentCombo.SelectedIndex = 0;
        }

        var fundCombo = FindControl<Syncfusion.WinForms.ListView.SfComboBox>(root, "cmbFund");
        fundCombo.DataSource = Enum.GetValues(typeof(MunicipalFundType));
        fundCombo.SelectedItem = MunicipalFundType.General;
        if (fundCombo.SelectedIndex < 0)
        {
            fundCombo.SelectedIndex = 0;
        }

        var typeCombo = FindControl<Syncfusion.WinForms.ListView.SfComboBox>(root, "cmbType");
        typeCombo.DataSource = Enum.GetValues(typeof(AccountType));
        typeCombo.SelectedItem = AccountType.Cash;
        if (typeCombo.SelectedIndex < 0)
        {
            typeCombo.SelectedIndex = 0;
        }

        FindControl<SfNumericTextBox>(root, "numBalance").Value = balance;
        FindControl<SfNumericTextBox>(root, "numBudget").Value = budget;
        FindControl<CheckBoxAdv>(root, "chkActive").Checked = isActive;
    }

    private static async Task AssertDatabaseAndViewModelRoundTripAsync(IServiceProvider provider, string accountNumber)
    {
        await using var verifyScope = provider.CreateAsyncScope();
        var repository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IMunicipalAccountRepository>(verifyScope.ServiceProvider);
        var saved = await repository.GetByAccountNumberAsync(accountNumber);

        saved.Should().NotBeNull("panel save should persist to repository");
        saved!.Balance.Should().Be(1250.75m);
        saved.BudgetAmount.Should().Be(2500.50m);
        saved.IsActive.Should().BeFalse();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(verifyScope.ServiceProvider);
        await viewModel.OnVisibilityChangedAsync(true);

        var display = viewModel.Accounts.Single(a => a.AccountNumber == accountNumber);
        display.CurrentBalance.Should().Be(1250.75m);
        display.BudgetAmount.Should().Be(2500.50m);
        display.IsActive.Should().BeFalse();
    }

    private static async Task TryCleanupCreatedAccountAsync(IServiceProvider provider, string accountNumber)
    {
        try
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var repository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IMunicipalAccountRepository>(cleanupScope.ServiceProvider);
            var saved = await repository.GetByAccountNumberAsync(accountNumber);
            if (saved != null)
            {
                await repository.DeleteAsync(saved.Id);
            }
        }
        catch
        {
        }
    }

    private static MunicipalAccountEditModel SyncEditorAndGetModel(AccountEditPanel panel)
    {
        var syncMethod = panel.GetType().GetMethod("SyncEditModelFromControls", BindingFlags.Instance | BindingFlags.NonPublic);
        syncMethod.Should().NotBeNull("AccountEditPanel must expose SyncEditModelFromControls for deterministic save mapping");
        syncMethod!.Invoke(panel, null);

        var editModelField = panel.GetType().GetField("_editModel", BindingFlags.Instance | BindingFlags.NonPublic);
        editModelField.Should().NotBeNull("AccountEditPanel should hold the active edit model backing the editor controls");

        var editModel = editModelField!.GetValue(panel) as MunicipalAccountEditModel;
        editModel.Should().NotBeNull("account editor must maintain a bound MunicipalAccountEditModel");
        return editModel!;
    }

    private static TControl FindControl<TControl>(Control root, string name)
        where TControl : Control
    {
        if (root is TControl typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        foreach (Control child in root.Controls)
        {
            var result = TryFindControl<TControl>(child, name);
            if (result != null)
            {
                return result;
            }
        }

        throw new InvalidOperationException($"Control '{name}' of type {typeof(TControl).Name} was not found.");
    }

    private static TControl? TryFindControl<TControl>(Control root, string name)
        where TControl : Control
    {
        if (root is TControl typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        foreach (Control child in root.Controls)
        {
            var nested = TryFindControl<TControl>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private sealed class TargetedAccountsRepository : IAccountsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly Func<string> _accountNumberAccessor;

        public TargetedAccountsRepository(IDbContextFactory<AppDbContext> dbContextFactory, Func<string> accountNumberAccessor)
        {
            _dbContextFactory = dbContextFactory;
            _accountNumberAccessor = accountNumberAccessor;
        }

        public Task<IReadOnlyList<MunicipalAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
            => QueryTargetedAsync(cancellationToken);

        public Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAsync(MunicipalFundType fundType, CancellationToken cancellationToken = default)
            => QueryTargetedAsync(cancellationToken, account => account.FundType == fundType);

        public Task<IReadOnlyList<MunicipalAccount>> GetAccountsByTypeAsync(AccountType accountType, CancellationToken cancellationToken = default)
            => QueryTargetedAsync(cancellationToken, account => account.Type == accountType);

        public Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAndTypeAsync(MunicipalFundType fundType, AccountType accountType, CancellationToken cancellationToken = default)
            => QueryTargetedAsync(cancellationToken, account => account.FundType == fundType && account.Type == accountType);

        public async Task<MunicipalAccount?> GetAccountByIdAsync(int accountId, CancellationToken cancellationToken = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await db.MunicipalAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        }

        public Task<IReadOnlyList<MunicipalAccount>> SearchAccountsAsync(string searchTerm, CancellationToken cancellationToken = default)
            => QueryTargetedAsync(cancellationToken, account =>
                account.Name.Contains(searchTerm) ||
                (account.AccountNumber != null && account.AccountNumber.Value.Contains(searchTerm)) ||
                account.FundDescription.Contains(searchTerm));

        public async Task<IReadOnlyList<WileyWidget.Business.Models.MonthlyRevenueAggregate>> GetMonthlyRevenueAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            // Test implementation - return sample data
            return new List<WileyWidget.Business.Models.MonthlyRevenueAggregate>
            {
                new() { Month = new DateTime(2024, 1, 1), Amount = 10000m, TransactionCount = 50 },
                new() { Month = new DateTime(2024, 2, 1), Amount = 12000m, TransactionCount = 55 }
            };
        }

        private async Task<IReadOnlyList<MunicipalAccount>> QueryTargetedAsync(
            CancellationToken cancellationToken,
            Func<MunicipalAccount, bool>? additionalPredicate = null)
        {
            var target = _accountNumberAccessor();
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var query = db.MunicipalAccounts
                .AsNoTracking()
                .Where(a => a.AccountNumber != null && a.AccountNumber.Value == target)
                .AsEnumerable();

            if (additionalPredicate != null)
            {
                query = query.Where(additionalPredicate);
            }

            return query.ToList();
        }
    }
}
