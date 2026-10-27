BeforeAll { . (Join-Path $PSScriptRoot 'Export-PesterCoverage.ps1') }

Describe 'Measured PowerShell coverage export' {
    BeforeEach {
        $source = Join-Path $TestDrive 'source.ps1'
        Set-Content $source @('Write-Output first', 'Write-Output second', 'throw missed')
        $output = Join-Path $TestDrive 'coverage.xml'
        $coverage = @{
            CommandsExecuted = @(@{File=$source; Line=1}, @{File=$source; Line=2})
            CommandsMissed = @(@{File=$source; Line=2}, @{File=$source; Line=3})
        }
    }
    It 'maps repository paths and preserves missed lines without inventing branch data' {
        Export-PesterCoverage $coverage $TestDrive @('source.ps1') $output
        $report = [xml](Get-Content $output -Raw)
        $report.coverage.version | Should -Be '1'
        $report.coverage.file.path | Should -Be 'source.ps1'
        @($report.coverage.file.lineToCover).Count | Should -Be 3
        $report.coverage.file.lineToCover[1].covered | Should -Be 'true'
        $report.coverage.file.lineToCover[2].covered | Should -Be 'false'
        $report.SelectNodes('//@branchesToCover').Count | Should -Be 0
    }
    It 'rejects unmeasured coverage' {
        $coverage.CommandsExecuted = @()
        { Export-PesterCoverage $coverage $TestDrive @('source.ps1') $output } | Should -Throw '*No executed*'
    }
    It 'rejects missing files instead of silently omitting them' {
        { Export-PesterCoverage $coverage $TestDrive @('source.ps1', 'missing.ps1') $output } | Should -Throw '*Missing measured*'
    }
    It 'rejects foreign paths and invalid line numbers' -ForEach @(
        @{file='../outside.ps1'; line=1}, @{file='source.ps1'; line=0}
    ) {
        $coverage.CommandsMissed = @(@{File=(Join-Path $TestDrive $file); Line=$line})
        { Export-PesterCoverage $coverage $TestDrive @('source.ps1') $output } | Should -Throw '*unexpected file or invalid line*'
    }
}
