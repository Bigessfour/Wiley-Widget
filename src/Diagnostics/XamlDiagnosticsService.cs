using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Xaml;
using Microsoft.Extensions.Logging;
using Serilog;
using Syncfusion.Licensing;
using Syncfusion.SfSkinManager;

namespace WileyWidget.Diagnostics
{
    /// <summary>
    /// Comprehensive XAML validation and diagnostics service
    /// </summary>
    public class XamlDiagnosticsService
    {
        private readonly ILogger<XamlDiagnosticsService> _logger;

        public XamlDiagnosticsService(ILogger<XamlDiagnosticsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs comprehensive XAML diagnostics
        /// </summary>
        public XamlDiagnosticsResult RunFullDiagnostics()
        {
            var result = new XamlDiagnosticsResult();

            try
            {
                _logger.LogInformation("Starting comprehensive XAML diagnostics");

                // Check Syncfusion license status
                result.SyncfusionLicenseValid = ValidateSyncfusionLicense();

                // Check SfSkinManager theme status
                result.SfSkinManagerConfigured = ValidateSfSkinManager();

                // Validate xmlns declarations in XAML files
                result.XamlFilesValidated = ValidateXamlFiles();

                // Check ViewModelLocator configuration
                result.ViewModelLocatorConfigured = ValidateViewModelLocator();

                // Validate assembly references
                result.RequiredAssembliesLoaded = ValidateRequiredAssemblies();

                result.Success = result.SyncfusionLicenseValid &&
                               result.SfSkinManagerConfigured &&
                               result.XamlFilesValidated &&
                               result.ViewModelLocatorConfigured &&
                               result.RequiredAssembliesLoaded;

                _logger.LogInformation("XAML diagnostics completed. Success: {Success}", result.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "XAML diagnostics failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Validates Syncfusion license registration
        /// </summary>
        private bool ValidateSyncfusionLicense()
        {
            try
            {
                _logger.LogDebug("Validating Syncfusion license");

                // Note: Syncfusion doesn't provide a direct license validation API
                // We can only check if a license key was registered, but not if it's valid
                // Invalid licenses will cause evaluation dialogs at runtime

                // Try to access a Syncfusion component to see if it throws an exception
                // This is an indirect way to check if licensing is working
                try
                {
                    // Create a simple Syncfusion component to test licensing
                    using var testTheme = new Syncfusion.SfSkinManager.Theme("FluentLight");
                    _logger.LogInformation("Syncfusion license appears to be working (no immediate licensing errors)");
                    return true;
                }
                catch (Exception themeEx)
                {
                    _logger.LogWarning(themeEx, "Syncfusion license validation failed - components may show evaluation dialogs");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate Syncfusion license");
                return false;
            }
        }

        /// <summary>
        /// Validates SfSkinManager theme configuration
        /// </summary>
        private bool ValidateSfSkinManager()
        {
            try
            {
                _logger.LogDebug("Validating SfSkinManager configuration");

                // Check if theme is applied globally
                var theme = SfSkinManager.ApplicationTheme;
                if (theme == null)
                {
                    _logger.LogWarning("SfSkinManager.ApplicationTheme is null");
                    return false;
                }

                _logger.LogInformation("SfSkinManager theme configured successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate SfSkinManager configuration");
                return false;
            }
        }

        /// <summary>
        /// Validates XAML files for common issues
        /// </summary>
        private bool ValidateXamlFiles()
        {
            try
            {
                _logger.LogDebug("Validating XAML files");

                var xamlFiles = Directory.GetFiles("src", "*.xaml", SearchOption.AllDirectories);
                var issuesFound = false;

                foreach (var xamlFile in xamlFiles)
                {
                    if (!ValidateSingleXamlFile(xamlFile))
                    {
                        issuesFound = true;
                    }
                }

                if (!issuesFound)
                {
                    _logger.LogInformation("All XAML files validated successfully");
                }

                return !issuesFound;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate XAML files");
                return false;
            }
        }

        /// <summary>
        /// Validates a single XAML file
        /// </summary>
        private bool ValidateSingleXamlFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var issues = new List<string>();

                // Check for required xmlns declarations
                if (!content.Contains("xmlns:syncfusion=\"http://schemas.syncfusion.com/wpf\"", StringComparison.Ordinal))
                {
                    issues.Add("Missing Syncfusion xmlns declaration");
                }

                if (!content.Contains("xmlns:prism=\"http://prismlibrary.com/\"", StringComparison.Ordinal))
                {
                    issues.Add("Missing Prism xmlns declaration");
                }

                if (!content.Contains("prism:ViewModelLocator.AutoWireViewModel=\"True\"", StringComparison.Ordinal))
                {
                    issues.Add("Missing or incorrect ViewModelLocator.AutoWireViewModel setting");
                }

                // Check for SfSkinManager theme application
                if (content.Contains("<syncfusion:", StringComparison.Ordinal) &&
                    !content.Contains("syncfusionskin:SfSkinManager.VisualStyle", StringComparison.Ordinal))
                {
                    issues.Add("Syncfusion controls found but SfSkinManager theme not applied");
                }

                if (issues.Any())
                {
                    _logger.LogWarning("XAML validation issues in {File}: {Issues}",
                        Path.GetFileName(filePath), string.Join(", ", issues));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate XAML file {File}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Validates ViewModelLocator configuration
        /// </summary>
        private bool ValidateViewModelLocator()
        {
            try
            {
                _logger.LogDebug("Validating ViewModelLocator configuration");

                // Check if Prism ViewModelLocator is available
                var viewModelLocatorType = Type.GetType("Prism.Mvvm.ViewModelLocator, Prism.Wpf");
                if (viewModelLocatorType == null)
                {
                    _logger.LogWarning("Prism ViewModelLocator type not found");
                    return false;
                }

                _logger.LogInformation("ViewModelLocator configuration validated");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate ViewModelLocator configuration");
                return false;
            }
        }

        /// <summary>
        /// Validates that required assemblies are loaded
        /// </summary>
        private bool ValidateRequiredAssemblies()
        {
            var requiredAssemblies = new[]
            {
                "Microsoft.Xaml.Behaviors.Wpf",
                "Prism.Wpf",
                // Validate the DryIoc Prism container assembly instead of legacy Unity
                "Prism.Container.DryIoc",
                "Syncfusion.SfSkinManager.WPF",
                "Syncfusion.SfGrid.WPF",
                "Syncfusion.Licensing"
            };

            var missingAssemblies = new List<string>();

            foreach (var assemblyName in requiredAssemblies)
            {
                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    if (assembly == null)
                    {
                        missingAssemblies.Add(assemblyName);
                    }
                }
                catch
                {
                    missingAssemblies.Add(assemblyName);
                }
            }

            if (missingAssemblies.Any())
            {
                _logger.LogWarning("Missing required assemblies: {Assemblies}",
                    string.Join(", ", missingAssemblies));
                return false;
            }

            _logger.LogInformation("All required assemblies loaded successfully");
            return true;
        }

        /// <summary>
        /// Attempts to recover from common XAML issues
        /// </summary>
        public bool AttemptRecovery()
        {
            try
            {
                _logger.LogInformation("Attempting XAML issue recovery");

                // Re-register Syncfusion license if needed
                if (!ValidateSyncfusionLicense())
                {
                    _logger.LogInformation("Attempting to re-register Syncfusion license");
                    // License re-registration would be handled by the license service
                }

                // Re-apply SfSkinManager theme if needed
                if (!ValidateSfSkinManager())
                {
                    _logger.LogInformation("Attempting to re-apply SfSkinManager theme");
                    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
                }

                _logger.LogInformation("XAML recovery attempt completed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "XAML recovery failed");
                return false;
            }
        }
    }

    /// <summary>
    /// Result of XAML diagnostics
    /// </summary>
    public class XamlDiagnosticsResult
    {
        public bool Success { get; set; }
        public bool SyncfusionLicenseValid { get; set; }
        public bool SfSkinManagerConfigured { get; set; }
        public bool XamlFilesValidated { get; set; }
        public bool ViewModelLocatorConfigured { get; set; }
        public bool RequiredAssembliesLoaded { get; set; }
        public string? ErrorMessage { get; set; }

        public override string ToString()
        {
            return $"Success: {Success}, License: {SyncfusionLicenseValid}, Theme: {SfSkinManagerConfigured}, " +
                   $"XAML: {XamlFilesValidated}, VM Locator: {ViewModelLocatorConfigured}, Assemblies: {RequiredAssembliesLoaded}";
        }
    }
}
