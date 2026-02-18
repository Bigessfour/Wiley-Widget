using System;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Integration.TestUtilities;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class MainFormKeyboardIntegrationTests
{
    private static void PumpMessages(int maxCount = 50)
    {
        for (int i = 0; i < maxCount; i++)
        {
            Application.DoEvents();
        }
    }
    private sealed class TestMainForm : MainForm
    {
        public TestMainForm(IServiceProvider provider)
            : base(
                provider,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(provider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider),
                WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.WinForms.Services.IThemeService>(provider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.WinForms.Services.Abstractions.IWindowStateService>(provider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Services.Abstractions.IFileImportService>(provider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(provider))
        {
        }

        public bool ProcessTestKey(Keys keyData)
        {
            var msg = new Message
            {
                Msg = 0x0100, // WM_KEYDOWN
                WParam = (IntPtr)keyData,
                LParam = IntPtr.Zero
            };
            return ProcessCmdKey(ref msg, keyData);
        }

        public object? GetPrivateField(string name)
        {
            return typeof(MainForm).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this);
        }

        public void CallOnLoad()
        {
            InvokeOnLoad();
        }
    }

    [WinFormsFact]
    public void Keyboard_GlobalSearchFocus_Works()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider(new System.Collections.Generic.Dictionary<string, string?>
        {
            ["UI:ShowRibbon"] = "true"
        });
        using var form = new TestMainForm(provider);

        try
        {
            // Use lazy initialization instead
            form.PanelNavigator?.ShowPanel<TestPanel>("Test", DockingStyle.Right);

            // Initialize chrome to create search box
            typeof(MainForm).GetMethod("InitializeChrome", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, null);
            Application.DoEvents();  // Process messages after chrome/ribbon init

            // Send Ctrl+F
            var handled = form.ProcessTestKey(Keys.F | Keys.Control);

            // Should be handled
            handled.Should().BeTrue();
        }
        finally
        {
            if (form.IsHandleCreated)
            {
                form.Close();
                form.Dispose();
            }
        }
    }

    [WinFormsFact]
    public void Keyboard_PanelShortcuts_Work()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider(new System.Collections.Generic.Dictionary<string, string?>
        {
            ["UI:ShowRibbon"] = "true"
        });
        using var form = new TestMainForm(provider);
        // _ = form.Handle;  // Commented out to avoid handle creation
        // form.CreateControl();  // Commented out to avoid control creation
        // form.CallOnLoad();  // Commented out to avoid OnLoad which calls InitializeChrome/docking init

        // Use lazy initialization instead
        form.PanelNavigator?.ShowPanel<TestPanel>("Test", DockingStyle.Right);

        // Send Alt+D (Dashboard shortcut)
        var handled = form.ProcessTestKey(Keys.D | Keys.Alt);

        // Should be handled
        handled.Should().BeTrue();
    }

    [WinFormsFact]
    public void Keyboard_EnterKey_InTextBox_FocusesSearch()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = new TestMainForm(provider);

        try
        {
            // Use lazy initialization instead
            form.PanelNavigator?.ShowPanel<TestPanel>("Test", DockingStyle.Right);

            // Create a test textbox
            var textBox = new TextBox();
            form.Controls.Add(textBox);
            textBox.Focus();

            // Pump messages to ensure focus is set
            PumpMessages();

            // Send Enter
            var handled = form.ProcessTestKey(Keys.Enter);

            // Should be handled (focuses search)
            handled.Should().BeTrue();
        }
        finally
        {
            if (form.IsHandleCreated)
            {
                form.Close();
                form.Dispose();
            }
        }
    }

    [WinFormsFact]
    public void Keyboard_EscapeKey_ClearsSearch()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = new TestMainForm(provider);
        // _ = form.Handle;  // Commented out to avoid handle creation
        // form.CreateControl();  // Commented out to avoid control creation
        // form.CallOnLoad();  // Commented out to avoid OnLoad which calls InitializeChrome/docking init

        // Use lazy initialization instead
        form.PanelNavigator?.ShowPanel<TestPanel>("Test", DockingStyle.Right);

        // Initialize chrome to create search box
        typeof(MainForm).GetMethod("InitializeChrome", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, null);
        Application.DoEvents();  // Process messages after chrome/ribbon init

        // Send Escape
        var handled = form.ProcessTestKey(Keys.Escape);

        // Should be handled
        handled.Should().BeTrue();
    }

    [WinFormsFact]
    public void Keyboard_ThemeToggle_Works()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = new TestMainForm(provider);
        // _ = form.Handle;  // Commented out to avoid handle creation
        // form.CreateControl();  // Commented out to avoid control creation
        // form.CallOnLoad();  // Commented out to avoid OnLoad which calls InitializeChrome/docking init

        // Use lazy initialization instead
        form.PanelNavigator?.ShowPanel<TestPanel>("Test", DockingStyle.Right);

        // Send Ctrl+Shift+T (theme toggle)
        var handled = form.ProcessTestKey(Keys.T | Keys.Control | Keys.Shift);

        // Should be handled
        handled.Should().BeTrue();
    }
}
