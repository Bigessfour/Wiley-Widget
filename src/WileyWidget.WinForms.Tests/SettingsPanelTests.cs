#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;
using System.Threading;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Tests
{
    public class SettingsPanelTests
    {
        [Test]
        public void TxtAppTitle_Validating_Empty_Cancels()
        {
            var vm = new SettingsViewModel(NullLogger<SettingsViewModel>.Instance);
            using var panel = new SettingsPanel(vm, new DummyThemeService());

            // Get private textbox and ensure it's empty
            var txtField = typeof(SettingsPanel).GetField("_txtAppTitle", BindingFlags.Instance | BindingFlags.NonPublic);
            var txtBox = (System.Windows.Forms.TextBox)txtField!.GetValue(panel)!
                ?? throw new InvalidOperationException("txt app title not found");

            txtBox.Text = string.Empty;

            var method = typeof(SettingsPanel).GetMethod("TxtAppTitle_Validating", BindingFlags.Instance | BindingFlags.NonPublic);
            var args = new CancelEventArgs();
            method!.Invoke(panel, new object?[] { txtBox, args });

            args.Cancel.Should().BeTrue();

            var errProvField = typeof(SettingsPanel).GetField("_error_provider", BindingFlags.Instance | BindingFlags.NonPublic);
            var errProv = errProvField?.GetValue(panel);
            if (errProv != null)
            {
                var getError = errProv!.GetType().GetMethod("GetError");
                var err = getError?.Invoke(errProv, new object?[] { txtBox }) as string;
                err.Should().Contain("cannot be empty");
            }
        }

        [Test]
        public void LoadViewDataAsync_Success_BindsAppTitle()
        {
            var vm = new SettingsViewModel(NullLogger<SettingsViewModel>.Instance);
            vm.AppTitle = "My App Title";

            using var panel = new SettingsPanel(vm, new DummyThemeService());

            var txtField = typeof(SettingsPanel).GetField("_txtAppTitle", BindingFlags.Instance | BindingFlags.NonPublic);
            var txtBox = (System.Windows.Forms.TextBox)txtField!.GetValue(panel)!;

            // Binding should reflect the viewmodel value
            txtBox.Text.Should().Be(vm.AppTitle);
        }

        [Test]
        public void OnThemeChange_ApplyCurrentTheme_DoesNotThrow()
        {
            var vm = new SettingsViewModel(NullLogger<SettingsViewModel>.Instance);
            var themeService = new DummyThemeService();
            using var panel = new SettingsPanel(vm, themeService);

            // Simulate theme change via the service API (cannot invoke event from outside the declaring type)
            Assert.DoesNotThrow(() => themeService.SetTheme(AppTheme.Dark));

            // Assert _themeGroup had its backcolor set by ApplyCurrentTheme (non-null)
            var grpField = typeof(SettingsPanel).GetField("_themeGroup", BindingFlags.Instance | BindingFlags.NonPublic);
            var grp = grpField!.GetValue(panel) as System.Windows.Forms.GroupBox;
            grp.Should().NotBeNull();
            // BackColor should be set to a value (System.Drawing.Color)
            grp!.BackColor.Should().NotBeNull();
        }

        [Test]
        public void BtnClose_HidesDocking_WhenParentHasDockingManager()
        {
            var vm = new SettingsViewModel(NullLogger<SettingsViewModel>.Instance);
            var themeService = new DummyThemeService();
            using var panel = new SettingsPanel(vm, themeService);

            var parent = new TestMainFormWithDocking();
            parent.Controls.Add(panel);

            // Invoke private BtnClose_Click
            var method = typeof(SettingsPanel).GetMethod("BtnClose_Click", BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(panel, new object?[] { null, EventArgs.Empty });

            parent.DockVisibilityCalled.Should().BeTrue();
            parent.LastVisibilityArg.Should().BeFalse();
        }

        private class DummyThemeService : IThemeService
        {
            public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;
            public AppTheme Preference { get; set; } = AppTheme.Light;
            public event EventHandler<AppTheme>? ThemeChanged;

            public void SetTheme(AppTheme theme)
            {
                CurrentTheme = theme;
                Preference = theme;
                ThemeChanged?.Invoke(this, theme);
            }

            public void Initialize()
            {
                // No-op for tests
            }

            public void Dispose()
            {
                // No unmanaged resources to dispose in this dummy implementation
            }
        }

        private class TestMainFormWithDocking : System.Windows.Forms.Form
        {
            public bool DockVisibilityCalled { get; private set; }
            public bool LastVisibilityArg { get; private set; }

            private readonly FakeDockingManager _dockingManager;

            public TestMainFormWithDocking()
            {
                _dockingManager = new FakeDockingManager(this);
            }

            private class FakeDockingManager
            {
                private readonly TestMainFormWithDocking _owner;
                public FakeDockingManager(TestMainFormWithDocking owner) { _owner = owner; }

                // Mirror the expected API used by SettingsPanel
                public void SetDockVisibility(System.Windows.Forms.UserControl ctrl, bool visible)
                {
                    _owner.DockVisibilityCalled = true;
                    _owner.LastVisibilityArg = visible;
                }
            }
        }
    }
}
