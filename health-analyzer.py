#!/usr/bin/env python3
"""
Process the actual CI/CD health check results
"""

import json
import os


def process_health_check():
    print("CI/CD Health Check Analysis")
    print("=" * 30)

    if os.path.exists("cicd-results.json"):
        with open("cicd-results.json", "r") as f:
            data = json.load(f)

        print("System Health Status:")
        all_ok = True

        for component, info in data.items():
            status = info.get("Status", "UNKNOWN")
            exit_code = info.get("ExitCode", -1)

            if status == "OK" and exit_code == 0:
                print(f"  ✅ {component}: {status}")
            else:
                print(f"  ❌ {component}: {status} (Exit: {exit_code})")
                all_ok = False

        print(
            f"\nOverall Status: {'✅ All Systems OK' if all_ok else '❌ Issues Detected'}"
        )

        # Extract version info
        print("\nVersion Information:")
        if "Azure CLI" in data:
            output = data["Azure CLI"].get("Output", "")
            if "azure-cli" in output:
                version_line = [
                    line for line in output.split("\n") if "azure-cli" in line
                ]
                if version_line:
                    print(f"  Azure CLI: {version_line[0].strip()}")

        if "NPM" in data:
            npm_version = data["NPM"].get("Output", "").strip()
            print(f"  NPM: {npm_version}")

        if "GitHub CLI" in data:
            output = data["GitHub CLI"].get("Output", "")
            version_line = [line for line in output.split("\n") if "gh version" in line]
            if version_line:
                print(f"  GitHub CLI: {version_line[0].strip()}")

    else:
        print("No health check results found")


if __name__ == "__main__":
    process_health_check()
