# VS Code Extension Management Script
# Run this to audit and clean up extensions

Write-Information "🔍 VS Code Extension Audit" -InformationAction Continue
Write-Information "==========================" -InformationAction Continue

# Get all extensions
$extensions = code --list-extensions

Write-Information "`n📊 Current Extension Count: $($extensions.Count)" -InformationAction Continue

# Categorize extensions
$categories = @{
    "Azure"     = $extensions | Where-Object { $_ -like "ms-azuretools.*" }
    "DotNet"    = $extensions | Where-Object { $_ -like "ms-dotnettools.*" -or $_ -like "karye.*" -or $_ -like "kreativ-software.*" }
    "Git"       = $extensions | Where-Object { $_ -like "*git*" -or $_ -like "eamodio.*" -or $_ -like "mhutchie.*" }
    "Database"  = $extensions | Where-Object { $_ -like "ms-mssql.*" }
    "AI"        = $extensions | Where-Object { $_ -like "*copilot*" -or $_ -like "*grok*" }
    "Languages" = $extensions | Where-Object { $_ -like "redhat.*" -or $_ -like "ms-toolsai.*" -or $_ -like "ionide.*" }
    "Utilities" = $extensions | Where-Object { $_ -like "gruntfuggly.*" -or $_ -like "usernamehw.*" -or $_ -like "streetsidesoftware.*" }
    "Other"     = $extensions | Where-Object {
        $_ -notlike "ms-azuretools.*" -and
        $_ -notlike "ms-dotnettools.*" -and
        $_ -notlike "karye.*" -and
        $_ -notlike "kreativ-software.*" -and
        $_ -notlike "*git*" -and
        $_ -notlike "eamodio.*" -and
        $_ -notlike "mhutchie.*" -and
        $_ -notlike "ms-mssql.*" -and
        $_ -notlike "*copilot*" -and
        $_ -notlike "*grok*" -and
        $_ -notlike "redhat.*" -and
        $_ -notlike "ms-toolsai.*" -and
        $_ -notlike "ionide.*" -and
        $_ -notlike "gruntfuggly.*" -and
        $_ -notlike "usernamehw.*" -and
        $_ -notlike "streetsidesoftware.*"
    }
}

Write-Information "`n📂 Extension Categories:" -InformationAction Continue
foreach ($category in $categories.GetEnumerator()) {
    Write-Information "  $($category.Key): $($category.Value.Count) extensions" -InformationAction Continue
    if ($category.Value.Count -gt 0) {
        $category.Value | ForEach-Object { Write-Information "    - $_" -InformationAction Continue }
    }
}

Write-Information "`n💡 Recommendations:" -InformationAction Continue
Write-Information "1. Azure Tools: Keep only actively used (Azure Functions, App Service)" -InformationAction Continue
Write-Information "2. .NET Tools: Essential - keep C# Dev Kit, remove duplicates" -InformationAction Continue
Write-Information "3. Git Tools: Keep GitLens, remove Git Graph if not needed" -InformationAction Continue
Write-Information "4. Database: Keep only if actively developing database features" -InformationAction Continue
Write-Information "5. AI Tools: Keep Copilot, remove others if causing conflicts" -InformationAction Continue
Write-Information "6. Languages: Keep XML/YAML, remove F#/Jupyter if not needed" -InformationAction Continue

Write-Information "`n🔧 Quick Cleanup Commands:" -InformationAction Continue
Write-Information "# Remove Azure overload (keep essentials):" -InformationAction Continue
Write-Information "code --uninstall-extension ms-azuretools.vscode-cosmosdb" -InformationAction Continue
Write-Information "code --uninstall-extension ms-azuretools.vscode-azurestorage" -InformationAction Continue
Write-Information "code --uninstall-extension ms-azuretools.vscode-azureresourcegroups" -InformationAction Continue

Write-Information "`n# Remove duplicate .NET tools:" -InformationAction Continue
Write-Information "code --uninstall-extension kreativ-software.csharpextensions" -InformationAction Continue
Write-Information "code --uninstall-extension karye.xaml-snippets" -InformationAction Continue

Write-Information "`n# Remove unused languages:" -InformationAction Continue
Write-Information "code --uninstall-extension ionide.ionide-fsharp" -InformationAction Continue
Write-Information "code --uninstall-extension ms-toolsai.jupyter" -InformationAction Continue

Write-Information "`n# Remove utility overload:" -InformationAction Continue
Write-Information "code --uninstall-extension spmeesseman.vscode-taskexplorer" -InformationAction Continue
Write-Information "code --uninstall-extension pflannery.vscode-versionlens" -InformationAction Continue

Write-Information "`n✅ After cleanup, restart VS Code and check memory usage with 'code --status'" -InformationAction Continue
