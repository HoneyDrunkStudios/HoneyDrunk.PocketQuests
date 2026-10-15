[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IdentitySourceRoot,
    [switch]$UsePublishedClientPackages,
    [string]$ClientPackageDirectory
)
$ErrorActionPreference = 'Stop'
if ($PSBoundParameters.ContainsKey('ClientPackageDirectory')) {
    throw '-ClientPackageDirectory is no longer supported. Use -UsePublishedClientPackages for the locked Identity packages from nuget.org, or omit it to use source references.'
}
$appRoot = Split-Path $PSScriptRoot -Parent
$identityRoot = (Resolve-Path -LiteralPath $IdentitySourceRoot).Path
$identityProject = Join-Path $identityRoot 'HoneyDrunk.Identity/HoneyDrunk.Identity.Api/HoneyDrunk.Identity.Api.csproj'
if (!(Test-Path -LiteralPath $identityProject)) { throw 'Expected a HoneyDrunk.Identity checkout with its API project.' }
$localDirectory = Join-Path $appRoot '.local'
New-Item -ItemType Directory -Force -Path $localDirectory | Out-Null
$escapedRoot = [Security.SecurityElement]::Escape($identityRoot)
$useSource = (-not $UsePublishedClientPackages).ToString().ToLowerInvariant()
"<Project><PropertyGroup><IdentitySourceRoot>$escapedRoot</IdentitySourceRoot><UseIdentitySource>$useSource</UseIdentitySource></PropertyGroup></Project>" | Set-Content (Join-Path $localDirectory 'Identity.props')
$settingsPath = Join-Path $localDirectory 'development.json'
$settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable } else { @{ Mobile = @{ DeviceHost = 'localhost' } } }
if (!$settings.ContainsKey('Identity')) { $settings.Identity = @{} }
$settings.Identity.SourceRoot = $identityRoot
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $settingsPath
dotnet build $identityProject --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Identity service build failed.' }
$solutionPath = Join-Path $appRoot 'HoneyDrunk.PocketQuests/HoneyDrunk.PocketQuests.slnx'
# Visual Studio can launch Aspire project resources only when they are loaded.
$solution = [System.Xml.Linq.XDocument]::Load($solutionPath)
$folder = @($solution.Root.Elements('Folder') | Where-Object { $_.Attribute('Name').Value -eq '/Identity/' })
foreach ($item in $folder) { $item.Remove() }
$solution.Save($solutionPath)
foreach ($name in @('HoneyDrunk.Identity.Abstractions', 'HoneyDrunk.Identity.Api', 'HoneyDrunk.Identity.Client', 'HoneyDrunk.Identity.Database', 'HoneyDrunk.Identity.Providers.Entra', 'HoneyDrunk.Identity')) {
    $extension = if ($name.EndsWith('.Database')) { 'sqlproj' } else { 'csproj' }
    dotnet sln $solutionPath add (Join-Path $identityRoot "HoneyDrunk.Identity/$name/$name.$extension") --solution-folder Identity
    if ($LASTEXITCODE -ne 0) { throw "Adding $name to the development solution failed." }
}
dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw 'Pocket Quests restore failed.' }
if ($UsePublishedClientPackages) {
    Write-Host 'Identity service source configured; consumers use the locked published Identity packages from nuget.org. No cloud service or GitHub state changed.'
} else {
    Write-Host 'Explicit Identity source configured; consumers use source references. No cloud service or GitHub state changed.'
}
