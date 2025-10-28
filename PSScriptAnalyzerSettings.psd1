@{
    # PSScriptAnalyzer settings for WileyWidget repository
    # Adjust IncludeRules/ExcludeRules to taste. Severity can be 'Error', 'Warning', or 'Information'.

    Severity = @{
        'PSAvoidUsingCmdletAliases' = 'Warning'
        'PSAvoidUsingPlainTextForPassword' = 'Error'
        'PSUseShouldProcessForStateChangingFunctions' = 'Warning'
        'PSAvoidUsingWriteHost' = 'Warning'
        'PSAvoidGlobalVars' = 'Warning'
        'PSUseCompatibleCmdlets' = 'Warning'
    }

    IncludeRules = @(
        'PSAvoidUsingCmdletAliases',
        'PSAvoidUsingPlainTextForPassword',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSAvoidUsingWriteHost',
        'PSAvoidGlobalVars',
        'PSUseCompatibleCmdlets'
    )

    # Keep some leniency for hobby scripts
    ExcludeRules = @(
        'PSUseDeclaredVarsMoreThanAssignments'
    )

    # Optional: common parameters for the analyzer
    Settings = @{
        Recurse = $true
        Severity = @('Error','Warning')
    }
}