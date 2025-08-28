param(
  [switch]$Publish,
  [string]$Config = 'Release',
  [switch]$SelfContained,
  [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
Write-Verbose '== Restore =='
dotnet restore ./WileyWidget.sln
Write-Verbose "== Build ($Config) =="
dotnet build ./WileyWidget.sln -c $Config --no-restore
Write-Verbose '== Test =='
dotnet test ./WileyWidget.sln -c $Config --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
if ($Publish) {
  Write-Verbose '== Publish =='
  $out = Join-Path -Path (Resolve-Path .) -ChildPath 'publish'
  $sc = $SelfContained ? '/p:SelfContained=true' : '/p:SelfContained=false'
  $rid = $SelfContained ? "-r $Runtime" : ''
  dotnet publish ./WileyWidget/WileyWidget.csproj -c $Config -o $out $rid /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false $sc
  Write-Verbose "Published to $out"
  if ($SelfContained) { Write-Verbose "Self-contained runtime: $Runtime" }
}
Write-Verbose 'Done.'
