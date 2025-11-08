// This file was superseded by AIAssistViewModel.Dialogs.cs which implements IDialogAware.
// Intentionally excluded from build to avoid duplicate partial members.
#if false
using Prism.Dialogs;

namespace WileyWidget.ViewModels.Main;

public partial class AIAssistViewModel : IDialogAware
{
    public string Title => "AI Assist";

    public event Action<IDialogResult> RequestClose;

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
        // Optional: cleanup when dialog closes
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        // Optional: initialize from parameters
    }

    // Helper to raise close from commands within the VM
    private void CloseDialog(IDialogResult result)
    {
        RequestClose?.Invoke(result);
    }
}
#endif
