using System;
using System.Windows;
using System.Windows.Input;

namespace WileyWidget.UI.Behaviors
{
    /// <summary>
    /// Sets keyboard focus to the element on mouse interaction.
    /// - OnClick: focus on left mouse button down
    /// - OnFirstMove: optional; focus on first mouse move after load to prime input without stealing focus repeatedly.
    /// Based on Microsoft WPF focus guidance: use Keyboard.Focus and respect focus scopes.
    /// </summary>
    public class MouseFocusBehavior
    {
        public static readonly DependencyProperty EnableOnClickProperty = DependencyProperty.RegisterAttached(
            "EnableOnClick",
            typeof(bool),
            typeof(MouseFocusBehavior),
            new PropertyMetadata(false, OnEnableOnClickChanged));

        public static void SetEnableOnClick(DependencyObject element, bool value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            element.SetValue(EnableOnClickProperty, value);
        }
        public static bool GetEnableOnClick(DependencyObject element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            return (bool)element.GetValue(EnableOnClickProperty);
        }

        public static readonly DependencyProperty EnableOnFirstMoveProperty = DependencyProperty.RegisterAttached(
            "EnableOnFirstMove",
            typeof(bool),
            typeof(MouseFocusBehavior),
            new PropertyMetadata(false, OnEnableOnFirstMoveChanged));

        public static void SetEnableOnFirstMove(DependencyObject element, bool value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            element.SetValue(EnableOnFirstMoveProperty, value);
        }
        public static bool GetEnableOnFirstMove(DependencyObject element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            return (bool)element.GetValue(EnableOnFirstMoveProperty);
        }

        private static void OnEnableOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement ui)
            {
                if ((bool)e.NewValue)
                {
                    ui.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                }
                else
                {
                    ui.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                }
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is IInputElement input)
            {
                Keyboard.Focus(input);
            }
        }

        private static void OnEnableOnFirstMoveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Loaded exists on FrameworkElement, not UIElement
            if (d is FrameworkElement fe)
            {
                if ((bool)e.NewValue)
                {
                    fe.Loaded += OnLoadedAttachMove;
                }
                else
                {
                    fe.Loaded -= OnLoadedAttachMove;
                }
            }
        }

        private static void OnLoadedAttachMove(object? sender, RoutedEventArgs e)
        {
            if (sender is UIElement ui)
            {
                void handler(object? s, MouseEventArgs args)
                {
                    ui.MouseMove -= handler;
                    Keyboard.Focus(ui);
                }
                ui.MouseMove += handler;
            }
        }
    }
}
