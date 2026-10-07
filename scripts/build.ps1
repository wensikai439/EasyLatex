param([switch]$Publish, [switch]$Verify)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
$sdk = if (Test-Path '.tools/dotnet/dotnet.exe') { Join-Path $taskRoot '.tools/dotnet/dotnet.exe' } else { 'dotnet' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $sdk build EasyLatex.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Verify) {
    & $sdk run --project tests/EasyLatex.Tests -c Release -- artifacts/verification --ui
    if ($LASTEXITCODE -ne 0) { throw 'Verification failed' }
}
if ($Publish) {
    $output = Join-Path $taskRoot 'artifacts/EasyLatex-win-x64'
    & $sdk publish src/EasyLatex -c Release -r win-x64 --self-contained true -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    if (!(Test-Path '.tools/tectonic/tectonic.exe')) { throw 'Run scripts/bootstrap.ps1 to fetch the bundled compiler' }
    New-Item -ItemType Directory -Force "$output/tools" | Out-Null
    Copy-Item '.tools/tectonic/tectonic.exe' "$output/tools/tectonic.exe"
    Copy-Item 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md' $output
    Compress-Archive -Path "$output/*" -DestinationPath 'artifacts/EasyLatex-win-x64.zip' -Force
    Get-FileHash 'artifacts/EasyLatex-win-x64.zip' -Algorithm SHA256 | Format-List
}
