# tests/ui_tests/test_dispose_docking.py
# Pytest + pywinauto test to repeatedly start the WinForms app, close it,
# and scan logs for disposal/docking related exceptions.

import os
import time
import re
import glob
import subprocess
import shutil
from pathlib import Path
from datetime import datetime

import pytest  # type: ignore[reportMissingImports]

try:
    from pywinauto import Application, timings  # type: ignore[reportMissingImports]
except Exception:
    Application = None
    timings = None

# Configure these to match your build/output (override with WW_APP_EXE env var)
DEFAULT_EXE_REL = (
    Path("src")
    / "WileyWidget.WinForms"
    / "bin"
    / "Debug"
    / "net9.0-windows"
    / "WileyWidget.WinForms.exe"
)
LOG_GLOB = "logs/wileywidget-*.log"
RUN_REPEATS = 3
# Increased timeouts to be less flaky on slower machines
LAUNCH_TIMEOUT = 40
CLOSE_TIMEOUT = 60
WINDOW_RETRY = 6
ARTIFACTS_ROOT = (
    Path(__file__).resolve().parents[2] / "tests" / "ui_tests" / "artifacts"
)


def locate_exe():
    env = os.environ.get("WW_APP_EXE")
    if env:
        return Path(env)
    repo_root = Path(__file__).resolve().parents[2]
    candidate = repo_root / DEFAULT_EXE_REL
    if candidate.exists():
        return candidate
    raise FileNotFoundError(
        f"Cannot find WileyWidget exe at {candidate}. Set WW_APP_EXE env to override."
    )


def tail_logs_since(ts):
    repo_root = Path(__file__).resolve().parents[2]
    logs_dir = repo_root / "logs"
    if not logs_dir.exists():
        return ""
    out = []
    # Grab any log files modified since the test started
    for path in sorted(
        glob.glob(str(logs_dir / "wileywidget-*.log")), key=os.path.getmtime
    ):
        try:
            mtime = os.path.getmtime(path)
            if mtime >= ts - 1:
                with open(path, "r", encoding="utf-8", errors="ignore") as f:
                    out.append(f.read())
        except Exception:
            continue
    return "\n".join(out)


def assert_no_dispose_exceptions(log_text):
    patterns = [
        r"Exception thrown while disposing",
        r"ObjectDisposedException",
        r"\bDisposed\b",
        r"Unhandled application domain exception",
        r"Unhandled exception during Application.Run",
        r"PendingModelChangesWarning",
        r"\bFatal\b",
    ]
    combined = re.compile("|".join(patterns), re.IGNORECASE)
    m = combined.search(log_text)
    if m:
        pytest.fail(
            "Disposal/docking-related exception text found in logs:\n\n" + m.group(0)
        )


def copy_logs_to_artifact(dest_dir: Path, since_ts: float):
    repo_root = Path(__file__).resolve().parents[2]
    logs_dir = repo_root / "logs"
    if not logs_dir.exists():
        return
    for path in sorted(
        glob.glob(str(logs_dir / "wileywidget-*.log")), key=os.path.getmtime
    ):
        try:
            mtime = os.path.getmtime(path)
            if mtime >= since_ts - 1:
                shutil.copy2(path, dest_dir / Path(path).name)
        except Exception:
            continue


@pytest.mark.skipif(Application is None, reason="pywinauto not installed")
@pytest.mark.parametrize("i", range(RUN_REPEATS))
def test_start_and_graceful_close_no_dispose_exceptions(i):
    # runtime-narrowing for Application/timings — skip early if pywinauto not available
    if Application is None or timings is None:
        pytest.skip("pywinauto not installed or incomplete")
    exe = locate_exe()
    start_ts = time.time()

    # Create artifacts dir for this run
    artifact_dir = ARTIFACTS_ROOT / datetime.utcnow().strftime("%Y%m%dT%H%M%SZ")
    artifact_dir.mkdir(parents=True, exist_ok=True)

    # Launch the app and capture stdout/stderr to artifact files so we can inspect on failures
    out_path = artifact_dir / "app_stdout.txt"
    err_path = artifact_dir / "app_stderr.txt"
    out_f = open(out_path, "wb")
    err_f = open(err_path, "wb")
    proc = subprocess.Popen([str(exe)], stdout=out_f, stderr=err_f)
    try:
        # Connect using pywinauto to ensure the window is created (retry loop)
        app = Application(backend="win32")
        connected = False
        for attempt in range(WINDOW_RETRY):
            try:
                timings.wait_until_passes(
                    max(1, LAUNCH_TIMEOUT // WINDOW_RETRY),
                    0.5,
                    lambda: app.connect(process=proc.pid),
                )
                app.connect(
                    process=proc.pid, timeout=max(1, LAUNCH_TIMEOUT // WINDOW_RETRY)
                )
                connected = True
                break
            except Exception:
                time.sleep(0.5)

        if not connected:
            # Capture any stdout/stderr and copy logs to artifacts then fail with diagnostics
            out_f.flush()
            err_f.flush()
            copy_logs_to_artifact(artifact_dir, start_ts)
            raise RuntimeError(
                f"Unable to connect to app process (pid={proc.pid}) or no top-level window found; see {artifact_dir}"
            )

        # Get the main window and wait until visible
        main = app.top_window()
        timings.wait_until_passes(
            LAUNCH_TIMEOUT, 0.5, lambda: main.exists() and main.is_visible()
        )
        time.sleep(0.5)

        # Optional: interact with UI to open docked views (customize to your app)
        # Example placeholder - replace with real control/menu sequences if desired.
        # try:
        #     main.menu_select("View->Accounts")
        # except Exception:
        #     pass

        # Close the main window (simulate user close so Dispose chain runs)
        try:
            main.type_keys("%{F4}")  # Alt+F4
        except Exception:
            proc.terminate()

        proc.wait(timeout=CLOSE_TIMEOUT)
    finally:
        try:
            proc.kill()
        except Exception:
            pass
        out_f.close()
        err_f.close()

        # Copy Serilog log files modified during the run to artifacts for post-mortem
        copy_logs_to_artifact(artifact_dir, start_ts)

    logs = tail_logs_since(start_ts)
    try:
        assert_no_dispose_exceptions(logs)
    except AssertionError:
        # Preserve logs and make failure easier to inspect
        raise
