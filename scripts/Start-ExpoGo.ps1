[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$BackendUrl,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $BackendUrl.IsAbsoluteUri -or $BackendUrl.Scheme -ne 'https' -or
    $BackendUrl.UserInfo -or $BackendUrl.Query -or $BackendUrl.Fragment -or $BackendUrl.AbsolutePath -ne '/') {
    throw 'BackendUrl must be the root of a trusted HTTPS reverse proxy to http://127.0.0.1:15219.'
}
foreach ($port in @(8081, 15219)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use. Stop its owning AppHost/session explicitly before using this launcher."
    }
}
$project = Join-Path $repo 'tools/PocketQuests.DevelopmentHost/PocketQuests.DevelopmentHost.csproj'
if (-not $NoBuild) {
    & dotnet build $project -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Development host build failed.' }
}
$dll = Join-Path $repo 'tools/PocketQuests.DevelopmentHost/bin/Release/net10.0/PocketQuests.DevelopmentHost.dll'
$cli = Join-Path $repo 'apps/mobile/node_modules/expo/bin/cli'
if (-not (Test-Path -LiteralPath $dll) -or -not (Test-Path -LiteralPath $cli)) {
    throw 'Build the development host and run npm ci in apps/mobile first.'
}
$pairingCode = [Security.Cryptography.RandomNumberGenerator]::GetInt32(1000000).ToString('D6', [Globalization.CultureInfo]::InvariantCulture)
$start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
$start.ArgumentList.Add($dll)
$start.WorkingDirectory = $repo
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
$start.Environment['DevelopmentSignIn__Enabled'] = 'true'
$start.Environment['DevelopmentSignIn__PairingCode'] = $pairingCode
$start.Environment['DevelopmentSignIn__SqlInstance'] = 'PocketQuests'
$hostProcess = $null
$variables = @('EXPO_PUBLIC_API_URL', 'EXPO_PUBLIC_IDENTITY_URL', 'EXPO_PUBLIC_DEVELOPMENT_SIGN_IN')
$saved = @{}
foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $hostProcess = [Diagnostics.Process]::Start($start)
    $ready = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if ($hostProcess.HasExited) { throw 'Development host exited before it became ready.' }
        try {
            $health = Invoke-RestMethod 'http://127.0.0.1:15219/health' -TimeoutSec 2
            if ($health.mode -eq 'development') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'Development host did not become ready.' }
    # This checks phone-facing TLS/routing too; a Metro tunnel alone is insufficient.
    $publicHealth = Invoke-RestMethod ($BackendUrl.AbsoluteUri.TrimEnd('/') + '/health') -TimeoutSec 15
    if ($publicHealth.mode -ne 'development' -or $publicHealth.instanceId -ne $health.instanceId) { throw 'BackendUrl does not route to this development host.' }
    $env:EXPO_PUBLIC_API_URL = $BackendUrl.AbsoluteUri.TrimEnd('/')
    $env:EXPO_PUBLIC_IDENTITY_URL = $env:EXPO_PUBLIC_API_URL + '/identity'
    $env:EXPO_PUBLIC_DEVELOPMENT_SIGN_IN = '1'
    Write-Host "Development pairing code: $pairingCode"
    Write-Host 'Scan the Expo Go QR code below, then enter this pairing code. Keep this terminal open.'
    Push-Location (Join-Path $repo 'apps/mobile')
    try {
        & node $cli start --go --lan --clear --port 8081
        if ($LASTEXITCODE -ne 0) { throw 'Expo Go development server exited with an error.' }
    }
    finally { Pop-Location }
} finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    if ($hostProcess -and -not $hostProcess.HasExited) { $hostProcess.Kill($true); $hostProcess.WaitForExit() }
    if ($hostProcess) { $hostProcess.Dispose() }
}
