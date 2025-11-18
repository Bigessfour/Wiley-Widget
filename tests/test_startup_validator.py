#!/usr/bin/env python3
"""
Unit tests for startup_validator.py
Tests patterns, version parsing, and validation logic
"""

import sys
from pathlib import Path

import pytest

# Add tools directory to path
sys.path.insert(0, str(Path(__file__).parent.parent / "tools"))

from startup_validator import (
    BOLD_LICENSE_PATTERN,
    ENV_VAR_ASSIGNMENT_PATTERN,
    MODULE_INIT_PATTERN,
    PACKAGE_REF_PATTERN,
    PLACEHOLDER_PATTERN,
    REGISTER_SINGLETON_PATTERN,
    SF_CHART_BINDING_PATTERN,
    SF_CONTROL_PATTERN,
    SF_DATAGRID_BINDING_PATTERN,
    SF_LICENSE_PATTERN,
    VALID_LICENSE_FORMAT,
    check_breaking_changes,
    is_version_outdated,
    parse_version,
)


class TestPatternMatching:
    """Test regex patterns for license keys, assemblies, and controls"""

    def test_sf_license_pattern_hardcoded(self):
        """Test Syncfusion license pattern with hardcoded key"""
        code = 'SyncfusionLicenseProvider.RegisterLicense("ABC123XYZ789")'
        match = SF_LICENSE_PATTERN.search(code)
        assert match is not None
        assert '"ABC123XYZ789"' in match.group(1)

    def test_sf_license_pattern_variable(self):
        """Test Syncfusion license pattern with variable"""
        code = "SfSkinManager.SetLicenseKey(licenseKey)"
        match = SF_LICENSE_PATTERN.search(code)
        assert match is not None
        assert "licenseKey" in match.group(1)

    def test_sf_license_pattern_async(self):
        """Test Syncfusion license pattern with async/await"""
        code = 'SfSkinManager.SetLicenseKey(await vault.GetSecretAsync("Key"))'
        match = SF_LICENSE_PATTERN.search(code)
        assert match is not None

    def test_bold_license_pattern(self):
        """Test Bold license pattern"""
        code = 'BoldLicenseProvider.RegisterLicense("BOLD_LICENSE_KEY")'
        match = BOLD_LICENSE_PATTERN.search(code)
        assert match is not None

    def test_env_var_assignment_pattern(self):
        """Test environment variable assignment pattern"""
        code = 'var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")'
        match = ENV_VAR_ASSIGNMENT_PATTERN.search(code)
        assert match is not None
        assert match.group(1) == "licenseKey"
        assert match.group(2) == "SYNCFUSION_LICENSE_KEY"

    def test_valid_license_format(self):
        """Test license key format validation"""
        # Valid base64-like keys (must be 40+ chars before padding)
        assert VALID_LICENSE_FORMAT.match(
            "MTIzNDU2Nzg5MGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6QUJDREVGR0g="
        )
        assert VALID_LICENSE_FORMAT.match(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCD+/=="
        )  # 40 chars + padding
        assert VALID_LICENSE_FORMAT.match(
            "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0=="
        )  # 40 chars + padding

        # Invalid keys
        assert not VALID_LICENSE_FORMAT.match("short")
        assert not VALID_LICENSE_FORMAT.match("invalid!@#$%^&*()")
        assert not VALID_LICENSE_FORMAT.match(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123=="
        )  # Only 30 chars

    def test_placeholder_pattern(self):
        """Test placeholder detection"""
        assert PLACEHOLDER_PATTERN.search("{LicenseKey}")
        assert PLACEHOLDER_PATTERN.search("{SyncfusionLicenseKey}")
        assert PLACEHOLDER_PATTERN.search("Set {licensekey} here")
        assert not PLACEHOLDER_PATTERN.search("MTIzNDU2Nzg5MA==")

    def test_package_ref_pattern(self):
        """Test .csproj PackageReference pattern"""
        xml = (
            '<PackageReference Include="Syncfusion.SfDataGrid.WPF" Version="27.1.48" />'
        )
        match = PACKAGE_REF_PATTERN.search(xml)
        assert match is not None
        assert match.group(1) == "Syncfusion.SfDataGrid.WPF"
        assert match.group(2) == "27.1.48"

    def test_sf_control_pattern(self):
        """Test Syncfusion control detection in XAML"""
        xaml = '<syncfusion:SfDataGrid ItemsSource="{Binding Items}" />'
        match = SF_CONTROL_PATTERN.search(xaml)
        assert match is not None
        assert match.group(1) == "SfDataGrid"

    def test_sf_chart_binding_pattern(self):
        """Test SfChart binding detection"""
        xaml = '<sf:SfChart Series="{Binding ChartData}" />'
        match = SF_CHART_BINDING_PATTERN.search(xaml)
        assert match is not None
        assert "ChartData" in match.group(1)

    def test_sf_datagrid_binding_pattern(self):
        """Test SfDataGrid binding detection"""
        xaml = '<sf:SfDataGrid ItemsSource="{Binding GridItems}" />'
        match = SF_DATAGRID_BINDING_PATTERN.search(xaml)
        assert match is not None
        assert "GridItems" in match.group(1)

    def test_register_singleton_pattern(self):
        """Test DI singleton registration pattern"""
        code = "container.RegisterSingleton<IService, ServiceImpl>()"
        match = REGISTER_SINGLETON_PATTERN.search(code)
        assert match is not None
        assert match.group(1) == "IService"
        assert match.group(2) == "ServiceImpl"

        # Single type registration
        code2 = "container.RegisterSingleton<MyService>()"
        match2 = REGISTER_SINGLETON_PATTERN.search(code2)
        assert match2 is not None
        assert match2.group(1) == "MyService"
        assert match2.group(2) is None

    def test_module_init_pattern(self):
        """Test Prism module detection"""
        code = "public class QuickBooksModule : IModule"
        match = MODULE_INIT_PATTERN.search(code)
        assert match is not None
        assert match.group(1) == "QuickBooksModule"


class TestResourceEvaluationPatterns:
    """Test new resource_evals scan patterns"""

    def test_brush_resource_pattern(self):
        """Test brush resource key detection"""
        from startup_validator import BRUSH_RESOURCE_PATTERN

        xaml = '<SolidColorBrush x:Key="PrimaryBrush" Color="#FF0000" />'
        match = BRUSH_RESOURCE_PATTERN.search(xaml)
        assert match is not None
        assert match.group(1) == "PrimaryBrush"

    def test_style_resource_pattern(self):
        """Test style resource key detection"""
        from startup_validator import STYLE_RESOURCE_PATTERN

        xaml = '<Style x:Key="ButtonStyle" TargetType="Button">'
        match = STYLE_RESOURCE_PATTERN.search(xaml)
        assert match is not None
        assert match.group(1) == "ButtonStyle"
        assert match.group(2) == "Button"

    def test_static_resource_ref_pattern(self):
        """Test StaticResource reference detection"""
        from startup_validator import STATIC_RESOURCE_REF_PATTERN

        xaml = 'Background="{StaticResource PrimaryBrush}"'
        match = STATIC_RESOURCE_REF_PATTERN.search(xaml)
        assert match is not None
        assert match.group(1) == "PrimaryBrush"

    def test_data_template_pattern(self):
        """Test DataTemplate key detection"""
        from startup_validator import DATA_TEMPLATE_PATTERN

        xaml = (
            '<DataTemplate x:Key="CustomerTemplate" DataType="{x:Type local:Customer}">'
        )
        match = DATA_TEMPLATE_PATTERN.search(xaml)
        assert match is not None
        assert match.group(1) == "CustomerTemplate"

    def test_fluentlight_reserved_brushes(self):
        """Test FluentLight reserved brush detection"""
        from startup_validator import FLUENTLIGHT_RESERVED_BRUSHES

        assert "PrimaryBrush" in FLUENTLIGHT_RESERVED_BRUSHES
        assert "AccentBrush" in FLUENTLIGHT_RESERVED_BRUSHES
        assert "BackgroundBrush" in FLUENTLIGHT_RESERVED_BRUSHES

    def test_sf_reserved_style_keys(self):
        """Test Syncfusion reserved style keys"""
        from startup_validator import SF_RESERVED_STYLE_KEYS

        assert "SfDataGridStyle" in SF_RESERVED_STYLE_KEYS
        assert "SyncfusionAccentBrush" in SF_RESERVED_STYLE_KEYS

    def test_dispatcher_deadlock_patterns(self):
        """Test .NET 9 dispatcher deadlock pattern detection"""
        from startup_validator import DISPATCHER_DEADLOCK_PATTERNS

        code = "Dispatcher.Invoke(() => { /* do work */ })"
        matches = [p.search(code) for p in DISPATCHER_DEADLOCK_PATTERNS]
        assert any(m is not None for m in matches)


class TestVersionParsing:
    """Test version parsing and comparison logic"""

    def test_parse_version_standard(self):
        """Test standard version parsing"""
        assert parse_version("27.1.48") == (27, 1, 48)
        assert parse_version("28.0.0") == (28, 0, 0)
        assert parse_version("31.2.5") == (31, 2, 5)

    def test_parse_version_invalid(self):
        """Test invalid version strings"""
        assert parse_version("invalid") == (0, 0, 0)
        assert parse_version("") == (0, 0, 0)
        assert parse_version(None) == (0, 0, 0)

    def test_parse_version_extra_segments(self):
        """Test version with extra segments (build numbers)"""
        # Should only take first 3 segments
        assert parse_version("27.1.48.1234") == (27, 1, 48)

    def test_is_version_outdated(self):
        """Test version outdated detection"""
        # 27.1.48 is the default minimum
        assert is_version_outdated("Syncfusion.SfDataGrid.WPF", "26.0.0")
        assert is_version_outdated("Syncfusion.SfDataGrid.WPF", "27.1.47")
        assert not is_version_outdated("Syncfusion.SfDataGrid.WPF", "27.1.48")
        assert not is_version_outdated("Syncfusion.SfDataGrid.WPF", "28.0.0")

    def test_check_breaking_changes_none(self):
        """Test no breaking changes for current versions"""
        # Version 31.x should not report breaking changes from v26-28
        warnings = check_breaking_changes("31.2.5")
        # Breaking changes are filtered to only show current major version
        assert len(warnings) == 0

    def test_check_breaking_changes_v27(self):
        """Test breaking changes detection for v27"""
        warnings = check_breaking_changes("27.0.0")
        assert len(warnings) >= 1
        assert any(".NET 8+" in w["message"] for w in warnings)

    def test_check_breaking_changes_v28(self):
        """Test breaking changes detection for v28"""
        warnings = check_breaking_changes("28.0.0")
        assert len(warnings) >= 1
        assert any("Theme system" in w["message"] for w in warnings)


class TestVersionComparison:
    """Test version comparison logic"""

    def test_version_tuple_comparison(self):
        """Test tuple comparison for versions"""
        assert (27, 1, 48) < (28, 0, 0)
        assert (26, 2, 99) < (27, 0, 0)
        assert (27, 1, 48) == (27, 1, 48)
        assert (28, 0, 0) > (27, 9, 99)


class TestIntegration:
    """Integration tests (require actual project structure)"""

    @pytest.mark.skipif(
        not (Path(__file__).parent.parent / "src" / "WileyWidget").exists(),
        reason="Requires Wiley Widget project structure",
    )
    def test_scan_licenses_on_real_project(self):
        """Test license scan on actual project"""
        from startup_validator import StartupValidator

        root = Path(__file__).parent.parent
        validator = StartupValidator(str(root), verbose=False)
        validator._scan_licenses()

        # Should find at least one license registration
        assert (
            len(validator.licenses["registrations"]) > 0
            or len(validator.licenses["env_fallbacks"]) > 0
        )

    @pytest.mark.skipif(
        not (Path(__file__).parent.parent / "WileyWidget.sln").exists(),
        reason="Requires Wiley Widget solution file",
    )
    def test_scan_assemblies_on_real_project(self):
        """Test assembly scan on actual project"""
        from startup_validator import StartupValidator

        root = Path(__file__).parent.parent
        validator = StartupValidator(str(root), verbose=False)
        validator._scan_assemblies()

        # Should find Syncfusion packages
        assert len(validator.assemblies["packages"]) > 0

        # Check for version consistency
        versions = {pkg["version"] for pkg in validator.assemblies["packages"]}
        print(f"Found versions: {versions}")


if __name__ == "__main__":
    # Run tests with verbose output
    pytest.main([__file__, "-v", "--tb=short"])
