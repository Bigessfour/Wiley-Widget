@{
    # PSScriptAnalyzer settings for WileyWidget repository
    # Adjust IncludeRules/ExcludeRules to taste. Severity can be 'Error', 'Warning', or 'Information'.

    Severity     = @('Error', 'Warning')

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
}
