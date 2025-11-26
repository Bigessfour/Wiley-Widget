# WileyWidget.WinUI

## Overview

**WileyWidget.WinUI** is the modern WinUI 3 shell application for the Wiley Widget project. This project serves as the new packaged Windows desktop application built on the Windows App SDK, replacing the legacy WPF implementation while maintaining compatibility with existing business logic and data layers.

## Architecture

### Technology Stack

- **Framework**: .NET 9.0
- **UI Framework**: WinUI 3 (Windows App SDK 1.6)
- **Target Platform**: Windows 10 (19041+) and Windows 11
- **Packaging**: MSIX (Windows App SDK packaging)

### Project Structure

```
WileyWidget.WinUI/
├── App.xaml                    # Application definition
├── App.xaml.cs                 # Application startup logic
├── MainWindow.xaml             # Main window UI
├── MainWindow.xaml.cs          # Main window code-behind
├── Package.appxmanifest        # App package manifest
├── app.manifest                # Application manifest (DPI awareness)
├── Assets/                     # Application assets (icons, images)
├── Properties/                 # Assembly information
└── WileyWidget.WinUI.csproj   # Project file
```

### Shared Dependencies

This project references the following shared libraries from the Wiley Widget ecosystem:

- **WileyWidget.Models**: Data models and entities
- **WileyWidget.Services**: Business services implementation
- **WileyWidget.Services.Abstractions**: Service interfaces
- **WileyWidget.Business**: Business logic layer
- **WileyWidget.Data**: Data access layer
- **WileyWidget.Abstractions**: Core abstractions and interfaces
- **WileyWidget.UI**: Shared XAML resources and controls

## Development

### Prerequisites

- Visual Studio 2022 17.8+ with Windows App SDK workload
- .NET 9.0 SDK
- Windows 10 SDK (19041) or newer

### Building

```powershell
# Build the project
dotnet build src/WileyWidget.WinUI/WileyWidget.WinUI.csproj

# Or build the entire solution
dotnet build WileyWidget.sln
```

### Running

```powershell
# Run in unpackaged mode (for debugging)
dotnet run --project src/WileyWidget.WinUI/WileyWidget.WinUI.csproj
```

### Debugging in Visual Studio

1. Open `WileyWidget.sln` in Visual Studio 2022
2. Set `WileyWidget.WinUI` as the startup project
3. Press F5 to build and run

## Migration Path

This project is part of the **WPF to WinUI 3 migration strategy**:

### Phase 1: Initial Setup ✅

- [x] Create WinUI 3 project structure
- [x] Add shared project references
- [x] Configure package manifest
- [x] Basic window and app lifecycle

### Phase 2: UI Migration (In Progress)

- [ ] Port existing Views and ViewModels
- [ ] Adapt XAML to WinUI 3 syntax
- [ ] Implement navigation framework
- [ ] Migrate custom controls

### Phase 3: Integration

- [ ] Wire up services and business logic
- [ ] Database connectivity
- [ ] QuickBooks integration
- [ ] Authentication and authorization

### Phase 4: Feature Parity

- [ ] Complete feature migration from WPF app
- [ ] Performance optimization
- [ ] Testing and validation
- [ ] User acceptance testing

## Key Differences from WPF

### WinUI 3 Advantages

✅ **Modern Windows 11 Design**: Fluent Design, Mica materials, rounded corners  
✅ **Better Performance**: Native Windows rendering, GPU acceleration  
✅ **Touch and Pen Support**: Built-in modern input methods  
✅ **MSIX Packaging**: Simplified deployment and updates  
✅ **Windows App SDK**: Regular updates independent of Windows releases

### Migration Considerations

⚠️ **Namespace Changes**: `System.Windows` → `Microsoft.UI.Xaml`  
⚠️ **Control Differences**: Some WPF controls have different names/behavior  
⚠️ **Data Binding**: Mostly compatible, but some syntax differences  
⚠️ **Resources**: Different theme resource keys

## Configuration

### Target Framework

```xml
<TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
<TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
```

### Supported Architectures

- x86 (32-bit)
- x64 (64-bit)
- ARM64 (Surface and Windows on ARM devices)

## Packaging

The app uses MSIX packaging via Windows App SDK:

- **Unpackaged mode**: For development and debugging
- **Packaged mode**: For distribution via Microsoft Store or sideloading

## Resources

- [WinUI 3 Documentation](https://learn.microsoft.com/windows/apps/winui/winui3/)
- [Windows App SDK Documentation](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [Migration Guide: WPF to WinUI 3](https://learn.microsoft.com/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/wpf)

## Status

🚧 **In Development** - Initial setup complete, migration in progress

---

**Last Updated**: November 12, 2025  
**Project Version**: 1.0.0.0
