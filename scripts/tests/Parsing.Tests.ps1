#Requires -Version 7.5
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'PowerShell scripts parse' {
    It 'has no parser errors in scripts/' {
        $root = Join-Path $PSScriptRoot '..'
        $files = Get-ChildItem -Path (Join-Path $root 'scripts') -Recurse -Include *.ps1, *.psm1 -File -ErrorAction SilentlyContinue
        foreach ($f in $files) {
            $tokens = $null; $errors = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errors)
            $errors | Should -BeNullOrEmpty -Because "{0} should parse cleanly" -f $f.FullName
        }
    }
}
