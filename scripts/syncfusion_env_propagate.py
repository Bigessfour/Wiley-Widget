#!/usr/bin/env python
"""
Syncfusion license environment propagation utility.

Purpose:
  - Read machine/user level Syncfusion license key (if accessible through
    a persisted file convention or environment variable)
  - Export into a process-level environment (.env file update) WITHOUT
    printing the full key (only prefix for diagnostics)
  - Validate format heuristically (length / placeholder detection)
  - Provide exit codes: 0=success, 2=key not found, 3=appears placeholder, 4=error

This script intentionally avoids any non-documented Syncfusion APIs; it only
manages environment setup. The actual registration MUST remain in App.xaml.cs
using Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("<key>").

Usage:
  python scripts/syncfusion_env_propagate.py

Outputs:
  - Updates or appends SYNCFUSION_LICENSE_KEY line in .env (project root)
  - Prints safe diagnostic summary
"""
from __future__ import annotations

import os
import platform
import re
import sys
from pathlib import Path

try:
    import winreg  # Windows only; guarded below
except Exception:  # pragma: no cover
    winreg = None

PROJECT_ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = PROJECT_ROOT / ".env"

PLACEHOLDER_PATTERNS = [
    "YOUR_SYNCFUSION_LICENSE_KEY_HERE",
    "INSERT",
    "PLACEHOLDER",
]


def _get_registry_env(scope: str, name: str) -> str | None:
    """Read environment variable from Windows registry directly (live), bypassing inherited snapshot.

    scope: 'user' or 'machine'
    Returns None on non-Windows or missing value.
    """
    if platform.system() != "Windows" or winreg is None:
        return None
    try:
        if scope == "user":
            root = winreg.HKEY_CURRENT_USER
            sub = r"Environment"
        else:  # machine
            root = winreg.HKEY_LOCAL_MACHINE
            sub = r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
        with winreg.OpenKey(root, sub) as k:
            val, _ = winreg.QueryValueEx(k, name)
            if isinstance(val, str) and val.strip():
                return val.strip()
    except FileNotFoundError:
        return None
    except Exception:
        return None
    return None


def _load_candidate_sources():
    sources: dict[str, str] = {}

    # Machine-level (authoritative) first
    machine_val = _get_registry_env("machine", "SYNCFUSION_LICENSE_KEY")
    if machine_val:
        sources["env_machine"] = machine_val

    # User-level
    user_val = _get_registry_env("user", "SYNCFUSION_LICENSE_KEY")
    if user_val and user_val != machine_val:
        sources["env_user"] = user_val

    # Process env (may be stale compared to registry if set after shell launch)
    proc_val = os.environ.get("SYNCFUSION_LICENSE_KEY")
    if proc_val and proc_val not in (machine_val, user_val):
        sources["env_process"] = proc_val.strip()

    # license.key file if present (lowest priority now)
    key_file = PROJECT_ROOT / "license.key"
    if key_file.exists():
        try:
            content = key_file.read_text(encoding="utf-8").splitlines()
            for line in reversed(content):
                line = line.strip()
                if line and not line.startswith("#"):
                    if line not in sources.values():
                        sources["license.key"] = line
                    break
        except Exception:
            pass
    return sources


def _select_key(sources: dict[str, str]) -> tuple[str | None, str | None]:
    # New priority: env_machine > env_user > env_process > license.key
    for k in ("env_machine", "env_user", "env_process", "license.key"):
        if k in sources:
            return sources[k], k
    return None, None


def _is_placeholder(k: str) -> bool:
    u = k.upper()
    return any(p in u for p in PLACEHOLDER_PATTERNS)


def _is_plausible(k: str) -> bool:
    return len(k) > 40 and "=" in k and "@" in k


def update_env_file(key: str):
    lines = []
    if ENV_FILE.exists():
        lines = ENV_FILE.read_text(encoding="utf-8").splitlines()
    updated = False
    new_lines = []
    for line in lines:
        if line.startswith("SYNCFUSION_LICENSE_KEY="):
            new_lines.append(f"SYNCFUSION_LICENSE_KEY={key}")
            updated = True
        else:
            new_lines.append(line)
    if not updated:
        new_lines.append(f"SYNCFUSION_LICENSE_KEY={key}")
    ENV_FILE.write_text("\n".join(new_lines) + "\n", encoding="utf-8")


def main():
    sources = _load_candidate_sources()
    if not sources:
        print("⚠️  No Syncfusion license key sources found (env or license.key).")
        return 2
    key, origin = _select_key(sources)
    if not key:
        print("⚠️  No selectable Syncfusion license key.")
        return 2
    if _is_placeholder(key):
        print(f"❌ Placeholder key detected from {origin}. Aborting propagation.")
        return 3
    if not _is_plausible(key):
        print(
            f"⚠️  Key from {origin} does not meet plausibility heuristics (length/pattern)."
        )
    update_env_file(key)
    safe_prefix = key[:8]
    print(
        f"✅ Propagated Syncfusion license key from {origin}. Prefix={safe_prefix}..., length={len(key)}"
    )
    if origin != "env_machine" and "env_machine" in sources:
        print(
            "ℹ️  Machine-level key also exists but a different source was selected (precedence logic)."
        )
    elif origin == "env_machine":
        print("🔒 Using authoritative machine-level license source.")
    print(f"📄 .env updated at {ENV_FILE}")
    return 0


if __name__ == "__main__":
    try:
        rc = main()
        sys.exit(rc)
    except Exception as e:
        print(f"❌ Unexpected error: {e}")
        sys.exit(4)
