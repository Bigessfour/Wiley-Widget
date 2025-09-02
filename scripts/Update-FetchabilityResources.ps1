# Update Fetchability Resources Script
# This script updates the fetchability-resources.json file with current repository information

param(
    [string]$OutputPath = "fetchability-resources.json",
    [switch]$Force
)

Write-Host "Updating fetchability resources..." -ForegroundColor Green

# Get repository information
$repoInfo = git log --oneline -1 --format="%H %s"
$commitHash = ($repoInfo -split ' ')[0]
$branch = git branch --show-current
$isDirty = (git status --porcelain | Measure-Object).Count -gt 0
$remoteUrl = git config --get remote.origin.url

# Get file statistics
$untrackedFiles = (git ls-files --others --exclude-standard | Measure-Object).Count
$trackedFiles = (git ls-files | Measure-Object).Count
$allUntrackedAndIgnored = (git ls-files --others | Measure-Object).Count
$ignoredFiles = $allUntrackedAndIgnored - $untrackedFiles
$totalFiles = $trackedFiles + $untrackedFiles + $ignoredFiles

# Calculate total size of tracked files
$totalSize = 0
git ls-files | ForEach-Object {
    if (Test-Path $_ -PathType Leaf) {
        $totalSize += (Get-Item $_).Length
    }
}

# Generate file list with metadata
$files = @()
git ls-files | ForEach-Object {
    $filePath = $_
    if (Test-Path $filePath -PathType Leaf) {
        $fileInfo = Get-Item $filePath
        $extension = [System.IO.Path]::GetExtension($filePath)
        try {
            $sha256 = (Get-FileHash $filePath -Algorithm SHA256).Hash.ToLower()
        } catch {
            $sha256 = "access-denied"
        }

        $files += @{
            path = $filePath
            lastModified = $fileInfo.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
            sha256 = $sha256
            tracked = $true
            extension = $extension
            size = $fileInfo.Length
        }
    }
}

# Add untracked files
git ls-files --others --exclude-standard | ForEach-Object {
    $filePath = $_
    if (Test-Path $filePath -PathType Leaf) {
        $fileInfo = Get-Item $filePath
        $extension = [System.IO.Path]::GetExtension($filePath)
        try {
            $sha256 = (Get-FileHash $filePath -Algorithm SHA256).Hash.ToLower()
        } catch {
            $sha256 = "access-denied"
        }

        $files += @{
            path = $filePath
            lastModified = $fileInfo.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
            sha256 = $sha256
            tracked = $false
            extension = $extension
            size = $fileInfo.Length
        }
    }
}

# Create the JSON structure
$jsonData = @{
    metadata = @{
        repository = @{
            commitHash = $commitHash
            branch = $branch
            isDirty = $isDirty
            remoteUrl = $remoteUrl
        }
        generatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
        generator = "Update-FetchabilityResources.ps1"
        statistics = @{
            untrackedFiles = $untrackedFiles
            totalSize = $totalSize
            totalFiles = $totalFiles
            trackedFiles = $trackedFiles
            ignoredFiles = $ignoredFiles
        }
    }
    files = $files
}

# Convert to JSON and save
$jsonString = $jsonData | ConvertTo-Json -Depth 10

if ($Force -or -not (Test-Path $OutputPath) -or (Read-Host "File exists. Overwrite? (y/n)") -eq 'y') {
    $jsonString | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "Successfully updated $OutputPath" -ForegroundColor Green
    Write-Host "Statistics:" -ForegroundColor Cyan
    Write-Host "  Total files: $totalFiles" -ForegroundColor White
    Write-Host "  Tracked files: $trackedFiles" -ForegroundColor White
    Write-Host "  Untracked files: $untrackedFiles" -ForegroundColor White
    Write-Host "  Ignored files: $ignoredFiles" -ForegroundColor White
    Write-Host "  Total size: $([math]::Round($totalSize / 1MB, 2)) MB" -ForegroundColor White
} else {
    Write-Host "Operation cancelled." -ForegroundColor Yellow
}
