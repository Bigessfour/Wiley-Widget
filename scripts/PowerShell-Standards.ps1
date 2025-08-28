# PowerShell Coding Standards for WileyWidget Project
# Add this to your PowerShell profile or source it in scripts

# Preferred error handling approach
$ErrorActionPreference = 'Stop'

# Function template with proper parameter handling
function Template-Function {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$RequiredParam,
        
        [Parameter()]
        [string]$OptionalParam = "DefaultValue"
    )
    
    begin {
        Write-Verbose "Starting $($MyInvocation.MyCommand.Name)"
    }
    
    process {
        try {
            if ($PSCmdlet.ShouldProcess($RequiredParam, "Perform Action")) {
                # Main logic here
                Write-Output "Result"
            }
        }
        catch {
            Write-Error "Failed to process $RequiredParam`: $_"
            throw
        }
    }
    
    end {
        Write-Verbose "Completed $($MyInvocation.MyCommand.Name)"
    }
}

# Export common patterns for Copilot to learn from
Export-ModuleMember -Function Template-Function
