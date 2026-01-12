# Conservative conversion helper: AppDbContext -> IDbContextFactory<AppDbContext>
# This script performs a safe, limited set of structural edits and emits a detailed report
# Manual review is required for method bodies and complex patterns — the script will
# *not* attempt to rewrite method internals automatically. Use this as an assist tool.

param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

# Files considered high-priority for migration. Update this list as needed.
$repositoryFiles = @(
    "src/WileyWidget.Data/BudgetRepository.cs",
    "src/WileyWidget.Data/AccountsRepository.cs",
    "src/WileyWidget.Data/MunicipalAccountRepository.cs",
    "src/WileyWidget.Data/DatabaseSeeder.cs",
    "src/WileyWidget.WinForms/Services/BudgetCategoryService.cs"
)

# Ensure output directory exists
if (-not (Test-Path -Path 'tmp')) { New-Item -Path 'tmp' -ItemType Directory -Force | Out-Null }

$report = [System.Collections.Generic.List[string]]::new()

foreach ($file in $repositoryFiles) {
    if (-not (Test-Path $file)) {
        $report.Add("MISSING: $file")
        continue
    }

    $report.Add("Processing: $file")

    $original = Get-Content -Raw -Encoding UTF8 -Path $file
    $modified = $original

    # 1) Replace field declarations like: private readonly AppDbContext _context;
    $modified = [System.Text.RegularExpressions.Regex]::Replace(
        $modified,
        '(^\s*private\s+readonly\s+)AppDbContext\s+_(?:context|dbContext)\s*;\s*$',
        '${1}IDbContextFactory<AppDbContext> _contextFactory;',
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )

    # 2) Replace constructor parameter types inside parameter lists: ( ... AppDbContext context, ... )
    #    -> ( ... IDbContextFactory<AppDbContext> contextFactory, ... )
    # This only edits parameter declarations inside parentheses (best-effort).
    $modified = [System.Text.RegularExpressions.Regex]::Replace(
        $modified,
        '\(([^)]*?)\bAppDbContext\s+([a-zA-Z_][\w]*)',
        '($1IDbContextFactory<AppDbContext> $2Factory',
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    # 3) Replace simple constructor assignment patterns:
    #    _context = context;  OR  _dbContext = dbContext;  ->  _contextFactory = contextFactory;
    $modified = [System.Text.RegularExpressions.Regex]::Replace(
        $modified,
        '(^\s*)_(?:context|dbContext)\s*=\s*(?:context|dbContext)\s*;\s*$',
        '${1}_contextFactory = contextFactory;',
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )

    # 4) Collect usage sites for manual review: occurrences of _context, _dbContext, AppDbContext
    $lines = $modified -split "`r?`n"
    $usageSites = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '\b(_context|_dbContext)\b' -or $line -match '\bAppDbContext\b') {
            $usageSites += "{0,4}: {1}" -f ($i + 1), $line.Trim()
        }
    }

    if ($usageSites.Count -gt 0) {
        $report.Add("  Manual changes required for usage sites: $($usageSites.Count)")
        foreach ($site in $usageSites) { $report.Add("    $site") }
        $report.Add("  Suggestion: For each method where _context/_dbContext was used, replace body with:")
        $report.Add("    // Example (inside async method):")
        $report.Add("    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);")
        $report.Add("    // use 'context' for queries and commands; add .AsNoTracking() for read-only queries")
    }

    if ($modified -ne $original) {
        if ($Apply) {
            Copy-Item -Path $file -Destination "$file.bak" -Force
            Set-Content -Path $file -Value $modified -Encoding UTF8
            $report.Add("  Applied structural changes and created backup: $file.bak")
        }
        else {
            $report.Add("  Preview: structural changes detected. Run with -Apply to apply them.")
        }
    }
    else {
        $report.Add("  No structural field/ctor/assignment changes detected.")
    }

    $report.Add("")
}

$report.Add("Conversion helper finished.")
$report.Add("IMPORTANT: This script does NOT rewrite method bodies. Please manually update each usage site listed above.")
$report.Add("For each method, create a scoped context with CreateDbContextAsync(cancellationToken) and replace _context usages with the local 'context' variable.")
$report | Out-File -FilePath 'tmp/convert-to-dbcontext-report.txt' -Encoding UTF8

if ($Apply) { Write-Host "Applied changes. See tmp/convert-to-dbcontext-report.txt." -ForegroundColor Green }
else { Write-Host "Dry run complete. Review tmp/convert-to-dbcontext-report.txt and run with -Apply to modify files." -ForegroundColor Yellow }
