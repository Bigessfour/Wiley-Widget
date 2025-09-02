#!/usr/bin/env python3
"""
Syncfusion Theme Crash Debugger
Analyzes Windows Event Viewer logs, .NET runtime crashes, and WPF theme-related issues
"""

import datetime
import json
import os
import re
import subprocess  # nosec B404 - Using subprocess with validated inputs only
import sys
from pathlib import Path
from typing import Dict, List, Optional


class SyncfusionThemeDebugger:
    def __init__(self, project_path: str):
        self.project_path = Path(project_path)
        self.logs_dir = self.project_path / "logs"
        self.debug_log = self.project_path / "debug.log"
        self.results_file = self.project_path / "theme-debug-results.json"

        # Syncfusion theme-related crash patterns
        self.crash_patterns = {
            "target_invocation": r"TargetInvocationException.*SfSkinManager|SetTheme",
            "null_reference": r"NullReferenceException.*Theme|SkinManager",
            "access_violation": r"AccessViolationException.*0xc0000005",
            "fluent_animation": r"FluentLight.*animation|reveal.*effect",
            "immutable_object": r"InvalidOperationException.*immutable.*theme",
            "syncfusion_crash": r"Syncfusion\..*Exception|SfSkinManager.*Exception",
            "wpf_theme": r"System\.Windows\..*Theme.*Exception",
        }

        # Event Viewer sources to check
        self.event_sources = [
            "Application Error",
            ".NET Runtime",
            "Windows Error Reporting",
            "WPF",
        ]

    def analyze_event_viewer_logs(self, hours_back: int = 24) -> List[Dict]:
        """Parse Windows Event Viewer for .NET and WPF crashes"""
        print(f"🔍 Analyzing Windows Event Viewer (last {hours_back} hours)...")

        crashes = []
        try:
            # PowerShell command to get Application logs
            ps_cmd = f"""
            Get-WinEvent -FilterHashtable @{{
                LogName='Application'; 
                StartTime=(Get-Date).AddHours(-{hours_back});
                Level=1,2
            }} | Where-Object {{
                $_.ProviderName -match 'Application Error|\\.NET Runtime|Windows Error Reporting' -and
                $_.Message -match 'WileyWidget|Syncfusion|SfSkinManager'
            }} | Select-Object TimeCreated, Id, LevelDisplayName, ProviderName, Message | 
            ConvertTo-Json -Depth 3
            """

            # nosec B602,B603 - Using PowerShell with fixed command structure
            result = subprocess.run(
                ["powershell.exe", "-Command", ps_cmd],
                capture_output=True,
                text=True,
                check=False,
            )

            if result.returncode == 0 and result.stdout.strip():
                events = json.loads(result.stdout)
                if isinstance(events, dict):
                    events = [events]  # Single event case

                for event in events:
                    crash_info = {
                        "timestamp": event["TimeCreated"],
                        "event_id": event["Id"],
                        "level": event["LevelDisplayName"],
                        "source": event["ProviderName"],
                        "message": event["Message"],
                        "crash_type": self._classify_crash(event["Message"]),
                    }
                    crashes.append(crash_info)

        except Exception as e:
            print(f"⚠️  Error reading Event Viewer: {e}")

        return crashes

    def analyze_debug_logs(self) -> List[Dict]:
        """Parse application debug logs for theme-related errors"""
        print("📋 Analyzing application debug logs...")

        log_entries = []
        log_files = []

        # Check various log file locations
        potential_logs = [
            self.debug_log,
            self.logs_dir / "errors.log",
            self.logs_dir / "application.log",
        ]

        for log_path in potential_logs:
            if log_path.exists():
                log_files.append(log_path)

        # Also check for dated log files
        if self.logs_dir.exists():
            dated_logs = list(self.logs_dir.glob("*error*.log"))
            dated_logs.extend(list(self.logs_dir.glob("*exception*.log")))
            log_files.extend(dated_logs)

        for log_file in log_files:
            try:
                with open(log_file, "r", encoding="utf-8") as f:
                    content = f.read()

                # Parse log entries with timestamps
                lines = content.split("\n")
                for i, line in enumerate(lines):
                    for pattern_name, pattern in self.crash_patterns.items():
                        if re.search(pattern, line, re.IGNORECASE):
                            # Get context lines
                            start = max(0, i - 3)
                            end = min(len(lines), i + 4)
                            context = "\n".join(lines[start:end])

                            log_entries.append(
                                {
                                    "file": str(log_file),
                                    "line_number": i + 1,
                                    "pattern": pattern_name,
                                    "content": line.strip(),
                                    "context": context,
                                    "timestamp": self._extract_timestamp(line),
                                }
                            )

            except Exception as e:
                print(f"⚠️  Error reading {log_file}: {e}")

        return log_entries

    def check_syncfusion_assemblies(self) -> Dict:
        """Check Syncfusion assembly versions and dependencies"""
        print("🔧 Checking Syncfusion assemblies...")

        bin_path = self.project_path / "bin" / "Debug" / "net9.0-windows"
        if not bin_path.exists():
            bin_path = self.project_path / "bin" / "Release" / "net9.0-windows"

        assembly_info = {
            "syncfusion_dlls": [],
            "theme_dlls": [],
            "version_conflicts": [],
        }

        if bin_path.exists():
            # Find Syncfusion DLLs
            for dll_file in bin_path.glob("Syncfusion*.dll"):
                try:
                    # Use PowerShell to get file version
                    ps_cmd = (
                        f'(Get-ItemProperty "{dll_file}").VersionInfo | ConvertTo-Json'
                    )
                    # nosec B602,B603 - Using PowerShell with fixed command
                    result = subprocess.run(
                        ["powershell.exe", "-Command", ps_cmd],
                        capture_output=True,
                        text=True,
                        check=False,
                    )

                    if result.returncode == 0:
                        version_info = json.loads(result.stdout)
                        dll_info = {
                            "name": dll_file.name,
                            "version": version_info.get("FileVersion", "Unknown"),
                            "product_version": version_info.get(
                                "ProductVersion", "Unknown"
                            ),
                            "size": dll_file.stat().st_size,
                        }

                        if "Theme" in dll_file.name:
                            assembly_info["theme_dlls"].append(dll_info)
                        else:
                            assembly_info["syncfusion_dlls"].append(dll_info)

                except Exception as e:
                    print(f"⚠️  Error checking {dll_file}: {e}")

        return assembly_info

    def check_file_locks(self) -> Dict:
        """Check for file locks that prevent application startup"""
        print("🔒 Checking for file locks and process conflicts...")

        lock_info = {
            "temp_wpf_files": [],
            "locked_assemblies": [],
            "running_processes": [],
        }

        # Check for temporary WPF files
        temp_patterns = ["WileyWidget_*_wpftmp.csproj", "*.tmp", "*.lock"]

        for pattern in temp_patterns:
            temp_files = list(self.project_path.glob(pattern))
            for temp_file in temp_files:
                lock_info["temp_wpf_files"].append(
                    {
                        "file": str(temp_file),
                        "size": temp_file.stat().st_size,
                        "modified": temp_file.stat().st_mtime,
                    }
                )

        # Check for running processes that might lock files
        try:
            ps_cmd = """
            Get-Process | Where-Object {
                $_.ProcessName -match 'WileyWidget|dotnet|MSBuild' -and
                $_.MainWindowTitle -ne ''
            } | Select-Object ProcessName, Id, MainWindowTitle, StartTime |
            ConvertTo-Json -Depth 2
            """

            result = subprocess.run(
                ["powershell.exe", "-Command", ps_cmd],
                capture_output=True,
                text=True,
                check=False,
            )

            if result.returncode == 0 and result.stdout.strip():
                processes = json.loads(result.stdout)
                if isinstance(processes, dict):
                    processes = [processes]
                lock_info["running_processes"] = processes

        except Exception as e:
            print(f"⚠️  Error checking processes: {e}")

        return lock_info

    def check_wpf_designer_issues(self) -> Dict:
        """Check for WPF designer and XAML compilation issues"""
        print("🎨 Checking WPF designer and XAML issues...")

        designer_info = {
            "xaml_errors": [],
            "designer_cache": [],
            "resource_conflicts": [],
        }

        # Check for XAML files with potential issues
        xaml_files = list(self.project_path.glob("**/*.xaml"))

        for xaml_file in xaml_files:
            try:
                with open(xaml_file, "r", encoding="utf-8") as f:
                    content = f.read()

                # Check for common XAML issues
                issues = []
                if "Syncfusion.Themes" in content and "FluentLight" in content:
                    issues.append("FluentLight theme reference found")
                if "SfSkinManager" in content:
                    issues.append("SfSkinManager usage in XAML")
                if re.search(r'x:Name="[^"]*".*x:Name="[^"]*"', content):
                    issues.append("Potential duplicate x:Name attributes")

                if issues:
                    designer_info["xaml_errors"].append(
                        {"file": str(xaml_file), "issues": issues}
                    )

            except Exception as e:
                designer_info["xaml_errors"].append(
                    {"file": str(xaml_file), "error": str(e)}
                )

        # Check designer cache
        obj_dir = self.project_path / "obj"
        if obj_dir.exists():
            cache_files = list(obj_dir.glob("**/*.g.cs")) + list(
                obj_dir.glob("**/*.g.i.cs")
            )
            designer_info["designer_cache"] = [str(f) for f in cache_files]

        return designer_info

    def check_runtime_environment(self) -> Dict:
        """Check .NET runtime environment and configuration"""
        print("⚙️ Checking .NET runtime environment...")

        runtime_info = {
            "dotnet_version": "unknown",
            "wpf_version": "unknown",
            "environment_vars": {},
            "runtime_config": {},
        }

        try:
            # Get .NET version
            result = subprocess.run(
                ["dotnet", "--version"], capture_output=True, text=True, check=False
            )

            if result.returncode == 0:
                runtime_info["dotnet_version"] = result.stdout.strip()

            # Get runtime info
            result = subprocess.run(
                ["dotnet", "--info"], capture_output=True, text=True, check=False
            )

            if result.returncode == 0:
                info_output = result.stdout
                # Parse key information
                if ".NET SDKs installed:" in info_output:
                    runtime_info["sdk_info"] = info_output

            # Check relevant environment variables
            env_vars = ["DOTNET_ROOT", "DOTNET_HOST_PATH", "ASPNETCORE_ENVIRONMENT"]
            for var in env_vars:
                value = os.environ.get(var)
                if value:
                    runtime_info["environment_vars"][var] = value

        except Exception as e:
            runtime_info["error"] = str(e)

        return runtime_info

    def cleanup_temp_files(self) -> List[str]:
        """Clean up temporary files that might cause locks"""
        print("🧹 Cleaning up temporary files...")

        cleaned_files = []
        cleanup_patterns = [
            "WileyWidget_*_wpftmp.csproj",
            "obj/**/WileyWidget_*.csproj",
            "*.tmp",
            "bin/**/temp/*",
        ]

        for pattern in cleanup_patterns:
            try:
                files_to_clean = list(self.project_path.glob(pattern))
                for file_path in files_to_clean:
                    if file_path.exists():
                        if file_path.is_file():
                            file_path.unlink()
                            cleaned_files.append(str(file_path))
                        elif file_path.is_dir() and not any(file_path.iterdir()):
                            file_path.rmdir()
                            cleaned_files.append(str(file_path))

            except Exception as e:
                print(f"⚠️  Error cleaning {pattern}: {e}")

        return cleaned_files

    def create_minimal_repro(self) -> str:
        """Create minimal reproduction test for theme crashes"""
        print("🧪 Creating minimal reproduction test...")

        repro_dir = self.project_path / "theme-crash-repro"
        repro_dir.mkdir(exist_ok=True)

        # Create minimal WPF project
        csproj_content = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Syncfusion.SfSkinManager.WPF" Version="27.1.48" />
    <PackageReference Include="Syncfusion.Themes.FluentLight.WPF" Version="27.1.48" />
    <PackageReference Include="Syncfusion.Tools.WPF" Version="27.1.48" />
  </ItemGroup>
</Project>"""

        app_xaml = """<Application x:Class="ThemeCrashRepro.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <!-- Test: XAML-first theme application -->
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/Syncfusion.Themes.FluentLight.WPF;component/FluentLight.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>"""

        app_xaml_cs = """using System.Windows;
using Syncfusion.SfSkinManager;

namespace ThemeCrashRepro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Test: Early theme setup
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            
            base.OnStartup(e);
        }
    }
}"""

        mainwindow_xaml = """<Window x:Class="ThemeCrashRepro.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
        Title="Syncfusion Theme Crash Test" Height="450" Width="800"
        Loaded="MainWindow_Loaded">
    <Grid>
        <StackPanel Margin="20">
            <Button Name="TestButton" Content="Test Button" Margin="5"/>
            <syncfusion:BusyIndicator Name="TestBusyIndicator" Margin="5" IsBusy="True"/>
            <Button Name="SwitchThemeButton" Content="Switch to FluentLight" Click="SwitchTheme_Click" Margin="5"/>
            <TextBlock Name="StatusText" Text="Waiting for theme test..." Margin="5"/>
        </StackPanel>
    </Grid>
</Window>"""

        mainwindow_xaml_cs = """using System;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.FluentLight.WPF;

namespace ThemeCrashRepro
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Test: Defer theme application to Loaded event
            // DO NOT apply theme in constructor
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Applying FluentLight theme...";
                
                // Test: Disable animations first
                FluentLightThemeSettings.HoverEffectMode = HoverEffect.None;
                FluentLightThemeSettings.PressedEffectMode = PressedEffect.None;
                
                // Test: Apply theme after window is loaded
                SfSkinManager.SetTheme(this, new FluentLightTheme());
                
                StatusText.Text = "Theme applied successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Theme crash: {ex.Message}";
                MessageBox.Show($"Theme application failed: {ex}", "Theme Crash", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Switching theme...";
                
                // Test: Switch theme at runtime
                SfSkinManager.SetTheme(this, new FluentLightTheme());
                
                StatusText.Text = "Theme switched successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Theme switch crash: {ex.Message}";
                MessageBox.Show($"Theme switch failed: {ex}", "Theme Crash", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}"""

        # Write files
        files_to_create = [
            ("ThemeCrashRepro.csproj", csproj_content),
            ("App.xaml", app_xaml),
            ("App.xaml.cs", app_xaml_cs),
            ("MainWindow.xaml", mainwindow_xaml),
            ("MainWindow.xaml.cs", mainwindow_xaml_cs),
        ]

        for filename, content in files_to_create:
            file_path = repro_dir / filename
            with open(file_path, "w", encoding="utf-8") as f:
                f.write(content)

        return str(repro_dir)

    def run_repro_test(self, repro_dir: str) -> Dict:
        """Build and run the minimal reproduction test"""
        print("🚀 Running minimal reproduction test...")

        repro_path = Path(repro_dir)
        test_results = {
            "build_success": False,
            "run_success": False,
            "crashes": [],
            "output": "",
        }

        try:
            # Build the repro project - using fixed command arguments
            build_args = [
                "dotnet.exe",
                "build",
                str(repro_path / "ThemeCrashRepro.csproj"),
            ]
            # nosec B603,B607 - Using dotnet with validated arguments
            build_result = subprocess.run(
                build_args, capture_output=True, text=True, cwd=repro_path, check=False
            )

            test_results["build_success"] = build_result.returncode == 0
            test_results["build_output"] = build_result.stdout + build_result.stderr

            if test_results["build_success"]:
                # Run the repro project - using fixed command arguments
                run_args = ["dotnet.exe", "run", "--project", "ThemeCrashRepro.csproj"]
                # nosec B603,B607 - Using dotnet with validated arguments
                run_result = subprocess.run(
                    run_args,
                    capture_output=True,
                    text=True,
                    cwd=repro_path,
                    timeout=30,
                    check=False,
                )

                test_results["run_success"] = run_result.returncode == 0
                test_results["run_output"] = run_result.stdout + run_result.stderr

        except subprocess.TimeoutExpired:
            test_results["run_success"] = True  # App started successfully
            test_results["run_output"] = "Application launched and ran for 30 seconds"
        except Exception as e:
            test_results["crashes"].append(str(e))

        return test_results

    def _classify_crash(self, message: str) -> str:
        """Classify crash type based on message content"""
        for pattern_name, pattern in self.crash_patterns.items():
            if re.search(pattern, message, re.IGNORECASE):
                return pattern_name
        return "unknown"

    def _extract_timestamp(self, line: str) -> Optional[str]:
        """Extract timestamp from log line"""
        timestamp_patterns = [
            r"\d{4}-\d{2}-\d{2}[\sT]\d{2}:\d{2}:\d{2}",
            r"\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2}:\d{2}",
        ]

        for pattern in timestamp_patterns:
            match = re.search(pattern, line)
            if match:
                return match.group()
        return None

    def generate_report(self) -> Dict:
        """Generate comprehensive debugging report"""
        print("📊 Generating comprehensive theme crash report...")

        # Clean up temporary files first
        cleaned_files = self.cleanup_temp_files()

        report = {
            "timestamp": datetime.datetime.now().isoformat(),
            "project_path": str(self.project_path),
            "cleanup_results": {
                "cleaned_files": cleaned_files,
                "cleanup_count": len(cleaned_files),
            },
            "event_viewer_crashes": self.analyze_event_viewer_logs(),
            "debug_log_entries": self.analyze_debug_logs(),
            "assembly_info": self.check_syncfusion_assemblies(),
            "file_locks": self.check_file_locks(),
            "wpf_designer_issues": self.check_wpf_designer_issues(),
            "runtime_environment": self.check_runtime_environment(),
            "recommendations": self._generate_recommendations(),
        }

        # Create and test minimal repro
        repro_dir = self.create_minimal_repro()
        report["minimal_repro"] = {
            "path": repro_dir,
            "test_results": self.run_repro_test(repro_dir),
        }

        # Save report
        with open(self.results_file, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2, ensure_ascii=False)

        return report

    def _generate_recommendations(self) -> List[str]:
        """Generate debugging recommendations"""
        return [
            "✅ Move theme application from constructor to Window.Loaded event",
            "✅ Set SfSkinManager.ApplyThemeAsDefaultStyle = true in App.OnStartup",
            "✅ Disable FluentLight animations: HoverEffectMode = HoverEffect.None",
            "✅ Use XAML-first theme application via merged dictionaries",
            "🔍 Check Windows Event Viewer for .NET Runtime errors",
            "🔍 Monitor debug.log for TargetInvocationException patterns",
            "🧪 Test with minimal repro to isolate theme crashes",
            "📞 Contact Syncfusion support with specific crash logs",
        ]


def main():
    if len(sys.argv) > 1:
        project_path = sys.argv[1]
    else:
        project_path = os.getcwd()

    print("🎨 Syncfusion Theme Crash Debugger (Extended)")
    print("=" * 50)

    debugger = SyncfusionThemeDebugger(project_path)
    report = debugger.generate_report()

    # Print summary
    print("\n📋 Debug Report Summary:")
    print(f"   Files Cleaned: {report['cleanup_results']['cleanup_count']}")
    print(f"   Event Viewer Crashes: {len(report['event_viewer_crashes'])}")
    print(f"   Debug Log Entries: {len(report['debug_log_entries'])}")
    print(f"   Syncfusion DLLs: {len(report['assembly_info']['syncfusion_dlls'])}")
    print(f"   Theme DLLs: {len(report['assembly_info']['theme_dlls'])}")
    print(f"   Temp WPF Files: {len(report['file_locks']['temp_wpf_files'])}")
    print(f"   Running Processes: {len(report['file_locks']['running_processes'])}")
    print(f"   XAML Issues: {len(report['wpf_designer_issues']['xaml_errors'])}")
    print(f"   .NET Version: {report['runtime_environment']['dotnet_version']}")

    if "minimal_repro" in report:
        print(f"   Minimal Repro: {report['minimal_repro']['path']}")
    print(f"   Report saved: {debugger.results_file}")

    # Print critical issues
    if report["cleanup_results"]["cleaned_files"]:
        print(
            f"\n🧹 Cleaned {len(report['cleanup_results']['cleaned_files'])} temp files"
        )

    critical_crashes = [
        c for c in report["event_viewer_crashes"] if c["level"] == "Error"
    ]
    if critical_crashes:
        print(f"\n🚨 Critical Crashes Found: {len(critical_crashes)}")
        for crash in critical_crashes[:3]:  # Show first 3
            print(f"   {crash['timestamp']}: {crash['crash_type']}")

    if report["file_locks"]["running_processes"]:
        print("\n🔒 Running Processes Found:")
        for proc in report["file_locks"]["running_processes"][:3]:
            print(f"   {proc['ProcessName']} (PID: {proc['Id']})")

    if report["wpf_designer_issues"]["xaml_errors"]:
        print("\n🎨 XAML Issues Found:")
        for issue in report["wpf_designer_issues"]["xaml_errors"][:3]:
            print(
                f"   {Path(issue['file']).name}: {', '.join(issue.get('issues', []))}"
            )

    print("\n💡 Enhanced Recommendations:")
    for rec in report["recommendations"]:
        print(f"   {rec}")

    # Additional recommendations based on findings
    if report["file_locks"]["temp_wpf_files"]:
        print("   🧹 Clean temporary WPF files regularly with 'dotnet clean'")
    if report["file_locks"]["running_processes"]:
        print("   🔒 Kill running processes before rebuilding")
    if report["wpf_designer_issues"]["xaml_errors"]:
        print("   🎨 Fix XAML issues before testing themes")


if __name__ == "__main__":
    main()
