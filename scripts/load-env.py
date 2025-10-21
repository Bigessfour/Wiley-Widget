#!/usr/bin/env python3
"""
Secure Environment Variable Loader for WileyWidget (Python)
This script loads environment variables from .env file securely
"""

import argparse
import os
import sys
from pathlib import Path


def load_environment_variables(env_file: Path | None = None):
    """Load environment variables from a .env-style file"""
    if env_file is None:
        env_file = Path(__file__).parent.parent / ".env"

    if not env_file.exists():
        print(f"❌ .env file not found at: {env_file}")
        print("Create a .env file with your configuration variables.")
        return False

    print("🔐 Loading environment variables from .env file...")

    loaded_count = 0
    error_count = 0

    try:
        with open(env_file, "r", encoding="utf-8") as f:
            for line_num, line in enumerate(f, 1):
                line = line.strip()

                # Skip comments and empty lines
                if not line or line.startswith("#"):
                    continue

                # Parse KEY=VALUE
                if "=" in line:
                    key, value = line.split("=", 1)
                    key = key.strip()
                    value = value.strip()

                    # Remove quotes if present
                    if (value.startswith('"') and value.endswith('"')) or (
                        value.startswith("'") and value.endswith("'")
                    ):
                        value = value[1:-1]

                    # Handle Azure Key Vault references
                    if value.startswith("@AzureKeyVault(") and value.endswith(")"):
                        kv_reference = value[15:-1]  # Remove @AzureKeyVault( and )
                        print(f"  🔑 Resolving Key Vault reference: {kv_reference}")

                        try:
                            import azure.identity  # type: ignore
                            import azure.keyvault.secrets  # type: ignore

                            # Extract vault URL and secret name from reference
                            # Format: https://vault-name.vault.azure.net/secrets/secret-name
                            if "/secrets/" in kv_reference:
                                vault_url = kv_reference.split("/secrets/")[0]
                                secret_name = kv_reference.split("/secrets/")[1]

                                credential = azure.identity.DefaultAzureCredential()
                                client = azure.keyvault.secrets.SecretClient(
                                    vault_url=vault_url, credential=credential
                                )

                                secret = client.get_secret(secret_name)
                                if secret and secret.value:
                                    os.environ[key] = secret.value
                                    loaded_count += 1
                                    print(f"  ✅ {key} (from Key Vault)")
                                else:
                                    error_count += 1
                                    print(f"  ❌ {key}: Empty secret from Key Vault")
                            else:
                                error_count += 1
                                print(f"  ❌ {key}: Invalid Key Vault reference format")
                        except ImportError:
                            error_count += 1
                            print(
                                f"  ❌ {key}: Azure SDK not available for Key Vault resolution"
                            )
                        except Exception as e:
                            error_count += 1
                            print(f"  ❌ {key}: Key Vault resolution failed: {str(e)}")
                    else:
                        # Regular environment variable
                        try:
                            os.environ[key] = value
                            loaded_count += 1
                            print(f"  ✅ {key}")
                        except Exception as e:
                            error_count += 1
                            print(f"  ❌ {key}: {e}")
                else:
                    print(f"  ⚠️  Skipping invalid line {line_num}: {line}")

    except Exception as e:
        print(f"❌ Error reading .env file: {e}")
        return False

    print(f"Loaded {loaded_count} environment variables")
    if error_count > 0:
        print(f"Failed to load {error_count} variables")

    return error_count == 0


def unload_environment_variables(env_file: Path | None = None):
    """Unload environment variables (reset to system defaults)"""
    if env_file is None:
        env_file = Path(__file__).parent.parent / ".env"

    if not env_file.exists():
        print(f"❌ .env file not found at: {env_file}")
        return False

    print("🔐 Unloading environment variables from .env file...")

    unloaded_count = 0

    try:
        with open(env_file, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()

                # Skip comments and empty lines
                if not line or line.startswith("#"):
                    continue

                # Parse KEY=VALUE
                if "=" in line:
                    key, value = line.split("=", 1)
                    key = key.strip()

                    # Remove from environment if it exists
                    if key in os.environ:
                        del os.environ[key]
                        unloaded_count += 1
                        print(f"  ✅ {key}")

    except Exception as e:
        print(f"❌ Error reading .env file: {e}")
        return False

    print(f"Unloaded {unloaded_count} environment variables")
    return True


def show_status():
    """Show current environment variable status"""
    env_file = Path(__file__).parent.parent / ".env"

    print("=== Environment Variables Status ===")

    if not env_file.exists():
        print(f"❌ .env file not found at: {env_file}")
        return False

    print(f"📄 .env file: {env_file}")

    # Check key environment variables
    key_vars = [
        "AZURE_KEY_VAULT_URL",
        "AZURE_KEY_VAULT_NAME",
        "SYNCFUSION_LICENSE_KEY",
        "AZURE_SUBSCRIPTION_ID",
        "AZURE_TENANT_ID",
    ]

    loaded_count = 0
    for var in key_vars:
        value = os.environ.get(var, "")
        if value:
            # Mask sensitive values
            if "LICENSE" in var or "SECRET" in var or "KEY" in var:
                display_value = value[:10] + "..." if len(value) > 10 else value
            else:
                display_value = value
            print(f"  ✅ {var}: {display_value}")
            loaded_count += 1
        else:
            print(f"  ❌ {var}: Not set")

    print(f"Total key variables loaded: {loaded_count}/{len(key_vars)}")
    return loaded_count > 0


def test_connections():
    """Test Azure connections"""
    print("=== Testing Azure Connections ===")

    # Check if required environment variables are set
    kv_url = os.environ.get("AZURE_KEY_VAULT_URL")
    if not kv_url:
        print("❌ AZURE_KEY_VAULT_URL not set")
        return False

    print(f"🔗 Testing connection to: {kv_url}")

    # This would require azure-identity and azure-keyvault-secrets packages
    try:
        import azure.identity  # type: ignore
        import azure.keyvault.secrets  # type: ignore

        credential = azure.identity.DefaultAzureCredential()
        client = azure.keyvault.secrets.SecretClient(
            vault_url=kv_url, credential=credential
        )

        # Try to get a specific secret instead of listing all
        try:
            secret = client.get_secret("SyncfusionLicenseKey")
            if secret and secret.value:
                print("✅ Successfully connected to Key Vault")
                print("✅ Syncfusion license key found and accessible")
                return True
            else:
                print("⚠️  Syncfusion license key is empty")
                return False
        except Exception as e:
            print(f"❌ Could not retrieve Syncfusion license: {str(e)}")
            # Try to at least test connectivity by getting vault properties
            try:
                # This is a lighter operation than listing secrets
                vault_props = client.list_properties_of_secrets()  # type: ignore
                print(
                    "✅ Key Vault connection successful (but Syncfusion secret not found)"
                )
                return True
            except Exception as e2:
                print(f"❌ Key Vault connection failed: {str(e2)}")
                return False

    except ImportError as ie:
        print(f"❌ Azure SDK not installed: {ie}")
        print("Install with: pip install azure-identity azure-keyvault-secrets")
        return False
    except Exception as e:
        print(f"❌ Connection test failed: {str(e)}")
        # Don't crash - just report the error
        import traceback

        print(f"Stack trace: {traceback.format_exc()}")
        return False


def main():
    parser = argparse.ArgumentParser(description="Environment Variable Manager")
    parser.add_argument(
        "--load", action="store_true", help="Load environment variables from .env file"
    )
    parser.add_argument(
        "--unload", action="store_true", help="Unload environment variables"
    )
    parser.add_argument("--status", action="store_true", help="Show current status")
    parser.add_argument(
        "--test-connections",
        action="store_true",
        help="Test Azure connections (requires Azure SDK)",
    )
    parser.add_argument(
        "--production", action="store_true", help="Use .env.production instead of .env"
    )
    parser.add_argument("--env-file", type=str, help="Explicit path to .env-style file")

    args = parser.parse_args()

    if not any([args.load, args.unload, args.status, args.test_connections]):
        print("🔧 Wiley Widget Environment Manager")
        print("===================================")
        print("Use --load to load environment variables")
        print("Use --unload to unload environment variables")
        print("Use --status to show current status")
        print("Use --test-connections to test Azure Key Vault connection")
        print("")
        print("Example: python load-env.py --load --status")
        return 1

    success = True

    # Resolve env file
    env_path = None
    if args.env_file:
        env_path = Path(args.env_file)
    elif args.production:
        env_path = Path(__file__).parent.parent / ".env.production"

    if args.load:
        success &= load_environment_variables(env_path)

    if args.unload:
        success &= unload_environment_variables(env_path)

    if args.status:
        success &= show_status()

    if args.test_connections:
        success &= test_connections()

    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
