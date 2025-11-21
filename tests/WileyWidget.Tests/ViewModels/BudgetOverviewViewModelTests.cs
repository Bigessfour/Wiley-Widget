using Xunit;
using Moq;
using CommunityToolkit.Mvvm.ComponentModel;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace WileyWidget.Tests.ViewModels
{
    public class BudgetOverviewViewModelTests
    {
        [Fact]
        public async Task RefreshAsync_LoadsData_WhenSuccessful()
        {
            // Arrange
            var mockRepo = new Mock<IBudgetRepository>();
            mockRepo.Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>()))
                    .ReturnsAsync(new List<BudgetEntry> { new BudgetEntry { Department = "Test Dept", AdoptedBudget = 10000 } });
            var appMock = new Mock<App>();  // Mock App for DI
            appMock.Setup(a => a.Services.GetRequiredService<IBudgetRepository>()).Returns(mockRepo.Object);
            App.Current = appMock.Object;  // Set for VM access

            var vm = new BudgetOverviewViewModel();

            // Act
            await vm.RefreshAsync();

            // Assert
            Assert.NotEmpty(vm.BudgetEntries);
            Assert.True(vm.BudgetEntries.Count > 0);
            Assert.Empty(vm.ErrorMessage);
            mockRepo.Verify(r => r.GetByFiscalYearAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SetsError_WhenRepositoryFails()
        {
            // Arrange
            var mockRepo = new Mock<IBudgetRepository>();
            mockRepo.Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>()))
                    .ThrowsAsync(new InvalidOperationException("DB Error"));
            var appMock = new Mock<App>();
            appMock.Setup(a => a.Services.GetRequiredService<IBudgetRepository>()).Returns(mockRepo.Object);
            App.Current = appMock.Object;

            var vm = new BudgetOverviewViewModel();

            // Act
            await vm.RefreshAsync();

            // Assert
            Assert.Empty(vm.BudgetEntries);
            Assert.Contains("DB Error", vm.ErrorMessage);
        }
    }
}

// Note: Add to WileyWidget.Tests.csproj: <PackageReference Include="Moq" Version="4.20.72" />
