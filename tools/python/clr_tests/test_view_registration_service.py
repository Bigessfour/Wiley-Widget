"""Validate ViewRegistrationService integration with Prism."""

from __future__ import annotations

import unittest

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

# Import CLR types only if available
if HAS_PYTHONNET:
    from System import ArgumentException  # type: ignore[attr-defined]
    from System import (  # type: ignore[attr-defined]
        Activator,
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


@unittest.skipUnless(HAS_PYTHONNET, "pythonnet required for CLR tests")
@unittest.skipUnless(HAS_PRISM, "Prism assemblies required for Prism tests")
class TestViewRegistrationService(unittest.TestCase):

    def _get_assemblies_dir(self):
        """Get the assemblies directory."""
        from pathlib import Path

        repo_root = Path(__file__).resolve().parents[3]
        return repo_root / "tools" / "python" / "clr_tests" / "assemblies"

    def _clr_loader(self, name: str):
        """Add CLR assembly reference."""
        import clr

        assemblies_dir = self._get_assemblies_dir()
        assembly_path = assemblies_dir / f"{name}.dll"
        if assembly_path.exists():
            clr.AddReference(str(assembly_path))
        else:
            clr.AddReference(name)

    def _create_view_service(self):
        """Create view service instance."""
        # Load Prism.Wpf
        self._clr_loader("Prism.Wpf")

        region_manager = RegionManager()
        assemblies_dir = self._get_assemblies_dir()
        service_type = dotnet_utils.get_type(
            assemblies_dir,
            "WileyWidget",
            "WileyWidget.Services.ViewRegistrationService",
        )
        service = Activator.CreateInstance(
            service_type, Array[Object]([region_manager])
        )
        return service, region_manager

    def _add_region(self, region_manager, name: str):
        region = Region()
        region.Name = name
        region_manager.Regions.Add(region)

    def _dashboard_view(self, assemblies_dir):
        return dotnet_utils.get_type(
            assemblies_dir, "WileyWidget", "WileyWidget.Views.DashboardView"
        )

    def _settings_view(self, assemblies_dir):
        return dotnet_utils.get_type(
            assemblies_dir, "WileyWidget", "WileyWidget.Views.SettingsView"
        )

    def test_register_single_region(self):
        service, region_manager = self._create_view_service()
        self._add_region(region_manager, "DashboardRegion")

        assemblies_dir = self._get_assemblies_dir()
        dashboard_view = self._dashboard_view(assemblies_dir)
        self.assertTrue(service.RegisterView("DashboardRegion", dashboard_view))
        self.assertTrue(service.IsViewRegistered("DashboardView"))

    def test_register_multiple_views(self):
        service, region_manager = self._create_view_service()
        for name in ("DashboardRegion", "SettingsRegion"):
            self._add_region(region_manager, name)

        assemblies_dir = self._get_assemblies_dir()
        dashboard_view = self._dashboard_view(assemblies_dir)
        settings_view = self._settings_view(assemblies_dir)

        self.assertTrue(service.RegisterView("DashboardRegion", dashboard_view))
        self.assertTrue(service.RegisterView("SettingsRegion", settings_view))

        dashboard_views = list(service.GetRegisteredViews("DashboardRegion"))
        self.assertTrue(dashboard_views and dashboard_views[0].Name == "DashboardView")

        result = service.ValidateRegions()
        self.assertGreaterEqual(result.TotalRegions, 1)
        self.assertTrue(
            "DashboardRegion" in result.ValidRegions
            or "DashboardRegion" in result.MissingRegions
        )

    def test_register_invalid_region(self):
        service, _ = self._create_view_service()
        assemblies_dir = self._get_assemblies_dir()
        dashboard_view = self._dashboard_view(assemblies_dir)

        with self.assertRaises(ArgumentException):
            service.RegisterView("", dashboard_view)
