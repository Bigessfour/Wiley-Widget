using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class DepartmentViewModel : ObservableRecipient
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

        private Task LoadDepartmentsAsync()
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
            return Task.CompletedTask;
        }
    }

    public class Department
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manager { get; set; } = string.Empty;
    }
}