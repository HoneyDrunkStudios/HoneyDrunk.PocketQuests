#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9._:-]+$')][string]$DeviceId,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$BuildCommit,
    [Parameter(Mandatory)][switch]$DisposableFixture
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (!$DisposableFixture) { throw 'Use the isolated, seeded account described in apps/mobile/.maestro/README.md.' }
$root = Split-Path $PSScriptRoot -Parent
$head = git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $BuildCommit) { throw 'The checked-out and installed build commits must match.' }
$changed = git -C $root status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify working-tree changes.' }
$maestro = Get-Command maestro -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if (!$maestro) { throw 'Maestro is required. Install it and attach the isolated test device.' }

$output = Join-Path $root ('artifacts/native-journey/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$summary = [ordered]@{
    scenario = 'quest-completion-undo-restart'
    checkoutCommit = $head
    reportedBuildCommit = $BuildCommit
    workingTreeDirty = [bool]$changed
    device = $DeviceId
    result = 'failed'
    exitCode = $null
    ledgerCheck = 'manual'
    liveProviderResult = 'not-run'
}
try {
    $report = Join-Path $output 'journey.xml'
    $flow = Join-Path $root 'apps/mobile/.maestro/quest-slice.yaml'
    & $maestro.Source "--device=$DeviceId" test --format junit --output $report --test-output-dir $output $flow
    $summary.exitCode = $LASTEXITCODE
    if ($summary.exitCode -ne 0) { throw "Maestro exited $($summary.exitCode)." }
    if (!(Test-Path $report)) { throw 'Maestro did not write the expected JUnit report.' }
    [xml]$junit = Get-Content -LiteralPath $report -Raw
    $cases = $junit.SelectNodes('//testcase')
    if ($cases.Count -ne 1 -or $cases[0].SelectSingleNode('failure | error | skipped')) {
        throw 'The JUnit report must contain one successful quest scenario.'
    }
    $summary.result = 'passed'
} finally {
    $summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'run.json') -Encoding utf8
    Write-Output "Native verification evidence: $output"
}
