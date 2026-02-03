using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;
using WileyWidget.Paneltest.Fixtures;
using WileyWidget.Paneltest.Helpers;
using Xunit;

namespace WileyWidget.Paneltest.TestCases;

/// <summary>
/// Base test case class for all panel tests.
/// Provides common setup, rendering, and assertion methods.
/// </summary>
public abstract class BasePanelTestCase : IDisposable
{
    protected readonly PanelTestFixture Fixture;
    protected Form? TestForm;
    protected UserControl? CurrentPanel;
    private readonly List<Form> _disposables = new();
    private bool _disposed;

    protected BasePanelTestCase(PanelTestFixture? fixture = null)
    {
        Fixture = fixture ?? new PanelTestFixture();
    }

    ~BasePanelTestCase()
    {
        Dispose(false);
    }

    /// <summary>
    /// Create and configure the test panel instance.
    /// Override this in derived classes to create specific panel types.
    /// </summary>
    protected abstract UserControl CreatePanel(IServiceProvider provider);

    /// <summary>
    /// Get the panel name for display and reporting.
    /// </summary>
    protected abstract string GetPanelName();

    /// <summary>
    /// Initialize panel with sample data (optional).
    /// Override in derived classes to load mock data.
    /// </summary>
    protected virtual void InitializePanelData(UserControl panel)
    {
        // Base implementation: no-op
    }

    /// <summary>
    /// Render the panel in a test form and optionally display it.
    /// </summary>
    public void RenderPanel(bool showForm = false)
    {
        var provider = Fixture.BuildServiceProvider();
        CurrentPanel = CreatePanel(provider);
        InitializePanelData(CurrentPanel);

        TestForm = new Form
        {
            Text = $"Panel Test - {GetPanelName()}",
            Size = new Size(1400, 900),
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = false
        };

        _disposables.Add(TestForm);

        CurrentPanel.Dock = DockStyle.Fill;
        CurrentPanel.MinimumSize = new Size(1400, 900);
        TestForm.Controls.Add(CurrentPanel);

        // Force handle creation and DI resolution
        PanelReflectionHelper.SimulateHandleCreation(CurrentPanel);

        if (showForm)
        {
            TestForm.Show();
            TestForm.Refresh();

            // Process UI events to allow form to render with proper size
            for (int i = 0; i < 15; i++)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(100);
            }

            // Ensure layout is performed
            TestForm.PerformLayout();
            CurrentPanel.PerformLayout();

            // One more refresh cycle
            Application.DoEvents();
            System.Threading.Thread.Sleep(500);

            // Log control states after layout is complete
            if (this is WarRoomPanelTestCase warRoomTest)
            {
                warRoomTest.LogControlStatesAfterLayout();
            }
        }
    }

    /// <summary>
    /// Get the ViewModel from the rendered panel.
    /// </summary>
    public object? GetViewModel()
    {
        if (CurrentPanel == null)
            throw new InvalidOperationException("Panel not rendered. Call RenderPanel() first.");

        return PanelReflectionHelper.GetViewModelForTesting(CurrentPanel);
    }

    /// <summary>
    /// Get a private field from the panel by name.
    /// </summary>
    protected object? GetPanelField(string fieldName)
    {
        if (CurrentPanel == null)
            throw new InvalidOperationException("Panel not rendered. Call RenderPanel() first.");

        return PanelReflectionHelper.GetPrivateField(CurrentPanel, fieldName);
    }

    /// <summary>
    /// Get a private property from the panel by name.
    /// </summary>
    protected object? GetPanelProperty(string propertyName)
    {
        if (CurrentPanel == null)
            throw new InvalidOperationException("Panel not rendered. Call RenderPanel() first.");

        return PanelReflectionHelper.GetPrivateProperty(CurrentPanel, propertyName);
    }

    /// <summary>
    /// Assert that the panel was successfully initialized.
    /// </summary>
    public void AssertPanelInitialized()
    {
        Assert.NotNull(CurrentPanel);
        Assert.NotNull(TestForm);
        var vm = GetViewModel();
        Assert.NotNull(vm);
    }

    /// <summary>
    /// Assert that a specific control exists on the panel.
    /// </summary>
    protected void AssertControlExists(string controlFieldName)
    {
        var control = GetPanelField(controlFieldName);
        Assert.NotNull(control);
    }

    /// <summary>
    /// Process UI events to allow async operations to complete.
    /// </summary>
    protected void ProcessUIEvents(int timeoutMs = 5000)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < TimeSpan.FromMilliseconds(timeoutMs))
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Get test execution context (for reporting).
    /// </summary>
    protected TestExecutionContext GetContext()
    {
        return new TestExecutionContext
        {
            PanelName = GetPanelName(),
            TestTimeUtc = DateTime.UtcNow,
            PanelHandle = CurrentPanel?.Handle ?? IntPtr.Zero,
            HasViewModel = GetViewModel() != null
        };
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources
            CurrentPanel?.Dispose();
            TestForm?.Dispose();

            foreach (var form in _disposables)
            {
                form?.Dispose();
            }

            _disposables.Clear();
            Fixture?.Dispose();
        }

        // Clean up native resources here (if any)
        _disposed = true;
    }
}

/// <summary>
/// Execution context for test reporting.
/// </summary>
public class TestExecutionContext
{
    public string? PanelName { get; set; }
    public DateTime TestTimeUtc { get; set; }
    public IntPtr PanelHandle { get; set; }
    public bool HasViewModel { get; set; }
}
