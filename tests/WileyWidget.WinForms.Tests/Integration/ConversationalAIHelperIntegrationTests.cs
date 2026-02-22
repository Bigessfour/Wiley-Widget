using System;
using FluentAssertions;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class ConversationalAIHelperIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public void GetWelcomeMessage_JarvisPersonality_ReturnsJarvisWelcome()
    {
        // Arrange
        var personality = "JARVIS";

        // Act
        var message = ConversationalAIHelper.GetWelcomeMessage(personality);

        // Assert
        message.Should().Contain("JARVIS online");
        message.Should().Contain("sir");
        message.Should().Contain("MORE COWBELL!");
    }

    [Fact]
    public void GetWelcomeMessage_NonJarvisPersonality_ReturnsGenericWelcome()
    {
        // Arrange
        var personality = "Friendly Assistant";

        // Act
        var message = ConversationalAIHelper.GetWelcomeMessage(personality);

        // Assert
        message.Should().Contain("Hello!");
        message.Should().Contain("Friendly Assistant");
        message.Should().Contain("How can I help you today?");
    }

    [Fact]
    public void GetWelcomeMessage_NullPersonality_ReturnsGenericWelcome()
    {
        // Act
        var message = ConversationalAIHelper.GetWelcomeMessage(null);

        // Assert
        message.Should().Contain("Hello!");
        message.Should().Contain("How can I help you today?");
    }

    [Fact]
    public void FormatFriendlyError_InvalidOperationWithApiKey_ReturnsApiKeyMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("The API key is invalid or missing");

        // Act
        var message = ConversationalAIHelper.FormatFriendlyError(exception);

        // Assert
        message.Should().Contain("⚠️");
        message.Should().Contain("AI service is not configured");
        message.Should().Contain("API key settings");
    }

    [Fact]
    public void FormatFriendlyError_TaskCanceledException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TaskCanceledException();

        // Act
        var message = ConversationalAIHelper.FormatFriendlyError(exception);

        // Assert
        message.Should().Contain("⏱️");
        message.Should().Contain("Request timed out");
    }

    [Fact]
    public void FormatFriendlyError_HttpRequestException_ReturnsNetworkMessage()
    {
        // Arrange
        var exception = new System.Net.Http.HttpRequestException();

        // Act
        var message = ConversationalAIHelper.FormatFriendlyError(exception);

        // Assert
        message.Should().Contain("🌐");
        message.Should().Contain("Network error");
    }

    [Fact]
    public void FormatFriendlyError_GenericException_ReturnsGenericMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var message = ConversationalAIHelper.FormatFriendlyError(exception);

        // Assert
        message.Should().Contain("❌");
        message.Should().Contain("Something went wrong");
    }

    [Fact]
    public void FormatFriendlyError_NullException_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => ConversationalAIHelper.FormatFriendlyError(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("exception");
    }
}
