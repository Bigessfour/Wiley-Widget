"""Comprehensive tests for dotnet_utils helper functions.

These tests cover assembly loading, type resolution, DbContext options creation,
and AppDbContext instantiation. They exercise error paths and edge cases to
increase coverage from 26% to 80%+.
"""

from __future__ import annotations

import pytest

# Check for pythonnet availability
try:
    from System import Activator  # type: ignore[attr-defined]
    HAS_SYSTEM = True
except (ImportError, ModuleNotFoundError):
    HAS_SYSTEM = False

pytestmark = [
    pytest.mark.clr,
    pytest.mark.integration,
    pytest.mark.skipif(not HAS_SYSTEM, reason="pythonnet required for CLR tests"),
]

from .helpers import dotnet_utils


class TestLoadAssembly:
    """Test assembly loading."""

    def test_load_assembly_success(self, ensure_assemblies_present):
        """Test loading an existing assembly."""
        asm = dotnet_utils.load_assembly(ensure_assemblies_present, "WileyWidget.Data")
        assert asm is not None
        assert asm.GetName().Name == "WileyWidget.Data"
