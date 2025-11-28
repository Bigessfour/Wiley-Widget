param(
    [Parameter(Mandatory=$true)]
    [string]$TargetPath,
    [Parameter(Mandatory=$true)]
    [string]$Content,
    [int]$Retries = 5,
    [double]$Delay = 0.05
)

# Atomic write in PowerShell using temporary file and Move-Item / Replace. Retries on failure.
if ([System.IO.Path]::IsPathRooted($TargetPath)) {
    $target = $TargetPath
} else {
    $target = Join-Path -Path (Get-Location) -ChildPath $TargetPath
}
$targetObj = New-Object System.IO.FileInfo ($target)
$dir = $targetObj.DirectoryName
if (-not (Test-Path -Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$tmp = Join-Path $dir ([System.IO.Path]::GetFileNameWithoutExtension($targetObj.Name) + '.' + [System.Guid]::NewGuid().ToString() + $targetObj.Extension + '.tmp')

$attempt = 0
while ($true) {
    try {
        Set-Content -Path $tmp -Value $Content -Force -Encoding UTF8
        Move-Item -Path $tmp -Destination $target -Force
        break
    }
    catch {
        $attempt++
        if ($attempt -ge $Retries) { throw }
        Start-Sleep -Seconds ($Delay * [math]::Pow(2, $attempt - 1))
    }
}
