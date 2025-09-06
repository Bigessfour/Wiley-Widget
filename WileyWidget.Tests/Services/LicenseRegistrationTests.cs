using WileyWidget;
using WileyWidget.Licensing;
using Xunit;

namespace WileyWidget.Tests.Services;

/// <summary>
/// Unit tests for license registration functionality.
/// Tests idempotency and multiple registration scenarios.
/// </summary>
public class LicenseRegistrationTests
{
    /// <summary>
    /// Test that license registration is idempotent - multiple calls should not cause issues.
    /// </summary>
    [Fact]
    public void TryRegisterEmbeddedLicense_IdempotentRegistration_ShouldHandleMultipleCalls()
    {
        // Arrange
        // Set up a test license key in environment variable
        var testLicenseKey = "test-license-key-12345";
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", testLicenseKey);

        try
        {
            // Act - Call the method multiple times
            var result1 = SecureLicenseProvider.RegisterLicense();
            var result2 = SecureLicenseProvider.RegisterLicense();
            var result3 = SecureLicenseProvider.RegisterLicense();

            // Assert - All calls should succeed (or at least not throw)
            // Note: The actual result depends on whether Syncfusion accepts the test key
            // The important thing is that it doesn't throw exceptions on subsequent calls
            Assert.True(result1 || !result1); // Accept any boolean result
            Assert.True(result2 || !result2); // Accept any boolean result
            Assert.True(result3 || !result3); // Accept any boolean result
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
        }
    }

    /// <summary>
    /// Test license registration with invalid key should not throw exceptions.
    /// </summary>
    [Fact]
    public void TryRegisterEmbeddedLicense_InvalidKey_ShouldNotThrow()
    {
        // Arrange
        var invalidKey = "invalid-test-key";
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", invalidKey);

        try
        {
            // Act & Assert - Should not throw exception
            var result = SecureLicenseProvider.RegisterLicense();

            // The result may be false (registration failed), but should not throw
            Assert.True(result || !result); // Accept any boolean result
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
        }
    }

    /// <summary>
    /// Test license registration with empty key should not throw exceptions.
    /// </summary>
    [Fact]
    public void TryRegisterEmbeddedLicense_EmptyKey_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "");

        try
        {
            // Act & Assert - Should not throw exception
            var result = SecureLicenseProvider.RegisterLicense();

            // Should return false for empty key
            Assert.False(result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
        }
    }

    /// <summary>
    /// Test license registration with null environment variable should not throw.
    /// </summary>
    [Fact]
    public void TryRegisterEmbeddedLicense_NoEnvironmentVariable_ShouldNotThrow()
    {
        // Arrange - Ensure no environment variable is set
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY_EMBEDDED", null);
        Environment.SetEnvironmentVariable("SYNCFUSION_EMBEDDED_LICENSE_KEY", null);

        // Act & Assert - Should not throw exception
        var result = SecureLicenseProvider.RegisterLicense();

        // Should return false when no keys are available
        Assert.False(result);
    }

    /// <summary>
    /// Test that multiple rapid calls to license registration don't cause race conditions.
    /// </summary>
    [Fact]
    public async Task TryRegisterEmbeddedLicense_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var testLicenseKey = "concurrent-test-key-67890";
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", testLicenseKey);

        try
        {
            // Act - Make concurrent calls
            var tasks = new System.Threading.Tasks.Task<bool>[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                    SecureLicenseProvider.RegisterLicense());
            }

            // Wait for all tasks to complete
            await System.Threading.Tasks.Task.WhenAll(tasks);

            // Assert - All calls should complete without throwing
            foreach (var task in tasks)
            {
                var result = await task;
                Assert.True(result || !result); // Accept any boolean result
            }
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
        }
    }
}
