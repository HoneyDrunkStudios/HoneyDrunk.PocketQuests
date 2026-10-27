Describe 'Development tooling orchestration' {
BeforeAll {
    $script:repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $script:launcher = Join-Path $script:repoRoot 'scripts/Start-ExpoGo.ps1'
    $script:integration = Join-Path $script:repoRoot 'scripts/Test-ExpoGo.ps1'
    Import-Module (Join-Path $script:repoRoot 'scripts/development-process.psm1')

    $script:originalScenario = $global:PocketQuestsToolingScenario
    $script:originalProbe = $global:PocketQuestsToolingProbe

    # These fallbacks fail closed if a mock is missed: no real native tool runs.
    function dotnet { throw 'Unmocked build is forbidden in orchestration tests.' }
    function node { throw 'Unmocked Expo is forbidden in orchestration tests.' }
    function sqllocaldb { throw 'Unmocked SQL is forbidden in orchestration tests.' }
    function sqlcmd { throw 'Unmocked SQL is forbidden in orchestration tests.' }

    function New-TestProcess([bool]$Exited, [int]$Code, [string]$ErrorText) {
        $reader = [pscustomobject]@{ Value = $ErrorText }
        $reader | Add-Member ScriptMethod ReadToEndAsync { [Threading.Tasks.Task[string]]::FromResult($this.Value) }
        $output = [pscustomobject]@{ Value = '' }
        $output | Add-Member ScriptMethod ReadToEndAsync { [Threading.Tasks.Task[string]]::FromResult($this.Value) }
        $child = [pscustomobject]@{
            HasExited = $Exited; ExitCode = $Code; StandardError = $reader
            StandardOutput = $output; Stats = $global:PocketQuestsToolingProbe
        }
        $child | Add-Member ScriptMethod Kill { param($tree) $this.Stats.killed++; $this.HasExited = $true }
        $child | Add-Member ScriptMethod WaitForExit { param($milliseconds) return $this.HasExited }
        $child | Add-Member ScriptMethod Dispose { $this.Stats.disposed++ }
        return $child
    }
}

AfterAll {
    $global:PocketQuestsToolingScenario = $script:originalScenario
    $global:PocketQuestsToolingProbe = $script:originalProbe
}

BeforeEach {
    $global:PocketQuestsToolingScenario = 'success'
    $global:PocketQuestsToolingProbe = @{
        fakeExe = Join-Path $TestDrive 'never-start-a-real-process.exe'
        starts = [Collections.Generic.List[object]]::new()
        killed = 0; disposed = 0; generation = 0; wrongPins = 0
        sql = [Collections.Generic.List[object]]::new()
        databaseCleanup = 0; nodeCalls = 0
        operations = [Collections.Generic.List[string]]::new()
    }
    $script:resultDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
    Mock Get-NetTCPConnection { if ($global:PocketQuestsToolingScenario -eq 'occupied') { 'existing-listener' } }
    Mock Get-Command { @{ Source = $global:PocketQuestsToolingProbe.fakeExe } } -ParameterFilter { $Name -eq 'dotnet' }
    Mock Test-Path { $global:PocketQuestsToolingScenario -ne 'missing-build' } -ParameterFilter {
        $LiteralPath -like '*DevelopmentHost.dll' -or $LiteralPath -like '*expo?bin?cli'
    }
    Mock dotnet { $global:LASTEXITCODE = [int]($global:PocketQuestsToolingScenario -eq 'build-failed') }
    Mock node {
        $global:PocketQuestsToolingProbe.nodeCalls++
        $env:EXPO_PUBLIC_API_URL | Should -Be 'https://phone.example'
        $env:EXPO_PUBLIC_IDENTITY_URL | Should -Be 'https://phone.example/identity'
        $env:EXPO_PUBLIC_DEVELOPMENT_SIGN_IN | Should -Be '1'
        $global:LASTEXITCODE = [int]($global:PocketQuestsToolingScenario -eq 'expo-failed')
    }
    Mock Write-Host {}
    Mock Start-Sleep {}
    Mock Start-DevelopmentProcess {
        $global:PocketQuestsToolingProbe.starts.Add($StartInfo)
        $StartInfo.UseShellExecute | Should -BeFalse
        $StartInfo.CreateNoWindow | Should -BeTrue
        $environment = $StartInfo.Environment['ASPNETCORE_ENVIRONMENT']
        $enabled = $StartInfo.Environment['DevelopmentSignIn__Enabled']
        $pin = $StartInfo.Environment['DevelopmentSignIn__PairingCode']
        if ($environment -ne 'Development' -or $enabled -ne 'true') {
            if ($global:PocketQuestsToolingScenario -eq 'gate-bypass') { return New-TestProcess $true 0 '' }
            return New-TestProcess $true 1 'requires Development and explicit'
        }
        if ($pin -notmatch '^\d{6}$') { return New-TestProcess $true 1 'exactly six digits' }
        $global:PocketQuestsToolingProbe.generation++
        $global:PocketQuestsToolingProbe.wrongPins = 0
        return New-TestProcess ($global:PocketQuestsToolingScenario -eq 'host-exited') 0 ''
    }
    Mock Invoke-RestMethod {
        if ($global:PocketQuestsToolingScenario -eq 'not-ready') { throw 'Not ready' }
        $instanceId = if ($global:PocketQuestsToolingScenario -eq 'wrong-proxy' -and "$Uri" -like 'https:*') { 'another-host' } else { 'this-host' }
        @{ mode = 'development'; instanceId = $instanceId }
    }
    Mock sqllocaldb {
        $verb = $args[0]
        $global:LASTEXITCODE = 0
        if ($verb -eq 'info') { return 'retained-production-instance' }
        $instance = $args[1]
        $instance | Should -Match '^PQExpoGo_[0-9a-f]{12}$'
        $global:PocketQuestsToolingProbe.sql.Add(@{ verb = $verb; instance = $instance })
        if ($verb -eq 'create' -and $global:PocketQuestsToolingScenario -eq 'create-failed') { $global:LASTEXITCODE = 1 }
        if ($verb -eq 'delete' -and $global:PocketQuestsToolingScenario -eq 'delete-failed') { $global:LASTEXITCODE = 1 }
    }
    Mock sqlcmd {
        $global:PocketQuestsToolingProbe.databaseCleanup++
        ($args -join ' ') | Should -Match '\(localdb\)\\PQExpoGo_[0-9a-f]{12}'
        $global:LASTEXITCODE = [int]($global:PocketQuestsToolingScenario -eq 'database-remains')
    }
    Mock Invoke-WebRequest {
        $requestPath = ([uri]$Uri).AbsolutePath
        $currentToken = 'fixture-session-' + $global:PocketQuestsToolingProbe.generation
        $authorized = $Headers -and $Headers.Authorization -eq "Bearer $currentToken"
        $status = 200
        $payload = @{}
        $cache = if ($global:PocketQuestsToolingScenario -eq 'cacheable') { 'public' } else { 'no-store' }
        if ($requestPath -eq '/development/sign-in') {
            $inputBody = $Body | ConvertFrom-Json
            $expectedPin = $global:PocketQuestsToolingProbe.starts[-1].Environment['DevelopmentSignIn__PairingCode']
            if ($inputBody.pairingCode -ne $expectedPin) {
                $global:PocketQuestsToolingProbe.wrongPins++
                $status = if ($global:PocketQuestsToolingProbe.wrongPins -ge 10 -and $global:PocketQuestsToolingScenario -ne 'no-throttle') { 429 } else { 401 }
            } else { $payload = @{ token = $currentToken } }
        } elseif (-not $authorized) {
            $status = if ($global:PocketQuestsToolingScenario -eq 'anonymous-accepted') { 200 } else { 401 }
        } elseif ($requestPath -eq '/identity/users/me/deletion') {
            $status = 404
        } elseif ($requestPath -eq '/identity/users/me') {
            $payload = @{ userId = if ($global:PocketQuestsToolingScenario -eq 'changed-account' -and $global:PocketQuestsToolingProbe.generation -gt 1) { 'other-user' } else { 'same-user' } }
        } elseif ($requestPath -eq '/api/commands') {
            $command = $Body | ConvertFrom-Json
            $global:PocketQuestsToolingProbe.operations.Add($command.operationId)
            $payload = @{ overallXp = if ($global:PocketQuestsToolingScenario -eq 'no-progress') { 0 } else { 10 }; occurrences = @(@{ occurrence = @{ id = 'same-occurrence' } }) }
        } elseif ($requestPath -eq '/api/state') {
            $payload = @{ overallXp = 10; occurrences = @(@{ occurrence = @{ id = 'same-occurrence' } }) }
        }
        @{ StatusCode = $status; Headers = @{ 'Cache-Control' = @($cache) }; Content = ($payload | ConvertTo-Json -Depth 8) }
    }
}

Context 'Expo Go launcher owns only its child process and temporary environment' {
    It 'configures a hidden development host, verifies the proxy, and restores environment after Expo exits' {
        $saved = @{}
        foreach ($name in @('EXPO_PUBLIC_API_URL','EXPO_PUBLIC_IDENTITY_URL','EXPO_PUBLIC_DEVELOPMENT_SIGN_IN')) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        }
        $location = (Get-Location).Path
        & $script:launcher -BackendUrl 'https://phone.example/' -NoBuild
        $global:PocketQuestsToolingProbe.starts.Count | Should -Be 1
        $global:PocketQuestsToolingProbe.starts[0].Environment['DevelopmentSignIn__SqlInstance'] | Should -Be 'PocketQuests'
        $global:PocketQuestsToolingProbe.starts[0].Environment['DevelopmentSignIn__PairingCode'] | Should -Match '^\d{6}$'
        $global:PocketQuestsToolingProbe.nodeCalls | Should -Be 1
        $global:PocketQuestsToolingProbe.killed | Should -Be 1
        $global:PocketQuestsToolingProbe.disposed | Should -Be 1
        (Get-Location).Path | Should -Be $location
        foreach ($name in $saved.Keys) { [Environment]::GetEnvironmentVariable($name, 'Process') | Should -Be $saved[$name] }
    }
    It 'rejects unsafe proxy URL <url> before starting a process' -ForEach @(
        @{url='http://phone.example/'}, @{url='https://user@phone.example/'},
        @{url='https://phone.example/path'}, @{url='https://phone.example/?secret=x'},
        @{url='https://phone.example/#fragment'}
    ) {
        { & $script:launcher -BackendUrl $url -NoBuild } | Should -Throw '*trusted HTTPS*'
        $global:PocketQuestsToolingProbe.starts.Count | Should -Be 0
    }
    It 'preserves existing listeners and rejects absent builds' -ForEach @(
        @{scenario='occupied'; message='*already in use*'},
        @{scenario='missing-build'; message='*Build the development host*'},
        @{scenario='build-failed'; message='*build failed*'}
    ) {
        $global:PocketQuestsToolingScenario = $scenario
        { & $script:launcher -BackendUrl 'https://phone.example/' } | Should -Throw $message
        $global:PocketQuestsToolingProbe.starts.Count | Should -Be 0
    }
    It 'cleans up its child on <scenario>' -ForEach @(
        @{scenario='wrong-proxy'; message='*does not route to this*'},
        @{scenario='not-ready'; message='*did not become ready*'},
        @{scenario='host-exited'; message='*exited before*'},
        @{scenario='expo-failed'; message='*Expo Go development server*'}
    ) {
        $global:PocketQuestsToolingScenario = $scenario
        { & $script:launcher -BackendUrl 'https://phone.example/' -NoBuild } | Should -Throw $message
        $global:PocketQuestsToolingProbe.disposed | Should -Be 1
        if ($scenario -ne 'host-exited') { $global:PocketQuestsToolingProbe.killed | Should -Be 1 }
    }
}

Context 'Development integration orchestration fails closed and removes only its fixture' {
    It 'checks authentication, persistence, restart, throttling and owned cleanup without leaking credentials in evidence' {
        & $script:integration -NoBuild -EvidenceDirectory $script:resultDirectory
        $global:PocketQuestsToolingProbe.generation | Should -Be 2
        $global:PocketQuestsToolingProbe.operations.Count | Should -Be 2
        @($global:PocketQuestsToolingProbe.operations | Select-Object -Unique).Count | Should -Be 2
        $global:PocketQuestsToolingProbe.databaseCleanup | Should -Be 1
        @($global:PocketQuestsToolingProbe.sql | Select-Object -ExpandProperty instance -Unique).Count | Should -Be 1
        @($global:PocketQuestsToolingProbe.sql | Where-Object verb -eq 'delete').Count | Should -Be 1
        $evidence = Get-Content (Join-Path $script:resultDirectory 'checks.json') -Raw
        @($evidence | ConvertFrom-Json).Count | Should -Be 8
        $evidence | Should -Not -Match 'fixture-session|Bearer|pairingCode'
    }
    It 'fails on <scenario> and still checks disposable cleanup' -ForEach @(
        @{scenario='cacheable'; message='*cacheable*'},
        @{scenario='anonymous-accepted'; message='*Unexpected status*'},
        @{scenario='no-progress'; message='*did not persist*'},
        @{scenario='changed-account'; message='*changed across restart*'},
        @{scenario='no-throttle'; message='*not throttled*'},
        @{scenario='host-exited'; message='*host exited*'},
        @{scenario='not-ready'; message='*readiness timed out*'}
    ) {
        $global:PocketQuestsToolingScenario = $scenario
        { & $script:integration -NoBuild -EvidenceDirectory $script:resultDirectory } | Should -Throw $message
        $global:PocketQuestsToolingProbe.databaseCleanup | Should -Be 1
        @($global:PocketQuestsToolingProbe.sql | Where-Object verb -eq 'delete').Count | Should -Be 1
    }
    It 'does not touch SQL when startup gates or the build fail' -ForEach @(
        @{scenario='occupied'; message='*port is occupied*'},
        @{scenario='build-failed'; message='*build failed*'},
        @{scenario='gate-bypass'; message='*not rejected*'}
    ) {
        $global:PocketQuestsToolingScenario = $scenario
        { & $script:integration -EvidenceDirectory $script:resultDirectory } | Should -Throw $message
        $global:PocketQuestsToolingProbe.sql.Count | Should -Be 0
    }
    It 'cleans up an attempted partial creation without touching database contents' {
        $global:PocketQuestsToolingScenario = 'create-failed'
        { & $script:integration -NoBuild -EvidenceDirectory $script:resultDirectory } | Should -Throw '*creation failed*'
        $global:PocketQuestsToolingProbe.databaseCleanup | Should -Be 0
        @($global:PocketQuestsToolingProbe.sql | Where-Object verb -eq 'delete').Count | Should -Be 1
    }
    It 'retains an instance when database cleanup cannot be verified' {
        $global:PocketQuestsToolingScenario = 'database-remains'
        { & $script:integration -NoBuild -EvidenceDirectory $script:resultDirectory } | Should -Throw '*instance retained*'
        @($global:PocketQuestsToolingProbe.sql | Where-Object verb -eq 'delete').Count | Should -Be 0
    }
    It 'reports failed instance deletion' {
        $global:PocketQuestsToolingScenario = 'delete-failed'
        { & $script:integration -NoBuild -EvidenceDirectory $script:resultDirectory } | Should -Throw '*SQL cleanup failed*'
        @($global:PocketQuestsToolingProbe.sql | Where-Object verb -eq 'delete').Count | Should -Be 1
    }
}
}
