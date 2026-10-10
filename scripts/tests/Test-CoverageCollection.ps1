# Exercise the coverage adapter without starting SQL or invoking external tools.
$ErrorActionPreference = 'Stop'
$coverageTestState = @{ caseName = ''; coverageRequested = $false; deleted = $false; evidence = ''; calls = 0 }

function python { $coverageTestState.calls++; $global:LASTEXITCODE = 0 }
function sqllocaldb {
    $coverageTestState.calls++
    if ($args[0] -eq 'delete') { $coverageTestState.deleted = $true }
    $global:LASTEXITCODE = 0
}
function sqlcmd { $coverageTestState.calls++; $global:LASTEXITCODE = 0 }
function dotnet {
    $coverageTestState.calls++
    $coverageTestState.coverageRequested = ($args -contains '--collect') -and
        ($args -contains 'XPlat Code Coverage') -and
        ($args -contains 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover')
    $failed = [int]($coverageTestState.caseName -like 'failure*')
    "<TestRun><ResultSummary><Counters total='1' passed='$(1-$failed)' failed='$failed' notExecuted='0' /></ResultSummary></TestRun>" |
        Set-Content (Join-Path $coverageTestState.evidence 'schema-tests.trx')
    if ($coverageTestState.caseName -notlike '*missing') {
        '<CoverageSession />' | Set-Content (Join-Path $coverageTestState.evidence 'coverage.opencover.xml')
    }
    $global:LASTEXITCODE = $failed
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('pq-coverage-contract-' + [guid]::NewGuid().ToString('N'))
foreach ($case in @('success', 'missing', 'failure', 'failure-missing')) {
    $coverageTestState.caseName = $case
    $coverageTestState.deleted = $false
    $coverageTestState.coverageRequested = $false
    $coverageTestState.evidence = Join-Path $fixtureRoot $case
    $failure = $null
    try {
        & (Join-Path $PSScriptRoot '../Test-RelationalSchema.ps1') -NoBuild -IdentityIntegration -CollectCoverage -EvidenceDirectory $coverageTestState.evidence | Out-Null
    } catch { $failure = $_.Exception.Message }
    if (!$coverageTestState.coverageRequested) { throw "$case did not request OpenCover from the test runner. Failure: $failure" }
    if (!$coverageTestState.deleted) { throw "$case did not execute fixture cleanup." }
    if ($case -eq 'success' -and $failure) { throw $failure }
    if ($case -eq 'missing' -and $failure -ne 'Coverage was requested but no OpenCover report was generated.') { throw "Missing coverage was not rejected: $failure" }
    if ($case -like 'failure*' -and $failure -ne 'Schema tests failed; inspect tests.log and TRX.') { throw "Test failure was not preserved: $failure" }
    $saved = Get-Content (Join-Path $coverageTestState.evidence 'run.json') -Raw | ConvertFrom-Json
    if (!$saved.databaseCleanupVerified -or $saved.testTotal -ne 1) { throw "$case hid test results or database cleanup verification." }
    if ($case -like 'failure*' -and $saved.testFailed -ne 1) { throw "$case lost failed test counts." }
    Write-Output "$case passed: coverage request, result and cleanup verified."
}

foreach ($case in @('unsupported', 'stale')) {
    $coverageTestState.calls = 0
    $coverageTestState.evidence = Join-Path $fixtureRoot $case
    $arguments = @{ NoBuild = $true; CollectCoverage = $true; EvidenceDirectory = $coverageTestState.evidence }
    if ($case -eq 'stale') {
        $arguments.IdentityIntegration = $true
        New-Item -ItemType Directory -Path $coverageTestState.evidence | Out-Null
        '<CoverageSession />' | Set-Content (Join-Path $coverageTestState.evidence 'coverage.opencover.xml')
    }
    $failure = $null
    try { & (Join-Path $PSScriptRoot '../Test-RelationalSchema.ps1') @arguments | Out-Null }
    catch { $failure = $_.Exception.Message }
    $expected = if ($case -eq 'stale') { 'Use a fresh evidence directory*' } else { 'Coverage collection currently requires*' }
    if ($failure -notlike $expected -or $coverageTestState.calls -ne 0) { throw "$case was not rejected before external commands: $failure" }
    Write-Output "$case passed: rejected before any external command."
}
