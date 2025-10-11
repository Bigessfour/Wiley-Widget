# PSScriptAnalyzer Settings for PowerShell 7.5.2 Compliance (Streamlined for Performance)
# https://github.com/PowerShell/PSScriptAnalyzer

@{
    # Include only essential rules for performance
    IncludeRules = @(
        'PSAvoidUsingWriteHost',           # STRICT: No Write-Host allowed
        'PSUseApprovedVerbs',             # Enforce approved verbs
        'PSAvoidUsingPlainTextForPassword' # Security best practice
    )

    # Exclude performance-impacting rules
    ExcludeRules = @(
        'PSAvoidUsingCmdletAliases',
        'PSAvoidUsingWMICmdlet',
        'PSAvoidUsingPositionalParameters',
        'PSAvoidUsingInvokeExpression',
        'PSAvoidUsingComputerNameHardcoded',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSAvoidUsingEmptyCatchBlock',
        'PSAvoidShouldContinueWithoutForce',
        'PSUseSingularNouns',
        'PSAvoidGlobalVars',
        'PSAvoidUsingUsernameAndPasswordParams',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSUseUsingScopeModifierInNewRunspaces'
    )

    # Minimal compatibility checking
    Rules = @{
        PSUseCompatibleSyntax = @{
            Enable = $false  # Disabled for performance
        }
        PSUseCompatibleCmdlets = @{
            Enable = $false  # Disabled for performance
        }
        PSUseCompatibleTypes = @{
            Enable = $false  # Disabled for performance
        }
        PSAvoidUsingWriteHost = @{
            Enable = $true
        }
    }
}
