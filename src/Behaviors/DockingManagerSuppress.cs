using System.Windows;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// Provides attached properties to temporarily suppress ActiveWindowChanged handling
    /// during bulk layout operations (load/apply default layout).
    /// </summary>
    public static class DockingManagerSuppress
    {
        public static readonly DependencyProperty SuppressActiveWindowEventsProperty =
            DependencyProperty.RegisterAttached(
                "SuppressActiveWindowEvents",
                typeof(bool),
                typeof(DockingManagerSuppress),
                new PropertyMetadata(false));

        public static void SetSuppressActiveWindowEvents(DependencyObject element, bool value)
        {
            element.SetValue(SuppressActiveWindowEventsProperty, value);
        }

        public static bool GetSuppressActiveWindowEvents(DependencyObject element)
        {
            return (bool)element.GetValue(SuppressActiveWindowEventsProperty);
        }

        // Optional: store last active window name while suppression is enabled for diagnostics
        public static readonly DependencyProperty LastActiveWindowNameProperty =
            DependencyProperty.RegisterAttached(
                "LastActiveWindowName",
                typeof(string),
                typeof(DockingManagerSuppress),
                new PropertyMetadata(string.Empty));

        public static void SetLastActiveWindowName(DependencyObject element, string value)
        {
            element.SetValue(LastActiveWindowNameProperty, value);
        }

        public static string GetLastActiveWindowName(DependencyObject element)
        {
            return (string)element.GetValue(LastActiveWindowNameProperty);
        }
    }
}
