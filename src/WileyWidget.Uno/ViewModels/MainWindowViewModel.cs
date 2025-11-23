namespace WileyWidget.Uno.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Wiley Widget - Uno Platform Migration";

    public MainWindowViewModel()
    {
    }

    [RelayCommand]
    private void Navigate(string? destination)
    {
        // Navigation logic will be added
    }
}
