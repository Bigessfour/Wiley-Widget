"""Shared pytest configuration for CLR integration tests."""

from __future__ import annotations

import os
import sys
from collections.abc import Callable
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[3]
PYTHON_ROOT = REPO_ROOT / "tools" / "python"
ASSEMBLIES_DIR = PYTHON_ROOT / "clr_tests" / "assemblies"
WPF_BIN_DIR = REPO_ROOT / "bin" / "Debug" / "net9.0-windows"

# Ensure Python can import repository modules if needed.
for path in {str(REPO_ROOT), str(PYTHON_ROOT), str(WPF_BIN_DIR)}:
    if path and path not in sys.path:
        sys.path.insert(0, path)


def _has_pythonnet() -> bool:
    """Check if pythonnet is available."""
    try:
        import clr  # type: ignore[import-not-found] # noqa: F401
        return True
    except (ImportError, RuntimeError, AttributeError):
        return False


def _has_prism() -> bool:
    """Check if Prism assemblies are available via pythonnet."""
    if not _has_pythonnet():
        return False
    try:
        from Prism.Regions import Region  # type: ignore[attr-defined] # noqa: F401
        return True
    except Exception:
        return False


HAS_PYTHONNET = _has_pythonnet()
HAS_PRISM = _has_prism()


def _ensure_pythonnet():
    """Ensure pythonnet is available or skip tests."""
    try:
        import clr  # type: ignore[import-not-found]
    except (ImportError, RuntimeError, AttributeError) as exc:  # pragma: no cover - infrastructure guard
        pytest.skip(f"pythonnet is required for CLR integration: {exc}")
    return clr  # type: ignore[return-value]


def pytest_configure(config):
    """Register custom pytest markers for test categorization."""
    config.addinivalue_line("markers", "clr: Tests requiring pythonnet/CLR runtime")
    config.addinivalue_line("markers", "integration: Integration tests requiring built .NET assemblies")
    config.addinivalue_line("markers", "prism: Tests requiring Prism WPF assemblies")
    config.addinivalue_line("markers", "slow: Slow-running tests (>5 seconds)")


@pytest.fixture(scope="session", autouse=True)
def validate_test_environment():
    """Validate and report on CLR test environment status."""
    issues = []

    if not HAS_PYTHONNET:
        issues.append("⚠️  pythonnet not installed (pip install pythonnet)")

    if not ASSEMBLIES_DIR.exists():
        issues.append(f"⚠️  Assemblies not found at {ASSEMBLIES_DIR} (run: dotnet build)")

    if issues and HAS_PYTHONNET:
        # Only report if we have pythonnet but missing assemblies
        import warnings
        warnings.warn("\n".join(["CLR test environment status:"] + issues), stacklevel=2)


@pytest.fixture(scope="session")
def clr_loader() -> Callable[[str], None]:
    """Provide a helper that adds CLR assembly references on demand."""

    clr = _ensure_pythonnet()

    # Ensure dependent DLLs are discoverable when pythonnet loads assemblies.
    existing_path = os.environ.get("PATH", "")
    probe_paths = [str(WPF_BIN_DIR)]
    for probe in probe_paths:
        if probe and probe not in existing_path:
            existing_path = f"{probe};{existing_path}" if existing_path else probe
    os.environ["PATH"] = existing_path

    def _add_reference(name: str) -> None:
        assembly_path = ASSEMBLIES_DIR / f"{name}.dll"
        if assembly_path.exists():
            clr.AddReference(str(assembly_path))
        else:
            clr.AddReference(name)

    return _add_reference


@pytest.fixture(scope="session")
def ensure_assemblies_present() -> Path:
    """Verify that the compiled .NET assemblies required for testing exist."""
    if not ASSEMBLIES_DIR.exists():
        pytest.skip(
            "Compiled assemblies not found in tools/python/tests/assemblies/. "
            "Run the Debug build step before executing Python tests."
        )
    return ASSEMBLIES_DIR


@pytest.fixture(scope="session")
def load_wileywidget_core(clr_loader: Callable[[str], None], ensure_assemblies_present: Path) -> None:
    """Load the core WileyWidget assemblies into the CLR once per test run."""
    for assembly in ("WileyWidget", "WileyWidget.Business", "WileyWidget.Data", "WileyWidget.Models"):
        clr_loader(assembly)


@pytest.fixture(scope="session")
def system_runtime(clr_loader: Callable[[str], None]) -> None:
    """Load common framework assemblies leveraged by integration tests."""
    for assembly in (
        "System",
        "System.Core",
        "System.Runtime",
        "System.Collections",
        "System.ComponentModel.Primitives",
    ):
        try:
            clr_loader(assembly)
        except Exception:  # pragma: no cover - optional on some runtimes
            continue
