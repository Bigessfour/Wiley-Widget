using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using WileyWidget.Business.Services;
using WileyWidget.Models;
using WileyWidget.Abstractions.Models;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    public class AccountMapperTests
    {
        [Fact]
        public void MapToDisplay_MapsSingleAccount_WithNullAccountNumberAndMissingDepartment()
        {
            // Arrange
            var mapper = new AccountMapper();
            var domain = new MunicipalAccount
            {
                Id = 1,
                AccountNumber = null,
                Name = null,
                FundDescription = null,
                Department = null,
                ParentAccountId = null,
                Balance = 123.45m,
                BudgetAmount = 200m,
                IsActive = true,
                Type = AccountType.Asset,
                Fund = MunicipalFundType.General
            };

            // Act
            var display = mapper.MapToDisplay(domain);

            // Assert
            display.AccountNumber.Should().Be("N/A");
            display.Name.Should().Be("(Unnamed)");
            display.Department.Should().Be("(Unassigned)");
            display.HasParent.Should().BeFalse();
            display.Balance.Should().Be(domain.Balance);
            display.BudgetAmount.Should().Be(domain.BudgetAmount);
        }

        [Fact]
        public void MapToDisplay_MapsEnumerable_CreatesCorrectCount()
        {
            // Arrange
            var mapper = new AccountMapper();
            var list = new List<MunicipalAccount>
            {
                new MunicipalAccount { Id = 1, AccountNumber = new AccountNumber("100"), Name = "A", Department = new Department { Name = "D" }, Balance = 1m, BudgetAmount = 2m, IsActive=true, Type = AccountType.Asset, Fund = MunicipalFundType.General },
                new MunicipalAccount { Id = 2, AccountNumber = new AccountNumber("101"), Name = "B", Department = new Department { Name = "E" }, Balance = 3m, BudgetAmount = 4m, IsActive=true, Type = AccountType.Revenue, Fund = MunicipalFundType.Enterprise }
            };

            // Act
            var mapped = mapper.MapToDisplay(list).ToList();

            // Assert
            mapped.Should().HaveCount(2);
            mapped[0].AccountNumber.Should().Be("100");
            mapped[1].Department.Should().Be("E");
        }
    }
}