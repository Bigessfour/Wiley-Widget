param(
    [string]$Root = "c:\Users\biges\Desktop\Wiley-Widget",
    [switch]$DryRun
)

Write-Host "Cleaning build artifacts under: $Root"

$bins = Get-ChildItem -Path $Root -Directory -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'bin' }
$objs = Get-ChildItem -Path $Root -Directory -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'obj' }

foreach ($d in $bins + $objs) {
    if ($d -ne $null) {
        if ($DryRun) { Write-Host "[DryRun] Would remove: $($d.FullName)" } else { Write-Host "Removing: $($d.FullName)"; Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# Remove QuestPdf native runtimes if present
$questFiles = Get-ChildItem -Path $Root -Recurse -Force -Include 'QuestPdfSkia*','libQuestPdfSkia*' -File -ErrorAction SilentlyContinue
foreach ($f in $questFiles) {
    if ($DryRun) { Write-Host "[DryRun] Would remove runtime file: $($f.FullName)" } else { Write-Host "Removing runtime file: $($f.FullName)"; Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue }
}

Write-Host "Cleanup complete."