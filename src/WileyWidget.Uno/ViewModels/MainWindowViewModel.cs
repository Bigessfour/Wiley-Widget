using Prism.Mvvm;
using Prism.Commands;
using System.Windows.Input;

namespace WileyWidget.Uno.ViewModels;

public class MainWindowViewModel : BindableBase
{
    private string _title = "Wiley Widget - Uno Platform Migration";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ICommand NavigateCommand { get; }

    public MainWindowViewModel()
    {
        NavigateCommand = new DelegateCommand<string>(OnNavigate);
    }

    private void OnNavigate(string? destination)
    {
        // Navigation logic will be added
    }
}
