#Requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Release', 'Debug')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
# Check every native exit explicitly, including cleanup after a failed journey.
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts/browser-journey'
if ((Test-Path $artifacts) -and (Get-ChildItem $artifacts -Force)) {
    throw 'Browser evidence directory must be empty; refusing stale evidence.'
}
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$private = Join-Path $root ('.local/browser-ci-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $private | Out-Null
$instance = 'PQSchema_' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$previousInstance = $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE
$previousDiagnostics = $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS
$diagnosticsPath = Join-Path $private 'journey-diagnostics.json'
$previousCI = $env:CI
$previousAppEnv = $env:EXPO_PUBLIC_APP_ENV
$expo = $null
$journey = $null
$creationAttempted = $false
$failure = $null
$summary = [ordered]@{
    schemaVersion = 2; stage = 'browser-adapter-real-api-sql'; result = 'not-run'
    testedHead = $null; expectedHead = $env:PQ_EXPECTED_HEAD; baseSha = $env:PQ_BASE_SHA
    repository = $env:GITHUB_REPOSITORY; runId = $env:GITHUB_RUN_ID; runAttempt = $env:GITHUB_RUN_ATTEMPT
    eventName = $env:GITHUB_EVENT_NAME; identityRevision = '5cfc1da16f075b566e845df8ba81de5f0e4347a8'
    startedAt = [DateTimeOffset]::UtcNow.ToString('o'); journeyHarnessExitCode = $null
    databaseCleanupVerified = $false; instanceDeleted = $false; ownedParentProcessesStopped = $false
    journeyCheckpoint = 'unknown'; journeyErrorCategory = 'unknown'
    fixtureLifecycleConfirmed = $false; nativeDeviceResult = 'not-run'; liveProviderResult = 'not-run'
}
try {
    $head = & git -C $root rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-f]{40}$') { throw 'Cannot identify tested commit.' }
    $summary.testedHead = $head
    if ($env:PQ_EXPECTED_HEAD -notmatch '^[0-9a-f]{40}$' -or $head -ne $env:PQ_EXPECTED_HEAD) {
        throw 'Checkout does not match the expected candidate.'
    }
    foreach ($port in @(8081, 5217, 5218, 5219)) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
            throw "Port $port is occupied; no existing listener will be stopped."
        }
    }
    $instances = @(& sqllocaldb info)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate LocalDB instances.' }
    if ($instances -contains $instance) { throw 'Refusing to reuse an existing LocalDB instance.' }
    $creationAttempted = $true
    & sqllocaldb create $instance -s *> (Join-Path $private 'instance-create.log')
    if ($LASTEXITCODE -ne 0) { throw 'Disposable LocalDB creation failed.' }
    $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE = $instance
    $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS = $diagnosticsPath
    $env:CI = '1'
    $env:EXPO_PUBLIC_APP_ENV = 'development'
    $expo = Start-Process -FilePath (Get-Command node).Source -ArgumentList 'node_modules/expo/bin/cli start --web --localhost --port 8081' -WorkingDirectory (Join-Path $root 'apps/mobile') -PassThru -RedirectStandardOutput (Join-Path $private 'expo.stdout.log') -RedirectStandardError (Join-Path $private 'expo.stderr.log')
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while ($true) {
        if ($expo.HasExited) { throw 'Owned Expo process exited before readiness.' }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Expo startup exceeded 120 seconds.' }
        try { $null = Invoke-WebRequest 'http://localhost:8081' -TimeoutSec 5; break }
        catch { Start-Sleep -Milliseconds 250 }
    }
    $journey = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoProfile', '-File', ('"{0}"' -f (Join-Path $PSScriptRoot 'Test-BrowserJourney.ps1')), '-Configuration', $Configuration) -WorkingDirectory $root -PassThru -RedirectStandardOutput (Join-Path $private 'harness.stdout.log') -RedirectStandardError (Join-Path $private 'harness.stderr.log')
    # Existing harness owns 120s fixture startup, 6m journey and 30s cleanup.
    if (-not $journey.WaitForExit(600000)) { throw 'Browser harness exceeded ten minutes.' }
    $summary.journeyHarnessExitCode = $journey.ExitCode
    if ($journey.ExitCode -ne 0) { throw 'Browser harness failed.' }
    if ($expo.HasExited) { throw 'Owned Expo process exited during the journey.' }
}
catch { $failure = $_ }
finally {
    # Stop only processes this invocation created. A process cleanup failure must
    # not prevent attempts to verify/remove the uniquely owned SQL instance.
    $processCleanup = $true
    foreach ($process in @($journey, $expo)) {
        try {
            if ($process -and -not $process.HasExited) {
                $process.Kill($true)
                if (-not $process.WaitForExit(30000)) { throw 'Owned process did not stop.' }
            }
        } catch { $processCleanup = $false; $failure = $_ }
    }
    $summary.ownedParentProcessesStopped = $processCleanup
    # An abnormal harness exit cannot certify descendant cleanup. The normal
    # harness path waits for browser.close() and the fixture's successful exit.
    $summary.fixtureLifecycleConfirmed = $null -ne $summary.journeyHarnessExitCode -and $summary.journeyHarnessExitCode -eq 0
    if ($creationAttempted) {
        try {
            & sqlcmd -S "(localdb)\$instance" -E -I -b -l 15 -t 30 -d master -Q "SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM sys.databases WHERE database_id > 4) THROW 51010,'Fixture databases remain.',1;" *> (Join-Path $private 'database-cleanup.log')
            if ($LASTEXITCODE -ne 0) { throw 'Fixture database cleanup is not confirmed.' }
            $summary.databaseCleanupVerified = $true
        } catch { $failure = $_ }
        try {
            & sqllocaldb stop $instance -k *> (Join-Path $private 'instance-stop.log')
            # Still attempt delete if stop failed (including partial creation).
            & sqllocaldb delete $instance *> (Join-Path $private 'instance-delete.log')
            if ($LASTEXITCODE -ne 0) { throw 'Disposable instance deletion failed.' }
            $remaining = @(& sqllocaldb info)
            if ($LASTEXITCODE -ne 0 -or $remaining -contains $instance) { throw 'Disposable instance deletion is not confirmed.' }
            $summary.instanceDeleted = $true
        } catch { $failure = $_ }
    }
    # Child output is untrusted: bounded JSON, exact keys and case-sensitive enums.
    # Reconstruct only these two values; never copy arbitrary fields into run.json.
    try {
        $file = Get-Item -LiteralPath $diagnosticsPath -ErrorAction Stop
        if ($file.Length -gt 4096) { throw 'Oversized diagnostic record.' }
        $diagnostics = Get-Content -LiteralPath $diagnosticsPath -Raw | ConvertFrom-Json -AsHashtable -NoEnumerate
        $checkpoints = @('setup', 'sign-in', 'onboarding', 'onboarding-save', 'custom-quest', 'save-retry', 'definition-edit', 'completion-response-loss', 'character', 'export-timer', 'offline-undo', 'clock-undo-retry', 'fresh-session', 'calendar-planning', 'plan-edit', 'viewports', 'page-errors', 'complete')
        $categories = @('none', 'timeout', 'assertion', 'unknown')
        if ($diagnostics -isnot [System.Collections.IDictionary] -or $diagnostics.Count -ne 2 -or
            @($diagnostics.Keys | Where-Object { $_ -cnotin @('checkpoint', 'errorCategory') }).Count -ne 0 -or
            $diagnostics.checkpoint -isnot [string] -or $checkpoints -cnotcontains $diagnostics.checkpoint -or
            $diagnostics.errorCategory -isnot [string] -or $categories -cnotcontains $diagnostics.errorCategory) {
            throw 'Unrecognized diagnostic record.'
        }
        $summary.journeyCheckpoint = $diagnostics.checkpoint
        $summary.journeyErrorCategory = $diagnostics.errorCategory
        if ($summary.journeyErrorCategory -eq 'none' -and $summary.journeyHarnessExitCode -ne 0) {
            $summary.journeyErrorCategory = 'unknown'
        }
    } catch { # Missing/invalid diagnostics do not replace the actual failure or skip cleanup.
        $summary.journeyCheckpoint = 'unknown'
        $summary.journeyErrorCategory = 'unknown'
    }
    $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS = $previousDiagnostics
    $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE = $previousInstance
    $env:CI = $previousCI
    $env:EXPO_PUBLIC_APP_ENV = $previousAppEnv
    $summary.result = if (-not $failure -and $summary.journeyHarnessExitCode -eq 0 -and $summary.databaseCleanupVerified -and $summary.instanceDeleted -and $summary.ownedParentProcessesStopped -and $summary.fixtureLifecycleConfirmed) { 'passed' } else { 'failed' }
    $summary.finishedAt = [DateTimeOffset]::UtcNow.ToString('o')
    # Metadata only: no environment dump, ready.json, tokens or private logs.
    $summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'run.json') -Encoding utf8
}
if ($failure) { throw $failure }
if ($summary.result -ne 'passed') { throw 'Browser evidence is incomplete.' }
Write-Host 'Actual browser/API/SQL journey and owned-resource cleanup passed.'
