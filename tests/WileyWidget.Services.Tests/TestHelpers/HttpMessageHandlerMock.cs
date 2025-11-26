using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Tests.TestHelpers
{
    // Very small HTTP handler to return canned responses based on substring match
    public class HttpMessageHandlerMock : HttpMessageHandler
    {
        private readonly List<(string match, HttpResponseMessage response)> _rules = new();

        public void When(string substring, HttpResponseMessage response)
        {
            _rules.Add((substring, response));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var uri = request.RequestUri?.ToString() ?? string.Empty;
            foreach (var (match, response) in _rules)
            {
                if (uri.Contains(match, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(response.Clone());
            }
            // Default: 404
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    static class HttpResponseMessageExtensions
    {
        public static HttpResponseMessage Clone(this HttpResponseMessage source)
        {
            var copy = new HttpResponseMessage(source.StatusCode)
            {
                Content = source.Content,
                ReasonPhrase = source.ReasonPhrase,
                Version = source.Version
            };
            foreach (var h in source.Headers)
                copy.Headers.TryAddWithoutValidation(h.Key, h.Value);
            return copy;
        }
    }
}
