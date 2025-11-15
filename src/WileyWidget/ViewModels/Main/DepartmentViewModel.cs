using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class DepartmentViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<DepartmentViewModel> _logger;

        private string _title = "Departments";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private ObservableCollection<Department> _departments = new();
        public ObservableCollection<Department> Departments
        {
            get => _departments;
            set => SetProperty(ref _departments, value);
        }

        public DepartmentViewModel(ILogger<DepartmentViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Departments View");
            LoadDepartmentsAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Departments View");
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading departments");
                // Load departments here
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
    }

    public class Department
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manager { get; set; } = string.Empty;
    }
}