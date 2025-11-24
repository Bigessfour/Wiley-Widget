using System.Threading.Tasks;
using WileyWidget.WinUI.ViewModels.Main;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.IntegrationTests;

public class ViewModelIntegrationTests : IntegrationTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        base.ConfigureServices(services, config);

        // Register ViewModels for testing
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AIAssistViewModel>();
        services.AddTransient<QuickBooksViewModel>();
        services.AddTransient<BudgetViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<EnterpriseViewModel>();
        services.AddTransient<MunicipalAccountViewModel>();
        services.AddTransient<ToolsViewModel>();
    }

    [Fact]
    public void DashboardViewModel_CanBeResolved()
    {
        // Act
        var vm = GetService<DashboardViewModel>();

        // Assert
        Assert.NotNull(vm);
        Assert.NotNull(vm.Title);
    }

    [Fact]
    public async Task DashboardViewModel_LoadsData_FromBackendService()
    {
        // Arrange
        var vm = GetService<DashboardViewModel>();

        // Act
        await vm.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(vm.DashboardItems);
        Assert.NotEmpty(vm.DashboardItems); // Should have seed/test data
        Assert.True(vm.TotalBudget >= 0);
    }

    [Fact]
    public void SettingsViewModel_CanToggleTheme()
    {
        var vm = GetService<SettingsViewModel>();
        var initial = vm.IsDarkTheme;

        vm.ToggleThemeCommand.Execute(null);

        Assert.NotEqual(initial, vm.IsDarkTheme);
    }

    [Fact]
    public void AIAssistViewModel_UsesNullAIService_InTest()
    {
        var vm = GetService<AIAssistViewModel>();
        var aiService = GetService<IAIService>();

        Assert.IsType<NullAIServiceDouble>(aiService);
        // NullAIServiceDouble returns stub responses
    }

    [Fact]
    public void QuickBooksViewModel_DoesNotCrash_WhenQBDisabled()
    {
        var vm = GetService<QuickBooksViewModel>();

        Assert.False(vm.IsConnected);
        Assert.Contains("Not Connected", vm.ConnectionStatus);
    }

    [Fact]
    public void AllMainViewModels_CanBeInstantiated_WithoutException()
    {
        var viewModels = new object[]
        {
            GetService<DashboardViewModel>(),
            GetService<BudgetViewModel>(),
            GetService<AnalyticsViewModel>(),
            GetService<ReportsViewModel>(),
            GetService<SettingsViewModel>(),
            GetService<AIAssistViewModel>(),
            GetService<EnterpriseViewModel>(),
            GetService<MunicipalAccountViewModel>(),
            GetService<ToolsViewModel>()
        };

        foreach (var vm in viewModels)
        {
            Assert.NotNull(vm);
            Assert.IsAssignableFrom<ObservableObject>(vm);
        }
    }
}