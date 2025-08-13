param(
  [switch]$Publish,
  [string]$Config = 'Release',
  [switch]$SelfContained,
  [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
Write-Host '== Restore =='
dotnet restore ./WileyWidget.sln
Write-Host "== Build ($Config) =="
dotnet build ./WileyWidget.sln -c $Config --no-restore
Write-Host '== Test =='
dotnet test ./WileyWidget.sln -c $Config --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
if ($Publish) {
  Write-Host '== Publish =='
  $out = Join-Path -Path (Resolve-Path .) -ChildPath 'publish'
  $sc = $SelfContained ? '/p:SelfContained=true' : '/p:SelfContained=false'
  $rid = $SelfContained ? "-r $Runtime" : ''
  dotnet publish ./WileyWidget/WileyWidget.csproj -c $Config -o $out $rid /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false $sc
  Write-Host "Published to $out"
  if ($SelfContained) { Write-Host "Self-contained runtime: $Runtime" }
}
Write-Host 'Done.'
