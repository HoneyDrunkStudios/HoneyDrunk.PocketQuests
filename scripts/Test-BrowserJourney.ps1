#Requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Release', 'Debug')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$mobile = Join-Path $root 'apps/mobile'
$assembly = Join-Path $root "HoneyDrunk.PocketQuests/PocketQuests.BrowserHost/bin/$Configuration/net10.0/PocketQuests.BrowserHost.dll"
if (!(Test-Path -LiteralPath $assembly)) { throw 'Build PocketQuests.BrowserHost before running this harness.' }
foreach ($port in @(5217, 5218, 5219)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use. This harness never terminates an existing service."
    }
}
# The caller owns Expo. Keep it on the reviewed tree with CI=1, without hot reload.
$null = Invoke-WebRequest 'http://localhost:8081' -TimeoutSec 20
$directory = Join-Path $root ('.local/browser-' + [Guid]::NewGuid().ToString('N'))
$artifacts = Join-Path $root 'artifacts/browser-journey'
New-Item -ItemType Directory -Force -Path $directory, $artifacts | Out-Null
$ready = Join-Path $directory 'ready.json'
$done = Join-Path $directory 'done'
$oldFixture = $env:POCKETQUESTS_FIXTURE_DIRECTORY
$oldArtifacts = $env:POCKETQUESTS_ARTIFACT_DIRECTORY
$fixture = $null
$journey = $null
$failure = $null
try {
    # These arguments are paths, passed directly to dotnet without a command shell.
    $fixtureArguments = @(('"{0}"' -f $assembly), ('"{0}"' -f $directory))
    $fixture = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $fixtureArguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifacts 'fixture.stdout.log') -RedirectStandardError (Join-Path $artifacts 'fixture.stderr.log')
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while (!(Test-Path -LiteralPath $ready)) {
        if ($fixture.HasExited) { throw 'Isolated fixture exited before readiness. Inspect fixture logs.' }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Isolated fixture startup exceeded 120 seconds.' }
        Start-Sleep -Milliseconds 250
    }
    # Never print/upload ready.json: it contains the short-lived fixture token.
    $env:POCKETQUESTS_FIXTURE_DIRECTORY = $directory
    $env:POCKETQUESTS_ARTIFACT_DIRECTORY = $artifacts
    $journey = Start-Process -FilePath (Get-Command node).Source -ArgumentList 'e2e/journey.mjs' -WorkingDirectory $mobile -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifacts 'journey.stdout.log') -RedirectStandardError (Join-Path $artifacts 'journey.stderr.log')
    $deadline = [DateTime]::UtcNow.AddMinutes(6)
    while (!$journey.HasExited) {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Browser journey exceeded six minutes.' }
        Start-Sleep -Milliseconds 250
    }
    $journey.WaitForExit()
    if ($journey.ExitCode -ne 0) { throw "Browser journey failed with exit code $($journey.ExitCode). Inspect artifacts/browser-journey." }
}
catch { $failure = $_ }
finally {
    if ($journey -and !$journey.HasExited) { $journey.Kill($true); $journey.WaitForExit() }
    Set-Content -LiteralPath $done -Value 'done'
    if ($fixture) {
        if (!$fixture.WaitForExit(30000)) {
            $fixture.Kill($true)
            $fixture.WaitForExit()
            $failure = 'Fixture cleanup exceeded 30 seconds; isolated database cleanup is not confirmed.'
        }
        elseif ($fixture.ExitCode -ne 0) { $failure = 'Fixture exited unsuccessfully; inspect cleanup logs.' }
    }
    if (Test-Path -LiteralPath $ready) {
        Remove-Item -LiteralPath $ready -Force
        $failure = 'Fixture token cleanup did not finish normally; inspect cleanup logs.'
    }
    $env:POCKETQUESTS_FIXTURE_DIRECTORY = $oldFixture
    $env:POCKETQUESTS_ARTIFACT_DIRECTORY = $oldArtifacts
}
if ($failure) { throw $failure }
Get-Content -LiteralPath (Join-Path $artifacts 'journey.stdout.log')
Write-Host 'Browser journey and isolated fixture cleanup passed.'
