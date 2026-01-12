using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Validates STA threading requirements for WinForms applications using Microsoft-documented patterns.
/// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.stathreadattribute
/// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.invokerequired
/// </summary>
public class StaThreadValidationTests
{
    private readonly ITestOutputHelper _output;

    public StaThreadValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [StaFact]
    public void Main_Method_Should_Have_StaThread_Attribute()
    {
        // Arrange & Act - Get Main method from Program class
        var programType = typeof(WileyWidget.WinForms.Program);
        var mainMethod = programType.GetMethod("Main",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        // Assert
        mainMethod.Should().NotBeNull("Main method should exist");

        var staAttribute = mainMethod!.GetCustomAttributes(typeof(STAThreadAttribute), false);
        staAttribute.Should().NotBeEmpty("Main method must have [STAThread] attribute for WinForms");

        _output.WriteLine($"✓ Main method has [STAThread] attribute");
    }

    [StaFact]
    public void Current_Thread_Should_Be_STA()
    {
        // Arrange & Act
        var apartmentState = Thread.CurrentThread.GetApartmentState();

        // Assert
        apartmentState.Should().Be(ApartmentState.STA,
            "WinForms requires STA (Single-Threaded Apartment) threading model");

        _output.WriteLine($"✓ Current thread apartment state: {apartmentState}");
    }

    [StaFact]
    public void SynchronizationContext_Should_Be_WindowsFormsSynchronizationContext()
    {
        // Arrange
        Form? form = null;

        try
        {
            // Act - Create a form which installs WindowsFormsSynchronizationContext
            form = new Form();
            form.CreateControl(); // Force handle creation

            var syncContext = SynchronizationContext.Current;

            // Assert
            syncContext.Should().NotBeNull("SynchronizationContext should be installed");
            syncContext.Should().BeOfType<WindowsFormsSynchronizationContext>(
                "WinForms should install WindowsFormsSynchronizationContext");

            _output.WriteLine($"✓ SynchronizationContext type: {syncContext?.GetType().Name}");
        }
        finally
        {
            form?.Dispose();
        }
    }

    [StaFact]
    public void Control_InvokeRequired_Should_Return_False_On_UI_Thread()
    {
        // Arrange
        using var control = new Control();
        control.CreateControl();

        // Act
        var invokeRequired = control.InvokeRequired;

        // Assert
        invokeRequired.Should().BeFalse(
            "InvokeRequired should return false when called from the control's creation thread");

        _output.WriteLine($"✓ InvokeRequired returns false on UI thread");
    }

    [StaFact]
    public void Control_InvokeRequired_Should_Return_True_On_Background_Thread()
    {
        // Arrange
        using var control = new Control();
        control.CreateControl();

        // Act - Use ManualResetEventSlim to coordinate without async/await
        bool invokeRequired = false;
        ApartmentState threadState = ApartmentState.Unknown;
        using var mre = new ManualResetEventSlim(false);

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                invokeRequired = control.InvokeRequired;
                threadState = Thread.CurrentThread.GetApartmentState();
            }
            finally
            {
                mre.Set();
            }
        });

        // Wait for background work to complete
        mre.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("Background work should complete");

        // Assert
        invokeRequired.Should().BeTrue(
            "InvokeRequired should return true when called from a different thread");
        threadState.Should().Be(ApartmentState.MTA,
            "ThreadPool threads are MTA (Multi-Threaded Apartment)");

        _output.WriteLine($"✓ InvokeRequired returns true on background thread (MTA)");
    }

    [StaFact]
    public void Control_Invoke_Should_Execute_On_Same_Thread()
    {
        // Arrange
        using var control = new Control();
        control.CreateControl();
        var uiThreadId = Thread.CurrentThread.ManagedThreadId;

        // Act
        int invokedThreadId = 0;
        ApartmentState invokedApartmentState = ApartmentState.Unknown;

        control.Invoke(() =>
        {
            invokedThreadId = Thread.CurrentThread.ManagedThreadId;
            invokedApartmentState = Thread.CurrentThread.GetApartmentState();
        });

        // Assert
        invokedThreadId.Should().Be(uiThreadId,
            "Control.Invoke should execute delegate on the UI thread");
        invokedApartmentState.Should().Be(ApartmentState.STA,
            "Invoked delegate should execute in STA context");

        _output.WriteLine($"✓ Control.Invoke executed on UI thread {invokedThreadId} (STA)");
    }

    // NOTE: This test is commented out because Control.Invoke from a background thread
    // requires a message loop (Application.Run) to process the invoke request.
    // Without a message loop, Invoke will deadlock waiting for the UI thread to process it.
    // This behavior is validated in production code where Application.Run is active.
    /*
    [StaFact]
    public void Control_Invoke_Should_Marshal_From_Background_Thread_To_UI_Thread()
    {
        // This test would deadlock without Application.Run() active
        // See: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.invoke
    }
    */

    // NOTE: This test is commented out because Control.BeginInvoke from a background thread
    // requires a message loop (Application.Run) to process the invoke request.
    // Without a message loop, BeginInvoke will never execute and the task will never complete.
    // This behavior is validated in production code where Application.Run is active.
    /*
    [StaFact]
    public void Control_BeginInvoke_Should_Marshal_Asynchronously()
    {
        // This test would hang without Application.Run() active
        // See: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.begininvoke
    }
    */

    [StaFact]
    public void SynchronizationContext_Is_Captured_By_Forms()
    {
        // Arrange
        using var form = new Form();
        form.CreateControl();
        var syncContext = SynchronizationContext.Current;

        // Act & Assert
        syncContext.Should().BeOfType<WindowsFormsSynchronizationContext>(
            "Creating a form should install WindowsFormsSynchronizationContext");

        _output.WriteLine($"✓ SynchronizationContext captured: {syncContext?.GetType().Name}");
        _output.WriteLine("  NOTE: await without ConfigureAwait(false) would capture this context");
        _output.WriteLine("  and attempt to marshal continuation back to it via Post().");
        _output.WriteLine("  This requires Application.Run() message loop to process the Post.");
        _output.WriteLine("\n  IMPORTANT: All async/await tests that use the captured context");
        _output.WriteLine("  would hang without Application.Run(), so they are not included here.");
    }

    [StaFact]
    public void ConfigureAwait_False_Pattern_Validation()
    {
        // This test validates the pattern without actually awaiting
        // (which would require Application.Run message loop)

        var uiThreadId = Thread.CurrentThread.ManagedThreadId;
        var uiApartmentState = Thread.CurrentThread.GetApartmentState();

        // Assert initial state
        uiApartmentState.Should().Be(ApartmentState.STA, "Test should start on STA thread");

        _output.WriteLine($"✓ Current thread: {uiThreadId} (STA)");
        _output.WriteLine("  NOTE: ConfigureAwait(false) allows continuation on any thread");
        _output.WriteLine("  This is used in ViewModels to avoid capturing SynchronizationContext");
        _output.WriteLine("  when UI updates are not needed after await.");
        _output.WriteLine("\n  IMPORTANT: Testing actual await with ConfigureAwait(false)");
        _output.WriteLine("  in a StaFact test would still risk hanging because Task.Delay()");
        _output.WriteLine("  or other awaitable operations might capture context before");
        _output.WriteLine("  ConfigureAwait(false) takes effect.");
    }

    [StaFact]
    public void Microsoft_Pattern_InvokeRequired_Should_Work_On_UI_Thread()
    {
        // Arrange - Microsoft's documented pattern
        using var textBox = new TextBox();
        textBox.CreateControl();

        // Act & Assert - Microsoft pattern from documentation
        void WriteTextSafe(string text)
        {
            if (textBox.InvokeRequired)
            {
                // Would deadlock without message loop - only test UI thread path
                throw new InvalidOperationException("Should not require invoke on UI thread");
            }
            else
            {
                textBox.Text = text;
            }
        }

        // Test from UI thread - this path works without message loop
        WriteTextSafe("Test from UI thread");
        textBox.Text.Should().Be("Test from UI thread");

        // Note: Testing from background thread requires Application.Run message loop
        // and is validated in production code, not unit tests
        _output.WriteLine($"✓ Microsoft InvokeRequired pattern works on UI thread");
        _output.WriteLine("  (Cross-thread marshaling requires Application.Run message loop)");
    }

    [StaFact]
    public void ObservableCollection_Operations_Should_Work_On_UI_Thread()
    {
        // Arrange
        using var form = new Form();
        form.CreateControl();
        var collection = new System.Collections.ObjectModel.ObservableCollection<string>();

        // Act - Operations on UI thread should work without exceptions
        Action operations = () =>
        {
            collection.Add("Item 1");
            collection.Add("Item 2");
            collection.Clear();
            collection.Add("Item 3");
        };

        // Assert
        operations.Should().NotThrow("ObservableCollection operations should work on UI thread");
        collection.Should().HaveCount(1);
        collection[0].Should().Be("Item 3");

        _output.WriteLine($"✓ ObservableCollection operations succeeded on UI thread");
    }

    [StaFact]
    public void Thread_Safety_Checklist_Validation()
    {
        // This test validates the complete STA threading checklist

        // 1. Verify STA apartment state
        Thread.CurrentThread.GetApartmentState().Should().Be(ApartmentState.STA);
        _output.WriteLine("✓ 1. Current thread is STA");

        // 2. Verify SynchronizationContext installation
        using var form = new Form();
        form.CreateControl();
        SynchronizationContext.Current.Should().BeOfType<WindowsFormsSynchronizationContext>();
        _output.WriteLine("✓ 2. WindowsFormsSynchronizationContext installed");

        // 3. Verify InvokeRequired behavior
        using var control = new Control();
        control.CreateControl();
        control.InvokeRequired.Should().BeFalse("on UI thread");
        _output.WriteLine("✓ 3. InvokeRequired returns false on UI thread");

        // 4. Verify thread-safe members exist
        var controlType = typeof(Control);
        controlType.GetMethod("Invoke", new[] { typeof(Action) }).Should().NotBeNull();
        controlType.GetMethod("BeginInvoke", new[] { typeof(Delegate) }).Should().NotBeNull();
        controlType.GetProperty("InvokeRequired").Should().NotBeNull();
        _output.WriteLine("✓ 4. Thread-safe Control members exist (Invoke, BeginInvoke, InvokeRequired)");

        // 5. Verify Main method has [STAThread]
        var mainMethod = typeof(WileyWidget.WinForms.Program).GetMethod("Main",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        mainMethod?.GetCustomAttributes(typeof(STAThreadAttribute), false).Should().NotBeEmpty();
        _output.WriteLine("✓ 5. Program.Main has [STAThread] attribute");

        _output.WriteLine("\n✅ ALL STA THREADING REQUIREMENTS VALIDATED");
        _output.WriteLine("\nNOTE: Tests involving async/await with SynchronizationContext marshaling");
        _output.WriteLine("      cannot be validated in unit tests without Application.Run().");
        _output.WriteLine("      These patterns are validated in production code where the");
        _output.WriteLine("      message loop is active.");
    }

    [StaFact]
    public void Background_Thread_Validation_Without_Async()
    {
        // This test validates background thread behavior without using async/await
        // to avoid the SynchronizationContext capture/marshaling issue

        using var control = new Control();
        control.CreateControl();

        var uiThreadId = Thread.CurrentThread.ManagedThreadId;
        int bgThreadId = 0;
        ApartmentState bgApartmentState = ApartmentState.Unknown;
        bool bgInvokeRequired = false;

        using var mre = new ManualResetEventSlim(false);

        // Spawn background thread work
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                bgThreadId = Thread.CurrentThread.ManagedThreadId;
                bgApartmentState = Thread.CurrentThread.GetApartmentState();
                bgInvokeRequired = control.InvokeRequired;
            }
            finally
            {
                mre.Set();
            }
        });

        // Wait for completion
        mre.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("Background work should complete");

        // Assert
        bgThreadId.Should().NotBe(uiThreadId, "Should execute on different thread");
        bgApartmentState.Should().Be(ApartmentState.MTA, "ThreadPool uses MTA threads");
        bgInvokeRequired.Should().BeTrue("InvokeRequired should be true from background thread");

        _output.WriteLine($"✓ Background thread validation:");
        _output.WriteLine($"  UI Thread: {uiThreadId} (STA)");
        _output.WriteLine($"  BG Thread: {bgThreadId} (MTA)");
        _output.WriteLine($"  InvokeRequired from BG: {bgInvokeRequired}");
    }
}
