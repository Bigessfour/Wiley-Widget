using FluentAssertions;
using Prism.Mvvm;
using Xunit;

namespace WileyWidget.Services.Tests.ViewModelTests;

/// <summary>
/// Unit tests for ViewModel base behavior focusing on property changes and data binding.
/// These tests ensure MVVM patterns work correctly with Prism.Mvvm.
/// 
/// Note: Tests a sample ViewModel implementation to avoid complex project dependencies.
/// Demonstrates testing patterns for all ViewModels in the solution.
/// </summary>
public class ViewModelBaseTests
{
    /// <summary>
    /// Sample ViewModel for testing Prism.Mvvm behavior
    /// </summary>
    private class TestViewModel : BindableBase
    {
        private string _name = "Default Name";
        private int _count;
        private bool _isEnabled = true;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var viewModel = new TestViewModel();

        // Assert
        viewModel.Name.Should().Be("Default Name");
        viewModel.Count.Should().Be(0);
        viewModel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Property_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var propertyChangedRaised = false;
        string? changedPropertyName = null;

        viewModel.PropertyChanged += (sender, args) =>
        {
            propertyChangedRaised = true;
            changedPropertyName = args.PropertyName;
        };

        // Act
        viewModel.Name = "New Name";

        // Assert
        propertyChangedRaised.Should().BeTrue("property changed event should be raised");
        changedPropertyName.Should().Be(nameof(TestViewModel.Name));
        viewModel.Name.Should().Be("New Name");
    }

    [Fact]
    public void Property_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var initialName = viewModel.Name;
        var propertyChangedCount = 0;

        viewModel.PropertyChanged += (sender, args) => propertyChangedCount++;

        // Act
        viewModel.Name = initialName; // Set to same value

        // Assert
        propertyChangedCount.Should().Be(0, "property changed should not be raised for same value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void IntProperty_SetVariousValues_UpdatesCorrectly(int value)
    {
        // Arrange
        var viewModel = new TestViewModel();

        // Act
        viewModel.Count = value;

        // Assert
        viewModel.Count.Should().Be(value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BoolProperty_SetValue_UpdatesCorrectly(bool value)
    {
        // Arrange
        var viewModel = new TestViewModel();

        // Act
        viewModel.IsEnabled = value;

        // Assert
        viewModel.IsEnabled.Should().Be(value);
    }

    [Fact]
    public void MultipleProperties_ChangeSequentially_EachRaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
                changedProperties.Add(args.PropertyName);
        };

        // Act
        viewModel.Name = "Test";
        viewModel.Count = 42;
        viewModel.IsEnabled = false;

        // Assert
        changedProperties.Should().HaveCount(3);
        changedProperties.Should().Contain(nameof(TestViewModel.Name));
        changedProperties.Should().Contain(nameof(TestViewModel.Count));
        changedProperties.Should().Contain(nameof(TestViewModel.IsEnabled));
    }

    [Fact]
    public void ViewModel_ImplementsINotifyPropertyChanged()
    {
        // Arrange & Act
        var viewModel = new TestViewModel();

        // Assert
        viewModel.Should().BeAssignableTo<System.ComponentModel.INotifyPropertyChanged>(
            "ViewModel must implement INotifyPropertyChanged for data binding");
    }

    [Fact]
    public void ViewModel_InheritsFromBindableBase()
    {
        // Arrange & Act
        var viewModel = new TestViewModel();

        // Assert
        viewModel.Should().BeAssignableTo<BindableBase>(
            "ViewModels should inherit from Prism.Mvvm.BindableBase for consistent MVVM behavior");
    }

    [Fact]
    public void PropertyChanged_WithNullHandler_DoesNotThrow()
    {
        // Arrange
        var viewModel = new TestViewModel();
        // No PropertyChanged handler attached

        // Act
        Action act = () => viewModel.Name = "New Value";

        // Assert
        act.Should().NotThrow("setting property without subscribers should not throw");
    }
}
