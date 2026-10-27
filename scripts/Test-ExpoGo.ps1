[CmdletBinding()]
param([switch]$NoBuild, [string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'development-process.psm1')
$PSNativeCommandUseErrorActionPreference = $false
$repo = Split-Path $PSScriptRoot -Parent
if (-not $EvidenceDirectory) { $EvidenceDirectory = Join-Path $repo ('artifacts/expo-go-' + [guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$EvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
if (Get-NetTCPConnection -LocalPort 15219 -State Listen -ErrorAction SilentlyContinue) { throw 'Development host port is occupied; no existing process was stopped.' }
if (-not $NoBuild) {
    & dotnet build (Join-Path $repo 'tools/PocketQuests.DevelopmentHost/PocketQuests.DevelopmentHost.csproj') -c Release --nologo *> (Join-Path $EvidenceDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Development host build failed.' }
}
$dll = Join-Path $repo 'tools/PocketQuests.DevelopmentHost/bin/Release/net10.0/PocketQuests.DevelopmentHost.dll'
$instance = 'PQExpoGo_' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$pairingCode = [Security.Cryptography.RandomNumberGenerator]::GetInt32(1000000).ToString('D6', [Globalization.CultureInfo]::InvariantCulture)
$baseUrl = 'http://127.0.0.1:15219'
$created = $false
$creationAttempted = $false
$process = $null
$checks = [Collections.Generic.List[string]]::new()
function Start-HostProcess([string]$environment, [string]$enabled, [string]$name, [string]$code = $pairingCode) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.ArgumentList.Add($dll)
    $start.WorkingDirectory = $repo
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = $environment
    $start.Environment['DevelopmentSignIn__Enabled'] = $enabled
    $start.Environment['DevelopmentSignIn__PairingCode'] = $code
    $start.Environment['DevelopmentSignIn__SqlInstance'] = $instance
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $child = Start-DevelopmentProcess -StartInfo $start
    # Drain both streams asynchronously so a verbose startup cannot deadlock.
    $script:outRead = $child.StandardOutput.ReadToEndAsync()
    $script:errRead = $child.StandardError.ReadToEndAsync()
    $script:logName = $name
    return $child
}
function Stop-HostProcess {
    if ($script:process) {
        if (-not $script:process.HasExited) { $script:process.Kill($true); $script:process.WaitForExit() }
        $script:outRead.GetAwaiter().GetResult() | Set-Content (Join-Path $EvidenceDirectory ($script:logName + '.log'))
        $script:errRead.GetAwaiter().GetResult() | Set-Content (Join-Path $EvidenceDirectory ($script:logName + '.error.log'))
        $script:process.Dispose()
        $script:process = $null
    }
}
function Wait-HostReady {
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if ($script:process.HasExited) { throw 'Development host exited; inspect host error log.' }
        try { if ((Invoke-RestMethod "$baseUrl/health" -TimeoutSec 1).mode -eq 'development') { return } } catch { Start-Sleep -Milliseconds 500 }
    }
    throw 'Development host readiness timed out.'
}
function Assert-Status([string]$path, [int]$status, [string]$token = '', [object]$body = $null) {
    $arguments = @{ Uri=$baseUrl + $path; SkipHttpErrorCheck=$true; TimeoutSec=20 }
    if ($token) { $arguments.Headers = @{ Authorization="Bearer $token" } }
    if ($null -ne $body) { $arguments.Method='POST'; $arguments.ContentType='application/json'; $arguments.Body=ConvertTo-Json $body -Depth 8 }
    $response = Invoke-WebRequest @arguments
    if ([int]$response.StatusCode -ne $status) { throw "Unexpected status for $path : $($response.StatusCode), expected $status." }
    return $response
}
try {
    foreach ($configuration in @(@('Production', 'true'), @('Development', 'false'))) {
        $process = Start-HostProcess $configuration[0] $configuration[1] ('gate-' + $configuration[0])
        if (-not $process.WaitForExit(15000) -or $process.ExitCode -eq 0) { throw 'Disabled/production startup was not rejected.' }
        Stop-HostProcess
        if ((Get-Content -LiteralPath (Join-Path $EvidenceDirectory ('gate-' + $configuration[0] + '.error.log')) -Raw) -notmatch 'requires Development and explicit') { throw 'Startup failed for an unexpected reason.' }
    }
    $checks.Add('Production and disabled startup rejected before SQL access')
    foreach ($invalidPin in @('12345', '1234567', 'abcdef')) {
        $name = 'invalid-pin-' + $invalidPin
        $process = Start-HostProcess 'Development' 'true' $name $invalidPin
        if (-not $process.WaitForExit(15000) -or $process.ExitCode -eq 0) { throw 'Malformed development PIN was not rejected.' }
        Stop-HostProcess
        if ((Get-Content -LiteralPath (Join-Path $EvidenceDirectory ($name + '.error.log')) -Raw) -notmatch 'exactly six digits') { throw 'PIN validation failed for an unexpected reason.' }
    }
    $checks.Add('Short, long and nonnumeric PIN configurations rejected before SQL access')
    $instances = @(& sqllocaldb info)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate LocalDB instances.' }
    if ($instances -contains $instance) { throw 'Refusing to reuse an existing SQL instance.' }
    $creationAttempted = $true
    & sqllocaldb create $instance -s *> (Join-Path $EvidenceDirectory 'instance-create.log')
    if ($LASTEXITCODE -ne 0) { throw 'Disposable SQL instance creation failed.' }
    $created = $true
    $process = Start-HostProcess 'Development' 'true' 'first-host'
    Wait-HostReady
    $null = Assert-Status '/api/state' 401
    $null = Assert-Status '/identity/users/me' 401
    $null = Assert-Status '/development/sign-in' 401 '' @{ pairingCode='wrong' }
    $checks.Add('Anonymous API/Identity and incorrect pairing code rejected')
    $paired = Assert-Status '/development/sign-in' 200 '' @{ pairingCode=$pairingCode }
    if ($paired.Headers['Cache-Control'] -notcontains 'no-store') { throw 'Credential response was cacheable.' }
    $token = ($paired.Content | ConvertFrom-Json).token
    $null = Assert-Status '/api/state' 401 ($token + 'forged')
    $checks.Add('Forged credentials rejected through real Identity validation')
    $user = ((Assert-Status '/identity/users/me' 200 $token).Content | ConvertFrom-Json).userId
    $null = Assert-Status '/api/profile' 200 $token @{ zone='UTC' }
    $accepted = (Assert-Status '/api/commands' 200 $token @{ operationId=[guid]::NewGuid().ToString(); action='accept'; questId='PQ-CAT-Q01' }).Content | ConvertFrom-Json
    $occurrence = $accepted.occurrences[0].occurrence.id
    $completed = (Assert-Status '/api/commands' 200 $token @{ operationId=[guid]::NewGuid().ToString(); action='complete'; occurrenceId=$occurrence }).Content | ConvertFrom-Json
    if ($completed.overallXp -le 0) { throw 'Completion did not persist rewarded progress.' }
    $checks.Add('Real SQL profile, quest acceptance and completion succeeded')
    Stop-HostProcess
    $process = Start-HostProcess 'Development' 'true' 'second-host'
    Wait-HostReady
    $null = Assert-Status '/api/state' 401 $token
    $token = ((Assert-Status '/development/sign-in' 200 '' @{ pairingCode=$pairingCode }).Content | ConvertFrom-Json).token
    $restoredUser = ((Assert-Status '/identity/users/me' 200 $token).Content | ConvertFrom-Json).userId
    $state = (Assert-Status '/api/state' 200 $token).Content | ConvertFrom-Json
    if ($restoredUser -ne $user -or $state.overallXp -ne $completed.overallXp -or $state.occurrences[0].occurrence.id -ne $occurrence) { throw 'Account/progress changed across restart.' }
    $checks.Add('Backend restart invalidated old token and preserved same SQL account, quest and XP')
    $null = Assert-Status '/identity/users/me/deletion' 404 $token @{ confirmed=$true }
    $checks.Add('Provider lifecycle mutations are not exposed by the development proxy')
    $statuses = foreach ($i in 1..11) { (Invoke-WebRequest "$baseUrl/development/sign-in" -Method Post -ContentType 'application/json' -Body '{"pairingCode":"wrong"}' -SkipHttpErrorCheck).StatusCode }
    if ($statuses -notcontains 429) { throw 'Pairing attempts were not throttled.' }
    $checks.Add('Pairing brute-force budget returns 429')
} finally {
    try { Stop-HostProcess }
    finally {
        if ($creationAttempted -and $instance -match '^PQExpoGo_[0-9a-f]{12}$') {
            $databaseCleanupVerified = -not $created
            if ($created) {
        & sqlcmd -S "(localdb)\$instance" -E -I -b -d master -Q "IF DB_ID(N'PocketQuests_ExpoGo') IS NOT NULL BEGIN ALTER DATABASE [PocketQuests_ExpoGo] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [PocketQuests_ExpoGo]; END; IF DB_ID(N'PocketQuests_ExpoGo_Identity') IS NOT NULL BEGIN ALTER DATABASE [PocketQuests_ExpoGo_Identity] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [PocketQuests_ExpoGo_Identity]; END; IF DB_ID(N'PocketQuests_ExpoGo') IS NOT NULL OR DB_ID(N'PocketQuests_ExpoGo_Identity') IS NOT NULL THROW 51000, 'Development fixture database cleanup failed.', 1;" *> (Join-Path $EvidenceDirectory 'database-cleanup.log')
                $databaseCleanupVerified = $LASTEXITCODE -eq 0
            }
            if (-not $databaseCleanupVerified) { throw 'Disposable database cleanup failed; instance retained for inspection.' }
            & sqllocaldb stop $instance -k *> (Join-Path $EvidenceDirectory 'instance-stop.log')
            & sqllocaldb delete $instance *> (Join-Path $EvidenceDirectory 'instance-delete.log')
            if ($LASTEXITCODE -ne 0) { throw 'Disposable SQL cleanup failed; inspect cleanup evidence.' }
        }
        $checks | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'checks.json')
    }
}
$checks
