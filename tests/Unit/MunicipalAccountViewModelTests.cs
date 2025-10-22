using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.Tests.Unit
{
    public class MunicipalAccountViewModelTests
    {
        private static AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
        public async Task LoadAccounts_Loads_25Accounts()
        {
            using var ctx = CreateInMemoryContext();
            var repo = new WileyWidget.Data.MunicipalAccountRepository(ctx);
            var vm = new MunicipalAccountViewModel(repo, null, null);

            await vm.InitializeAsync();

            Assert.Equal(25, vm.MunicipalAccounts.Count);
        }

        [Fact]
        public async Task Filtering_ByType_ReducesResults()
        {
            using var ctx = CreateInMemoryContext();
            var repo = new WileyWidget.Data.MunicipalAccountRepository(ctx);
            var vm = new MunicipalAccountViewModel(repo, null, null);

            await vm.InitializeAsync();

            // Apply filter: TypeDescription == "Cash"
            vm.TypeFilter = "Cash";

            Assert.Equal(8, ((System.Collections.IEnumerable)vm.Accounts).Cast<MunicipalAccount>().Count());  // Reduced from 25
            Assert.All(((System.Collections.IEnumerable)vm.Accounts).Cast<MunicipalAccount>(), account => Assert.Equal("Cash", account.TypeDescription));
        }
    }
}
