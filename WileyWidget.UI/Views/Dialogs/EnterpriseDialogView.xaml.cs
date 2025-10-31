using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.Models;
using WileyWidget.ViewModels;

namespace WileyWidget.Views.Dialogs {
    /// <summary>
    /// Interaction logic for EnterpriseDialogView.xaml
    /// </summary>
    public partial class EnterpriseDialogView : Window
    {
        public EnterpriseDialogView()
        {
            InitializeComponent();
            // DataContext will be set by Prism dialog service
        }
        public EnterpriseDialogView(Enterprise enterprise) : this()
        {
            // For backward compatibility - parameters will be handled by ViewModel via IDialogParameters
        }

        // Note: Static ShowDialog method removed - now using Prism IDialogService
    }

    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value is string str && string.IsNullOrWhiteSpace(str))
            {
                return new ValidationResult(false, "This field is required.");
            }
            return ValidationResult.ValidResult;
        }
    }
}
