param([switch]$Publish, [switch]$Verify)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
$sdk = if (Test-Path '.tools/dotnet/dotnet.exe') { Join-Path $taskRoot '.tools/dotnet/dotnet.exe' } else { 'dotnet' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$buildTarget = if ($Publish -and !$Verify) { 'src/EasyLatex/EasyLatex.csproj' } else { 'EasyLatex.sln' }
if (Test-Path '.tools/dotnet/dotnet.exe') { $env:DOTNET_ROOT = Join-Path $taskRoot '.tools/dotnet' }
& $sdk build $buildTarget -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Verify) {
    & $sdk run --project tests/EasyLatex.Tests -c Release -- artifacts/verification --ui
    if ($LASTEXITCODE -ne 0) { throw 'Verification failed' }
}
if ($Publish) {
    $output = Join-Path $taskRoot 'artifacts/EasyLatex-win-x64'
    & $sdk publish src/EasyLatex -c Release -r win-x64 --self-contained true -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    foreach ($debugFile in Get-ChildItem $output -Filter '*.pdb' -File) { Remove-Item -LiteralPath $debugFile.FullName }
    if (!(Test-Path '.tools/tectonic/tectonic.exe')) { throw 'Run scripts/bootstrap.ps1 to fetch the bundled compiler' }
    New-Item -ItemType Directory -Force "$output/tools" | Out-Null
    Copy-Item '.tools/tectonic/tectonic.exe' "$output/tools/tectonic.exe"
    $cacheReady = $false
    $manifestPath = '.tools/compiler-cache/.easylatex-ready.json'
    if (Test-Path $manifestPath) {
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $cacheReady = $manifest.engineVersion -eq '0.17.0' -and $manifest.templatesHash -eq (Get-FileHash 'src/EasyLatex/Templates.cs' -Algorithm SHA256).Hash
    }
    if (!$cacheReady) { & "$PSScriptRoot/bootstrap.ps1" -WarmCache }
    Copy-Item '.tools/compiler-cache' "$output/tools" -Recurse -Force
    Copy-Item 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md' $output
    New-Item -ItemType Directory -Force "$output/licenses" | Out-Null
    Copy-Item 'licenses/*.txt' "$output/licenses"
    $sdkDirectory = Split-Path (Get-Command $sdk).Source -Parent
    foreach ($notice in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
        if (Test-Path (Join-Path $sdkDirectory $notice)) { Copy-Item (Join-Path $sdkDirectory $notice) "$output/licenses/Dotnet-$notice" }
    }
    Copy-Item 'examples' $output -Recurse -Force
    Copy-Item 'docs' $output -Recurse -Force
    Compress-Archive -Path "$output/*" -DestinationPath 'artifacts/EasyLatex-win-x64.zip' -Force
    $hash = Get-FileHash 'artifacts/EasyLatex-win-x64.zip' -Algorithm SHA256
    [IO.File]::WriteAllText((Join-Path $taskRoot 'artifacts/SHA256SUMS.txt'), $hash.Hash.ToLowerInvariant() + '  EasyLatex-win-x64.zip' + [Environment]::NewLine)
    $hash | Format-List
}
