function Export-PesterCoverage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Coverage,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string[]]$SourceFiles,
        [Parameter(Mandatory)][string]$OutputPath
    )
    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $lines = @{}
    foreach ($source in $SourceFiles) { $lines[$source.Replace('\', '/')] = @{} }
    # Keep every measured command, including misses. A line with any executed
    # command has line coverage; no branch coverage is inferred from this data.
    foreach ($group in @(
        @{ Commands = @($Coverage.CommandsMissed); Covered = $false },
        @{ Commands = @($Coverage.CommandsExecuted); Covered = $true }
    )) {
        foreach ($command in $group.Commands) {
            $relative = [IO.Path]::GetRelativePath($root, $command.File).Replace('\', '/')
            if (-not $lines.ContainsKey($relative) -or $command.Line -lt 1) {
                throw 'Coverage contains an unexpected file or invalid line.'
            }
            $lines[$relative][[int]$command.Line] = $group.Covered
        }
    }
    if (@($Coverage.CommandsExecuted).Count -eq 0) { throw 'No executed coverage was measured.' }
    foreach ($source in $lines.Keys) {
        if ($lines[$source].Count -eq 0) { throw "Missing measured coverage for $source." }
    }
    $xml = [xml]'<coverage version="1" />'
    foreach ($source in ($lines.Keys | Sort-Object)) {
        $file = $xml.CreateElement('file')
        $file.SetAttribute('path', $source)
        foreach ($number in ($lines[$source].Keys | Sort-Object)) {
            $line = $xml.CreateElement('lineToCover')
            $line.SetAttribute('lineNumber', [string]$number)
            $line.SetAttribute('covered', ([bool]$lines[$source][$number]).ToString().ToLowerInvariant())
            $null = $file.AppendChild($line)
        }
        $null = $xml.DocumentElement.AppendChild($file)
    }
    $xml.Save($OutputPath)
}
