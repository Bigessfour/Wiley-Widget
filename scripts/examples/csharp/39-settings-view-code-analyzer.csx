// Settings View Code Analyzer
// Purpose: Validate that SettingsView and SettingsViewModel exist and are registered for navigation
// and that region registration is present in SettingsModule/CoreModule.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable

Console.WriteLine("[39] Settings View Code Analyzer - START\n");

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT");
if (string.IsNullOrWhiteSpace(repoRoot)) repoRoot = Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);

// Targets
var settingsModulePath = Path.Combine(repoRoot, "src", "Startup", "Modules", "SettingsModule.cs");
var coreModulePath = Path.Combine(repoRoot, "src", "Startup", "Modules", "CoreModule.cs");
var uiSettingsViewPath = Path.Combine(repoRoot, "WileyWidget.UI", "Views", "Main", "SettingsView.xaml.cs");
var uiSettingsViewModelPath = Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "SettingsViewModel.cs");

bool existsSettingsModule = File.Exists(settingsModulePath);
bool existsCoreModule = File.Exists(coreModulePath);
bool existsView = File.Exists(uiSettingsViewPath);
bool existsViewModel = File.Exists(uiSettingsViewModelPath);

string settingsModuleSrc = existsSettingsModule ? File.ReadAllText(settingsModulePath) : string.Empty;
string coreModuleSrc = existsCoreModule ? File.ReadAllText(coreModulePath) : string.Empty;

bool hasRegisterForNavigation = settingsModuleSrc.IndexOf("RegisterForNavigation<SettingsView, SettingsViewModel>", StringComparison.OrdinalIgnoreCase) >= 0
    || coreModuleSrc.IndexOf("RegisterForNavigation<SettingsView, SettingsViewModel>", StringComparison.OrdinalIgnoreCase) >= 0;

bool hasRegionRegistration = settingsModuleSrc.IndexOf("RegisterViewWithRegion(\"SettingsRegion\", typeof(SettingsView))", StringComparison.OrdinalIgnoreCase) >= 0
    || coreModuleSrc.IndexOf("RegisterViewWithRegion(\"SettingsRegion\", typeof(SettingsView))", StringComparison.OrdinalIgnoreCase) >= 0;

var result = new {
    analyzedAt = DateTime.UtcNow,
    existsView,
    existsViewModel,
    existsSettingsModule,
    existsCoreModule,
    hasRegisterForNavigation,
    hasRegionRegistration,
    files = new {
        settingsModulePath,
        coreModulePath,
        uiSettingsViewPath,
        uiSettingsViewModelPath
    }
};

string outFile = Path.Combine(logsDir, "settings-view-code-analyzer.json");
var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions{ WriteIndented = true });
File.WriteAllText(outFile, json);

Console.WriteLine("[39] Output -> " + outFile);
Console.WriteLine(json);
Console.WriteLine("\n[39] Settings View Code Analyzer - END");
