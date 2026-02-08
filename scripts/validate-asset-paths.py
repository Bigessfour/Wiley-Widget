#!/usr/bin/env python3
"""
Asset Path Validation Script for JARVIS BlazorWebView
Verifies all static assets required for proper BlazorWebView initialization.
"""

import json
import sys
from pathlib import Path
from typing import Dict, List

# Configuration
PROJECT_ROOT = Path(__file__).parent
WINFORMS_PROJECT = PROJECT_ROOT / "src" / "WileyWidget.WinForms"
WWWROOT = WINFORMS_PROJECT / "wwwroot"
BIN_FOLDER = WINFORMS_PROJECT / "bin" / "Debug" / "net10.0-windows"

class AssetValidator:
    """Validates BlazorWebView asset paths and configurations."""
    
    REQUIRED_ASSETS = {
        "index.html": {
            "path": WWWROOT / "index.html",
            "type": "HTML host page",
            "required": True,
            "critical": True,
        },
        "blazor.webview.js": {
            "path": "_framework/blazor.webview.js",
            "type": "Blazor runtime (local, from NuGet)",
            "required": True,
            "critical": True,
            "validation": "Should be served from BlazorWebView's virtual asset handler",
        },
        "syncfusion-blazor.min.js": {
            "path": "_content/Syncfusion.Blazor.Core/scripts/syncfusion-blazor.min.js",
            "type": "Syncfusion Blazor component library",
            "required": True,
            "critical": True,
            "validation": "Must be present in NuGet package",
        },
        "fluent.css": {
            "path": "_content/Syncfusion.Blazor.Themes/fluent.css",
            "type": "Syncfusion Fluent theme CSS",
            "required": True,
            "critical": False,
            "fallback": "https://cdn.syncfusion.com/blazor/32.2.3/styles/fluent.css",
        },
        "ai-assist.css": {
            "path": WWWROOT / "css" / "ai-assist.css",
            "type": "Custom JARVIS CSS",
            "required": False,
            "critical": False,
        },
    }

    def __init__(self):
        self.issues: List[Dict] = []
        self.warnings: List[Dict] = []
        self.validations: List[Dict] = []

    def validate_wwwroot_structure(self) -> bool:
        """Check if wwwroot folder exists and contains required files."""
        print("\n📋 Validating wwwroot structure...")
        
        if not WWWROOT.exists():
            self.issues.append({
                "severity": "CRITICAL",
                "file": str(WWWROOT),
                "issue": "wwwroot folder not found",
                "fix": f"Create {WWWROOT} folder"
            })
            return False
        
        print(f"✓ wwwroot found: {WWWROOT}")
        
        # Check index.html
        index_html = self.REQUIRED_ASSETS["index.html"]["path"]
        if not index_html.exists():
            self.issues.append({
                "severity": "CRITICAL",
                "file": str(index_html),
                "issue": "index.html is missing",
                "fix": f"Create {index_html} with proper BlazorWebView host page"
            })
            return False
        
        print(f"✓ index.html found: {index_html}")
        return True

    def validate_index_html(self) -> bool:
        """Verify index.html contains all required script and link tags."""
        print("\n📄 Validating index.html content...")
        
        index_html = self.REQUIRED_ASSETS["index.html"]["path"]
        try:
            with open(index_html, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            self.issues.append({
                "severity": "CRITICAL",
                "file": str(index_html),
                "issue": f"Cannot read index.html: {e}",
                "fix": "Ensure file is readable"
            })
            return False
        
        checks = {
            "_framework/blazor.webview.js": "Blazor runtime bootstrap",
            "_content/Syncfusion.Blazor.Core/scripts/syncfusion-blazor.min.js": "Syncfusion Blazor library",
            "_content/Syncfusion.Blazor.Themes/fluent.css": "Syncfusion theme (or CDN fallback)",
            "<base href=\"/\" />": "Base href for routing",
            "<div id=\"app\">": "App container for Blazor",
            "loadSyncfusionFromCDN": "CDN fallback for Syncfusion JS",
            "loadSyncfusionThemeFromCDN": "CDN fallback for theme CSS",
        }
        
        for check, description in checks.items():
            if check in content:
                print(f"✓ Found: {description}")
                self.validations.append({
                    "check": description,
                    "status": "PASS"
                })
            else:
                severity = "CRITICAL" if check.startswith("_") else "WARNING"
                self.warnings.append({
                    "severity": severity,
                    "file": "index.html",
                    "issue": f"Missing or incorrect: {description}",
                    "check_text": check
                })
                print(f"⚠ Missing: {description}")
        
        return len(self.issues) == 0

    def validate_project_configuration(self) -> bool:
        """Check WileyWidget.WinForms.csproj for proper BlazorWebView setup."""
        print("\n⚙️ Validating project configuration...")
        
        csproj = WINFORMS_PROJECT / "WileyWidget.WinForms.csproj"
        if not csproj.exists():
            self.issues.append({
                "severity": "CRITICAL",
                "file": str(csproj),
                "issue": "Project file not found",
                "fix": f"Ensure {csproj} exists"
            })
            return False
        
        try:
            with open(csproj, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            self.issues.append({
                "severity": "ERROR",
                "file": str(csproj),
                "issue": f"Cannot read project file: {e}",
                "fix": "Ensure file is readable"
            })
            return False
        
        checks = {
            "Microsoft.AspNetCore.Components.WebView.WindowsForms": "BlazorWebView package",
            "Microsoft.Web.WebView2": "WebView2 package",
            "Syncfusion.Blazor.Core": "Syncfusion Blazor Core",
            "Syncfusion.Blazor.Themes": "Syncfusion themes",
            "<UseWindowsForms>true</UseWindowsForms>": "Windows Forms enabled",
            "wwwroot": "wwwroot content included",
        }
        
        for check, description in checks.items():
            if check in content:
                print(f"✓ Found: {description}")
                self.validations.append({
                    "check": description,
                    "status": "PASS"
                })
            else:
                self.warnings.append({
                    "severity": "WARNING",
                    "file": "WileyWidget.WinForms.csproj",
                    "issue": f"Missing or incorrect: {description}",
                    "check_text": check
                })
        
        return len(self.issues) == 0

    def validate_dotnet_packages(self) -> bool:
        """Check if required NuGet packages are listed in Directory.Packages.props."""
        print("\n📦 Validating NuGet packages...")
        
        props = PROJECT_ROOT / "Directory.Packages.props"
        if not props.exists():
            self.warnings.append({
                "severity": "WARNING",
                "file": str(props),
                "issue": "Central package management file not found",
                "fix": "This feature (Directory.Packages.props) is optional but recommended"
            })
            return True
        
        try:
            with open(props, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            self.warnings.append({
                "severity": "WARNING",
                "file": str(props),
                "issue": f"Cannot read props file: {e}",
                "fix": "Ensure file is readable"
            })
            return True
        
        required_packages = {
            "Microsoft.AspNetCore.Components.WebView.WindowsForms": "10.0.30+",
            "Microsoft.Web.WebView2": "1.0.3650+",
            "Syncfusion.Blazor.Core": "32.2.3+",
            "Syncfusion.Blazor.Themes": "32.2.3+",
            "Syncfusion.Blazor.InteractiveChat": "32.2.3+",
        }
        
        for package, version_hint in required_packages.items():
            if package in content:
                print(f"✓ Found: {package} ({version_hint})")
                self.validations.append({
                    "check": package,
                    "status": "PASS"
                })
            else:
                self.warnings.append({
                    "severity": "WARNING",
                    "file": "Directory.Packages.props",
                    "issue": f"NuGet package possibly missing: {package}",
                    "recommended_version": version_hint
                })
        
        return True

    def validate_blazor_component_structure(self) -> bool:
        """Check if Blazor components are properly configured."""
        print("\n🔧 Validating Blazor component structure...")
        
        component_files = [
            WINFORMS_PROJECT / "BlazorComponents" / "App.razor",
            WINFORMS_PROJECT / "BlazorComponents" / "MainLayout.razor",
            WINFORMS_PROJECT / "BlazorComponents" / "JARVISAssist.razor",
        ]
        
        for component in component_files:
            if component.exists():
                print(f"✓ Found: {component.name}")
                self.validations.append({
                    "check": f"Component {component.name}",
                    "status": "PASS"
                })
            else:
                self.warnings.append({
                    "severity": "WARNING",
                    "file": str(component),
                    "issue": f"Component file missing: {component.name}",
                    "fix": f"Ensure {component} exists"
                })
        
        return True

    def generate_summary(self) -> Dict:
        """Generate validation summary report."""
        return {
            "timestamp": str(Path.cwd()),
            "total_validations": len(self.validations),
            "total_warnings": len(self.warnings),
            "total_issues": len(self.issues),
            "status": "PASS" if len(self.issues) == 0 else "FAIL",
            "validations": self.validations,
            "warnings": self.warnings,
            "issues": self.issues,
        }

    def print_report(self):
        """Print formatted validation report."""
        summary = self.generate_summary()
        
        print("\n" + "=" * 70)
        print("JARVIS BlazorWebView Asset Validation Report".center(70))
        print("=" * 70)
        
        if summary["issues"]:
            print("\n🔴 CRITICAL ISSUES:")
            for i, issue in enumerate(summary["issues"], 1):
                print(f"  {i}. {issue['severity']}: {issue['issue']}")
                print(f"     File: {issue['file']}")
                print(f"     Fix: {issue['fix']}\n")
        
        if summary["warnings"]:
            print("\n🟡 WARNINGS:")
            for i, warning in enumerate(summary["warnings"], 1):
                print(f"  {i}. {warning['severity']}: {warning['issue']}")
                print(f"     File: {warning['file']}\n")
        
        if summary["validations"]:
            print(f"\n✅ PASSED CHECKS: {len(summary['validations'])}")
            for validation in summary["validations"]:
                print(f"  ✓ {validation['check']}")
        
        print("\n" + "=" * 70)
        print(f"Status: {summary['status']} | Issues: {summary['total_issues']} | Warnings: {summary['total_warnings']}")
        print("=" * 70)
        
        return summary

def main():
    """Run validation."""
    validator = AssetValidator()
    
    # Run all validations
    validator.validate_wwwroot_structure()
    validator.validate_index_html()
    validator.validate_project_configuration()
    validator.validate_dotnet_packages()
    validator.validate_blazor_component_structure()
    
    # Print report
    summary = validator.print_report()
    
    # Save JSON report
    report_file = PROJECT_ROOT / "asset-validation-report.json"
    with open(report_file, 'w') as f:
        json.dump(summary, f, indent=2)
    print(f"\n📄 Detailed report saved to: {report_file}")
    
    # Return appropriate exit code
    sys.exit(0 if summary["status"] == "PASS" else 1)

if __name__ == "__main__":
    main()
