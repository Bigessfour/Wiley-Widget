@{
    Rules = @{
        # Allow scripts that intentionally skip ShouldProcess for state-changing functions
        PSUseShouldProcessForStateChangingFunctions = @{ Enable = $false }

        # Example: Allow some other rules to be relaxed for quicker prototyping
        # Add or tune rules as needed by CI and team policy
        # Enforce PSScriptAnalyzer default rules otherwise
    }
}
