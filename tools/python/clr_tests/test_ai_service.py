"""Tests for XAIService via pythonnet with stubbed dependencies."""

from __future__ import annotations

import json

import pytest

# Check for pythonnet availability
try:
    import clr  # type: ignore[import-not-found]
    HAS_PYTHONNET = True
except (ImportError, RuntimeError, AttributeError):
    HAS_PYTHONNET = False

pytestmark = [
    pytest.mark.clr,
    pytest.mark.integration,
    pytest.mark.skipif(not HAS_PYTHONNET, reason="pythonnet required for CLR tests"),
]

# Import CLR types only if available
if HAS_PYTHONNET:
    clr.AddReference("Microsoft.Extensions.Configuration")
    try:
        from Microsoft.Extensions.Configuration import (  # type: ignore[attr-defined, import-not-found]
            ConfigurationBuilder,
        )
        from System import (  # type: ignore[attr-defined, import-not-found]
            Activator,
            ArgumentException,
            Array,
            InvalidOperationException,
            Object,
            String,
        )
        from System.Collections.Generic import Dictionary  # type: ignore[attr-defined]
        from System.Net import HttpStatusCode  # type: ignore[attr-defined]
        from System.Net.Http import (  # type: ignore[attr-defined, import-not-found]
            HttpClient,
            HttpMessageHandler,
            HttpRequestException,
            HttpRequestMessage,
            HttpResponseMessage,
            IHttpClientFactory,
            StringContent,
        )
        from System.Text import Encoding  # type: ignore[attr-defined]
        from System.Threading.Tasks import (
            Task,  # type: ignore[attr-defined, import-not-found]
        )
    except Exception:
        # If imports fail, set to None
        ConfigurationBuilder = None
        Activator = None
        ArgumentException = None
        Array = None
        InvalidOperationException = None
        Object = None
        String = None
        Dictionary = None
        HttpStatusCode = None
        HttpClient = None
        HttpMessageHandler = None
        HttpRequestException = None
        HttpRequestMessage = None
        HttpResponseMessage = None
        IHttpClientFactory = None
        StringContent = None
        Encoding = None
        Task = None
else:
    # Define dummy classes to avoid NameError
    ConfigurationBuilder = None
    Activator = None
    ArgumentException = None
    Array = None
    InvalidOperationException = None
    Object = None
    String = None
    Dictionary = None
    HttpStatusCode = None
    HttpClient = None
    HttpMessageHandler = None
    HttpRequestException = None
    HttpRequestMessage = None
    HttpResponseMessage = None
    IHttpClientFactory = None
    StringContent = None
    Encoding = None
    Task = None

if HAS_PYTHONNET and ConfigurationBuilder is None:
    pytest.skip("Required CLR types not available", allow_module_level=True)

from .helpers import dotnet_utils


def _await(task):
    return task.GetAwaiter().GetResult()


if HAS_PYTHONNET:
    class SequenceHandler(HttpMessageHandler):
        def __init__(self, responders):
            super().__init__()
            self._responders = responders
            self.calls = 0

        def SendAsync(self, request: HttpRequestMessage, cancellation_token):  # type: ignore[override]
            index = min(self.calls, len(self._responders) - 1)
            responder = self._responders[index]
            self.calls += 1
            result = responder(request)
            if isinstance(result, HttpResponseMessage):
                return Task.FromResult(result)
            if isinstance(result, Exception):
                raise result
            raise InvalidOperationException("Responder returned unsupported type")


    class HttpClientFactoryStub(IHttpClientFactory):
        def __init__(self, handler: SequenceHandler):
            self._handler = handler

        def CreateClient(self, name):  # type: ignore[override]
            return HttpClient(self._handler, False)


    def _json_response(payload: dict, status: HttpStatusCode = HttpStatusCode.OK):
        message = HttpResponseMessage(status)
        content = json.dumps(payload)
        message.Content = StringContent(content, Encoding.UTF8, "application/json")
        return message


def _build_configuration(clr_loader, overrides=None):
    clr_loader("Microsoft.Extensions.Configuration")
    clr_loader("Microsoft.Extensions.Configuration.Json")

    data = {
        "XAI:ApiKey": "x" * 32,
        "XAI:BaseUrl": "https://api.test.local/",
        "XAI:TimeoutSeconds": "5",
        "XAI:Model": "grok-4-0709",
        "XAI:MaxConcurrentRequests": "3",
    }
    if overrides:
        data.update(overrides)

    builder = ConfigurationBuilder()
    builder.AddInMemoryCollection(data.items())
    return builder.Build()


def _create_service(clr_loader, ensure_assemblies_present, responders, config_overrides=None):
    clr_loader("Microsoft.Extensions.Http")
    clr_loader("Microsoft.Extensions.Logging.Abstractions")
    clr_loader("Microsoft.Extensions.Caching.Memory")

    from Microsoft.Extensions.Caching.Memory import (  # type: ignore[attr-defined]
        MemoryCache,
        MemoryCacheOptions,
    )

    configuration = _build_configuration(clr_loader, config_overrides)

    from System.Reflection import Assembly  # type: ignore[attr-defined]

    logging_assembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions")
    null_factory_type = logging_assembly.GetType("Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory")
    null_factory = null_factory_type.GetProperty("Instance").GetValue(None, None)
    logger = null_factory.CreateLogger("WileyWidget.Services.XAIService")

    cache = MemoryCache(MemoryCacheOptions())
    handler = SequenceHandler(responders)
    factory = HttpClientFactoryStub(handler)
    from WileyWidget.Services import (  # type: ignore[attr-defined]
        IAILoggingService,
        IWileyWidgetContextService,
    )

    class ContextServiceStub(IWileyWidgetContextService):
        def BuildCurrentSystemContextAsync(self, cancellation_token=None):  # type: ignore[override]
            return Task.FromResult("system context")

        def GetEnterpriseContextAsync(self, enterprise_id):  # type: ignore[override]
            return Task.FromResult("enterprise context")

        def GetBudgetContextAsync(self, start_date, end_date):  # type: ignore[override]
            return Task.FromResult("budget context")

        def GetOperationalContextAsync(self):  # type: ignore[override]
            return Task.FromResult("operational context")

    class AILoggingServiceStub(IAILoggingService):
        def __init__(self):
            self.queries = []
            self.responses = []
            self.errors = []

        def LogQuery(self, query, context, model):  # type: ignore[override]
            self.queries.append((query, context, model))

        def LogResponse(self, query, response, response_time_ms, tokens_used=0):  # type: ignore[override]
            self.responses.append((query, response, response_time_ms, tokens_used))

        def LogError(self, query, error, error_type=None):  # type: ignore[override]
            self.errors.append((query, error, error_type))

        def LogMetric(self, metric_name, metric_value, metadata=None):  # type: ignore[override]
            pass

        def LogError_overload_1(self, query, exception):  # type: ignore[override]
            self.errors.append((query, str(exception), "exception"))

        def GetUsageStatisticsAsync(self, start_date, end_date):  # type: ignore[override]
            stats = Dictionary[String, Object]()
            return Task.FromResult(stats)

        def GetTodayQueryCount(self):  # type: ignore[override]
            return len(self.queries)

        def GetAverageResponseTime(self):  # type: ignore[override]
            return 0.0

        def GetErrorRate(self):  # type: ignore[override]
            return 0.0

        def ExportLogsAsync(self, file_path, start_date, end_date):  # type: ignore[override]
            return Task.CompletedTask

    context_service = ContextServiceStub()
    logging_service = AILoggingServiceStub()

    xai_type = dotnet_utils.get_type(ensure_assemblies_present, "WileyWidget", "WileyWidget.Services.XAIService")
    service = Activator.CreateInstance(
        xai_type,
        Array[Object]([factory, configuration, logger, context_service, logging_service, cache]),
    )
    return service, handler, logging_service


def test_get_insights_success(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    responders = [
        lambda _req: _json_response({"choices": [{"message": {"content": "Hello"}}]}),
    ]
    service, handler, logging_service = _create_service(clr_loader, ensure_assemblies_present, responders)
    result = _await(service.GetInsightsAsync("context", "question"))
    assert result == "Hello"
    assert handler.calls == 1
    assert logging_service.responses


def test_get_insights_failure_network(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    responders = [
        lambda _req: HttpRequestException("Network error"),
    ]
    service, handler, logging_service = _create_service(clr_loader, ensure_assemblies_present, responders)
    result = _await(service.GetInsightsAsync("context", "question"))
    assert "network" in result.lower()
    assert handler.calls == 1
    assert logging_service.errors


def test_get_insights_rate_limit_retry(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    responders = [
        lambda _req: _json_response({"error": None, "choices": []}, HttpStatusCode.TooManyRequests),
        lambda _req: _json_response({"choices": [{"message": {"content": "Retried"}}]}),
    ]
    service, handler, _ = _create_service(clr_loader, ensure_assemblies_present, responders)
    result = _await(service.GetInsightsAsync("context", "question"))
    assert result == "Retried"
    assert handler.calls >= 2


def test_invalid_api_key_raises(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    with pytest.raises(ArgumentException):
        _create_service(clr_loader, ensure_assemblies_present, [lambda _req: _json_response({})], {"XAI:ApiKey": "short"})


def test_get_insights_uses_cache(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    responders = [
        lambda _req: _json_response({"choices": [{"message": {"content": "Cached"}}]}),
    ]
    service, handler, _ = _create_service(clr_loader, ensure_assemblies_present, responders)
    first = _await(service.GetInsightsAsync("context", "question"))
    second = _await(service.GetInsightsAsync("context", "question"))
    assert first == second == "Cached"
    assert handler.calls == 1
