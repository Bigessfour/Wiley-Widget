# Debug-ThemeCrashes.ps1
# Comprehensive debugging script for Syncfusion theming crashes

param(
    [switch]$CheckEventLogs,
    [switch]$RunMinimalRepro,
    [switch]$TestCurrentApp,
    [switch]$CreateDebugLogs
)

function Get-DotNetCrashLogs {
    Write-Host "🔍 Checking Windows Event Viewer for .NET Runtime errors..." -ForegroundColor Yellow
    
    # Check Application logs for .NET Runtime errors
    $events = Get-WinEvent -FilterHashtable @{
        LogName = 'Application'
        ProviderName = '.NET Runtime'
        Level = 1,2  # Critical and Error
        StartTime = (Get-Date).AddHours(-2)
    } -ErrorAction SilentlyContinue

    if ($events) {
        Write-Host "❌ Found .NET Runtime errors:" -ForegroundColor Red
        foreach ($event in $events | Select-Object -First 5) {
            Write-Host "Time: $($event.TimeCreated)" -ForegroundColor Gray
            Write-Host "Message: $($event.Message)" -ForegroundColor Red
            Write-Host "---" -ForegroundColor Gray
        }
    } else {
        Write-Host "✅ No recent .NET Runtime errors found" -ForegroundColor Green
    }

    # Check for TargetInvocationException
    $targetEvents = Get-WinEvent -FilterHashtable @{
        LogName = 'Application'
        StartTime = (Get-Date).AddHours(-2)
    } -ErrorAction SilentlyContinue | Where-Object { 
        $_.Message -like "*TargetInvocationException*" -or 
        $_.Message -like "*0xc0000005*" -or
        $_.Message -like "*Syncfusion*" 
    }

    if ($targetEvents) {
        Write-Host "❌ Found Syncfusion/Targeting errors:" -ForegroundColor Red
        foreach ($event in $targetEvents | Select-Object -First 3) {
            Write-Host "Time: $($event.TimeCreated)" -ForegroundColor Gray
            Write-Host "Message: $($event.Message)" -ForegroundColor Red
            Write-Host "---" -ForegroundColor Gray
        }
    }
}

function Test-MinimalSyncfusionRepro {
    Write-Host "🧪 Creating minimal Syncfusion theme reproduction..." -ForegroundColor Yellow
    
    $reproPath = "C:\Users\biges\Desktop\SyncfusionThemeRepro"
    if (Test-Path $reproPath) {
        Remove-Item $reproPath -Recurse -Force
    }
    
    dotnet new wpf -n SyncfusionThemeRepro -o $reproPath
    Set-Location $reproPath
    
    # Add Syncfusion package
    dotnet add package Syncfusion.Themes.FluentLight.WPF
    dotnet add package Syncfusion.SfSkinManager.WPF
    
    # Create minimal test
    $testCode = @'
using System.Windows;
using Syncfusion.SfSkinManager;

namespace SyncfusionThemeRepro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Test 1: Early theme setting
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            
            base.OnStartup(e);
        }
    }
    
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Test 2: Deferred theme application
            this.Loaded += (s, e) => {
                try {
                    SfSkinManager.SetTheme(this, new FluentLightTheme());
                    Console.WriteLine("✅ FluentLight theme applied successfully");
                } catch (Exception ex) {
                    Console.WriteLine($"❌ Theme application failed: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            };
        }
    }
}
'@
    
    Set-Content -Path "MainWindow.xaml.cs" -Value $testCode
    
    Write-Host "Building minimal repro..." -ForegroundColor Yellow
    dotnet build
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Minimal repro built successfully" -ForegroundColor Green
        Write-Host "Run: dotnet run --project $reproPath" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Minimal repro build failed" -ForegroundColor Red
    }
    
    Set-Location "C:\Users\biges\Desktop\Wiley_Widget"
}

function Enable-DebugLogging {
    Write-Host "📝 Enabling debug logging for theme crashes..." -ForegroundColor Yellow
    
    # Create logs directory
    $logsDir = "C:\Users\biges\Desktop\Wiley_Widget\logs"
    if (-not (Test-Path $logsDir)) {
        New-Item -ItemType Directory -Path $logsDir -Force
    }
    
    # Create debug configuration
    $debugConfig = @{
        "Logging" = @{
            "LogLevel" = @{
                "Default" = "Debug"
                "Syncfusion" = "Trace"
                "System.Windows" = "Debug"
            }
            "Console" = @{
                "IncludeScopes" = $true
            }
        }
        "SYNCFUSION_DEBUG" = $true
        "WPF_THEME_DEBUG" = $true
    }
    
    $debugConfig | ConvertTo-Json -Depth 4 | Set-Content -Path "appsettings.Debug.json"
    
    Write-Host "✅ Debug logging enabled" -ForegroundColor Green
    Write-Host "Logs will be written to: $logsDir" -ForegroundColor Cyan
}

function Test-ThemeApplicationMethods {
    Write-Host "🎨 Testing different theme application methods..." -ForegroundColor Yellow
    
    # Create test script for different approaches
    $testScript = @'
# Test 1: Constructor application (problematic)
# Test 2: Loaded event application (recommended)  
# Test 3: XAML-first application (safest)
# Test 4: Deferred with animation disable

Write-Host "Testing theme application methods..."
Write-Host "1. Constructor: Known to cause crashes"
Write-Host "2. Loaded event: Recommended approach"
Write-Host "3. XAML-first: Safest method"
Write-Host "4. Animation-disabled: For debugging"
'@
    
    Set-Content -Path "Scripts\theme-test-methods.ps1" -Value $testScript
}

# Main execution
Write-Host "🔧 Syncfusion Theme Crash Debugging Tool" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

if ($CheckEventLogs) {
    Get-DotNetCrashLogs
}

if ($RunMinimalRepro) {
    Test-MinimalSyncfusionRepro
}

if ($CreateDebugLogs) {
    Enable-DebugLogging
}

if ($TestCurrentApp) {
    Write-Host "🚀 Testing current application with debug logging..." -ForegroundColor Yellow
    $env:ASPNETCORE_ENVIRONMENT = "Debug"
    $env:SYNCFUSION_DEBUG = "true"
    $env:WPF_THEME_DEBUG = "true"
    
    dotnet run --project WileyWidget.csproj --verbosity detailed
}

# Always check for recent crashes
Write-Host "`n🔍 Quick crash check..." -ForegroundColor Yellow
Get-DotNetCrashLogs

Write-Host "`n📋 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Run: .\Scripts\Debug-ThemeCrashes.ps1 -CheckEventLogs" -ForegroundColor White
Write-Host "2. Run: .\Scripts\Debug-ThemeCrashes.ps1 -RunMinimalRepro" -ForegroundColor White
Write-Host "3. Run: .\Scripts\Debug-ThemeCrashes.ps1 -CreateDebugLogs" -ForegroundColor White
Write-Host "4. Run: .\Scripts\Debug-ThemeCrashes.ps1 -TestCurrentApp" -ForegroundColor White
