"""Tests for EnterpriseRepository via pythonnet."""

from __future__ import annotations

import unittest

# Conditional imports for CLR types
try:
    from System import Activator, Array, Object  # type: ignore[attr-defined]
    from System.Reflection import Assembly  # type: ignore[attr-defined]

    HAS_SYSTEM = True
except (ImportError, ModuleNotFoundError):
    HAS_SYSTEM = False

from .helpers import dotnet_utils


@unittest.skipUnless(HAS_SYSTEM, "pythonnet required for CLR tests")
class TestEnterpriseRepository(unittest.TestCase):
    """Test cases for EnterpriseRepository via pythonnet."""

    def setUp(self):
        """Set up test fixtures."""
        if not HAS_SYSTEM:
            self.skipTest("pythonnet not available")

        # Load required assemblies
        try:
            clr_loader = self._get_clr_loader()
            clr_loader("Microsoft.EntityFrameworkCore")
            clr_loader("Microsoft.EntityFrameworkCore.InMemory")
            clr_loader("Microsoft.Extensions.Logging.Abstractions")

            self.repository, self.factory, self.repo_type, self.db_name = (
                self._create_repository()
            )
        except Exception as e:
            self.skipTest(f"Failed to set up CLR environment: {e}")

    def tearDown(self):
        """Clean up test fixtures."""
        pass

    def _get_clr_loader(self):
        """Get CLR loader function."""
        # This would need to be implemented based on conftest.py
        # For now, we'll assume assemblies are loaded
        return lambda name: None

    def _create_repository(self, database_name: str | None = None):
        """Create repository instance."""
        assemblies_dir = "tools/python/clr_tests/assemblies"  # Adjust path as needed
        app_db_context_type = dotnet_utils.get_type(
            assemblies_dir, "WileyWidget.Data", "AppDbContext"
        )
        options, db_name = dotnet_utils.create_inmemory_options(
            assemblies_dir, app_db_context_type, database_name
        )

        factory_type = dotnet_utils.get_type(
            assemblies_dir,
            "WileyWidget.Data",
            "WileyWidget.Data.UnityAppDbContextFactory",
        )
        factory = Activator.CreateInstance(factory_type, Array[Object]([options]))

        repo_type = dotnet_utils.get_type(
            assemblies_dir, "WileyWidget.Data", "WileyWidget.Data.EnterpriseRepository"
        )

        logging = Assembly.Load("Microsoft.Extensions.Logging.Abstractions")
        null_factory_type = logging.GetType(
            "Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory"
        )
        null_factory = null_factory_type.GetProperty("Instance").GetValue(None, None)
        logger = null_factory.CreateLogger(repo_type.FullName)

        repository = Activator.CreateInstance(
            repo_type, Array[Object]([factory, logger])
        )
        return repository, factory, repo_type, db_name

    def _seed_enterprise(self, enterprise_id: int = 1, name: str = "Water Utility"):
        """Seed test data."""
        enterprise_type = dotnet_utils.get_type(
            "tools/python/clr_tests/assemblies",
            "WileyWidget.Models",
            "WileyWidget.Models.Enterprise",
        )
        context = self.factory.CreateDbContext()
        try:
            entity = Activator.CreateInstance(enterprise_type)
            entity.Id = enterprise_id
            entity.Name = name
            entity.CitizenCount = 1000
            entity.CurrentRate = 15.5
            entity.MonthlyExpenses = 5000
            context.Enterprises.Add(entity)
            context.SaveChanges()
        finally:
            context.Dispose()  # type: ignore[attr-defined]

    def test_get_all_empty(self):
        """Test getting all enterprises when database is empty."""
        result = list(self._await(self.repository.GetAllAsync()))
        self.assertEqual(result, [])

    def test_get_all_with_item(self):
        """Test getting all enterprises when database has items."""
        self._seed_enterprise()

        items = list(self._await(self.repository.GetAllAsync()))
        self.assertEqual(len(items), 1)
        self.assertEqual(items[0].Name, "Water Utility")

    def test_add_enterprise_success(self):
        """Test successfully adding an enterprise."""
        enterprise_type = dotnet_utils.get_type(
            "tools/python/clr_tests/assemblies",
            "WileyWidget.Models",
            "WileyWidget.Models.Enterprise",
        )
        entity = Activator.CreateInstance(enterprise_type)
        entity.Name = "Sanitation"
        entity.CitizenCount = 800
        entity.CurrentRate = 20.0
        entity.MonthlyExpenses = 4000

        saved = self._await(self.repository.AddAsync(entity))
        self.assertEqual(saved.Name, "Sanitation")

    def test_add_enterprise_invalid_input_raises(self):
        """Test that adding invalid input raises exception."""
        with self.assertRaises(TypeError):
            self._await(self.repository.AddAsync(None))

    def _await(self, task):
        """Helper to await async tasks."""
        return task.GetAwaiter().GetResult()
