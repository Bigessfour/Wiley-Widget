#!/usr/bin/env python3
"""
Wiley Widget Manifest Generator v3.14 Lite
Fast, minimal manifest with only fetch URLs and git info.
Converted/validated for Python 3.14 — Tagged: v3.14
"""
from pathlib import Path
from datetime import datetime, timedelta
import json
import subprocess


def git(cmd):
    try:
        return (
            subprocess.check_output(
                ["git"] + cmd,
                cwd=Path("."),
                stderr=subprocess.DEVNULL,
            )
            .decode()
            .strip()
        )
    except Exception:
        return None


remote = git(["config", "--get", "remote.origin.url"]) or ""
parts = (
    remote.replace("git@github.com:", "https://github.com/")
    .replace(".git", "")
    .split("/")
)
owner_repo = "/".join(parts[-2:]) if len(parts) >= 2 else None
commit = git(["rev-parse", "HEAD"])
branch = git(["rev-parse", "--abbrev-ref", "HEAD"]) or "main"
ref = commit or branch

# build a fetch base (short string) and keep it under sensible line lengths
if owner_repo:
    fetch_base = f"https://raw.githubusercontent.com/{owner_repo}/{ref}/"
else:
    fetch_base = None

manifest = {
    "$schema": (
        "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget"
        "/main/schemas/ai-fetchable-manifest.json"
    ),
    "generator": "Wiley Widget Manifest Generator v3.14 Lite",
    "repository": {
        "owner_repo": owner_repo,
        "branch": branch,
        "commit_hash": commit,
        "remote_url": remote,
        "fetch_base": fetch_base,
    },
    "generated_at": datetime.utcnow().isoformat() + "Z",
    "valid_until": (datetime.utcnow() + timedelta(days=7)).isoformat() + "Z",
    "note": "Ultra-fast manifest v3.14 Lite — just the essentials."
}

Path("ai-fetchable-manifest.json").write_text(json.dumps(manifest, indent=2))
print("ai-fetchable-manifest.json (v3.14 Lite) generated")
