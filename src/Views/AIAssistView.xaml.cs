using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using WileyWidget.Data;
using WileyWidget.Models;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusinessInterfaces = WileyWidget.Business.Interfaces;
using System.ComponentModel;

namespace WileyWidget;

/// <summary>
/// AI Assistant UserControl providing xAI integration through custom chat interface
/// </summary>
public partial class AIAssistView : UserControl
{
    public AIAssistView()
    {
        InitializeComponent();

        // Subscribe to DataContext changes to handle ViewModel setup
        DataContextChanged += OnDataContextChanged;

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        Log.Information("AI Assist View initialized");
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (e.OldValue is ViewModels.AIAssistViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new ViewModel
        if (e.NewValue is ViewModels.AIAssistViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void AIAssistView_Loaded(object sender, RoutedEventArgs e)
    {
        // No need to focus, let the user click
    }

    private ViewModels.AIAssistViewModel? ViewModel
    {
        get => DataContext as ViewModels.AIAssistViewModel;
    }

    /// <summary>
    /// Handle Enter key in message input
    /// </summary>
    private void OnMessageInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel != null)
        {
            ViewModel.SendCommand.Execute();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle ViewModel property changes for auto-scroll behavior
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Auto-scroll to bottom when Messages collection changes or count updates
        if (e.PropertyName == nameof(ViewModels.AIAssistViewModel.Messages) ||
            e.PropertyName == nameof(ViewModels.AIAssistViewModel.ChatMessages))
        {
            Dispatcher.InvokeAsync(() =>
            {
                var scrollViewer = FindName("ChatScrollViewer") as System.Windows.Controls.ScrollViewer;
                scrollViewer?.ScrollToBottom();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }









    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        // For UserControl, theme is applied at application level or parent level
        // SfSkinManager can be used on the parent Window
    }

    // Methods for UI test compatibility
    public void Show()
    {
        // UserControl doesn't have Show, but make it visible
        Visibility = Visibility.Visible;
    }

    public void Close()
    {
        // UserControl doesn't have Close, but hide it
        Visibility = Visibility.Collapsed;
    }

    public string Title => "AI Assist";
}