using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using FlaUI.Core.AutomationElements; // For AsButton and other element extensions
using Xunit;
using Xunit.Abstractions;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// E2E tests for Municipal Account View with Conservation Trust Fund data.
/// Validates Syncfusion SfDataGrid interactions via FlaUI.
///
/// Test Coverage:
/// - Load 31 Conservation Trust Fund accounts
/// - Filter by Type="Bank" (5 rows)
/// - Validate Type column distribution (Bank/Investment/Cash)
/// - Performance: Grid loads within 10 seconds
///
/// Prerequisites:
/// - WileyWidget.exe must be built in Debug mode
/// - Application must have MunicipalAccountsGrid AutomationId set
/// - Tests run in STA thread (WPF requirement)
/// </summary>
[Collection("E2E Tests")]
public class MunicipalAccountViewE2ETests : WpfTestBase
{
    private const string AppExePath = @"bin\Debug\net9.0-windows\WileyWidget.exe";
    private const string GridAutomationId = "MunicipalAccountsGrid";
    private const int ExpectedTotalAccounts = 31;
    private const int ExpectedBankAccounts = 5;

    private readonly ITestOutputHelper _output;

    public MunicipalAccountViewE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Test: Load Conservation Trust Fund and verify 31 accounts are displayed.
    /// </summary>
    [StaFact(Skip = "E2E test - requires WileyWidget.exe to be built")]
    public void LoadConservationTrustFund_ShouldDisplay31Accounts()
    {
        // Arrange
        var exePath = GetApplicationPath();
        _output.WriteLine($"Launching application from: {exePath}");

        LaunchApplication(exePath, timeoutSeconds: 30);
        _output.WriteLine("Application launched successfully");

        // Act - Navigate to Municipal Accounts view
        var municipalButton = WaitForElement(() => FindElementByAutomationId("MunicipalAccountsButton"), timeoutSeconds: 10);
        municipalButton.Should().NotBeNull("Municipal Accounts button should be visible");
        municipalButton!.AsButton().Click();
        _output.WriteLine("Clicked Municipal Accounts button");

        System.Threading.Thread.Sleep(2000); // Wait for grid to load data

        var dataGrid = WaitForElement(() => FindElementByAutomationId(GridAutomationId), timeoutSeconds: 10);
        dataGrid.Should().NotBeNull("Municipal Accounts grid should be visible after navigation");

        // Assert
        var rowCount = SyncfusionHelpers.GetDataGridRowCount(dataGrid!);
        _output.WriteLine($"Grid row count: {rowCount}");

        rowCount.Should().Be(ExpectedTotalAccounts,
            "Conservation Trust Fund should have 31 accounts loaded from data source");
    }

    /// <summary>
    /// Test: Filter by Type="Bank" and verify 5 rows remain.
    /// </summary>
    [StaFact(Skip = "E2E test - requires WileyWidget.exe to be built")]
    public void FilterByTypeBank_ShouldDisplay5Rows()
    {
        // Arrange
        var exePath = GetApplicationPath();
        LaunchApplication(exePath, timeoutSeconds: 30);

        var municipalButton = WaitForElement(() => FindElementByAutomationId("MunicipalAccountsButton"));
        municipalButton!.AsButton().Click();
        System.Threading.Thread.Sleep(2000);

        var dataGrid = WaitForElement(() => FindElementByAutomationId(GridAutomationId));
        dataGrid.Should().NotBeNull();

        _output.WriteLine("Grid loaded, applying filter...");

        // Act - Apply filter to Type column
        SyncfusionHelpers.ApplyFilter(dataGrid!, "Bank");
        System.Threading.Thread.Sleep(1000); // Wait for filter to apply

        // Assert
        var rowCount = SyncfusionHelpers.GetDataGridRowCount(dataGrid!);
        _output.WriteLine($"Filtered row count: {rowCount}");

        rowCount.Should().Be(ExpectedBankAccounts,
            "Filtering Type='Bank' should show exactly 5 bank accounts");
    }

    /// <summary>
    /// Test: Validate Type column distribution matches expected counts.
    /// Ensures data integrity across Bank/Investment/Cash categories.
    /// </summary>
    [StaFact(Skip = "E2E test - requires WileyWidget.exe to be built")]
    public void ValidateTypeColumnDistribution_ShouldMatchExpectedCounts()
    {
        // Arrange
        var exePath = GetApplicationPath();
        LaunchApplication(exePath, timeoutSeconds: 30);

        var municipalButton = WaitForElement(() => FindElementByAutomationId("MunicipalAccountsButton"));
        municipalButton!.AsButton().Click();
        System.Threading.Thread.Sleep(2000);

        var dataGrid = WaitForElement(() => FindElementByAutomationId(GridAutomationId));
        dataGrid.Should().NotBeNull();

        // Act - Get Type column values (assuming column index 2: Account, Description, Type)
        var typeValues = SyncfusionHelpers.GetColumnValues(dataGrid!, columnIndex: 2);
        _output.WriteLine($"Retrieved {typeValues.Count} type values from grid");

        // Assert - Validate distribution
        var bankCount = typeValues.Count(t => t.Equals("Bank", StringComparison.OrdinalIgnoreCase));
        var investmentCount = typeValues.Count(t => t.Equals("Investment", StringComparison.OrdinalIgnoreCase));
        var cashCount = typeValues.Count(t => t.Equals("Cash", StringComparison.OrdinalIgnoreCase));

        _output.WriteLine($"Bank: {bankCount}, Investment: {investmentCount}, Cash: {cashCount}");

        bankCount.Should().Be(ExpectedBankAccounts, "Should have 5 Bank accounts");
        investmentCount.Should().BeGreaterThan(0, "Should have at least one Investment account");
        cashCount.Should().BeGreaterThan(0, "Should have at least one Cash account");

        var totalCategorized = bankCount + investmentCount + cashCount;
        totalCategorized.Should().Be(ExpectedTotalAccounts,
            $"Total categorized accounts ({totalCategorized}) should equal expected total ({ExpectedTotalAccounts})");
    }

    /// <summary>
    /// Performance test: Ensure grid loads within 10 seconds.
    /// Enterprise requirement for acceptable UI responsiveness.
    /// </summary>
    [StaFact(Skip = "E2E test - requires WileyWidget.exe to be built")]
    public void GridLoad_ShouldCompleteWithin10Seconds()
    {
        // Arrange
        var exePath = GetApplicationPath();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        LaunchApplication(exePath, timeoutSeconds: 30);
        var municipalButton = WaitForElement(() => FindElementByAutomationId("MunicipalAccountsButton"));
        municipalButton!.AsButton().Click();

        var dataGrid = WaitForElement(() => FindElementByAutomationId(GridAutomationId), timeoutSeconds: 15);
        dataGrid.Should().NotBeNull("Grid should appear within timeout");

        stopwatch.Stop();
        _output.WriteLine($"Grid load time: {stopwatch.ElapsedMilliseconds}ms");

        // Assert - Performance requirement
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "Grid should load within 10 seconds for enterprise performance standards");
    }

    /// <summary>
    /// Test: Verify column headers are correctly displayed.
    /// </summary>
    [StaFact(Skip = "E2E test - requires WileyWidget.exe to be built")]
    public void GridHeaders_ShouldDisplayCorrectColumns()
    {
        // Arrange
        var exePath = GetApplicationPath();
        LaunchApplication(exePath, timeoutSeconds: 30);

        var municipalButton = WaitForElement(() => FindElementByAutomationId("MunicipalAccountsButton"));
        municipalButton!.AsButton().Click();
        System.Threading.Thread.Sleep(2000);

        var dataGrid = WaitForElement(() => FindElementByAutomationId(GridAutomationId));
        dataGrid.Should().NotBeNull();

        // Act
        var headers = SyncfusionHelpers.GetColumnHeaders(dataGrid!);
        _output.WriteLine($"Column headers: {string.Join(", ", headers)}");

        // Assert - Verify expected columns exist
        headers.Should().Contain("Account", "Should have Account column");
        headers.Should().Contain("Description", "Should have Description column");
        headers.Should().Contain("Type", "Should have Type column");
        headers.Should().HaveCountGreaterThan(2, "Should have at least 3 columns");
    }

    /// <summary>
    /// Test: Row selection updates the detail view or selection indicator.
    /// </summary>
    [StaFact(Skip = "E2E test - requires WileyWidget.exe to be built")]
    public void ClickRow_ShouldUpdateSelection()
    {
        // Arrange
        var exePath = GetApplicationPath();
        LaunchApplication(exePath, timeoutSeconds: 30);

        var municipalButton = WaitForElement(() => FindElementByAutomationId("MunicipalAccountsButton"));
        municipalButton!.AsButton().Click();
        System.Threading.Thread.Sleep(2000);

        var dataGrid = WaitForElement(() => FindElementByAutomationId(GridAutomationId));
        dataGrid.Should().NotBeNull();

        // Act - Click first row
        SyncfusionHelpers.ClickRow(dataGrid!, rowIndex: 0);
        System.Threading.Thread.Sleep(500);

        // Assert
        var selectedIndex = SyncfusionHelpers.GetSelectedRowIndex(dataGrid!);
        _output.WriteLine($"Selected row index: {selectedIndex}");

        selectedIndex.Should().Be(0, "First row should be selected after click");
    }

    #region Helper Methods

    /// <summary>
    /// Gets the full path to the WileyWidget.exe application.
    /// Resolves relative path from test project to main application output.
    /// </summary>
    private string GetApplicationPath()
    {
        // Navigate from test output directory to main app output
        var exePath = Path.Combine(
            Environment.CurrentDirectory,
            "..", "..", "..", "..",
            AppExePath);

        var fullPath = Path.GetFullPath(exePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"WileyWidget.exe not found. Build the main application first.\nExpected path: {fullPath}");
        }

        return fullPath;
    }

    #endregion
}
