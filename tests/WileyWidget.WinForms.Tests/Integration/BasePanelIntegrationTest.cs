using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Syncfusion.Windows.Forms.Tools;
using Xunit;
using Xunit.Abstractions;
using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Tests.Integration;

/// <summary>
/// Test collection for integration tests that share a single app instance.
/// Prevents multiple WileyWidget.exe processes from running simultaneously.
/// </summary>
[CollectionDefinition("WileyWidgetIntegration")]
public class WileyWidgetIntegrationCollection : ICollectionFixture<WileyWidgetIntegrationFixture>
{
}

/// <summary>
/// Shared fixture for integration tests - manages app lifecycle.
/// </summary>
public class WileyWidgetIntegrationFixture : IDisposable
{
    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Reusable integration test base for ANY ScopedPanelBase panel.
/// STA-aware, launches full WileyWidget.exe, navigates via ribbon, uses FlaUI.
/// Steve — this is the pro template we'll use for every panel going forward.
/// </summary>
[Collection("WileyWidgetIntegration")]
public abstract class BasePanelIntegrationTest<TPanel> : IAsyncLifetime, IDisposable
    where TPanel : ScopedPanelBase
{
    protected readonly ITestOutputHelper _output;
    protected readonly WileyWidgetIntegrationFixture _fixture;
    protected FlaUI.Core.Application? _app;
    protected Window? _mainWindow;
    protected AutomationBase? _automation;
    protected TPanel? _panel; // cast after navigation

    private Process? _process;
    private readonly string _artifactsRoot;

    protected BasePanelIntegrationTest(ITestOutputHelper output, WileyWidgetIntegrationFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _artifactsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestArtifacts");
        Directory.CreateDirectory(_artifactsRoot);
    }

    public async Task InitializeAsync()
    {
        // Launch full app in TEST mode (uses InMemory or Test DB)
        string config = "Debug";
#if !DEBUG
        config = "Release";
#endif
        var exePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", config, "net10.0-windows", "WileyWidget.WinForms.exe"));

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"WileyWidget.WinForms.exe not found at {exePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--testmode", // your app checks this and sets TestHarness=true
            UseShellExecute = true
        };

        _process = Process.Start(startInfo)!;
        _output.WriteLine($"Process started with ID {_process.Id}");

        if (_process.HasExited)
        {
            _output.WriteLine($"Process exited immediately with code {_process.ExitCode}");
            throw new InvalidOperationException($"Process exited prematurely with code {_process.ExitCode}");
        }

        await Task.Delay(8000); // give splash + ribbon time (adjust for your machine)

        if (_process.HasExited)
        {
            _output.WriteLine($"Process exited during delay with code {_process.ExitCode}");
            throw new InvalidOperationException($"Process exited during startup delay with code {_process.ExitCode}");
        }

        _automation = new UIA3Automation();
        _app = FlaUI.Core.Application.Attach(_process.Id);
        _mainWindow = _app.GetMainWindow(_automation);

        _output.WriteLine($"🚀 App launched — MainWindow Title: {_mainWindow?.Title ?? "null"}");
    }

    public Task DisposeAsync()
    {
        _app?.Dispose();
        _automation?.Dispose();
        _process?.Kill(true);
        return Task.CompletedTask;
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    // ====================== NAVIGATION HELPERS ======================

    protected async Task NavigateToPanel(string ribbonTab, string buttonText)
    {
        // Find and click the ribbon tab
        var tab = _mainWindow!.FindFirstDescendant(cf => cf.ByText(ribbonTab));
        if (tab != null)
        {
            await SafeClick(tab, $"{ribbonTab}Tab");
            await Task.Delay(400);
        }

        // Find and click the panel button
        var btn = _mainWindow.FindFirstDescendant(cf => cf.ByText(buttonText));
        if (btn != null)
        {
            await SafeClick(btn, buttonText);
        }
        await Task.Delay(1500); // wait for docking + lazy load
    }

    protected void AssertPanelLoaded(string expectedTitle)
    {
        var header = _mainWindow!.FindFirstDescendant(cf => cf.ByText(expectedTitle));
        Assert.NotNull(header);
        _output.WriteLine($"✅ Panel header verified: {expectedTitle}");
    }

    // ====================== NEW DEBUGGING SUPERPOWERS ======================

    protected void CaptureScreenshot(string name = "failure")
    {
        try
        {
            var screenshot = _mainWindow!.Capture();
            var path = Path.Combine(_artifactsRoot, "Screenshots", $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            screenshot.Save(path);
            _output.WriteLine($"📸 Screenshot saved → {path}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ Screenshot failed: {ex.Message}");
        }
    }

    protected void DumpWindowHierarchy(string context = "")
    {
        try
        {
            var path = Path.Combine(_artifactsRoot, "Hierarchy", $"{context}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var sw = new StreamWriter(path);
            sw.WriteLine($"=== UI HIERARCHY DUMP @ {DateTime.Now} === {context}");
            DumpElement(_mainWindow!, sw, 0);
            _output.WriteLine($"📋 Hierarchy dumped → {path}");
        }
        catch { }
    }

    private void DumpElement(AutomationElement el, StreamWriter sw, int indent)
    {
        var pad = new string(' ', indent * 2);
        sw.WriteLine($"{pad}├─ {el.Name} | {el.ControlType} | AutomationId={el.AutomationId} | IsEnabled={el.IsEnabled}");
        foreach (var child in el.FindAllChildren())
            DumpElement(child, sw, indent + 1);
    }

    protected async Task SafeClick(AutomationElement element, string elementName)
    {
        try
        {
            element.Click();
            await Task.Delay(300);
        }
        catch
        {
            _output.WriteLine($"💥 SafeClick failed on {elementName} — capturing debug info");
            CaptureScreenshot($"SafeClick_Fail_{elementName}");
            DumpWindowHierarchy($"SafeClick_{elementName}");
            throw;
        }
    }

    protected async Task SafeType(AutomationElement element, string text, string elementName)
    {
        try
        {
            element.As<FlaUI.Core.AutomationElements.TextBox>().Text = text;
            await Task.Delay(300);
        }
        catch
        {
            _output.WriteLine($"💥 SafeType failed on {elementName}");
            CaptureScreenshot($"SafeType_Fail_{elementName}");
            DumpWindowHierarchy($"SafeType_{elementName}");
            throw;
        }
    }
}
