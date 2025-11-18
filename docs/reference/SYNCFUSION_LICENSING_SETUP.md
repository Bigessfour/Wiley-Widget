# Syncfusion & Bold Reports Licensing Setup

This document provides the **official** setup instructions for Syncfusion and Bold Reports licensing in the Wiley Widget application, following Syncfusion's strict documentation requirements.

## ⚠️ Critical Implementation Notes

1. **Single Registration Point**: Licenses are registered **ONLY** in the `App` static constructor, per Syncfusion documentation
2. **No Runtime Registration**: License keys cannot be changed at runtime - application restart is required
3. **No Duplicate Registration**: Multiple calls to `RegisterLicense()` can cause conflicts and trial watermarks

## 🔑 License Key Requirements

### Syncfusion Community License (FREE)

- **Eligibility**: Companies/individuals with < $1M USD annual revenue
- **Coverage**: Includes both Syncfusion WPF controls AND Bold Reports
- **Get License**: https://www.syncfusion.com/account/downloads
- **Cost**: FREE

### Commercial License

- **Required**: For companies with ≥ $1M USD annual revenue
- **Contact**: Syncfusion sales team
- **Cost**: Paid license

## 🚀 Setup Instructions

### Option 1: Environment Variable (Recommended for Production)

```powershell
# Set system-wide environment variable
[Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "YOUR_LICENSE_KEY_HERE", "Machine")

# Or set for current user only
[Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "YOUR_LICENSE_KEY_HERE", "User")
```

### Option 2: Configuration File (Development)

Edit `appsettings.json`:

```json
{
  "Syncfusion": {
    "LicenseKey": "YOUR_LICENSE_KEY_HERE"
  }
}
```

### Option 3: Docker/Container Environment

In your `docker-compose.yml` or container environment:

```yaml
environment:
  - SYNCFUSION_LICENSE_KEY=YOUR_LICENSE_KEY_HERE
```

## 📋 Validation Steps

1. **Set License Key**: Use one of the methods above
2. **Restart Application**: License registration only happens at startup
3. **Check Debug Output**: Look for "✓ Syncfusion license registered from..." message
4. **Verify No Trial Dialogs**: Application should not show evaluation watermarks

## 🔍 Implementation Details

### Static Constructor Registration

The license registration happens in `App.xaml.cs` static constructor:

```csharp
static App()
{
    // Read license keys directly from environment variables
    var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

    if (!string.IsNullOrWhiteSpace(syncfusionKey))
    {
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
        // Bold Reports uses the same Community License key
        Bold.Licensing.BoldLicenseProvider.RegisterLicense(syncfusionKey);
    }
}
```

### Priority Order

1. `SYNCFUSION_LICENSE_KEY` environment variable (highest priority)
2. `appsettings.json` → `Syncfusion:LicenseKey` setting
3. No license → Trial mode (30 days)

## 🚫 Common Mistakes to Avoid

### ❌ Runtime Registration

```csharp
// WRONG - Don't do this!
SyncfusionLicenseProvider.RegisterLicense(key); // at runtime
```

### ❌ Multiple Registration Calls

```csharp
// WRONG - Don't register multiple times!
static App() { RegisterLicense(key); }
public App() { RegisterLicense(key); }      // Duplicate!
OnStartup() { RegisterLicense(key); }       // Duplicate!
```

### ❌ Late Registration

```csharp
// WRONG - Too late! Controls already instantiated
public App() {
    InitializeComponent(); // Creates controls FIRST
    RegisterLicense(key);  // TOO LATE!
}
```

## ✅ Correct Implementation

### ✅ Single Static Registration

```csharp
static App()
{
    // Register BEFORE any controls are instantiated
    var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
    if (!string.IsNullOrWhiteSpace(key))
    {
        SyncfusionLicenseProvider.RegisterLicense(key);
    }
}
```

## 🔧 Troubleshooting

### Problem: Trial dialogs still appear

**Solution**:

- Verify license key is valid (check Syncfusion account)
- Ensure environment variable is set correctly
- Restart application completely
- Check debug output for registration success/failure

### Problem: "License key is invalid" error

**Solution**:

- Get fresh license from https://www.syncfusion.com/account/downloads
- Ensure you're eligible for Community License (< $1M revenue)
- Contact Syncfusion support for license verification

### Problem: App crashes at startup

**Solution**:

- Check license key format (no extra spaces, line breaks)
- Verify environment variable name is correct: `SYNCFUSION_LICENSE_KEY`
- Check application logs for specific error messages

## 🏗️ Development vs Production

### Development

- Can use trial mode (30 days) without license
- Set license via `appsettings.json` for convenience
- Trial watermarks are expected without valid license

### Production

- **MUST** have valid license (Community or Commercial)
- Use environment variables for security
- Never commit license keys to source control

## 📚 References

- [Syncfusion WPF Licensing Documentation](https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application)
- [Syncfusion Community License](https://www.syncfusion.com/products/communitylicense)
- [Bold Reports Licensing](https://help.boldreports.com/embedded-reporting/licensing/)

## 📞 Support

For license-related issues:

1. Check this documentation first
2. Verify setup against Syncfusion's official docs
3. Contact Syncfusion support with license questions
4. For application-specific issues, check application logs

---

**Last Updated**: November 7, 2025
**Syncfusion Version**: Compatible with all versions (license registration is universal)
