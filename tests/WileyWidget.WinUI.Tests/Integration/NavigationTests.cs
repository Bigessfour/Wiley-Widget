using System;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinUI.ViewModels.Main;
using WileyWidget.WinUI.Behaviors;

namespace WileyWidget.WinUI.Tests.Integration;

public class NavigationTests
{
    [Fact]
    public void DiResolution_WithoutPrism_ResolvesViewModels()
    {
        var services = new ServiceCollection();
        // Register a mock logger required by MainViewModel
        var mockLogger = new Mock<ILogger<MainViewModel>>().Object;
        services.AddSingleton(mockLogger);
        services.AddTransient<MainViewModel>();

        var provider = services.BuildServiceProvider();

        var vm = provider.GetService<MainViewModel>();
        vm.Should().NotBeNull();
    }

    [Fact]
    public void DefaultRegionFactory_ProducesActivatableRegion()
    {
        var region = RegionFactory.CreateDefaultRegion();
        var view = new object();

        region.Views.Should().BeEmpty();
        region.ActiveViews.Should().BeEmpty();

        region.Activate(view);

        region.Views.Should().Contain(view);
        region.ActiveViews.Should().Contain(view);

        region.Deactivate(view);
        region.Views.Should().Contain(view);
        region.ActiveViews.Should().NotContain(view);
    }

    [Fact]
    public void NavigationJournal_CanStoreCurrentEntry()
    {
        var nav = new NavigationService();
        nav.Journal.CurrentEntry = new NavigationEntry { Uri = new Uri("app://test") };
        nav.Journal.CurrentEntry.Should().NotBeNull();
        nav.Journal.CurrentEntry!.Uri!.ToString().Should().Contain("test");
    }
}
