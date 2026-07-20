$runnerPath = Join-Path $PSScriptRoot 'Invoke-A4BackendIntegration.ps1'

Describe 'Invoke-A4BackendIntegration runner ownership' {
    BeforeAll {
        $runner = Get-Content -Raw -LiteralPath $runnerPath
    }

    It 'captures, reports, waits for, and cleans up every owned process' {
        $runner | Should Match '\$runId\s*=\s*\[Guid\]::NewGuid\(\)\.ToString\(''N''\)'
        $runner | Should Match '"a4-integration-server-\$runId\.exe"'
        $runner | Should Match '"a4-integration-devprobe-\$runId\.exe"'
        $runner | Should Not Match "Join-Path \$backendLogs 'a4-integration-(server|devprobe)\.exe'"
        $runner | Should Match 'Write-Host "BACKEND_PID=\$\(\$serverProcess\.Id\)"'
        $runner | Should Match '\$probeProcess\s*=\s*Start-Process'
        $runner | Should Match 'Write-Host "DEVPROBE_PID=\$\(\$probeProcess\.Id\)"'
        $runner | Should Match '-PassThru\s*`\s*\n\s*-Wait'
        $runner | Should Match 'Write-Host "UNITY_PID=\$\(\$unityProcess\.Id\)"'
        $runner | Should Match 'Stop-Process -Id \$unityProcess\.Id -Force'
        $runner | Should Match 'Stop-Process -Id \$serverProcess\.Id -Force'
        $runner | Should Match 'Stop-Process -Id \$probeProcess\.Id -Force'
        $runner | Should Match 'SetEnvironmentVariable\(\s*''GAME_BACKEND_INTEGRATION'',\s*\$originalIntegrationEnvironment'
        $runner | Should Match 'Get-IntegrationPortListeners'
        $runner | Should Match 'Remove-Item -LiteralPath \$temporaryExecutable -Force'
    }

    It 'runs the complete three-test Unity fixture after the complete devprobe contract' {
        $runner | Should Match ([regex]::Escape(
            'development session probe passed: protobuf login found=false typed save typed reload combat duplicate'))
        $runner | Should Match "'-testFilter',\s*'Game\.Tests\.PlayMode\.RealBackendOnlineFlowTests'"
        $runner | Should Match '\$total -ne 3 -or \$passed -ne 3 -or \$failed -ne 0 -or \$skipped -ne 0'
        $runner | Should Not Match 'dataLen'
    }

    It 'requires all Unity completion markers exactly once' {
        foreach ($marker in @(
            '[REAL_BACKEND] ARCHIVE_ROUND_TRIP_OK',
            '[REAL_BACKEND] VICTORY_PERSISTENCE_OK',
            '[REAL_BACKEND] DEFEAT_SETTLEMENT_OK')) {
            $runner | Should Match ([regex]::Escape($marker))
        }

        $runner | Should Match '\$archiveRoundTripEvidence -ne 1'
        $runner | Should Match '\$victoryPersistenceEvidence -ne 1'
        $runner | Should Match '\$defeatSettlementEvidence -ne 1'
    }

    It 'requires backend login evidence for all fixed test identities while the server remains alive' {
        foreach ($identity in @(
            'integration-client',
            'integration-battle-victory',
            'integration-battle-defeat')) {
            $runner | Should Match ([regex]::Escape($identity))
        }

        $runner | Should Match '\$serverProcess\.Refresh\(\)\s*\r?\n\s*if \(\$serverProcess\.HasExited\)'
    }
}
