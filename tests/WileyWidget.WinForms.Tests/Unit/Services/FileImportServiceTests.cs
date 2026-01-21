using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services
{
    public class FileImportServiceTests : IDisposable
    {
        private readonly Mock<ILogger<FileImportService>> _loggerMock;
        private readonly FileImportService _service;
        private readonly string _testTempDir;

        public FileImportServiceTests()
        {
            _loggerMock = new Mock<ILogger<FileImportService>>();
            _service = new FileImportService(_loggerMock.Object);
            _testTempDir = Path.Combine(Path.GetTempPath(), "WileyWidgetTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_testTempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }

        [Fact]
        public async Task ImportDataAsync_WithValidJson_ShouldReturnSuccess()
        {
            // Arrange
            var filePath = Path.Combine(_testTempDir, "test.json");
            var testData = new TestModel { Name = "Test", Value = 123 };
            var json = JsonSerializer.Serialize(testData);
            await File.WriteAllTextAsync(filePath, json);

            // Act
            var result = await _service.ImportDataAsync<TestModel>(filePath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Name.Should().Be("Test");
            result.Data!.Value.Should().Be(123);
        }

        [Fact]
        public async Task ImportDataAsync_WithInvalidJson_ShouldReturnFailure()
        {
            // Arrange
            var filePath = Path.Combine(_testTempDir, "invalid.json");
            await File.WriteAllTextAsync(filePath, "{ invalid json }");

            // Act
            var result = await _service.ImportDataAsync<TestModel>(filePath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid JSON format");
        }

        [Fact]
        public async Task ImportDataAsync_WithNonExistentFile_ShouldReturnFailure()
        {
            // Arrange
            var filePath = Path.Combine(_testTempDir, "nonexistent.json");

            // Act
            var result = await _service.ImportDataAsync<TestModel>(filePath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("File not found");
        }

        [Fact]
        public async Task ImportDataAsync_WithEmptyFile_ShouldReturnFailure()
        {
            // Arrange
            var filePath = Path.Combine(_testTempDir, "empty.json");
            await File.WriteAllTextAsync(filePath, "");

            // Act
            var result = await _service.ImportDataAsync<TestModel>(filePath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("File is empty");
        }

        [Fact]
        public async Task ImportDataAsync_WithLargeFile_ShouldReturnFailure()
        {
            // Arrange
            var filePath = Path.Combine(_testTempDir, "large.json");
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                fs.SetLength(101 * 1024 * 1024); // 101 MB (limit is 100MB)
            }

            // Act
            var result = await _service.ImportDataAsync<TestModel>(filePath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("exceeds maximum size");
        }

        private class TestModel
        {
            public string Name { get; set; } = "";
            public int Value { get; set; }
        }
    }
}
