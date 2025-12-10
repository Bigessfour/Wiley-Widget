#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Start MCP Servers for Continue.dev (e.g., syncfusion-docs, grok-agent-tools)

Verifies venv, starts servers via subprocess. For Wiley Widget: Syncfusion docs + Grok agentic.

Verification:
1. Lint: pylint scripts/tools/start-mcp-servers.py
2. Dry-Run: python scripts/tools/start-mcp-servers.py --dry-run
3. Test: Run full; check logs for 'Running' state.
4. Confirm: No -32000 errors; servers connect in Continue.dev.
"""

import argparse
import subprocess
import sys
from pathlib import Path
from typing import List


def run_cmd(commands: List[str], dry_run: bool = False) -> None:
    if dry_run:
        print(f"[DRY-RUN] Would run: {' '.join(commands)}")
        return
    try:
        result = subprocess.run(commands, check=True, capture_output=True, text=True)
        print(result.stdout)
    except subprocess.CalledProcessError as e:
        print(f"Error: {e.stderr}")
        sys.exit(1)


def start_servers(dry_run: bool = False) -> None:
    venv_path = Path(".continue/venv")
    if not venv_path.exists():
        print("Venv missing – run fix-continue-venv.py first.")
        sys.exit(1)

    venv_python = (
        venv_path / "Scripts" / "python.exe"
        if Path.cwd().drive
        else venv_path / "bin" / "python"
    )

    # Start syncfusion-docs (assume MCP command; adjust if custom)
    sync_cmd = [
        str(venv_python),
        "-m",
        "mcp_server_fetch",
        "--server",
        "syncfusion-docs",
    ]  # Placeholder; use actual entrypoint
    run_cmd(sync_cmd, dry_run=dry_run)

    # Start syncfusion-docs-node (Node.js? Assume npm if mixed)
    node_cmd = [
        "npm",
        "start",
        "--",
        "syncfusion-docs-node",
    ]  # If Node-based; check .continue/mcpServers/
    run_cmd(node_cmd, dry_run=dry_run)

    # Grok agent tools server (from YAML)
    grok_cmd = [str(venv_python), "-m", "grok_agent_tools", "start"]  # Assume module
    run_cmd(grok_cmd, dry_run=dry_run)

    print("MCP servers started. Check Continue.dev logs.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    start_servers(dry_run=args.dry_run)
