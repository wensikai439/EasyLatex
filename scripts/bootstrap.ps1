param([switch]$Sdk)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
New-Item -ItemType Directory -Force '.tools' | Out-Null
$zip = Join-Path $taskRoot '.tools/tectonic.zip'
Invoke-WebRequest 'https://github.com/tectonic-typesetting/tectonic/releases/download/tectonic%400.17.0/tectonic-0.17.0-x86_64-pc-windows-msvc.zip' -OutFile $zip
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne 'F61CE51F0B0ADE1015B7DE7EF368541C5424E9756ECBD0D7AF97D6D48030845F') { throw 'Tectonic checksum mismatch' }
Expand-Archive $zip '.tools/tectonic' -Force
if ($Sdk) {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $sdkFile = $metadata.releases[0].sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' }
    $sdkZip = Join-Path $taskRoot '.tools/dotnet-sdk.zip'
    Invoke-WebRequest $sdkFile.url -OutFile $sdkZip
    if ((Get-FileHash $sdkZip -Algorithm SHA512).Hash -ne $sdkFile.hash) { throw 'SDK checksum mismatch' }
    Expand-Archive $sdkZip '.tools/dotnet' -Force
}
