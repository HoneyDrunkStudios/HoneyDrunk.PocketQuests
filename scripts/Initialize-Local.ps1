[CmdletBinding()]
param([Parameter(Mandatory)][string]$IdentitySourceRoot, [string]$ClientPackageDirectory)
$ErrorActionPreference = 'Stop'
$appRoot = Split-Path $PSScriptRoot -Parent
$identityRoot = (Resolve-Path -LiteralPath $IdentitySourceRoot).Path
$identityProject = Join-Path $identityRoot 'HoneyDrunk.Identity/HoneyDrunk.Identity.Api/HoneyDrunk.Identity.Api.csproj'
if (!(Test-Path -LiteralPath $identityProject)) { throw 'Expected a HoneyDrunk.Identity checkout with its API project.' }
$localDirectory = Join-Path $appRoot '.local'
New-Item -ItemType Directory -Force -Path $localDirectory | Out-Null
$escapedRoot = [Security.SecurityElement]::Escape($identityRoot)
$useSource = (!$ClientPackageDirectory).ToString().ToLowerInvariant()
"<Project><PropertyGroup><IdentitySourceRoot>$escapedRoot</IdentitySourceRoot><UseIdentitySource>$useSource</UseIdentitySource></PropertyGroup></Project>" | Set-Content (Join-Path $localDirectory 'Identity.props')
$settingsPath = Join-Path $localDirectory 'development.json'
$settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable } else { @{ Mobile = @{ DeviceHost = 'localhost' } } }
if (!$settings.ContainsKey('Identity')) { $settings.Identity = @{} }
$settings.Identity.SourceRoot = $identityRoot
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $settingsPath
$feedDirectory = Join-Path $appRoot '.packages/feed'
New-Item -ItemType Directory -Force -Path $feedDirectory | Out-Null
if ($ClientPackageDirectory) {
    foreach ($package in @('HoneyDrunk.Identity.Abstractions.0.1.0-alpha.4.nupkg', 'HoneyDrunk.Identity.Client.0.1.0-alpha.4.nupkg')) {
        Copy-Item -LiteralPath (Join-Path $ClientPackageDirectory $package) -Destination (Join-Path $feedDirectory $package)
    }
}

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
Write-Host 'Explicit Identity source configured; source references are the default unless candidate packages were supplied. No cloud service or GitHub state changed.'
