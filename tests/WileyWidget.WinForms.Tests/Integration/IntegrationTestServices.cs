using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Tests.Integration;

internal static class IntegrationTestServices
{
    public static ServiceProvider BuildProvider(Dictionary<string, string?>? overrides = null)
    {
        var services = new ServiceCollection();

        var defaultConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "false",
                ["UI:UseSyncfusionDocking"] = "false",
                ["UI:ShowRibbon"] = "true",
                ["UI:ShowStatusBar"] = "true"
            })
            .Build();

        var configuration = overrides == null
            ? defaultConfig
            : new ConfigurationBuilder().AddInMemoryCollection(overrides).Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton(ReportViewerLaunchOptions.Disabled);

        var themeMock = new Mock<IThemeService>();
        themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
        themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback((string theme) =>
        {
            // Avoid mutating global theme during tests
        });

        services.AddSingleton<IThemeService>(themeMock.Object);
        services.AddSingleton<IWindowStateService>(Mock.Of<IWindowStateService>());
        services.AddSingleton<IFileImportService>(Mock.Of<IFileImportService>());
        services.AddSingleton<DpiAwareImageService>();

        services.AddScoped<IDashboardService>(_ => Mock.Of<IDashboardService>());
        services.AddScoped<IAILoggingService>(_ => Mock.Of<IAILoggingService>());
        services.AddScoped<IQuickBooksService>(_ => Mock.Of<IQuickBooksService>());
        services.AddScoped<IGlobalSearchService>(_ => Mock.Of<IGlobalSearchService>());
        services.AddScoped<MainViewModel>();
        services.AddScoped<QuickBooksViewModel>();
        services.AddScoped<JARVISChatViewModel>();
        services.AddWindowsFormsBlazorWebView();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public static MainForm CreateMainForm(IServiceProvider provider)
    {
        var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
        var windowStateService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IWindowStateService>(provider);
        var fileImportService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IFileImportService>(provider);

        return new MainForm(
            provider,
            configuration,
            logger,
            ReportViewerLaunchOptions.Disabled,
            themeService,
            windowStateService,
            fileImportService);
    }
}
