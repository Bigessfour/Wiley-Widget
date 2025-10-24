#Requires -Version 7.5
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#!
.SYNOPSIS
Short description of what this script does.

.DESCRIPTION
Longer description explaining the script's purpose and behavior.

.PARAMETER ExampleParam
Describe what this parameter does.

.EXAMPLE
./script.ps1 -ExampleParam Value
Demonstrates typical usage.

.NOTES
Author: Your Name
#>
[CmdletBinding()] param(
    [Parameter(Mandatory = $false)]
    [string]$ExampleParam
)

try {
    Write-Verbose 'Starting script'
    # Your script logic here
}
catch {
    Write-Error ("Unhandled error: {0}" -f $_.Exception.Message)
    throw
}
finally {
    Write-Verbose 'Script complete'
}
