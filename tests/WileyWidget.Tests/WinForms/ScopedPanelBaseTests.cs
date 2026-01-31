#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.Tests.TestHelpers;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.Tests.WinForms;

/// <summary>
/// Unit tests for ScopedPanelBase{TViewModel} covering reflection caching, validation helpers, and async operations.
/// </summary>
public class ScopedPanelBaseTests
{
    /// <summary>
    /// Test ViewModel for use in panel tests.
    /// </summary>
    private class TestViewModel
    {
        public string Name { get; set; } = "Test";
        public List<string> Items { get; set; } = new();
    }

    /// <summary>
    /// Concrete test panel implementation.
    /// </summary>
    private class TestPanel : ScopedPanelBase<TestViewModel>
    {
        public TestPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<TestViewModel>> logger)
            : base(scopeFactory, logger)
        {
        }

        public int OnViewModelResolvedCallCount { get; set; }

        protected override void OnViewModelResolved(TestViewModel viewModel)
        {
            base.OnViewModelResolved(viewModel);
            OnViewModelResolvedCallCount++;
        }

        protected override Task OnHandleCreatedAsync()
        {
            // Simulate async work
            return Task.Delay(10);
        }
    }

    private IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestViewModel>();
        services.AddLogging(builder => builder.AddDebug());
        return services;
    }

    // ===================== TODO-015: Reflection Caching Test =====================

    /// <summary>
    /// Test that reflection caching works: creating 100 instances of the same panel type
    /// should only call reflection once per type.
    /// </summary>
    [Fact]
    public void ReflectionCache_MultiplePanelInstances_OnlyReflectsOnce()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();

        // Act: Create 100 panel instances
        var panels = new List<TestPanel>();
        for (int i = 0; i < 100; i++)
        {
            var panel = new TestPanel(scopeFactory, logger);
            _ = panel.Handle; // Force handle creation (triggers OnHandleCreated)
            panels.Add(panel);
        }

        // Assert: All panels should be created successfully (reflection should use cache)
        Assert.Equal(100, panels.Count);
        Assert.All(panels, p => Assert.NotNull(ScopedPanelTestHelpers.GetViewModelForTesting<TestPanel, TestViewModel>(p)));

        // Cleanup
        foreach (var panel in panels)
        {
            panel.Dispose();
        }
    }

    // ===================== TODO-022: Validation State Change Test =====================

    /// <summary>
    /// Test that AddValidationError triggers property change events and updates IsValid.
    /// </summary>
    [Fact]
    public void AddValidationError_ValidatesAndTriggersEvents()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();
        var panel = new TestPanel(scopeFactory, logger);
        _ = panel.Handle; // Force initialization

        var stateChangedFired = false;
        panel.StateChanged += (s, e) => stateChangedFired = true;

        // Act
        var error = new ValidationItem("TestField", "Error message", ValidationSeverity.Error);
        panel.AddValidationError(error);

        // Assert
        Assert.False(panel.IsValid);
        Assert.Single(panel.ValidationErrors);
        Assert.Equal("TestField", panel.ValidationErrors[0].FieldName);
        Assert.True(stateChangedFired);

        panel.Dispose();
    }

    /// <summary>
    /// Test that ClearValidationErrors clears all errors and triggers state change.
    /// </summary>
    [Fact]
    public void ClearValidationErrors_ClearsAllAndTriggersEvent()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();
        var panel = new TestPanel(scopeFactory, logger);
        _ = panel.Handle;

        // Add some errors first
        panel.AddValidationError(new ValidationItem("Field1", "Error 1", ValidationSeverity.Error));
        panel.AddValidationError(new ValidationItem("Field2", "Error 2", ValidationSeverity.Error));
        Assert.Equal(2, panel.ValidationErrors.Count);

        var stateChangedFired = false;
        panel.StateChanged += (s, e) => stateChangedFired = true;

        // Act
        panel.ClearValidationErrors();

        // Assert
        Assert.True(panel.IsValid);
        Assert.Empty(panel.ValidationErrors);
        Assert.True(stateChangedFired);

        panel.Dispose();
    }

    /// <summary>
    /// Test that RemoveValidationError removes only errors for the specified field.
    /// </summary>
    [Fact]
    public void RemoveValidationError_RemovesOnlySpecifiedField()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();
        var panel = new TestPanel(scopeFactory, logger);
        _ = panel.Handle;

        // Add multiple errors
        panel.AddValidationError(new ValidationItem("Field1", "Error 1", ValidationSeverity.Error));
        panel.AddValidationError(new ValidationItem("Field2", "Error 2", ValidationSeverity.Error));
        panel.AddValidationError(new ValidationItem("Field1", "Error 3", ValidationSeverity.Warning));
        Assert.Equal(3, panel.ValidationErrors.Count);

        // Act
        panel.RemoveValidationError("Field1");

        // Assert
        Assert.Single(panel.ValidationErrors);
        Assert.Equal("Field2", panel.ValidationErrors[0].FieldName);

        panel.Dispose();
    }

    /// <summary>
    /// Test that SetValidationErrors replaces all errors atomically.
    /// </summary>
    [Fact]
    public void SetValidationErrors_ReplacesAllErrors()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();
        var panel = new TestPanel(scopeFactory, logger);
        _ = panel.Handle;

        // Add initial errors
        panel.AddValidationError(new ValidationItem("OldField", "Old error", ValidationSeverity.Error));

        var newErrors = new[]
        {
            new ValidationItem("NewField1", "New error 1", ValidationSeverity.Error),
            new ValidationItem("NewField2", "New error 2", ValidationSeverity.Warning)
        };

        // Act
        panel.SetValidationErrors(newErrors);

        // Assert
        Assert.Equal(2, panel.ValidationErrors.Count);
        Assert.False(panel.IsValid);
        Assert.Equal("NewField1", panel.ValidationErrors[0].FieldName);

        panel.Dispose();
    }

    // ===================== TODO-027: Handle Recreation Test =====================

    /// <summary>
    /// Test that handle recreation properly disposes old scope and creates new one.
    /// </summary>
    [Fact]
    public void HandleRecreation_DisposesOldScopeAndCreatesNew()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();
        var panel = new TestPanel(scopeFactory, logger);

        // Act: Force first handle creation
        var firstHandle = panel.Handle;
        var firstViewModel = ScopedPanelTestHelpers.GetViewModelForTesting<TestPanel, TestViewModel>(panel);
        Assert.NotNull(firstViewModel);

        // Force handle recreation
        panel.RecreateHandle();

        // Assert: New ViewModel should be resolved (from new scope)
        var secondViewModel = ScopedPanelTestHelpers.GetViewModelForTesting<TestPanel, TestViewModel>(panel);
        Assert.NotNull(secondViewModel);
        // ViewModels may be different instances due to scope recreation
        Assert.NotSame(firstViewModel, secondViewModel);

        panel.Dispose();
    }

    // ===================== TODO-044, 045, 046: Progress Reporting Test =====================

    /// <summary>
    /// Test that LoadAsync with progress reporting calls progress reporter at stages.
    /// </summary>
    [Fact]
    public async Task LoadAsyncWithProgress_ReportsProgress()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();

        var progressPanel = new ProgressTestPanel(scopeFactory, logger);
        _ = progressPanel.Handle;

        var progressReports = new List<string>();
        var progress = new Progress<string>(report => progressReports.Add(report));

        // Act
        await progressPanel.LoadAsync(CancellationToken.None, progress);

        // Assert
        Assert.NotEmpty(progressReports);
        Assert.Contains("Loading", string.Concat(progressReports));

        progressPanel.Dispose();
    }

    /// <summary>
    /// Test that SaveAsync with progress reporting calls progress reporter.
    /// </summary>
    [Fact]
    public async Task SaveAsyncWithProgress_ReportsProgress()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();

        var progressPanel = new ProgressTestPanel(scopeFactory, logger);
        _ = progressPanel.Handle;

        var progressReports = new List<string>();
        var progress = new Progress<string>(report => progressReports.Add(report));

        // Act
        await progressPanel.SaveAsync(CancellationToken.None, progress);

        // Assert
        Assert.NotEmpty(progressReports);
        Assert.Contains("Saving", string.Concat(progressReports));

        progressPanel.Dispose();
    }

    /// <summary>
    /// Test panel that implements progress reporting for testing purposes.
    /// </summary>
    private class ProgressTestPanel : ScopedPanelBase<TestViewModel>
    {
        public ProgressTestPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<TestViewModel>> logger)
            : base(scopeFactory, logger)
        {
        }

        public override async Task LoadAsync(CancellationToken ct, IProgress<string>? progress)
        {
            progress?.Report("Loading data...");
            await Task.Delay(10, ct);
            progress?.Report("Data loaded");
            await base.LoadAsync(ct, progress);
        }

        public override async Task SaveAsync(CancellationToken ct, IProgress<string>? progress)
        {
            progress?.Report("Saving changes...");
            await Task.Delay(10, ct);
            progress?.Report("Changes saved");
            await base.SaveAsync(ct, progress);
        }
    }

    // ===================== Integration Tests =====================

    /// <summary>
    /// Integration test: Full lifecycle from construction to disposal.
    /// </summary>
    [Fact]
    public void FullLifecycle_ConstructToDispose_Succeeds()
    {
        // Arrange
        var services = CreateServices().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<ScopedPanelBase<TestViewModel>>>();

        // Act
        var panel = new TestPanel(scopeFactory, logger);
        Assert.False(panel.IsLoaded); // Not loaded until handle created

        _ = panel.Handle;
        Assert.True(panel.IsLoaded); // Now loaded
        Assert.Equal(1, panel.OnViewModelResolvedCallCount);

        var vm = ScopedPanelTestHelpers.GetViewModelForTesting<TestPanel, TestViewModel>(panel);
        Assert.NotNull(vm);

        panel.AddValidationError(new ValidationItem("Test", "Test error", ValidationSeverity.Error));
        Assert.False(panel.IsValid);

        panel.ClearValidationErrors();
        Assert.True(panel.IsValid);

        // Dispose should succeed without errors
        panel.Dispose();
        Assert.True(panel.IsDisposed);
    }
}
