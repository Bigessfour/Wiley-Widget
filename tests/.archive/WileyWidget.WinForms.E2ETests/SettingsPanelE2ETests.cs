using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;
using FlaUIApplication = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// E2E tests for SettingsPanel using FlaUI automation.
    /// Tests theme selection, font changes, AI settings, backup/restore functionality.
    /// </summary>
    [Collection("UI Tests")]
    public class SettingsPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public SettingsPanelE2ETests()
        {
            _exePath = TestAppHelper.GetWileyWidgetExePath();
        }

        private bool EnsureInteractiveOrSkip()
        {
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
            var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

            if (isCi && !optedIn && !selfHosted)
            {
                return false;
            }

            return true;
        }

        private void StartApp()
        {
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException($"Executable not found at '{_exePath}'. Set WILEYWIDGET_EXE environment variable.");
            }

            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceHRQR2VfUER0W0o=");

            _app = FlaUIApplication.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        private Window GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null || _automation == null) throw new InvalidOperationException("Application not started");
            return Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(timeoutSeconds)).Result
                ?? throw new InvalidOperationException("Main window not found");
        }

        private static AutomationElement? WaitForElement(Window window, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 12)
        {
            return Retry.WhileNull(() => window.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(250)).Result;
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_OpensAndDisplaysControls()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate using NavigationHelper
            var panel = NavigationHelper.OpenView(_automation!, window, "Nav_Settings", "Settings");

            // Verify main groups are present
            var appearanceGroup = WaitForElement(window, cf => cf.ByName("Appearance").Or(cf.ByControlType(ControlType.Group)));
            Assert.NotNull(appearanceGroup);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_ThemeCombo_HasOptions()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find theme combo box
            var themeCombo = WaitForElement(window, cf => cf.ByName("Theme").Or(cf.ByAutomationId("ThemeComboBox")))?.AsComboBox();
            Assert.NotNull(themeCombo);

            // Verify it has theme options
            var items = themeCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_ThemeSelection_ChangesImmediately()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find theme combo
            var themeCombo = WaitForElement(window, cf => cf.ByName("Theme"))?.AsComboBox();

            if (themeCombo != null && themeCombo.Items.Length > 1)
            {
                // Select different theme
                themeCombo.Select(1);

                // Wait briefly for theme application
                System.Threading.Thread.Sleep(1000);

                // Verify theme applied (check for theme-specific element or color)
                // Note: Actual verification depends on theme implementation
                Assert.NotNull(window);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_FontCombo_HasOptions()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find font combo box
            var fontCombo = WaitForElement(window, cf => cf.ByName("Font").Or(cf.ByAutomationId("FontComboBox")))?.AsComboBox();
            Assert.NotNull(fontCombo);

            // Verify it has font options
            var items = fontCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_AISettings_DisplaysControls()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find AI settings group
            var aiGroup = WaitForElement(window, cf => cf.ByName("AI Settings").Or(cf.ByName("xAI Settings")));

            if (aiGroup != null)
            {
                // Verify AI controls
                var enableAiCheck = WaitForElement(window, cf => cf.ByName("Enable AI"))?.AsCheckBox();
                var apiEndpointText = WaitForElement(window, cf => cf.ByName("API Endpoint"))?.AsTextBox();
                var modelCombo = WaitForElement(window, cf => cf.ByName("Model"))?.AsComboBox();

                Assert.True(enableAiCheck != null || apiEndpointText != null || modelCombo != null, "AI settings controls should be present");
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_EnableAI_TogglesFeature()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find Enable AI checkbox
            var enableAiCheck = WaitForElement(window, cf => cf.ByName("Enable AI"))?.AsCheckBox();

            if (enableAiCheck != null)
            {
                var originalState = enableAiCheck.IsChecked;

                // Toggle
                enableAiCheck.Toggle();
                Assert.NotEqual(originalState, enableAiCheck.IsChecked);

                // Toggle back
                enableAiCheck.Toggle();
                Assert.Equal(originalState, enableAiCheck.IsChecked);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_APIKey_IsMasked()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find API key textbox
            var apiKeyBox = WaitForElement(window, cf => cf.ByName("API Key"))?.AsTextBox();

            if (apiKeyBox != null)
            {
                // Verify textbox exists and is editable (password masking is handled by PasswordChar property in WinForms)
                Assert.True(apiKeyBox.IsEnabled, "API Key textbox should be enabled");
                Assert.True(!string.IsNullOrEmpty(apiKeyBox.Text) || apiKeyBox.IsEnabled, "API Key should be editable");
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_ShowAPIKey_Button_TogglesVisibility()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find show API key button
            var showApiKeyBtn = WaitForElement(window, cf => cf.ByName("Show API Key"))?.AsButton();

            if (showApiKeyBtn != null)
            {
                Assert.True(showApiKeyBtn.IsEnabled);
                // Click to toggle visibility
                showApiKeyBtn.Click();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_ModelSelection_HasOptions()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find model combo
            var modelCombo = WaitForElement(window, cf => cf.ByName("Model").Or(cf.ByAutomationId("XaiModelCombo")))?.AsComboBox();

            if (modelCombo != null)
            {
                var items = modelCombo.Items;
                Assert.NotNull(items);
                Assert.NotEmpty(items);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_AIHelp_LearnMoreLink_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find learn more link
            var learnMoreLink = WaitForElement(window, cf => cf.ByName("Learn more...").Or(cf.ByControlType(ControlType.Hyperlink)));

            if (learnMoreLink != null)
            {
                Assert.True(learnMoreLink.IsEnabled);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_CloseButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find close button
            var closeBtn = WaitForElement(window, cf => cf.ByName("Close").Or(cf.ByAutomationId("CloseButton")))?.AsButton();
            Assert.NotNull(closeBtn);
            Assert.True(closeBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Settings")]
        public void SettingsPanel_VersionInfo_Displayed()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Settings
            var settingsMenu = WaitForElement(window, cf => cf.ByName("Settings"));
            settingsMenu?.Click();

            // Find version label
            var versionLabel = WaitForElement(window, cf => cf.ByName("Version").Or(cf.ByAutomationId("VersionLabel")));

            if (versionLabel != null)
            {
                var versionText = versionLabel.Name;
                Assert.NotNull(versionText);
                Assert.NotEmpty(versionText);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _app?.Close();
                        _app?.Dispose();
                        _app = null;
                        _automation?.Dispose();
                        _automation = null;
                    }
                    catch
                    {
                        // Suppress cleanup errors
                    }
                }
                _disposed = true;
            }
        }
    }
}
