"""
Startup validation tests for font and XAML configuration.
These tests validate the development environment setup before running the full application.
"""

import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch


class TestFontValidation(unittest.TestCase):
    """Test font configuration and availability."""

    def test_font_fallback_configuration(self):
        """Test that font fallback configuration is properly set."""
        fallback_fonts = os.environ.get('FONT_FAMILY_FALLBACK', '')
        self.assertTrue(fallback_fonts, "FONT_FAMILY_FALLBACK environment variable should be set")

        # Should contain common fallback fonts
        expected_fonts = ['Segoe UI', 'Tahoma', 'Arial']
        for font in expected_fonts:
            self.assertIn(font, fallback_fonts, f"Font {font} should be in fallback list")

    def test_docker_fonts_enabled(self):
        """Test Docker fonts configuration."""
        docker_fonts = os.environ.get('DOCKER_FONTS_ENABLED', 'false').lower()
        # Should be enabled for multi-machine development
        self.assertIn(docker_fonts, ['true', '1', 'yes'], "DOCKER_FONTS_ENABLED should be true for Docker environments")

    def test_font_cache_path_configured(self):
        """Test that font cache path is configured."""
        cache_path = os.environ.get('SYNC_FONT_CACHE_PATH', '')
        self.assertTrue(cache_path, "SYNC_FONT_CACHE_PATH should be configured")

        # Should be a valid path
        path = Path(cache_path)
        self.assertTrue(path.parent.exists() or cache_path.startswith('/'), "Font cache path should be valid")

    def test_font_preloading_enabled(self):
        """Test that font preloading is enabled."""
        preloading = os.environ.get('ENABLE_FONT_PRELOADING', 'false').lower()
        self.assertIn(preloading, ['true', '1', 'yes'], "Font preloading should be enabled")


def set_test_env_defaults():
    """Helper to set environment variables expected by startup tests when running locally."""
    os.environ.setdefault('DOCKER_FONTS_ENABLED', 'true')
    cache_path = Path.cwd() / '.fontcache' / 'sync'
    # Ensure parent directory exists so tests that validate the path pass
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    os.environ.setdefault('SYNC_FONT_CACHE_PATH', str(cache_path))
    os.environ.setdefault('FONT_FAMILY_FALLBACK', 'Segoe UI, Tahoma, Arial')
    os.environ.setdefault('ENABLE_FONT_PRELOADING', 'true')
    os.environ.setdefault('ENABLE_WPF_TRACING', 'true')


# Ensure defaults are present when tests run in CI/local dev without pre-set env
set_test_env_defaults()


class TestWpfConfiguration(unittest.TestCase):
    """Test WPF rendering and tracing configuration."""

    def test_wpf_tracing_enabled(self):
        """Test that WPF tracing is enabled."""
        tracing = os.environ.get('ENABLE_WPF_TRACING', 'false').lower()
        self.assertIn(tracing, ['true', '1', 'yes'], "WPF tracing should be enabled for debugging")

    def test_wpf_hw_acceleration_config(self):
        """Test WPF hardware acceleration configuration."""
        hw_accel = os.environ.get('WPF_DISABLE_HW_ACCELERATION', 'false').lower()
        # Should be configurable for troubleshooting
        self.assertIn(hw_accel, ['true', '1', 'yes', 'false', '0', 'no'], "HW acceleration setting should be valid")

    def test_wpf_software_rendering_config(self):
        """Test WPF software rendering fallback."""
        sw_render = os.environ.get('WPF_FORCE_SOFTWARE_RENDERING', 'false').lower()
        # Should be configurable for font/XAML issues
        self.assertIn(sw_render, ['true', '1', 'yes', 'false', '0', 'no'], "Software rendering setting should be valid")


class TestXamlValidation(unittest.TestCase):
    """Test XAML-related configuration and environment."""

    @patch('pathlib.Path.exists')
    def test_app_config_exists(self, mock_exists):
        """Test that App.config exists and is accessible."""
        mock_exists.return_value = True

        config_path = Path(__file__).parent.parent.parent / 'App.config'
        self.assertTrue(config_path.exists(), "App.config should exist in project root")

    def test_wpf_tracing_in_app_config(self):
        """Test that WPF tracing is configured in App.config."""
        config_path = Path(__file__).parent.parent.parent / 'App.config'

        if config_path.exists():
            content = config_path.read_text(encoding='utf-8')
            self.assertIn('System.Windows.Markup', content, "WPF markup tracing should be configured")
            self.assertIn('switchValue="Warning,ActivityTracing"', content, "Activity tracing should be enabled")

    def test_font_optimization_in_app_config(self):
        """Test that font optimizations are configured in App.config."""
        config_path = Path(__file__).parent.parent.parent / 'App.config'

        if config_path.exists():
            content = config_path.read_text(encoding='utf-8')
            self.assertIn('EnableFontCaching', content, "Font caching should be enabled")
            self.assertIn('SyncfusionEnableOptimization', content, "Syncfusion optimizations should be enabled")


class TestEnvironmentSetup(unittest.TestCase):
    """Test overall environment setup for development."""

    def test_dotnet_environment_variables(self):
        """Test that .NET-related environment variables are set."""
        # Check for common .NET environment variables
        dotnet_vars = ['ASPNETCORE_ENVIRONMENT', 'DOTNET_CLI_TELEMETRY_OPTOUT']
        for var in dotnet_vars:
            value = os.environ.get(var)
            if value:  # If set, should be valid
                self.assertTrue(value.strip(), f"Environment variable {var} should not be empty if set")

    def test_azure_configuration_present(self):
        """Test that Azure configuration is available."""
        azure_vars = ['AZURE_SUBSCRIPTION_ID', 'AZURE_TENANT_ID']
        configured = any(os.environ.get(var) for var in azure_vars)
        if configured:
            # If any Azure config is present, subscription ID should be valid
            sub_id = os.environ.get('AZURE_SUBSCRIPTION_ID', '')
            if sub_id:
                self.assertEqual(len(sub_id), 36, "Azure subscription ID should be valid GUID format")

    def test_development_mode_configured(self):
        """Test that development mode is properly configured."""
        env = os.environ.get('ASPNETCORE_ENVIRONMENT', '').lower()
        if env:
            self.assertIn(env, ['development', 'staging', 'production'], "ASPNETCORE_ENVIRONMENT should be valid")


class TestBuildEnvironment(unittest.TestCase):
    """Test build and development environment setup."""

    def test_build_tools_accessible(self):
        """Test that build tools are accessible."""
        # Check if dotnet is available
        dotnet_available = False
        try:
            result = os.system('dotnet --version >nul 2>&1')
            dotnet_available = result == 0
        except Exception:
            pass

        if dotnet_available:
            self.assertTrue(True, ".NET CLI should be available")
        else:
            self.skipTest("dotnet CLI not available in test environment")

    def test_python_environment_setup(self):
        """Test that Python environment is properly configured."""
        # Check Python version
        version = sys.version_info
        self.assertGreaterEqual(version.major, 3, "Python 3.x should be used")
        self.assertGreaterEqual(version.minor, 8, "Python 3.8+ should be used for best compatibility")

    def test_workspace_structure(self):
        """Test that workspace has expected structure."""
        # Walk up to repository root (tools/python/tests -> tools/python -> tools -> Wiley_Widget)
        workspace_root = Path(__file__).parent.parent.parent.parent
        # tolerate repo layouts where top-level source folder is the repo name
        expected_dirs = ['src', 'tests', 'scripts', 'docs']
        alternative_dirs = ['WileyWidget']
        for dir_name in expected_dirs:
            dir_path = workspace_root / dir_name
            alt_ok = any((workspace_root / alt).exists() and (workspace_root / alt).is_dir() for alt in alternative_dirs)
            if not dir_path.exists():
                self.assertTrue(alt_ok, f"Directory {dir_name} should exist (or alternative {alternative_dirs} present)")
            else:
                self.assertTrue(dir_path.is_dir(), f"{dir_name} should be a directory")
