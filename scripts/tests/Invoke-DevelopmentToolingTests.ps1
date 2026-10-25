[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PesterModulePath,
    [string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $EvidenceDirectory) { $EvidenceDirectory = Join-Path $repo 'artifacts/development-tooling' }
$null = New-Item -ItemType Directory -Force -Path $EvidenceDirectory
$evidence = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$module = Import-Module $PesterModulePath -PassThru
if ($module.Version -ne [version]'6.2.0') { throw 'This runner requires pinned Pester 6.2.0.' }
. (Join-Path $PSScriptRoot 'Export-PesterCoverage.ps1')
$sources = @('scripts/Start-ExpoGo.ps1', 'scripts/Test-ExpoGo.ps1', 'scripts/development-process.psm1')
$config = New-PesterConfiguration
$config.Run.Path = @((Join-Path $PSScriptRoot 'DevelopmentTooling.Tests.ps1'), (Join-Path $PSScriptRoot 'CoverageReport.Tests.ps1'))
$config.Run.PassThru = $true
# These tests need files, not a registry fixture or elevated registry access.
$config.TestRegistry.Enabled = $false
$config.Output.Verbosity = 'Normal'
$config.TestResult.Enabled = $true
$config.TestResult.OutputPath = Join-Path $evidence 'tests.xml'
$config.CodeCoverage.Enabled = $true
$config.CodeCoverage.Path = @($sources | ForEach-Object { Join-Path $repo $_ })
$config.CodeCoverage.ReportRoot = $repo
$config.CodeCoverage.OutputPath = Join-Path $evidence 'coverage.xml'
$result = Invoke-Pester -Configuration $config
if ($result.Result -ne 'Passed' -or $result.TotalCount -eq 0) { throw 'Development tooling tests did not pass.' }
Export-PesterCoverage $result.CodeCoverage $repo $sources (Join-Path $evidence 'sonar-coverage.xml')
