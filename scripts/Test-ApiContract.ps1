param([switch]$Update, [switch]$NoBuild, [string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path ([System.IO.Path]::GetTempPath()) ('pq-openapi-' + [Guid]::NewGuid().ToString('N') + '.json')
$expected = Join-Path $root 'contracts/pocketquests-v1.openapi.json'
$previousOutput = $env:POCKETQUESTS_OPENAPI_OUTPUT
try {
    $env:POCKETQUESTS_OPENAPI_OUTPUT = $output
    $arguments = @('test', (Join-Path $root 'HoneyDrunk.PocketQuests/PocketQuests.SchemaTests/PocketQuests.SchemaTests.csproj'),
        '--filter', 'FullyQualifiedName~PocketQuests.Tests.Api.ApiContractTests', '--configuration', $Configuration, '--verbosity', 'minimal')
    if ($NoBuild) { $arguments += '--no-build' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Endpoint contract or wire compatibility tests failed.' }
    if (-not (Test-Path -LiteralPath $output)) { throw 'The endpoint-derived document was not generated.' }
    if ($Update) {
        New-Item -ItemType Directory -Path (Split-Path $expected -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $output -Destination $expected
    }
    elseif (-not (Test-Path -LiteralPath $expected) -or
        ([IO.File]::ReadAllText($expected) -replace "`r`n", "`n") -cne ([IO.File]::ReadAllText($output) -replace "`r`n", "`n")) {
        throw 'Endpoint OpenAPI drifted. Run scripts/Test-ApiContract.ps1 -Update and regenerate the mobile API client.'
    }
    & (Join-Path $PSScriptRoot 'Test-ClientContract.ps1') -Update:$Update
    Write-Output 'Endpoint metadata, committed OpenAPI, HTTP wire compatibility, fixtures and generated client agree.'
}
finally {
    $env:POCKETQUESTS_OPENAPI_OUTPUT = $previousOutput
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
}
