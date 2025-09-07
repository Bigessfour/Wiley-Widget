#!/usr/bin/env python3
"""
Generate a fetchability resources manifest for CI/CD pipelines.

This script creates a machine-readable JSON manifest containing SHA256 hashes,
timestamps, and metadata for all Git-tracked files in the repository.
Used for file integrity verification and CI/CD pipeline automation.
"""

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Set


def get_file_sha256(file_path: str) -> Optional[str]:
    """Calculate SHA256 hash of a file."""
    try:
        hasher = hashlib.sha256()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hasher.update(chunk)
        return hasher.hexdigest()
    except Exception as e:
        print(
            f"Warning: Could not calculate hash for file: {file_path}. Error: {e}",
            file=sys.stderr,
        )
        return None


def get_git_tracked_files() -> List[str]:
    """Get list of all Git-tracked files."""
    try:
        result = subprocess.run(
            ["git", "ls-files"], capture_output=True, text=True, check=True
        )
        return [
            line.strip() for line in result.stdout.strip().split("\n") if line.strip()
        ]
    except subprocess.CalledProcessError as e:
        print(f"Error: Failed to get Git-tracked files: {e}", file=sys.stderr)
        sys.exit(1)


def get_git_info() -> Dict[str, Optional[str]]:
    """Get Git repository information."""
    git_info = {
        "commit_hash": None,
        "branch": None,
        "is_dirty": False,
        "remote_url": None,
    }

    try:
        # Get commit hash
        result = subprocess.run(
            ["git", "rev-parse", "HEAD"], capture_output=True, text=True
        )
        if result.returncode == 0:
            git_info["commit_hash"] = result.stdout.strip()

        # Get branch name
        result = subprocess.run(
            ["git", "rev-parse", "--abbrev-ref", "HEAD"],
            capture_output=True,
            text=True,
        )
        if result.returncode == 0:
            git_info["branch"] = result.stdout.strip()

        # Check if repository is dirty
        result = subprocess.run(
            ["git", "status", "--porcelain"], capture_output=True, text=True
        )
        if result.returncode == 0:
            git_info["is_dirty"] = bool(result.stdout.strip())

        # Get remote URL
        result = subprocess.run(
            ["git", "config", "--get", "remote.origin.url"],
            capture_output=True,
            text=True,
        )
        if result.returncode == 0:
            git_info["remote_url"] = result.stdout.strip()

    except Exception as e:
        print(f"Warning: Could not retrieve git information: {e}", file=sys.stderr)

    return git_info


def collect_diagnostics(include_diagnostics: bool) -> Optional[Dict]:
    """Collect runtime diagnostics from the latest structured log."""
    if not include_diagnostics:
        return None

    try:
        logs_path = Path("logs")
        if not logs_path.exists():
            return None

        # Find the newest structured log file
        structured_logs = list(logs_path.glob("structured-*.log"))
        if not structured_logs:
            return None

        latest_log = max(structured_logs, key=lambda p: p.stat().st_mtime)

        # Initialize counters
        theme_attempts = 0
        theme_successes = 0
        theme_failures = 0
        fallback_failures = 0
        fatal_count = 0
        exit_codes = []
        missing_keys = set()
        missing_from_exceptions = set()
        last_fatals = []

        # Regex for missing resource keys
        regex_missing = re.compile(r"Cannot find resource named '([^']+)'\.$")

        # Read the last 2000 lines to avoid memory issues
        with open(latest_log, "r", encoding="utf-8") as f:
            lines = f.readlines()[-2000:]

        for line in lines:
            line = line.strip()
            if not line:
                continue

            try:
                obj = json.loads(line)
            except json.JSONDecodeError:
                continue

            if not obj:
                continue

            mt = str(obj.get("MessageTemplate", ""))
            level = str(obj.get("Level", ""))

            # Count theme operations
            if "🎨 Applying" in mt and "application theme" in mt:
                theme_attempts += 1
            elif "🎨 Theme applied successfully" in mt or "✅ Syncfusion application theme applied successfully" in mt:
                theme_successes += 1
            elif "❌ Failed to apply application theme" in mt:
                theme_failures += 1
            elif "❌ All theme fallbacks failed" in mt:
                fallback_failures += 1

            # Count fatal errors
            if level == "Fatal":
                fatal_count += 1
                if len(last_fatals) < 3:
                    exception_line = ""
                    if "Exception" in obj:
                        exception_line = str(obj["Exception"]).split("\n")[0]
                    last_fatals.append({
                        "timestamp": obj.get("Timestamp"),
                        "message_template": mt,
                        "exception": exception_line,
                    })

            # Extract exit codes
            if "📊 Exit code:" in mt and "Properties" in obj:
                properties = obj.get("Properties", {})
                if "ExitCode" in properties:
                    try:
                        exit_codes.append(int(properties["ExitCode"]))
                    except (ValueError, TypeError):
                        pass

            # Extract missing resource keys
            if "⚠️ Missing static resource key:" in mt and "Properties" in obj:
                properties = obj.get("Properties", {})
                if "Key" in properties:
                    missing_keys.add(str(properties["Key"]))

            # Parse exceptions for missing resource keys
            if "Exception" in obj:
                match = regex_missing.search(str(obj["Exception"]))
                if match:
                    missing_from_exceptions.add(match.group(1))

        # Combine missing keys
        all_missing = sorted(missing_keys | missing_from_exceptions)

        # Determine health status
        if fatal_count > 0:
            health = "Unhealthy"
        elif all_missing:
            health = "Degraded"
        else:
            health = "Healthy"

        return {
            "log_file": {
                "name": latest_log.name,
                "size": latest_log.stat().st_size,
                "last_write_time_utc": datetime.fromtimestamp(
                    latest_log.stat().st_mtime
                ).isoformat() + "Z",
            },
            "startup": {
                "fatal_count": fatal_count,
                "exit_codes": exit_codes,
                "recent_fatals": last_fatals,
            },
            "theming": {
                "attempts": theme_attempts,
                "successes": theme_successes,
                "failures": theme_failures,
                "fallback_failures": fallback_failures,
            },
            "resources": {"missing_keys": all_missing},
            "health": health,
        }

    except Exception as e:
        print(f"Warning: Diagnostics collection failed: {e}", file=sys.stderr)
        return None


def generate_manifest(
    output_path: str = "fetchability-resources.json",
    include_diagnostics: bool = True,
) -> None:
    """Generate the fetchability manifest."""
    print("🔍 Scanning repository files...")

    # Ensure we're in a git repository
    if not Path(".git").exists():
        print("Error: This script must be run from the root of a Git repository.", file=sys.stderr)
        sys.exit(1)

    # Get git information
    git_info = get_git_info()

    # Get all Git-tracked files
    tracked_files = get_git_tracked_files()
    print(f"📁 Found {len(tracked_files)} tracked files to process")

    # Process each file
    file_manifest = []
    for i, file_path in enumerate(tracked_files):
        if i % 100 == 0:  # Progress indicator
            print(f"Processing {i}/{len(tracked_files)}...")

        try:
            file_info = Path(file_path).stat()

            # Calculate SHA256
            sha256 = get_file_sha256(file_path)
            if not sha256:
                print(f"Warning: Skipping file due to hash calculation failure: {file_path}")
                continue

            # Create file entry
            file_entry = {
                "path": file_path.replace("\\", "/"),  # Use forward slashes for consistency
                "sha256": sha256,
                "size": file_info.st_size,
                "last_modified": datetime.fromtimestamp(file_info.st_mtime).isoformat() + "Z",
                "extension": Path(file_path).suffix,
            }

            file_manifest.append(file_entry)

        except Exception as e:
            print(f"Warning: Error processing file '{file_path}': {e}", file=sys.stderr)

    print("Processing completed.")

    # Collect diagnostics
    diagnostics = collect_diagnostics(include_diagnostics)

    # Create manifest object
    manifest = {
        "metadata": {
            "generated_at": datetime.utcnow().isoformat() + "Z",
            "generator": "generate_fetchability_manifest.py",
            "repository": {
                "commit_hash": git_info["commit_hash"],
                "branch": git_info["branch"],
                "is_dirty": git_info["is_dirty"],
                "remote_url": git_info["remote_url"],
            },
            "statistics": {
                "total_files": len(file_manifest),
                "total_size": sum(f["size"] for f in file_manifest),
            },
            "diagnostics": diagnostics,
        },
        "files": sorted(file_manifest, key=lambda x: x["path"]),
    }

    # Write manifest to file
    print(f"💾 Writing manifest to {output_path}...")
    try:
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2, ensure_ascii=False)

        print(f"✅ Manifest successfully created: {output_path}")
        print("📊 Statistics:")
        print(f"   • Total files: {manifest['metadata']['statistics']['total_files']}")
        total_size_mb = manifest['metadata']['statistics']['total_size'] / (1024 * 1024)
        print(f"   • Total size: {total_size_mb:.2f} MB")

    except Exception as e:
        print(f"Error: Failed to write manifest file: {e}", file=sys.stderr)
        sys.exit(1)


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Generate a fetchability resources manifest for CI/CD pipelines"
    )
    parser.add_argument(
        "-o",
        "--output",
        default="fetchability-resources.json",
        help="Output path for the manifest file (default: fetchability-resources.json)",
    )
    parser.add_argument(
        "--no-diagnostics",
        action="store_true",
        help="Skip diagnostics collection from log files",
    )

    args = parser.parse_args()

    print("🎉 Starting fetchability manifest generation...")
    generate_manifest(
        output_path=args.output,
        include_diagnostics=not args.no_diagnostics,
    )
    print("🎉 Fetchability manifest generation completed!")


if __name__ == "__main__":
    main()
