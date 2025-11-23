using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;
using Xunit;

namespace WileyWidget.WinUI.Tests;

public class ViewModelBaseTests
{
    private class TestViewModel : ObservableObject
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
        var viewModel = new TestViewModel();

        viewModel.Name.Should().Be("Default Name");
        viewModel.Count.Should().Be(0);
        viewModel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Property_SetValue_RaisesPropertyChanged()
    {
        var viewModel = new TestViewModel();
        var propertyChangedRaised = false;
        string? changedPropertyName = null;

        viewModel.PropertyChanged += (sender, args) =>
        {
            propertyChangedRaised = true;
            changedPropertyName = args.PropertyName;
        };

        viewModel.Name = "New Name";

        propertyChangedRaised.Should().BeTrue();
        changedPropertyName.Should().Be(nameof(TestViewModel.Name));
        viewModel.Name.Should().Be("New Name");
    }

    [Fact]
    public void Property_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var viewModel = new TestViewModel();
        var propertyChangedCount = 0;

        viewModel.PropertyChanged += (sender, args) => propertyChangedCount++;

        var initialName = viewModel.Name;
        viewModel.Name = initialName; // same value

        propertyChangedCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void IntProperty_SetVariousValues_UpdatesCorrectly(int value)
    {
        var viewModel = new TestViewModel();

        viewModel.Count = value;

        viewModel.Count.Should().Be(value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BoolProperty_SetValue_UpdatesCorrectly(bool value)
    {
        var viewModel = new TestViewModel();

        viewModel.IsEnabled = value;

        viewModel.IsEnabled.Should().Be(value);
    }
}
