[CmdletBinding()]
param([switch]$NoBuild, [string]$EvidenceDirectory, [string]$TestFilter)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$EvidenceDirectory) { $EvidenceDirectory = Join-Path $root 'artifacts/schema-validation' }
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$EvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$instance = 'PQSchema_' + [guid]::NewGuid().ToString('N').Substring(0,12)
$created = $false
$previousInstance = $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE
$previousEvidence = $env:POCKETQUESTS_SCHEMA_EVIDENCE
$summary = [ordered]@{ instance=$instance; startedAt=[DateTimeOffset]::UtcNow.ToString('o'); result='not-run'; databaseCleanupVerified=$false; instanceDeleted=$false }
try {
    $checkerHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'schema/schema-checks.sql') -Algorithm SHA256).Hash
    if ($checkerHash -ne '326979B44AB43EC01293DB427DF2682FE02E797C6C6D542012D8062A50214BAB') { throw 'The reviewed table-design checker changed; review it explicitly before replacing the pinned hash.' }
    $summary.checkerSha256 = $checkerHash
    & python (Join-Path $PSScriptRoot 'schema/validate_contract.py') *> (Join-Path $EvidenceDirectory 'contract.log')
    if ($LASTEXITCODE -ne 0) { throw 'Contract structure failed.' }
    & python (Join-Path $PSScriptRoot 'schema/generate_schema.py') --check *> (Join-Path $EvidenceDirectory 'generation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Generated DDL/mapping/seed drift.' }
    & python (Join-Path $PSScriptRoot 'schema/generate_mutation.py') --check *> (Join-Path $EvidenceDirectory 'mutation-generation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Generated mutation boundary drift.' }
    & python (Join-Path $PSScriptRoot 'schema/generate_erasure.py') --check *> (Join-Path $EvidenceDirectory 'erasure-generation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Generated erasure ownership/order drift.' }
    & python (Join-Path $PSScriptRoot 'schema/test_contract.py') *> (Join-Path $EvidenceDirectory 'contract-mutation-tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Contract mutation probes failed.' }
    Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'contract-mutation-tests.log')
    $project = Join-Path $root 'HoneyDrunk.PocketQuests/PocketQuests.SchemaTests/PocketQuests.SchemaTests.csproj'
    if (!$NoBuild) {
        & dotnet build $project --configuration Release --nologo -m:1 -nr:false *> (Join-Path $EvidenceDirectory 'build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Schema build failed; inspect build.log.' }
    }
    if (@(& sqllocaldb info) -contains $instance) { throw 'Refusing to reuse an existing SQL instance.' }
    & sqllocaldb create $instance -s *> (Join-Path $EvidenceDirectory 'instance-create.log')
    if ($LASTEXITCODE -ne 0) { throw 'Isolated LocalDB creation failed.' }
    $created = $true
    $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE = $instance
    $env:POCKETQUESTS_SCHEMA_EVIDENCE = $EvidenceDirectory
    $filterArguments = @()
    if ($TestFilter) { $filterArguments = @('--filter', $TestFilter); $summary.testFilter = $TestFilter }
    & dotnet test $project --configuration Release --no-build --no-restore --logger 'trx;LogFileName=schema-tests.trx' --results-directory $EvidenceDirectory @filterArguments *> (Join-Path $EvidenceDirectory 'tests.log')
    $testExitCode = $LASTEXITCODE
    [xml]$testResults = Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'schema-tests.trx') -Raw
    $counters = $testResults.TestRun.ResultSummary.Counters
    $summary.testProject = 'PocketQuests.SchemaTests'
    $summary.testTotal = [int]$counters.total
    $summary.testPassed = [int]$counters.passed
    $summary.testFailed = [int]$counters.failed
    $summary.testNotExecuted = [int]$counters.notExecuted
    if ($testExitCode -ne 0) { throw 'Schema tests failed; inspect tests.log and TRX.' }
    & sqlcmd -S "(localdb)\$instance" -E -I -b -d master -Q "SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM sys.databases WHERE name LIKE N'PocketQuests[_]SchemaTests[_]%') THROW 51010,'A schema fixture did not remove its database.',1; PRINT 'All generated schema fixture databases were removed.';" -o (Join-Path $EvidenceDirectory 'database-cleanup.log')
    if ($LASTEXITCODE -ne 0) { throw 'Scratch database cleanup verification failed.' }
    $summary.databaseCleanupVerified = $true
    $summary.result = 'passed'
}
catch {
    $summary.result = 'failed'
    $summary.error = $_.Exception.Message
    throw
}
finally {
    $env:POCKETQUESTS_SCHEMA_TEST_INSTANCE = $previousInstance
    $env:POCKETQUESTS_SCHEMA_EVIDENCE = $previousEvidence
    if ($created -and $instance -match '^PQSchema_[0-9a-f]{12}$') {
        & sqllocaldb stop $instance -k *> (Join-Path $EvidenceDirectory 'instance-stop.log')
        & sqllocaldb delete $instance *> (Join-Path $EvidenceDirectory 'instance-delete.log')
        $summary.instanceDeleted = $LASTEXITCODE -eq 0
    }
    $summary.finishedAt = [DateTimeOffset]::UtcNow.ToString('o')
    $summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'run.json') -Encoding utf8
    $summary | ConvertTo-Json
}
