using FluentAssertions;
using WileyWidget.Business.Configuration;
using WileyWidget.Models;

namespace WileyWidget.LayerProof.Tests;

public sealed class AccountTypeValidatorTests
{
    private readonly AccountTypeValidator _validator = new();

    [Fact]
    public void ValidateAccountTypeForNumber_AllowsGovernmentRevenueRange()
    {
        var isValid = _validator.ValidateAccountTypeForNumber(AccountType.Taxes, "410.20");

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAccount_ReturnsErrorForMismatchedAccountRange()
    {
        var account = new MunicipalAccount
        {
            Id = 17,
            Name = "Water Revenue Misclassified",
            DepartmentId = 1,
            BudgetPeriodId = 1,
            AccountNumber = new AccountNumber("410"),
            Type = AccountType.Utilities,
            FundType = MunicipalFundType.General,
        };

        var errors = _validator.ValidateAccount(account);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("not valid for account number '410.00'");
    }

    [Fact]
    public void GetValidAccountTypesForFund_ExcludesTaxesFromProprietaryFunds()
    {
        var validTypes = _validator.GetValidAccountTypesForFund(FundClass.Proprietary).ToArray();

        validTypes.Should().NotContain(AccountType.Taxes);
        validTypes.Should().Contain(AccountType.Utilities);
    }
}