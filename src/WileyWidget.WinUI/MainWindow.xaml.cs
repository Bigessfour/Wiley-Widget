using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WileyWidget.WinUI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private int _clickCount = 0;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Wiley Widget - WinUI 3 Shell";
    }

    private void myButton_Click(object sender, RoutedEventArgs e)
    {
        _clickCount++;
        myButton.Content = $"Clicked {_clickCount} time{(_clickCount == 1 ? "" : "s")}";
        StatusText.Text = $"Button clicked {_clickCount} time{(_clickCount == 1 ? "" : "s")}!";
    }
}
