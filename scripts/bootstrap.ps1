param([switch]$Sdk, [switch]$WarmCache)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
New-Item -ItemType Directory -Force '.tools' | Out-Null
$zip = Join-Path $taskRoot '.tools/tectonic.zip'
if (!(Test-Path $zip) -or (Get-FileHash $zip -Algorithm SHA256).Hash -ne 'F61CE51F0B0ADE1015B7DE7EF368541C5424E9756ECBD0D7AF97D6D48030845F') {
    Invoke-WebRequest 'https://github.com/tectonic-typesetting/tectonic/releases/download/tectonic%400.17.0/tectonic-0.17.0-x86_64-pc-windows-msvc.zip' -OutFile $zip
}
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne 'F61CE51F0B0ADE1015B7DE7EF368541C5424E9756ECBD0D7AF97D6D48030845F') { throw 'Tectonic checksum mismatch' }
Expand-Archive $zip '.tools/tectonic' -Force
if ($WarmCache) {
    $env:TECTONIC_CACHE_DIR = Join-Path $taskRoot '.tools/compiler-cache'
    $warmDirectory = Join-Path $taskRoot '.tools/warm'
    New-Item -ItemType Directory -Force $warmDirectory | Out-Null
    $fonts = [Security.SecurityElement]::Escape([Environment]::GetFolderPath('Fonts').Replace('\','/'))
    $fontConfig = Join-Path $warmDirectory 'fonts.conf'
    $fontCacheDirectory = Join-Path $taskRoot '.tools/font-cache'
    New-Item -ItemType Directory -Force $fontCacheDirectory | Out-Null
    $fontCache = [Security.SecurityElement]::Escape($fontCacheDirectory.Replace('\','/'))
    [IO.File]::WriteAllText($fontConfig, "<?xml version=`"1.0`"?><fontconfig><dir>$fonts</dir><cachedir>$fontCache</cachedir></fontconfig>")
    $env:FONTCONFIG_FILE = $fontConfig
    $templates = [IO.File]::ReadAllText((Join-Path $taskRoot 'src/EasyLatex/Templates.cs'))
    foreach ($match in [regex]::Matches($templates, '(?s)public const string (\w+) = """(.*?)""";')) {
        $file = Join-Path $warmDirectory ($match.Groups[1].Value + '.tex')
        [IO.File]::WriteAllText($file, $match.Groups[2].Value.Trim())
        & (Join-Path $taskRoot '.tools/tectonic/tectonic.exe') -X compile --untrusted --synctex --outdir $warmDirectory $file
        if ($LASTEXITCODE -ne 0) { throw "Template warmup failed: $file" }
        & (Join-Path $taskRoot '.tools/tectonic/tectonic.exe') -X compile --only-cached --untrusted --outdir $warmDirectory $file
        if ($LASTEXITCODE -ne 0) { throw "Template offline verification failed: $file" }
    }
    @{ engineVersion='0.17.0'; templatesHash=(Get-FileHash 'src/EasyLatex/Templates.cs' -Algorithm SHA256).Hash; verifiedUtc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content (Join-Path $env:TECTONIC_CACHE_DIR '.easylatex-ready.json') -Encoding utf8
}
if ($Sdk) {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $sdkFile = $metadata.releases[0].sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' }
    $sdkZip = Join-Path $taskRoot '.tools/dotnet-sdk.zip'
    Invoke-WebRequest $sdkFile.url -OutFile $sdkZip
    if ((Get-FileHash $sdkZip -Algorithm SHA512).Hash -ne $sdkFile.hash) { throw 'SDK checksum mismatch' }
    Expand-Archive $sdkZip '.tools/dotnet' -Force
}
