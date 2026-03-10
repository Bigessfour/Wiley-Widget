using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Themes;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Automation;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class JARVISChatUserControlIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_InvokesInitialization()
    {
        // Arrange - Force headless mode to prevent BlazorWebView hangs in test environment
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = IntegrationTestServices.BuildProvider();

        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<JARVISChatUserControl>>(provider);

        var control = new JARVISChatUserControl(scopeFactory, provider, logger);

        // Act
        await control.InitializeAsync(cts.Token);

        // Assert - Control should be initialized without errors
        control.Should().NotBeNull();
        control.Name.Should().Be("JARVISChatUserControl");
    }

    [StaFact(Skip = "BlazorWebView requires Microsoft.WinForms.Utilities.Shared v1.6.0.0 which is absent from the .NET 10 WindowsDesktop runtime. Instantiating BlazorWebView triggers a process-wide CLR assembly-load failure that poisons all subsequent tests in the run.")]
    public async Task JARVISChatUserControl_InitializeAsync_CreatesBlazorWebView_WhenNotHeadless()
    {
        // Arrange - Enable Blazor initialization (not headless)
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
        Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = IntegrationTestServices.BuildProvider();

        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<JARVISChatUserControl>>(provider);

        var control = new JARVISChatUserControl(scopeFactory, provider, logger)
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            Width = 400,
            Height = 300
        };

        try
        {
            // Act
            await control.InitializeAsync(cts.Token);

            // Assert - BlazorWebView should be created and visible
            control.Controls.Count.Should().BeGreaterThan(0, "BlazorWebView should be added to controls");

            // In non-headless mode, BlazorWebView should exist
            var blazorWebView = FindControl<BlazorWebView>(control);
            blazorWebView.Should().NotBeNull("BlazorWebView should be created");
            blazorWebView!.Visible.Should().BeTrue("BlazorWebView should be visible");
            blazorWebView.Name.Should().Be("JARVISChatBlazorView");
        }
        finally
        {
            // Cleanup
            control.Dispose();
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", null);
        }
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_WithMockedAIService_InHeadlessMode_DoesNotDispatchInitialPrompt()
    {
        // Arrange - Headless mode skips Blazor startup, so the initial prompt should remain queued.
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var mockAIService = new Mock<IAIService>();
        var testPrompt = "What is the current budget status?";

        var mockChatBridgeService = new Mock<IChatBridgeService>();

        mockChatBridgeService
            .Setup(s => s.RequestExternalPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("UI:IsUiTestHarness", "false") })
            .Build());
        services.AddLogging(builder => builder.AddDebug());
        services.AddScoped<IAIService>(_ => mockAIService.Object);
        services.AddScoped<IChatBridgeService>(_ => mockChatBridgeService.Object);
        services.AddScoped<IThemeService>(_ => Mock.Of<IThemeService>());
        services.AddScoped<IWindowStateService>(_ => Mock.Of<IWindowStateService>());
        services.AddScoped<IFileImportService>(_ => Mock.Of<IFileImportService>());
        services.AddScoped<SyncfusionControlFactory>();
        services.AddScoped<JARVISChatViewModel>();
        // NOTE: AddWindowsFormsBlazorWebView() is intentionally NOT called here.
        // This test runs in headless mode (WILEYWIDGET_UI_TESTS=true); JARVISChatUserControl.InitializeAsync
        // skips BlazorWebView creation, so Blazor DI services are not needed. Calling it triggers loading
        // Microsoft.WinForms.Utilities.Shared v1.6.0.0 (absent from .NET 10), poisoning the CLR process-wide.

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(provider);
        var loggerFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILoggerFactory>(provider);
        var logger = loggerFactory.CreateLogger<JARVISChatUserControl>();

        var control = new JARVISChatUserControl(scopeFactory, provider, logger)
        {
            InitialPrompt = testPrompt
        };

        try
        {
            // Act
            await control.InitializeAsync(cts.Token);

            // Assert - Headless mode completes initialization without dispatching the queued prompt.
            control.AutomationStatusBox.Should().NotBeNull();
            control.InitialPrompt.Should().Be(testPrompt);
            mockChatBridgeService.Verify(
                s => s.RequestExternalPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            control.Dispose();
        }
    }

    [StaFact]
    public async Task JARVISChatUserControl_AutomationStatusPanel_UpdatesOnStateChange()
    {
        // Arrange - Create control directly to test immediate TextBox creation
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<JARVISChatUserControl>>(provider);

        var control = new JARVISChatUserControl(scopeFactory, provider, logger);

        try
        {
            // Assert - AutomationStatusBox should exist immediately (created in constructor)
            control.AutomationStatusBox.Should().NotBeNull("Automation status TextBox should exist immediately after control creation");

            // Verify it has expected properties
            control.AutomationStatusBox!.ReadOnly.Should().BeTrue("Status TextBox should be read-only");
            control.AutomationStatusBox!.Name.Should().Be("JarvisAutomationStatus", "TextBox should have correct automation name");

            // Act - Initialize the control
            await control.InitializeAsync(System.Threading.CancellationToken.None);

            // Assert - TextBox should still exist after initialization
            control.AutomationStatusBox.Should().NotBeNull("Automation status TextBox should persist after initialization");

            // Success: The TextBox exists immediately and survives initialization
        }
        finally
        {
            control.Dispose();
        }
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_WhenCanceled_ThrowsTaskCanceledException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var provider = IntegrationTestServices.BuildProvider();

        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<JARVISChatUserControl>>(provider);

        var control = new JARVISChatUserControl(scopeFactory, provider, logger);

        try
        {
            // Act & Assert - InitializeAsync honors the provided cancellation token.
            await FluentActions.Awaiting(() => control.InitializeAsync(cts.Token))
                .Should().ThrowAsync<TaskCanceledException>();
        }
        finally
        {
            control.Dispose();
        }
    }

    [StaFact]
    public async Task JARVISChatUserControl_InitializeAsync_IsIdempotent()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = IntegrationTestServices.BuildProvider();

        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<JARVISChatUserControl>>(provider);

        var control = new JARVISChatUserControl(scopeFactory, provider, logger);

        // Act - Call InitializeAsync multiple times
        await control.InitializeAsync(cts.Token);
        var firstCallControlsCount = control.Controls.Count;

        await control.InitializeAsync(cts.Token);
        var secondCallControlsCount = control.Controls.Count;

        // Assert - Should not add duplicate controls or fail
        secondCallControlsCount.Should().Be(firstCallControlsCount, "Calling InitializeAsync twice should be safe");

        control.Dispose();
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
}
