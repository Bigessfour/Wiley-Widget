using System.Threading.Tasks;
using Moq;
using Xunit;

// Example ViewModel test template - replace types with actual ViewModel and service interfaces
namespace WileyWidget.Tests.Templates
{
    public class ViewModelTests
    {
        [Fact]
        public async Task LoadAccountsAsync_PopulatesCollection()
        {
            // Arrange
            var mockRepo = new Mock<IMunicipalAccountRepository>();
            mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { new MunicipalAccount { Id = 1, Name = "Test" } });

            var vm = new MunicipalAccountViewModel(mockRepo.Object, null, new Mock<IGrokSupercomputer>().Object, new Mock<IRegionManager>().Object, new Mock<IEventAggregator>().Object);

            // Act
            await vm.InitializeAsync();

            // Assert
            Assert.NotEmpty(vm.MunicipalAccounts);
            Assert.Equal("Test", vm.MunicipalAccounts[0].Name);
        }
    }
}
