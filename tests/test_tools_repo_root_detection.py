import importlib.util
from pathlib import Path


def load_module_from_path(p: Path):
    spec = importlib.util.spec_from_file_location(p.stem, str(p))
    if spec is None:
        raise ImportError(f"Cannot create module spec for {p}")
    mod = importlib.util.module_from_spec(spec)
    loader = spec.loader
    if loader is None:
        raise ImportError(f"No loader available for module spec of {p}")
    # explicitly narrow types for static checkers and call the loader safely
    loader.exec_module(mod)  # type: ignore[arg-type]
    return mod


def assert_is_repo_root(path: Path):
    assert path.exists(), f"Path {path} does not exist"
    # heuristics: repo root should contain 'sql' folder or the solution file
    assert (
        (path / "sql").exists()
        or (path / "WileyWidget.sln").exists()
        or (path / "README.md").exists()
    )


def test_fix_seed_enums_detects_root():
    p = Path(__file__).resolve().parents[1] / "scripts" / "tools" / "fix_seed_enums.py"
    mod = load_module_from_path(p)
    # function should exist
    assert hasattr(mod, "find_repo_root")
    root = mod.find_repo_root(Path(p))
    assert_is_repo_root(root)


def test_sql_enum_audit_v2_detects_root():
    p = (
        Path(__file__).resolve().parents[1]
        / "scripts"
        / "tools"
        / "sql_enum_audit_v2.py"
    )
    mod = load_module_from_path(p)
    assert hasattr(mod, "find_repo_root")
    root = mod.find_repo_root(Path(p))
    assert_is_repo_root(root)


def test_sql_enum_audit_detects_root():
    p = Path(__file__).resolve().parents[1] / "scripts" / "tools" / "sql_enum_audit.py"
    mod = load_module_from_path(p)
    assert hasattr(mod, "find_repo_root")
    root = mod.find_repo_root(Path(p))
    assert_is_repo_root(root)
