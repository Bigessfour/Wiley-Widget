using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WileyWidget.Services
{
    // Simple attached property to enable binding the PasswordBox.Password value to a ViewModel property.
    // Not perfect for SecureString semantics but acceptable for this application's existing pattern.
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper), new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static string GetBoundPassword(DependencyObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return (string)obj.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject obj, string value)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.SetValue(BoundPasswordProperty, value);
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox pb)
            {
                pb.PasswordChanged -= Pb_PasswordChanged;
                var newVal = e.NewValue as string ?? string.Empty;
                if (pb.Password != newVal)
                    pb.Password = newVal;
                pb.PasswordChanged += Pb_PasswordChanged;
            }
        }

        private static void Pb_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                SetBoundPassword(pb, pb.Password);
            }
        }
    }
}
