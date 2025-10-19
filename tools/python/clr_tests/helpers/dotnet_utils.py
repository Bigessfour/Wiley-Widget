"""Utilities for creating .NET instances via pythonnet."""

from __future__ import annotations

import uuid

# Conditional imports for CLR types
try:
    from System import Activator, Array, Object, Type  # type: ignore[attr-defined]
    from System.Reflection import Assembly, BindingFlags  # type: ignore[attr-defined]
    HAS_SYSTEM = True
except (ImportError, ModuleNotFoundError):
    HAS_SYSTEM = False


def load_assembly(assemblies_dir, name):
    """Load an assembly by name from the test assemblies folder."""
    if not HAS_SYSTEM:
        raise RuntimeError("CLR not available")
    return Assembly.LoadFrom(str(assemblies_dir / f"{name}.dll"))


def get_type(assemblies_dir, assembly_name: str, type_name: str):
    """Return a System.Type from the given assembly."""
    if not HAS_SYSTEM:
        raise RuntimeError("CLR not available")
    assembly = load_assembly(assemblies_dir, assembly_name)
    target = assembly.GetType(type_name)
    if target is None:
        raise ValueError(f"Type '{type_name}' not found in {assembly_name}.")
    return target


def create_inmemory_options(assemblies_dir, app_db_context_type, database_name: str | None = None):
    """Create DbContextOptions<AppDbContext> configured for EF InMemory."""
    if not HAS_SYSTEM:
        raise RuntimeError("CLR not available")
    db_name = database_name or f"pytests-{uuid.uuid4()}"

    ef_core = Assembly.Load("Microsoft.EntityFrameworkCore")
    builder_generic = ef_core.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1")
    builder_type = builder_generic.MakeGenericType(Array[Type]([app_db_context_type]))
    builder = Activator.CreateInstance(builder_type)

    in_memory = Assembly.Load("Microsoft.EntityFrameworkCore.InMemory")
    extensions = in_memory.GetType("Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions")
    candidate_methods = [
        method
        for method in extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
        if method.Name == "UseInMemoryDatabase"
    ]
    # Prefer overload accepting builder, string, action
    use_in_memory = max(candidate_methods, key=lambda method: method.GetParameters().Length)
    parameters = use_in_memory.GetParameters()
    args = [builder, db_name]
    if parameters.Length >= 3:
        args.append(None)
    use_in_memory.Invoke(None, Array[Object](args))

    return builder_type.GetProperty("Options").GetValue(builder, None), db_name


def create_app_db_context(assemblies_dir, database_name: str | None = None) -> tuple[object, object, str]:
    """Instantiate AppDbContext configured with an in-memory provider."""
    if not HAS_SYSTEM:
        raise RuntimeError("CLR not available")
    data_type = get_type(assemblies_dir, "WileyWidget.Data", "AppDbContext")
    options, db_name = create_inmemory_options(assemblies_dir, data_type, database_name)
    context = Activator.CreateInstance(data_type, Array[Object]([options]))
    return context, data_type, db_name
