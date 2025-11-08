using Prism.Mvvm;
using System.Collections.ObjectModel;
using WileyWidget.Models;

namespace WileyWidget.ViewModels.Panels {
    /// <summary>
    /// ViewModel for the Enterprise Panel View
    /// </summary>
    public class EnterprisePanelViewModel : BindableBase
    {
        public EnterprisePanelViewModel()
        {
        }

        // Core data collections
        public ObservableCollection<Enterprise> Enterprises { get; } = new();

        private Enterprise _selectedEnterprise;
        public Enterprise SelectedEnterprise
        {
            get => _selectedEnterprise;
            set
            {
                if (_selectedEnterprise != value)
                {
                    _selectedEnterprise = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<EnterpriseTypeItem> EnterpriseTypes { get; } = new();

        // Form properties
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    RaisePropertyChanged();
                }
            }
        }

        private DateTime _fiscalYearStart = new DateTime(DateTime.Now.Year, 1, 1);
        public DateTime FiscalYearStart
        {
            get => _fiscalYearStart;
            set
            {
                if (_fiscalYearStart != value)
                {
                    _fiscalYearStart = value;
                    RaisePropertyChanged();
                }
            }
        }

        // UI state
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    RaisePropertyChanged();
                }
            }
        }
    }
}
