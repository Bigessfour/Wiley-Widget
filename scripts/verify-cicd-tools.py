#!/usr/bin/env python3
"""
Wiley Widget CI/CD Tools Verification (Python)
Cross-platform tool verification with enhanced performance
"""

import argparse
import logging
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Tuple


class CICDToolsVerifier:
    """CI/CD Tools Verification Engine"""

    def __init__(
        self,
        detailed: bool = False,
        fix_issues: bool = False,
        log_file: str = "cicd-verification.log",
    ):
        self.detailed = detailed
        self.fix_issues = fix_issues
        self.log_file = Path(log_file)
        self.results = {}
        self.issues = []

        # Setup logging
        self._setup_logging()

    def _setup_logging(self):
        """Configure logging to both file and console"""
        # Clear previous log
        if self.log_file.exists():
            self.log_file.write_text("")

        # Configure logging
        logging.basicConfig(
            level=logging.INFO,
            format="[%(asctime)s] [%(levelname)s] %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S",
            handlers=[
                logging.FileHandler(self.log_file),
                logging.StreamHandler(sys.stdout),
            ],
        )
        self.logger = logging.getLogger(__name__)

    def log(self, message: str, level: str = "INFO"):
        """Log a message with specified level"""
        if level == "ERROR":
            self.logger.error(message)
        elif level == "WARN":
            self.logger.warning(message)
        else:
            self.logger.info(message)

    def run_command(self, command: str, shell: bool = True) -> Tuple[int, str, str]:
        """Run a command and return exit code, stdout, stderr"""
        try:
            result = subprocess.run(
                command, shell=shell, capture_output=True, text=True, timeout=30
            )
            return result.returncode, result.stdout, result.stderr
        except subprocess.TimeoutExpired:
            return -1, "", "Command timed out"
        except Exception as e:
            return -1, "", str(e)

    def test_tool(
        self, name: str, command: str, expected_output: str = "", required: bool = True
    ) -> bool:
        """Test a tool and record results"""
        print(f"  🔍 Checking {name}...", end="", flush=True)

        exit_code, stdout, stderr = self.run_command(command)

        success = exit_code == 0 and (not expected_output or expected_output in stdout)

        if success:
            print(" ✅")
            self.results[name] = {
                "status": "OK",
                "output": stdout.strip(),
                "exit_code": exit_code,
            }
            if self.detailed and stdout.strip():
                display_output = stdout.replace("\n", " | ").strip()
                if len(display_output) > 100:
                    display_output = display_output[:100] + "..."
                print(f"     Output: {display_output}")
        else:
            print(" ❌")
            combined_output = f"{stdout}\n{stderr}".strip()
            self.results[name] = {
                "status": "FAIL",
                "output": combined_output,
                "exit_code": exit_code,
            }
            if required:
                self.issues.append(f"{name} failed (Exit: {exit_code})")
            if self.detailed and combined_output:
                display_error = combined_output.replace("\n", " | ").strip()
                if len(display_error) > 100:
                    display_error = display_error[:100] + "..."
                print(f"     Error: {display_error}")

        return success

    def check_trunk_config(self) -> bool:
        """Check trunk.yaml configuration"""
        trunk_config = Path(".trunk/trunk.yaml")

        print("  🔍 Trunk Configuration...", end="", flush=True)

        if not trunk_config.exists():
            print(" ❌")
            self.results["Trunk Config"] = {
                "status": "MISSING",
                "output": "File not found",
                "exit_code": -1,
            }
            self.issues.append("Trunk configuration file missing")
            return False

        try:
            content = trunk_config.read_text()

            if "version:" in content and "lint:" in content:
                print(" ✅")
                # Count enabled linters
                linter_count = content.count("enabled:") + content.count("- ")
                self.results["Trunk Config"] = {
                    "status": "OK",
                    "output": f"Valid config with ~{linter_count} linters",
                    "exit_code": 0,
                }
                if self.detailed:
                    print("     Contains linter configuration")
                return True
            else:
                raise ValueError("Invalid trunk configuration format")

        except Exception as e:
            print(" ❌")
            self.results["Trunk Config"] = {
                "status": "ERROR",
                "output": str(e),
                "exit_code": -1,
            }
            self.issues.append(f"Trunk config error: {e}")
            return False

    def check_github_actions(self) -> bool:
        """Check GitHub Actions workflows"""
        workflows_dir = Path(".github/workflows")

        print("  🔍 GitHub Actions Workflows...", end="", flush=True)

        if not workflows_dir.exists():
            print(" ❌")
            self.results["GitHub Actions"] = {
                "status": "MISSING",
                "output": "Directory not found",
                "exit_code": -1,
            }
            self.issues.append("GitHub Actions workflows directory missing")
            return False

        try:
            workflow_files = list(workflows_dir.glob("*.yml"))
            workflow_count = len(workflow_files)

            if workflow_count > 0:
                print(" ✅")
                self.results["GitHub Actions"] = {
                    "status": "OK",
                    "output": f"Found {workflow_count} workflow(s)",
                    "exit_code": 0,
                }
                if self.detailed:
                    workflow_names = [f.name for f in workflow_files[:3]]
                    if workflow_count > 3:
                        workflow_names.append("...")
                    print(f"     Workflows: {', '.join(workflow_names)}")
                return True
            else:
                raise ValueError("No workflow files found")

        except Exception as e:
            print(" ❌")
            self.results["GitHub Actions"] = {
                "status": "ERROR",
                "output": str(e),
                "exit_code": -1,
            }
            self.issues.append(f"GitHub Actions error: {e}")
            return False

    def run_verification(self):
        """Run the complete verification suite"""
        print("🔧 WileyWidget CI/CD Tools Verification")
        print("=====================================")

        start_time = datetime.now()

        self.log("Starting CI/CD Tools Verification")

        # Check Core Development Tools
        print("\n📦 Core Development Tools:")
        self.test_tool("Git", "git --version", "git version")
        self.test_tool("Node.js", "node --version", "v")
        self.test_tool("NPM", "npm --version", required=False)
        self.test_tool("PowerShell", "pwsh --version", "PowerShell")
        self.test_tool(".NET SDK", "dotnet --version", required=False)

        # Check Trunk and Linters
        print("\n🔍 Trunk & Linters:")
        self.test_tool("Trunk CLI", "trunk --version", "1.25.0")
        self.test_tool(
            "Trunk Check", "trunk check --help", "trunk check", required=False
        )

        # Check Azure Tools
        print("\n☁️  Azure Tools:")
        self.test_tool("Azure CLI", "az --version", "azure-cli", required=False)
        self.test_tool("Azure Account", "az account show", required=False)

        # Check Build Tools
        print("\n🔨 Build Tools:")
        self.test_tool("MSBuild", "msbuild /version", required=False)
        self.test_tool("NuGet", "nuget help", "NuGet", required=False)

        # Check Testing Tools
        print("\n🧪 Testing Tools:")
        self.test_tool("VSTest", "vstest.console.exe /?", required=False)

        # Check GitHub Tools
        print("\n🐙 GitHub Tools:")
        self.test_tool("GitHub CLI", "gh --version", "gh version", required=False)

        # Check CI/CD Configuration
        print("\n⚙️  CI/CD Configuration:")
        self.check_trunk_config()
        self.check_github_actions()

        # Summary
        duration = datetime.now() - start_time
        success_count = sum(1 for r in self.results.values() if r["status"] == "OK")
        total_count = len(self.results)

        print(f"\n📊 Verification Complete: {success_count}/{total_count} tools OK")
        print(f"⏱️  Duration: {duration.total_seconds():.1f}s")

        if self.issues:
            print(f"\n❌ Issues Found ({len(self.issues)}):")
            for issue in self.issues:
                print(f"   - {issue}")
        else:
            print("\n✅ All checks passed!")

        self.log(f"Verification complete: {success_count}/{total_count} tools OK")

        return len(self.issues) == 0


def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="WileyWidget CI/CD Tools Verification")
    parser.add_argument(
        "--detailed", "-d", action="store_true", help="Show detailed output"
    )
    parser.add_argument(
        "--fix-issues", "-f", action="store_true", help="Attempt to fix issues"
    )
    parser.add_argument(
        "--log-file", "-l", default="cicd-verification.log", help="Log file path"
    )

    args = parser.parse_args()

    verifier = CICDToolsVerifier(
        detailed=args.detailed, fix_issues=args.fix_issues, log_file=args.log_file
    )

    success = verifier.run_verification()
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
