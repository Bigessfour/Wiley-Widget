// ── MainForm.Testing.cs ──────────────────────────────────────────────────────────────────────────
// Test-only scaffolding partial.  All members in this file are internal and are ONLY intended for
// use by WileyWidget.WinForms.Tests and WileyWidget.LayoutRegression.Tests (InternalsVisibleTo).
//
// DO NOT reference these members from production code.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test constructor — creates a fully disposed-safe form with minimal stubs.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parameterless constructor for layout / regression tests.
    /// Chains to the primary DI constructor using no-op service stubs so tests
    /// can exercise layout behaviour without a full application host.
    /// </summary>
    internal MainForm()
        : this(
            serviceProvider: null!,
            configuration: new ConfigurationBuilder().Build(),
            logger: NullLogger<MainForm>.Instance,
            reportViewerLaunchOptions: ReportViewerLaunchOptions.Disabled,
            themeService: new NullThemeService(),
            windowStateService: new NullWindowStateService(),
            fileImportService: new NullFileImportService(),
            controlFactory: new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance))
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test helper methods (called from MainFormLayoutTests / integration tests)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal ribbon and adds it to the form's Controls collection.
    /// Call BEFORE <see cref="CreateHandleForTests"/> so all HWNDs are created atomically.
    /// </summary>
    internal void InitializeMinimalChromeForTests()
    {
        if (_ribbon != null) return;

        void QueueTestLayoutSync(string reason)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                SyncContentHostTopInsetToRibbon(reason);
                return;
            }

            try
            {
                // For tests, call synchronously to avoid hanging on BeginInvoke without message pump
                if (!IsDisposed && !Disposing)
                {
                    SyncContentHostTopInsetToRibbon(reason);
                    ConstrainMdiClientToContentHost(reason);
                }
            }
            catch
            {
            }
        }

        var ribbon = new RibbonControlAdv
        {
            Name = "Ribbon_Main",
            Dock = Syncfusion.Windows.Forms.Tools.DockStyleEx.Top,
            Visible = true,
        };

        ribbon.SizeChanged += (_, _) => QueueTestLayoutSync("InitializeMinimalChromeForTests.RibbonSizeChanged");
        ribbon.VisibleChanged += (_, _) => QueueTestLayoutSync("InitializeMinimalChromeForTests.RibbonVisibleChanged");
        ribbon.Layout += (_, _) => QueueTestLayoutSync("InitializeMinimalChromeForTests.RibbonLayout");
        Layout += (_, _) => QueueTestLayoutSync("InitializeMinimalChromeForTests.FormLayout");

        _ribbon = ribbon;
        Controls.Add(ribbon);
    }

    /// <summary>
    /// Creates the native HWND for the form (equivalent to calling <c>CreateHandle()</c>
    /// in test context where the form is never shown).
    /// </summary>
    internal void CreateHandleForTests()
    {
        if (!IsHandleCreated)
        {
            CreateHandle();
        }
    }

    /// <summary>
    /// Creates the <see cref="TabbedMDIManager"/> and the right-dock panel
    /// (<c>_rightDockPanel</c>) so layout assertions can validate their positions.
    /// Call AFTER <see cref="CreateHandleForTests"/>.
    /// </summary>
    internal void InitializeForTests()
    {
        // Wire TabbedMDIManager using the existing production initializer.
        InitializeMDIManager();

        // Create a minimal right-dock panel (320 px, docked right) that mirrors
        // what InitializeLayoutComponents() produces at runtime.
        if (_rightDockPanel == null)
        {
            var host = (_panelHost as Control) ?? this;
            _rightDockPanel = new Panel
            {
                Name = "RightDockPanel",
                Dock = DockStyle.Right,
                Width = 320,
                Visible = true,
            };
            host.Controls.Add(_rightDockPanel);
        }

        if (_ribbon != null && !_ribbon.IsDisposed && Controls.Contains(_ribbon))
        {
            Controls.SetChildIndex(_ribbon, 0);
            _ribbon.BringToFront();
        }

        if (_panelHost != null && !_panelHost.IsDisposed && Controls.Contains(_panelHost))
        {
            Controls.SetChildIndex(_panelHost, Math.Min(1, Controls.Count - 1));
            _panelHost.SendToBack();
        }

        RefreshPanelHostLayout("InitializeForTests", force: true);
        PerformLayout();
        PerformLayoutRecursive(this);
        Application.DoEvents();
        SyncContentHostTopInsetToRibbon("InitializeForTests.PostLayout");
        ConstrainMdiClientToContentHost("InitializeForTests.PostLayout");
        Application.DoEvents();
        ConstrainMdiClientToContentHost("InitializeForTests.AfterDoEvents");
        Update();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test accessors — expose private fields via internal getters
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the live <see cref="RibbonControlAdv"/> instance, or <c>null</c>.</summary>
    internal RibbonControlAdv? GetRibbon() => _ribbon;

    /// <summary>Returns the live <see cref="TabbedMDIManager"/> instance, or <c>null</c>.</summary>
    internal TabbedMDIManager? GetTabbedMDIManager() => _tabbedMdi;

    /// <summary>Returns the right-dock <see cref="Panel"/>, or <c>null</c>.</summary>
    internal Panel? GetJarvisPanel() => _rightDockPanel;

    /// <summary>Returns a compact layout snapshot for test failure diagnostics.</summary>
    internal string GetLayoutSnapshotForTests()
    {
        var mdiClient = GetMdiClientControl();
        return string.Join(
            " | ",
            $"Ribbon={DescribeControl(_ribbon)}",
            $"ContentHost={DescribeControl(_contentHostPanel)} Padding={_contentHostPanel?.Padding}",
            $"MdiClient={DescribeControl(mdiClient)}",
            $"RightDock={DescribeControl(_rightDockPanel)}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Legacy reflection-bypass helpers (MainFormTestHelpers.cs already uses
    // direct reflection, but keep these for any callers relying on the named API)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Exposes <c>InitializeChrome()</c> for tests that cannot use reflection.</summary>
    internal void InvokeInitializeChrome() => InitializeChrome();

    /// <summary>Exposes <c>OnLoad()</c> for tests that cannot use reflection.</summary>
    internal void InvokeOnLoad() => OnLoad(EventArgs.Empty);

    [Conditional("DEBUG")]
    private void TraceLayoutSnapshot(string stage, Control? root = null)
    {
        if (!IsLayoutTraceEnabled())
        {
            return;
        }

        root ??= this;

        try
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine($"=== LAYOUT SNAPSHOT: {stage} ===");
            sb.AppendLine($"Form: Name={Name} HandleCreated={IsHandleCreated} Visible={Visible} Bounds={Bounds} Client={ClientSize}");

            var mdiClient = GetMdiClientControl();
            sb.AppendLine($"Ribbon={DescribeControl(_ribbon)}");
            sb.AppendLine($"PanelHost={DescribeControl(_panelHost)}");
            sb.AppendLine($"MdiClient={DescribeControl(mdiClient)}");
            sb.AppendLine($"RightDock={DescribeControl(_rightDockPanel)}");
            sb.AppendLine($"RightDockSplitter={DescribeControl(_rightDockSplitter)}");
            sb.AppendLine($"JarvisStrip={DescribeControl(_jarvisAutoHideStrip)}");

            AppendDockTopChildren(this, sb, "Form");
            if (_panelHost != null && !_panelHost.IsDisposed)
            {
                AppendDockTopChildren(_panelHost, sb, "PanelHost");
            }

            WriteControlTree(root, sb, depth: 0, maxDepth: 2);

            var snapshot = sb.ToString();
            Debug.WriteLine(snapshot);
            _logger?.LogInformation("{LayoutSnapshot}", snapshot);

            AssertControlStartsBelow(_panelHost, _ribbon, stage, "PanelHost starts below Ribbon");
            AssertControlStartsBelow(mdiClient, _ribbon, stage, "MdiClient starts below Ribbon");
            AssertMdiDoesNotOverlapRightDock(mdiClient, _rightDockPanel, stage);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[LAYOUT-TRACE] Failed snapshot for stage {Stage}", stage);
        }
    }

    private static bool IsLayoutTraceEnabled()
    {
        if (Debugger.IsAttached)
        {
            return true;
        }

        var value = Environment.GetEnvironmentVariable("WILEYWIDGET_LAYOUT_TRACE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeControl(Control? control)
    {
        if (control == null)
        {
            return "<null>";
        }

        return $"{control.Name}<{control.GetType().Name}> Visible={control.Visible} Dock={control.Dock} Bounds={control.Bounds} Parent={control.Parent?.Name ?? "<null>"}";
    }

    private static void AppendDockTopChildren(Control root, StringBuilder sb, string label)
    {
        sb.AppendLine($"{label} Dock=Top Controls:");
        foreach (Control child in root.Controls)
        {
            if (child.IsDisposed || !child.Visible || child.Dock != DockStyle.Top)
            {
                continue;
            }

            sb.AppendLine($"  - {DescribeControl(child)} Z={root.Controls.GetChildIndex(child)}");
        }
    }

    private static void WriteControlTree(Control control, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            return;
        }

        var indent = new string(' ', depth * 2);
        var zIndex = control.Parent is null ? -1 : control.Parent.Controls.GetChildIndex(control);
        sb.AppendLine($"{indent}{control.Name}<{control.GetType().Name}> Visible={control.Visible} Dock={control.Dock} Bounds={control.Bounds} Z={zIndex}");

        foreach (Control child in control.Controls)
        {
            WriteControlTree(child, sb, depth + 1, maxDepth);
        }
    }

    private Rectangle? GetFormClientBounds(Control? control)
    {
        if (control == null || control.IsDisposed || !control.Visible || !IsHandleCreated)
        {
            return null;
        }

        try
        {
            var screenRect = control.RectangleToScreen(control.ClientRectangle);
            return RectangleToClient(screenRect);
        }
        catch
        {
            return null;
        }
    }

    [Conditional("DEBUG")]
    private void AssertControlStartsBelow(Control? lowerControl, Control? upperControl, string stage, string relationship)
    {
        if (!IsLayoutTraceEnabled())
        {
            return;
        }

        var lower = GetFormClientBounds(lowerControl);
        var upper = GetFormClientBounds(upperControl);

        if (!lower.HasValue || !upper.HasValue)
        {
            return;
        }

        if (lower.Value.Top >= upper.Value.Bottom)
        {
            return;
        }

        var message =
            $"[LAYOUT-TRACE] Overlap detected at {stage} ({relationship}) => " +
            $"{lowerControl?.Name ?? "<lower>"}.Top={lower.Value.Top} < {upperControl?.Name ?? "<upper>"}.Bottom={upper.Value.Bottom}";
        Debug.WriteLine(message);
        _logger?.LogWarning("{OverlapMessage}", message);

        if (Debugger.IsAttached)
        {
            Debug.Fail(message);
        }
    }

    [Conditional("DEBUG")]
    private void AssertMdiDoesNotOverlapRightDock(Control? mdiClient, Control? rightDock, string stage)
    {
        if (!IsLayoutTraceEnabled())
        {
            return;
        }

        var mdi = GetFormClientBounds(mdiClient);
        var dock = GetFormClientBounds(rightDock);

        if (!mdi.HasValue || !dock.HasValue)
        {
            return;
        }

        if (mdi.Value.Right <= dock.Value.Left)
        {
            return;
        }

        var message =
            $"[LAYOUT-TRACE] Horizontal overlap at {stage} (MdiClient vs RightDock) => " +
            $"MdiClient.Right={mdi.Value.Right} > RightDock.Left={dock.Value.Left}";
        Debug.WriteLine(message);
        _logger?.LogWarning("{OverlapMessage}", message);

        if (Debugger.IsAttached)
        {
            Debug.Fail(message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private no-op service stubs (used only by the parameterless test ctor)
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class NullThemeService : IThemeService
    {
        public event EventHandler<string>? ThemeChanged { add { } remove { } }
        public string CurrentTheme => "Office2019Colorful";
        public bool IsDark => false;
        public void ApplyTheme(string themeName) { }
        public void ReapplyCurrentTheme() { }
    }

    private sealed class NullWindowStateService : IWindowStateService
    {
        public void RestoreWindowState(Form form) { }
        public void SaveWindowState(Form form) { }
        public List<string> LoadMru() => new();
        public void SaveMru(List<string> mruList) { }
        public void AddToMru(string filePath) { }
        public void ClearMru() { }
    }

    private sealed class NullFileImportService : IFileImportService
    {
        public Task<Result<T>> ImportDataAsync<T>(string filePath, CancellationToken ct = default) where T : class
            => Task.FromResult(Result<T>.Failure("test stub"));

        public Task<Result> ValidateImportFileAsync(string filePath, CancellationToken ct = default)
            => Task.FromResult(Result.Failure("test stub"));
    }
}
