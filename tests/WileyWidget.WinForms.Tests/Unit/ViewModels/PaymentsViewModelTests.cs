using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class PaymentsViewModelTests
{
    [Fact]
    public async Task LoadPaymentsAsync_LoadsBudgetAccountOptionsAndHistoricalMappings()
    {
        var paymentRepository = new Mock<IPaymentRepository>();
        var accountRepository = new Mock<IMunicipalAccountRepository>();
        var logger = new Mock<ILogger<PaymentsViewModel>>();

        paymentRepository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Payment
                {
                    Id = 1,
                    CheckNumber = "1001",
                    PaymentDate = new DateTime(2026, 3, 9),
                    Payee = "Acme Supplies",
                    Amount = 125.50m,
                    Description = "Office materials",
                    MunicipalAccountId = 42,
                }
            });

        accountRepository
            .Setup(repository => repository.GetBudgetAccountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MunicipalAccount
                {
                    Id = 7,
                    Name = "Water Operations",
                    AccountNumber = new AccountNumber("101-1000-000"),
                    IsActive = true,
                    BudgetAmount = 1000m,
                }
            });

        var viewModel = new PaymentsViewModel(paymentRepository.Object, accountRepository.Object, logger.Object);

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);

        viewModel.BudgetAccountOptions.Select(option => option.AccountId)
            .Should()
            .Contain(new int?[] { null, 7, 42 });
        viewModel.BudgetAccountOptions.Should()
            .Contain(option => option.AccountId == 42 && option.Display.Contains("Historical Account #42", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdatePaymentBudgetAccountAsync_PersistsSelectedAccount()
    {
        var paymentRepository = new Mock<IPaymentRepository>();
        var accountRepository = new Mock<IMunicipalAccountRepository>();
        var logger = new Mock<ILogger<PaymentsViewModel>>();
        var budgetAccount = new MunicipalAccount
        {
            Id = 7,
            Name = "Water Operations",
            AccountNumber = new AccountNumber("101-1000-000"),
            IsActive = true,
            BudgetAmount = 1000m,
        };

        paymentRepository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Payment
                {
                    Id = 1,
                    CheckNumber = "1002",
                    PaymentDate = new DateTime(2026, 3, 9),
                    Payee = "Acme Supplies",
                    Amount = 99.95m,
                    Description = "Field repair",
                }
            });
        paymentRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        accountRepository
            .Setup(repository => repository.GetBudgetAccountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { budgetAccount });

        var viewModel = new PaymentsViewModel(paymentRepository.Object, accountRepository.Object, logger.Object);
        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);

        var payment = viewModel.Payments.Single();

        await viewModel.UpdatePaymentBudgetAccountAsync(payment, budgetAccount.Id, CancellationToken.None);

        payment.MunicipalAccountId.Should().Be(budgetAccount.Id);
        payment.MunicipalAccount.Should().BeSameAs(budgetAccount);
        paymentRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Payment>(candidate => candidate.Id == payment.Id && candidate.MunicipalAccountId == budgetAccount.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}