using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksQueryValidationTests
{
    private static readonly Regex QueryLiteralRegex = new("(?:\\$)?\"(?<query>SELECT \\* FROM [^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex EntityRegex = new("SELECT \\* FROM (?<entity>[A-Za-z]+)", RegexOptions.Compiled);
    private static readonly Regex ReferenceNamePredicateRegex = new("\\b[A-Za-z]+Ref\\.(?:Name|name)\\b", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> DocumentedEntityUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Account"] = "https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account",
        ["CompanyInfo"] = "https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/companyinfo",
        ["Invoice"] = "https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/invoice",
        ["JournalEntry"] = "https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/journalentry",
        ["Purchase"] = "https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/purchase",
    };

    [Fact]
    public void QuickBooksQueryLiterals_AreDiscoverable()
    {
        GetQueryLiterals().Should().NotBeEmpty();
    }

    [Fact]
    public void QuickBooksQueryLiterals_DoNotUseReferenceNamePredicates()
    {
        var offendingQueries = GetQueryLiterals()
            .Where(query => ReferenceNamePredicateRegex.IsMatch(query.Query))
            .Select(query => $"{query.FilePath}: {query.Query}")
            .ToArray();

        offendingQueries.Should().BeEmpty(
            "Intuit IDS queries do not support nested reference-name predicates like DepartmentRef.Name; " +
            "filter by documented fields or fetch a broader set and apply local filtering instead.");
    }

    [Fact]
    public void QuickBooksQueryLiterals_MapToDocumentedEntities()
    {
        var undocumentedQueries = GetQueryLiterals()
            .Select(query =>
            {
                var entityMatch = EntityRegex.Match(query.Query);
                var entityName = entityMatch.Success ? entityMatch.Groups["entity"].Value : string.Empty;
                return new { query.FilePath, query.Query, EntityName = entityName };
            })
            .Where(query => string.IsNullOrWhiteSpace(query.EntityName) || !DocumentedEntityUrls.ContainsKey(query.EntityName))
            .Select(query => $"{query.FilePath}: {query.Query}")
            .ToArray();

        undocumentedQueries.Should().BeEmpty(
            "every QuickBooks query literal should map to an explicit Intuit API entity doc so the validation surface stays reviewable as the integration grows.");
    }

    [Fact]
    public void DocumentedEntityUrls_AreDeveloperDocsLinks()
    {
        DocumentedEntityUrls.Values
            .Should()
            .OnlyContain(url => url.StartsWith("https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<QueryLiteral> GetQueryLiterals()
    {
        var repositoryRoot = FindRepositoryRoot();
        var servicesRoot = Path.Combine(repositoryRoot, "src", "WileyWidget.Services");

        return Directory.EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                Path.GetFileName(path).Contains("QuickBooks", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "IntuitDataServiceAdapter.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(path => ExtractQueryLiterals(repositoryRoot, path))
            .ToArray();
    }

    private static IEnumerable<QueryLiteral> ExtractQueryLiterals(string repositoryRoot, string filePath)
    {
        var content = File.ReadAllText(filePath);
        var relativePath = Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/');

        foreach (Match match in QueryLiteralRegex.Matches(content))
        {
            var query = match.Groups["query"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(query))
            {
                yield return new QueryLiteral(relativePath, query);
            }
        }
    }

    private static string FindRepositoryRoot()
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

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private sealed record QueryLiteral(string FilePath, string Query);
}
