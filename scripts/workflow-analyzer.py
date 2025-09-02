#!/usr/bin/env python3
"""
GitHub Actions Workflow Analyzer
Helps analyze and validate GitHub Actions workflow files
"""

from pathlib import Path
from typing import Any, Dict, List

import yaml


class WorkflowAnalyzer:
    def __init__(self, repo_path: str = "."):
        self.repo_path = Path(repo_path)
        self.workflows_path = self.repo_path / ".github" / "workflows"

    def find_workflow_files(self) -> List[Path]:
        """Find all workflow files in .github/workflows directory"""
        if not self.workflows_path.exists():
            return []

        return list(self.workflows_path.glob("*.yml")) + list(
            self.workflows_path.glob("*.yaml")
        )

    def validate_yaml(self, file_path: Path) -> Dict[str, Any]:
        """Validate YAML syntax and structure"""
        result = {"file": str(file_path), "valid": False, "errors": [], "warnings": []}

        try:
            with open(file_path, "r", encoding="utf-8-sig") as f:
                content = yaml.safe_load(f)
                result["valid"] = True
                print(
                    f"DEBUG: Parsed keys for {file_path.name}: {list(content.keys())}"
                )
                print(f"DEBUG: 'on' in content: {'on' in content}")
                if True in content:
                    print(f"DEBUG: True key value: {content[True]}")

            # Basic workflow validation
            if not isinstance(content, dict):
                result["errors"].append("Workflow file must contain a dictionary")
                result["valid"] = False
                return result

            # Check for required fields
            if "name" not in content:
                result["warnings"].append("Workflow missing 'name' field")

            # Check for 'on' trigger field (YAML parses 'on:' as True key)
            trigger_field = None
            if "on" in content:
                trigger_field = "on"
            elif True in content:
                trigger_field = True

            if not trigger_field:
                result["errors"].append("Workflow missing 'on' trigger field")
                result["valid"] = False

            if "jobs" not in content:
                result["errors"].append("Workflow missing 'jobs' field")
                result["valid"] = False

            # Check trigger configuration
            if trigger_field:
                triggers = content[trigger_field]
                if isinstance(triggers, str):
                    triggers = [triggers]

                if isinstance(triggers, list):
                    for trigger in triggers:
                        if trigger not in [
                            "push",
                            "pull_request",
                            "schedule",
                            "workflow_dispatch",
                        ]:
                            result["warnings"].append(
                                f"Non-standard trigger: {trigger}"
                            )

        except yaml.YAMLError as e:
            result["errors"].append(f"YAML syntax error: {str(e)}")
        except Exception as e:
            result["errors"].append(f"Error reading file: {str(e)}")

        return result

    def analyze_triggers(self, workflow: Dict[str, Any]) -> Dict[str, Any]:
        """Analyze workflow triggers and their configuration"""
        analysis = {"triggers": [], "issues": []}

        if "on" not in workflow:
            analysis["issues"].append("No triggers defined")
            return analysis

        triggers = workflow["on"]

        if isinstance(triggers, str):
            analysis["triggers"].append({"type": triggers, "config": {}})
        elif isinstance(triggers, list):
            for trigger in triggers:
                analysis["triggers"].append({"type": trigger, "config": {}})
        elif isinstance(triggers, dict):
            for trigger_type, config in triggers.items():
                analysis["triggers"].append({"type": trigger_type, "config": config})

                # Check for common issues
                if trigger_type == "push" and isinstance(config, dict):
                    if "branches" in config:
                        branches = config["branches"]
                        if isinstance(branches, list) and "main" not in branches:
                            analysis["issues"].append(
                                "Push trigger doesn't include 'main' branch"
                            )

        return analysis

    def analyze_workflow(self, file_path: Path) -> Dict[str, Any]:
        """Complete workflow analysis"""
        validation = self.validate_yaml(file_path)

        if not validation["valid"]:
            return validation

        # Load workflow for deeper analysis
        try:
            with open(file_path, "r", encoding="utf-8-sig") as f:
                workflow = yaml.safe_load(f)

            trigger_analysis = self.analyze_triggers(workflow)

            return {
                **validation,
                "name": workflow.get("name", "Unnamed"),
                "triggers": trigger_analysis["triggers"],
                "trigger_issues": trigger_analysis["issues"],
                "job_count": len(workflow.get("jobs", {})),
            }
        except Exception as e:
            validation["errors"].append(f"Analysis error: {str(e)}")
            return validation

    def generate_report(self) -> str:
        """Generate a comprehensive workflow analysis report"""
        workflow_files = self.find_workflow_files()

        if not workflow_files:
            return "No workflow files found in .github/workflows/"

        report = []
        report.append("GitHub Actions Workflow Analysis Report")
        report.append("=" * 50)
        report.append(f"Found {len(workflow_files)} workflow file(s)")
        report.append("")

        total_valid = 0
        total_warnings = 0
        total_errors = 0

        for wf_file in workflow_files:
            analysis = self.analyze_workflow(wf_file)
            report.append(f"File: {analysis['file']}")

            if analysis["valid"]:
                total_valid += 1
                report.append("  ✅ Valid YAML syntax")
            else:
                report.append("  ❌ Invalid YAML syntax")

            if "name" in analysis:
                report.append(f"  Name: {analysis['name']}")

            if analysis["errors"]:
                total_errors += len(analysis["errors"])
                report.append("  Errors:")
                for error in analysis["errors"]:
                    report.append(f"    - {error}")

            if analysis["warnings"]:
                total_warnings += len(analysis["warnings"])
                report.append("  Warnings:")
                for warning in analysis["warnings"]:
                    report.append(f"    - {warning}")

            if "triggers" in analysis and analysis["triggers"]:
                report.append("  Triggers:")
                for trigger in analysis["triggers"]:
                    report.append(f"    - {trigger['type']}")

            if "trigger_issues" in analysis and analysis["trigger_issues"]:
                report.append("  Trigger Issues:")
                for issue in analysis["trigger_issues"]:
                    report.append(f"    - {issue}")

            if "job_count" in analysis:
                report.append(f"  Jobs: {analysis['job_count']}")

            report.append("")

        # Summary
        report.append("Summary:")
        report.append(f"  Valid workflows: {total_valid}/{len(workflow_files)}")
        report.append(f"  Total warnings: {total_warnings}")
        report.append(f"  Total errors: {total_errors}")

        return "\n".join(report)


def main():
    analyzer = WorkflowAnalyzer()
    report = analyzer.generate_report()
    print(report)


if __name__ == "__main__":
    main()
    main()
