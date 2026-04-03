using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using FluentAssertions;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Models.Validators;
using Xunit;

namespace WileyWidget.LayerProof.Tests.Models;

[Trait("Category", "Models")]
[Trait("Category", "LayerProof")]
public sealed class ModelsContractAndBehaviorProofTests
{
    [Fact]
    public void ChatMessage_Factories_And_Aliases_PreserveSharedChatContract()
    {
        var userMessage = ChatMessage.CreateUserMessage("hello");
        var aiMessage = ChatMessage.CreateAIMessage("response");

        userMessage.IsUser.Should().BeTrue();
        userMessage.Message.Should().Be("hello");
        userMessage.Content.Should().Be("hello");
        userMessage.Text.Should().Be("hello");

        userMessage.Content = null!;
        userMessage.Text = "updated";
        userMessage.DateTime = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);
        userMessage.Metadata["channel"] = "jarvis";

        userMessage.Message.Should().Be("updated");
        userMessage.Timestamp.Should().Be(new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc));
        userMessage.Metadata.Should().ContainKey("channel").WhoseValue.Should().Be("jarvis");

        aiMessage.IsUser.Should().BeFalse();
        aiMessage.Message.Should().Be("response");
        aiMessage.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EnterpriseSnapshot_And_TrendPoint_ComputeFinancialSummaries()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        try
        {
            var snapshot = new EnterpriseSnapshot
            {
                Name = "Water",
                Revenue = 1200m,
                Expenses = 1000m,
            };

            var trendPoint = new EnterpriseMonthlyTrendPoint
            {
                MonthStart = new DateTime(2026, 7, 1),
                Revenue = 100m,
                Expenses = 80m,
            };

            snapshot.NetPosition.Should().Be(200m);
            snapshot.BreakEvenRatio.Should().Be(120d);
            snapshot.IsSelfSustaining.Should().BeTrue();
            snapshot.CrossSubsidyNote.Should().Be("Self-funded");
            snapshot.TrendNarrative.Should().Contain("unavailable");

            trendPoint.NetPosition.Should().Be(20m);
            trendPoint.MonthLabel.Should().Be("Jul 26");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void AccountNumber_Enforces_Format_Hierarchy_And_ValueEquality()
    {
        var accountNumber = new AccountNumber("410.2.1");
        var equalAccountNumber = new AccountNumber("410.2.1");
        var rootAccountNumber = new AccountNumber("410");

        accountNumber.Value.Should().Be("410.2.1");
        accountNumber.Level.Should().Be(3);
        accountNumber.ParentNumber.Should().Be("410.2");
        accountNumber.IsParent.Should().BeFalse();
        accountNumber.GetParentNumber().Should().Be("410.2");
        accountNumber.Equals(equalAccountNumber).Should().BeTrue();
        accountNumber.GetHashCode().Should().Be(equalAccountNumber.GetHashCode());

        rootAccountNumber.Level.Should().Be(1);
        rootAccountNumber.ParentNumber.Should().BeNull();
        rootAccountNumber.IsParent.Should().BeFalse();

        var act = () => new AccountNumber("ABC-410");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MunicipalAccount_ComputedFields_And_PropertyChanged_StayAligned()
    {
        var account = new MunicipalAccount();
        var changedProperties = new List<string>();
        account.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                changedProperties.Add(args.PropertyName!);
            }
        };

        account.AccountNumber = new AccountNumber("410.1");
        account.Name = "Water Revenue";
        account.BudgetAmount = 1000m;
        account.Balance = -250m;
        account.FundType = MunicipalFundType.Enterprise;
        account.TypeDescription = "  Revenue  ";

        account.DisplayName.Should().Be("410.1 - Water Revenue");
        account.Variance.Should().Be(1250m);
        account.VariancePercent.Should().Be(125m);
        account.FormattedBalance.Should().Be("($250.00)");
        account.FundClass.Should().Be(FundClass.Proprietary);
        account.TypeDescription.Should().Be("Revenue");

        changedProperties.Should().Contain(new[]
        {
            nameof(MunicipalAccount.AccountNumber),
            nameof(MunicipalAccount.DisplayName),
            nameof(MunicipalAccount.Name),
            nameof(MunicipalAccount.BudgetAmount),
            nameof(MunicipalAccount.Variance),
            nameof(MunicipalAccount.VariancePercent),
            nameof(MunicipalAccount.Balance),
            nameof(MunicipalAccount.FormattedBalance),
            nameof(MunicipalAccount.FundType),
        });
    }

    [Fact]
    public void Enterprise_DomainBehavior_ComputesRates_Recommendations_And_TrimmedStrings()
    {
        var enterprise = new Enterprise
        {
            Name = "Water",
            Description = "  Core utility  ",
            Notes = "  Requires capital planning  ",
            Type = "  Utility  ",
            CurrentRate = 7m,
            CitizenCount = 100,
            MonthlyExpenses = 900m,
        };

        enterprise.MonthlyRevenue.Should().Be(700m);
        enterprise.MonthlyBalance.Should().Be(-200m);
        enterprise.BreakEvenRate.Should().Be(9m);
        enterprise.IsProfitable().Should().BeFalse();
        enterprise.CalculateRateAdjustmentForTarget(100m).Should().Be(3m);
        enterprise.ProjectAnnualRevenue().Should().Be(8400m);
        enterprise.ProjectAnnualExpenses().Should().Be(10800m);
        enterprise.CalculateBreakEvenVariance().Should().Be(-2m);
        enterprise.GetRateRecommendation().Should().Contain("Immediate rate adjustment required");
        enterprise.Description.Should().Be("Core utility");
        enterprise.Notes.Should().Be("Requires capital planning");
        enterprise.Type.Should().Be("Utility");

        enterprise.ValidateRateChange(20m, out var warningMessage).Should().BeTrue();
        warningMessage.Should().Contain("exceeds typical adjustment range");

        enterprise.ValidateRateChange(-1m, out var errorMessage).Should().BeFalse();
        errorMessage.Should().Be("Rate cannot be negative");
    }

    [Fact]
    public void Enterprise_UpdateMeterReading_Rejects_BackwardData_And_Shifts_PreviousValues()
    {
        var enterprise = new Enterprise
        {
            Name = "Water",
            CurrentRate = 8m,
            CitizenCount = 100,
            MonthlyExpenses = 700m,
        };

        enterprise.UpdateMeterReading(120m, new DateTime(2026, 3, 1), out var initialError).Should().BeTrue();
        initialError.Should().BeNull();
        enterprise.MeterReading.Should().Be(120m);
        enterprise.PreviousMeterReading.Should().BeNull();
        enterprise.WaterConsumption.Should().BeNull();

        enterprise.UpdateMeterReading(150m, new DateTime(2026, 4, 1), out var secondError).Should().BeTrue();
        secondError.Should().BeNull();
        enterprise.MeterReading.Should().Be(150m);
        enterprise.PreviousMeterReading.Should().Be(120m);
        enterprise.WaterConsumption.Should().Be(30m);

        enterprise.UpdateMeterReading(140m, new DateTime(2026, 5, 1), out var readingError).Should().BeFalse();
        readingError.Should().Contain("cannot be less than previous reading");

        enterprise.UpdateMeterReading(160m, new DateTime(2026, 3, 15), out var dateError).Should().BeFalse();
        dateError.Should().Contain("cannot be before previous read date");
    }

    [Fact]
    public void BudgetEntry_ComputedProperties_PreserveBudgetMath_And_EntityClassification()
    {
        var townEntry = new BudgetEntry
        {
            BudgetedAmount = 1000m,
            ActualAmount = 250m,
            Variance = 750m,
            Description = "Fallback Description",
            FundType = FundType.GeneralFund,
            Fund = new Fund { Name = "Town of Wiley General Fund" },
            Department = new Department { Name = "Utilities" },
            MunicipalAccount = new MunicipalAccount { Name = "Water Revenue", Type = AccountType.Revenue },
        };

        var wsdEntry = new BudgetEntry
        {
            BudgetedAmount = 500m,
            ActualAmount = 300m,
            FundType = FundType.SpecialRevenue,
            Fund = new Fund { Name = "Wiley Sanitation District" },
        };

        townEntry.TotalBudget.Should().Be(1000m);
        townEntry.ActualSpent.Should().Be(250m);
        townEntry.Remaining.Should().Be(750m);
        townEntry.PercentOfBudget.Should().Be(25m);
        townEntry.PercentOfBudgetFraction.Should().Be(0.25m);
        townEntry.RemainingAmount.Should().Be(750m);
        townEntry.PercentRemainingFraction.Should().Be(0.75m);
        townEntry.EntityName.Should().Be("Town of Wiley General Fund");
        townEntry.AccountName.Should().Be("Water Revenue");
        townEntry.AccountTypeName.Should().Be("Revenue");
        townEntry.DepartmentName.Should().Be("Utilities");
        townEntry.FundTypeDescription.Should().Be(nameof(FundType.GeneralFund));
        townEntry.VarianceAmount.Should().Be(750m);
        townEntry.VariancePercentage.Should().Be(0.75m);
        townEntry.TownOfWileyBudgetedAmount.Should().Be(1000m);
        townEntry.TownOfWileyActualAmount.Should().Be(250m);
        townEntry.WsdBudgetedAmount.Should().Be(0m);
        townEntry.AccountName = "Edited Description";
        townEntry.Description.Should().Be("Edited Description");

        wsdEntry.TownOfWileyBudgetedAmount.Should().Be(0m);
        wsdEntry.WsdBudgetedAmount.Should().Be(500m);
        wsdEntry.WsdActualAmount.Should().Be(300m);
    }

    [Fact]
    public void ModelValidators_EnforceDomainInvariants()
    {
        var accountTypeValidator = new AccountTypeValidator();
        var enterpriseValidator = new EnterpriseValidator();
        var budgetDataValidator = new BudgetDataValidator();

        accountTypeValidator.ValidateAccountTypeForNumber(AccountType.Cash, "101.100").Should().BeTrue();
        accountTypeValidator.ValidateAccountTypeForFund(AccountType.Sales, FundClass.Governmental).Should().BeFalse();
        accountTypeValidator.GetValidAccountTypesForNumber("510.100").Should().Contain(AccountType.Services);
        accountTypeValidator.GetValidAccountTypesForFund(FundClass.Proprietary).Should().Contain(AccountType.Revenue);

        var invalidEnterprise = new Enterprise
        {
            Name = string.Empty,
            CurrentRate = 0m,
            MonthlyExpenses = -1m,
            CitizenCount = 0,
        };
        var validBudgetData = new BudgetData
        {
            EnterpriseId = 4,
            FiscalYear = 2026,
            TotalBudget = 1000m,
            TotalExpenditures = 250m,
            RemainingBudget = 750m,
        };
        var invalidBudgetData = new BudgetData
        {
            EnterpriseId = 0,
            FiscalYear = 1999,
            TotalBudget = 1000m,
            TotalExpenditures = 250m,
            RemainingBudget = 700m,
        };

        enterpriseValidator.Validate(invalidEnterprise).IsValid.Should().BeFalse();
        budgetDataValidator.Validate(validBudgetData).IsValid.Should().BeTrue();
        budgetDataValidator.Validate(invalidBudgetData).IsValid.Should().BeFalse();
    }

    [Fact]
    public void BudgetInsights_And_ComplianceReport_Update_From_EnterpriseData()
    {
        var enterprises = new ObservableCollection<Enterprise>
        {
            new()
            {
                Name = "Water",
                CurrentRate = 10m,
                CitizenCount = 100,
                MonthlyExpenses = 900m,
            },
            new()
            {
                Name = "Sewer",
                CurrentRate = 1m,
                CitizenCount = 100,
                MonthlyExpenses = 1500m,
            },
        };

        var insights = new BudgetInsights
        {
            Enterprises = enterprises,
        };

        insights.VarianceAnalysis.Should().ContainKey("Water").WhoseValue.Should().Be(1d);
        insights.VarianceAnalysis.Should().ContainKey("Sewer").WhoseValue.Should().Be(-14d);
        insights.TrendProjections.Should().HaveCount(12);
        insights.Summary.Should().Contain("Total Enterprises: 2");
        insights.Summary.Should().Contain("Total Revenue");
        insights.StatisticalSummaries.Count.Should().Be(2);
        insights.KPIs.Should().Contain(kpi => kpi.Name == "Total Revenue" && kpi.Value == 1100d);

        var complianceReport = new ComplianceReport
        {
            Enterprises = enterprises,
        };
        complianceReport.UpdateCompliance();

        complianceReport.ComplianceItems.Should().HaveCount(5);
        complianceReport.OverallStatus.Should().Be(ComplianceStatus.NonCompliant);
        complianceReport.Recommendations.Should().Contain(item => item.Contains("Compliance Issues Detected", StringComparison.Ordinal));
        complianceReport.Recommendations.Should().Contain(item => item.Contains("Sewer - Budget Variance Compliance", StringComparison.Ordinal));
    }
}
