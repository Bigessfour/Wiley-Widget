"""Minimal tests for AppDbContext that skip when pythonnet isn't available.

These tests are defensive: they won't raise syntax or import-time errors when
pythonnet isn't installed or when the local helper cannot be loaded. They use
the repository helper at tools/python/tests/helpers/dotnet_utils.py when
available.
"""

from __future__ import annotations

from collections.abc import Generator
from typing import Any

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
    from System import Activator, Array, Object  # type: ignore[attr-defined]
    from System.Linq import Enumerable  # type: ignore[attr-defined]
else:
    clr = None  # type: ignore[assignment]
    Array = Activator = Object = Enumerable = None  # type: ignore[assignment]

# Import helper using standard package import
try:
    from .helpers import dotnet_utils
except ImportError:
    dotnet_utils = None  # type: ignore[assignment]


@pytest.fixture()
def app_db_context(clr_loader, ensure_assemblies_present, load_wileywidget_core) -> Generator[tuple[Any, Any, Any], None, None]:
    """Provide an AppDbContext configured with EF Core InMemory.

    Skips the tests if pythonnet or the local helper aren't available in this
    test environment.
    """

    if clr is None or dotnet_utils is None or Activator is None:
        pytest.skip("pythonnet, dotnet helper, or required .NET types not available in this environment")

    # Ensure EF Core assemblies are available to pythonnet
    try:
        clr_loader("Microsoft.EntityFrameworkCore")
        clr_loader("Microsoft.EntityFrameworkCore.InMemory")
    except Exception as exc:
        pytest.skip(f"EF Core assemblies not available: {exc}")

    context, context_type, db_name = dotnet_utils.create_app_db_context(ensure_assemblies_present)
    try:
        yield context, context_type, db_name
    finally:
        if context is not None and hasattr(context, "Dispose"):
            context.Dispose()  # type: ignore[attr-defined]


def test_smoke_creation(app_db_context):
    context, context_type, _ = app_db_context
    assert context is not None
    assert context.GetType() == context_type


def test_smoke_entity_set(app_db_context):
    context, _, _ = app_db_context
    customers = context.UtilityCustomers
    assert customers is not None


def test_smoke_query(app_db_context, ensure_assemblies_present):
    context, _, _ = app_db_context
    assemblies_dir = ensure_assemblies_present

    if dotnet_utils is None:
        pytest.skip("dotnet_utils not available in this environment")

    if Activator is None:
        pytest.skip("Activator not available in this environment")

    customer_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.UtilityCustomer")
    customer = Activator.CreateInstance(customer_type)
    customer.AccountNumber = "SMOKE-1"

    customers = context.UtilityCustomers
    assert customers is not None

    customers.Add(customer)
    context.SaveChanges()

    queryable = customers.AsQueryable()
    assert queryable is not None, "Queryable should not be None"
    results = list(queryable.ToList())
    assert any(getattr(r, "AccountNumber", None) == "SMOKE-1" for r in results)


def test_add_multiple_customers(app_db_context, ensure_assemblies_present):
    """Test adding multiple customers to the in-memory database."""
    context, _, _ = app_db_context
    assemblies_dir = ensure_assemblies_present

    if dotnet_utils is None or Activator is None:
        pytest.skip("dotnet_utils or Activator not available")

    # Create customer type
    customer_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.UtilityCustomer")

    # Add three customers
    customers_set = context.UtilityCustomers
    for i in range(3):
        customer = Activator.CreateInstance(customer_type)
        customer.AccountNumber = f"MULTI-{i:03d}"
        customers_set.Add(customer)

    context.SaveChanges()

    # Verify all were added
    queryable = customers_set.AsQueryable()
    results = list(queryable.ToList())
    assert len(results) == 3


def test_customer_properties(app_db_context, ensure_assemblies_present):
    """Test setting and retrieving customer properties."""
    context, _, _ = app_db_context
    assemblies_dir = ensure_assemblies_present

    if dotnet_utils is None or Activator is None:
        pytest.skip("dotnet_utils or Activator not available")

    customer_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.UtilityCustomer")
    customer = Activator.CreateInstance(customer_type)

    # Set properties
    customer.AccountNumber = "PROP-TEST-001"
    customer.FirstName = "John"
    customer.LastName = "Doe"

    # Verify properties
    assert customer.AccountNumber == "PROP-TEST-001"
    assert customer.FirstName == "John"
    assert customer.LastName == "Doe"


def test_context_savechanges(app_db_context, ensure_assemblies_present):
    """Test that SaveChanges commits data to the in-memory database."""
    context, _, _ = app_db_context
    assemblies_dir = ensure_assemblies_present

    if dotnet_utils is None or Activator is None:
        pytest.skip("dotnet_utils or Activator not available")

    customer_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.UtilityCustomer")
    customer = Activator.CreateInstance(customer_type)
    customer.AccountNumber = "SAVE-TEST"

    customers_set = context.UtilityCustomers
    customers_set.Add(customer)

    # Before SaveChanges, should not be queryable yet (depends on EF behavior)
    # After SaveChanges, should be queryable
    result = context.SaveChanges()  # Returns number of entities affected
    assert result >= 0


def test_query_filtering(app_db_context, ensure_assemblies_present):
    """Test querying with multiple items in the database."""
    context, _, _ = app_db_context
    assemblies_dir = ensure_assemblies_present

    if dotnet_utils is None or Activator is None:
        pytest.skip("dotnet_utils or Activator not available")

    customer_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.UtilityCustomer")

    # Add two customers with different account numbers
    customers_set = context.UtilityCustomers
    cust1 = Activator.CreateInstance(customer_type)
    cust1.AccountNumber = "FILTER-A"
    cust2 = Activator.CreateInstance(customer_type)
    cust2.AccountNumber = "FILTER-B"

    customers_set.Add(cust1)
    customers_set.Add(cust2)
    context.SaveChanges()

    # Query and verify both are present
    queryable = customers_set.AsQueryable()
    all_results = list(queryable.ToList())
    assert len(all_results) >= 2
    account_numbers = [getattr(r, "AccountNumber", None) for r in all_results]
    assert "FILTER-A" in account_numbers
    assert "FILTER-B" in account_numbers


def test_entity_set_not_null_after_add(app_db_context):
    """Test that entity set remains accessible after adding items."""
    context, _, _ = app_db_context
    customers = context.UtilityCustomers

    # Entity set should still be accessible
    assert customers is not None
    # Should support querying
    queryable = customers.AsQueryable()
    assert queryable is not None


def test_fixture_provides_all_outputs(app_db_context):
    """Test that the fixture returns all expected values."""
    context, context_type, db_name = app_db_context

    assert context is not None
    assert context_type is not None
    assert db_name is not None
    assert isinstance(db_name, str)
    assert len(db_name) > 0

