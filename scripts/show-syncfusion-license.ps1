param(
  [switch]$Watch,
  [int]$IntervalSeconds = 5,
  [switch]$RevealFull,
  [switch]$Log
)

function Get-MaskedKey($key) {
  if (-not $key) { return '' }
  if ($RevealFull) { return $key }
  if ($key.Length -le 10) { return ('*' * ($key.Length - 2)) + $key.Substring($key.Length-2) }
  return $key.Substring(0,8) + '***' + $key.Substring($key.Length-6)
}

Write-Host "Syncfusion License Key Inspector" -ForegroundColor Cyan
Write-Host "Variable: SYNCFUSION_LICENSE_KEY`n" -ForegroundColor DarkCyan

function Get-LicenseFileInfo {
  try {
    $file = Get-ChildItem -Path . -Filter 'license.key' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($file) {
      $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
      return [pscustomobject]@{ Path = $file.FullName; Length = $content.Length; Key = $content }
    }
  } catch { }
  return $null
}

function Write-Log([string]$message) {
  if (-not $Log) { return }
  try {
    $dir = Join-Path $env:APPDATA 'WileyWidget/logs'
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -ErrorAction SilentlyContinue | Out-Null }
    $line = "[$(Get-Date -Format o)] $message"
    $line | Out-File -FilePath (Join-Path $dir 'license-check.log') -Append -Encoding UTF8
  } catch { }
}

if ($Watch) {
  Write-Host "Watching every $IntervalSeconds second(s). Press Ctrl+C to stop." -ForegroundColor Yellow
  while ($true) {
    try { $k = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','User') } catch { $k = $Env:SYNCFUSION_LICENSE_KEY }
    $fileInfo = Get-LicenseFileInfo
    if ([string]::IsNullOrWhiteSpace($k)) { Write-Host ("[{0}] ENV: NOT SET" -f (Get-Date -Format o)) -ForegroundColor DarkYellow } else { $masked = Get-MaskedKey $k; Write-Host ("[{0}] ENV: SET len={1} value={2}" -f (Get-Date -Format o), $k.Length, $masked) -ForegroundColor Green; Write-Log "Env key len=$($k.Length) value=$masked" }
    if ($fileInfo) { Write-Host ("[{0}] FILE: {1} len={2} value={3}" -f (Get-Date -Format o), $fileInfo.Path, $fileInfo.Length, (Get-MaskedKey $fileInfo.Key)) -ForegroundColor Cyan; Write-Log "File key len=$($fileInfo.Length) path=$($fileInfo.Path)" } else { Write-Host ("[{0}] FILE: license.key not found" -f (Get-Date -Format o)) -ForegroundColor DarkYellow }
    Start-Sleep -Seconds $IntervalSeconds
  }
} else {
  try { $k = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','User') } catch { $k = $Env:SYNCFUSION_LICENSE_KEY }
  $fileInfo = Get-LicenseFileInfo
  if ([string]::IsNullOrWhiteSpace($k)) { Write-Host "SYNCFUSION_LICENSE_KEY is NOT SET" -ForegroundColor Yellow } else { $masked = Get-MaskedKey $k; Write-Host "SYNCFUSION_LICENSE_KEY is SET length=$($k.Length) value=$masked" -ForegroundColor Green; Write-Log "Env key len=$($k.Length) value=$masked" }
  if ($fileInfo) { Write-Host "license.key found at $($fileInfo.Path) length=$($fileInfo.Length) value=$(Get-MaskedKey $fileInfo.Key)" -ForegroundColor Cyan; Write-Log "File key len=$($fileInfo.Length) path=$($fileInfo.Path)" } else { Write-Host 'license.key not found (optional)' -ForegroundColor DarkYellow }
  Write-Host "Use -Watch to monitor changes. Use -RevealFull cautiously to show full key." -ForegroundColor DarkCyan
}
