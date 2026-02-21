using System.Net;
using System.Text;
using Moq;
using Moq.Protected;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Fluent builder for creating mock HttpMessageHandler instances to simulate xAI API responses.
/// </summary>
public class MockHttpMessageHandlerBuilder
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _responseContent = string.Empty;
    private Dictionary<string, string> _responseHeaders = new();
    private TimeSpan? _delay;
    private Exception? _exception;
    private Queue<(HttpStatusCode statusCode, string content)>? _sequenceResponses;

    /// <summary>
    /// Sets the HTTP status code for the response.
    /// </summary>
    public MockHttpMessageHandlerBuilder WithStatusCode(HttpStatusCode statusCode)
    {
        _statusCode = statusCode;
        return this;
    }

    /// <summary>
    /// Sets the response content body (typically JSON).
    /// </summary>
    public MockHttpMessageHandlerBuilder WithContent(string content)
    {
        _responseContent = content;
        return this;
    }

    /// <summary>
    /// Sets a response header (e.g., Retry-After for rate limiting).
    /// </summary>
    public MockHttpMessageHandlerBuilder WithHeader(string name, string value)
    {
        _responseHeaders[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a delay before returning the response (simulates network latency).
    /// </summary>
    public MockHttpMessageHandlerBuilder WithDelay(TimeSpan delay)
    {
        _delay = delay;
        return this;
    }

    /// <summary>
    /// Throws an exception instead of returning a response (simulates network failure).
    /// </summary>
    public MockHttpMessageHandlerBuilder WithException(Exception exception)
    {
        _exception = exception;
        return this;
    }

    /// <summary>
    /// Sets up a sequence of responses (for testing retries).
    /// Example: First call returns 500, second returns 500, third returns 200.
    /// </summary>
    public MockHttpMessageHandlerBuilder WithSequence(params (HttpStatusCode statusCode, string content)[] responses)
    {
        _sequenceResponses = new Queue<(HttpStatusCode, string)>(responses);
        return this;
    }

    /// <summary>
    /// Builds a mock HttpMessageHandler configured with the specified behavior.
    /// </summary>
    public HttpMessageHandler Build()
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        if (_sequenceResponses != null && _sequenceResponses.Count > 0)
        {
            // Setup sequence for retry testing
            var setup = mockHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

            foreach (var (statusCode, content) in _sequenceResponses)
            {
                var response = CreateResponse(statusCode, content);
                setup = setup.ReturnsAsync(response);
            }
        }
        else if (_exception != null)
        {
            // Setup exception throwing
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(_exception);
        }
        else
        {
            // Setup single response
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async () =>
                {
                    if (_delay.HasValue)
                    {
                        await Task.Delay(_delay.Value);
                    }

                    return CreateResponse(_statusCode, _responseContent);
                });
        }

        return mockHandler.Object;
    }

    private HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        foreach (var header in _responseHeaders)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }

    /// <summary>
    /// Creates a mock response for a successful xAI API chat completion.
    /// </summary>
    public static string CreateSuccessResponse(string message = "Test response", int totalTokens = 100)
    {
        return $$"""
        {
          "id": "chatcmpl-{{Guid.NewGuid()}}",
          "object": "chat.completion",
          "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "model": "grok-4-1-fast-reasoning",
          "choices": [
            {
              "index": 0,
              "message": {
                "role": "assistant",
                "content": "{{message}}"
              },
              "finish_reason": "stop"
            }
          ],
          "usage": {
            "prompt_tokens": 50,
            "completion_tokens": 50,
            "total_tokens": {{totalTokens}}
          }
        }
        """;
    }

    /// <summary>
    /// Creates a mock response for an xAI API error (e.g., deprecated model).
    /// </summary>
    public static string CreateErrorResponse(string errorMessage, string errorType = "invalid_request_error")
    {
        return $$"""
        {
          "error": {
            "message": "{{errorMessage}}",
            "type": "{{errorType}}",
            "code": "model_not_found"
          }
        }
        """;
    }

    /// <summary>
    /// Creates a mock streaming response chunk (SSE format).
    /// </summary>
    public static string CreateStreamingChunk(string contentDelta, bool isLast = false)
    {
        var finishReason = isLast ? "\"stop\"" : "null";
        return $$"""
        data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},"model":"grok-4-1-fast-reasoning","choices":[{"index":0,"delta":{"content":"{{contentDelta}}"},"finish_reason":{{finishReason}}}]}

        """;
    }
}
