# PSScriptAnalyzer Settings for Wiley Widget Project
# Following Microsoft PowerShell 7.5.2 best practices
# https://docs.microsoft.com/powershell/utility-modules/psscriptanalyzer/overview

@{
    # Severity levels to include in analysis
    Severity = @('Error', 'Warning', 'Information')

    # Include specific rules
    IncludeRules = @(
        # Basic PowerShell best practices
        'PSAvoidUsingCmdletAliases',
        'PSAvoidUsingWMICmdlet',
        'PSAvoidUsingPositionalParameters',
        'PSAvoidUsingInvokeExpression',
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingComputerNameHardcoded',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSAvoidUsingUserNameAndPasswordParams',
        'PSAvoidUsingClearTextPassword',

        # Variable and scoping rules
        'PSUseDeclaredVarsMoreThanAssignments',
        'PSUsePSCredentialType',
        'PSAvoidGlobalVars',
        'PSAvoidUsingUninitializedVariable',

        # Function and script structure
        'PSUseShouldProcessForStateChangingFunctions',
        'PSUseSingularNouns',
        'PSUseApprovedVerbs',
        'PSUseDeclaredVarsMoreThanAssignments',
        'PSAvoidDefaultValueSwitchParameter',
        'PSAvoidMultipleTypeAttributes',
        'PSAvoidUsingEmptyCatchBlock',

        # Code style and formatting
        'PSUseConsistentWhitespace',
        'PSUseConsistentIndentation',
        'PSAlignAssignmentStatement',
        'PSPlaceOpenBrace',
        'PSPlaceCloseBrace',
        'PSUseCorrectCasing',

        # Performance and efficiency
        'PSAvoidUsingWriteHost',
        'PSAvoidUsingInvokeExpression',
        'PSAvoidUsingPositionalParameters',

        # Security best practices
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSAvoidUsingUserNameAndPasswordParams',
        'PSAvoidUsingComputerNameHardcoded',
        'PSUsePSCredentialType'
    )

    # Exclude rules that may not be applicable
    ExcludeRules = @(
        # Allow Write-Host in setup and utility scripts
        'PSAvoidUsingWriteHost'
    )

    # Custom rule configurations
    Rules = @{

        # Consistent whitespace settings
        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckPipe = $true
            CheckSeparator = $true
            CheckParameter = $true
        }

        # Consistent indentation settings
        PSUseConsistentIndentation = @{
            Enable = $true
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            Kind = 'space'
        }

        # Alignment settings
        PSAlignAssignmentStatement = @{
            Enable = $true
            CheckHashtable = $true
        }

        # Brace placement settings
        PSPlaceOpenBrace = @{
            Enable = $true
            OnSameLine = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
        }

        PSPlaceCloseBrace = @{
            Enable = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
        }

        # Correct casing settings
        PSUseCorrectCasing = @{
            Enable = $true
        }

        # Approved verbs settings
        PSUseApprovedVerbs = @{
            Enable = $true
        }

        # Singular nouns settings
        PSUseSingularNouns = @{
            Enable = $true
        }

        # Should process settings
        PSUseShouldProcessForStateChangingFunctions = @{
            Enable = $true
            Verbose = $true
        }

        # Variable assignment settings
        PSUseDeclaredVarsMoreThanAssignments = @{
            Enable = $true
        }

        # Global variable settings
        PSAvoidGlobalVars = @{
            Enable = $true
        }

        # Uninitialized variable settings
        PSAvoidUsingUninitializedVariable = @{
            Enable = $true
        }
    }

    # Custom rule paths (if any custom rules are developed)
    # CustomRulePath = @(
    #     './PSScriptAnalyzerRules'
    # )

    # Settings for specific file types
    IncludeDefaultRules = $true

    # Recurse through subdirectories
    RecurseCustomRulePath = $true
}
