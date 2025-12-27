using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using WileyWidget.WinForms;
using System.Reflection;

namespace WileyWidget.WinForms.Tests.Unit.Configuration
{
    public class SyncfusionLicenseTests
    {
        [Fact]
        public void RegisterSyncfusionLicense_WithValidKey_ShouldNotThrow()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            var mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(s => s.Value).Returns("Ngo9BigBOggjHTQxAR8/V1JGaF5cXGpCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH1feHRdRGhZV0FzX0JWYEs=");
            mockConfig.Setup(c => c["Syncfusion:LicenseKey"]).Returns("Ngo9BigBOggjHTQxAR8/V1JGaF5cXGpCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH1feHRdRGhZV0FzX0JWYEs=");

            // Act & Assert
            // Note: This test verifies that the method doesn't throw with a valid key
            // The actual license registration is tested in integration tests
            var exception = Record.Exception(() => InvokeRegisterSyncfusionLicense(mockConfig.Object));
            Assert.Null(exception);
        }

        [Fact]
        public void RegisterSyncfusionLicense_WithNullKey_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["Syncfusion:LicenseKey"]).Returns((string?)null);

            // Act & Assert
            var exception = Assert.Throws<TargetInvocationException>(() => InvokeRegisterSyncfusionLicense(mockConfig.Object));
            var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Syncfusion license key not found", innerException.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RegisterSyncfusionLicense_WithEmptyKey_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["Syncfusion:LicenseKey"]).Returns(string.Empty);

            // Act & Assert
            var exception = Assert.Throws<TargetInvocationException>(() => InvokeRegisterSyncfusionLicense(mockConfig.Object));
            var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Syncfusion license key not found", innerException.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RegisterSyncfusionLicense_WithWhitespaceKey_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["Syncfusion:LicenseKey"]).Returns("   ");

            // Act & Assert
            var exception = Assert.Throws<TargetInvocationException>(() => InvokeRegisterSyncfusionLicense(mockConfig.Object));
            var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Syncfusion license key not found", innerException.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Helper method to invoke the private static method
        private static void InvokeRegisterSyncfusionLicense(IConfiguration configuration)
        {
            // Use reflection to invoke the private method
            var method = typeof(Program).GetMethod("RegisterSyncfusionLicense", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(null, new object[] { configuration });
        }
    }
}
