using System.Windows.Forms;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class QuickBooksPanelIntegrationTests
{
    [StaFact]
    public void ShowQuickBooksPanel_AddsPanelToDockingHost()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, _, _, _, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<PanelNavigationService>>(provider);
        var navigator = new PanelNavigationService(dockingManager, form, provider, navLogger);

        navigator.ShowPanel<QuickBooksPanel>("QuickBooks Synchronization", DockingStyle.Right);

        var panel = FindControl<QuickBooksPanel>(form);
        panel.Should().NotBeNull();
    }

    [StaFact]
    public void ApplySplitterMinSizesWithConstraintCheck_NarrowContainer_DoesNotThrowAndKeepsInvariant()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<ScopedPanelBase<QuickBooksViewModel>>>(provider);

        using var panel = new QuickBooksPanel(scopeFactory, logger);
        using var host = new Form { Width = 160, Height = 120 };
        using var splitter = CreateHostedSplitter(host, width: 88, height: 96, Orientation.Vertical, splitterWidth: 8);

        FluentActions.Invoking(() =>
                InvokePrivate(panel, "ApplySplitterMinSizesWithConstraintCheck", splitter, 240, 180, "Test"))
            .Should().NotThrow();

        AssertSplitterMinSizesWithinBounds(splitter);
    }

    [StaFact]
    public void AdjustMinSizesForCurrentWidth_NarrowLayout_DoesNotThrowAndKeepsAllSplittersValid()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ILogger<ScopedPanelBase<QuickBooksViewModel>>>(provider);

        using var panel = new QuickBooksPanel(scopeFactory, logger);
        using var host = new Form { Width = 220, Height = 180 };
        using var topSplitter = CreateHostedSplitter(host, width: 105, height: 90, Orientation.Vertical, splitterWidth: 8);
        using var bottomSplitter = CreateHostedSplitter(host, width: 95, height: 90, Orientation.Vertical, splitterWidth: 8);
        using var mainSplitter = CreateHostedSplitter(host, width: 110, height: 110, Orientation.Vertical, splitterWidth: 8);

        SetPrivateField(panel, "_splitContainerTop", topSplitter);
        SetPrivateField(panel, "_splitContainerBottom", bottomSplitter);
        SetPrivateField(panel, "_splitContainerMain", mainSplitter);

        FluentActions.Invoking(() => InvokePrivate(panel, "AdjustMinSizesForCurrentWidth"))
            .Should().NotThrow();

        AssertSplitterMinSizesWithinBounds(topSplitter);
        AssertSplitterMinSizesWithinBounds(bottomSplitter);
        AssertSplitterMinSizesWithinBounds(mainSplitter);
    }

    private static SplitContainerAdv CreateHostedSplitter(
        Control host,
        int width,
        int height,
        Orientation orientation,
        int splitterWidth)
    {
        var splitter = new SplitContainerAdv
        {
            Orientation = orientation,
            Width = width,
            Height = height,
            SplitterWidth = splitterWidth
        };

        host.Controls.Add(splitter);
        _ = host.Handle;
        host.CreateControl();
        splitter.CreateControl();
        splitter.Panel1MinSize = 0;
        splitter.Panel2MinSize = 0;

        return splitter;
    }

    private static void AssertSplitterMinSizesWithinBounds(SplitContainerAdv splitter)
    {
        var containerDim = splitter.Orientation == Orientation.Horizontal ? splitter.Height : splitter.Width;
        var available = Math.Max(0, containerDim - Math.Max(0, splitter.SplitterWidth));

        splitter.Panel1MinSize.Should().BeGreaterThanOrEqualTo(0);
        splitter.Panel2MinSize.Should().BeGreaterThanOrEqualTo(0);
        (splitter.Panel1MinSize + splitter.Panel2MinSize).Should().BeLessThanOrEqualTo(available);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"Field '{fieldName}' should exist for test setup");
        field!.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object?[]? args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"Method '{methodName}' should exist for test invocation");
        method!.Invoke(target, args);
    }

    private static TPanel? FindControl<TPanel>(Control root) where TPanel : Control
    {
        if (root is TPanel match)
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindControl<TPanel>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
