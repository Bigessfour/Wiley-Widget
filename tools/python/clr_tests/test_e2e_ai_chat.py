"""E2E-style CLR tests exercising XAIService through to the AI ViewModel.

These tests use pythonnet to instantiate CLR types, stub the HTTP responses
from the xAI endpoint, and assert the ViewModel surfaces the AI response.

Note: Marked as clr/integration and skipped when pythonnet isn't available (same
pattern as other clr tests in this folder).
"""

from __future__ import annotations

import json

import pytest

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

if HAS_PYTHONNET:
    try:
        clr.AddReference("Microsoft.Extensions.Configuration")
        from Microsoft.Extensions.Configuration import ConfigurationBuilder  # type: ignore[attr-defined]
        from System import Activator, Array, Object, String
        from System.Text import Encoding  # type: ignore[attr-defined]
        from System.Net import HttpStatusCode  # type: ignore[attr-defined]
        from System.Net.Http import HttpClient, HttpMessageHandler, HttpRequestException, HttpRequestMessage, HttpResponseMessage, IHttpClientFactory, StringContent  # type: ignore[attr-defined]
        from System.Reflection import BindingFlags  # type: ignore[attr-defined]
    except Exception as exc:
        HAS_PYTHONNET = False
        pytest.skip(f"Missing required CLR types: {exc}", allow_module_level=True)
else:
    ConfigurationBuilder = None
    Activator = None
    Array = None
    Object = None
    String = None
    Encoding = None
    HttpStatusCode = None
    HttpClient = None
    HttpMessageHandler = None
    HttpRequestException = None
    HttpRequestMessage = None
    HttpResponseMessage = None
    IHttpClientFactory = None
    StringContent = None
    BindingFlags = None

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
                from System.Threading.Tasks import Task  # type: ignore[attr-defined]

                return Task.FromResult(result)
            if isinstance(result, Exception):
                raise result
            raise Exception("Responder returned unsupported type")


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


def _create_xai_service(clr_loader, ensure_assemblies_present, responders, config_overrides=None):
    clr_loader("Microsoft.Extensions.Http")
    clr_loader("Microsoft.Extensions.Logging.Abstractions")
    clr_loader("Microsoft.Extensions.Caching.Memory")

    from Microsoft.Extensions.Caching.Memory import MemoryCache, MemoryCacheOptions  # type: ignore[attr-defined]

    configuration = _build_configuration(clr_loader, config_overrides)

    from System.Reflection import Assembly  # type: ignore[attr-defined]

    logging_assembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions")
    null_factory_type = logging_assembly.GetType("Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory")
    null_factory = null_factory_type.GetProperty("Instance").GetValue(None, None)
    logger = null_factory.CreateLogger("WileyWidget.Services.XAIService")

    cache = MemoryCache(MemoryCacheOptions())
    handler = SequenceHandler(responders)
    factory = HttpClientFactoryStub(handler)
    from WileyWidget.Services import IAILoggingService, IWileyWidgetContextService  # type: ignore[attr-defined]

    class ContextServiceStub(IWileyWidgetContextService):
        def BuildCurrentSystemContextAsync(self, cancellation_token=None):  # type: ignore[override]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

            return Task.FromResult("system context")

        def GetEnterpriseContextAsync(self, enterprise_id):  # type: ignore[override]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

            return Task.FromResult("enterprise context")

        def GetBudgetContextAsync(self, start_date, end_date):  # type: ignore[override]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

            return Task.FromResult("budget context")

        def GetOperationalContextAsync(self):  # type: ignore[override]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

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
            from System.Collections.Generic import Dictionary  # type: ignore[attr-defined]
            from System import Object, String  # type: ignore[attr-defined]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

            stats = Dictionary[String, Object]()
            return Task.FromResult(stats)

        def GetTodayQueryCount(self):  # type: ignore[override]
            return len(self.queries)

        def GetAverageResponseTime(self):  # type: ignore[override]
            return 0.0

        def GetErrorRate(self):  # type: ignore[override]
            return 0.0

        def ExportLogsAsync(self, file_path, start_date, end_date):  # type: ignore[override]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

            return Task.CompletedTask

    context_service = ContextServiceStub()
    logging_service = AILoggingServiceStub()

    xai_type = dotnet_utils.get_type(ensure_assemblies_present, "WileyWidget", "WileyWidget.Services.XAIService")
    service = Activator.CreateInstance(
        xai_type,
        Array[Object]([factory, configuration, logger, context_service, logging_service, cache]),
    )
    return service, handler, logging_service


def test_get_insights_with_status_e2e(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    responders = [
        lambda _req: _json_response({"choices": [{"message": {"content": "E2E reply"}}]}),
    ]
    service, handler, logging_service = _create_xai_service(clr_loader, ensure_assemblies_present, responders)
    result = _await(service.GetInsightsWithStatusAsync("ctx", "question"))
    # result is an AIResponseResult (CLR type) with properties Content and HttpStatusCode
    assert result.Content == "E2E reply"
    assert result.HttpStatusCode == 200
    assert handler.calls == 1
    assert logging_service.responses


def test_ai_viewmodel_integration_e2e(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    # This test wires XAIService into the AIAssistViewModel and invokes the internal Generate method
    responders = [
        lambda _req: _json_response({"choices": [{"message": {"content": "VM reply"}}]}),
    ]
    xai_service, handler, logging_service = _create_xai_service(clr_loader, ensure_assemblies_present, responders)

    # Build simple stubs for the many AIAssistViewModel dependencies
    from System.Reflection import Assembly  # type: ignore[attr-defined]
    logging_assembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions")
    null_factory_type = logging_assembly.GetType("Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory")
    null_factory = null_factory_type.GetProperty("Instance").GetValue(None, None)
    vm_logger = null_factory.CreateLogger("WileyWidget.ViewModels.AIAssistViewModel")

    # Minimal stub implementations
    from WileyWidget.Services import IGrokSupercomputer  # type: ignore[attr-defined]

    class GrokStub(IGrokSupercomputer):
        def FetchEnterpriseDataAsync(self, *args, **kwargs):  # type: ignore[override]
            from System.Threading.Tasks import Task  # type: ignore[attr-defined]

            return Task.FromResult(None)

    class DummyChargeService(object):
        pass

    class DummyWhatIfEngine(object):
        pass

    class DummyEnterpriseRepo(object):
        pass

    # Dispatcher helper stub that simply invokes the action immediately
    from WileyWidget.Services.Threading import IDispatcherHelper  # type: ignore[attr-defined]

    class DispatcherStub(IDispatcherHelper):
        def Invoke(self, action):  # type: ignore[override]
            return action()

    # Event aggregator stub
    from Prism.Events import EventAggregator  # type: ignore[attr-defined]

    event_aggregator = EventAggregator()

    # Create the ViewModel via Activator
    vm_type = dotnet_utils.get_type(ensure_assemblies_present, "WileyWidget", "WileyWidget.ViewModels.AIAssistViewModel")

    vm = Activator.CreateInstance(
        vm_type,
        Array[Object]([
            xai_service,
            DummyChargeService(),
            DummyWhatIfEngine(),
            GrokStub(),
            DummyEnterpriseRepo(),
            DispatcherStub(),
            vm_logger,
            event_aggregator,
        ]),
    )

    # Set QueryText and invoke the private Generate() method via reflection
    setattr(vm, "QueryText", "Test Query")
    method = vm_type.GetMethod("Generate", BindingFlags.NonPublic | BindingFlags.Instance)
    task = method.Invoke(vm, Array[Object]([]))
    # Wait for the Task to complete
    _await(task)

    # Verify ViewModel Response property was set
    response = getattr(vm, "Response")
    assert response == "VM reply"
    assert handler.calls == 1
