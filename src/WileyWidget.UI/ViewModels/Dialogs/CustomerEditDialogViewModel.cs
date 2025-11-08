using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using WileyWidget.Models;

namespace WileyWidget.ViewModels.Dialogs {
    public class CustomerEditDialogViewModel : BindableBase, IDialogAware, INotifyDataErrorInfo
    {
        private UtilityCustomer _customer = new UtilityCustomer();
        public UtilityCustomer Customer
        {
            get => _customer;
            set
            {
                if (_customer != null)
                {
                    _customer.PropertyChanged -= OnInnerCustomerPropertyChanged;
                }

                if (SetProperty(ref _customer, value))
                {
                    if (_customer != null)
                    {
                        _customer.PropertyChanged += OnInnerCustomerPropertyChanged;
                    }

                    // Notify that the Customer reference changed and update SaveCommand CanExecute
                    RaisePropertyChanged(nameof(Customer));
                    SaveCommand?.RaiseCanExecuteChanged();
                }
            }
        }

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

        public CustomerEditDialogViewModel()
        {
            SaveCommand = new DelegateCommand(OnSave, CanSave).ObservesProperty(() => Customer.FirstName).ObservesProperty(() => Customer.LastName);
            CancelCommand = new DelegateCommand(OnCancel);
            // Ensure we listen to changes on the initial customer instance
            if (_customer != null)
            {
                _customer.PropertyChanged += OnInnerCustomerPropertyChanged;
            }
        }

        private void OnSave()
        {
            // Validate before closing
            ValidateAll();
            if (HasErrors) return;

            var result = new DialogResult(ButtonResult.OK);
            result.Parameters.Add("customer", Customer);
            RequestClose.Invoke(result);
        }

        private bool CanSave() => !HasErrors;

        private void OnCancel()
        {
            var cancel = new DialogResult(ButtonResult.Cancel);
            cancel.Parameters.Add("canceled", true);
            RequestClose.Invoke(cancel);
        }

        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters != null && parameters.ContainsKey("customer"))
            {
                var c = parameters.GetValue<UtilityCustomer>("customer");
                Customer = c != null ? CloneCustomer(c) : new UtilityCustomer();
            }
            // Validate initial model and populate any errors
            ValidateModel();
        }

        private UtilityCustomer CloneCustomer(UtilityCustomer src)
        {
            // shallow clone for editing in dialog
            return new UtilityCustomer
            {
                Id = src.Id,
                AccountNumber = src.AccountNumber,
                FirstName = src.FirstName,
                LastName = src.LastName,
                CompanyName = src.CompanyName,
                CustomerType = src.CustomerType,
                ServiceAddress = src.ServiceAddress,
                ServiceCity = src.ServiceCity,
                ServiceState = src.ServiceState,
                ServiceZipCode = src.ServiceZipCode,
                PhoneNumber = src.PhoneNumber,
                EmailAddress = src.EmailAddress,
                MeterNumber = src.MeterNumber,
                CurrentBalance = src.CurrentBalance,
                Status = src.Status,
                AccountOpenDate = src.AccountOpenDate,
                Notes = src.Notes
            };
        }

        private void ValidateAll()
        {
            // Deprecated single-property helper; prefer ValidateModel which uses model validators
            ValidateModel();
        }

        private void ValidateModel()
        {
            _errors.Clear();

            if (Customer == null)
            {
                ValidationSummary = "Customer is null.";
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(null));
                SaveCommand?.RaiseCanExecuteChanged();
                return;
            }

            var context = new ValidationContext(Customer);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(Customer, context, results, validateAllProperties: true);

            // Also include IValidatableObject.Validate results
            try
            {
                var vv = (Customer as IValidatableObject) ?? Customer as IValidatableObject;
                if (vv != null)
                {
                    var extra = vv.Validate(context);
                    if (extra != null)
                    {
                        results.AddRange(extra);
                    }
                }
            }
            catch
            {
                // ignore validation exceptions and continue
            }

            foreach (var r in results)
            {
                foreach (var member in r.MemberNames.DefaultIfEmpty(string.Empty))
                {
                    var key = string.IsNullOrEmpty(member) ? string.Empty : member;
                    if (!_errors.TryGetValue(key, out var list))
                    {
                        list = new List<string>();
                        _errors[key] = list;
                    }
                    if (!string.IsNullOrEmpty(r.ErrorMessage)) list.Add(r.ErrorMessage);

                    // Also populate the prefixed form so WPF bindings targeting "Customer.Property" can find errors
                    if (!string.IsNullOrEmpty(key)) _errors[$"{nameof(Customer)}.{key}"] = _errors[key];
                }
            }

            // Build summary
            ValidationSummary = string.Join(" \n", results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)));

            // Notify error changes for all affected properties
            var keys = _errors.Keys.ToList();
            if (keys.Count == 0 && results.Count == 0)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(null));
            }
            else
            {
                foreach (var k in keys)
                {
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(k));
                }
            }

            SaveCommand?.RaiseCanExecuteChanged();
        }

        #region INotifyDataErrorInfo (lightweight pass-through)
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return _errors.Values.SelectMany(e => e);
            }

            // Support both "PropertyName" and "Customer.PropertyName" keys so WPF bindings find errors whether they
            // request the nested path or the raw property name.
            if (_errors.TryGetValue(propertyName, out var list)) return list;

            const string prefix = nameof(Customer) + ".";
            if (propertyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && _errors.TryGetValue(propertyName.Substring(prefix.Length), out var shortList)) return shortList;

            // If asked for the short name but we have stored the prefixed key, try that too
            var prefixed = prefix + propertyName;
            if (_errors.TryGetValue(prefixed, out var prefixedList)) return prefixedList;

            return Enumerable.Empty<string>();
        }

        public bool HasErrors => _errors.Any();

        private void ValidateProperty(string propertyName, object? value)
        {
            var list = new List<string>();

            // Normalize to short property name (e.g. "FirstName") for switch comparisons
            var shortName = propertyName?.StartsWith(nameof(Customer) + ".", StringComparison.OrdinalIgnoreCase) == true
                ? propertyName.Substring((nameof(Customer) + ".").Length)
                : propertyName;

            switch (shortName)
            {
                case nameof(Customer.FirstName):
                    if (string.IsNullOrWhiteSpace(value as string)) list.Add("First name is required.");
                    break;
                case nameof(Customer.LastName):
                    if (string.IsNullOrWhiteSpace(value as string)) list.Add("Last name is required.");
                    break;
                case nameof(Customer.AccountNumber):
                    if (string.IsNullOrWhiteSpace(value as string)) list.Add("Account number is required.");
                    break;
                case nameof(Customer.ServiceZipCode):
                    if (!string.IsNullOrWhiteSpace(value as string) && !System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "", "^\\d{5}(-\\d{4})?$")) list.Add("ZIP code must be 12345 or 12345-6789.");
                    break;
            }

            // Store errors for both the short name and the prefixed "Customer.Property" key so WPF will find them.
            _errors[shortName ?? string.Empty] = list;
            _errors[$"{nameof(Customer)}.{shortName}"] = list;

            // Notify listeners for both key forms
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(shortName));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs($"{nameof(Customer)}.{shortName}"));
            // Update Save command availability
            SaveCommand?.RaiseCanExecuteChanged();
        }
        #endregion

        private void OnInnerCustomerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.PropertyName)) return;

            // Validate the changed property
            switch (e.PropertyName)
            {
                case nameof(UtilityCustomer.FirstName):
                    ValidateModel();
                    break;
                case nameof(UtilityCustomer.LastName):
                    ValidateModel();
                    break;
                case nameof(UtilityCustomer.AccountNumber):
                    ValidateModel();
                    break;
                case nameof(UtilityCustomer.ServiceZipCode):
                    ValidateModel();
                    break;
            }

            // Raise a nested PropertyChanged so ObservesProperty(() => Customer.FirstName) can pick it up
            RaisePropertyChanged($"{nameof(Customer)}.{e.PropertyName}");
            SaveCommand?.RaiseCanExecuteChanged();
        }
    }
}
