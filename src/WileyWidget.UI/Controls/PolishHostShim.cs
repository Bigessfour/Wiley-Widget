using System.Windows;
using System.Windows.Controls;

namespace WileyWidget.Controls
{
    // Shim control for XAML/Design-time compilation: provides a minimal public PolishHost
    // type in the WileyWidget.UI assembly so the wpftmp markup compiler can resolve
    // the control during design/markup-compile. The real implementation remains in
    // src\WileyWidget\Controls\PolishHost.cs and is used at runtime where appropriate.
    public class PolishHost : ContentControl
    {
        static PolishHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PolishHost), new FrameworkPropertyMetadata(typeof(PolishHost)));
        }
    }
}
