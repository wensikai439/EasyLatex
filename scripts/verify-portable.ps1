param([string]$Output = 'artifacts/portable-default-verification', [string]$AppPath = 'artifacts/EasyLatex-win-x64/EasyLatex.exe')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
$outputDirectory = [IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
if (Test-Path (Join-Path $outputDirectory 'result.json')) { throw 'Choose a fresh output directory for first-start verification' }
$app = [IO.Path]::GetFullPath((Join-Path $taskRoot $AppPath))
$priorPath = $env:PATH
$priorRoot = $env:DOTNET_ROOT
$priorHttp = $env:HTTP_PROXY
$priorHttps = $env:HTTPS_PROXY
try {
    $env:PATH = "$env:SystemRoot\System32;$env:SystemRoot"
    $env:DOTNET_ROOT = ''
    # Prove the preseeded templates work with default cache settings and no usable network.
    $env:HTTP_PROXY = 'http://127.0.0.1:1'
    $env:HTTPS_PROXY = $env:HTTP_PROXY
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $app -ArgumentList '--verify-portable-default',('"' + $outputDirectory + '"') -WorkingDirectory (Split-Path $app -Parent) -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(120000)) { Stop-Process -Id $process.Id; throw 'Portable verification timed out' }
    $result = Get-Content (Join-Path $outputDirectory 'result.json') -Raw | ConvertFrom-Json
    $result | ConvertTo-Json -Depth 5
    Write-Output ('Total first-start verification time: {0:F1}s' -f $watch.Elapsed.TotalSeconds)
    if ($process.ExitCode -ne 0 -or !$result.success -or $result.templates.Count -ne 3 -or @($result.templates | Where-Object { !$_.success }).Count -ne 0) { throw 'Portable template verification failed' }
    if ($result.runtimeDirectory.TrimEnd('\','/') -ne (Split-Path $app -Parent).TrimEnd('\','/')) { throw 'Published app did not use its bundled runtime' }
} finally {
    $env:PATH = $priorPath
    $env:DOTNET_ROOT = $priorRoot
    $env:HTTP_PROXY = $priorHttp
    $env:HTTPS_PROXY = $priorHttps
}
