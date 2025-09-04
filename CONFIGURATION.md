# Wiley Widget Configuration Guide

## Feature Flags

The application supports various feature flags that can be configured in `appsettings.json`:

```json
{
  "Features": {
    "EnableHealthMonitoring": false,
    "EnableResourceMonitoring": false,
    "EnablePerformanceLogging": true,
    "EnableUserActionLogging": true,
    "EnableThemeChangeLogging": true,
    "EnableSyncfusionLogging": false,
    "EnableHealthLogging": true
  }
}
```

### Feature Descriptions

- **EnableHealthMonitoring**: Enables health checks for database and system resources
- **EnableResourceMonitoring**: Monitors memory, CPU, and disk usage
- **EnablePerformanceLogging**: Logs performance metrics and timing information
- **EnableUserActionLogging**: Tracks user interactions and actions
- **EnableThemeChangeLogging**: Logs theme changes and UI customizations
- **EnableSyncfusionLogging**: Enables detailed Syncfusion component logging
- **EnableHealthLogging**: Logs health check results and system status

## Environment Variables

### License Configuration
- `SYNCFUSION_LICENSE_KEY`: Syncfusion license key (highest priority)
- `WILEYWIDGET_ENABLE_HEALTHZ`: Enable health endpoint (set to "1")

### Azure Configuration
- `AZURE_SUBSCRIPTION_ID`: Azure subscription identifier
- `AZURE_TENANT_ID`: Azure tenant identifier
- `AZURE_SQL_SERVER`: Azure SQL server name
- `AZURE_SQL_DATABASE`: Azure SQL database name
- `AZURE_SQL_RETRY_ATTEMPTS`: Number of retry attempts for SQL operations

### Third-party Services
- `QBO_CLIENT_ID`: QuickBooks Online client ID
- `QBO_CLIENT_SECRET`: QuickBooks Online client secret
- `QBO_REDIRECT_URI`: QuickBooks Online redirect URI
- `QBO_ENVIRONMENT`: QuickBooks Online environment (sandbox/production)
- `BRIGHTDATA_API_KEY`: BrightData API key
- `BRIGHTDATA_BASE_URL`: BrightData base URL (default: https://mcp.brightdata.com/)

## Command Line Arguments

- `--diag-startup`: Enable verbose startup diagnostics and metrics logging
- `--ttfw-exit`: Exit immediately after first window is shown (used for SLA testing)

## Logging

Logs are written to the `logs/` directory in the application root:

- `bootstrap.log`: Early startup logging
- `app-.log`: Human-readable application logs
- `structured-.log`: JSON-structured logs for analysis
- `errors-.log`: Error-only logs
- `startup-metrics.log`: Startup performance metrics (when `--diag-startup` is used)
- `selflog.txt`: Serilog internal diagnostics

## Health Endpoint

When enabled, the application exposes a health check endpoint via named pipe:

- Pipe Name: `wileywidget-healthz`
- Response: `{"status":"Healthy"}`
- Enable via: `--diag-startup` flag or `WILEYWIDGET_ENABLE_HEALTHZ=1` environment variable

## Startup Diagnostics

Use `--diag-startup` to enable detailed startup metrics:

- Process start time
- Bootstrap phase timing
- Core startup phase timing
- Time-to-first-window (TTFW)
- Memory and CPU usage
- Thread count

Metrics are logged to `logs/startup-metrics.log` with timestamps and elapsed times.
