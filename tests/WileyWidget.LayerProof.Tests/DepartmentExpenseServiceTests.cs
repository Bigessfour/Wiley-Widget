using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.LayerProof.Tests;

public sealed class DepartmentExpenseServiceTests
{
    [Fact]
    public async Task GetDepartmentExpensesAsync_UsesQuickBooksTotalsWhenEnabled()
    {
        var quickBooks = new Mock<IQuickBooksService>();
        quickBooks
            .Setup(service => service.QueryExpensesByDepartmentAsync("Water", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExpenseLine(12.5m),
                new ExpenseLine(7.5m),
            });

        var service = CreateService(true, quickBooks.Object);

        var total = await service.GetDepartmentExpensesAsync("water", new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        total.Should().Be(20.0m);
    }

    [Fact]
    public async Task GetDepartmentExpensesAsync_FallsBackToSampleDataWhenDisabled()
    {
        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        var service = CreateService(false, quickBooks.Object);

        var total = await service.GetDepartmentExpensesAsync("Water", new DateTime(2026, 1, 1), new DateTime(2026, 3, 2));

        total.Should().Be(90000m);
        quickBooks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetDepartmentExpensesAsync_FallsBackToSampleDataWhenQuickBooksThrows()
    {
        var quickBooks = new Mock<IQuickBooksService>();
        quickBooks
            .Setup(service => service.QueryExpensesByDepartmentAsync("Trash", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("QBO unavailable"));

        var service = CreateService(true, quickBooks.Object);

        var total = await service.GetDepartmentExpensesAsync("Trash", new DateTime(2026, 1, 1), new DateTime(2026, 3, 2));

        total.Should().Be(56000m);
    }

    private static DepartmentExpenseService CreateService(bool quickBooksEnabled, IQuickBooksService quickBooksService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = quickBooksEnabled.ToString(),
            })
            .Build();

        return new DepartmentExpenseService(
            Mock.Of<ILogger<DepartmentExpenseService>>(),
            configuration,
            quickBooksService);
    }
}