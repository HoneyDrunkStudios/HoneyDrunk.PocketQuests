# Exercise the coverage adapter without starting SQL or invoking external tools.
$ErrorActionPreference = 'Stop'
$coverageTestState = @{ caseName = ''; coverageRequested = $false; deleted = $false; evidence = '' }

function python { $global:LASTEXITCODE = 0 }
function sqllocaldb {
    if ($args[0] -eq 'delete') { $coverageTestState.deleted = $true }
    $global:LASTEXITCODE = 0
}
function sqlcmd { $global:LASTEXITCODE = 0 }
function dotnet {
    $coverageTestState.coverageRequested = ($args -contains '--collect') -and
        ($args -contains 'XPlat Code Coverage') -and
        ($args -contains 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover')
    '<TestRun><ResultSummary><Counters total="1" passed="1" failed="0" notExecuted="0" /></ResultSummary></TestRun>' |
        Set-Content (Join-Path $coverageTestState.evidence 'schema-tests.trx')
    if ($coverageTestState.caseName -ne 'missing') {
        '<CoverageSession />' | Set-Content (Join-Path $coverageTestState.evidence 'coverage.opencover.xml')
    }
    $global:LASTEXITCODE = if ($coverageTestState.caseName -eq 'failure') { 1 } else { 0 }
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('pq-coverage-contract-' + [guid]::NewGuid().ToString('N'))
foreach ($case in @('success', 'missing', 'failure')) {
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
    if ($case -eq 'failure' -and $failure -ne 'Schema tests failed; inspect tests.log and TRX.') { throw "Test failure was not preserved: $failure" }
    Write-Output "$case passed: coverage request, result and cleanup verified."
}
