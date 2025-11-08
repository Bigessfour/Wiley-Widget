using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Views.Panels;

/// <summary>
/// AI Assistant panel view for embedding in docking layout
/// </summary>
public partial class AIAssistPanelView : UserControl
{
    public AIAssistPanelView()
    {
        InitializeComponent();
    }

    private AIAssistViewModel? ViewModel
    {
        get => DataContext as AIAssistViewModel;
    }

    /// <summary>
    /// Handle view loaded event to set initial focus
    /// </summary>
    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        // Set focus to the message input when view loads
        var messageInput = FindName("MessageInput") as TextBox;
        messageInput?.Focus();
    }

    /// <summary>
    /// Handle Enter key in message input
    /// </summary>
    private void OnMessageInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel != null && ViewModel.IsInputValid)
        {
            ViewModel.SendMessageCommand.Execute();
            e.Handled = true;
        }
    }
}
