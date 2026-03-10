using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Reports;

public sealed class ReportTemplateConsistencyTests
{
    [Theory]
    [InlineData("src/WileyWidget.WinForms/Reports/BudgetComparison.frx")]
    [InlineData("publish/release-candidate/Reports/BudgetComparison.frx")]
    public void BudgetComparisonTemplate_DoesNotUseLegacyWrapperNodes(string relativePath)
    {
        var templatePath = Path.Combine(FindSolutionRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(templatePath).Should().BeTrue();

        var document = XDocument.Load(templatePath);
        var legacyNodeNames = document
            .Descendants()
            .Select(element => element.Name.LocalName)
            .Where(name => name is "Parameters" or "TableDataSources" or "Columns")
            .ToList();

        legacyNodeNames.Should().BeEmpty();
    }

    private static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WileyWidget.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WileyWidget.sln from the test base directory.");
    }
}