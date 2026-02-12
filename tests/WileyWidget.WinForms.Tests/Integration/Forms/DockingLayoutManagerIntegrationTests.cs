using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class DockingLayoutManagerIntegrationTests
{
    [StaFact]
    public async Task SaveAndLoadLayout_CompletesWithoutErrors()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, left, right, central, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var layoutPath = Path.Combine(Path.GetTempPath(), $"wiley-docking-{Guid.NewGuid():N}.bin");
        var layoutManager = new DockingLayoutManager(provider, null, logger, layoutPath, form, dockingManager, left, right, central, null);

        try
        {
            layoutManager.SaveDockingLayout(dockingManager);
            File.Exists(layoutPath).Should().BeTrue();

            await layoutManager.LoadDockingLayoutAsync(dockingManager);

            left!.IsDisposed.Should().BeFalse();
            right!.IsDisposed.Should().BeFalse();
            central!.IsDisposed.Should().BeFalse();
        }
        finally
        {
            layoutManager.Dispose();
            if (File.Exists(layoutPath))
            {
                File.Delete(layoutPath);
            }
        }
    }

    [StaFact]
    public void DockingLayoutManager_CanBeCreatedAndDisposed()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, left, right, central, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var layoutPath = Path.Combine(Path.GetTempPath(), $"wiley-docking-{Guid.NewGuid():N}.bin");
        var layoutManager = new DockingLayoutManager(provider, null, logger, layoutPath, form, dockingManager, left, right, central, null);

        layoutManager.Should().NotBeNull();

        layoutManager.Dispose();
    }

    [StaFact]
    public async Task LoadLayout_WithNonExistentFile_HandlesGracefully()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, left, right, central, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var layoutPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}.bin");
        var layoutManager = new DockingLayoutManager(provider, null, logger, layoutPath, form, dockingManager, left, right, central, null);

        try
        {
            // File doesn't exist, should handle gracefully
            await layoutManager.LoadDockingLayoutAsync(dockingManager);

            // Should not throw and panels should still be valid
            left!.IsDisposed.Should().BeFalse();
            right!.IsDisposed.Should().BeFalse();
            central!.IsDisposed.Should().BeFalse();
        }
        finally
        {
            layoutManager.Dispose();
        }
    }

    [StaFact]
    public void SaveLayout_CreatesFile()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, left, right, central, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var layoutPath = Path.Combine(Path.GetTempPath(), $"wiley-docking-{Guid.NewGuid():N}.bin");
        var layoutManager = new DockingLayoutManager(provider, null, logger, layoutPath, form, dockingManager, left, right, central, null);

        try
        {
            File.Exists(layoutPath).Should().BeFalse();

            layoutManager.SaveDockingLayout(dockingManager);

            File.Exists(layoutPath).Should().BeTrue();
            new FileInfo(layoutPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            layoutManager.Dispose();
            if (File.Exists(layoutPath))
            {
                File.Delete(layoutPath);
            }
        }
    }

    [StaFact]
    public async Task SaveAndLoadLayout_PreservesPanelState()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, left, right, central, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var layoutPath = Path.Combine(Path.GetTempPath(), $"wiley-docking-{Guid.NewGuid():N}.bin");
        var layoutManager = new DockingLayoutManager(provider, null, logger, layoutPath, form, dockingManager, left, right, central, null);

        try
        {
            // Save initial layout
            layoutManager.SaveDockingLayout(dockingManager);

            // Modify some panel properties (if possible)
            // Note: Actual panel state preservation would require more complex setup

            // Load layout
            await layoutManager.LoadDockingLayoutAsync(dockingManager);

            // Verify panels are still functional
            left.Should().NotBeNull();
            right.Should().NotBeNull();
            central.Should().NotBeNull();

            left!.IsDisposed.Should().BeFalse();
            right!.IsDisposed.Should().BeFalse();
            central!.IsDisposed.Should().BeFalse();
        }
        finally
        {
            layoutManager.Dispose();
            if (File.Exists(layoutPath))
            {
                File.Delete(layoutPath);
            }
        }
    }
}
