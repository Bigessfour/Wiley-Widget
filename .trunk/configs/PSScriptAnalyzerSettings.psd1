# PSScriptAnalyzer Configuration for Trunk CI/CD
# Optimized for PowerShell 7.5.2 formatting and best practices

@{
    # PowerShell 7.5.2 Compatibility Settings
    IncludeRules = @(
        # Core formatting and style rules
        'PSPlaceOpenBrace',
        'PSPlaceCloseBrace',
        'PSUseConsistentIndentation',
        'PSUseConsistentWhitespace',
        'PSAlignAssignmentStatement',
        'PSUseCorrectCasing',

        # PowerShell 7.5.2 specific rules
        'PSUseCompatibleSyntax',
        'PSUseCompatibleCmdlets',
        'PSUseCompatibleTypes',

        # Security and best practices
        'PSAvoidUsingWriteHost',
        'PSAvoidUsingWMICmdlet',
        'PSAvoidUsingPositionalParameters',
        'PSAvoidUsingInvokeExpression',
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingComputerNameHardcoded',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSAvoidUsingEmptyCatchBlock',
        'PSAvoidShouldContinueWithoutForce',
        'PSUseApprovedVerbs',
        'PSUseSingularNouns',
        'PSAvoidGlobalVars',
        'PSAvoidUsingUsernameAndPasswordParams',

        # Code quality
        'PSAvoidUsingCmdletAliases',
        'PSUseLiteralInitializerForHashtable',
        'PSUsePSCredentialType',
        'PSUseOutputTypeCorrectly',
        'PSAvoidDefaultValueSwitchParameter',
        'PSAvoidMultipleTypeAttributes',
        'PSUseProcessBlockForPipelineCommand',
        'PSUseSupportsShouldProcess',
        'PSUseShouldProcessForStateChangingFunctions'
    )

    # Exclude rules that conflict with PowerShell 7.5.2 development
    ExcludeRules = @(
        'PSUseShouldProcessForStateChangingFunctions',  # May not apply to all functions
        'PSUseUsingScopeModifierInNewRunspaces',       # False positives for parameter variables
        'PSAvoidUsingEmptyCatchBlock'                   # Sometimes necessary for specific error handling
    )

    # Detailed rule configurations for PowerShell 7.5.2
    Rules = @{
        # Formatting Rules - Strict enforcement
        PSPlaceOpenBrace = @{
            Enable = $true
            OnSameLine = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $false
        }

        PSPlaceCloseBrace = @{
            Enable = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $false
        }

        PSUseConsistentIndentation = @{
            Enable = $true
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            Kind = 'space'
        }

        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckPipe = $true
            CheckSeparator = $true
        }

        PSAlignAssignmentStatement = @{
            Enable = $true
            CheckHashtable = $true
        }

        PSUseCorrectCasing = @{
            Enable = $true
        }

        # PowerShell 7.5.2 Compatibility
        PSUseCompatibleSyntax = @{
            Enable = $true
            TargetPowerShellVersion = '7.5'
        }

        PSUseCompatibleCmdlets = @{
            Enable = $true
            TargetPowerShellVersion = '7.5'
        }

        PSUseCompatibleTypes = @{
            Enable = $true
            TargetPowerShellVersion = '7.5'
        }

        # Security Rules - Maximum enforcement
        PSAvoidUsingWriteHost = @{
            Enable = $true
            CommandName = @('Write-Host')
        }

        PSAvoidUsingWMICmdlet = @{
            Enable = $true
        }

        PSAvoidUsingPositionalParameters = @{
            Enable = $true
            CommandAllowList = @('Write-Output', 'Write-Verbose', 'Write-Warning', 'Write-Error')
        }

        PSAvoidUsingInvokeExpression = @{
            Enable = $true
        }

        PSAvoidUsingPlainTextForPassword = @{
            Enable = $true
        }

        PSAvoidUsingComputerNameHardcoded = @{
            Enable = $true
        }

        # Code Quality Rules
        PSUseApprovedVerbs = @{
            Enable = $true
        }

        PSUseSingularNouns = @{
            Enable = $true
        }

        PSAvoidGlobalVars = @{
            Enable = $true
        }

        PSAvoidUsingCmdletAliases = @{
            Enable = $true
        }

        PSUseLiteralInitializerForHashtable = @{
            Enable = $true
        }
    }

    # Severity levels for different rule types
    Severity = @{
        'PSPlaceOpenBrace' = 'Error'
        'PSPlaceCloseBrace' = 'Error'
        'PSUseConsistentIndentation' = 'Error'
        'PSUseConsistentWhitespace' = 'Error'
        'PSAlignAssignmentStatement' = 'Warning'
        'PSAvoidUsingWriteHost' = 'Error'
        'PSUseCompatibleSyntax' = 'Error'
        'PSUseCompatibleCmdlets' = 'Error'
        'PSUseCompatibleTypes' = 'Error'
        'PSUseApprovedVerbs' = 'Warning'
        'PSAvoidUsingCmdletAliases' = 'Warning'
    }
}
