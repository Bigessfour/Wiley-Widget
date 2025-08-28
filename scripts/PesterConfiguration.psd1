# Pester Configuration for Wiley Widget Project
# Following Microsoft PowerShell 7.5.2 and Pester 5.5.0 best practices
# https://pester.dev/docs/quick-start

@{
    # Run configuration
    Run = @{
        # Path to test files
        Path = @(
            './WileyWidget.Tests',
            './scripts/Tests'
        )

        # Test execution options
        PassThru = $true
        Quiet = $false
        SkipRemainingOnFailure = 'None'

        # Container settings
        Container = @{
            # Data to share between test containers
            Data = @{
                ProjectRoot = $PSScriptRoot
                TestDataPath = Join-Path $PSScriptRoot 'TestData'
            }
        }
    }

    # Code coverage configuration
    CodeCoverage = @{
        # Enable code coverage
        Enabled = $true

        # Output format and path
        OutputFormat = 'JaCoCo'
        OutputPath = './TestResults/coverage.xml'
        OutputEncoding = 'UTF8'

        # Paths to include in coverage analysis
        Path = @(
            './WileyWidget/*.ps1',
            './WileyWidget/*.psm1',
            './scripts/*.ps1',
            './Models/*.cs',
            './ViewModels/*.cs'
        )

        # Paths to exclude from coverage
        ExcludePath = @(
            './WileyWidget.Tests/*',
            './scripts/Tests/*',
            './TestResults/*',
            './obj/*',
            './bin/*'
        )

        # Coverage thresholds
        CoveragePercentTarget = 80
        FunctionCoverageThreshold = 75
        BranchCoverageThreshold = 70

        # Recurse into subdirectories
        RecursePaths = $true
    }

    # Test result configuration
    TestResult = @{
        # Enable test results output
        Enabled = $true

        # Output format and path
        OutputFormat = 'NUnitXml'
        OutputPath = './TestResults/test-results.xml'
        OutputEncoding = 'UTF8'

        # Test result options
        TestSuiteName = 'WileyWidget'
    }

    # Output configuration
    Output = @{
        # Output verbosity
        Verbosity = 'Detailed'

        # Stack trace options
        StackTraceVerbosity = 'FirstLine'

        # CI/CD integration
        CIFormat = 'Auto'
    }

    # Filter configuration
    Filter = @{
        # Tag filters
        Tag = @()
        ExcludeTag = @('Slow', 'Integration', 'Manual')

        # Line filters
        Line = @()
        ExcludeLine = @()

        # Test name filters
        FullName = @()
    }

    # Should configuration
    Should = @{
        # Error action for failed assertions
        ErrorAction = 'Continue'
    }

    # Debug configuration
    Debug = @{
        # Show full error details
        ShowFullErrors = $true

        # Write debug messages
        WriteDebugMessages = $false

        # Write verbose messages
        WriteVerboseMessages = $false
    }

    # Plugin configuration
    Plugins = @(
        # Example plugin configuration
        # @{
        #     Name = 'CustomPlugin'
        #     Configuration = @{
        #         Setting1 = 'Value1'
        #         Setting2 = 'Value2'
        #     }
        # }
    )

    # User-defined data
    UserData = @{
        # Project-specific configuration
        ProjectName = 'Wiley Widget'
        ProjectVersion = '1.0.0'
        Environment = 'Development'

        # Test categories
        TestCategories = @{
            Unit = 'Fast unit tests'
            Integration = 'Integration tests'
            E2E = 'End-to-end tests'
            Performance = 'Performance tests'
        }

        # Test data paths
        TestData = @{
            SampleData = './TestData/SampleData.json'
            MockData = './TestData/MockData'
            Configuration = './TestData/Config'
        }
    }
}
