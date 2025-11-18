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
        'PSAvoidUsingWriteHost',            # Write-Host is acceptable for user-interactive scripts with colored output
        'PSUseApprovedVerbs',               # Allow descriptive function names like Discover-*, Run-*
        'PSUseSingularNouns',               # Allow plural nouns when semantically appropriate (e.g., Discover-CSXTests)
        'PSReviewUnusedParameter',          # Script parameters may be used conditionally
        'PSAvoidUsingEmptyCatchBlock',      # Empty catch blocks acceptable when intentionally silencing errors
        'PSAvoidTrailingWhitespace'         # Formatting handled by editor/formatter
    )
}
