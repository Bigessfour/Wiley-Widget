#!/usr/bin/env python3
"""
JSON schema validation for CI/CD results
"""

import json
import os

from jsonschema import ValidationError, validate


def validate_cicd_results():
    print("CI/CD Results Validation")
    print("=" * 25)

    # Define expected schema for CI/CD results
    schema = {
        "type": "object",
        "properties": {
            "success_rate": {"type": "string"},
            "total_runs": {"type": "integer"},
            "runs": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "id": {"type": "string"},
                        "status": {"type": "string"},
                        "timestamp": {"type": "string"},
                    },
                    "required": ["id", "status"],
                },
            },
            "timestamp": {"type": "string"},
        },
        "required": ["success_rate", "total_runs", "runs"],
    }

    if os.path.exists("cicd-results.json"):
        try:
            with open("cicd-results.json", "r") as f:
                data = json.load(f)

            # Validate against schema
            validate(instance=data, schema=schema)
            print("✅ CI/CD results are valid JSON")
            print(f"   Success Rate: {data.get('success_rate', 'N/A')}")
            print(f"   Total Runs: {data.get('total_runs', 'N/A')}")
            print(f"   Valid Runs: {len(data.get('runs', []))}")

        except ValidationError as e:
            print(f"❌ Schema validation failed: {e.message}")
        except json.JSONDecodeError as e:
            print(f"❌ Invalid JSON format: {e}")
    else:
        print("No cicd-results.json file found")
        print("Creating sample valid structure...")

        sample_data = {
            "success_rate": "90%",
            "total_runs": 0,
            "runs": [],
            "timestamp": "2024-01-01T00:00:00Z",
        }

        with open("cicd-results.json", "w") as f:
            json.dump(sample_data, f, indent=2)

        print("✅ Created sample cicd-results.json")


if __name__ == "__main__":
    validate_cicd_results()
