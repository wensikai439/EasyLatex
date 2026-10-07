param([Parameter(Mandatory=$true)][string]$AppPath, [Parameter(Mandatory=$true)][string]$Output, [int]$Runs = 5)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$app = [IO.Path]::GetFullPath((Join-Path $taskRoot $AppPath))
$destination = [IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
if (Test-Path -LiteralPath $destination) { throw 'Use a fresh output directory' }
New-Item -ItemType Directory -Path $destination | Out-Null
$measurements = @()
for ($run = 1; $run -le $Runs; $run++) {
    $runDirectory = Join-Path $destination ('run-' + $run)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $app -ArgumentList '--profile-startup', ('"' + $runDirectory + '"') -WorkingDirectory (Split-Path $app -Parent) -WindowStyle Hidden -PassThru
    $ready = Join-Path $runDirectory 'ready.json'
    while (!(Test-Path -LiteralPath $ready)) {
        if ($process.HasExited) { throw 'App exited before the UI was ready' }
        if ($watch.Elapsed.TotalSeconds -gt 30) { Stop-Process -Id $process.Id; throw 'Startup timed out' }
        Start-Sleep -Milliseconds 20
    }
    $readyMs = $watch.Elapsed.TotalMilliseconds
    if (!$process.WaitForExit(10000)) { Stop-Process -Id $process.Id; throw 'Profile did not exit' }
    $profile = Get-Content (Join-Path $runDirectory 'profile.json') -Raw | ConvertFrom-Json
    $measurements += [pscustomobject]@{ run=$run; launcherToReadyMs=$readyMs; workingSetMiB=$profile.workingSetMiB; privateMiB=$profile.privateMiB; managedMiB=$profile.managedMiB }
}
$result = [pscustomobject]@{ app=$app; exeMiB=(Get-Item -LiteralPath $app).Length/1MB; pollResolutionMs=20; runs=$measurements; timestamp=[DateTimeOffset]::UtcNow }
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'startup.json') -Encoding utf8
$measurements | Format-Table
