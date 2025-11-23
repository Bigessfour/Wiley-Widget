namespace WileyWidget.Uno.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string WelcomeMessage { get; set; } = "Welcome to Wiley Widget on Uno Platform!";

    public DashboardViewModel()
    {
        // Initialization logic
    }
}
