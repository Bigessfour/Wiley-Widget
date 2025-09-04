using System.Windows;
using Syncfusion.SfSkinManager;

namespace ThemeCrashRepro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Test: Early theme setup
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            
            base.OnStartup(e);
        }
    }
}