namespace WileyWidget.Uno.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Wiley Widget - Uno Platform Migration";

    public MainWindowViewModel()
    {
    }

    [RelayCommand]
    private void Navigate(string? destination)
    {
        // Navigation logic will be added
    }
}
