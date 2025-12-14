using Xunit;
using Xunit.Sdk;
using Moq;
using System;
using System.Windows.Forms;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FluentAssertions;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

/// <summary>
/// Tests for MDI child form defensive patterns.
/// Validates that child forms handle IsMdiContainer checks correctly per project guidelines.
/// </summary>
public class MdiChildFormTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<MainForm>> _mockLogger;
    private MainForm? _mainForm;
    private readonly List<Form> _formsToDispose = new();

    public MdiChildFormTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<MainForm>>();

        // Setup required configuration values for MainForm constructor
        // MainForm.cs line 81 uses GetValue<T> with key lookups, so mock the section access
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("false");

        _mockConfiguration.Setup(c => c.GetSection("UI:UIMode")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:UseDockingManager")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:UseMdiMode")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:UseTabbedMdi")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:IsUiTestHarness")).Returns(mockSection.Object);

        // Also mock the key-value access for older config patterns
        _mockConfiguration.Setup(c => c["UI:IsUiTestHarness"]).Returns("true");
        _mockConfiguration.Setup(c => c["UI:UseMdiMode"]).Returns("false");
        _mockConfiguration.Setup(c => c["UI:UseTabbedMdi"]).Returns("false");
        _mockConfiguration.Setup(c => c["UI:UseDockingManager"]).Returns("false");
    }

    [StaFact]
    public void SettingsForm_ShouldSetMdiParent_WhenIsMdiContainerIsTrue()
    {
        // Arrange
        _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object)
        {
            IsMdiContainer = true
        };
        _formsToDispose.Add(_mainForm);

        var mockViewModel = new Mock<SettingsViewModel>();

        // Act
        var childForm = new SettingsForm(mockViewModel.Object, _mainForm);
        _formsToDispose.Add(childForm);

        // Assert
        childForm.MdiParent.Should().NotBeNull();
        childForm.MdiParent.Should().Be(_mainForm);
    }

    [StaFact]
    public void SettingsForm_ShouldNotThrow_WhenIsMdiContainerIsFalse()
    {
        // Arrange
        _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object)
        {
            IsMdiContainer = false
        };
        _formsToDispose.Add(_mainForm);

        var mockViewModel = new Mock<SettingsViewModel>();

        // Act
        Action act = () =>
        {
            var childForm = new SettingsForm(mockViewModel.Object, _mainForm);
            _formsToDispose.Add(childForm);
        };

        // Assert - should not throw ArgumentException
        act.Should().NotThrow<ArgumentException>("defensive pattern should prevent exception when IsMdiContainer is false");
    }

    [StaFact]
    public void SettingsForm_ShouldHaveNullMdiParent_WhenIsMdiContainerIsFalse()
    {
        // Arrange
        _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object)
        {
            IsMdiContainer = false
        };
        _formsToDispose.Add(_mainForm);

        var mockViewModel = new Mock<SettingsViewModel>();

        // Act
        var childForm = new SettingsForm(mockViewModel.Object, _mainForm);
        _formsToDispose.Add(childForm);

        // Assert
        childForm.MdiParent.Should().BeNull("MdiParent should not be set when IsMdiContainer is false");
    }

    // NOTE: AccountsForm test removed - AccountsViewModel requires AppDbContext
    // constructor parameter which cannot be mocked by Moq (requires concrete DB).
    // This validation belongs in integration tests with InMemory database.
    // See TEST_DESIGN_ANALYSIS.md Phase 2 for integration test roadmap.

    [StaFact]
    public void MainForm_ShouldInitializeWithIsMdiContainer()
    {
        // Arrange & Act
        _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object);
        _formsToDispose.Add(_mainForm);

        // Assert - MainForm should be configurable as MDI container
        Action act = () => _mainForm.IsMdiContainer = true;
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        foreach (var form in _formsToDispose)
        {
            try
            {
                form?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
        _formsToDispose.Clear();
    }
}
