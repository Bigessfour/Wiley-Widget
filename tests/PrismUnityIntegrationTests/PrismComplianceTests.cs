using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Prism.Dialogs;
using Xunit;

namespace PrismUnityIntegrationTests;

public class PrismComplianceTests
{
    private static string RepoRoot
    {
        get
        {
            // test bin dir -> project dir -> repo root
            var baseDir = AppContext.BaseDirectory;
            var root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            return root;
        }
    }

    private static string SrcPath => Path.Combine(RepoRoot, "src");
    private static string ViewsPath => Path.Combine(SrcPath, "Views");
    private static string AppFile => Path.Combine(SrcPath, "App.xaml.cs");

    [Fact]
    public void Views_Have_AutoWireViewModel_Attribute()
    {
        Assert.True(Directory.Exists(ViewsPath), $"Views path not found: {ViewsPath}");
        var xamlFiles = Directory.GetFiles(ViewsPath, "*.xaml", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(xamlFiles);

        foreach (var file in xamlFiles)
        {
            // Skip resource dictionaries
            var text = File.ReadAllText(file);
            if (text.Contains("ResourceDictionary")) continue;

            var hasPrismNs = text.Contains("http://prismlibrary.com/");
            var hasAutoWire = text.Contains("ViewModelLocator.AutoWireViewModel=\"True\"");
            Assert.True(hasPrismNs && hasAutoWire, $"Missing AutoWireViewModel in {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void Views_CodeBehind_Does_Not_Set_DataContext()
    {
        var csFiles = Directory.GetFiles(ViewsPath, "*.xaml.cs", SearchOption.TopDirectoryOnly);
        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("DataContext =", text);
        }
    }

    [Fact]
    public void Views_Do_Not_Use_ServiceLocator()
    {
        var csFiles = Directory.GetFiles(ViewsPath, "*.xaml.cs", SearchOption.TopDirectoryOnly);
        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("App.GetContainerProvider(", text);
            Assert.DoesNotContain("ServiceLocator", text);
            Assert.DoesNotContain("CommonServiceLocator", text);
        }
    }

    [Fact]
    public void App_Registers_RegionAdapterMappings_For_Syncfusion()
    {
        var appText = File.ReadAllText(AppFile);
        Assert.Contains("SfDataGridRegionAdapter", appText);
        Assert.Contains("RegionAdapterMappings.RegisterMapping", appText);
        Assert.Contains("DockingManagerRegionAdapter", appText);
    }

    [Fact]
    public void App_Registers_Custom_Region_Behaviors()
    {
        var appText = File.ReadAllText(AppFile);
        Assert.Contains("NavigationLoggingBehavior", appText);
        Assert.Contains("AutoActivateBehavior", appText);
        Assert.Contains("AutoSaveBehavior", appText);
        Assert.Contains("SyncContextWithHostBehavior", appText);
    }

    [Fact]
    public void Dialog_ViewModels_Implement_IDialogAware()
    {
        var appText = File.ReadAllText(AppFile);
        var rx = new Regex(@"RegisterDialog<([^,>]+),\s*([^>]+)>\(", RegexOptions.Compiled);
        var matches = rx.Matches(appText).Cast<Match>().ToList();
        Assert.NotEmpty(matches);

        var asm = typeof(WileyWidget.App).Assembly;
        foreach (var m in matches)
        {
            var vmRaw = m.Groups[2].Value.Trim();
            // Normalize VM type name
            string vmTypeName = vmRaw.Contains(".") ? vmRaw : $"WileyWidget.{vmRaw}";
            var vmType = asm.GetType(vmTypeName, throwOnError: false);
            Assert.NotNull(vmType);
            Assert.True(typeof(IDialogAware).IsAssignableFrom(vmType!), $"{vmTypeName} does not implement IDialogAware");
        }
    }
}
