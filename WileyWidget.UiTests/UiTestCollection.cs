using Xunit;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.ViewModels;

[CollectionDefinition("UiTests", DisableParallelization = true)]
public class UiTestCollection { }

[Collection("UiTests")]
public class MunicipalAccountFilterTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;

        var ctx = new AppDbContext(options);

        // Seed 25 municipal accounts similar to FakeQuickBooksService
        for (int i = 1; i <= 25; i++)
        {
            ctx.MunicipalAccounts.Add(new MunicipalAccount
            {
                AccountNumber = new AccountNumber { Value = i == 1 ? "110" : $"110.{i - 1}" },
                Name = $"Account {i}",
                Balance = i * 100m,
                FundDescription = i % 2 == 0 ? "General" : "Special",
                TypeDescription = i % 3 == 0 ? "Cash" : "Asset",
                Type = i % 3 == 0 ? WileyWidget.Models.AccountType.Cash : WileyWidget.Models.AccountType.Asset,
                Department = new Department { Name = $"Dept {i % 5}" }
            });
        }

        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task LoadMunicipalView_ApplyCashFilter_AssertViewModelAndUiCounts()
    {
        // Mock repository with test data seeding
        using var ctx = CreateInMemoryContext();
        var repo = new MunicipalAccountRepository(ctx);
        var vm = new MunicipalAccountViewModel(repo, null, null, null, null);

        // Load accounts
        await vm.InitializeAsync();
        Assert.Equal(25, vm.MunicipalAccounts.Count);

        // Apply "Cash" filter
        vm.TypeFilter = "Cash";
        await vm.ApplyFiltersAsync();

        // Assert ViewModel count = 8
        Assert.Equal(8, vm.MunicipalAccounts.Count);
        Assert.All(vm.MunicipalAccounts, account => Assert.Equal("Cash", account.TypeDescription));

        // Note: UI grid rows assertion would be in FlaUI test, but here we verify the ViewModel matches expected count
        // In a full UI test, this would be combined with FlaUI to verify grid.Rows.Count == 8
    }
}
