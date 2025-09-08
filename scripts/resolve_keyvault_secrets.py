#!/usr/bin/env python3
"""
Azure Key Vault Local Resolver for MCP Configuration
Resolves Key Vault references to actual values for local development

Microsoft Documentation Reference:
https://learn.microsoft.com/en-us/azure/key-vault/secrets/quick-create-python

Usage:
    python resolve_keyvault_secrets.py
    # or
    python -m resolve_keyvault_secrets
"""

import json
import logging
import os
import sys
from typing import Dict, List, Optional

from azure.core.exceptions import ClientAuthenticationError, HttpResponseError
from azure.identity import AzureCliCredential, DefaultAzureCredential
from azure.keyvault.secrets import SecretClient

# Configure logging
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


class KeyVaultResolver:
    """Resolves Azure Key Vault references to actual secret values"""

    def __init__(self, vault_name: str = "wiley-widget-secrets"):
        self.vault_name = vault_name
        self.vault_url = f"https://{vault_name}.vault.azure.net"
        self.client = None
        self._authenticate()

    def _authenticate(self) -> None:
        """Authenticate with Azure using DefaultAzureCredential"""
        try:
            # Try Azure CLI first (most common for local development)
            credential = AzureCliCredential()

            # Test the credential
            token = credential.get_token("https://vault.azure.net/.default")
            logger.info("Azure CLI authentication successful")

        except Exception as e:
            logger.warning(f"Azure CLI auth failed: {e}")
            try:
                # Fall back to DefaultAzureCredential
                credential = DefaultAzureCredential()
                logger.info("DefaultAzureCredential authentication successful")
            except Exception as e2:
                logger.error(f"❌ All authentication methods failed: {e2}")
                logger.error("Please run 'az login' or set up Azure authentication")
                sys.exit(1)

        try:
            self.client = SecretClient(vault_url=self.vault_url, credential=credential)
            logger.info(f"Connected to Key Vault: {self.vault_name}")
        except Exception as e:
            logger.error(f"❌ Failed to connect to Key Vault: {e}")
            sys.exit(1)

    def resolve_secret(self, secret_name: str) -> Optional[str]:
        """Resolve a single secret from Key Vault"""
        try:
            secret = self.client.get_secret(secret_name)
            value = secret.value
            logger.info(
                f"Retrieved secret: {secret_name} (length: {len(value) if value else 0})"
            )
            return value
        except HttpResponseError as e:
            if e.status_code == 404:
                logger.warning(f"⚠️ Secret not found: {secret_name}")
            else:
                logger.error(f"❌ Failed to retrieve secret {secret_name}: {e}")
            return None
        except Exception as e:
            logger.error(f"❌ Unexpected error retrieving {secret_name}: {e}")
            return None

    def resolve_all_secrets(self, secret_mappings: Dict[str, str]) -> Dict[str, str]:
        """Resolve all secrets and return as dictionary"""
        resolved = {}

        for env_var, kv_secret in secret_mappings.items():
            value = self.resolve_secret(kv_secret)
            if value:
                resolved[env_var] = value

        return resolved

    def set_environment_variables(self, secrets: Dict[str, str]) -> None:
        """Set resolved secrets as environment variables"""
        for env_var, value in secrets.items():
            os.environ[env_var] = value
            logger.info(f"Set environment variable: {env_var}")

    def load_env_file(self, env_file: str = ".env") -> Dict[str, str]:
        """Load and parse .env file to find Key Vault references"""
        kv_references = {}

        if not os.path.exists(env_file):
            logger.warning(f"⚠️ .env file not found: {env_file}")
            return kv_references

        logger.info(f"📖 Reading .env file: {env_file}")

        with open(env_file, "r", encoding="utf-8") as f:
            for line_num, line in enumerate(f, 1):
                line = line.strip()
                if not line or line.startswith("#"):
                    continue

                if "=" in line:
                    key, value = line.split("=", 1)
                    key = key.strip()
                    value = value.strip()

                    # Check if it's a Key Vault reference
                    if value.startswith("@Microsoft.KeyVault(SecretUri="):
                        # Extract secret name from URI
                        # Format: @Microsoft.KeyVault(SecretUri=https://vault.vault.azure.net/secrets/SECRET-NAME)
                        try:
                            uri_part = value.split("secrets/")[1]
                            if ")" in uri_part:
                                secret_name = uri_part.split(")")[0]
                                kv_references[key] = secret_name
                                logger.info(
                                    f"🔗 Found Key Vault reference: {key} -> {secret_name}"
                                )
                        except IndexError:
                            logger.warning(
                                f"⚠️ Could not parse Key Vault reference on line {line_num}: {line}"
                            )

        return kv_references


def main():
    """Main execution function"""
    print("Azure Key Vault Local Resolver for MCP Configuration")
    print("=" * 60)

    # Configuration
    VAULT_NAME = "wiley-widget-secrets"

    # Manual secret mappings (Key Vault name -> Environment variable name)
    SECRET_MAPPINGS = {
        "SYNCFUSION_LICENSE_KEY": "SYNCFUSION-LICENSE-KEY",
        "XAI_API_KEY": "XAI-API-KEY",
        "GITHUB_TOKEN": "GITHUB-PAT",
    }

    try:
        # Initialize resolver
        resolver = KeyVaultResolver(VAULT_NAME)

        # Load Key Vault references from .env file
        env_references = resolver.load_env_file()

        # Use manual mappings if .env references not found
        if not env_references:
            logger.info("🔄 Using manual secret mappings")
            env_references = SECRET_MAPPINGS

        # Resolve all secrets
        logger.info("🔍 Resolving secrets from Key Vault...")
        resolved_secrets = resolver.resolve_all_secrets(env_references)

        if not resolved_secrets:
            logger.error("❌ No secrets were successfully resolved")
            sys.exit(1)

        # Set environment variables
        logger.info("📝 Setting environment variables...")
        resolver.set_environment_variables(resolved_secrets)

        # Verification
        print("\n" + "=" * 60)
        print("VERIFICATION - Environment Variables Set:")
        print("=" * 60)

        for env_var in resolved_secrets.keys():
            value = os.environ.get(env_var, "")
            status = "SET" if value else "MISSING"
            length = len(value) if value else 0
            print("15")

        print("\n" + "=" * 60)
        print("SUCCESS: Key Vault secrets resolved and environment variables set!")
        print("=" * 60)
        print("Next steps:")
        print("1. Run your application: dotnet run --project WileyWidget.csproj")
        print("2. Verify Syncfusion license registration in console output")
        print("3. Check that all secrets are properly loaded")
        print(
            "\nNote: These environment variables are set for the current process only."
        )
        print("   They will not persist across terminal sessions.")

        # Output for PowerShell to capture
        print("\n" + "=" * 60)
        print("POWERSHELL ENVIRONMENT VARIABLE ASSIGNMENTS:")
        print("=" * 60)
        for env_var, value in resolved_secrets.items():
            # Escape single quotes in the value for PowerShell
            escaped_value = value.replace("'", "''")
            print(f"$env:{env_var} = '{escaped_value}'")

    except KeyboardInterrupt:
        logger.info("⏹️ Operation cancelled by user")
        sys.exit(0)
    except Exception as e:
        logger.error(f"❌ Unexpected error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
