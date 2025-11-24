param(
    [string]$Project = "src\WileyWidget.WinUI\WileyWidget.WinUI.csproj",
    [int]$TimeoutSeconds = 300
)

Write-Host "Running CompileXaml target for project: $Project"
$msbuild = "dotnet"
$arguments = "msbuild `"$Project`" /t:CompileXaml /v:m"

Write-Host "Executing: $msbuild $arguments"
$proc = Start-Process -FilePath $msbuild -ArgumentList $arguments -NoNewWindow -PassThru -Wait -ErrorAction SilentlyContinue
if ($proc.ExitCode -ne 0) {
    Write-Host "CompileXaml failed with exit code $($proc.ExitCode)" -ForegroundColor Red
    exit $proc.ExitCode
}

Write-Host "CompileXaml completed successfully" -ForegroundColor Green
exit 0
