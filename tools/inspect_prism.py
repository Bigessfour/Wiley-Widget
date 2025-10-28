"""
inspect_prism.py

Python helper to discover Prism assemblies in the user's NuGet package cache
and inspect their metadata for exported types and XmlnsDefinition/XmlnsPrefix
custom attributes. Uses the `dnfile` package to parse .NET metadata without
loading assemblies into the current process.

This script is safe to run from a normal Windows developer shell. It will
try to install `dnfile` into the active Python environment if it's missing.

Usage:
    python tools/inspect_prism.py

Output: JSON printed to stdout with keys: prism_core, prism_wpf, types, attributes
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional


def ensure_dnfile() -> None:
    try:
        return
    except Exception:
        print(
            "dnfile not found; attempting to install via pip into current environment...",
            file=sys.stderr,
        )
        subprocess.check_call([sys.executable, "-m", "pip", "install", "dnfile"])


def find_nuget_package(package_name: str) -> Optional[Path]:
    # Look under the user's NuGet package cache
    home = Path(os.environ.get("USERPROFILE") or os.environ.get("HOME") or ".")
    base = home / ".nuget" / "packages"
    if not base.exists():
        return None
    pkg_dir = base / package_name
    if not pkg_dir.exists():
        # try lower-case
        alt = base / package_name.lower()
        if alt.exists():
            pkg_dir = alt
        else:
            return None
    # choose highest version directory by name
    versions = [p for p in pkg_dir.iterdir() if p.is_dir()]
    if not versions:
        return None
    versions.sort(key=lambda p: p.name, reverse=True)
    return versions[0]


def find_assembly_in_package(pkg_path: Path, assembly_name: str) -> Optional[Path]:
    # search for lib/**/assembly_name
    for lib in pkg_path.rglob(assembly_name):
        if lib.is_file():
            return lib
    return None


def inspect_assembly(path: Path) -> Dict:
    import dnfile  # type: ignore

    pe = dnfile.dnPE(str(path))
    types: List[str] = []
    attrs: List[str] = []

    # Collect type names (TypeDef table)
    try:
        metadata = pe.net.mdtables
        if hasattr(metadata, "TypeDef"):
            for row in metadata.TypeDef:
                try:
                    ns = getattr(row, "TypeNamespace", None)
                    name = getattr(row, "TypeName", None)
                    if ns:
                        types.append(f"{ns}.{name}")
                    else:
                        types.append(str(name))
                except Exception:
                    continue
    except Exception:
        pass

    # Collect custom attribute type names and fixed arg blobs (best-effort)
    try:
        if hasattr(metadata, "CustomAttribute"):
            for ca in metadata.CustomAttribute:
                try:
                    # ca.Type will be a coded token; convert to string if possible
                    typename = getattr(ca.Type, "TypeName", None)
                    if not typename:
                        typename = str(ca.Type)
                    attrs.append(str(typename))
                except Exception:
                    attrs.append(str(ca))
    except Exception:
        pass

    return {
        "path": str(path),
        "types": types,
        "custom_attributes": attrs,
    }


def main() -> int:
    ensure_dnfile()
    from pathlib import Path

    result = {"prism_core": None, "prism_wpf": None, "inspections": {}}

    core_pkg = find_nuget_package("prism.core")
    wpf_pkg = find_nuget_package("prism.wpf")

    if core_pkg:
        core_assembly = find_assembly_in_package(core_pkg, "Prism.dll")
        if core_assembly:
            result["prism_core"] = str(core_assembly)
            result["inspections"]["prism_core"] = inspect_assembly(core_assembly)
    if wpf_pkg:
        wpf_assembly = find_assembly_in_package(wpf_pkg, "Prism.Wpf.dll")
        if wpf_assembly:
            result["prism_wpf"] = str(wpf_assembly)
            result["inspections"]["prism_wpf"] = inspect_assembly(wpf_assembly)

    # If nothing found, list the nuget packages directory to help debugging
    if not core_pkg and not wpf_pkg:
        home = Path(os.environ.get("USERPROFILE") or os.environ.get("HOME") or ".")
        base = home / ".nuget" / "packages"
        entries = []
        if base.exists():
            for p in sorted(base.iterdir()):
                if p.is_dir():
                    entries.append(p.name)
        result["nuget_packages"] = entries

    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
