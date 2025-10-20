# pyright: reportMissingImports=false, reportGeneralTypeIssues=false
"""E2E-style CLR test for QuickBooks OAuth/tunnel flow and token refresh.

This test exercises two critical integration paths without hitting external services:
- cloudflared tunnel readiness (mocked via a local .cmd that prints a trycloudflare URL)
- token refresh entrypoint (ensures no MissingMethodException is thrown)

Notes:
- We supply QuickBooks client credentials via a stubbed ISecretVaultService so EnsureInitializedAsync succeeds
  even though EnvironmentVariableTarget.User values aren't populated for this process.
- We keep a valid access token to avoid live HTTP calls to Intuit; the goal here is verifying method wiring
  and preventing runtime MissingMethodException regressions.
"""

from __future__ import annotations

import os
import tempfile
from pathlib import Path

import pytest

# pythonnet/CLR availability guard without importing unused symbols
try:
    import importlib.util as _importlib_util

    HAS_PYTHONNET = _importlib_util.find_spec("clr") is not None
    if HAS_PYTHONNET:
        import clr  # type: ignore[import-not-found]

        _ = clr  # mark used
except Exception:
    HAS_PYTHONNET = False

pytestmark = [
    pytest.mark.clr,
    pytest.mark.integration,
    pytest.mark.skipif(not HAS_PYTHONNET, reason="pythonnet required for CLR tests"),
]

if not HAS_PYTHONNET:  # pragma: no cover - import guard
    pytest.skip("pythonnet required for CLR integration tests", allow_module_level=True)


def _await(task):
    return task.GetAwaiter().GetResult()


def _make_fake_cloudflared_cmd(tmpdir: Path) -> str:
    """Create a temporary .cmd that prints a trycloudflare URL and lingers briefly.

    QuickBooksService watches stdout for a URL matching `*.trycloudflare.com` to mark readiness.
    """
    cmd_path = tmpdir / "fake-cloudflared.cmd"
    # Keep this minimal; ignore any passed arguments.
    content = (
        "@echo off\r\n"
        "echo https://unit-test.trycloudflare.com\r\n"
        "rem brief wait to keep process alive while reader hooks\r\n"
        "ping -n 2 127.0.0.1 >NUL\r\n"
    )
    cmd_path.write_text(content, encoding="utf-8")
    return str(cmd_path)


def _build_quickbooks_service(clr_loader, ensure_assemblies_present):
    # Load dependencies
    clr_loader("Microsoft.Extensions.Logging.Abstractions")

    from System import (  # type: ignore[attr-defined]
        Activator,
        Array,
        DateTime,  # type: ignore[attr-defined]
        Object,
    )
    from System.Reflection import Assembly  # type: ignore[attr-defined]
    from System.Threading import (  # type: ignore[attr-defined]
        CancellationToken,
        CancellationTokenSource,
    )

    # Null logger via factory
    logging_assembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions")
    null_factory_type = logging_assembly.GetType(
        "Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory"
    )
    null_factory = null_factory_type.GetProperty("Instance").GetValue(None, None)
    logger = null_factory.CreateLogger("WileyWidget.Services.QuickBooksService")

    # SettingsService instance with test-friendly directory
    from WileyWidget.Services import SettingsService  # type: ignore[attr-defined]

    # Ensure temp settings location to avoid touching AppData
    tmp_dir = Path(tempfile.mkdtemp(prefix="wileywidget_settings_"))
    os.environ["WILEYWIDGET_SETTINGS_DIR"] = str(tmp_dir)
    settings = SettingsService()
    settings.ResetForTests()

    # Prime tokens so RefreshTokenIfNeededAsync is a no-op (no outbound HTTP)
    now_utc = DateTime.UtcNow
    settings.Current.QboAccessToken = "x" * 64
    settings.Current.QboRefreshToken = "r" * 64
    settings.Current.QboTokenExpiry = now_utc.AddMinutes(10)

    # Stub secret vault so EnsureInitializedAsync can fetch client credentials
    from System.Threading.Tasks import Task  # type: ignore[attr-defined]
    from WileyWidget.Services import ISecretVaultService  # type: ignore[attr-defined]

    class SecretVaultStub(ISecretVaultService):  # type: ignore[misc]
        def GetSecretAsync(self, secretName):  # type: ignore[override]
            name = str(secretName)
            if name in ("QBO-CLIENT-ID", "QuickBooks-ClientId"):
                return Task.FromResult("client-id-123")
            if name in ("QBO-CLIENT-SECRET", "QuickBooks-ClientSecret"):
                return Task.FromResult("client-secret-xyz")
            if name in ("QBO-REALM-ID", "QuickBooks-RealmId"):
                return Task.FromResult("1234567890")
            if name == "QBO-REDIRECT-URI":
                return Task.FromResult("http://localhost:8080/callback")
            return Task.FromResult[str | None](None)

        def SetSecretAsync(self, secretName, value):  # type: ignore[override]
            return Task.CompletedTask

        def TestConnectionAsync(self):  # type: ignore[override]
            return Task.FromResult(True)

    secret_stub = SecretVaultStub()

    # Construct QuickBooksService via reflection (assembly already loaded by fixture)
    from WileyWidget.Services import QuickBooksService  # type: ignore[attr-defined]

    service = Activator.CreateInstance(
        QuickBooksService, Array[Object]([settings, secret_stub, logger])
    )

    return service, CancellationToken, CancellationTokenSource


def test_oauth_flow_with_cloudflare_tunnel_and_refresh(
    clr_loader, ensure_assemblies_present, load_wileywidget_core
):
    """Mock tunnel startup, then verify token refresh path doesn't raise MissingMethodException.

    Steps:
    1) Set CLOUDFLARED_EXE to a temp .cmd that prints a trycloudflare URL so readiness is detected.
    2) Invoke the private EnsureCloudflaredTunnelAsync via reflection and assert True is returned.
    3) Call RefreshTokenIfNeededAsync and assert it completes without throwing.
    """

    from System import Array, Object  # type: ignore[attr-defined]
    from System.Reflection import BindingFlags  # type: ignore[attr-defined]

    service, CancellationToken, CancellationTokenSource = _build_quickbooks_service(
        clr_loader, ensure_assemblies_present
    )

    # Prepare a fake cloudflared binary
    with tempfile.TemporaryDirectory(prefix="ww_fake_cf_") as tmp:
        fake_exe = _make_fake_cloudflared_cmd(Path(tmp))
        os.environ["CLOUDFLARED_EXE"] = fake_exe
        # Optional extra args (ignored by our .cmd)
        os.environ["CLOUDFLARED_ARGS"] = "--test-arg"

        # Reflect the private EnsureCloudflaredTunnelAsync(CancellationToken) method
        svc_type = service.GetType()
        method = svc_type.GetMethod(
            "EnsureCloudflaredTunnelAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
        )
        assert (
            method is not None
        ), "EnsureCloudflaredTunnelAsync not found via reflection"

        cts = CancellationTokenSource()
        cts.CancelAfter(5000)
        task = method.Invoke(service, Array[Object]([cts.Token]))
        ready = _await(task)
        assert ready is True, "cloudflared tunnel readiness was not detected"

    # Now exercise the token refresh entrypoint; we primed settings so this is a no-op
    refresh_method = svc_type.GetMethod(
        "RefreshTokenIfNeededAsync", BindingFlags.Instance | BindingFlags.Public
    )
    assert refresh_method is not None, "RefreshTokenIfNeededAsync not found"
    # No parameters for RefreshTokenIfNeededAsync
    result_task = refresh_method.Invoke(service, None)
    # Expect no MissingMethodException or other runtime wiring failures
    _await(result_task)


@pytest.mark.slow
def test_interactive_oauth_listener_handles_error_callback(
    clr_loader, ensure_assemblies_present, load_wileywidget_core
):
    """Start the real OAuth listener and simulate an error callback.

    This avoids live token exchange by providing `error=access_denied` in the querystring.
    It verifies that:
    - URL ACL is present for the redirect URI (otherwise the listener cannot start)
    - cloudflared readiness is mocked and detected
    - AuthorizeAsync returns False and responds to the HTTP request without throwing
    """

    if os.environ.get("RUN_QB_OAUTH_TESTS", "0").lower() not in ("1", "true", "yes"):
        pytest.skip("Interactive OAuth listener test is opt-in; set RUN_QB_OAUTH_TESTS=1 to enable.")

    from System import Array, Object  # type: ignore[attr-defined]
    from System.Net.Http import HttpClient  # type: ignore[attr-defined]
    from System.Reflection import BindingFlags  # type: ignore[attr-defined]

    service, CancellationToken, CancellationTokenSource = _build_quickbooks_service(
        clr_loader, ensure_assemblies_present
    )

    # Ensure cloudflared is mocked for this run
    with tempfile.TemporaryDirectory(prefix="ww_fake_cf_") as tmp:
        fake_exe = _make_fake_cloudflared_cmd(Path(tmp))
        os.environ["CLOUDFLARED_EXE"] = fake_exe

        # 1) Verify URL ACL is ready for the redirect URI; otherwise skip (requires admin to add)
        check_task = service.CheckUrlAclAsync("http://localhost:8080/callback")
        acl = _await(check_task)
        if not acl.IsReady:
            pytest.skip(
                "URL ACL not present for http://localhost:8080/callback; run elevated to enable HttpListener."
            )

        # 2) Ensure cloudflared tunnel reports ready
        svc_type = service.GetType()
        ensure_cf = svc_type.GetMethod(
            "EnsureCloudflaredTunnelAsync", BindingFlags.Instance | BindingFlags.NonPublic
        )
        assert ensure_cf is not None
        cts = CancellationTokenSource()
        cts.CancelAfter(5000)
        ready_task = ensure_cf.Invoke(service, Array[Object]([cts.Token]))
        ready = _await(ready_task)
        assert ready is True

        # 3) Launch the OAuth listener via public API and send an error callback
        auth_task = service.AuthorizeAsync()

        # Give the listener a moment to start
        client = HttpClient()
        # error path avoids token exchange but still exercises the listener response
        resp_task = client.GetAsync("http://localhost:8080/callback?error=access_denied")
        _ = _await(resp_task)  # ignore status details

        # AuthorizeAsync should return False due to error
        result = _await(auth_task)
        assert result is False


def test_expired_token_triggers_refresh_without_missingmethod(
    clr_loader, ensure_assemblies_present, load_wileywidget_core
):
    """Force the refresh path and ensure no MissingMethodException is raised.

    This simulates a realistic scenario where the access token is expired but a refresh token exists.
    Network failures are tolerated; we only assert that no MissingMethodException is thrown, guarding
    against assembly binding issues.
    """

    from System import DateTime, MissingMethodException  # type: ignore[attr-defined]
    from System.Reflection import BindingFlags  # type: ignore[attr-defined]

    service, _CancellationToken, _CancellationTokenSource = _build_quickbooks_service(
        clr_loader, ensure_assemblies_present
    )

    # Expire the token and keep a refresh token so refresh path is taken
    svc_type = service.GetType()
    settings_field = svc_type.GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)
    settings_service = settings_field.GetValue(service)
    current = settings_service.Current
    current.QboAccessToken = "expired"
    current.QboTokenExpiry = DateTime.UtcNow.AddMinutes(-10)
    current.QboRefreshToken = current.QboRefreshToken or "refresh-token"
    settings_service.Save()

    # Invoke public refresh method
    refresh_method = svc_type.GetMethod("RefreshTokenIfNeededAsync", BindingFlags.Instance | BindingFlags.Public)
    task = refresh_method.Invoke(service, None)
    try:
        _await(task)
    except Exception as ex:  # noqa: BLE001 - testing CLR exception type
        # It's acceptable to see HTTP-related exceptions in offline environments
        assert not isinstance(ex, MissingMethodException), (
            "MissingMethodException during refresh indicates assembly binding/runtime mismatch"
        )
