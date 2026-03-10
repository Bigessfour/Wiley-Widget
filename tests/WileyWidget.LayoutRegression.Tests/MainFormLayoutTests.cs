using NUnit.Framework;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using WileyWidget.WinForms.Forms;

[TestFixture]
[Apartment(ApartmentState.STA)]   // REQUIRED for Syncfusion WinForms controls
public sealed class MainFormLayoutTests : IDisposable
{
    private MainForm? _form;

    [SetUp]
    public void Setup()
    {
        _form = new MainForm();

        // Must be MDI container BEFORE handle creation so WinForms creates MdiClient automatically.
        _form.IsMdiContainer = true;

        // Add the ribbon control BEFORE CreateHandle so WinForms creates all child HWNDs
        // atomically in one shot. Adding a Syncfusion control to an already-handled form
        // triggers immediate HWND creation outside of a message pump, which can deadlock
        // Syncfusion's internal SendMessage calls on an STA test thread.
        _form.InitializeMinimalChromeForTests();

        // Now create the form handle. All controls in Controls at this point (including the
        // ribbon) get their HWNDs created together during WM_CREATE processing.
        _form.CreateHandleForTests();

        // Create Jarvis panel + TabbedMDIManager.
        _form.InitializeForTests();

        // Force a layout pass so positional assertions are valid.
        _form.PerformLayout();
        Application.DoEvents();
    }

    [TearDown]
    public void TearDown() => _form?.Dispose();

    public void Dispose()
    {
        _form?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Test 1: Ribbon must be docked top and span the full client width ──────────────────────
    [Test]
    public void Ribbon_IsDocked_Top_FullWidth_NoClip()
    {
        var form = _form!;
        var ribbon = form.GetRibbon();

        Assert.That(ribbon, Is.Not.Null, "Ribbon should be initialized by InitializeMinimalChromeForTests");
        Assert.That(ribbon!.Dock, Is.EqualTo(DockStyleEx.Top), "Ribbon must be docked to the top");
        Assert.That(ribbon.Top, Is.EqualTo(0), "Ribbon must start at the very top of the form");
        Assert.That(ribbon.Width, Is.EqualTo(form.ClientSize.Width), "Ribbon must span full client width (no clip)");
    }

    // ── Test 2: TabbedMDIManager must be attached; MdiClient must exist below the ribbon ──────
    [Test]
    public void TabbedMDIManager_Attached_And_MDIClient_BelowRibbon_NoOverlap()
    {
        var form = _form!;
        var tmdim = form.GetTabbedMDIManager();
        var layoutSnapshot = form.GetLayoutSnapshotForTests();
        Assert.That(tmdim, Is.Not.Null, "TabbedMDIManager must be created by InitializeForTests");

        // WinForms creates the MdiClient automatically when IsMdiContainer = true.
        var mdiClient = form.Controls.OfType<MdiClient>().FirstOrDefault();
        Assert.That(mdiClient, Is.Not.Null, "MdiClient must exist because IsMdiContainer=true");

        var ribbonBottom = form.GetRibbon()?.Bottom ?? 0;
        Assert.That(mdiClient!.Top, Is.GreaterThanOrEqualTo(ribbonBottom - 3),
            $"MdiClient top must not overlap ribbon. {layoutSnapshot}");
    }

    // ── Test 3: Jarvis must be permanently docked on the right —————————————————————
    [Test]
    public void Jarvis_IsPermanently_Docked_Right()
    {
        var form = _form!;
        var jp = form.GetJarvisPanel();
        Assert.That(jp, Is.Not.Null, "Jarvis panel must be stored in _rightDockPanel by InitializeLayoutComponents");

        Assert.That(jp!.Dock, Is.EqualTo(DockStyle.Right),
            "Jarvis panel must have DockStyle.Right");
        Assert.That(jp!.Width, Is.EqualTo(320),
            "Jarvis panel must be exactly 320 px wide");
    }
}
