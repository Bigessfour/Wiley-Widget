<#
.SYNOPSIS
Normalize line endings in a repository in a safe, idempotent way.

.DESCRIPTION
This script normalizes text file line endings to LF or CRLF. It performs a dry-run by default
and reports which files would be changed. It skips binary files, common build dirs, and honors
Include/Exclude patterns.

.PARAMETER Path
Root path to start scanning. Default is repository root ('.').

.PARAMETER Eol
Target line ending: 'lf' or 'crlf'. Default is 'lf'.

.PARAMETER DryRun
If specified, only report changes and do not modify files.

.PARAMETER Include
Array of file patterns to include (e.g. '*.ps1','*.json'). Default is a broad set.

.PARAMETER Exclude
Array of paths or patterns to exclude (e.g. 'bin','obj','node_modules').

.EXAMPLE
# Dry run, show changes
.
\scripts\normalize-line-endings.ps1 -Path . -Eol lf -DryRun

.EXAMPLE
# Apply changes
.
\scripts\normalize-line-endings.ps1 -Path . -Eol lf
# Note: run without DryRun only after reviewing output.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [Parameter(Position = 0)]
    [string]
    $Path = '.',

    [ValidateSet('lf', 'crlf')]
    [string]
    $Eol = 'lf',

    [switch]
    $DryRun,

    [string[]]
    $Include = @('*.ps1', '*.psm1', '*.psd1', '*.psd*', '*.psm*', '*.cs', '*.xaml', '*.xaml.cs', '*.csproj', '*.sln', '*.json', '*.yml', '*.yaml', '*.xml', '*.config', '*.md', '*.py', '*.txt'),

    [string[]]
    $Exclude = @('bin', 'obj', '.git', '.venv', 'venv', 'node_modules', 'TestResults', 'coverage', 'logs')
)

begin {
    Write-Verbose "Starting normalize-line-endings.ps1 with Path='$Path', Eol='$Eol', DryRun=$DryRun"

    switch ($Eol) {
        'lf' { $description = 'LF (Unix)' }
        'crlf' { $description = 'CRLF (Windows)' }
    }

    $globExcludes = $Exclude
    $changes = [System.Collections.Generic.List[string]]::new()
}

process {
    $files = @()

    foreach ($pattern in $Include) {
        try {
            $g = Get-ChildItem -Path $Path -Recurse -File -ErrorAction Stop -Include $pattern -Force
            $files += $g
        }
        catch {
            # Use formatted string to avoid ambiguous variable parsing and show the exception message
            Write-Verbose ("Get-ChildItem failed for pattern {0}: {1}" -f $pattern, $_.Exception.Message)
        }
    }

    # Filter excludes and duplicates
    $files = $files | Where-Object {
        $full = $_.FullName
        foreach ($ex in $globExcludes) {
            if ($full -like "*${ex}*") { return $false }
        }
        return $true
    } | Sort-Object -Unique

    Write-Verbose "Found $($files.Count) candidate files to check"

    foreach ($file in $files) {
        try {
            # Skip empty files
            if ($file.Length -eq 0) { continue }

            # Read bytes to detect NUL (binary) and preserve BOM
            $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
            if ($bytes -contains 0) { Write-Verbose "Skipping binary file: $($file.FullName)"; continue }

            # Detect BOM
            $hasBom = $false
            if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $hasBom = $true }

            # Decode text (try UTF8, fallback to default encoding)
            try {
                $text = [System.Text.Encoding]::UTF8.GetString($bytes)
            }
            catch {
                $text = [System.Text.Encoding]::Default.GetString($bytes)
            }

            # Normalize line endings: first convert CRLF->LF, then handle target
            $norm = $text -replace "\r\n", "`n"

            if ($Eol -eq 'crlf') {
                $norm = $norm -replace "(?<!`r)`n", "`r`n"
            }

            if ($norm -ne $text) {
                $changes.Add($file.FullName)
                if (-not $DryRun) {
                    if ($PSCmdlet.ShouldProcess($file.FullName, "Normalize line endings to $description")) {
                        # Re-encode with UTF8 and restore BOM if present
                        $enc = [System.Text.Encoding]::UTF8
                        $outBytes = $enc.GetBytes($norm)
                        if ($hasBom) {
                            # Prepend BOM
                            $bom = [byte[]](0xEF, 0xBB, 0xBF)
                            $outBytes = $bom + $outBytes
                        }
                        [System.IO.File]::WriteAllBytes($file.FullName, $outBytes)
                        Write-Verbose "Wrote normalized file: $($file.FullName)"
                    }
                }
            }
        }
        catch {
            Write-Warning "Error processing $($file.FullName): $_"
        }
    }
}

end {
    if ($changes.Count -eq 0) {
        Write-Output "No files required normalization."
    }
    else {
        Write-Output "Files that would be changed (or were changed):"
        $changes | ForEach-Object { Write-Output " - $_" }
        Write-Output "Total: $($changes.Count)"
        if ($DryRun) { Write-Output "Dry run: no files were modified." }
        else { Write-Output "Files modified." }
    }
}
