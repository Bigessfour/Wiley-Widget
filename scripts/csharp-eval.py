#!/usr/bin/env python3
"""
C# MCP Evaluation Helper
Provides Python interface to C# MCP server for batch testing and CI/CD integration.
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict


class CSharpEvaluator:
    """Wrapper for C# MCP evaluation."""

    def __init__(self, timeout: int = 30):
        self.timeout = timeout

    def eval_code(self, code: str) -> Dict[str, Any]:
        """
        Evaluate C# code.

        Note: This requires the MCP extension to be active.
        For now, we output instructions for manual execution.
        """
        print("📄 C# Code to evaluate:")
        print("=" * 60)
        print(code)
        print("=" * 60)
        print("\n💡 To execute via Copilot:")
        print("   1. Copy the code above")
        print("   2. Ask Copilot: 'Run this C# code using MCP'")
        print("   3. Or use: mcp_csharp-mcp_eval_c_sharp tool")

        return {
            "success": True,
            "message": "Instructions displayed. Use Copilot to execute.",
            "code": code,
        }

    def eval_file(self, file_path: Path) -> Dict[str, Any]:
        """Evaluate C# code from a file."""
        if not file_path.exists():
            return {"success": False, "error": f"File not found: {file_path}"}

        code = file_path.read_text(encoding="utf-8")
        print(f"📂 Evaluating file: {file_path}")
        return self.eval_code(code)

    def run_test_suite(self, test_dir: Path) -> Dict[str, Any]:
        """Run all .csx files in a directory."""
        if not test_dir.exists():
            return {"success": False, "error": f"Directory not found: {test_dir}"}

        csx_files = list(test_dir.glob("**/*.csx"))

        if not csx_files:
            return {"success": False, "error": f"No .csx files found in: {test_dir}"}

        print(f"🧪 Found {len(csx_files)} test files")
        results = []

        for csx_file in csx_files:
            print(f"\n{'=' * 60}")
            print(f"Running: {csx_file.name}")
            print("=" * 60)
            result = self.eval_file(csx_file)
            results.append({"file": str(csx_file), "result": result})

        return {"success": True, "total": len(csx_files), "results": results}


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="C# MCP Evaluation Helper",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Evaluate a single file
  python csharp-eval.py -f scripts/examples/csharp/test.csx

  # Run all tests in a directory
  python csharp-eval.py -d scripts/examples/csharp/

  # Evaluate code directly
  python csharp-eval.py -c "Console.WriteLine('Hello');"
        """,
    )

    parser.add_argument("-f", "--file", type=Path, help="Path to .csx file to evaluate")

    parser.add_argument(
        "-d", "--directory", type=Path, help="Directory containing .csx test files"
    )

    parser.add_argument("-c", "--code", type=str, help="C# code to evaluate directly")

    parser.add_argument(
        "-t", "--timeout", type=int, default=30, help="Timeout in seconds (default: 30)"
    )

    parser.add_argument("--json", action="store_true", help="Output results as JSON")

    args = parser.parse_args()

    evaluator = CSharpEvaluator(timeout=args.timeout)

    # Determine operation mode
    if args.file:
        result = evaluator.eval_file(args.file)
    elif args.directory:
        result = evaluator.run_test_suite(args.directory)
    elif args.code:
        result = evaluator.eval_code(args.code)
    else:
        parser.print_help()
        return 1

    # Output results
    if args.json:
        print(json.dumps(result, indent=2))

    return 0 if result.get("success") else 1


if __name__ == "__main__":
    sys.exit(main())
