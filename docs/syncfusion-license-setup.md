# Syncfusion License Setup Guide

## Overview

WileyWidget uses Syncfusion WPF controls (version 30.2.7) which require a valid license for production use. The application supports multiple license registration methods with automatic fallback.

## 🔗 Integration with Database Connection Methods

### License Configuration in Different Environments

The Syncfusion license can be configured alongside database connection methods for comprehensive environment setup:

#### Development Environment (LocalDB + License)

```env
# .env file - Development Configuration
SYNCFUSION_LICENSE_KEY=your-community-license-key-here

# Database Configuration (LocalDB)
# Uses default LocalDB connection string from appsettings.json
```

#### Production Environment (Azure SQL + License)

```env
# .env file - Production Configuration
SYNCFUSION_LICENSE_KEY=your-commercial-license-key-here

# Azure SQL Database Configuration
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=your-admin-user
AZURE_SQL_PASSWORD=your-secure-password
AZURE_SQL_RETRY_ATTEMPTS=3
```

#### Automated Setup with License

```powershell
# Complete setup including license
.\scripts\setup-license.ps1
.\scripts\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"

# Test both license and database
.\scripts\test-database-connection.ps1
```

### Configuration Priority

1. **Environment Variables** (Highest Priority)
    - `SYNCFUSION_LICENSE_KEY` environment variable
    - Works across all connection methods

2. **License File** (Medium Priority)
    - `license.key` file in application directory
    - Independent of database configuration

3. **Embedded Code** (Lowest Priority)
    - `LicenseKey.Private.cs` file
    - Development only, never commit to source control

## License Acquisition

### Step 1: Create Syncfusion Account

1. Visit: <https://www.syncfusion.com/>
2. Click "Sign In" or "Create Account"
3. Register with your email address
4. Verify your email

### Step 2: Download License Key

1. **Community License (Free)**:
    - Go to: <https://www.syncfusion.com/account/manage-license/communitylicense>
    - Click "Get Community License"
    - Copy the license key (format: starts with letter, ~90+ characters)

2. **Commercial License**:
    - Purchase a license from: <https://www.syncfusion.com/sales/license>
    - Download from your account dashboard
    - Use the provided license key

### Step 3: License Key Format

Valid Syncfusion license keys:

- Start with a letter (A-Z)
- Are approximately 90-100 characters long
- Contain alphanumeric characters
- Example: `M1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ...`

## License Registration Methods

The application supports three registration methods (tried in order):

### Method 1: Environment Variable (Recommended)

```powershell
# Set license key as environment variable (User scope)
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'YOUR_LICENSE_KEY_HERE', 'User')

# Verify the environment variable is set
[System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
```

**Advantages:**

- Secure (not stored in files)
- Works across all applications
- Easy to manage in development teams
- Can be set per-user or system-wide

### Method 2: License File

1. **Create license.key file**:

    ```
    YOUR_LICENSE_KEY_HERE
    ```

2. **Place the file**:
    - Beside the executable: `WileyWidget.exe` → `license.key`
    - Or in the application directory

**Advantages:**

- Simple file-based approach
- Easy to backup/restore
- Works in containerized environments

### Method 3: Embedded Code (Development Only)

1. **Copy the sample file**:

    ```powershell
    Copy-Item LicenseKey.Private.sample.cs LicenseKey.Private.cs
    ```

2. **Edit LicenseKey.Private.cs**:
    ```csharp
    private partial bool TryRegisterEmbeddedLicense()
    {
        string key = "YOUR_REAL_LICENSE_KEY_HERE";
        if (!string.IsNullOrWhiteSpace(key))
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key.Trim());
            return true;
        }
        return false;
    }
    ```

**⚠️ WARNING:** Never commit `LicenseKey.Private.cs` to version control!

## Verification Steps

### 1. Check License Registration

The application logs license registration status. Check the logs:

```powershell
# Open application logs
explorer "$env:APPDATA\WileyWidget\logs"
```

Look for these messages:

- ✅ `Syncfusion license registered from environment variable.`
- ✅ `Syncfusion license loaded from file.`
- ✅ `Syncfusion license registered from embedded partial.`
- ❌ `Syncfusion license NOT registered...trial mode`

### 2. Visual Verification

- **Trial Mode**: Controls show "Syncfusion Trial" watermark
- **Licensed Mode**: Clean controls without watermarks
- **About Dialog**: Check for license status information

### 3. Programmatic Verification

```powershell
# Run the license verification script
pwsh ./scripts/show-syncfusion-license.ps1
```

## Troubleshooting

### Common Issues

1. **"Invalid license key"**
    - Verify the key format (starts with letter, correct length)
    - Check for typos or extra characters
    - Ensure the key hasn't expired

2. **License not registering**
    - Check application logs for error messages
    - Verify environment variable scope (User vs Machine)
    - Ensure license.key file is in the correct location
    - Check file permissions

3. **Trial dialog appears**
    - License registration failed
    - Check logs for specific error messages
    - Verify key validity and format

### Debug Commands

```powershell
# Check environment variable
[System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')

# Test file existence
Test-Path "license.key"

# Check file contents (first/last 10 chars)
$content = Get-Content "license.key"
$content.Substring(0, 10) + "..." + $content.Substring($content.Length - 10)
```

## Development Team Setup

### Shared Development Environment

1. **Team License Key**:

    ```powershell
    # Set team license for all developers
    [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'TEAM_LICENSE_KEY', 'Machine')
    ```

2. **Individual Keys**:
    ```powershell
    # Each developer sets their own key
    [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'INDIVIDUAL_KEY', 'User')
    ```

### CI/CD Pipeline

For automated builds, set the environment variable in your CI system:

```yaml
# GitHub Actions example
- name: Set Syncfusion License
  run: |
      [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', '${{ secrets.SYNCFUSION_LICENSE }}', 'Machine')
  shell: pwsh
```

## License Management

### License Types

1. **Community License**:
    - Free for personal/educational use
    - Valid for 1 year
    - Limited to development environments

2. **Commercial License**:
    - Paid license for production use
    - Perpetual or subscription-based
    - Includes support and updates

### License Renewal

1. **Community License**:
    - Renew annually from your Syncfusion account
    - Update the license key in your environment

2. **Commercial License**:
    - Automatic renewal (subscription)
    - Manual renewal (perpetual)
    - Update key when renewed

## Security Best Practices

1. **Never commit license keys** to version control
2. **Use environment variables** for development
3. **Store production keys** securely (Azure Key Vault, etc.)
4. **Rotate keys** periodically
5. **Monitor license usage** in Syncfusion dashboard

## Support

### Syncfusion Support

- **Documentation**: <https://help.syncfusion.com/>
- **Community Forums**: <https://www.syncfusion.com/forums/>
- **Support Tickets**: <https://www.syncfusion.com/support>
- **License Portal**: <https://www.syncfusion.com/account>

### Application-Specific Help

If license issues persist:

1. Check application logs in `%APPDATA%\WileyWidget\logs`
2. Run `pwsh ./scripts/show-syncfusion-license.ps1 -Watch`
3. Verify key format and validity
4. Test with different registration methods
