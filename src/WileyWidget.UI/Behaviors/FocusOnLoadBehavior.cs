using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WileyWidget.UI.Behaviors
{
    /// <summary>
    /// Attached behavior to set keyboard focus when a control finishes loading.
    /// Usage: behaviors:FocusOnLoadBehavior.IsEnabled="True" and optional behaviors:FocusOnLoadBehavior.TargetName="ElementName"
    /// </summary>
    public class FocusOnLoadBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FocusOnLoadBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            element.SetValue(IsEnabledProperty, value);
        }
        public static bool GetIsEnabled(DependencyObject element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            return (bool)element.GetValue(IsEnabledProperty);
        }

        public static readonly DependencyProperty TargetNameProperty = DependencyProperty.RegisterAttached(
            "TargetName",
            typeof(string),
            typeof(FocusOnLoadBehavior),
            new PropertyMetadata(null));

        public static void SetTargetName(DependencyObject element, string? value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            element.SetValue(TargetNameProperty, value);
        }
        public static string? GetTargetName(DependencyObject element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            return (string?)element.GetValue(TargetNameProperty);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                if ((bool)e.NewValue)
                {
                    fe.Loaded += OnLoaded;
                }
                else
                {
                    fe.Loaded -= OnLoaded;
                }
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe)
                return;

            fe.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Try to bring the window to foreground first
                if (Window.GetWindow(fe) is Window win && !win.IsActive)
                {
                    win.Activate();
                }

                IInputElement? target = fe;
                var targetName = GetTargetName(fe);
                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    if (FindDescendantByName(fe, targetName!) is FrameworkElement named)
                    {
                        target = named;
                    }
                }

                if (target is FrameworkElement targetFe)
                {
                    // Ensure focusable where reasonable
                    if (!targetFe.Focusable)
                    {
                        targetFe.Focusable = true;
                    }
                    targetFe.Focus();
                }
                else
                {
                    Keyboard.Focus(target);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static FrameworkElement? FindDescendantByName(FrameworkElement root, string name)
        {
            if (root.Name == name)
                return root;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(root, i) is FrameworkElement child)
                {
                    var result = FindDescendantByName(child, name);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }
    }
}
