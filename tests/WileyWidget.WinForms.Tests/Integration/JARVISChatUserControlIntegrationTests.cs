using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.AIAssistView;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Automation;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class JARVISChatUserControlIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_CreatesNativeAssistView()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = IntegrationTestServices.BuildProvider();

        using var control = CreateControl(provider);

        await control.InitializeAsync(cts.Token);

        control.Name.Should().Be("JARVISChatUserControl");
        control.AutoScroll.Should().BeFalse();
        var assistView = FindControl<SfAIAssistView>(control);
        assistView.Should().NotBeNull();
        assistView!.Name.Should().Be("JarvisAssistView");
        assistView.Visible.Should().BeTrue();
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_HostsAssistViewInRaisedInsetPanel()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        await control.InitializeAsync(cts.Token);

        var assistHost = FindNamedControl<System.Windows.Forms.Panel>(control, "JarvisAssistHost");
        var assistView = FindControl<SfAIAssistView>(control);
        var expectedBottomInset = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(24.0f);

        assistHost.Should().NotBeNull();
        assistHost!.Dock.Should().Be(System.Windows.Forms.DockStyle.Fill);
        assistHost.Padding.Bottom.Should().Be(expectedBottomInset);
        assistView.Should().NotBeNull();
        assistView!.Parent.Should().BeSameAs(assistHost);
    }

    [StaFact]
    public void JARVISChatUserControl_RuntimeHost_HidesAutomationStatusPanel()
    {
        var originalUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
        var originalTests = Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", null);
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", null);
            TestThemeHelper.EnsureOffice2019Colorful();

            using var provider = IntegrationTestServices.BuildProvider(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "false"
            });
            using var control = CreateControl(provider);

            control.AutomationStatusBox.Should().BeNull();
            control.Controls.ContainsKey("JarvisAutomationStatus").Should().BeFalse();
            control.AutoScroll.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", originalUiTests);
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", originalTests);
        }
    }

    [StaFact]
    public void JARVISChatUserControl_RightDockHost_UsesSidebarCompatibleMinimumWidth()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        using var form = IntegrationTestServices.CreateMainForm(scopedProvider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(scopedProvider);

        var (rightDockPanel, _, _, jarvisControl) = RightDockPanelFactory.CreateRightDockPanel(form, scopedProvider, logger);

        jarvisControl.MinimumSize.Width.Should().BeLessThanOrEqualTo(rightDockPanel.MinimumSize.Width);
        jarvisControl.Dock.Should().Be(System.Windows.Forms.DockStyle.Fill);

        var assistView = FindControl<SfAIAssistView>(jarvisControl);
        assistView.Should().NotBeNull();
        assistView!.Dock.Should().Be(System.Windows.Forms.DockStyle.Fill);
        assistView.Margin.Should().Be(System.Windows.Forms.Padding.Empty);
        jarvisControl.MinimumSize.Height.Should().BeGreaterThanOrEqualTo(520);
    }

    [StaFact]
    public void JARVISChatUserControl_ProvidesCopyExportAndResponseViewerActions()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        FindNamedControl<SfButton>(control, "JarvisOpenResponseViewerButton").Should().NotBeNull();
        FindNamedControl<SfButton>(control, "JarvisCopyLatestResponseButton").Should().NotBeNull();
        FindNamedControl<SfButton>(control, "JarvisCopyTranscriptButton").Should().NotBeNull();
        FindNamedControl<SfButton>(control, "JarvisExportTranscriptButton").Should().NotBeNull();
    }

    [StaFact]
    public void JARVISChatUserControl_ResponseToolbarButtons_UseSharedToolbarBoundaries()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        var openButton = FindNamedControl<SfButton>(control, "JarvisOpenResponseViewerButton");
        var copyButton = FindNamedControl<SfButton>(control, "JarvisCopyLatestResponseButton");
        var transcriptButton = FindNamedControl<SfButton>(control, "JarvisCopyTranscriptButton");
        var exportButton = FindNamedControl<SfButton>(control, "JarvisExportTranscriptButton");

        openButton.Should().NotBeNull();
        copyButton.Should().NotBeNull();
        transcriptButton.Should().NotBeNull();
        exportButton.Should().NotBeNull();

        openButton!.MinimumSize.Should().Be(new System.Drawing.Size(104, 32));
        copyButton!.MinimumSize.Should().Be(new System.Drawing.Size(104, 32));
        transcriptButton!.MinimumSize.Should().Be(new System.Drawing.Size(104, 32));
        exportButton!.MinimumSize.Should().Be(new System.Drawing.Size(104, 32));
        openButton.Width.Should().BeGreaterThanOrEqualTo(104);
        exportButton.Width.Should().BeGreaterThanOrEqualTo(104);
    }

    [StaFact]
    public void JARVISChatUserControl_BeginResponse_ShowsVisibleThinkingState()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        var beginResponse = typeof(JARVISChatUserControl).GetMethod(
            "BeginResponse",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var endResponse = typeof(JARVISChatUserControl).GetMethod(
            "EndResponse",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        beginResponse.Should().NotBeNull();
        endResponse.Should().NotBeNull();
        beginResponse!.Invoke(control, Array.Empty<object>());

        var assistView = FindControl<SfAIAssistView>(control);
        var statusPanel = FindNamedControl<System.Windows.Forms.TableLayoutPanel>(control, "JarvisResponseStatusPanel");
        var statusLabel = FindNamedControl<System.Windows.Forms.Label>(control, "JarvisResponseStatusLabel");
        var progressBar = FindNamedControl<Syncfusion.Windows.Forms.Tools.ProgressBarAdv>(control, "JarvisResponseStatusProgress");

        assistView.Should().NotBeNull();
        assistView!.ShowTypingIndicator.Should().BeTrue();
        statusPanel.Should().NotBeNull();
        statusPanel!.Visible.Should().BeTrue();
        statusLabel.Should().NotBeNull();
        statusLabel!.Text.Should().Contain("thinking");
        progressBar.Should().NotBeNull();
        progressBar!.Visible.Should().BeTrue();

        endResponse!.Invoke(control, Array.Empty<object>());
    }

    [StaFact]
    public void JARVISChatUserControl_EndResponse_HidesVisibleThinkingState()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        var beginResponse = typeof(JARVISChatUserControl).GetMethod(
            "BeginResponse",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var endResponse = typeof(JARVISChatUserControl).GetMethod(
            "EndResponse",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        beginResponse.Should().NotBeNull();
        endResponse.Should().NotBeNull();

        beginResponse!.Invoke(control, Array.Empty<object>());
        endResponse!.Invoke(control, Array.Empty<object>());

        var assistView = FindControl<SfAIAssistView>(control);
        var statusPanel = FindNamedControl<System.Windows.Forms.TableLayoutPanel>(control, "JarvisResponseStatusPanel");
        var progressBar = FindNamedControl<Syncfusion.Windows.Forms.Tools.ProgressBarAdv>(control, "JarvisResponseStatusProgress");

        assistView.Should().NotBeNull();
        assistView!.ShowTypingIndicator.Should().BeFalse();
        statusPanel.Should().NotBeNull();
        statusPanel!.Visible.Should().BeFalse();
        progressBar.Should().NotBeNull();
        progressBar!.Visible.Should().BeFalse();
    }

    [StaFact]
    public void JARVISChatUserControl_AssistantResponse_EnablesResponseActions()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        var appendAssistantMessage = typeof(JARVISChatUserControl).GetMethod(
            "AppendAssistantMessage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        appendAssistantMessage.Should().NotBeNull();
        appendAssistantMessage!.Invoke(control, new object[] { "This is a long JARVIS response that should be copyable and exportable." });

        FindNamedControl<SfButton>(control, "JarvisOpenResponseViewerButton")!.Enabled.Should().BeTrue();
        FindNamedControl<SfButton>(control, "JarvisCopyLatestResponseButton")!.Enabled.Should().BeTrue();
        FindNamedControl<SfButton>(control, "JarvisCopyTranscriptButton")!.Enabled.Should().BeTrue();
        FindNamedControl<SfButton>(control, "JarvisExportTranscriptButton")!.Enabled.Should().BeTrue();
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_WithInitialPrompt_SubmitsPrompt()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var mockAIService = new Mock<IAIService>();
        var testPrompt = "What is the current budget status?";

        var mockChatBridgeService = new Mock<IChatBridgeService>();
        mockChatBridgeService
            .Setup(s => s.RequestExternalPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPersonalityService = new Mock<IJARVISPersonalityService>();
        mockPersonalityService.Setup(s => s.GetSystemPrompt()).Returns("system");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("UI:IsUiTestHarness", "true") })
            .Build());
        services.AddLogging(builder => builder.AddDebug());
        services.AddScoped<IAIService>(_ => mockAIService.Object);
        services.AddScoped<IChatBridgeService>(_ => mockChatBridgeService.Object);
        services.AddScoped<IThemeService>(_ => Mock.Of<IThemeService>(service => service.CurrentTheme == "Office2019Colorful"));
        services.AddScoped<IWindowStateService>(_ => Mock.Of<IWindowStateService>());
        services.AddScoped<IFileImportService>(_ => Mock.Of<IFileImportService>());
        services.AddScoped<IJARVISPersonalityService>(_ => mockPersonalityService.Object);
        services.AddScoped<IAILoggingService>(_ => Mock.Of<IAILoggingService>());
        services.AddScoped<JarvisAutomationState>();
        services.AddScoped<JARVISChatViewModel>();
        services.AddScoped<SyncfusionControlFactory>();
        services.AddScoped<JarvisGrokBridgeHandler>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var control = CreateControl(provider);
        control.InitialPrompt = testPrompt;

        await control.InitializeAsync(cts.Token);

        mockChatBridgeService.Verify(
            s => s.RequestExternalPromptAsync(testPrompt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_UpdatesAutomationStatusForNativeControl()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        control.AutomationStatusBox.Should().NotBeNull();

        await control.InitializeAsync(CancellationToken.None);

        var automationText = control.AutomationStatusBox!.Text;
        automationText.Should().Contain("ChatUiReady=True");
        automationText.Should().Contain("AssistViewReady=True");
    }

    [StaFact]
    public async Task JARVISChatUserControl_AutomationStatusPanel_PersistsAfterInitialization()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        control.AutomationStatusBox.Should().NotBeNull();
        control.AutomationStatusBox!.ReadOnly.Should().BeTrue();
        control.AutomationStatusBox.Name.Should().Be("JarvisAutomationStatus");

        await control.InitializeAsync(CancellationToken.None);

        control.AutomationStatusBox.Should().NotBeNull();
    }

    [StaFact]
    public async Task JARVISChatUserControl_HandlesCancellationToken_Gracefully()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        await FluentActions.Awaiting(() => control.InitializeAsync(cts.Token))
            .Should().NotThrowAsync();
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_IsIdempotent()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = IntegrationTestServices.BuildProvider();
        using var control = CreateControl(provider);

        await control.InitializeAsync(cts.Token);
        var firstCallControlsCount = control.Controls.Count;

        await control.InitializeAsync(cts.Token);
        var secondCallControlsCount = control.Controls.Count;

        secondCallControlsCount.Should().Be(firstCallControlsCount);
    }

    private static JARVISChatUserControl CreateControl(IServiceProvider provider)
    {
        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(provider);
        var scope = scopeFactory.CreateScope();
        var control = ActivatorUtilities.CreateInstance<JARVISChatUserControl>(scope.ServiceProvider);
        control.Dock = System.Windows.Forms.DockStyle.Fill;
        control.Width = 400;
        control.Height = 300;
        control.Disposed += (_, _) => scope.Dispose();
        return control;
    }

    // Helper method to find controls by type
    private static TControl? FindControl<TControl>(System.Windows.Forms.Control root)
        where TControl : System.Windows.Forms.Control
    {
        if (root is TControl match)
        {
            return match;
        }

        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            var found = FindControl<TControl>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // Helper method to find controls by name
    private static System.Windows.Forms.TextBox? FindTextBox(System.Windows.Forms.Control root, string controlName)
    {
        if (root is System.Windows.Forms.TextBox textBox && root.Name == controlName)
        {
            return textBox;
        }

        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            var found = FindTextBox(child, controlName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TControl? FindNamedControl<TControl>(System.Windows.Forms.Control root, string controlName)
        where TControl : System.Windows.Forms.Control
    {
        if (root is TControl match && root.Name == controlName)
        {
            return match;
        }

        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            var found = FindNamedControl<TControl>(child, controlName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
