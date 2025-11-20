# registers the local Syncfusion offline NuGet source if not already present
$sourceName = 'Syncfusion Local WinUI'
$sourcePath = 'C:\Program Files (x86)\Syncfusion\Essential Studio\WinUI\31.2.2\NuGetPackages'

# Check existing sources
$sources = dotnet nuget list source --format short | Out-String
if ($sources -match [regex]::Escape($sourcePath)) {
    Write-Host "Syncfusion local source already registered"
    exit 0
}

Write-Host "Registering Syncfusion local NuGet source: $sourcePath"
dotnet nuget add source "$sourcePath" --name "$sourceName" --store-password-in-clear-text | Out-Null
Write-Host "Done. Verify in Visual Studio under Tools -> NuGet Package Manager -> Package Sources."
