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

        var defaults = new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "false",
            ["UI:UseSyncfusionDocking"] = "false",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "true",
            ["UI:AutoShowDashboard"] = "true"
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
        services.AddScoped<IAILoggingService>(_ => Mock.Of<IAILoggingService>());
        services.AddScoped<IQuickBooksService>(_ => Mock.Of<IQuickBooksService>());
        services.AddScoped<IGlobalSearchService>(_ => Mock.Of<IGlobalSearchService>());
        services.AddScoped<MainViewModel>();
        services.AddScoped<QuickBooksViewModel>();
        services.AddScoped<WileyWidget.WinForms.Controls.Supporting.JARVISChatViewModel>();
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
