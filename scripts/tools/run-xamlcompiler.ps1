<#
.SYNOPSIS
    Run XamlCompiler and capture outputs with multiple invocation methods for diagnostics

.DESCRIPTION
    Attempts three invocation methods and writes detailed logs so we can see any native console output.

.PARAMETER XamlCompilerPath
    Full path to XamlCompiler.exe

.PARAMETER InputPath
    Path to the XamlCompiler input JSON

.PARAMETER OutputPath
    Path to the XamlCompiler output JSON
#>

#Requires -Version 7.5

[CmdletBinding()]
param(
    [string]$XamlCompilerPath = "$env:USERPROFILE\\.nuget\\packages\\microsoft.windowsappsdk\\1.6.250108002\\tools\\net472\\XamlCompiler.exe",
    [string]$InputPath = "obj\\Debug\\net8.0-windows10.0.19041.0\\input.json",
    [string]$OutputPath = "obj\\Debug\\net8.0-windows10.0.19041.0\\output.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Move to project root
try {
    $projectRoot = Resolve-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath '..\\..') -ErrorAction Stop
    Set-Location -LiteralPath $projectRoot
}
catch {
    Write-Error "Unable to determine project root: $_"
    exit 2
}

Write-Output "Project root: $(Get-Location)"
Write-Output "XamlCompiler path: $XamlCompilerPath"
if (Test-Path -LiteralPath $XamlCompilerPath) { Get-Item -LiteralPath $XamlCompilerPath | Format-List Name,Length,LastWriteTime }
else { Write-Output 'XamlCompiler not found.' }

Write-Output "Input: $InputPath"
if (Test-Path -LiteralPath $InputPath) { Get-Item -LiteralPath $InputPath | Format-List Name,Length,LastWriteTime; Write-Output '--- Input tail ---'; Get-Content $InputPath -Tail 60 }
else { Write-Output 'Input file not found.' }

Write-Output "Output: $OutputPath"
if (Test-Path -LiteralPath $OutputPath) { Get-Item -LiteralPath $OutputPath | Format-List Name,Length,LastWriteTime }

# Prepare log names
$logDir = Join-Path -Path $projectRoot -ChildPath 'xaml-logs'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$psStartOut = Join-Path $logDir 'ps-start-stdout.log'
$psStartErr = Join-Path $logDir 'ps-start-stderr.log'
$cmdOut = Join-Path $logDir 'cmd-out.log'
$ampOut = Join-Path $logDir 'amp-out.log'

# Method 1: Start-Process with redirected streams
Write-Output "\n=== Method 1: Start-Process with redirected streams ==="
try {
    if (Test-Path $psStartOut) { Remove-Item $psStartOut -ErrorAction SilentlyContinue }
    if (Test-Path $psStartErr) { Remove-Item $psStartErr -ErrorAction SilentlyContinue }

    $si = New-Object System.Diagnostics.ProcessStartInfo
    $si.FileName = $XamlCompilerPath
    $si.Arguments = ('"{0}" "{1}"' -f $InputPath, $OutputPath)
    $si.RedirectStandardOutput = $true
    $si.RedirectStandardError = $true
    $si.UseShellExecute = $false
    $si.CreateNoWindow = $true

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $si
    $proc.Start() | Out-Null
    $out = $proc.StandardOutput.ReadToEnd()
    $err = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    $code1 = $proc.ExitCode

    if ($out) { $out | Out-File -FilePath $psStartOut -Encoding utf8 }
    if ($err) { $err | Out-File -FilePath $psStartErr -Encoding utf8 }

    Write-Output "Method1 ExitCode: $code1"
    if (Test-Path $psStartOut) { Write-Output '--- Method1 STDOUT ---'; Get-Content $psStartOut -Tail 200 }
    if (Test-Path $psStartErr) { Write-Output '--- Method1 STDERR ---'; Get-Content $psStartErr -Tail 200 }
}
catch {
    Write-Output "Method1 failed: $_"
}

# Method 2: Use cmd.exe /c to force native redirection
Write-Output "\n=== Method 2: cmd.exe /c redirection ==="
try {
    if (Test-Path $cmdOut) { Remove-Item $cmdOut -ErrorAction SilentlyContinue }
    $cmd = '"' + $XamlCompilerPath + '" "' + $InputPath + '" "' + $OutputPath + '"'
    $full = "/c " + $cmd + ' 1> "' + $cmdOut + '" 2>&1'
    # Run via cmd.exe
    & cmd.exe $full
    Write-Output "cmd.exe returned with exit code $LASTEXITCODE"
    if (Test-Path $cmdOut) { Write-Output '--- cmd.exe output ---'; Get-Content $cmdOut -Tail 400 }
    else { Write-Output 'No cmd.exe output file' }
}
catch {
    Write-Output "Method2 failed: $_"
}

# Method 3: Direct ampersand invocation with 2>&1 capture
Write-Output "\n=== Method 3: Direct invocation with 2>&1 capture ==="
try {
    if (Test-Path $ampOut) { Remove-Item $ampOut -ErrorAction SilentlyContinue }
    $invOut = & $XamlCompilerPath $InputPath $OutputPath 2>&1
    $exit = $LASTEXITCODE
    if ($invOut) { $invOut | Out-File -FilePath $ampOut -Encoding utf8 }
    Write-Output "Direct invocation exit code: $exit"
    if (Test-Path $ampOut) { Write-Output '--- Direct invocation output ---'; Get-Content $ampOut -Tail 400 }
    else { Write-Output 'No direct invocation output' }
}
catch {
    Write-Output "Method3 failed: $_"
}

# Summarize created logs
Write-Output "\nLog directory: $logDir"
Get-ChildItem -Path $logDir | Select-Object Name,Length | Format-Table -AutoSize

# Exit with non-zero if all methods failed
if (($code1 -ne 0) -and ($LASTEXITCODE -ne 0)) { exit 1 } else { exit 0 }
