using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Tests.TestHelpers
{
    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responder(request, cancellationToken);
        }

        public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            return (req, ct) => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json")
            });
        }
    }
}
