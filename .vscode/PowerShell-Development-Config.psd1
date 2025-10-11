# PowerShell Development Tools Configuration
# This file helps GitHub Copilot understand PowerShell 7.5.2 requirements

@{
    # PowerShell Version Requirements
    PowerShellVersion = @{
        Required = '7.5.2'
        Minimum = '7.5.0'
        Features = @(
            'Pipeline Parallelization',
            'Null-Coalescing Assignment',
            'Ternary Operator',
            'Using declarations',
            'Concise syntax for class properties'
        )
    }

    # Approved Verbs (Microsoft Standard)
    ApprovedVerbs = @(
        'Add', 'Clear', 'Close', 'Copy', 'Enter', 'Exit', 'Find', 'Format', 'Get', 'Hide',
        'Join', 'Lock', 'Move', 'New', 'Open', 'Optimize', 'Pop', 'Push', 'Redo', 'Remove',
        'Rename', 'Reset', 'Resize', 'Search', 'Select', 'Set', 'Show', 'Skip', 'Split',
        'Step', 'Switch', 'Undo', 'Unlock', 'Update', 'Use', 'Watch', 'Write'
    )

    # Best Practices Rules
    BestPractices = @{
        AvoidWriteHost = $true
        UseApprovedVerbs = $true
        UseVerboseOutput = $true
        UsePipelineFriendly = $true
        UseParameterValidation = $true
        UseCommentBasedHelp = $true
        UseConsistentNaming = $true
        UseErrorHandling = $true
    }

    # Code Style Guidelines
    CodeStyle = @{
        Indentation = '4 spaces'
        NamingConvention = 'camelCase'
        FunctionNaming = 'Verb-Noun'
        ParameterNaming = 'camelCase'
        VariableNaming = 'camelCase'
        CommentStyle = '# Comment'
        BraceStyle = 'OTBS (One True Brace Style)'
    }

    # Security Requirements
    Security = @{
        AvoidPlainTextPasswords = $true
        UseSecureString = $true
        ValidateInput = $true
        UseParameterSets = $true
        AvoidScriptInjection = $true
    }

    # Testing Requirements
    Testing = @{
        UsePester = $true
        WriteTests = $true
        TestFunctions = $true
        TestErrorHandling = $true
        UseTestDrive = $true
    }

    # Module Dependencies
    Dependencies = @(
        'PSScriptAnalyzer',
        'Pester',
        'platyPS',
        'PSReadLine'
    )

    # Development Tools
    Tools = @{
        PSScriptAnalyzer = @{
            Version = '1.21.0'
            Rules = @('PSAvoidUsingWriteHost', 'PSUseApprovedVerbs', 'PSUseCompatibleSyntax')
        }
        Pester = @{
            Version = '5.5.0'
            Framework = 'Pester 5'
        }
        VSCodeExtensions = @(
            'ms-vscode.powershell',
            'pspester.pester-test',
            'ms-dotnettools.dotnet-interactive-vscode'
        )
    }
}
