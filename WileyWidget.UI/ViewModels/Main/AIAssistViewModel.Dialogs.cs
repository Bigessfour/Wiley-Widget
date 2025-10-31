using System;
using Prism.Dialogs;

namespace WileyWidget.ViewModels.Main {
    // Partial class to add Prism IDialogAware support to AIAssistViewModel without touching the large primary file
    public partial class AIAssistViewModel : IDialogAware
    {
        public string Title { get; private set; } = "AI Assistant";

        // Align with Prism's IDialogAware in this project which uses DialogCloseListener (not Action<IDialogResult>)
        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog() => !IsProcessing; // prevent closing while processing

        public void OnDialogClosed()
        {
            // Optional: cleanup if needed
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters != null && parameters.ContainsKey("Title"))
            {
                Title = parameters.GetValue<string>("Title");
            }
        }

        // Helper to close from within the VM if needed
        private void CloseDialog(ButtonResult result = ButtonResult.OK)
        {
            // Follow the base class pattern: invoke the RequestClose listener directly.
            // DialogCloseListener is not nullable in the Prism implementation used here,
            // so avoid null-comparison which the compiler rejects for this type.
            RequestClose.Invoke(new DialogResult(result));
        }
    }
}
