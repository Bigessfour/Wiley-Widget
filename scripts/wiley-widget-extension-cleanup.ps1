# Wiley Widget - Essential Extension Cleanup
# This script removes extension overload while keeping what you need

Write-Host "🧹 WILEY WIDGET EXTENSION CLEANUP" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green
Write-Host ""

# Extensions to KEEP (Essential for your project)
$keepExtensions = @(
    # Core .NET Development
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit",
    "ms-vscode.powershell",

    # AI Assistance
    "github.copilot",
    "github.copilot-chat",

    # Azure (only essentials)
    "ms-azuretools.vscode-azurefunctions",
    "ms-azuretools.vscode-azureappservice",

    # Utilities (lightweight)
    "usernamehw.errorlens",
    "gruntfuggly.todo-tree",
    "eamodio.gitlens",

    # XML/YAML (needed for config files)
    "redhat.vscode-xml",
    "redhat.vscode-yaml"
)

# Extensions to REMOVE (causing overload)
$removeExtensions = @(
    # Duplicate .NET tools
    "kreativ-software.csharpextensions",
    "karye.xaml-completion",
    "karye.xaml-snippets",
    "josefpihrt-vscode.roslynator",
    "adrianwilczynski.namespace",
    "adrianwilczynski.user-secrets",

    # Excessive Azure tools
    "ms-azuretools.vscode-cosmosdb",
    "ms-azuretools.vscode-azurestorage",
    "ms-azuretools.vscode-azureresourcegroups",
    "ms-azuretools.vscode-azure-mcp-server",
    "ms-azuretools.vscode-azure-github-copilot",

    # Database tools (not needed for your project)
    "ms-mssql.data-workspace-vscode",
    "ms-mssql.mssql",
    "ms-mssql.sql-bindings-vscode",
    "ms-mssql.sql-database-projects-vscode",

    # Unused languages
    "ionide.ionide-fsharp",
    "ms-toolsai.jupyter",

    # Heavy utilities
    "spmeesseman.vscode-taskexplorer",
    "pflannery.vscode-versionlens",
    "mhutchie.git-graph",
    "streetsidesoftware.code-spell-checker",
    "visualstudioexptteam.vscodeintellicode",
    "yamachu.targetframeworksswitcher",

    # Multiple AI tools (keep only Copilot)
    "erikkralj.vscode-grok",

    # Test runners (keep built-in)
    "formulahendry.dotnet-test-explorer",
    "forms.nunit-test-runner",

    # Other utilities you don't need
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

Write-Host "📦 EXTENSIONS TO KEEP ($($keepExtensions.Count)):" -ForegroundColor Green
$keepExtensions | ForEach-Object { Write-Host "  ✅ $_" -ForegroundColor White }

Write-Host ""
Write-Host "🗑️  EXTENSIONS TO REMOVE ($($removeExtensions.Count)):" -ForegroundColor Red
$removeExtensions | ForEach-Object { Write-Host "  ❌ $_" -ForegroundColor Gray }

Write-Host ""
Write-Host "🔧 CLEANUP OPTIONS:" -ForegroundColor Cyan
Write-Host "1. Quick cleanup (removes most problematic):" -ForegroundColor White
Write-Host "   Run: .\cleanup-extensions.ps1 -Quick" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Full cleanup (removes all non-essential):" -ForegroundColor White
Write-Host "   Run: .\cleanup-extensions.ps1 -Full" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Selective cleanup (review each one):" -ForegroundColor White
Write-Host "   Run: .\cleanup-extensions.ps1 -Selective" -ForegroundColor Gray

Write-Host ""
Write-Host "💡 RECOMMENDATION: Start with Quick cleanup, then restart VS Code" -ForegroundColor Yellow
Write-Host "   Check memory usage with 'code --status' after restart" -ForegroundColor Yellow
