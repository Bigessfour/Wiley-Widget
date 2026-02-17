using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using BusinessInterfaces = WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Tests.Integration;

internal static class IntegrationTestServices
{
    public static ServiceProvider BuildProvider(Dictionary<string, string?>? overrides = null)
    {
        var services = new ServiceCollection();

        var defaults = new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "false",
            ["UI:UseSyncfusionDocking"] = "false",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "true",
            ["UI:AutoShowDashboard"] = "true",
            ["UI:MinimalMode"] = "false"
        };

        // Merge overrides into defaults
        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                defaults[kvp.Key] = kvp.Value;
            }
        }

        ApplyEnvironmentOverrides(defaults);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();

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
        services.AddScoped<IAnalyticsService>(_ => Mock.Of<IAnalyticsService>());
        services.AddScoped<IReportService>(_ => Mock.Of<IReportService>());
        services.AddScoped<IAuditService>(_ => Mock.Of<IAuditService>());
        services.AddScoped<IReportExportService>(_ => Mock.Of<IReportExportService>());
        services.AddScoped<IAILoggingService>(_ => Mock.Of<IAILoggingService>());
        services.AddScoped<IQuickBooksService>(_ => Mock.Of<IQuickBooksService>());
        services.AddScoped<IGlobalSearchService>(_ => Mock.Of<IGlobalSearchService>());

        // Repository mocks required by ScopedPanelBase<TViewModel> panel activation paths in integration tests
        services.AddScoped<BusinessInterfaces.IBudgetRepository>(_ => Mock.Of<BusinessInterfaces.IBudgetRepository>());
        services.AddScoped<BusinessInterfaces.IEnterpriseRepository>(_ => Mock.Of<BusinessInterfaces.IEnterpriseRepository>());
        services.AddScoped<BusinessInterfaces.IAccountsRepository>(_ => Mock.Of<BusinessInterfaces.IAccountsRepository>());
        services.AddScoped<BusinessInterfaces.IMunicipalAccountRepository>(_ => Mock.Of<BusinessInterfaces.IMunicipalAccountRepository>());
        services.AddScoped<BusinessInterfaces.IUtilityBillRepository>(_ => Mock.Of<BusinessInterfaces.IUtilityBillRepository>());
        services.AddScoped<BusinessInterfaces.IUtilityCustomerRepository>(_ => Mock.Of<BusinessInterfaces.IUtilityCustomerRepository>());
        services.AddScoped<BusinessInterfaces.IDepartmentRepository>(_ => Mock.Of<BusinessInterfaces.IDepartmentRepository>());
        services.AddScoped<BusinessInterfaces.IActivityLogRepository>(_ => Mock.Of<BusinessInterfaces.IActivityLogRepository>());

        // ViewModels needed by ribbon/nav panel activation in integration tests
        services.AddScoped<IDashboardViewModel, DashboardViewModel>();
        services.AddScoped<DashboardViewModel>();
        services.AddScoped<MainViewModel>();
        services.AddScoped<AccountsViewModel>();
        services.AddScoped<ActivityLogViewModel>();
        services.AddScoped<AnalyticsHubViewModel>();
        services.AddScoped<AuditLogViewModel>();
        services.AddScoped<BudgetViewModel>();
        services.AddScoped<CustomersViewModel>();
        services.AddScoped<DepartmentSummaryViewModel>();
        services.AddScoped<QuickBooksViewModel>();
        services.AddScoped<RecommendedMonthlyChargeViewModel>();
        services.AddScoped<ReportsViewModel>();
        services.AddScoped<RevenueTrendsViewModel>();
        services.AddScoped<SettingsViewModel>();
        services.AddScoped<UtilityBillViewModel>();
        services.AddScoped<WarRoomViewModel>();
        services.AddScoped<WileyWidget.WinForms.Controls.Supporting.JARVISChatViewModel>();
        services.AddWindowsFormsBlazorWebView();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Builds a service provider with AI-specific mocked dependencies for isolated AI component testing.
    /// </summary>
    public static ServiceProvider BuildAITestProvider(Dictionary<string, string?>? overrides = null, Action<ServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();

        var defaults = new Dictionary<string, string?>
        {
            ["XAI:Model"] = "grok-4-1-fast-reasoning",
            ["XAI:Endpoint"] = "https://api.x.ai/v1",
            ["XAI:AutoSelectModelOnStartup"] = "false",
            ["XAI:DefaultPresencePenalty"] = "0.0",
            ["XAI:DefaultFrequencyPenalty"] = "0.0"
        };

        // Merge overrides
        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                defaults[kvp.Key] = kvp.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddDebug());

        // Mock AI dependencies
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IChatBridgeService>(Mock.Of<IChatBridgeService>());
        services.AddSingleton<IJARVISPersonalityService>(Mock.Of<IJARVISPersonalityService>());
        services.AddSingleton<IXaiModelDiscoveryService>(Mock.Of<IXaiModelDiscoveryService>());
        services.AddSingleton<IGrokApiKeyProvider>(Mock.Of<IGrokApiKeyProvider>());
        services.AddSingleton<IAILoggingService>(Mock.Of<IAILoggingService>());

        // Allow custom service configuration
        configureServices?.Invoke(services);

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

    private const string ArtifactsDirEnv = "WILEYWIDGET_TEST_ARTIFACTS_DIR";
    private const string ScreenshotOnFailureEnv = "WILEYWIDGET_TEST_SCREENSHOT_ON_FAILURE";
    private const string DashboardAutoLoadEnv = "WILEYWIDGET_TEST_DASHBOARD_AUTOLOAD";
    private const string DisableDockingEnv = "WILEYWIDGET_TEST_DISABLE_DOCKING";
    private const string DisableRibbonEnv = "WILEYWIDGET_TEST_DISABLE_RIBBON";

    internal static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        Action<string>? onTimeout = null,
        CancellationToken ct = default)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            if (condition())
            {
                return true;
            }

            await Task.Delay(interval, ct).ConfigureAwait(false);
        }

        onTimeout?.Invoke($"Condition not met within {timeout}.");
        return false;
    }

    internal static string DumpControlTreeToFile(Control root, string fileName = "control-tree.txt")
    {
        var dir = GetArtifactsDirectory();
        var path = Path.Combine(dir, fileName);
        var builder = new StringBuilder();
        DumpControlTree(root, builder, 0);
        File.WriteAllText(path, builder.ToString());
        return path;
    }

    internal static string? TryCaptureScreenshot(Control root, string fileName = "screenshot.png")
    {
        if (!IsScreenshotEnabled() || root == null || !root.IsHandleCreated || root.Width <= 0 || root.Height <= 0)
        {
            return null;
        }

        var dir = GetArtifactsDirectory();
        var path = Path.Combine(dir, fileName);
        using var bitmap = new Bitmap(root.Width, root.Height);
        root.DrawToBitmap(bitmap, new Rectangle(Point.Empty, root.Size));
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    private static void ApplyEnvironmentOverrides(IDictionary<string, string?> settings)
    {
        var autoLoad = Environment.GetEnvironmentVariable(DashboardAutoLoadEnv);
        if (!string.IsNullOrWhiteSpace(autoLoad))
        {
            settings["UI:AutoShowDashboard"] = autoLoad;
        }

        var disableDocking = Environment.GetEnvironmentVariable(DisableDockingEnv);
        if (bool.TryParse(disableDocking, out var disableDockingValue))
        {
            settings["UI:UseSyncfusionDocking"] = (!disableDockingValue).ToString();
        }

        var disableRibbon = Environment.GetEnvironmentVariable(DisableRibbonEnv);
        if (bool.TryParse(disableRibbon, out var disableRibbonValue))
        {
            settings["UI:ShowRibbon"] = (!disableRibbonValue).ToString();
        }
    }

    private static bool IsScreenshotEnabled()
    {
        var value = Environment.GetEnvironmentVariable(ScreenshotOnFailureEnv);
        return bool.TryParse(value, out var enabled) && enabled;
    }

    private static string GetArtifactsDirectory()
    {
        var dir = Environment.GetEnvironmentVariable(ArtifactsDirEnv);
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, "artifacts");
        }

        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DumpControlTree(Control control, StringBuilder builder, int depth)
    {
        builder.Append(' ', depth * 2)
            .Append(control.Name)
            .Append(" : ")
            .Append(control.GetType().FullName)
            .Append(" (Visible=")
            .Append(control.Visible)
            .Append(", Bounds=")
            .Append(control.Bounds)
            .AppendLine(")");

        foreach (Control child in control.Controls)
        {
            DumpControlTree(child, builder, depth + 1);
        }
    }
}
