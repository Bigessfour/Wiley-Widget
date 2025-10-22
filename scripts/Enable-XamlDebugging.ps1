# XAML Debugging and Binary Logging Script

<#
.SYNOPSIS
    Enables comprehensive XAML debugging and binary logging for WPF applications.

.DESCRIPTION
    This script configures MSBuild and WPF tracing for detailed XAML compilation
    and runtime diagnostics. Useful for troubleshooting XamlParseException and
    XamlObjectWriterException issues.

.PARAMETER EnableTracing
    Enable WPF trace sources for runtime diagnostics.

.PARAMETER EnableBinaryLogging
    Enable MSBuild binary logging for compilation diagnostics.

.PARAMETER LogPath
    Path for binary logs and trace files.

.PARAMETER CleanLogs
    Remove existing log files before starting.

.EXAMPLE
    .\Enable-XamlDebugging.ps1 -EnableTracing -EnableBinaryLogging

.EXAMPLE
    .\Enable-XamlDebugging.ps1 -EnableBinaryLogging -LogPath "C:\Temp\XamlLogs"
#>

param(
    [switch]$EnableTracing,
    [switch]$EnableBinaryLogging,
    [string]$LogPath = "$env:TEMP\XamlDebug",
    [switch]$CleanLogs
)

# Ensure log directory exists
if (!(Test-Path $LogPath)) {
    New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
}

# Clean existing logs if requested
if ($CleanLogs) {
    Get-ChildItem $LogPath -Filter "*.binlog" -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem $LogPath -Filter "*.log" -ErrorAction SilentlyContinue | Remove-Item -Force
}

Write-Information "XAML Debugging Configuration" -InformationAction Continue
Write-Information "============================" -InformationAction Continue
Write-Information "Log Path: $LogPath" -InformationAction Continue

# Configure MSBuild binary logging
if ($EnableBinaryLogging) {
    Write-Information "`nConfiguring MSBuild Binary Logging..." -InformationAction Continue

    # Set MSBuild debug path for binary logs
    $env:MSBUILDDEBUGPATH = $LogPath
    [Environment]::SetEnvironmentVariable("MSBUILDDEBUGPATH", $LogPath, "Process")

    Write-Information "MSBUILDDEBUGPATH set to: $LogPath" -InformationAction Continue
    Write-Information "Use 'dotnet build /bl:xaml-debug.binlog' to create binary logs" -InformationAction Continue
}

# Configure WPF tracing
if ($EnableTracing) {
    Write-Information "`nConfiguring WPF Trace Sources..." -InformationAction Continue

    # Enable comprehensive WPF tracing
    $env:ENABLE_XAML_DIAGNOSTICS = "1"
    $env:WPF_TRACE_SETTINGS = "DataBinding:All;Markup:All;Resources:All"

    [Environment]::SetEnvironmentVariable("ENABLE_XAML_DIAGNOSTICS", "1", "Process")
    [Environment]::SetEnvironmentVariable("WPF_TRACE_SETTINGS", "DataBinding:All;Markup:All;Resources:All", "Process")

    Write-Information "WPF tracing enabled for:" -InformationAction Continue
    Write-Information "  - Data Binding" -InformationAction Continue
    Write-Information "  - XAML Markup" -InformationAction Continue
    Write-Information "  - Resource Resolution" -InformationAction Continue
}

# Display usage instructions
Write-Information "`nUsage Instructions:" -InformationAction Continue
Write-Information "==================" -InformationAction Continue

if ($EnableBinaryLogging) {
    Write-Information "`n1. Build with binary logging:" -InformationAction Continue
    Write-Information "   dotnet build /bl:xaml-debug.binlog" -InformationAction Continue
    Write-Information "   # Binary log will be saved to: $LogPath\xaml-debug.binlog" -InformationAction Continue

    Write-Information "`n2. View binary logs:" -InformationAction Continue
    Write-Information "   # Install MSBuild Binary Log Viewer:" -InformationAction Continue
    Write-Information "   dotnet tool install -g msbuild.binlog.viewer" -InformationAction Continue
    Write-Information "   # View logs:" -InformationAction Continue
    Write-Information "   msbuild-binlog-viewer $LogPath\xaml-debug.binlog" -InformationAction Continue
}

if ($EnableTracing) {
    Write-Information "`n3. WPF trace logs:" -InformationAction Continue
    Write-Information "   # Look for 'WPF Trace' messages during application startup" -InformationAction Continue
}

Write-Information "`n4. Additional debugging:" -InformationAction Continue
Write-Information "   # Enable XAML debugging in VS: Debug -> Options -> Debugging -> Output Window -> WPF Trace Settings" -InformationAction Continue
Write-Information "   # Check Application event logs for .NET errors" -InformationAction Continue

Write-Information "`nConfiguration Complete!" -InformationAction Continue
Write-Information "Run your application and check logs at: $LogPath" -InformationAction Continue

# Display current environment variables
Write-Information "`nCurrent Environment:" -InformationAction Continue
Write-Information "MSBUILDDEBUGPATH: $env:MSBUILDDEBUGPATH" -InformationAction Continue
Write-Information "ENABLE_XAML_DIAGNOSTICS: $env:ENABLE_XAML_DIAGNOSTICS" -InformationAction Continue
Write-Information "WPF_TRACE_SETTINGS: $env:WPF_TRACE_SETTINGS" -InformationAction Continue
