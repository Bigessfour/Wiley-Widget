using System.Windows;

namespace WileyWidget.Helpers
{
    /// <summary>
    /// Provides attached properties for simulating separators in Syncfusion CustomMenuItem collections.
    /// </summary>
    public static class SeparatorExt
    {
        /// <summary>
        /// Identifies the IsSeparator attached property.
        /// </summary>
        public static readonly DependencyProperty IsSeparatorProperty =
            DependencyProperty.RegisterAttached("IsSeparator", typeof(bool), typeof(SeparatorExt), new PropertyMetadata(false));

        /// <summary>
        /// Gets the value of the IsSeparator attached property.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <returns>True if the object should be rendered as a separator.</returns>
        public static bool GetIsSeparator(DependencyObject obj) => (bool)obj.GetValue(IsSeparatorProperty);

        /// <summary>
        /// Sets the value of the IsSeparator attached property.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="value">True to render as a separator.</param>
        public static void SetIsSeparator(DependencyObject obj, bool value) => obj.SetValue(IsSeparatorProperty, value);
    }
}
