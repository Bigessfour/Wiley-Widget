using Prism.Mvvm;

namespace WileyWidget.Uno.ViewModels;

public class DashboardViewModel : BindableBase
{
    private string _welcomeMessage = "Welcome to Wiley Widget on Uno Platform!";

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set => SetProperty(ref _welcomeMessage, value);
    }

    public DashboardViewModel()
    {
        // Initialization logic
    }
}
