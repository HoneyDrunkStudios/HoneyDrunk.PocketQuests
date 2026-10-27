function Start-DevelopmentProcess {
    [CmdletBinding()]
    param([Parameter(Mandatory)][Diagnostics.ProcessStartInfo]$StartInfo)
    # Keep process creation at one boundary; callers own the returned process.
    [Diagnostics.Process]::Start($StartInfo)
}

Export-ModuleMember -Function Start-DevelopmentProcess
