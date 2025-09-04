#!/usr/bin/env python3
"""
Syncfusion Theme Crash Debugger
Analyzes Windows Event Viewer logs, .NET runtime crashes, and WPF theme-related issues
"""

import datetime
import json
import os
import re
import shutil
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

        # WPF startup procedure files
        self.app_xaml_file = self.project_path / "App.xaml"
        self.app_xaml_cs_file = self.project_path / "App.xaml.cs"
        self.main_window_file = (
            self.project_path / "Wiley Widget" / "Views" / "MainWindow.xaml.cs"
        )

        # Syncfusion theme-related crash patterns
        self.crash_patterns = {
            "target_invocation": r"TargetInvocationException.*SfSkinManager|SetTheme",
            "null_reference": r"NullReferenceException.*Theme|SkinManager",
            "access_violation": r"AccessViolationException.*0xc0000005",
            "fluent_animation": r"FluentLight.*animation|reveal.*effect",
            "immutable_object": r"InvalidOperationException.*immutable.*theme",
            "syncfusion_crash": r"Syncfusion\..*Exception|SfSkinManager.*Exception",
            "wpf_theme": r"System\.Windows\..*Theme.*Exception",
            "startup_failure": r"Phase 1.*failed|Core Startup.*failed|constructor.*failed",
            "license_failure": r"Syncfusion.*license|RegisterEarlyLicenses.*failed",
            "theme_initialization": r"InitializeThemeSystem.*failed|ApplyTheme.*failed",
        }

        # WPF startup phase patterns
        self.startup_patterns = {
            "constructor": r"App\(\)|constructor.*started|constructor.*completed",
            "onstartup": r"OnStartup.*started|Phase 1.*Core Startup|OnStartup.*completed",
            "configuration": r"LoadConfiguration|appsettings\.json|IConfiguration",
            "logging": r"ConfigureSerilogLogger|Serilog.*initialized",
            "license_reg": r"RegisterSyncfusionLicense|RegisterEarlyLicenses|Syncfusion.*license",
            "theme_init": r"InitializeAndApplyTheme|ThemeService.*initialized",
            "main_window": r"MainWindow.*new|MainWindow.*Show|SourceInitialized",
            "deferred_init": r"DeferredInitializer|StartAsync|fire.*forget",
            "app_ready": r"SignalAppReady|AppReady.*event|application.*ready",
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

            # Find PowerShell executable securely
            powershell_exe = shutil.which("powershell.exe")
            if not powershell_exe:
                print("❌ PowerShell not found in PATH")
                return crashes

            # Secure subprocess call with validated executable and fixed command
            result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                [powershell_exe, "-Command", ps_cmd],
                capture_output=True,
                text=True,
                check=False,
                shell=False,  # Explicitly disable shell for security
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

    def analyze_wpf_startup_procedures(self) -> Dict:
        """Analyze App.xaml and App.xaml.cs startup procedures for issues"""
        print("🔍 Analyzing WPF App.xaml and App.xaml.cs startup procedures...")

        analysis = {
            "app_xaml_analysis": self._analyze_app_xaml(),
            "app_xaml_cs_analysis": self._analyze_app_xaml_cs(),
            "startup_sequence_analysis": self._analyze_startup_sequence(),
            "theme_initialization_analysis": self._analyze_theme_initialization(),
            "potential_issues": [],
            "recommendations": [],
        }

        return analysis

    def _analyze_app_xaml(self) -> Dict:
        """Analyze App.xaml for resource loading and startup configuration"""
        print("📄 Analyzing App.xaml...")

        analysis = {
            "file_exists": self.app_xaml_file.exists(),
            "resources": [],
            "merged_dictionaries": [],
            "issues": [],
        }

        if not analysis["file_exists"]:
            analysis["issues"].append("App.xaml file not found")
            return analysis

        try:
            with open(self.app_xaml_file, "r", encoding="utf-8") as f:
                content = f.read()

            # Look for resource dictionaries
            resource_patterns = [
                r"<ResourceDictionary\.MergedDictionaries>(.*?)</ResourceDictionary\.MergedDictionaries>",
                r'<ResourceDictionary Source="([^"]+)"',
                r"SyncfusionResources\.xaml",
                r'pack://application:,,,/([^"]+)',
            ]

            for pattern in resource_patterns:
                matches = re.findall(pattern, content, re.DOTALL | re.IGNORECASE)
                if matches:
                    analysis["resources"].extend(matches)

            # Check for startup URI configuration
            if "StartupUri" in content:
                analysis["startup_uri_configured"] = True
            else:
                analysis["startup_uri_configured"] = False
                analysis["issues"].append(
                    "No StartupUri configured - relying on code-behind"
                )

            # Check for application-level resources
            if "Application.Resources" in content:
                analysis["has_app_resources"] = True
            else:
                analysis["has_app_resources"] = False

        except Exception as e:
            analysis["issues"].append(f"Error reading App.xaml: {e}")

        return analysis

    def _analyze_app_xaml_cs(self) -> Dict:
        """Analyze App.xaml.cs for startup procedure implementation"""
        print("📄 Analyzing App.xaml.cs startup procedures...")

        analysis = {
            "file_exists": self.app_xaml_cs_file.exists(),
            "constructor_analysis": {},
            "onstartup_analysis": {},
            "startup_phases": [],
            "performance_metrics": {},
            "error_handling": {},
            "issues": [],
        }

        if not analysis["file_exists"]:
            analysis["issues"].append("App.xaml.cs file not found")
            return analysis

        try:
            with open(self.app_xaml_cs_file, "r", encoding="utf-8") as f:
                content = f.read()

            # Analyze constructor
            analysis["constructor_analysis"] = self._analyze_constructor(content)

            # Analyze OnStartup method
            analysis["onstartup_analysis"] = self._analyze_onstartup_method(content)

            # Find startup phases
            analysis["startup_phases"] = self._extract_startup_phases(content)

            # Analyze performance monitoring
            analysis["performance_metrics"] = self._analyze_performance_monitoring(
                content
            )

            # Analyze error handling
            analysis["error_handling"] = self._analyze_error_handling(content)

        except Exception as e:
            analysis["issues"].append(f"Error reading App.xaml.cs: {e}")

        return analysis

    def _analyze_constructor(self, content: str) -> Dict:
        """Analyze the App() constructor implementation"""
        constructor_analysis = {
            "found": False,
            "essential_operations": [],
            "heavy_operations": [],
            "follows_best_practices": True,
            "issues": [],
        }

        # Find constructor
        constructor_match = re.search(
            r"public App\(\)(.*?)(?=\n    [a-zA-Z]|\n\n|\Z)", content, re.DOTALL
        )
        if not constructor_match:
            constructor_analysis["issues"].append("App() constructor not found")
            return constructor_analysis

        constructor_analysis["found"] = True
        constructor_code = constructor_match.group(1)

        # Check for essential operations (Microsoft best practices)
        essential_patterns = {
            "startup_timer": r"_startupTimer.*Stopwatch",
            "license_registration": r"RegisterEarlyLicenses|WileyWidget\.Infrastructure\.LicenseRegistrar",
            "console_output": r"Console\.WriteLine",
        }

        for operation, pattern in essential_patterns.items():
            if re.search(pattern, constructor_code):
                constructor_analysis["essential_operations"].append(operation)

        # Check for heavy operations (should be avoided in constructor)
        heavy_patterns = {
            "configuration_loading": r"LoadConfiguration|appsettings",
            "database_operations": r"database|DbContext",
            "theme_application": r"SfSkinManager\.SetTheme|ApplyTheme",
            "window_creation": r"new MainWindow|\.Show\(",
            "file_operations": r"File\.|Directory\.",
            "network_operations": r"HttpClient|WebRequest",
        }

        for operation, pattern in heavy_patterns.items():
            if re.search(pattern, constructor_code):
                constructor_analysis["heavy_operations"].append(operation)
                constructor_analysis["follows_best_practices"] = False
                constructor_analysis["issues"].append(
                    f"Heavy operation in constructor: {operation}"
                )

        return constructor_analysis

    def _analyze_onstartup_method(self, content: str) -> Dict:
        """Analyze the OnStartup method implementation"""
        onstartup_analysis = {
            "found": False,
            "overrides_base": False,
            "startup_steps": [],
            "error_handling": False,
            "performance_monitoring": False,
            "issues": [],
        }

        # Find OnStartup method with more flexible matching
        onstartup_match = re.search(
            r"protected override void OnStartup\(StartupEventArgs e\)(.*?)(?=\n    [a-zA-Z]|\n\n|\Z)",
            content,
            re.DOTALL,
        )
        if not onstartup_match:
            onstartup_analysis["issues"].append("OnStartup method not found")
            return onstartup_analysis

        onstartup_analysis["found"] = True
        onstartup_analysis["overrides_base"] = True
        onstartup_code = onstartup_match.group(1)

        # Extract startup steps
        step_patterns = [
            r"Step \d+:.*?(?=Step \d+:|$)",
            r"Phase \d+:.*?(?=Phase \d+:|$)",
            r"LoadConfiguration\(\)",
            r"ConfigureSerilogLogger\(\)",
            r"RegisterSyncfusionLicense\(\)",
            r"InitializeAndApplyTheme\(\)",
            r"new MainWindow\(\)",
            r"DeferredInitializer",
        ]

        for pattern in step_patterns:
            matches = re.findall(pattern, onstartup_code, re.DOTALL)
            onstartup_analysis["startup_steps"].extend(matches)

        # Enhanced error handling detection - look for try-catch with proper scope
        try_catch_patterns = [
            r"try\s*{.*catch\s*\(.*Exception.*ex\)",  # Standard try-catch
            r"try\s*{.*}.*catch.*{.*Log\.Fatal.*Shutdown",  # Startup-specific pattern
            r"catch\s*\(Exception\s+ex\).*{.*Log\.Fatal",  # Fatal error logging
            r"try\s*{[^}]*startupTimer[^}]*}[^}]*catch",  # Try block with startup timer
        ]

        error_handling_found = any(
            re.search(pattern, onstartup_code, re.DOTALL | re.IGNORECASE)
            for pattern in try_catch_patterns
        )

        if error_handling_found:
            onstartup_analysis["error_handling"] = True
            # Remove the error if we found proper error handling
            onstartup_analysis["issues"] = [
                issue
                for issue in onstartup_analysis["issues"]
                if "error handling" not in issue.lower()
            ]
        else:
            onstartup_analysis["issues"].append(
                "No comprehensive error handling in OnStartup"
            )

        # Check for performance monitoring
        if re.search(r"Stopwatch|ElapsedMilliseconds|startupTimer", onstartup_code):
            onstartup_analysis["performance_monitoring"] = True
        else:
            onstartup_analysis["issues"].append(
                "No performance monitoring in OnStartup"
            )

        return onstartup_analysis

    def _extract_startup_phases(self, content: str) -> List[Dict]:
        """Extract and analyze startup phases"""
        phases = []

        # Look for phase markers
        phase_patterns = [
            r"Phase 1.*?Core Startup",
            r"Phase 2.*?Deferred",
            r"Phase 3.*?Ready",
            r"Step \d+:.*",
        ]

        for pattern in phase_patterns:
            matches = re.findall(pattern, content, re.IGNORECASE)
            for match in matches:
                phases.append({"description": match.strip(), "pattern": pattern})

        return phases

    def _analyze_performance_monitoring(self, content: str) -> Dict:
        """Analyze performance monitoring implementation"""
        perf_analysis = {
            "startup_timer": re.search(r"_startupTimer.*Stopwatch", content)
            is not None,
            "elapsed_logging": re.search(
                r"ElapsedMilliseconds|TotalMilliseconds", content
            )
            is not None,
            "memory_monitoring": re.search(r"WorkingSet64|MemoryUsage", content)
            is not None,
            "startup_metrics": re.search(r"StartupMetrics|_startupMetrics", content)
            is not None,
            "performance_report": re.search(
                r"LogStartupReport|FINAL STARTUP REPORT", content
            )
            is not None,
        }

        return perf_analysis

    def _analyze_error_handling(self, content: str) -> Dict:
        """Analyze error handling implementation"""
        error_analysis = {
            "global_exception_handler": re.search(
                r"DispatcherUnhandledException|AppDomain.*UnhandledException", content
            )
            is not None,
            "constructor_try_catch": re.search(
                r"public App\(\).*try.*catch", content, re.DOTALL
            )
            is not None,
            "onstartup_try_catch": re.search(
                r"OnStartup.*try.*catch", content, re.DOTALL
            )
            is not None,
            "graceful_shutdown": re.search(r"Shutdown\(\d+\)", content) is not None,
            "error_logging": re.search(r"Log\.Error|Log\.Fatal", content) is not None,
        }

        return error_analysis

    def _analyze_startup_sequence(self) -> Dict:
        """Analyze the overall startup sequence for potential issues"""
        print("🔄 Analyzing startup sequence...")

        sequence_analysis = {
            "proper_order": True,
            "phase_separation": True,
            "deferred_operations": [],
            "critical_path_operations": [],
            "issues": [],
            "recommendations": [],
        }

        # Check if heavy operations are properly deferred
        if self.app_xaml_cs_file.exists():
            try:
                with open(self.app_xaml_cs_file, "r", encoding="utf-8") as f:
                    content = f.read()

                # Check if DeferredInitializer is used
                if re.search(r"DeferredInitializer|StartAsync", content):
                    sequence_analysis["deferred_operations"].append(
                        "DeferredInitializer found"
                    )
                else:
                    sequence_analysis["issues"].append(
                        "No DeferredInitializer found - Phase 2 operations may block startup"
                    )

            except Exception as e:
                sequence_analysis["issues"].append(
                    f"Error analyzing startup sequence: {e}"
                )

        return sequence_analysis

    def _analyze_theme_initialization(self) -> Dict:
        """Analyze theme initialization procedures"""
        print("🎨 Analyzing theme initialization...")

        theme_analysis = {
            "constructor_theme_ops": [],
            "onstartup_theme_ops": [],
            "mainwindow_theme_ops": [],
            "proper_deferral": True,
            "issues": [],
            "recommendations": [],
        }

        # Check App.xaml.cs for theme operations
        if self.app_xaml_cs_file.exists():
            try:
                with open(self.app_xaml_cs_file, "r", encoding="utf-8") as f:
                    content = f.read()

                # Find theme operations in constructor
                constructor_match = re.search(
                    r"public App\(\)(.*?)(?=\n    [a-zA-Z]|\n\n|\Z)", content, re.DOTALL
                )
                if constructor_match:
                    constructor_code = constructor_match.group(1)
                    theme_patterns = [
                        "SfSkinManager",
                        "SetTheme",
                        "ApplyTheme",
                        "ThemeService",
                    ]
                    for pattern in theme_patterns:
                        if re.search(pattern, constructor_code):
                            theme_analysis["constructor_theme_ops"].append(pattern)
                            theme_analysis["proper_deferral"] = False
                            theme_analysis["issues"].append(
                                f"Theme operation in constructor: {pattern}"
                            )

                # Find theme operations in OnStartup
                onstartup_match = re.search(
                    r"OnStartup.*?\{(.*?)(?=\n    [a-zA-Z]|\n\n|\Z)", content, re.DOTALL
                )
                if onstartup_match:
                    onstartup_code = onstartup_match.group(1)
                    if re.search(
                        r"InitializeAndApplyTheme|ThemeService", onstartup_code
                    ):
                        theme_analysis["onstartup_theme_ops"].append(
                            "Theme initialization found"
                        )

            except Exception as e:
                theme_analysis["issues"].append(
                    f"Error analyzing App.xaml.cs theme operations: {e}"
                )

        # Check MainWindow.xaml.cs for theme deferral
        if self.main_window_file.exists():
            try:
                with open(self.main_window_file, "r", encoding="utf-8") as f:
                    content = f.read()

                if re.search(r"ApplyDeferredTheme|Window\.Loaded.*theme", content):
                    theme_analysis["mainwindow_theme_ops"].append(
                        "Deferred theme application found"
                    )
                else:
                    theme_analysis["issues"].append(
                        "No deferred theme application in MainWindow"
                    )

            except Exception as e:
                theme_analysis["issues"].append(
                    f"Error analyzing MainWindow theme operations: {e}"
                )

        return theme_analysis

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

                    # Find PowerShell executable securely
                    powershell_exe = shutil.which("powershell.exe")
                    if not powershell_exe:
                        print("❌ PowerShell not found in PATH")
                        continue

                    # Secure subprocess call with validated executable and fixed command
                    result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                        [powershell_exe, "-Command", ps_cmd],
                        capture_output=True,
                        text=True,
                        check=False,
                        shell=False,  # Explicitly disable shell for security
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

            # Find PowerShell executable securely
            powershell_exe = shutil.which("powershell.exe")
            if not powershell_exe:
                print("❌ PowerShell not found in PATH")
                return lock_info

            # Secure subprocess call with validated executable and fixed command
            result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                [powershell_exe, "-Command", ps_cmd],
                capture_output=True,
                text=True,
                check=False,
                shell=False,  # Explicitly disable shell for security
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
            # Find dotnet executable securely
            dotnet_exe = shutil.which("dotnet")
            if not dotnet_exe:
                runtime_info["error"] = "dotnet.exe not found in PATH"
                return runtime_info

            # Get .NET version - secure subprocess call with validated executable
            result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                [dotnet_exe, "--version"],
                capture_output=True,
                text=True,
                check=False,
                shell=False,  # Explicitly disable shell for security
            )

            if result.returncode == 0:
                runtime_info["dotnet_version"] = result.stdout.strip()

            # Get runtime info - secure subprocess call with validated executable
            result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                [dotnet_exe, "--info"],
                capture_output=True,
                text=True,
                check=False,
                shell=False,  # Explicitly disable shell for security
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
            # Find dotnet executable securely
            dotnet_exe = shutil.which("dotnet")
            if not dotnet_exe:
                test_results["crashes"].append("dotnet.exe not found in PATH")
                return test_results

            # Build the repro project - secure subprocess call with validated executable
            build_args = [
                dotnet_exe,
                "build",
                str(repro_path / "ThemeCrashRepro.csproj"),
            ]
            build_result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                build_args,
                capture_output=True,
                text=True,
                cwd=repro_path,
                check=False,
                shell=False,  # Explicitly disable shell for security
            )

            test_results["build_success"] = build_result.returncode == 0
            test_results["build_output"] = build_result.stdout + build_result.stderr

            if test_results["build_success"]:
                # Run the repro project - secure subprocess call with validated executable
                run_args = [dotnet_exe, "run", "--project", "ThemeCrashRepro.csproj"]
                run_result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
                    run_args,
                    capture_output=True,
                    text=True,
                    cwd=repro_path,
                    timeout=30,
                    check=False,
                    shell=False,  # Explicitly disable shell for security
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
        """Generate comprehensive debugging report including WPF startup analysis"""
        print("📊 Generating comprehensive theme crash and WPF startup report...")

        # Clean up temporary files first
        cleaned_files = self.cleanup_temp_files()

        report = {
            "timestamp": datetime.datetime.now().isoformat(),
            "project_path": str(self.project_path),
            "cleanup_results": {
                "cleaned_files": cleaned_files,
                "cleanup_count": len(cleaned_files),
            },
            "wpf_startup_analysis": self.analyze_wpf_startup_procedures(),
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
        """Generate debugging recommendations including WPF startup best practices"""
        return [
            "🚀 WPF STARTUP BEST PRACTICES:",
            "   ✅ Keep App() constructor minimal - only essential operations",
            "   ✅ Defer heavy operations to OnStartup method",
            "   ✅ Use DeferredInitializer for Phase 2 background operations",
            "   ✅ Implement performance monitoring with Stopwatch",
            "   ✅ Use structured logging for startup diagnostics",
            "",
            "🎨 THEME APPLICATION BEST PRACTICES:",
            "   ✅ Move theme application from constructor to Window.Loaded event",
            "   ✅ Set SfSkinManager.ApplyThemeAsDefaultStyle = true in App.OnStartup",
            "   ✅ Initialize theme system early but defer actual application",
            "   ✅ Use ThemeService for centralized theme management",
            "   ✅ Disable FluentLight animations: HoverEffectMode = HoverEffect.None",
            "",
            "🔍 DIAGNOSTIC STEPS:",
            "   🔍 Check Windows Event Viewer for .NET Runtime errors",
            "   🔍 Monitor debug.log for TargetInvocationException patterns",
            "   🔍 Analyze startup sequence timing with performance logs",
            "   🔍 Verify proper phase separation (Constructor → OnStartup → Loaded)",
            "",
            "🧪 TESTING RECOMMENDATIONS:",
            "   🧪 Test with minimal repro to isolate theme crashes",
            "   🧪 Verify startup phases execute in correct order",
            "   🧪 Test theme switching during runtime",
            "   🧪 Monitor memory usage during startup",
            "",
            "📞 ESCALATION PATH:",
            "   📞 Contact Syncfusion support with specific crash logs",
            "   📞 Include WPF startup analysis in support tickets",
        ]


def main():
    if len(sys.argv) > 1:
        project_path = sys.argv[1]
    else:
        project_path = os.getcwd()

    print("🎨 Syncfusion Theme Crash Debugger & WPF Startup Analyzer")
    print("=" * 60)

    debugger = SyncfusionThemeDebugger(project_path)
    report = debugger.generate_report()

    # Print summary
    print("\n📋 Debug Report Summary:")
    print(f"   Files Cleaned: {report['cleanup_results']['cleanup_count']}")

    # WPF Startup Analysis Summary
    if "wpf_startup_analysis" in report:
        startup = report["wpf_startup_analysis"]
        print("\n🚀 WPF Startup Analysis:")
        print(f"   App.xaml Found: {startup['app_xaml_analysis']['file_exists']}")
        print(f"   App.xaml.cs Found: {startup['app_xaml_cs_analysis']['file_exists']}")

        if startup["app_xaml_cs_analysis"]["file_exists"]:
            constructor = startup["app_xaml_cs_analysis"]["constructor_analysis"]
            onstartup = startup["app_xaml_cs_analysis"]["onstartup_analysis"]
            print(f"   Constructor Found: {constructor['found']}")
            print(
                f"   Constructor Best Practices: {constructor['follows_best_practices']}"
            )
            print(f"   OnStartup Found: {onstartup['found']}")
            print(f"   OnStartup Error Handling: {onstartup['error_handling']}")
            print(f"   Performance Monitoring: {onstartup['performance_monitoring']}")

            if constructor["heavy_operations"]:
                print(
                    f"   ⚠️  Heavy Constructor Operations: {', '.join(constructor['heavy_operations'])}"
                )

            theme_analysis = startup["theme_initialization_analysis"]
            print(f"   Theme Deferral Correct: {theme_analysis['proper_deferral']}")
            if theme_analysis["issues"]:
                print(f"   ⚠️  Theme Issues: {len(theme_analysis['issues'])}")

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

    # Print WPF startup issues in detail
    if "wpf_startup_analysis" in report:
        startup = report["wpf_startup_analysis"]
        print("\n🚀 WPF Startup Issues Found:")

        all_issues = []
        all_issues.extend(startup["app_xaml_analysis"].get("issues", []))
        all_issues.extend(startup["app_xaml_cs_analysis"].get("issues", []))
        all_issues.extend(startup["startup_sequence_analysis"].get("issues", []))
        all_issues.extend(startup["theme_initialization_analysis"].get("issues", []))

        if all_issues:
            for issue in all_issues[:5]:  # Show first 5 issues
                print(f"   ⚠️  {issue}")
        else:
            print("   ✅ No major startup issues detected")

    # Print cleanup results
    if report["cleanup_results"]["cleaned_files"]:
        print(
            f"\n🧹 Cleaned {len(report['cleanup_results']['cleaned_files'])} temp files"
        )

    # Print critical crashes
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

    # Additional recommendations based on WPF startup findings
    if "wpf_startup_analysis" in report:
        startup = report["wpf_startup_analysis"]

        if not startup["app_xaml_cs_analysis"]["constructor_analysis"][
            "follows_best_practices"
        ]:
            print("   🚀 Move heavy operations from constructor to OnStartup")

        if not startup["theme_initialization_analysis"]["proper_deferral"]:
            print("   🎨 Defer theme application to Window.Loaded event")

        if not startup["app_xaml_cs_analysis"]["onstartup_analysis"]["error_handling"]:
            print("   🛡️  Add comprehensive error handling to OnStartup method")

    # Additional recommendations based on findings
    if report["file_locks"]["temp_wpf_files"]:
        print("   🧹 Clean temporary WPF files regularly with 'dotnet clean'")
    if report["file_locks"]["running_processes"]:
        print("   🔒 Kill running processes before rebuilding")
    if report["wpf_designer_issues"]["xaml_errors"]:
        print("   🎨 Fix XAML issues before testing themes")


if __name__ == "__main__":
    main()
