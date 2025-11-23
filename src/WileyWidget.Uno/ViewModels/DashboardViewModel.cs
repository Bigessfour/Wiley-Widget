namespace WileyWidget.Uno.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string welcomeMessage = "Welcome to Wiley Widget on Uno Platform!";

    public DashboardViewModel()
    {
        // Initialization logic
    }
}
