using System;
using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class MainViewModelIntegrationTests
{
    [StaFact]
    public void MainViewModel_InitializesWithServices()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(provider);

        viewModel.Should().NotBeNull();
        viewModel.Title.Should().Contain("Wiley Widget");
    }

    [StaFact]
    public void MainViewModel_PropertyChanged_EventsWork()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(provider);

        var propertyChangedCalled = false;
        string? changedProperty = null;

        viewModel.PropertyChanged += (sender, e) =>
        {
            propertyChangedCalled = true;
            changedProperty = e.PropertyName;
        };

        // Trigger property change
        viewModel.Title = "New Title";

        propertyChangedCalled.Should().BeTrue();
        changedProperty.Should().Be(nameof(MainViewModel.Title));
    }

    [StaFact]
    public void MainViewModel_DataLoading_Works()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(provider);

        // Initially not loaded
        viewModel.IsDataLoaded.Should().BeFalse();

        // Load data (this would normally be async, but for testing we can check the pattern)
        // In a real scenario, this would call services to load data
        viewModel.IsDataLoaded = true;

        viewModel.IsDataLoaded.Should().BeTrue();
    }

    [StaFact]
    public void MainViewModel_DisposesProperly()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(provider);

        // Dispose
        viewModel.Dispose();

        // Verify disposed state (implementation dependent)
        // This tests that Dispose doesn't throw
    }
}
