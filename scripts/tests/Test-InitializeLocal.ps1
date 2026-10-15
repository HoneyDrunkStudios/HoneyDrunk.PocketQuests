# Exercise setup in disposable copies; never invoke the real .NET CLI or SQL.
$ErrorActionPreference = 'Stop'
$initializeTestState = @{ calls = [Collections.Generic.List[object]]::new() }

function dotnet {
    $initializeTestState.calls.Add([string[]]$args)
    $global:LASTEXITCODE = 0
}

function Assert-Setup([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
}

function Get-FixtureSnapshot([string]$root) {
    (@(Get-ChildItem -LiteralPath $root -Force -Recurse | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
        if ($_.PSIsContainer) { "directory:$relative" }
        else { "file:${relative}:$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)" }
    }) | Sort-Object) -join "`n"
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('pq-initialize-contract-' + [guid]::NewGuid().ToString('N'))
$initializer = Join-Path $PSScriptRoot '../Initialize-Local.ps1'
try {
    foreach ($caseName in @('source', 'published', 'legacy', 'legacy-empty')) {
        $initializeTestState.calls.Clear()
        $appRoot = Join-Path $fixtureRoot $caseName
        $scripts = Join-Path $appRoot 'scripts'
        $solutionDirectory = Join-Path $appRoot 'HoneyDrunk.PocketQuests'
        $localDirectory = Join-Path $appRoot '.local'
        $identityRoot = Join-Path $appRoot 'identity source & test'
        $identityProject = Join-Path $identityRoot 'HoneyDrunk.Identity/HoneyDrunk.Identity.Api/HoneyDrunk.Identity.Api.csproj'
        foreach ($directory in @($scripts, $solutionDirectory, $localDirectory, (Split-Path $identityProject -Parent))) {
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
        }
        $scriptPath = Join-Path $scripts 'Initialize-Local.ps1'
        Copy-Item -LiteralPath $initializer -Destination $scriptPath
        '<Project />' | Set-Content -LiteralPath $identityProject
        $solutionPath = Join-Path $solutionDirectory 'HoneyDrunk.PocketQuests.slnx'
        '<Solution><Folder Name="/Identity/"><Project Path="old-identity.csproj" /></Folder><Folder Name="/Docs/"><File Path="../README.md" /></Folder><Project Path="Product.csproj" /></Solution>' |
            Set-Content -LiteralPath $solutionPath
        '{"Mobile":{"DeviceHost":"retained-host"},"Identity":{"SourceRoot":"old-source"}}' |
            Set-Content -LiteralPath (Join-Path $localDirectory 'development.json')
        'Keep unrelated files unchanged.' | Set-Content -LiteralPath (Join-Path $appRoot 'unrelated.txt')

        $arguments = @{ IdentitySourceRoot = $identityRoot }
        if ($caseName -eq 'published') { $arguments.UsePublishedClientPackages = $true }
        if ($caseName -like 'legacy*') {
            $arguments.ClientPackageDirectory = if ($caseName -eq 'legacy-empty') { '' } else { 'old-candidates' }
            # Rejection must precede both mode selection and even source-path resolution.
            $arguments.UsePublishedClientPackages = $true
            $arguments.IdentitySourceRoot = Join-Path $appRoot 'missing-identity'
        }
        $before = Get-FixtureSnapshot $appRoot
        $failure = $null
        try { & $scriptPath @arguments | Out-Null }
        catch { $failure = $_.Exception.Message }

        if ($caseName -like 'legacy*') {
            Assert-Setup ($failure -like '-ClientPackageDirectory is no longer supported.*') "${caseName}: unexpected rejection: $failure"
            Assert-Setup ($initializeTestState.calls.Count -eq 0) "${caseName}: invoked dotnet before rejection."
            Assert-Setup ((Get-FixtureSnapshot $appRoot) -ceq $before) "${caseName}: changed files or directories before rejection."
            Write-Output "${caseName} passed: explicit rejection, no writes and no external commands."
            continue
        }

        Assert-Setup (!$failure) "${caseName}: initialization failed: $failure"
        $props = [xml](Get-Content -LiteralPath (Join-Path $localDirectory 'Identity.props') -Raw)
        $expectedMode = if ($caseName -eq 'source') { 'true' } else { 'false' }
        Assert-Setup ($props.Project.PropertyGroup.UseIdentitySource -ceq $expectedMode) "${caseName}: incorrect source/package mode."
        Assert-Setup ($props.Project.PropertyGroup.IdentitySourceRoot -ceq $identityRoot) "${caseName}: source path was not preserved and XML-escaped."
        $settings = Get-Content -LiteralPath (Join-Path $localDirectory 'development.json') -Raw | ConvertFrom-Json
        Assert-Setup ($settings.Identity.SourceRoot -ceq $identityRoot) "${caseName}: development source path was not updated."
        Assert-Setup ($settings.Mobile.DeviceHost -ceq 'retained-host') "${caseName}: unrelated development settings changed."
        Assert-Setup (!(Test-Path -LiteralPath (Join-Path $appRoot '.packages'))) "${caseName}: created a retired local package feed."
        Assert-Setup ((Get-Content -LiteralPath (Join-Path $appRoot 'unrelated.txt') -Raw).Trim() -ceq 'Keep unrelated files unchanged.') "${caseName}: unrelated file changed."
        $solution = [xml](Get-Content -LiteralPath $solutionPath -Raw)
        Assert-Setup (@($solution.Solution.Folder | Where-Object Name -eq '/Identity/').Count -eq 0) "${caseName}: retained stale Identity solution paths."
        Assert-Setup (@($solution.Solution.Folder | Where-Object Name -eq '/Docs/').Count -eq 1) "${caseName}: removed an unrelated solution folder."
        Assert-Setup ($solution.Solution.Project.Path -ceq 'Product.csproj') "${caseName}: changed an unrelated solution project."
        $calls = $initializeTestState.calls
        Assert-Setup ($calls.Count -eq 8) "${caseName}: expected one build, six project registrations and one restore."
        Assert-Setup (($calls[0][0] -ceq 'build') -and ($calls[0][1] -ceq $identityProject)) "${caseName}: incorrect service build target."
        $names = @('HoneyDrunk.Identity.Abstractions', 'HoneyDrunk.Identity.Api', 'HoneyDrunk.Identity.Client', 'HoneyDrunk.Identity.Database', 'HoneyDrunk.Identity.Providers.Entra', 'HoneyDrunk.Identity')
        for ($index = 0; $index -lt $names.Count; $index++) {
            $name = $names[$index]
            $extension = if ($name.EndsWith('.Database')) { 'sqlproj' } else { 'csproj' }
            $project = Join-Path $identityRoot "HoneyDrunk.Identity/$name/$name.$extension"
            $expected = @('sln', $solutionPath, 'add', $project, '--solution-folder', 'Identity')
            Assert-Setup (($calls[$index + 1] -join '|') -ceq ($expected -join '|')) "${caseName}: incorrect Identity project registration for $name."
        }
        Assert-Setup (($calls[7][0] -ceq 'restore') -and ($calls[7][1] -ceq $solutionPath)) "${caseName}: incorrect solution restore target."
        Write-Output "${caseName} passed: mode, preserved settings, solution targets and absence of a local feed verified."
    }
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
