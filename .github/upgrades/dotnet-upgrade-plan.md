# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade src\WileyWidget.Abstractions\WileyWidget.Abstractions.csproj
4. Upgrade src\WileyWidget.Models\WileyWidget.Models.csproj
5. Upgrade src\WileyWidget.Services.Abstractions\WileyWidget.Services.Abstractions.csproj
6. Upgrade src\WileyWidget.Business\WileyWidget.Business.csproj
7. Upgrade src\WileyWidget.Data\WileyWidget.Data.csproj
8. Upgrade src\WileyWidget.Services\WileyWidget.Services.csproj
9. Upgrade WileyWidget.IntegrationTests\WileyWidget.IntegrationTests.csproj
10. Upgrade WileyWidget.Services.UnitTests\WileyWidget.Services.UnitTests.csproj
11. Upgrade tests\WileyWidget.Services.Tests\WileyWidget.Services.Tests.csproj

## Settings

### Excluded projects

| Project name |     Description      |
| :----------- | :------------------: |
| (none)       | No projects excluded |

### Aggregate NuGet packages modifications across all projects

| Package Name                                            | Current Version | New Version | Description                                 |
| :------------------------------------------------------ | :-------------: | :---------: | :------------------------------------------ |
| Microsoft.ApplicationInsights                           |     2.22.0      |   2.23.0    | Deprecated version; update to latest        |
| Microsoft.EntityFrameworkCore                           |     9.0.10      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.EntityFrameworkCore.Design                    |     9.0.10      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.EntityFrameworkCore.InMemory                  |     9.0.10      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.EntityFrameworkCore.SqlServer                 |     9.0.10      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.EntityFrameworkCore.Tools                     |     9.0.10      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Caching.Memory                     |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Configuration                      |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Configuration.EnvironmentVariables |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Configuration.Json                 |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Configuration.UserSecrets          |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.DependencyInjection                |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Diagnostics.HealthChecks           |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Hosting                            |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Http                               |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Logging                            |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| Microsoft.Extensions.Logging.Abstractions               |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility (tests)   |
| Microsoft.Extensions.Options                            |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility (tests)   |
| Microsoft.Extensions.Primitives                         |      9.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| System.Management                                       |      8.0.0      |   10.0.0    | Upgrade for .NET 10 compatibility           |
| System.Net.Http                                         |      4.3.4      | (framework) | Functionality included in .NET 10 framework |
| System.Net.Security                                     |      4.3.2      | (framework) | Functionality included in .NET 10 framework |
| System.Security.Cryptography.X509Certificates           |      4.3.2      | (framework) | Functionality included in .NET 10 framework |
| System.Security.Cryptography.Xml                        |      8.0.2      |   10.0.0    | Upgrade for .NET 10 compatibility           |

### Project upgrade details

#### src\WileyWidget.Abstractions\WileyWidget.Abstractions.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- No package changes detected specifically for this project beyond target framework change.

Other changes:

- Verify WinUI / Windows App SDK compatibility after framework switch.

#### src\WileyWidget.Models\WileyWidget.Models.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update `Microsoft.EntityFrameworkCore` 9.0.10 -> 10.0.0
- Update `Microsoft.EntityFrameworkCore.SqlServer` 9.0.10 -> 10.0.0
- Update `Microsoft.EntityFrameworkCore.Tools` 9.0.10 -> 10.0.0
- Update Microsoft.Extensions.\* packages referenced in this project to 10.0.0 as listed above.

Other changes:

- Run a build to capture API breakages and fix code where EF or Extensions APIs changed.

#### src\WileyWidget.Services.Abstractions\WileyWidget.Services.Abstractions.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update `Microsoft.Extensions.Primitives` 9.0.0 -> 10.0.0
- Update `Microsoft.Extensions.Logging` 9.0.0 -> 10.0.0

#### src\WileyWidget.Business\WileyWidget.Business.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update EF packages to 10.0.0 where referenced
- Update Microsoft.Extensions.\* packages to 10.0.0

Other changes:

- Address EF API breaking changes after build.

#### src\WileyWidget.Data\WileyWidget.Data.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update `Microsoft.EntityFrameworkCore` 9.0.10 -> 10.0.0
- Update `Microsoft.EntityFrameworkCore.Design` 9.0.10 -> 10.0.0
- Update `Microsoft.EntityFrameworkCore.InMemory` 9.0.10 -> 10.0.0
- Update `Microsoft.EntityFrameworkCore.SqlServer` 9.0.10 -> 10.0.0
- Update `Microsoft.EntityFrameworkCore.Tools` 9.0.10 -> 10.0.0
- Update Microsoft.Extensions.\* packages to 10.0.0

Other changes:

- Verify EF migrations and design-time tools after package updates.

#### src\WileyWidget.Services\WileyWidget.Services.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update EF packages to 10.0.0 where referenced
- Update Microsoft.Extensions.\* packages to 10.0.0
- Update `System.Management` 8.0.0 -> 10.0.0
- Update `System.Security.Cryptography.Xml` 8.0.2 -> 10.0.0
- Remove explicit references to `System.Net.Http`, `System.Net.Security`, `System.Security.Cryptography.X509Certificates` if present since .NET 10 includes their functionality in the framework reference.
- Update `Microsoft.ApplicationInsights` 2.22.0 -> 2.23.0

Other changes:

- Rebuild and address any API changes for System.\* and AI packages.

#### WileyWidget.IntegrationTests\WileyWidget.IntegrationTests.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows10.0.26100.0` (keep platform version pin as-is if required).

NuGet packages changes:

- Update Microsoft.Extensions.\* packages to 10.0.0
- Update `Microsoft.EntityFrameworkCore.InMemory` 9.0.10 -> 10.0.0

#### WileyWidget.Services.UnitTests\WileyWidget.Services.UnitTests.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update `Microsoft.Extensions.Logging.Abstractions` 9.0.0 -> 10.0.0

#### tests\WileyWidget.Services.Tests\WileyWidget.Services.Tests.csproj

Project properties changes:

- Target framework should be changed from `net9.0-windows10.0.26100.0` to `net10.0-windows`.

NuGet packages changes:

- Update `Microsoft.Extensions.Options` 9.0.0 -> 10.0.0
