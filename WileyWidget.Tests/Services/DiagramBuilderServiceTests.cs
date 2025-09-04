using System.Collections.Generic;
using FluentAssertions;
using Syncfusion.UI.Xaml.Diagram;
using WileyWidget.Models;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests.Services;

public class DiagramBuilderServiceTests
{
    [Fact]
    public void BuildEnterpriseDiagram_NoEnterprises_ReturnsEmpty()
    {
        var svc = new DiagramBuilderService();
        var (nodes, connectors) = svc.BuildEnterpriseDiagram(new List<Enterprise>(), new List<BudgetInteraction>());
        nodes.Should().BeEmpty();
        connectors.Should().BeEmpty();
    }

    [Fact]
    public void BuildEnterpriseDiagram_SimpleTwoEnterprises_InteractionCreatesConnector()
    {
        var svc = new DiagramBuilderService();
        var enterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "A", CitizenCount = 10, CurrentRate = 10, MonthlyExpenses = 50 },
            new Enterprise { Id = 2, Name = "B", CitizenCount = 20, CurrentRate = 10, MonthlyExpenses = 120 }
        };
        var interactions = new List<BudgetInteraction>
        {
            new BudgetInteraction { PrimaryEnterpriseId = 1, SecondaryEnterpriseId = 2, InteractionType = "Transfer", MonthlyAmount = 75 }
        };

        var (nodes, connectors) = svc.BuildEnterpriseDiagram(enterprises, interactions);
        nodes.Should().HaveCount(2);
        connectors.Should().HaveCount(1);
        connectors.Should().OnlyContain(c => c.Content!.ToString()!.Contains("Transfer"));
    }
}
