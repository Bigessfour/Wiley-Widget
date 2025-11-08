@{
    # PSScriptAnalyzer settings for WileyWidget repository
    # Adjust IncludeRules/ExcludeRules to taste. Severity can be 'Error', 'Warning', or 'Information'.

    Severity     = @('Error', 'Warning')

    IncludeRules = @(
        'PSAvoidUsingCmdletAliases',
        'PSAvoidUsingPlainTextForPassword',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSAvoidGlobalVars',
        'PSUseCompatibleCmdlets'
    )

    # Exclude rules that are not applicable or too noisy
    ExcludeRules = @(
        'PSUseDeclaredVarsMoreThanAssignments',
        'PSAvoidUsingWriteHost'  # Write-Host is acceptable for user-interactive scripts with colored output
    )
}
