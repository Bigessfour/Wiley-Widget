<#
.SYNOPSIS
    Finds and optionally updates asynchronous methods to include a CancellationToken parameter.

.DESCRIPTION
    Scans C# files for methods returning Task or ValueTask (including async methods) 
    that do not already have a CancellationToken parameter.

.PARAMETER Path
    The root directory to scan. Defaults to 'src'.

.PARAMETER Apply
    If specified, actually updates the files. Otherwise, just lists them.

.EXAMPLE
    ./Propagate-CancellationToken.ps1 -Path "src\WileyWidget.Services" -Apply
#>
param(
    [string]$Path = "src",
    [switch]$Apply
)

# Ensure absolute path
$rootPath = Resolve-Path $Path

Write-Host "Scanning $rootPath for async methods missing CancellationTokens..." -ForegroundColor Yellow

$files = Get-ChildItem -Path $rootPath -Filter *.cs -Recurse | Where-Object { 
    $_.FullName -notmatch "\\obj\\" -and 
    $_.FullName -notmatch "\\bin\\" -and 
    $_.FullName -notmatch "\\Migrations\\" 
}

$foundCount = 0
$updatedCount = 0

foreach ($file in $files) {
    try {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        # Balanced Parentheses Regex for C# method parameters
        # Handles namespaces in return types (e.g. System.Threading.Tasks.Task)
        $methodRegex = "(?m)^(\s*)((?:(?:public|private|protected|internal|static|async|virtual|override|abstract|sealed|partial|new)\s+)*)((?:[\w\.]+\.)?(?:Task(?:<.+?>)?|ValueTask(?:<.+?>)?))\s+(\w+)\s*(\((?>\((?<DEPTH>)|\)(?<-DEPTH>)|[^\(\)]*)*(?(DEPTH)(?!))\))(?=\s*[\{;=>])"
        
        $allMatches = [regex]::Matches($content, $methodRegex)
        $fileModified = $false
        $newContent = $content

        foreach ($match in $allMatches) {
            $indent = $match.Groups[1].Value
            $modifiers = $match.Groups[2].Value
            $returnType = $match.Groups[3].Value
            $methodName = $match.Groups[4].Value
            $fullParams = $match.Groups[5].Value # Includes the outer parentheses
            $params = $fullParams.Substring(1, $fullParams.Length - 2)

            # Skip if already has CancellationToken
            if ($params -match "CancellationToken") { continue }

            # Skip common non-propagation candidates
            if ($methodName -match "^(Main|DisposeAsync|GetEnumeratorAsync)$") { continue }
            
            $foundCount++
            Write-Host "MATCH: [$($file.Name)] $methodName" -ForegroundColor Cyan

            if ($Apply) {
                $newParams = $params.Trim()
                if ($newParams.Length -gt 0) {
                    $newParams = "$newParams, CancellationToken cancellationToken = default"
                } else {
                    $newParams = "CancellationToken cancellationToken = default"
                }

                $oldSig = $match.Value
                $newSig = "$indent$modifiers$returnType $methodName($newParams)"
                
                $newContent = $newContent.Replace($oldSig, $newSig)
                $fileModified = $true
            }
        }

        if ($fileModified) {
            # Add using System.Threading; if missing
            if ($newContent -notmatch "using System\.Threading;") {
                if ($newContent -match "(?m)^using .*") {
                    # Insert BEFORE the first using to be safe
                    # We use single quotes for the replacement string to avoid $& being interpreted by PowerShell
                    $regex = [regex]"(?m)^using .*"
                    $newContent = $regex.Replace($newContent, 'using System.Threading;' + [Environment]::NewLine + '$&', 1)
                } else {
                    $newContent = "using System.Threading;" + [Environment]::NewLine + $newContent
                }
                $fileModified = $true
            }
        }

        if ($Apply -and $fileModified) {
            [System.IO.File]::WriteAllText($file.FullName, $newContent)
            $updatedCount++
            Write-Host "UPDATED: $($file.FullName)" -ForegroundColor Green
        }
    } catch {
        Write-Error "Failed to process $($file.FullName): $($_.Exception.Message)"
    }
}

Write-Host "`nSummary:" -ForegroundColor Yellow
Write-Host "  Found candidates: $foundCount"
Write-Host "  Files updated:    $updatedCount"
