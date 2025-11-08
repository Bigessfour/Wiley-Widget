using FluentAssertions;
using Xunit;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WileyWidget.ViewModels.Panels;

namespace WileyWidget.Tests.ViewModels.Panels
{
    public class ToolsPanelViewModelTests
    {
        [Fact]
        public void Constructor_InitializesToolItemsCollection()
        {
            // Arrange & Act
            var viewModel = new ToolsPanelViewModel();

            // Assert
            viewModel.ToolItems.Should().NotBeNull();
            viewModel.ToolItems.Should().HaveCount(4);
        }

        [Fact]
        public void ToolItems_HaveCorrectNamesAndIcons()
        {
            // Arrange & Act
            var viewModel = new ToolsPanelViewModel();

            // Assert
            viewModel.ToolItems[0].Name.Should().Be("Calculator");
            viewModel.ToolItems[0].Icon.Should().Be("🧮");

            viewModel.ToolItems[1].Name.Should().Be("Unit Converter");
            viewModel.ToolItems[1].Icon.Should().Be("🔄");

            viewModel.ToolItems[2].Name.Should().Be("Date Calculator");
            viewModel.ToolItems[2].Icon.Should().Be("📅");

            viewModel.ToolItems[3].Name.Should().Be("Notes");
            viewModel.ToolItems[3].Icon.Should().Be("📝");
        }

        [Fact]
        public void ToolItems_HaveCommands()
        {
            // Arrange & Act
            var viewModel = new ToolsPanelViewModel();

            // Assert
            foreach (var item in viewModel.ToolItems)
            {
                item.Command.Should().NotBeNull();
            }
        }

        [Fact]
        public void SelectedTabIndex_DefaultsToZero()
        {
            // Arrange & Act
            var viewModel = new ToolsPanelViewModel();

            // Assert
            viewModel.SelectedTabIndex.Should().Be(0);
        }

        [Fact]
        public void SelectedTabIndex_UpdatesToolbarItemStates()
        {
            // Arrange
            var viewModel = new ToolsPanelViewModel();
            var propertyChangedEvents = new List<string>();
            viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName ?? string.Empty);

            // Act
            viewModel.SelectedTabIndex = 1; // Switch to Unit Converter

            // Assert
            viewModel.SelectedTabIndex.Should().Be(1);
            viewModel.ToolItems[1].IsEnabled.Should().BeFalse(); // Unit Converter should be disabled
            viewModel.ToolItems[0].IsEnabled.Should().BeTrue();  // Calculator should be enabled
            viewModel.ToolItems[2].IsEnabled.Should().BeTrue();  // Date Calculator should be enabled
            viewModel.ToolItems[3].IsEnabled.Should().BeTrue();  // Notes should be enabled

            // Verify PropertyChanged was raised for ToolItems
            propertyChangedEvents.Should().Contain(nameof(viewModel.ToolItems));
        }

        [Fact]
        public void ToolbarCommands_ChangeSelectedTabIndex()
        {
            // Arrange
            var viewModel = new ToolsPanelViewModel();

            // Act
            viewModel.ToolItems[1].Command?.Execute(null); // Execute Unit Converter command

            // Assert
            viewModel.SelectedTabIndex.Should().Be(1);
        }

        [Fact]
        public void ToolItem_ImplementsINotifyPropertyChanged()
        {
            // Arrange
            var toolItem = new ToolItem();
            var propertyChangedEvents = new List<string>();
            toolItem.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName ?? string.Empty);

            // Act
            toolItem.Name = "Test";
            toolItem.IsEnabled = false;

            // Assert
            propertyChangedEvents.Should().Contain(nameof(ToolItem.Name));
            propertyChangedEvents.Should().Contain(nameof(ToolItem.IsEnabled));
        }
    }
}
