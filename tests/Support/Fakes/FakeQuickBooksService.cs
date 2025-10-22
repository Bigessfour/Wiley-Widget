using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Fakes;

public class FakeQuickBooksService : IQuickBooksService
{
    public Task<IList<MunicipalAccount>> GetChartOfAccountsAsync()
    {
        // Return a small seeded list suitable for tests (25 items expected by UI tests)
        var list = new List<MunicipalAccount>();
        for (int i = 1; i <= 25; i++)
        {
            list.Add(new MunicipalAccount
            {
                Id = i,
                AccountNumber = new AccountNumber { Value = i == 1 ? "110" : $"110.{i - 1}" },
                Name = $"Account {i}",
                Balance = i * 100m,
                FundDescription = i % 2 == 0 ? "General" : "Special",
                TypeDescription = i % 3 == 0 ? "Cash" : "Asset",
                Department = new Department { Name = $"Dept {i % 5}" }
            });
        }

        return Task.FromResult((IList<MunicipalAccount>)list);
    }

    public Task SyncFromQuickBooksAsync(IList<MunicipalAccount> accounts)
    {
        // No-op for tests
        return Task.CompletedTask;
    }
}
