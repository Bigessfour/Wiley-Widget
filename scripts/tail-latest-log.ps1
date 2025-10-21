param(
    [int]$Lines = 200,
    [string]$Folder = "logs"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-WorkspacePath {
    param([string]$Path)
    # Convert to full path and ensure drive letter is uppercase for case-sensitive gatekeepers
    $full = [System.IO.Path]::GetFullPath($Path)
    if ($full.Length -ge 2 -and $full[1] -eq ':') {
        return ($full[0].ToString().ToUpper() + $full.Substring(1))
    }
    return $full
}

try {
    $workspace = Convert-WorkspacePath "$PSScriptRoot\.."
    $logDir = Convert-WorkspacePath (Join-Path $workspace $Folder)

    if (-not (Test-Path $logDir)) {
        Write-Warning "Log directory not found: $logDir"
        return
    }

    $logs = Get-ChildItem -Path $logDir -File -Filter '*.log' | Sort-Object LastWriteTime -Descending
    if (-not $logs) {
        Write-Warning "No .log files found under $logDir"
        return
    }

    $latest = $logs[0].FullName
    $latest = Convert-WorkspacePath $latest
    Write-Host "📄 Latest log: $latest" -ForegroundColor Cyan
    Write-Host "— Last $Lines lines —" -ForegroundColor DarkGray
    Get-Content -Path $latest -Tail $Lines -Wait:$false
}
catch {
    Write-Error "Failed to read latest log: $($_.Exception.Message)"
}
