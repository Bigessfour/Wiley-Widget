using FluentAssertions;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Models;

public sealed class PaymentTests
{
    [Fact]
    public void BudgetAccountDisplay_WhenMunicipalAccountIsLoaded_ReturnsDisplayName()
    {
        var payment = new Payment
        {
            MunicipalAccount = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("410.1"),
                Name = "Office Supplies"
            }
        };

        payment.BudgetAccountDisplay.Should().Be("410.10 - Office Supplies");
    }

    [Fact]
    public void BudgetAccountDisplay_WhenAccountReferenceIsHistorical_ReturnsFallbackText()
    {
        var payment = new Payment
        {
            MunicipalAccountId = 42
        };

        payment.BudgetAccountDisplay.Should().Be("Historical Account #42");
    }

    [Fact]
    public void BudgetAccountDisplay_WhenNoAccountIsAssigned_ReturnsEmptyString()
    {
        var payment = new Payment();

        payment.BudgetAccountDisplay.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenBudgetEntryAlreadyLinked_ReturnsPosted()
    {
        var account = new MunicipalAccount
        {
            Id = 7,
            AccountNumber = new AccountNumber("410.1"),
            Name = "Office Supplies"
        };

        var payment = new Payment
        {
            PaymentDate = new DateTime(2025, 8, 1),
            MunicipalAccountId = 7,
            MunicipalAccount = account,
            Amount = 125m
        };

        var budgetEntry = new BudgetEntry
        {
            Id = 12,
            FiscalYear = 2026,
            AccountNumber = "410.10",
            Description = "Office Supplies",
            MunicipalAccountId = 7
        };

        var resolution = PaymentBudgetPostingResolver.Resolve(payment, account, new[] { budgetEntry });

        resolution.State.Should().Be(PaymentBudgetPostingState.Posted);
        resolution.MatchedBudgetEntry.Should().BeSameAs(budgetEntry);
        resolution.StatusText.Should().Be("Posted");
    }

    [Fact]
    public void Resolve_WhenAccountNumberMatchesSingleUnlinkedBudgetEntry_ReturnsReadyToLink()
    {
        var account = new MunicipalAccount
        {
            Id = 7,
            AccountNumber = new AccountNumber("410.1"),
            Name = "Office Supplies"
        };

        var payment = new Payment
        {
            PaymentDate = new DateTime(2025, 8, 1),
            MunicipalAccountId = 7,
            MunicipalAccount = account,
            Amount = 125m
        };

        var budgetEntry = new BudgetEntry
        {
            Id = 12,
            FiscalYear = 2026,
            AccountNumber = "410.10",
            Description = "Office Supplies"
        };

        var resolution = PaymentBudgetPostingResolver.Resolve(payment, account, new[] { budgetEntry });

        resolution.State.Should().Be(PaymentBudgetPostingState.NeedsReconciliation);
        resolution.CanAutoLinkByAccountNumber.Should().BeTrue();
        resolution.MatchedBudgetEntry.Should().BeSameAs(budgetEntry);
        resolution.StatusText.Should().Be("Ready to link");
    }

    [Fact]
    public void Resolve_WhenAccountNumberMatchesMultipleBudgetEntries_ReturnsMultipleBudgetLines()
    {
        var account = new MunicipalAccount
        {
            Id = 7,
            AccountNumber = new AccountNumber("410.1"),
            Name = "Office Supplies"
        };

        var payment = new Payment
        {
            PaymentDate = new DateTime(2025, 8, 1),
            MunicipalAccountId = 7,
            MunicipalAccount = account,
            Amount = 125m
        };

        var budgetEntries = new[]
        {
            new BudgetEntry
            {
                Id = 12,
                FiscalYear = 2026,
                AccountNumber = "410.10",
                Description = "Office Supplies"
            },
            new BudgetEntry
            {
                Id = 13,
                FiscalYear = 2026,
                AccountNumber = "410.10",
                Description = "Office Supplies Duplicate"
            }
        };

        var resolution = PaymentBudgetPostingResolver.Resolve(payment, account, budgetEntries);

        resolution.State.Should().Be(PaymentBudgetPostingState.MultipleBudgetLines);
        resolution.StatusText.Should().Be("Multiple budget lines");
        resolution.CanAutoLinkByAccountNumber.Should().BeFalse();
    }
}
