$runnerPath = Join-Path $PSScriptRoot 'Invoke-A4BackendIntegration.ps1'

Describe 'Invoke-A4BackendIntegration runner ownership' {
    It 'captures, reports, waits for, and cleans up the devprobe PID' {
        $runner = Get-Content -Raw -LiteralPath $runnerPath

        $runner | Should Match '\$probeProcess\s*=\s*Start-Process'
        $runner | Should Match 'Write-Host "DEVPROBE_PID=\$\(\$probeProcess\.Id\)"'
        $runner | Should Match '-PassThru\s*`\s*\n\s*-Wait'
        $runner | Should Match 'Stop-Process -Id \$probeProcess\.Id -Force'
        $runner | Should Match 'protobuf login found=false typed save typed reload'
        $runner | Should Not Match 'dataLen'
    }
}
