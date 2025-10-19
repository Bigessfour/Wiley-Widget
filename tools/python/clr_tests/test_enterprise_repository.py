"""Tests for EnterpriseRepository via pythonnet."""

from __future__ import annotations

import pytest

# Conditional imports for CLR types
try:
    from System import Activator, Array, Object  # type: ignore[attr-defined]
    from System.Reflection import Assembly  # type: ignore[attr-defined]
    HAS_SYSTEM = True
except (ImportError, ModuleNotFoundError):
    HAS_SYSTEM = False

from .helpers import dotnet_utils

pytestmark = [
    pytest.mark.clr,
    pytest.mark.integration,
    pytest.mark.skipif(not HAS_SYSTEM, reason="pythonnet required for CLR tests"),
]


def _await(task):
    return task.GetAwaiter().GetResult()


def _create_repository(assemblies_dir, database_name: str | None = None):
    app_db_context_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Data", "AppDbContext")
    options, db_name = dotnet_utils.create_inmemory_options(assemblies_dir, app_db_context_type, database_name)

    factory_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Data", "WileyWidget.Data.UnityAppDbContextFactory")
    factory = Activator.CreateInstance(factory_type, Array[Object]([options]))

    repo_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Data", "WileyWidget.Data.EnterpriseRepository")

    logging = Assembly.Load("Microsoft.Extensions.Logging.Abstractions")
    null_factory_type = logging.GetType("Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory")
    null_factory = null_factory_type.GetProperty("Instance").GetValue(None, None)
    logger = null_factory.CreateLogger(repo_type.FullName)

    repository = Activator.CreateInstance(repo_type, Array[Object]([factory, logger]))
    return repository, factory, repo_type, db_name


@pytest.fixture()
def enterprise_repo(clr_loader, ensure_assemblies_present):
    clr_loader("Microsoft.EntityFrameworkCore")
    clr_loader("Microsoft.EntityFrameworkCore.InMemory")
    clr_loader("Microsoft.Extensions.Logging.Abstractions")

    repository, factory, repo_type, db_name = _create_repository(ensure_assemblies_present)
    yield repository, factory, repo_type, db_name


def _seed_enterprise(factory, assemblies_dir, enterprise_id: int = 1, name: str = "Water Utility"):
    enterprise_type = dotnet_utils.get_type(assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.Enterprise")
    context = factory.CreateDbContext()
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


def test_get_all_empty(enterprise_repo, ensure_assemblies_present):
    repository, _, _, _ = enterprise_repo
    result = list(_await(repository.GetAllAsync()))
    assert result == []


def test_get_all_with_item(enterprise_repo, ensure_assemblies_present):
    repository, factory, _, _ = enterprise_repo
    _seed_enterprise(factory, ensure_assemblies_present)

    items = list(_await(repository.GetAllAsync()))
    assert len(items) == 1
    assert items[0].Name == "Water Utility"


def test_add_enterprise_success(enterprise_repo, ensure_assemblies_present):
    repository, _, _, _ = enterprise_repo
    enterprise_type = dotnet_utils.get_type(ensure_assemblies_present, "WileyWidget.Models", "WileyWidget.Models.Enterprise")
    entity = Activator.CreateInstance(enterprise_type)
    entity.Name = "Sanitation"
    entity.CitizenCount = 800
    entity.CurrentRate = 20.0
    entity.MonthlyExpenses = 4000

    saved = _await(repository.AddAsync(entity))
    assert saved.Name == "Sanitation"


def test_add_enterprise_invalid_input_raises(enterprise_repo):
    repository, _, _, _ = enterprise_repo
    with pytest.raises(TypeError):
        _await(repository.AddAsync(None))
