# .NET Repair Procedures for WileyWidget

## Cache Clear Failure Recovery

When `dotnet clean` or cache clearing operations fail, follow these procedures in order.

### Primary Repair: Windows Features Reset

1. **Open Control Panel**
   - Search for "Control Panel" in Windows search
   - Navigate to "Programs" → "Turn Windows features on or off"

2. **Locate .NET Framework 4.8+**
   - Scroll down to find ".NET Framework 4.8 Advanced Services" or similar
   - **Uncheck** the .NET Framework 4.8 option
   - Click "OK" and wait for the uninstall to complete

3. **Restart Computer**
   - Required after uninstalling .NET Framework

4. **Re-enable .NET Framework**
   - Return to "Turn Windows features on or off"
   - **Check** the .NET Framework 4.8 option again
   - Click "OK" and wait for installation to complete

5. **Restart Computer Again**
   - Required after reinstalling .NET Framework

### Alternative Repair: Winget Reinstall

If the Windows Features method fails:

```powershell
# Uninstall .NET Runtime
winget uninstall Microsoft.DotNet.Runtime.8

# Uninstall .NET SDK (if installed)
winget uninstall Microsoft.DotNet.SDK.8

# Reinstall .NET Runtime
winget install Microsoft.DotNet.Runtime.8

# Reinstall .NET SDK (if needed)
winget install Microsoft.DotNet.SDK.8
```

### Verification Steps

After repair, verify .NET installation:

```powershell
# Check .NET version
dotnet --version

# Check .NET info
dotnet --info

# Test build
dotnet build WileyWidget.csproj
```

### Additional Troubleshooting

#### Clear NuGet Cache
```powershell
# Clear all NuGet caches
dotnet nuget locals all --clear

# Clear NuGet HTTP cache
nuget locals http-cache -clear
```

#### Clear .NET Tool Cache
```powershell
# Clear .NET tools cache
dotnet tool list -g | ForEach-Object { dotnet tool uninstall -g $_.Split()[0] }
```

#### Reset Visual Studio (if applicable)
- Close Visual Studio
- Delete `.vs` folder in project root
- Delete `bin` and `obj` folders
- Restart Visual Studio

#### Check Windows Environment
```powershell
# Check PATH environment variable
echo $env:PATH

# Check for conflicting .NET installations
where dotnet
```

### Prevention Measures

To prevent future cache issues:

1. **Use Consistent .NET Version**
   - Pin to specific .NET 8.x version in `global.json`
   - Avoid mixing .NET versions

2. **Regular Maintenance**
   ```powershell
   # Weekly cleanup
   dotnet clean
   dotnet nuget locals all --clear
   ```

3. **Use Build Scripts**
   - Always use provided build scripts instead of manual commands
   - Scripts include proper cleanup and cache management

### Emergency Recovery

If all else fails:

1. **Backup Source Code**
2. **Delete Repository**
3. **Re-clone from Git**
4. **Run Initial Setup**
   ```powershell
   # Fresh environment setup
   .\scripts\setup-environment-variables.ps1
   .\scripts\load-env.ps1
   dotnet restore
   ```

### Support Resources

- [.NET Download Page](https://dotnet.microsoft.com/download)
- [Winget Package Reference](https://winget.run/pkg/Microsoft/DotNet)
- [Visual Studio Repair Tool](https://learn.microsoft.com/en-us/visualstudio/install/repair-visual-studio)

### Logging Cache Issues

When reporting cache problems, include:

- `dotnet --info` output
- Windows version and build
- Visual Studio version (if applicable)
- Exact error messages
- Steps taken before failure