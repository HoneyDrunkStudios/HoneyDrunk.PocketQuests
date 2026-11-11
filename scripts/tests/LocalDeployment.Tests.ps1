Describe 'Verified local deployment reporting' {
    BeforeAll {
        $script:reporter = Join-Path (Split-Path $PSScriptRoot -Parent) 'Report-LocalDeployment.ps1'
        $script:originalProbe = $global:PocketQuestsDeploymentProbe
        $script:commit = '1234567890abcdef1234567890abcdef12345678'
        $script:fixtures = @{}
        Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
        foreach ($kind in @('valid', 'wrong-product', 'missing-commit')) {
            $directory = Join-Path $TestDrive $kind
            $null = New-Item -ItemType Directory -Path $directory
            $assembly = Join-Path $directory 'PocketQuests.Api.dll'
            $product = if ($kind -eq 'wrong-product') { 'Another.Api' } else { 'PocketQuests.Api' }
            $version = if ($kind -eq 'missing-commit') { '1.0.0' } else { "1.0.0+$script:commit" }
            $type = 'Fixture' + [guid]::NewGuid().ToString('N')
            $source = @"
using System.Reflection;
[assembly: AssemblyProduct("$product")]
[assembly: AssemblyInformationalVersion("$version")]
public class $type { }
"@
            # Windows FileVersionInfo reads VERSIONINFO, not managed attributes.
            # Add-Type alone does not emit that resource for its output assembly.
            $syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source)
            $reference = [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile([object].Assembly.Location)
            $options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
            $compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
                $type, [Microsoft.CodeAnalysis.SyntaxTree[]]@($syntax), [Microsoft.CodeAnalysis.MetadataReference[]]@($reference), $options)
            $resources = $compilation.CreateDefaultWin32Resources($true, $true, $null, $null)
            $output = [IO.File]::Create($assembly)
            try {
                $result = $compilation.Emit($output, $null, $null, $resources, $null, $null, [Threading.CancellationToken]::None)
                if (-not $result.Success) { throw ($result.Diagnostics -join [Environment]::NewLine) }
            } finally {
                $output.Dispose()
                $resources.Dispose()
            }
            $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly)
            $metadata.ProductName | Should -Be $product
            $metadata.ProductVersion | Should -Be $version
            $script:fixtures[$kind] = Join-Path $directory 'PocketQuests.Api.exe'
        }

        # Native commands are replaced with closed, in-memory fixtures. No GitHub
        # credentials, real process discovery or real deployment writes are used.
        function netstat.exe {
            $probe = $global:PocketQuestsDeploymentProbe
            $client = $probe.listener.AcceptTcpClient()
            try {
                $probe.discoveries++
                $port = $client.Client.RemoteEndPoint.Port
                if ($probe.scenario -eq 'missing-owner') { return }
                "  TCP  127.0.0.1:$($probe.port)  127.0.0.1:$port  ESTABLISHED  1234"
                if ($probe.scenario -eq 'multiple-owners') {
                    "  TCP  127.0.0.1:$($probe.port)  127.0.0.1:$port  ESTABLISHED  5678"
                }
            } finally { $client.Dispose() }
        }
        function gh {
            [CmdletBinding(PositionalBinding = $false)]
            param(
                [Parameter(ValueFromRemainingArguments)][string[]]$Arguments,
                [Parameter(ValueFromPipeline)][string]$Json
            )
            process {
                $probe = $global:PocketQuestsDeploymentProbe
                $probe.calls.Add(@{ arguments = $Arguments; json = $Json })
                $global:LASTEXITCODE = 0
                $Arguments[0] | Should -Be 'api'
                $endpoint = @($Arguments | Where-Object { $_ -like 'repos/*' })
                $endpoint.Count | Should -Be 1
                $endpoint = $endpoint[0]
                $api = 'repos/HoneyDrunkStudios/HoneyDrunk.PocketQuests'
                if ($endpoint -eq "$api/commits/$($probe.commit)") {
                    if ($probe.scenario -eq 'commit-read-failed') { $global:LASTEXITCODE = 1; return }
                    if ($probe.scenario -eq 'wrong-commit') { return ('f' * 40) }
                    return $probe.commit
                }
                if ($endpoint.StartsWith("$api/deployments?")) {
                    $endpoint | Should -Match '\?environment=local-development&task=deploy&per_page=(30|100)$'
                    $pageSize = [int]([regex]::Match($endpoint, 'per_page=(\d+)').Groups[1].Value)
                    $pages = [Collections.Generic.List[object]]::new()
                    for ($offset = 0; $offset -lt $probe.deployments.Count; $offset += $pageSize) {
                        $pages.Add(@($probe.deployments | Select-Object -Skip $offset -First $pageSize))
                        if ($Arguments -notcontains '--paginate') { break }
                    }
                    if ($pages.Count -eq 0) { $pages.Add(@()) }
                    if ($probe.scenario -eq 'later-page-failed') { $global:LASTEXITCODE = 1 }
                    if ($probe.scenario -eq 'malformed-page') { return '{not-json' }
                    if ($Arguments -contains '--slurp') {
                        return ConvertTo-Json -InputObject $pages.ToArray() -Depth 8 -Compress
                    }
                    foreach ($page in $pages) { ConvertTo-Json -InputObject $page -Depth 8 -Compress }
                    return
                }
                if ($endpoint -match "^$api/deployments/(?<id>\d+)/statuses\?per_page=1$") {
                    if ($probe.scenario -eq 'status-read-failed') { $global:LASTEXITCODE = 1; return }
                    return $probe.statuses[[int]$Matches.id]
                }
                $Arguments | Should -Contain '--method'
                $Arguments | Should -Contain 'POST'
                $Arguments | Should -Contain '--input'
                $Arguments | Should -Contain '-'
                $body = $Json | ConvertFrom-Json
                if ($endpoint -eq "$api/deployments") {
                    $probe.creations++
                    $body.ref | Should -Be $probe.commit
                    $body.environment | Should -Be 'local-development'
                    $body.task | Should -Be 'deploy'
                    $body.auto_merge | Should -BeFalse
                    $body.required_contexts.Count | Should -Be 0
                    $body.transient_environment | Should -BeFalse
                    $body.production_environment | Should -BeFalse
                    $body.payload.reportKey | Should -Be $probe.key
                    if ($probe.scenario -eq 'creation-failed') { $global:LASTEXITCODE = 1; return }
                    $created = @{ id = 999; sha = $probe.commit; payload = @{ reportKey = $probe.key } }
                    $probe.deployments.Add($created)
                    if ($probe.scenario -eq 'creation-wrong-sha') { $created.sha = 'f' * 40 }
                    if ($probe.scenario -eq 'creation-missing-id') { $created.Remove('id') }
                    return ConvertTo-Json -InputObject $created -Compress
                }
                if ($endpoint -match "^$api/deployments/(?<id>\d+)/statuses$") {
                    $id = [int]$Matches.id
                    $probe.confirmations.Add($id)
                    $body.state | Should -Be 'success'
                    $body.environment | Should -Be 'local-development'
                    $body.auto_inactive | Should -BeTrue
                    if ($probe.scenario -eq 'confirmation-failed') { $global:LASTEXITCODE = 1; return }
                    $probe.statuses[$id] = 'success'
                    $state = if ($probe.scenario -eq 'confirmation-wrong-state') { 'pending' } else { 'success' }
                    $environment = if ($probe.scenario -eq 'confirmation-wrong-environment') { 'production' } else { 'local-development' }
                    return @{ state = $state; environment = $environment; id = 987 } | ConvertTo-Json -Compress
                }
                throw "Unexpected mocked GitHub endpoint: $endpoint"
            }
        }
        function Add-OlderDeployment([int]$NewerCount = 100, [string]$Status = 'success') {
            $probe = $global:PocketQuestsDeploymentProbe
            for ($index = 0; $index -lt $NewerCount; $index++) {
                $probe.deployments.Add(@{ id = $index + 10; sha = $probe.commit; payload = @{ reportKey = "another-process-$index" } })
            }
            $probe.deployments.Add(@{ id = 1; sha = $probe.commit; payload = @{ reportKey = $probe.key } })
            $probe.statuses[1] = $Status
        }
    }
    AfterAll { $global:PocketQuestsDeploymentProbe = $script:originalProbe }

    BeforeEach {
        # Only the ephemeral fixture listener is contacted. Process, health and
        # GitHub responses remain mocked; the executable fixture is never run.
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $started = [datetime]::UtcNow.AddMinutes(1)
        $identity = [Text.Encoding]::UTF8.GetBytes("$([Environment]::MachineName):$($started.Ticks):1234:$script:commit")
        $global:PocketQuestsDeploymentProbe = @{
            listener = $listener; port = $listener.LocalEndpoint.Port
            scenario = ''; discoveries = 0; healthChecks = 0; creations = 0
            path = $script:fixtures.valid; started = $started; commit = $script:commit
            key = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($identity))
            calls = [Collections.Generic.List[object]]::new()
            deployments = [Collections.Generic.List[object]]::new()
            confirmations = [Collections.Generic.List[int]]::new()
            statuses = @{}
        }
        Mock Get-Process {
            $probe = $global:PocketQuestsDeploymentProbe
            $Id | Should -Be 1234
            $owner = if ($probe.scenario -eq 'changed-process' -and $probe.discoveries -gt 1) { 9876 } else { 1234 }
            $start = if ($probe.scenario -eq 'changed-start' -and $probe.discoveries -gt 1) { $probe.started.AddSeconds(1) } else { $probe.started }
            @{ Id = $owner; StartTime = $start; Path = $probe.path }
        }
        Mock Invoke-WebRequest {
            $probe = $global:PocketQuestsDeploymentProbe
            $Uri | Should -Be "http://127.0.0.1:$($probe.port)/health"
            $NoProxy | Should -BeTrue
            $MaximumRedirection | Should -Be 0
            $ConnectionTimeoutSeconds | Should -Be 15
            $probe.healthChecks++
            if ($probe.scenario -eq 'health-error') { throw 'Fixture health request failed.' }
            $code = if ($probe.scenario -eq 'health-status') { 503 } else { 200 }
            $body = if ($probe.scenario -eq 'unhealthy') { 'Unhealthy' } else { 'Healthy' }
            if ($probe.scenario -eq 'changed-before-confirmation' -and $probe.healthChecks -gt 2) { $body = 'Unhealthy' }
            @{ StatusCode = $code; Content = $body }
        }
        Mock Get-Item {
            $probe = $global:PocketQuestsDeploymentProbe
            $changed = $probe.scenario -eq 'assembly-updated' -or ($probe.scenario -eq 'assembly-changed-during-verification' -and $probe.discoveries -gt 1)
            @{ LastWriteTimeUtc = if ($changed) { $probe.started.AddSeconds(1) } else { $probe.started.AddSeconds(-1) } }
        } -ParameterFilter { $LiteralPath -like '*PocketQuests.Api.dll' }
    }
    AfterEach { $global:PocketQuestsDeploymentProbe.listener.Stop() }

    It 'creates and confirms the exact verified build, then reuses it on retry' {
        $probe = $global:PocketQuestsDeploymentProbe
        (& $script:reporter -Port $probe.port).state | Should -Be 'success'
        & $script:reporter -Port $probe.port | Should -Match 'already confirms'
        $probe.creations | Should -Be 1
        $probe.confirmations.Count | Should -Be 1
    }
    It 'finds an existing successful deployment beyond the first page without writes' {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment
        & $script:reporter -Port $probe.port | Should -Match 'deployment 1'
        & $script:reporter -Port $probe.port | Should -Match 'deployment 1'
        $probe.creations | Should -Be 0
        $probe.confirmations.Count | Should -Be 0
    }
    It 'recovers a status-less deployment beyond the first page and is idempotent' {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment -Status ''
        (& $script:reporter -Port $probe.port).state | Should -Be 'success'
        & $script:reporter -Port $probe.port | Should -Match 'deployment 1'
        $probe.creations | Should -Be 0
        $probe.confirmations.ToArray() | Should -Be @(1)
    }
    It 'recovers after an uncertain status write without creating another deployment' {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.scenario = 'confirmation-failed'
        { & $script:reporter -Port $probe.port } | Should -Throw '*did not confirm the local deployment status*'
        $probe.scenario = ''
        (& $script:reporter -Port $probe.port).state | Should -Be 'success'
        $probe.creations | Should -Be 1
        $probe.confirmations.ToArray() | Should -Be @(999, 999)
    }
    It 'detects conflicting duplicate records on different pages before writing' {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment
        $probe.deployments[0].payload.reportKey = $probe.key
        { & $script:reporter -Port $probe.port } | Should -Throw '*Conflicting GitHub records*'
        $probe.creations | Should -Be 0
        $probe.confirmations.Count | Should -Be 0
    }
    It 'rejects a matching report key for a different commit on a later page' {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment
        $probe.deployments[-1].sha = 'f' * 40
        { & $script:reporter -Port $probe.port } | Should -Throw '*Conflicting GitHub records*'
        $probe.creations | Should -Be 0
        $probe.confirmations.Count | Should -Be 0
    }
    It 'does not write if a later page fails or is malformed: <scenario>' -ForEach @(
        @{ scenario = 'later-page-failed'; message = '*Could not read the local deployment records*' },
        @{ scenario = 'malformed-page'; message = '*Conversion from JSON failed*' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment
        $probe.scenario = $scenario
        { & $script:reporter -Port $probe.port } | Should -Throw $message
        $probe.creations | Should -Be 0
        $probe.confirmations.Count | Should -Be 0
    }
    It 'does not reconfirm an existing deployment with status <status>' -ForEach @(
        @{ status = 'failure' }, @{ status = 'inactive' }, @{ status = 'pending' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment -Status $status
        { & $script:reporter -Port $probe.port } | Should -Throw '*no longer eligible*'
        $probe.creations | Should -Be 0
        $probe.confirmations.Count | Should -Be 0
    }
    It 'checks the local build in WhatIf mode without making any GitHub calls' {
        $probe = $global:PocketQuestsDeploymentProbe
        & $script:reporter -Port $probe.port -WhatIf
        $probe.healthChecks | Should -Be 1
        $probe.calls.Count | Should -Be 0
    }
    It 'rejects local verification failure before GitHub: <scenario>' -ForEach @(
        @{ scenario = 'missing-owner'; message = '*Could not identify the process*' },
        @{ scenario = 'multiple-owners'; message = '*Could not identify the process*' },
        @{ scenario = 'health-status'; message = '*did not report Healthy*' },
        @{ scenario = 'unhealthy'; message = '*did not report Healthy*' },
        @{ scenario = 'health-error'; message = '*Fixture health request failed*' },
        @{ scenario = 'changed-process'; message = '*build changed during verification*' },
        @{ scenario = 'changed-start'; message = '*build changed during verification*' },
        @{ scenario = 'assembly-updated'; message = '*assembly changed after*' },
        @{ scenario = 'assembly-changed-during-verification'; message = '*build changed during verification*' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.scenario = $scenario
        { & $script:reporter -Port $probe.port } | Should -Throw $message
        $probe.calls.Count | Should -Be 0
    }
    It 'rejects the wrong executable' {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.path = '/another/Unrelated.Api.exe'
        { & $script:reporter -Port $probe.port } | Should -Throw '*must be the published PocketQuests.Api apphost*'
        $probe.calls.Count | Should -Be 0
    }
    It 'rejects an assembly with <kind>' -ForEach @(
        @{ kind = 'wrong-product' }, @{ kind = 'missing-commit' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.path = $script:fixtures[$kind]
        { & $script:reporter -Port $probe.port } | Should -Throw '*does not contain a full source commit*'
        $probe.calls.Count | Should -Be 0
    }
    It 'fails closed on GitHub verification: <scenario>' -ForEach @(
        @{ scenario = 'commit-read-failed'; message = '*could not verify the serving commit*' },
        @{ scenario = 'wrong-commit'; message = '*could not verify the serving commit*' },
        @{ scenario = 'status-read-failed'; message = '*Could not verify the existing local deployment status*' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        Add-OlderDeployment
        $probe.scenario = $scenario
        { & $script:reporter -Port $probe.port } | Should -Throw $message
        $probe.creations | Should -Be 0
        $probe.confirmations.Count | Should -Be 0
    }
    It 'does not confirm an unverified creation: <scenario>' -ForEach @(
        @{ scenario = 'creation-failed' }, @{ scenario = 'creation-wrong-sha' }, @{ scenario = 'creation-missing-id' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.scenario = $scenario
        { & $script:reporter -Port $probe.port } | Should -Throw '*did not confirm creation of the exact local deployment record*'
        $probe.confirmations.Count | Should -Be 0
    }
    It 'rejects an unexpected status response: <scenario>' -ForEach @(
        @{ scenario = 'confirmation-wrong-state' }, @{ scenario = 'confirmation-wrong-environment' }
    ) {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.scenario = $scenario
        { & $script:reporter -Port $probe.port } | Should -Throw '*did not confirm the local deployment status*'
    }
    It 'rechecks health immediately before confirming a newly created deployment' {
        $probe = $global:PocketQuestsDeploymentProbe
        $probe.scenario = 'changed-before-confirmation'
        { & $script:reporter -Port $probe.port } | Should -Throw '*did not report Healthy*'
        $probe.creations | Should -Be 1
        $probe.confirmations.Count | Should -Be 0
    }
}
