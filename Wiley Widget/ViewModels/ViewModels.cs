using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.ViewModels;

/// <summary>
/// Base view model class with INotifyPropertyChanged implementation.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Main window view model.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private string _title;
    private string _statusMessage;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public MainWindowViewModel()
    {
        Title = "Wiley Widget";
        StatusMessage = "Application started successfully";
    }
}

/// <summary>
/// Dashboard view model.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private string _welcomeMessage;

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set => SetProperty(ref _welcomeMessage, value);
    }

    public DashboardViewModel()
    {
        WelcomeMessage = "Welcome to Wiley Widget Dashboard";
    }
}
