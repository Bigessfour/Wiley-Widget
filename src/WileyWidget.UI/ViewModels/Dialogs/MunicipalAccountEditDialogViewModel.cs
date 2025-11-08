using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel for the Municipal Account Edit Dialog
    /// Handles creating and editing municipal accounts with validation
    /// </summary>
    public class MunicipalAccountEditDialogViewModel : BindableBase, IDialogAware, INotifyDataErrorInfo
    {
        private readonly IMunicipalAccountRepository? _accountRepository;
        private readonly IDepartmentRepository? _departmentRepository;

        private MunicipalAccount _account = new MunicipalAccount();
        private Department? _selectedDepartment;
        private string _dialogTitle = "Edit Municipal Account";

        public MunicipalAccount Account
        {
            get => _account;
            set
            {
                if (_account != null)
                {
                    _account.PropertyChanged -= OnInnerAccountPropertyChanged;
                }

                if (SetProperty(ref _account, value))
                {
                    if (_account != null)
                    {
                        _account.PropertyChanged += OnInnerAccountPropertyChanged;
                    }

                    // Update dialog title based on whether this is a new or existing account
                    DialogTitle = _account.Id == 0 ? "Add Municipal Account" : "Edit Municipal Account";

                    // Notify that the Account reference changed and update SaveCommand CanExecute
                    RaisePropertyChanged(nameof(Account));
                    SaveCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public Department? SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                if (SetProperty(ref _selectedDepartment, value) && _account != null)
                {
                    _account.DepartmentId = value?.Id ?? 0;
                    _account.Department = value;
                }
            }
        }

        // Properties for binding to the view
        public string AccountNumber
        {
            get => _account?.AccountNumber?.Value ?? string.Empty;
            set
            {
                if (_account != null)
                {
                    _account.AccountNumber = new AccountNumber(value);
                    RaisePropertyChanged();
                }
            }
        }

        public string Name
        {
            get => _account?.Name ?? string.Empty;
            set
            {
                if (_account != null)
                {
                    _account.Name = value;
                    RaisePropertyChanged();
                }
            }
        }

        public AccountType Type
        {
            get => _account?.Type ?? AccountType.Asset;
            set
            {
                if (_account != null)
                {
                    _account.Type = value;
                    RaisePropertyChanged();
                }
            }
        }

        public MunicipalFundType Fund
        {
            get => _account?.Fund ?? MunicipalFundType.General;
            set
            {
                if (_account != null)
                {
                    _account.Fund = value;
                    RaisePropertyChanged();
                }
            }
        }

        public decimal Balance
        {
            get => _account?.Balance ?? 0;
            set
            {
                if (_account != null)
                {
                    _account.Balance = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Notes
        {
            get => _account?.Notes ?? string.Empty;
            set
            {
                if (_account != null)
                {
                    _account.Notes = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Collections for combo boxes
        public IEnumerable<AccountType> AccountTypeValues => Enum.GetValues(typeof(AccountType)).Cast<AccountType>();
        public IEnumerable<MunicipalFundType> FundTypeValues => Enum.GetValues(typeof(MunicipalFundType)).Cast<MunicipalFundType>();
        public IEnumerable<Department> Departments { get; private set; } = new List<Department>();

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private readonly Dictionary<string, List<string>> _errors = new();
        private string _validationSummary = string.Empty;

        public string ValidationSummary
        {
            get => _validationSummary;
            private set => SetProperty(ref _validationSummary, value);
        }

        private bool _showAcrylicBackground = true;

        public bool ShowAcrylicBackground
        {
            get => _showAcrylicBackground;
            set => SetProperty(ref _showAcrylicBackground, value);
        }

        public MunicipalAccountEditDialogViewModel(
            IMunicipalAccountRepository? accountRepository = null,
            IDepartmentRepository? departmentRepository = null)
        {
            _accountRepository = accountRepository;
            _departmentRepository = departmentRepository;

            SaveCommand = new DelegateCommand(OnSave, CanSave);
            CancelCommand = new DelegateCommand(OnCancel);

            // Ensure we listen to changes on the initial account instance
            if (_account != null)
            {
                _account.PropertyChanged += OnInnerAccountPropertyChanged;
            }

            // Load departments asynchronously
            _ = LoadDepartmentsAsync();
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                if (_departmentRepository != null)
                {
                    var departments = await _departmentRepository.GetAllAsync();
                    Departments = departments?.ToList() ?? new List<Department>();
                    RaisePropertyChanged(nameof(Departments));
                }
            }
            catch (Exception ex)
            {
                // Log error but don't show to user in dialog
                System.Diagnostics.Debug.WriteLine($"Failed to load departments: {ex.Message}");
            }
        }

        private void OnSave()
        {
            // Validate before closing
            ValidateAll();
            if (HasErrors) return;

            var result = new DialogResult(ButtonResult.OK);
            result.Parameters.Add("account", Account);
            RequestClose.Invoke(result);
        }

        private bool CanSave() => !HasErrors && !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(Name);

        private void OnCancel()
        {
            var cancel = new DialogResult(ButtonResult.Cancel);
            cancel.Parameters.Add("canceled", true);
            RequestClose.Invoke(cancel);
        }

        private void OnInnerAccountPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Re-validate when account properties change
            ValidateProperty(e.PropertyName);
            SaveCommand?.RaiseCanExecuteChanged();
        }

        private void ValidateAll()
        {
            _errors.Clear();

            ValidateProperty(nameof(AccountNumber));
            ValidateProperty(nameof(Name));
            ValidateProperty(nameof(Balance));

            UpdateValidationSummary();
        }

        private void ValidateProperty(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            var errors = new List<string>();

            switch (propertyName)
            {
                case nameof(AccountNumber):
                    if (string.IsNullOrWhiteSpace(AccountNumber))
                        errors.Add("Account number is required");
                    else if (!System.Text.RegularExpressions.Regex.IsMatch(AccountNumber, @"^\d+([.-]\d+)*$"))
                        errors.Add("Account number must be numeric with optional separators (dots or hyphens)");
                    break;

                case nameof(Name):
                    if (string.IsNullOrWhiteSpace(Name))
                        errors.Add("Account name is required");
                    else if (Name.Length > 100)
                        errors.Add("Account name cannot exceed 100 characters");
                    break;

                case nameof(Balance):
                    if (Balance < -999999999.99m || Balance > 999999999.99m)
                        errors.Add("Balance must be between -999,999,999.99 and 999,999,999.99");
                    break;
            }

            if (errors.Any())
                _errors[propertyName] = errors;
            else
                _errors.Remove(propertyName);

            UpdateValidationSummary();
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void UpdateValidationSummary()
        {
            var allErrors = _errors.Values.SelectMany(e => e).ToList();
            ValidationSummary = allErrors.Any() ? string.Join(Environment.NewLine, allErrors) : string.Empty;
        }

        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters != null && parameters.ContainsKey("account"))
            {
                var account = parameters.GetValue<MunicipalAccount>("account");
                if (account != null)
                {
                    Account = account;
                    SelectedDepartment = account.Department;
                }
            }
            else
            {
                // New account
                Account = new MunicipalAccount();
            }
        }

        #region INotifyDataErrorInfo Implementation

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return _errors.Values.SelectMany(e => e);
            return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
        }

        public bool HasErrors => _errors.Any();

        #endregion
    }
}