using FluentAssertions;
using WileyWidget.Models;
using WileyWidget.Models.Validators;

namespace WileyWidget.LayerProof.Tests;

public sealed class ModelValidationTests
{
    [Fact]
    public void EnterpriseValidator_AcceptsValidEnterprise()
    {
        var validator = new EnterpriseValidator();
        var enterprise = new Enterprise
        {
            Name = "Water",
            CurrentRate = 18.5m,
            MonthlyExpenses = 12000m,
            CitizenCount = 400,
        };

        var result = validator.Validate(enterprise);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AccountNumber_FormatDisplay_NormalizesNumericRootValues()
    {
        var accountNumber = new AccountNumber("410");

        accountNumber.Value.Should().Be("410.00");
        AccountNumber.GetEquivalentValues("410").Should().Contain(new[] { "410", "410.00", "410.0" });
    }
}