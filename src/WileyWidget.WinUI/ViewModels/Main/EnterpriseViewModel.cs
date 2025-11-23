using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class EnterpriseViewModel : ObservableRecipient
    {
        private readonly ILogger<EnterpriseViewModel> _logger;
        private readonly AppDbContext _dbContext;

        [ObservableProperty]
        private string title = "Enterprise Management";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<EnterpriseItemDisplay> enterpriseItems = new();

        [ObservableProperty]
        private decimal totalRevenue;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private int activeEnterprises;

        public EnterpriseViewModel(
            ILogger<EnterpriseViewModel> logger,
            AppDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            LoadEnterpriseDataCommand = new AsyncRelayCommand(LoadEnterpriseDataAsync);
        }

        public IAsyncRelayCommand LoadEnterpriseDataCommand { get; }

        private async Task LoadEnterpriseDataAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading enterprise data");

                var enterprises = await _dbContext.Enterprises
                    .AsNoTracking()
                    .ToListAsync();

                EnterpriseItems.Clear();
                
                foreach (var enterprise in enterprises)
                {
                    var isActive = enterprise is ISoftDeletable softDel ? !softDel.IsDeleted : true;
                    EnterpriseItems.Add(new EnterpriseItemDisplay
                    {
                        Id = enterprise.Id,
                        Name = enterprise.Name,
                        Description = enterprise.Description ?? string.Empty,
                        Type = enterprise.Type?.ToString() ?? "Unknown",
                        IsActive = isActive,
                        CreatedDate = enterprise.CreatedDate
                    });
                }

                ActiveEnterprises = enterprises.Count(e => e is ISoftDeletable sd ? !sd.IsDeleted : true);

                _logger.LogInformation("Enterprise data loaded successfully: {Count} enterprises", EnterpriseItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load enterprise data");
                EnterpriseItems.Clear();
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Display model for enterprise items
    /// </summary>
    public class EnterpriseItemDisplay
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
