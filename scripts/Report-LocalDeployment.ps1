[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 5217
)

$ErrorActionPreference = 'Stop'
$repository = 'HoneyDrunkStudios/HoneyDrunk.PocketQuests'
$environment = 'local-development'
$healthUrl = "http://127.0.0.1:$Port/health"

function Get-ServingProcess {
    $connection = [Net.Sockets.TcpClient]::new([Net.Sockets.AddressFamily]::InterNetwork)
    try {
        $connection.Connect('127.0.0.1', $Port)
        $clientPort = $connection.Client.LocalEndPoint.Port
        $server = [regex]::Escape("127.0.0.1:$Port")
        $client = [regex]::Escape("127.0.0.1:$clientPort")
        $ids = @(netstat.exe -ano -p tcp | ForEach-Object {
            if ($_ -match "^\s*TCP\s+$server\s+$client\s+ESTABLISHED\s+(?<owner>[1-9][0-9]*)\s*$") {
                [int]$Matches.owner
            }
        } | Sort-Object -Unique)
    } finally {
        $connection.Dispose()
    }

    if ($ids.Count -ne 1) {
        throw "Could not identify the process serving 127.0.0.1:$Port."
    }

    $process = Get-Process -Id $ids[0]
    if (-not $process.Path -or [IO.Path]::GetFileName($process.Path) -ne 'PocketQuests.Api.exe') {
        throw 'The loopback listener must be the published PocketQuests.Api apphost.'
    }

    return $process
}

function Confirm-ServingProcess($expectedProcess, [datetime]$expectedStart, [string]$assemblyPath) {
    $health = Invoke-WebRequest -Uri $healthUrl -NoProxy -MaximumRedirection 0 -TimeoutSec 15
    if ($health.StatusCode -ne 200 -or $health.Content.Trim() -ne 'Healthy') {
        throw 'The Pocket Quests readiness probe did not report Healthy.'
    }

    $current = Get-ServingProcess
    if ($current.Id -ne $expectedProcess.Id -or $current.StartTime.ToUniversalTime() -ne $expectedStart -or
        (Get-Item -LiteralPath $assemblyPath).LastWriteTimeUtc -gt $expectedStart) {
        throw 'The serving Pocket Quests build changed during verification.'
    }
}

$serving = Get-ServingProcess
$startedAt = $serving.StartTime.ToUniversalTime()
$assembly = Join-Path ([IO.Path]::GetDirectoryName($serving.Path)) 'PocketQuests.Api.dll'
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly)
if ($version.ProductName -ne 'PocketQuests.Api' -or $version.ProductVersion -notmatch '\+(?:[a-z0-9.-]+\.)?(?<commit>[a-f0-9]{40})$') {
    throw 'The running Pocket Quests assembly does not contain a full source commit.'
}

$commit = $Matches.commit.ToLowerInvariant()
if ((Get-Item -LiteralPath $assembly).LastWriteTimeUtc -gt $startedAt) {
    throw 'The on-disk assembly changed after the serving process started.'
}

Confirm-ServingProcess $serving $startedAt $assembly

if (-not $PSCmdlet.ShouldProcess("$repository/$environment", "Report serving commit $commit")) {
    return
}

$lock = [Threading.Mutex]::new($false, 'Local\HoneyDrunk.PocketQuests.ReportLocalDeployment')
$ownsLock = $false
try {
    try {
        $ownsLock = $lock.WaitOne(0)
    } catch [Threading.AbandonedMutexException] {
        $ownsLock = $true
    }
    if (-not $ownsLock) {
        throw 'Another local deployment report is in progress.'
    }

    $api = "repos/$repository"
    $reportedCommit = gh api "$api/commits/$commit" --jq .sha
    if ($LASTEXITCODE -ne 0 -or $reportedCommit -ne $commit) {
        throw 'GitHub could not verify the serving commit in the Pocket Quests repository.'
    }

    $identity = [Text.Encoding]::UTF8.GetBytes("$([Environment]::MachineName):$($startedAt.Ticks):$($serving.Id):$commit")
    $reportKey = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($identity))
    # Slurp keeps all paginated arrays in one JSON document. Do not infer absence
    # from the newest page, including when recovering a deployment without a status.
    $pages = gh api "$api/deployments?environment=$environment&task=deploy&per_page=100" --paginate --slurp | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the local deployment records.' }
    $previous = @($pages | ForEach-Object { $_ } | Where-Object { $_.payload.reportKey -eq $reportKey })
    if ($previous.Count -gt 1 -or ($previous.Count -eq 1 -and $previous[0].sha -ne $commit)) {
        throw 'Conflicting GitHub records exist for the running process.'
    }

    if ($previous.Count -eq 1) {
        $deployment = $previous[0]
        $status = gh api "$api/deployments/$($deployment.id)/statuses?per_page=1" --jq '.[0].state // empty'
        if ($LASTEXITCODE -ne 0) { throw 'Could not verify the existing local deployment status.' }
        if ($status -eq 'success') {
            Write-Output "GitHub already confirms $commit for $environment (deployment $($deployment.id))."
            return
        }
        if ($status) { throw 'The existing deployment is no longer eligible for confirmation.' }
    } else {
        Confirm-ServingProcess $serving $startedAt $assembly
        $deployment = @{
            ref = $commit
            task = 'deploy'
            environment = $environment
            auto_merge = $false
            required_contexts = @()
            transient_environment = $false
            production_environment = $false
            payload = @{ reportKey = $reportKey }
            description = 'Verified Windows loopback Pocket Quests API'
        } | ConvertTo-Json -Compress | gh api --method POST "$api/deployments" --input - | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $deployment.id -or $deployment.sha -ne $commit) {
            throw 'GitHub did not confirm creation of the exact local deployment record. Check before retrying.'
        }
    }

    Confirm-ServingProcess $serving $startedAt $assembly
    $confirmation = @{
        state = 'success'
        environment = $environment
        auto_inactive = $true
        description = 'Loopback readiness and the serving assembly revision verified'
    } | ConvertTo-Json -Compress | gh api --method POST "$api/deployments/$($deployment.id)/statuses" --input - --jq '{id,state,environment,created_at}' | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $confirmation.state -ne 'success' -or $confirmation.environment -ne $environment) {
        throw 'GitHub did not confirm the local deployment status. Check before retrying.'
    }
    $confirmation
} finally {
    if ($ownsLock) {
        $lock.ReleaseMutex()
    }
    $lock.Dispose()
}
