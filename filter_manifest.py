#!/usr/bin/env python3
import json
import sys
from pathlib import Path


def should_keep_file(file_entry):
    """Determine if a file should be kept in the filtered manifest."""
    path = file_entry["metadata"]["path"]
    category = file_entry["context"]["category"]
    language = file_entry["metadata"]["language"]
    extension = file_entry["metadata"]["extension"]

    # Keep ALL files under src/
    if path.startswith("src/"):
        return True

    # Keep essential configuration files
    essential_configs = [
        ".csproj",
        ".sln",
        "global.json",
        "Directory.Build.props",
        "Directory.Build.targets",
    ]
    if any(path.endswith(ext) for ext in essential_configs):
        return True

    # Remove categories
    if category in ["documentation", "automation", "test", "unknown"]:
        return False

    # Remove log files
    if extension in [".txt", ".log", ".binlog"]:
        return False

    # Remove lock files
    if "packages.lock.json" in path:
        return False

    # Remove temp files
    if extension in [".wpftmp", ".bak"]:
        return False

    # Remove assets (except those under src/ which are already kept)
    if category == "assets":
        return False

    # Keep configuration files that are essential
    if category == "configuration":
        # Keep .json configs but not lock files
        if extension == ".json" and "lock" not in path:
            return True
        return False

    # Default: keep if not explicitly removed
    return True


def filter_manifest(manifest):
    """Filter the manifest and update totals."""
    files = manifest["files"]

    # Filter files
    filtered_files = [f for f in files if should_keep_file(f)]

    # Update summary
    total_files = len(filtered_files)
    total_size = sum(f["metadata"]["size"] for f in filtered_files)

    # Recalculate categories
    categories = {}
    languages = {}

    for f in filtered_files:
        cat = f["context"]["category"]
        lang = f["metadata"]["language"]

        categories[cat] = categories.get(cat, 0) + 1
        languages[lang] = languages.get(lang, 0) + 1

    # Update manifest
    manifest["files"] = filtered_files
    manifest["summary"]["total_files"] = total_files
    manifest["summary"]["files_in_manifest"] = total_files
    manifest["summary"]["total_size"] = total_size
    manifest["summary"]["categories"] = categories
    manifest["summary"]["languages"] = languages

    return manifest


def main():
    # Load manifest
    manifest_path = Path("ai-fetchable-manifest.json")
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    print(f"Original manifest: {manifest['summary']['total_files']} files")

    # Filter manifest
    filtered_manifest = filter_manifest(manifest)

    print(f"Filtered manifest: {filtered_manifest['summary']['total_files']} files")
    print(f"Categories: {filtered_manifest['summary']['categories']}")

    # Save filtered manifest
    output_path = Path("ai-fetchable-manifest-filtered.json")
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(filtered_manifest, f, indent=2, ensure_ascii=False)

    print(f"Filtered manifest saved to: {output_path}")


if __name__ == "__main__":
    main()
