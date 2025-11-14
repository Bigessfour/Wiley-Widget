# Dockerfile for WinUI Development (Linux-based for Docker Compose)
# Note: For production MSIX, build on Windows host

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY WileyWidget.sln .
COPY Directory.Build.props .
COPY Directory.Build.targets .
COPY Directory.Packages.props .
COPY global.json .
COPY NuGet.config .

# Copy all project files for restore
COPY src/WileyWidget.WinUI/*.csproj src/WileyWidget.WinUI/
COPY src/WileyWidget.Abstractions/*.csproj src/WileyWidget.Abstractions/
COPY src/WileyWidget.Business/*.csproj src/WileyWidget.Business/
COPY src/WileyWidget.Models/*.csproj src/WileyWidget.Models/
COPY src/WileyWidget.Services/*.csproj src/WileyWidget.Services/
COPY src/WileyWidget.Services.Abstractions/*.csproj src/WileyWidget.Services.Abstractions/

# Restore dependencies
RUN dotnet restore WileyWidget.sln

# Copy everything else
COPY . .

# Build in Release mode
WORKDIR /src/src/WileyWidget.WinUI
RUN dotnet publish -c Release -o /app/publish --no-restore

# ──────────────────────────────────────────────────────────────
# Runtime stage (for dev only - production uses Windows Container)
# ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Note: WinUI apps cannot run headless in Linux containers
# This stage is for API/service testing only
# For full WinUI UI testing, use Windows Container or local build

ENTRYPOINT ["dotnet", "WileyWidget.WinUI.dll"]
