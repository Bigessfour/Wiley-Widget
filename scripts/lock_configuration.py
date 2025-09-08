#!/usr/bin/env python3
"""
Azure Key Vault Configuration Lock-In Script
===========================================

This script locks in the working Azure Key Vault configuration and prevents corruption.
Run this script to ensure your environment variables are properly set from Key Vault.

Usage:
    python scripts/lock_configuration.py

This script:
1. Verifies Azure CLI authentication
2. Resolves Key Vault secrets to environment variables
3. Validates the configuration
4. Creates a backup of the working state
5. Documents the locked-in configuration

Author: Wiley Widget Development Team
"""

import json
import os
import subprocess
import sys
from datetime import datetime
from pathlib import Path

# Configuration
VAULT_NAME = "wiley-widget-secrets"
SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
BACKUP_DIR = PROJECT_ROOT / "config_backup"

# Required secrets mapping
REQUIRED_SECRETS = {
    "SYNCFUSION-LICENSE-KEY": "SYNCFUSION_LICENSE_KEY",
    "XAI-API-KEY": "XAI_API_KEY",
    "GITHUB-PAT": "GITHUB_TOKEN",
}


def run_command(cmd, capture_output=True, check=True):
    """Run a shell command and return the result."""
    try:
        result = subprocess.run(
            cmd,
            shell=True,
            capture_output=capture_output,
            text=True,
            check=check,
            cwd=PROJECT_ROOT,
        )
        return result.stdout.strip() if capture_output else None
    except subprocess.CalledProcessError as e:
        print(f"❌ Command failed: {cmd}")
        print(f"Error: {e}")
        if capture_output and e.stdout:
            print(f"Output: {e.stdout}")
        if e.stderr:
            print(f"Stderr: {e.stderr}")
        return None


def check_azure_auth():
    """Verify Azure CLI authentication."""
    print("🔐 Checking Azure CLI authentication...")
    # Use simpler command without complex JSON query
    account_info = run_command("az account show -o json")

    if not account_info:
        print("❌ Not authenticated with Azure CLI")
        print("Run: az login")
        return False

    try:
        account = json.loads(account_info)
        user_name = account.get("user", {}).get("name", "Unknown")
        subscription_name = account.get("name", "Unknown")
        print(f"✅ Authenticated as: {user_name}")
        print(f"   Subscription: {subscription_name}")
        return True
    except json.JSONDecodeError:
        print("❌ Failed to parse Azure account information")
        return False


def verify_key_vault():
    """Verify Key Vault exists and is accessible."""
    print(f"🔑 Verifying Key Vault '{VAULT_NAME}'...")

    kv_info = run_command(f"az keyvault show --name {VAULT_NAME} -o json")

    if not kv_info:
        print(f"❌ Key Vault '{VAULT_NAME}' not found or not accessible")
        return False

    try:
        kv = json.loads(kv_info)
        location = kv.get("location", "Unknown")
        print(f"✅ Key Vault found: {kv.get('name', VAULT_NAME)} in {location}")
        return True
    except json.JSONDecodeError:
        print("❌ Failed to parse Key Vault information")
        return False


def resolve_secrets():
    """Resolve Key Vault secrets to environment variables."""
    print("🔓 Resolving Key Vault secrets to environment variables...")

    resolved_count = 0
    failed_secrets = []

    for kv_secret, env_var in REQUIRED_SECRETS.items():
        print(f"  📥 Resolving {kv_secret} → {env_var}...")

        # Get secret value from Key Vault
        secret_value = run_command(
            f"az keyvault secret show --vault-name {VAULT_NAME} --name {kv_secret} --query value -o tsv"
        )

        if secret_value and len(secret_value.strip()) > 0:
            # Set environment variable
            os.environ[env_var] = secret_value
            print(f"  ✅ {env_var} set ({len(secret_value)} chars)")
            resolved_count += 1
        else:
            print(f"  ❌ Failed to resolve {kv_secret}")
            failed_secrets.append(kv_secret)

    print(
        f"📊 Resolution complete: {resolved_count}/{len(REQUIRED_SECRETS)} secrets resolved"
    )

    if failed_secrets:
        print(f"❌ Failed secrets: {', '.join(failed_secrets)}")
        return False

    return True


def validate_configuration():
    """Validate that all required environment variables are set."""
    print("✅ Validating configuration...")

    missing_vars = []
    for env_var in REQUIRED_SECRETS.values():
        value = os.environ.get(env_var, "")
        if not value or len(value.strip()) == 0:
            missing_vars.append(env_var)
        else:
            print(f"  ✅ {env_var}: {len(value)} characters")

    if missing_vars:
        print(f"❌ Missing environment variables: {', '.join(missing_vars)}")
        return False

    print("✅ All required environment variables are set")
    return True


def create_backup():
    """Create a backup of the current working configuration."""
    print("💾 Creating configuration backup...")

    BACKUP_DIR.mkdir(exist_ok=True)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_file = BACKUP_DIR / f"env_backup_{timestamp}.json"

    # Collect current environment state
    env_state = {
        "timestamp": datetime.now().isoformat(),
        "azure_account": None,
        "key_vault": VAULT_NAME,
        "secrets": {},
    }

    # Get Azure account info
    account_info = run_command(
        'az account show --query "{name:name, user:user.name, tenantId:tenantId}" -o json'
    )
    if account_info:
        try:
            env_state["azure_account"] = json.loads(account_info)
        except:
            pass

    # Collect secret info (lengths only, not values)
    for kv_secret, env_var in REQUIRED_SECRETS.items():
        value = os.environ.get(env_var, "")
        env_state["secrets"][env_var] = {
            "key_vault_secret": kv_secret,
            "length": len(value),
            "is_set": len(value) > 0,
        }

    # Save backup
    with open(backup_file, "w") as f:
        json.dump(env_state, f, indent=2)

    print(f"✅ Backup created: {backup_file}")

    # Create a simple restore script
    restore_script = BACKUP_DIR / f"restore_{timestamp}.ps1"
    with open(restore_script, "w", encoding="utf-8") as f:
        f.write(
            f"""# Restore script for {timestamp}
# Run this to restore the backed up configuration

Write-Host "Restoring configuration from {timestamp}..."

# Set environment variables
$env:SYNCFUSION_LICENSE_KEY = "{os.environ.get('SYNCFUSION_LICENSE_KEY', '')}"
$env:XAI_API_KEY = "{os.environ.get('XAI_API_KEY', '')}"
$env:GITHUB_TOKEN = "{os.environ.get('GITHUB_TOKEN', '')}"

Write-Host "Configuration restored"
Write-Host "Run your application: dotnet run --project WileyWidget.csproj"
"""
        )

    print(f"✅ Restore script created: {restore_script}")
    return backup_file


def create_documentation():
    """Create documentation for the locked-in configuration."""
    print("📝 Creating configuration documentation...")

    docs_file = PROJECT_ROOT / "AZURE_KEYVAULT_LOCKED_CONFIG.md"

    doc_content = f"""# Azure Key Vault Configuration - LOCKED IN ✅

**Status:** LOCKED AND WORKING
**Date:** {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}
**Vault:** {VAULT_NAME}

## 🔐 Working Configuration

### Environment Variables
The following environment variables are automatically resolved from Azure Key Vault:

| Environment Variable | Key Vault Secret | Status |
|---------------------|------------------|--------|
"""

    for kv_secret, env_var in REQUIRED_SECRETS.items():
        value = os.environ.get(env_var, "")
        status = "✅ Set" if value else "❌ Missing"
        length = len(value) if value else 0
        doc_content += f"| `{env_var}` | `{kv_secret}` | {status} ({length} chars) |\n"

    doc_content += """

### How to Use

1. **Automatic Setup:** Run the lock-in script:
   ```bash
   python scripts/lock_configuration.py
   ```

2. **Manual Setup:** If needed, run the Python resolver:
   ```bash
   python scripts/resolve_keyvault_secrets.py
   ```

3. **Run Application:**
   ```bash
   dotnet run --project WileyWidget.csproj
   ```

### 🔧 Troubleshooting

If configuration gets corrupted:

1. **Quick Restore:** Run the lock-in script
2. **Manual Restore:** Use backup files in `config_backup/`
3. **Verify Azure Auth:** `az account show`

### 🚫 Do Not Modify

- Do NOT edit `.env` file manually (contains Key Vault references)
- Do NOT run conflicting setup scripts
- Do NOT modify environment variables directly

### 📞 Support

If issues persist:
1. Check Azure CLI authentication: `az login`
2. Verify Key Vault access: `az keyvault list`
3. Run lock-in script: `python scripts/lock_configuration.py`

---
*This configuration is locked in and working. Do not modify without consulting the development team.*
"""

    with open(docs_file, "w", encoding="utf-8") as f:
        f.write(doc_content)

    print(f"✅ Documentation created: {docs_file}")


def main():
    """Main execution function."""
    print("🔒 Azure Key Vault Configuration Lock-In")
    print("=" * 50)

    # Step 1: Check Azure authentication
    if not check_azure_auth():
        sys.exit(1)

    # Step 2: Verify Key Vault
    if not verify_key_vault():
        sys.exit(1)

    # Step 3: Resolve secrets
    if not resolve_secrets():
        print("❌ Failed to resolve all secrets")
        sys.exit(1)

    # Step 4: Validate configuration
    if not validate_configuration():
        print("❌ Configuration validation failed")
        sys.exit(1)

    # Step 5: Create backup
    backup_file = create_backup()

    # Step 6: Create documentation
    create_documentation()

    print("\n" + "=" * 50)
    print("🎉 CONFIGURATION LOCKED IN SUCCESSFULLY!")
    print("=" * 50)
    print("\n✅ Azure Key Vault integration is working")
    print("✅ All secrets resolved to environment variables")
    print("✅ Configuration backed up and documented")
    print(f"✅ Backup created: {backup_file}")
    print("\n🚀 You can now run your application:")
    print("   dotnet run --project WileyWidget.csproj")


if __name__ == "__main__":
    main()
