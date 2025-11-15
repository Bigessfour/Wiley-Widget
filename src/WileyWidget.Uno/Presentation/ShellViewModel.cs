using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.Uno.Presentation;

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigator _navigator;

    public ShellViewModel(
        INavigator navigator)
    {
        _navigator = navigator;
        Navigator = navigator;
        // Add code here to initialize or attach event handlers to singleton services
    }

    public INavigator Navigator { get; }
}
