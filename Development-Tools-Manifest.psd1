# Development Tools Manifest - Wiley Widget Project
# Following Microsoft PowerShell 7.5.2 and MCP best practices
# Generated: 2025-08-28
# Version: 1.0.0

@{
    # Project Metadata
    Project = @{
        Name = "Wiley Widget"
        Version = "1.0.0"
        Framework = "net8.0-windows"
        PowerShellVersion = "7.5.2"
        LastUpdated = "2025-08-28"
    }

    # Core Development Tools
    CoreTools = @{

        # PowerShell Ecosystem
        PowerShell = @{
            Version = "7.5.2"
            Path = "C:\Program Files\PowerShell\7\pwsh.exe"
            Profile = "$env:USERPROFILE\Documents\PowerShell\Microsoft.PowerShell_profile.ps1"
            Modules = @(
                @{
                    Name = "PSScriptAnalyzer"
                    Version = "1.24.0"
                    Purpose = "Static code analysis and linting"
                    Required = $true
                    AutoUpdate = $true
                },
                @{
                    Name = "Pester"
                    Version = "5.5.0"
                    Purpose = "Testing framework"
                    Required = $true
                    AutoUpdate = $true
                },
                @{
                    Name = "platyPS"
                    Version = "0.14.2"
                    Purpose = "Help documentation generation"
                    Required = $true
                    AutoUpdate = $true
                },
                @{
                    Name = "PSReadLine"
                    Version = "2.3.4"
                    Purpose = "Enhanced command-line editing"
                    Required = $true
                    AutoUpdate = $true
                },
                @{
                    Name = "PSFramework"
                    Version = "1.12.346"
                    Purpose = "PowerShell development framework"
                    Required = $false
                    AutoUpdate = $true
                },
                @{
                    Name = "ImportExcel"
                    Version = "7.8.6"
                    Purpose = "Excel file manipulation"
                    Required = $false
                    AutoUpdate = $true
                }
            )
        }

        # .NET Development Tools
        DotNet = @{
            Version = "8.0"
            SDK = "8.0.401"
            Runtime = "8.0.8"
            Tools = @(
                @{
                    Name = "dotnet-ef"
                    Version = "8.0.8"
                    Purpose = "Entity Framework CLI"
                    Required = $true
                },
                @{
                    Name = "Microsoft.Web.LibraryManager.Cli"
                    Version = "2.1.175"
                    Purpose = "Library manager for client-side packages"
                    Required = $false
                }
            )
        }

        # Node.js Ecosystem (for MCP and tooling)
        NodeJS = @{
            Version = "20.11.1"
            NPM = "10.5.0"
            Packages = @(
                @{
                    Name = "@modelcontextprotocol/server-github"
                    Version = "latest"
                    Purpose = "GitHub MCP server for AI integration"
                    Required = $true
                    Type = "global"
                }
            )
        }
    }

    # IDE and Editor Tools
    IDE = @{
        VSCode = @{
            Version = "1.93.0"
            Extensions = @(
                @{
                    Name = "ms-vscode.powershell"
                    Version = "2024.2.2"
                    Purpose = "PowerShell language support"
                    Required = $true
                },
                @{
                    Name = "ms-dotnettools.dotnet-interactive-vscode"
                    Version = "1.0.5261010"
                    Purpose = ".NET Interactive Notebooks"
                    Required = $true
                },
                @{
                    Name = "ms-vscode.test-adapter-converter"
                    Version = "0.1.9"
                    Purpose = "Test adapter for Pester"
                    Required = $true
                },
                @{
                    Name = "github.copilot"
                    Version = "1.204.0"
                    Purpose = "GitHub Copilot AI assistance"
                    Required = $true
                },
                @{
                    Name = "github.copilot-chat"
                    Version = "0.17.0"
                    Purpose = "GitHub Copilot Chat"
                    Required = $true
                }
            )
            Settings = @{
                Path = ".vscode\settings.json"
                LastUpdated = "2025-08-28"
            }
        }
    }

    # MCP (Model Context Protocol) Configuration
    MCP = @{
        Version = "2024-11-05"
        Servers = @(
            @{
                Name = "github"
                Type = "remote"
                URL = "wss://your-mcp-server.example.com/github"
                Purpose = "GitHub repository integration"
                Authentication = "Bearer"
                Headers = @{
                    "Authorization" = "Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}"
                }
                Reconnect = $true
                ReconnectInterval = 5000
                Timeout = 30000
                Enabled = $true
            }
        )
        Clients = @(
            @{
                Name = "VSCode"
                Version = "1.93.0"
                MCPVersion = "1.0.0"
                ConfigurationPath = ".vscode\settings.json"
            }
        )
    }

    # CI/CD and Quality Tools
    QualityTools = @{
        Trunk = @{
            Version = "1.21.0"
            ConfigPath = ".trunk\trunk.yaml"
            Linters = @(
                @{
                    Name = "psscriptanalyzer"
                    Version = "1.24.0"
                    Purpose = "PowerShell static analysis"
                    Enabled = $true
                }
            )
        }
        Git = @{
            Version = "2.45.0"
            Hooks = @(
                @{
                    Name = "pre-commit"
                    Purpose = "Code quality checks before commit"
                    Enabled = $true
                },
                @{
                    Name = "commit-msg"
                    Purpose = "Commit message validation"
                    Enabled = $true
                }
            )
        }
    }

    # Environment Configuration
    Environment = @{
        Variables = @(
            @{
                Name = "WILEY_WIDGET_ROOT"
                Value = "C:\Users\biges\Desktop\Wiley_Widget"
                Scope = "Machine"
                Purpose = "Project root directory"
            },
            @{
                Name = "WILEY_WIDGET_CONFIG"
                Value = "Release"
                Scope = "Process"
                Purpose = "Build configuration"
            },
            @{
                Name = "POWERSHELL_EXECUTION_POLICY"
                Value = "RemoteSigned"
                Scope = "Machine"
                Purpose = "PowerShell execution policy"
            },
            @{
                Name = "GITHUB_PERSONAL_ACCESS_TOKEN"
                Value = "********"  # Masked for security
                Scope = "Process"
                Purpose = "GitHub API authentication"
                Secure = $true
            }
        )
        Paths = @(
            @{
                Name = "Scripts"
                Path = "%WILEY_WIDGET_ROOT%\scripts"
                Purpose = "Project scripts directory"
                AddedToPath = $true
            },
            @{
                Name = "PowerShellModules"
                Path = "%USERPROFILE%\Documents\PowerShell\Modules"
                Purpose = "User PowerShell modules"
                AddedToPath = $false
            }
        )
    }

    # Compliance and Security
    Compliance = @{
        PowerShell = @{
            Version = "7.5.2"
            ApprovedVerbs = $true
            ScriptAnalyzer = $true
            ExecutionPolicy = "RemoteSigned"
        }
        Security = @{
            SecureStringUsage = $true
            TokenEncryption = $true
            EnvironmentIsolation = $true
        }
        Microsoft = @{
            MCPCompliance = $true
            PowerShellBestPractices = $true
            AzureIntegration = $false
        }
    }

    # Maintenance and Updates
    Maintenance = @{
        AutoUpdate = @{
            Enabled = $true
            Schedule = "Weekly"
            Tools = @("PSScriptAnalyzer", "Pester", "VSCodeExtensions")
        }
        HealthChecks = @{
            Enabled = $true
            Frequency = "Daily"
            Checks = @("ModuleVersions", "EnvironmentVariables", "MCPConnections")
        }
        Backup = @{
            Enabled = $true
            Frequency = "Weekly"
            Paths = @(".vscode", "scripts", ".env")
        }
    }
}
