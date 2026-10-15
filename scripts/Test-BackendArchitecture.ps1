[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'HoneyDrunk.PocketQuests/PocketQuests.SchemaTests/PocketQuests.SchemaTests.csproj'

Push-Location $root
try {
    if (-not $NoBuild) {
        dotnet restore $project --locked-mode -p:UseIdentitySource=false
        if ($LASTEXITCODE -ne 0) { throw "Architecture restore failed ($LASTEXITCODE)." }
        dotnet build $project --configuration $Configuration --no-restore -p:UseIdentitySource=false
        if ($LASTEXITCODE -ne 0) { throw "Architecture build failed ($LASTEXITCODE)." }
    }

    # These tests inspect compiled code and scoped registrations; they never open SQL.
    dotnet test $project --configuration $Configuration --no-build --no-restore -p:UseIdentitySource=false `
        --filter 'FullyQualifiedName~PocketQuests.SchemaTests.Architecture.|FullyQualifiedName~PocketQuests.SchemaTests.EfDataServiceRegistrationTests' `
        -- RunConfiguration.TreatNoTestsAsError=true
    if ($LASTEXITCODE -ne 0) { throw "Architecture tests failed ($LASTEXITCODE)." }
}
finally {
    Pop-Location
}
