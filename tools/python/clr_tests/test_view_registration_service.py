"""Validate ViewRegistrationService integration with Prism."""

from __future__ import annotations

import pytest

# Check for pythonnet and Prism availability
try:
    import clr  # type: ignore[import-not-found]
    HAS_PYTHONNET = True
    try:
        from Prism.Regions import Region, RegionManager  # type: ignore[attr-defined]
        HAS_PRISM = True
    except Exception:
        HAS_PRISM = False
except (ImportError, RuntimeError, AttributeError):
    HAS_PYTHONNET = False
    HAS_PRISM = False

pytestmark = [
    pytest.mark.clr,
    pytest.mark.prism,
    pytest.mark.skipif(not HAS_PYTHONNET, reason="pythonnet required for CLR tests"),
    pytest.mark.skipif(not HAS_PRISM, reason="Prism assemblies required for Prism tests"),
]

# Import CLR types only if available
if HAS_PYTHONNET:
    from System import (  # type: ignore[attr-defined]
        Activator,
        ArgumentException,  # type: ignore[attr-defined]
        Array,
        Object,
    )
else:
    Activator = None
    ArgumentException = None
    Array = None
    Object = None

if HAS_PRISM:
    from Prism.Regions import Region, RegionManager  # type: ignore[attr-defined]
else:
    Region = None
    RegionManager = None

from .helpers import dotnet_utils


@pytest.fixture()
def view_service(clr_loader, ensure_assemblies_present, load_wileywidget_core):
    clr_loader("Prism.Wpf")
    region_manager = RegionManager()
    service_type = dotnet_utils.get_type(ensure_assemblies_present, "WileyWidget", "WileyWidget.Services.ViewRegistrationService")
    service = Activator.CreateInstance(service_type, Array[Object]([region_manager]))
    return service, region_manager


def _add_region(region_manager, name: str):
    region = Region()
    region.Name = name
    region_manager.Regions.Add(region)


def _dashboard_view(assemblies_dir):
    return dotnet_utils.get_type(assemblies_dir, "WileyWidget", "WileyWidget.Views.DashboardView")


def _settings_view(assemblies_dir):
    return dotnet_utils.get_type(assemblies_dir, "WileyWidget", "WileyWidget.Views.SettingsView")


def test_register_single_region(view_service, ensure_assemblies_present):
    service, region_manager = view_service
    _add_region(region_manager, "DashboardRegion")

    dashboard_view = _dashboard_view(ensure_assemblies_present)
    assert service.RegisterView("DashboardRegion", dashboard_view)
    assert service.IsViewRegistered("DashboardView")


def test_register_multiple_views(view_service, ensure_assemblies_present):
    service, region_manager = view_service
    for name in ("DashboardRegion", "SettingsRegion"):
        _add_region(region_manager, name)

    dashboard_view = _dashboard_view(ensure_assemblies_present)
    settings_view = _settings_view(ensure_assemblies_present)

    assert service.RegisterView("DashboardRegion", dashboard_view)
    assert service.RegisterView("SettingsRegion", settings_view)

    dashboard_views = list(service.GetRegisteredViews("DashboardRegion"))
    assert dashboard_views and dashboard_views[0].Name == "DashboardView"

    result = service.ValidateRegions()
    assert result.TotalRegions >= 1
    assert "DashboardRegion" in result.ValidRegions or "DashboardRegion" in result.MissingRegions


def test_register_invalid_region(view_service, ensure_assemblies_present):
    service, _ = view_service
    dashboard_view = _dashboard_view(ensure_assemblies_present)

    with pytest.raises(ArgumentException):
        service.RegisterView("", dashboard_view)
