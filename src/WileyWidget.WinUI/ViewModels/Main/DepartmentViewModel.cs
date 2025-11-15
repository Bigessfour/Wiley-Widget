using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class DepartmentViewModel : ObservableRecipient, INavigationAware
    {
        private readonly ILogger<DepartmentViewModel> _logger;

        [ObservableProperty]
        private string title = "Departments";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<Department> departments = new();

        public DepartmentViewModel(ILogger<DepartmentViewModel> logger)
        {
            _logger = logger;
            LoadDepartmentsCommand = new AsyncRelayCommand(LoadDepartmentsAsync);
        }

        public IAsyncRelayCommand LoadDepartmentsCommand { get; }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading departments");
                Departments.Clear();
                _logger.LogInformation("Departments loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load departments");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Departments View");
            LoadDepartmentsCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Departments View");
        }
    }

    public class Department
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manager { get; set; } = string.Empty;
    }
}