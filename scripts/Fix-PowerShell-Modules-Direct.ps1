# PowerShell Module Fix - Direct Approach
# This script directly addresses the module persistence issues

Write-Output "🔧 Direct PowerShell Module Fix"
Write-Output "=" * 40

# Step 1: Check current PSModulePath
Write-Output "`n📋 Current PSModulePath:"
$paths = $env:PSModulePath -split ';'
foreach ($path in $paths) {
    Write-Output "  $path"
}

# Step 2: Set clean PSModulePath
Write-Output "`n🔧 Setting clean PSModulePath..."
$userModules = "$env:USERPROFILE\Documents\PowerShell\Modules"
$systemModules = "C:\Program Files\PowerShell\7\Modules"

# Create user modules directory if it doesn't exist
if (-not (Test-Path $userModules)) {
    New-Item -ItemType Directory -Path $userModules -Force | Out-Null
    Write-Output "✅ Created user modules directory"
}

# Set clean PSModulePath
$env:PSModulePath = "$userModules;$systemModules"
Write-Output "✅ PSModulePath updated"

# Step 3: Check for conflicting modules
Write-Output "`n📦 Checking for module conflicts..."
$modules = @('PSScriptAnalyzer', 'Pester', 'PSReadLine')

foreach ($module in $modules) {
    $installs = Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue
    if ($installs) {
        Write-Output "  $module`: $($installs.Count) installations found"
        foreach ($install in $installs) {
            Write-Output "    $($install.ModuleBase)"
        }
    } else {
        Write-Output "  $module`: Not found"
    }
}

# Step 4: Install modules in correct location
Write-Output "`n📦 Installing modules in correct location..."

foreach ($module in $modules) {
    Write-Output "Installing $module..."
    try {
        Install-Module -Name $module -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
        Write-Output "✅ $module installed successfully"
    }
    catch {
        Write-Output "❌ Failed to install $module`: $_"
    }
}

# Step 5: Verify installations
Write-Output "`n🧪 Verifying installations..."

foreach ($module in $modules) {
    $install = Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($install) {
        Write-Output "✅ $module`: $($install.Version) at $($install.ModuleBase)"
    } else {
        Write-Output "❌ $module`: Installation failed"
    }
}

# Step 6: Test module loading
Write-Output "`n🧪 Testing module loading..."

foreach ($module in $modules) {
    try {
        Import-Module -Name $module -ErrorAction Stop
        $loaded = Get-Module -Name $module
        Write-Output "✅ $module loaded successfully (v$($loaded.Version))"
        Remove-Module -Name $module -ErrorAction SilentlyContinue
    }
    catch {
        Write-Output "❌ $module failed to load: $_"
    }
}

Write-Output "`n📋 Summary:"
Write-Output "• PSModulePath: $env:PSModulePath"
Write-Output "• User modules location: $userModules"
Write-Output "• System modules location: $systemModules"

Write-Output "`n💡 Next steps:"
Write-Output "1. Restart VS Code completely"
Write-Output "2. Close all PowerShell terminals"
Write-Output "3. Test with: Get-Module -Name PSScriptAnalyzer -ListAvailable"

Write-Output "`n✅ Direct module fix complete!"
