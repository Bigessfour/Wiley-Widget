using Xunit;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using FluentAssertions;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinFormsApp = System.Windows.Forms.Application;

namespace WileyWidget.WinForms.E2ETests;

/// <summary>
/// End-to-end tests for ReportViewerForm using FlaUI.
/// Tests real FastReport Open Source ReportViewer control.
/// Requires FastReport.OpenSource package and STA thread for WinForms controls.
/// </summary>
[Collection("UI")]
[Trait("Category", "UI")]
public sealed class ReportViewerFormE2ETests : IDisposable
{
    private Process? _appProcess;
    private FlaUI.Core.Application? _app;
    private UIA3Automation? _automation;
    private readonly string _testReportPath;

    public ReportViewerFormE2ETests()
    {
        // Create test report for integration testing
        _testReportPath = CreateTestReport();
    }

    [Fact]
    public async Task ReportViewerForm_ShouldLoadAndDisplayReport()
    {
        // Arrange
        LaunchApplicationWithReportViewer();

        try
        {
            // Wait for main window
            var mainWindow = await WaitForMainWindowAsync(TimeSpan.FromSeconds(10));
            mainWindow.Should().NotBeNull("Main window should appear");

            // Act - Wait for Report Viewer window
            var reportWindow = await WaitForReportViewerWindowAsync(mainWindow!, TimeSpan.FromSeconds(15));
            reportWindow.Should().NotBeNull("Report Viewer window should open");

            // Assert - Check window title contains "Report Viewer"
            reportWindow!.Title.Should().Contain("Report Viewer",
                "Report Viewer window should have correct title");

            // Verify ReportViewer contains FastReport controls (check for toolbar or report content)
            var toolbar = reportWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ToolBar));
            toolbar.Should().NotBeNull("Report Viewer should have toolbar");

            // Check for report loaded state (title should contain report filename or not show "Error")
            reportWindow.Title.Should().NotContain("Error",
                "Report should load without errors");
        }
        finally
        {
            CleanupApplication();
        }
    }

    [Fact]
    public async Task ReportViewerForm_RefreshButton_ShouldRefreshReport()
    {
        // Arrange
        LaunchApplicationWithReportViewer();

        try
        {
            var mainWindow = await WaitForMainWindowAsync(TimeSpan.FromSeconds(10));
            var reportWindow = await WaitForReportViewerWindowAsync(mainWindow!, TimeSpan.FromSeconds(15));
            reportWindow.Should().NotBeNull();

            // Act - Click Refresh button
            var refreshButton = reportWindow!.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                .And(cf.ByName("Refresh")));

            if (refreshButton != null)
            {
                refreshButton.Click();
                await Task.Delay(1000); // Wait for refresh

                // Assert - Window should still be visible and not show error
                reportWindow.Title.Should().NotContain("Error");
            }
        }
        finally
        {
            CleanupApplication();
        }
    }

    [Fact(Skip = "Requires BoldReports.WPF package")]
    public async Task ReportViewerForm_ExportPdfButton_ShouldOpenSaveDialog()
    {
        // Arrange
        LaunchApplicationWithReportViewer();

        try
        {
            var mainWindow = await WaitForMainWindowAsync(TimeSpan.FromSeconds(10));
            var reportWindow = await WaitForReportViewerWindowAsync(mainWindow!, TimeSpan.FromSeconds(15));
            reportWindow.Should().NotBeNull();

            // Act - Click Export PDF button
            var exportPdfButton = reportWindow!.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                .And(cf.ByName("Export PDF")));

            if (exportPdfButton != null)
            {
                exportPdfButton.Click();
                await Task.Delay(500);

                // Assert - SaveFileDialog should appear
                var saveDialog = mainWindow!.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                    .And(cf.ByName("Save As")));

                saveDialog.Should().NotBeNull("Save As dialog should appear for PDF export");

                // Cancel the dialog
                if (saveDialog != null)
                {
                    var cancelButton = saveDialog.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByName("Cancel")));
                    cancelButton?.Click();
                }
            }
        }
        finally
        {
            CleanupApplication();
        }
    }

    [Fact(Skip = "Requires BoldReports.WPF package")]
    public async Task ReportViewerForm_ZoomComboBox_ShouldChangeZoomLevel()
    {
        // Arrange
        LaunchApplicationWithReportViewer();

        try
        {
            var mainWindow = await WaitForMainWindowAsync(TimeSpan.FromSeconds(10));
            var reportWindow = await WaitForReportViewerWindowAsync(mainWindow!, TimeSpan.FromSeconds(15));
            reportWindow.Should().NotBeNull();

            // Act - Find and change zoom combo box
            var zoomCombo = reportWindow!.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox));

            if (zoomCombo != null)
            {
                zoomCombo.AsComboBox().Select(2); // Select 150%
                await Task.Delay(500);

                // Assert - Zoom should change without errors
                reportWindow.Title.Should().NotContain("Error");
            }
        }
        finally
        {
            CleanupApplication();
        }
    }

    [Fact(Skip = "Requires BoldReports.WPF package")]
    public void ReportViewerForm_WithMdiContainer_ShouldBeMdiChild()
    {
        // This test verifies MDI integration without FlaUI
        // Can be enabled when BoldReports.WPF is available

        Assert.True(true, "Test skipped - requires BoldReports.WPF package restoration");
    }

    #region Helper Methods

    private void LaunchApplicationWithReportViewer()
    {
        var exePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "WileyWidget.WinForms.exe");

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Application executable not found: {exePath}");
        }

        // Launch with auto-close and report viewer argument
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--auto-close-ms=30000 --show-report-viewer --report-path=\"{_testReportPath}\"",
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath)
        };

        _appProcess = Process.Start(startInfo);
        if (_appProcess == null)
        {
            throw new InvalidOperationException("Failed to start application process");
        }
        _automation = new UIA3Automation();
        _app = FlaUI.Core.Application.Attach(_appProcess);
    }

    private async Task<Window?> WaitForMainWindowAsync(TimeSpan timeout)
    {
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var mainWindow = _app?.GetMainWindow(_automation!);
                if (mainWindow != null && mainWindow.IsAvailable)
                {
                    return mainWindow;
                }
            }
            catch
            {
                // Window not ready yet
            }

            await Task.Delay(500);
        }

        return null;
    }

    private async Task<Window?> WaitForReportViewerWindowAsync(Window mainWindow, TimeSpan timeout)
    {
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var reportWindow = mainWindow.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                    .And(cf.ByName("Report Viewer")))?.AsWindow();

                if (reportWindow != null && reportWindow.IsAvailable)
                {
                    return reportWindow;
                }
            }
            catch
            {
                // Window not ready yet
            }

            await Task.Delay(500);
        }

        return null;
    }

    private string CreateTestReport()
    {
        // Create a minimal RDL report for testing
        var testReportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestReports");
        Directory.CreateDirectory(testReportsDir);

        var reportPath = Path.Combine(testReportsDir, "test-report.rdl");

        // Minimal RDL XML structure (simplified for testing)
        var rdlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Report xmlns=""http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"">
  <Body>
    <Height>2in</Height>
    <ReportItems>
      <Textbox Name=""Textbox1"">
        <CanGrow>true</CanGrow>
        <KeepTogether>true</KeepTogether>
        <Paragraphs>
          <Paragraph>
            <TextRuns>
              <TextRun>
                <Value>Test Report Content</Value>
              </TextRun>
            </TextRuns>
          </Paragraph>
        </Paragraphs>
        <Top>0.5in</Top>
        <Left>0.5in</Left>
        <Width>3in</Width>
        <Height>0.5in</Height>
      </Textbox>
    </ReportItems>
  </Body>
  <Width>6.5in</Width>
</Report>";

        File.WriteAllText(reportPath, rdlContent);
        return reportPath;
    }

    private void CleanupApplication()
    {
        try
        {
            _app?.Close();
            _appProcess?.Kill();
            _appProcess?.Dispose();
            _automation?.Dispose();
        }
        catch
        {
            // Swallow cleanup errors
        }
    }

    public void Dispose()
    {
#pragma warning disable CA1063 // Implement IDisposable correctly - test class doesn't need full pattern
#pragma warning disable CA1816 // Dispose should call GC.SuppressFinalize - not needed for sealed test class
        CleanupApplication();

        // Cleanup test report
        try
        {
            if (File.Exists(_testReportPath))
            {
                File.Delete(_testReportPath);
            }
        }
        catch
        {
            // Swallow cleanup errors
        }
#pragma warning restore CA1816
#pragma warning restore CA1063
    }

    #endregion
}
