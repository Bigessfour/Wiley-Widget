#!/usr/bin/env python3
"""
Monitor Test Resources Script

Monitors CPU/memory during dotnet test runs and parses coverage.cobertura.xml
"""

import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

import debugpy
import psutil


def setup_debugpy():
    """Setup debugpy for remote debugging"""
    debugpy.listen(("localhost", 5678))
    print("Debugpy listening on localhost:5678")
    print("Attach debugger to continue...")
    debugpy.wait_for_client()


def monitor_resources(process, duration=60):
    """Monitor CPU and memory usage"""
    start_time = time.time()
    cpu_percent = []
    memory_mb = []

    try:
        while time.time() - start_time < duration and process.poll() is None:
            try:
                cpu = psutil.cpu_percent(interval=1)
                mem = psutil.virtual_memory()
                proc_mem = process.memory_info().rss / 1024 / 1024  # MB

                cpu_percent.append(cpu)
                memory_mb.append(proc_mem)

                print(".1f")
            except psutil.NoSuchProcess:
                break

        return {
            "cpu_avg": sum(cpu_percent) / len(cpu_percent) if cpu_percent else 0,
            "cpu_max": max(cpu_percent) if cpu_percent else 0,
            "memory_avg": sum(memory_mb) / len(memory_mb) if memory_mb else 0,
            "memory_max": max(memory_mb) if memory_mb else 0,
        }
    except Exception as e:
        print(f"Error monitoring resources: {e}")
        return {}


def parse_coverage_xml(xml_file):
    """Parse coverage XML file"""
    if not xml_file.exists():
        print(f"Coverage file not found: {xml_file}")
        return {}

    try:
        tree = ET.parse(xml_file)
        root = tree.getroot()

        total_lines = 0
        covered_lines = 0

        for package in root.findall(".//package"):
            for cls in package.findall(".//class"):
                for line in cls.findall(".//line"):
                    total_lines += 1
                    if line.get("hits", "0") != "0":
                        covered_lines += 1

        coverage_percent = (covered_lines / total_lines * 100) if total_lines > 0 else 0

        return {
            "total_lines": total_lines,
            "covered_lines": covered_lines,
            "coverage_percent": coverage_percent,
        }
    except Exception as e:
        print(f"Error parsing coverage XML: {e}")
        return {}


def run_dotnet_test():
    """Run dotnet test and monitor resources"""
    workspace_folder = Path(__file__).parent.parent

    cmd = [
        "dotnet",
        "test",
        str(workspace_folder / "WileyWidget.UiTests" / "WileyWidget.UiTests.csproj"),
        "--filter",
        "FullyQualifiedName=WileyWidget.UiTests.EndToEndStartupTests.E2E_01_FullApplicationStartup_WithTiming",
        '--collect:"XPlat Code Coverage"',
        "--results-directory:TestResults",
        "--logger",
        "console;verbosity=normal",
    ]

    print(f"Running: {' '.join(cmd)}")

    try:
        proc = subprocess.Popen(cmd, cwd=workspace_folder)

        # Monitor resources for 60 seconds
        resources = monitor_resources(proc, 60)

        # Wait for process to complete
        proc.wait()

        print(f"Test exit code: {proc.returncode}")

        # Parse coverage
        coverage_file = workspace_folder / "TestResults" / "coverage.cobertura.xml"
        coverage = parse_coverage_xml(coverage_file)

        # Report
        print("\n=== Resource Usage Report ===")
        print(".1f")
        print(".1f")
        print(".1f")
        print(".1f")

        print("\n=== Coverage Report ===")
        print(f"Total lines: {coverage.get('total_lines', 0)}")
        print(f"Covered lines: {coverage.get('covered_lines', 0)}")
        print(".1f")

    except Exception as e:
        print(f"Error running test: {e}")


def main():
    import argparse

    parser = argparse.ArgumentParser(description="Monitor Test Resources Script")
    parser.add_argument(
        "--report", action="store_true", help="Generate detailed report"
    )
    parser.add_argument(
        "--timeout", type=int, default=60, help="Monitoring timeout in seconds"
    )
    parser.add_argument("--debug", action="store_true", help="Enable debugpy debugging")

    args = parser.parse_args()

    if args.debug:
        setup_debugpy()

    print("Monitoring test resources...")
    run_dotnet_test()
    print("Monitoring completed")


if __name__ == "__main__":
    main()
