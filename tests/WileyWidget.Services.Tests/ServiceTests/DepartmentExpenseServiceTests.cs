using Xunit;
using Moq;
using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using WileyWidget.Business.Services;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public sealed class DepartmentExpenseServiceTests : IDisposable
    {
        private readonly Mock<ILogger<DepartmentExpenseService>> _logger = new();

        public DepartmentExpenseServiceTests()
        {
        }

        public void Dispose()
        {
        }

        private static IConfiguration BuildConfiguration(bool enableQuickBooks = false)
        {
            var dic = new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = enableQuickBooks ? "true" : "false"
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dic).Build();
        }

        [Fact]
        public async Task GetDepartmentExpensesAsync_ValidDepartment_ReturnsExpense()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act
            var result = await service.GetDepartmentExpensesAsync("Water", DateTime.Now.AddMonths(-1), DateTime.Now);

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetDepartmentExpensesAsync_InvalidDepartment_ThrowsArgumentException()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetDepartmentExpensesAsync("InvalidDepartment", DateTime.Now.AddMonths(-1), DateTime.Now));
            exception.Message.Should().Contain("Unknown department 'InvalidDepartment'");
            exception.Message.Should().Contain("Known departments are:");
        }

        [Fact]
        public async Task GetDepartmentExpensesAsync_NullDepartment_ThrowsArgumentException()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetDepartmentExpensesAsync(null!, DateTime.Now.AddMonths(-1), DateTime.Now));
            exception.Message.Should().Contain("Department name cannot be null or empty");
        }

        [Fact]
        public async Task GetDepartmentExpensesAsync_EmptyDepartment_ThrowsArgumentException()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetDepartmentExpensesAsync("", DateTime.Now.AddMonths(-1), DateTime.Now));
            exception.Message.Should().Contain("Department name cannot be null or empty");
        }

        [Fact]
        public async Task GetAllDepartmentExpensesAsync_ReturnsExpensesForAllDepartments()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act
            var result = await service.GetAllDepartmentExpensesAsync(DateTime.Now.AddMonths(-1), DateTime.Now);

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainKey("Water");
            result.Should().ContainKey("Sewer");
            result.Should().ContainKey("Trash");
            result.Should().ContainKey("Apartments");
            foreach (var expense in result.Values)
            {
                expense.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public async Task GetRollingAverageExpensesAsync_ValidDepartment_ReturnsAverage()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act
            var result = await service.GetRollingAverageExpensesAsync("Water");

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetRollingAverageExpensesAsync_InvalidDepartment_ThrowsArgumentException()
        {
            // Arrange
            var config = BuildConfiguration(enableQuickBooks: false);
            var quickBooks = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            var service = new DepartmentExpenseService(_logger.Object, config, quickBooks.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetRollingAverageExpensesAsync("InvalidDepartment"));
            exception.Message.Should().Contain("Unknown department 'InvalidDepartment'");
        }
    }
}
