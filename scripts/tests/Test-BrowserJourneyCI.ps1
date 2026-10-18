# Portable orchestration contract tests. No SQL, Expo, browser or network calls.
$ErrorActionPreference = 'Stop'
# Keep uploads narrower than the ignored fixture directory: literal paths only.
$workflow = Get-Content (Join-Path $PSScriptRoot '../../.github/workflows/browser-journey.yml') -Raw
$uploadBlock = [regex]::Match($workflow, '(?m)^          path: \|\r?\n(?<paths>(?: {12}[^\r\n]+(?:\r?\n|$))+)')
if (!$uploadBlock.Success) { throw 'Missing explicit browser upload block.' }
$uploaded = @($uploadBlock.Groups['paths'].Value.Trim() -split '\r?\n' | ForEach-Object { $_.Trim() })
$allowed = @('run.json', 'completion-celebration.png', 'character-home.png', 'available-quests.png', 'available-quest-details.png', 'active-quests.png', 'active-quest-details.png', 'completed-quests.png', 'home-320.png', 'home-430.png', 'browser-failure.png')
$allowed = @($allowed | ForEach-Object { 'product/artifacts/browser-journey/' + $_ })
if (Compare-Object $allowed $uploaded) { throw 'Browser artifact allowlist changed; explicitly review its data boundary.' }
Write-Output 'Explicit browser artifact allowlist passed.'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('pq-browser-contract-' + [guid]::NewGuid().ToString('N'))
$state = @{}
$head = 'a' * 40
$originalExpected = $env:PQ_EXPECTED_HEAD
$originalInstance = $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE
$originalDiagnostics = $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS
$originalCI = $env:CI
$originalAppEnv = $env:EXPO_PUBLIC_APP_ENV
function git { $global:LASTEXITCODE = 0; return $head }
function Get-NetTCPConnection { if ($state.case -eq 'occupied') { 'existing-listener' } }
function Get-Command { param($Name) return @{ Source = $Name } }
function sqllocaldb {
    $global:LASTEXITCODE = 0
    switch ($args[0]) {
        'info' { if ($state.exists) { $state.instance } }
        'create' {
            $state.creates++
            $state.instance = $args[1]
            $state.exists = $true
            if ($state.case -eq 'partial-create') { $global:LASTEXITCODE = 1 }
        }
        'stop' { $state.stops++; if ($state.case -eq 'stop-failed') { $global:LASTEXITCODE = 1 } }
        'delete' {
            if ($args[1] -ne $state.instance) { throw 'Attempted deletion of an unowned instance.' }
            $state.deletes++
            if ($state.case -eq 'delete-failed') { $global:LASTEXITCODE = 1 }
            elseif ($state.case -ne 'delete-unconfirmed') { $state.exists = $false }
        }
        default { throw 'Unexpected LocalDB command.' }
    }
}
function sqlcmd {
    if ($args -notcontains "(localdb)\$($state.instance)") { throw 'SQL cleanup targeted an unowned instance.' }
    $state.databaseChecks++
    $global:LASTEXITCODE = [int]($state.case -eq 'database-remains')
}
function Invoke-WebRequest { return @{ StatusCode = 200 } }
function Start-Process {
    param($FilePath, $ArgumentList, $WorkingDirectory, [switch]$PassThru, $RedirectStandardOutput, $RedirectStandardError)
    if ($env:CI -ne '1' -or $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE -ne $state.instance -or $env:EXPO_PUBLIC_APP_ENV -ne 'development') { throw 'Fixture environment was not set.' }
    $state.starts++
    $kind = if ($FilePath -eq 'node') { 'expo' } else { 'journey' }
    if ($state.case -eq 'start-failed' -and $kind -eq 'journey') { throw 'Mock journey spawn failure.' }
    if ($kind -eq 'journey') {
        $record = switch -Wildcard ($state.case) {
            'diagnostics-extra*' { '{"checkpoint":"onboarding-save","errorCategory":"timeout","headers":"Bearer secret"}' }
            'diagnostics-enum' { '{"checkpoint":"https://private/?token=secret","errorCategory":"Bearer secret"}' }
            'diagnostics-type' { '{"checkpoint":["onboarding"],"errorCategory":{"token":"secret"}}' }
            'diagnostics-case' { '{"checkpoint":"ONBOARDING","errorCategory":"TIMEOUT"}' }
            'diagnostics-key-case' { '{"Checkpoint":"onboarding","errorCategory":"timeout"}' }
            'diagnostics-malformed' { 'Bearer secret' }
            'diagnostics-large' { 'secret' * 1000 }
            'diagnostics-null' { 'null' }
            'diagnostics-array' { '[{"checkpoint":"onboarding","errorCategory":"timeout"}]' }
            'journey-failed' { '{"checkpoint":"onboarding-save","errorCategory":"assertion"}' }
            'journey-timeout' { '{"checkpoint":"onboarding-save","errorCategory":"unknown"}' }
            'diagnostics-none-failed' { '{"checkpoint":"complete","errorCategory":"none"}' }
            'diagnostics-timeout' { '{"checkpoint":"onboarding","errorCategory":"timeout"}' }
            'diagnostics-checkpoint-*' { @{checkpoint = $state.case.Substring('diagnostics-checkpoint-'.Length); errorCategory = 'unknown'} | ConvertTo-Json -Compress }
            default { '{"checkpoint":"complete","errorCategory":"none"}' }
        }
        if ($state.case -ne 'diagnostics-missing') { Set-Content -LiteralPath $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS -Value $record }
    }
    $process = [pscustomobject]@{
        Kind = $kind
        HasExited = ($kind -eq 'journey' -and $state.case -ne 'journey-timeout') -or ($kind -eq 'expo' -and $state.case -eq 'expo-exited')
        ExitCode = [int]($state.case -in @('journey-failed', 'diagnostics-none-failed', 'diagnostics-extra-failed')) * 7
    }
    $process | Add-Member ScriptMethod WaitForExit {
        param($milliseconds)
        if ($this.Kind -eq 'journey' -and $state.case -eq 'journey-timeout' -and $milliseconds -eq 600000) { return $false }
        if ($this.Kind -eq 'expo' -and $state.case -eq 'process-cleanup-failed') { return $false }
        $this.HasExited = $true
        return $true
    }
    $process | Add-Member ScriptMethod Kill {
        param($entireTree)
        if (!$entireTree) { throw 'Must stop owned descendants too.' }
        $state.killed += $this.Kind
        $this.HasExited = $true
    }
    return $process
}
$checkpointCases = @('setup', 'sign-in', 'onboarding', 'onboarding-save', 'custom-quest', 'save-retry', 'definition-edit', 'completion-response-loss', 'character', 'export-timer', 'offline-undo', 'clock-undo-retry', 'fresh-session', 'calendar-planning', 'plan-edit', 'viewports', 'page-errors', 'complete') | ForEach-Object { 'diagnostics-checkpoint-' + $_ }
try {
    foreach ($case in @('success', 'journey-failed', 'journey-timeout', 'expo-exited', 'start-failed', 'partial-create', 'database-remains', 'delete-failed', 'delete-unconfirmed', 'process-cleanup-failed', 'stop-failed', 'occupied', 'wrong-head', 'stale', 'diagnostics-extra', 'diagnostics-enum', 'diagnostics-type', 'diagnostics-case', 'diagnostics-key-case', 'diagnostics-malformed', 'diagnostics-large', 'diagnostics-null', 'diagnostics-array', 'diagnostics-missing', 'diagnostics-none-failed', 'diagnostics-extra-failed', 'diagnostics-timeout') + $checkpointCases) {
        $root = Join-Path $fixtureRoot $case
        New-Item -ItemType Directory -Path (Join-Path $root 'scripts') -Force | Out-Null
        Copy-Item (Join-Path $PSScriptRoot '../Test-BrowserJourneyCI.ps1') (Join-Path $root 'scripts/Test-BrowserJourneyCI.ps1')
        $state = @{ case = $case; exists = $false; instance = ''; creates = 0; stops = 0; deletes = 0; databaseChecks = 0; starts = 0; killed = @() }
        $env:PQ_EXPECTED_HEAD = if ($case -eq 'wrong-head') { 'b' * 40 } else { $head }
        $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE = 'preexisting-must-not-touch'
        $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS = 'prior-diagnostics'
        $env:CI = 'prior-ci'
        $env:EXPO_PUBLIC_APP_ENV = 'prior-app-env'
        if ($case -eq 'stale') {
            New-Item -ItemType Directory -Path (Join-Path $root 'artifacts/browser-journey') -Force | Out-Null
            'old' | Set-Content (Join-Path $root 'artifacts/browser-journey/run.json')
        }
        $failure = $null
        try { & (Join-Path $root 'scripts/Test-BrowserJourneyCI.ps1') | Out-Null }
        catch { $failure = $_ }
        $shouldPass = $case -in @('success', 'stop-failed') -or ($case.StartsWith('diagnostics-') -and !$case.EndsWith('-failed'))
        if ($shouldPass -and $failure) { throw "$case unexpectedly failed: $failure" }
        if ($case -eq 'diagnostics-extra-failed' -and ($failure.Exception.Message -ne 'Browser harness failed.')) { throw 'Malformed diagnostics replaced the original failure.' }
        if (!$shouldPass -and !$failure) { throw "$case falsely passed." }
        if ($env:POCKETQUESTS_JOURNEY_DIAGNOSTICS -ne 'prior-diagnostics' -or $env:CI -ne 'prior-ci' -or $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE -ne 'preexisting-must-not-touch' -or $env:EXPO_PUBLIC_APP_ENV -ne 'prior-app-env') { throw "$case did not restore environment." }
        if ($case -in @('occupied', 'wrong-head', 'stale')) {
            if ($state.creates -or $state.starts -or $state.deletes) { throw "$case changed resources before preflight rejection." }
        } else {
            if ($state.deletes -ne 1 -or $state.databaseChecks -ne 1) { throw "$case skipped SQL cleanup after failure." }
            if ($state.instance -notmatch '^PQSchema_[0-9a-f]{12}$') { throw 'Non-disposable instance name.' }
            if ($state.starts -gt 0 -and $case -ne 'expo-exited' -and $state.killed -notcontains 'expo') { throw "$case did not stop owned Expo." }
        }
        if ($case -ne 'stale') {
            $report = Get-Content (Join-Path $root 'artifacts/browser-journey/run.json') -Raw | ConvertFrom-Json
            $expectedCheckpoint = 'unknown'
            $expectedCategory = 'unknown'
            if ($case -in @('success', 'stop-failed', 'database-remains', 'delete-failed', 'delete-unconfirmed', 'process-cleanup-failed', 'diagnostics-none-failed')) { $expectedCheckpoint = 'complete' }
            if ($case -in @('success', 'stop-failed', 'database-remains', 'delete-failed', 'delete-unconfirmed', 'process-cleanup-failed')) { $expectedCategory = 'none' }
            if ($case -in @('journey-failed', 'journey-timeout')) { $expectedCheckpoint = 'onboarding-save' }
            if ($case -eq 'journey-failed') { $expectedCategory = 'assertion' }
            if ($case -eq 'diagnostics-timeout') { $expectedCheckpoint = 'onboarding'; $expectedCategory = 'timeout' }
            if ($case.StartsWith('diagnostics-checkpoint-')) { $expectedCheckpoint = $case.Substring('diagnostics-checkpoint-'.Length) }
            if ($report.journeyCheckpoint -cne $expectedCheckpoint -or $report.journeyErrorCategory -cne $expectedCategory) { throw "$case recorded incorrect diagnostics." }
            if (($report | ConvertTo-Json -Depth 10) -match 'secret|Bearer|headers|private/|token=') { throw "$case leaked child metadata." }
            if ($report.schemaVersion -ne 2) { throw 'Unexpected summary schema.' }
            if (($report.result -eq 'passed') -ne $shouldPass) { throw "$case recorded incorrect result." }
            if ($report.testedHead -ne $head -or $report.nativeDeviceResult -ne 'not-run' -or $report.liveProviderResult -ne 'not-run') { throw "$case misrepresented tested evidence." }
            if ($case -in @('journey-failed', 'journey-timeout') -and $report.fixtureLifecycleConfirmed) { throw 'Abnormal exit falsely confirmed descendant cleanup.' }
            if ($case -eq 'journey-failed' -and $report.journeyHarnessExitCode -ne 7) { throw 'Real harness exit code was lost.' }
            if ($case -eq 'journey-timeout' -and $null -ne $report.journeyHarnessExitCode) { throw 'Timeout fabricated an exit code.' }
        }
        Write-Output "$case passed."
    }
} finally {
    $env:POCKETQUESTS_JOURNEY_DIAGNOSTICS = $originalDiagnostics
    $env:PQ_EXPECTED_HEAD = $originalExpected
    $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE = $originalInstance
    $env:CI = $originalCI
    $env:EXPO_PUBLIC_APP_ENV = $originalAppEnv
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
}
