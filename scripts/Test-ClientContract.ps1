param([switch]$Update)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path ([System.IO.Path]::GetTempPath()) ('pq-wire-' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $output 'wire-fixtures.json'
$expected = Join-Path $root 'contracts/wire-fixtures.json'
try {
    & dotnet run --project (Join-Path $root 'tools/PocketQuests.WireFixtures') -- $output
    if ($LASTEXITCODE -ne 0) { throw 'Wire fixture generation failed.' }
    if ($Update) { Copy-Item -LiteralPath $fixture -Destination $expected }
    elseif (-not (Test-Path -LiteralPath $expected) -or
        ([IO.File]::ReadAllText($expected) -replace "`r`n", "`n") -cne ([IO.File]::ReadAllText($fixture) -replace "`r`n", "`n")) {
        throw 'Wire fixtures drifted. Run scripts/Test-ApiContract.ps1 -Update.'
    }
    $arguments = @((Join-Path $root 'apps/mobile/scripts/generate-api.cjs'))
    if (-not $Update) { $arguments += '--check' }
    & node @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Generated mobile client drifted.' }
    Write-Output 'Serialized wire fixtures and generated client agree with their committed sources.'
}
finally {
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
}
