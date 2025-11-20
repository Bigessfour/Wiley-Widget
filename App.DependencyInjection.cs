// In ServicesConfiguration.cs or wherever DI is set up
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.ViewModels;
using WileyWidget.Views;

public class ServicesConfiguration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Register BudgetOverview page and ViewModel
        services.AddSingleton<BudgetOverviewViewModel>();
        services.AddSingleton<BudgetOverviewPage>();
    }
}
