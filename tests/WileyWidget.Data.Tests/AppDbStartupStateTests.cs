using FluentAssertions;
using WileyWidget.Data;
using Xunit;

namespace WileyWidget.Data.Tests
{
    public class AppDbStartupStateTests
    {
        [Fact]
        public void ResetForTests_ClearsState()
        {
            // Arrange - activate
            AppDbStartupState.ActivateFallback("test cleanup");

            // Act
            AppDbStartupState.ResetForTests();

            // Assert
            AppDbStartupState.IsDegradedMode.Should().BeFalse();
            AppDbStartupState.FallbackActivated.Should().BeFalse();
            AppDbStartupState.InitializationAttempted.Should().BeFalse();
            AppDbStartupState.FallbackReason.Should().BeNull();
        }

        [Fact]
        public void ActivateFallback_SetsDegradedModeAndReason()
        {
            // Arrange
            AppDbStartupState.ResetForTests();

            // Act
            AppDbStartupState.ActivateFallback("reason-for-test");

            // Assert
            AppDbStartupState.FallbackActivated.Should().BeTrue();
            AppDbStartupState.IsDegradedMode.Should().BeTrue();
            AppDbStartupState.FallbackReason.Should().Be("reason-for-test");
        }
    }
}
