using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.ViewModels;
using Xunit;

namespace PrismUnityIntegrationTests;

public class MunicipalAccountSeedingTests
{
    private static AppDbContext CreateDbContext()
    {
        // Reuse the same configuration as the app via design-time factory
        var factory = new AppDbContextFactory();
        return factory.CreateDbContext(Array.Empty<string>());
    }

    [Fact(DisplayName = "Seeding inserts 25 Conservation Trust accounts")]
    public async Task Seeding_Inserts_25_ConservationTrust_Accounts()
    {
        await using var context = CreateDbContext();
        var count = await context.MunicipalAccounts
            .Where(a => a.Fund == MunicipalFundType.ConservationTrust)
            .CountAsync();

        Assert.Equal(25, count);
    }

    [Fact(DisplayName = "Bank-like filter returns 5 rows (Cash/Investments/Receivables)")]
    public async Task BankLike_Filter_Returns_5()
    {
        await using var context = CreateDbContext();
        var bankLike = new[] { AccountType.Cash, AccountType.Investments, AccountType.Receivables };

        var count = await context.MunicipalAccounts
            .Where(a => a.Fund == MunicipalFundType.ConservationTrust && bankLike.Contains(a.Type))
            .CountAsync();

        Assert.Equal(5, count);
    }

    [Fact(DisplayName = "ViewModel LoadAccountsAsync populates 25 rows")]
    public async Task ViewModel_LoadAccounts_Populates_25()
    {
        // Arrange: query real DB, but pass data via mocked repository to the VM
        await using var context = CreateDbContext();
        var all = await context.MunicipalAccounts
            .Where(a => a.Fund == MunicipalFundType.ConservationTrust)
            .OrderBy(a => a.AccountNumber!.Value)
            .ToListAsync();

        var repo = new Mock<WileyWidget.Business.Interfaces.IMunicipalAccountRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(all);

        // Minimal other deps mocked
        var qb = new Mock<WileyWidget.Services.IQuickBooksService>();
        var grok = new Mock<WileyWidget.Services.IGrokSupercomputer>();
        var regions = new Mock<IRegionManager>();
        var eventsAgg = new Mock<IEventAggregator>();

        var vm = new MunicipalAccountViewModel(repo.Object, qb.Object, grok.Object, regions.Object, eventsAgg.Object);

        // Act
        var loadMethod = typeof(MunicipalAccountViewModel)
            .GetMethod("LoadAccountsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(loadMethod);
        var task = (Task?)loadMethod!.Invoke(vm, Array.Empty<object>());
        if (task != null) await task.ConfigureAwait(false);

        // Assert
        Assert.Equal(25, vm.MunicipalAccounts.Count);
    }
}
