param(
    [switch]$Quick,
    [switch]$Full,
    [switch]$Selective,
    [switch]$DryRun
)

Write-Information "🧹 WILEY WIDGET EXTENSION CLEANUP" -InformationAction Continue
Write-Information "===================================" -InformationAction Continue

if ($DryRun) {
    Write-Information "🔍 DRY RUN MODE - No extensions will be removed" -InformationAction Continue
}

# Quick cleanup - removes most problematic extensions
$quickRemove = @(
    "kreativ-software.csharpextensions",
    "karye.xaml-completion",
    "karye.xaml-snippets",
    "josefpihrt-vscode.roslynator",
    "adrianwilczynski.namespace",
    "adrianwilczynski.user-secrets",
    "ms-azuretools.vscode-cosmosdb",
    "ms-azuretools.vscode-azurestorage",
    "ms-azuretools.vscode-azureresourcegroups",
    "ms-azuretools.vscode-azure-mcp-server",
    "ms-azuretools.vscode-azure-github-copilot",
    "ms-mssql.data-workspace-vscode",
    "ms-mssql.mssql",
    "ms-mssql.sql-bindings-vscode",
    "ms-mssql.sql-database-projects-vscode",
    "ionide.ionide-fsharp",
    "ms-toolsai.jupyter",
    "spmeesseman.vscode-taskexplorer",
    "pflannery.vscode-versionlens",
    "mhutchie.git-graph",
    "streetsidesoftware.code-spell-checker",
    "erikkralj.vscode-grok",
    "formulahendry.dotnet-test-explorer",
    "forms.nunit-test-runner"
)

# Full cleanup - removes all non-essential
$fullRemove = $quickRemove + @(
    "visualstudioexptteam.vscodeintellicode",
    "yamachu.targetframeworksswitcher",
    "doggy8088.netcore-extension-pack",
    "doggy8088.netcore-snippets",
    "christian-kohler.path-intellisense",
    "codezombiech.gitignore",
    "markis.code-coverage",
    "prateekmahendrakrak.prettyxml",
    "rogalmic.vscode-xml-complete",
    "syncfusioninc.aspnetcore-vscode-extensions",
    "syncfusioninc.document-viewer-vscode-extensions",
    "tintoy.msbuild-project-tools",
    "trunk.io"
)

function Remove-Extension {
    param([string]$extensionId, [bool]$dryRun = $false)

    if ($dryRun) {
        Write-Information "  [DRY RUN] Would remove: $extensionId" -InformationAction Continue
        return
    }

    try {
        $result = code --uninstall-extension $extensionId 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "  ✅ Removed: $extensionId" -InformationAction Continue
        }
        else {
            Write-Information "  ❌ Failed to remove: $extensionId" -InformationAction Continue
            Write-Information "     Error: $result" -InformationAction Continue
        }
    }
    catch {
        Write-Information "  ❌ Error removing $extensionId : $($_.Exception.Message)" -InformationAction Continue
    }
}

if ($Quick) {
    Write-Information "`n🚀 QUICK CLEANUP MODE" -InformationAction Continue
    Write-Information "Removing $($quickRemove.Count) problematic extensions..." -InformationAction Continue

    $removed = 0
    foreach ($ext in $quickRemove) {
        Remove-Extension -extensionId $ext -dryRun $DryRun
        if (!$DryRun) { $removed++ }
    }

    Write-Information "`n✅ Quick cleanup complete! Removed $removed extensions." -InformationAction Continue

}
elseif ($Full) {
    Write-Information "`n🔥 FULL CLEANUP MODE" -InformationAction Continue
    Write-Information "Removing $($fullRemove.Count) non-essential extensions..." -InformationAction Continue
    Write-Information "⚠️  This will remove many extensions. Are you sure? (y/N): " -InformationAction Continue

    if (!$DryRun) {
        $confirm = Read-Host
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Information "Operation cancelled." -InformationAction Continue
            exit 0
        }
    }

    $removed = 0
    foreach ($ext in $fullRemove) {
        Remove-Extension -extensionId $ext -dryRun $DryRun
        if (!$DryRun) { $removed++ }
    }

    Write-Information "`n✅ Full cleanup complete! Removed $removed extensions." -InformationAction Continue

}
elseif ($Selective) {
    Write-Information "`n🎯 SELECTIVE CLEANUP MODE" -InformationAction Continue
    Write-Information "Reviewing each extension individually..." -InformationAction Continue

    $removed = 0
    foreach ($ext in $fullRemove) {
        Write-Information "`nRemove '$ext'? (y/n/skip): " -InformationAction Continue
        if (!$DryRun) {
            $choice = Read-Host
        }
        else {
            $choice = 'skip'
        }

        if ($choice -eq 'y' -or $choice -eq 'Y') {
            Remove-Extension -extensionId $ext -dryRun $DryRun
            if (!$DryRun) { $removed++ }
        }
        elseif ($choice -eq 'skip') {
            Write-Information "  ⏭️  Skipped: $ext" -InformationAction Continue
        }
        else {
            Write-Information "  ⏸️  Kept: $ext" -InformationAction Continue
        }
    }

    Write-Information "`n✅ Selective cleanup complete! Removed $removed extensions." -InformationAction Continue

}
else {
    Write-Information "`n📋 CLEANUP OPTIONS:" -InformationAction Continue
    Write-Information "  .\cleanup-extensions.ps1 -Quick      # Remove most problematic" -InformationAction Continue
    Write-Information "  .\cleanup-extensions.ps1 -Full       # Remove all non-essential" -InformationAction Continue
    Write-Information "  .\cleanup-extensions.ps1 -Selective  # Review each one" -InformationAction Continue
    Write-Information "  .\cleanup-extensions.ps1 -DryRun -Quick  # Preview what would be removed" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "💡 RECOMMENDATION: Start with -Quick -DryRun to see what would be removed" -InformationAction Continue
}

Write-Information "`n🔄 NEXT STEPS:" -InformationAction Continue
Write-Information "1. Restart VS Code completely" -InformationAction Continue
Write-Information "2. Check memory usage: code --status" -InformationAction Continue
Write-Information "3. Test your development workflow" -InformationAction Continue
Write-Information "4. If still slow, try -Full cleanup" -InformationAction Continue
