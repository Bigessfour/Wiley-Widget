#!/usr/bin/env python3
"""
Environment Variable Validation Script for CI/CD Pipelines

Usage:
    python validate_env_vars.py \
        --required QBO_CLIENT_ID,QBO_CLIENT_SECRET,QBO_REALM_ID \
        --optional XAI_API_KEY,WEBHOOKS_PORT

Exit Codes:
    0 - All required variables present
    1 - One or more required variables missing
"""

import os
import sys
import argparse
from typing import List, Tuple


def validate_env_vars(required: List[str], optional: List[str]) -> int:
    """
    Validates that required environment variables are set.
    Warns about missing optional variables.
    
    Args:
        required: List of required environment variable names
        optional: List of optional environment variable names
        
    Returns:
        Exit code (0 = success, 1 = failure)
    """
    missing_required = [var for var in required if not os.getenv(var)]
    missing_optional = [var for var in optional if not os.getenv(var)]
    
    # Check required variables
    if missing_required:
        print(
            f"❌ Missing REQUIRED environment variables: {', '.join(missing_required)}",
            file=sys.stderr
        )
        print("\nSet these variables before deployment:", file=sys.stderr)
        for var in missing_required:
            print(f"  export {var}=<value>", file=sys.stderr)
        return 1
    
    # Report success
    print(f"✅ All {len(required)} required environment variables are set")
    
    # Warn about optional variables
    if missing_optional:
        print(
            f"⚠️  Missing optional environment variables (features may be degraded): "
            f"{', '.join(missing_optional)}"
        )
    
    # List all validated variables (for audit trail)
    print("\nValidated environment variables:")
    for var in required:
        value = os.getenv(var, "")
        # Redact secret values (show only first 4 chars)
        if any(keyword in var.upper() for keyword in ['SECRET', 'KEY', 'TOKEN', 'PASSWORD']):
            display_value = f"{value[:4]}..." if len(value) > 4 else "***"
        else:
            display_value = value
        print(f"  {var}: {display_value}")
    
    return 0


def main():
    parser = argparse.ArgumentParser(
        description="Validate required environment variables for Wiley Widget deployment"
    )
    parser.add_argument(
        "--required",
        type=str,
        required=True,
        help="Comma-separated list of required environment variable names"
    )
    parser.add_argument(
        "--optional",
        type=str,
        default="",
        help="Comma-separated list of optional environment variable names"
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Show detailed output including optional variable values"
    )
    
    args = parser.parse_args()
    
    # Parse comma-separated lists
    required = [v.strip() for v in args.required.split(",") if v.strip()]
    optional = [v.strip() for v in args.optional.split(",") if v.strip()]
    
    if not required:
        print("❌ No required variables specified", file=sys.stderr)
        return 1
    
    # Run validation
    exit_code = validate_env_vars(required, optional)
    
    # Verbose mode: show optional variable status
    if args.verbose and optional:
        print("\nOptional environment variables:")
        for var in optional:
            value = os.getenv(var)
            if value:
                print(f"  ✓ {var}: SET")
            else:
                print(f"  ✗ {var}: NOT SET")
    
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
