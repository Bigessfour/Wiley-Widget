// Global using aliases to resolve type ambiguities between multiple assemblies
extern alias WWUI;
extern alias WWServices;

// Prefer WileyWidget.UI versions of ViewModels where they exist
global using SettingsViewModel = WWUI::WileyWidget.ViewModels.Main.SettingsViewModel;

// Prefer WileyWidget.Services versions of services
global using SettingsService = WWServices::WileyWidget.Services.SettingsService;
