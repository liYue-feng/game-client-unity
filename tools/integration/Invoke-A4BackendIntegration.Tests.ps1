$runnerPath = Join-Path $PSScriptRoot 'Invoke-A4BackendIntegration.ps1'
$clientRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$clientParent = Split-Path $clientRoot -Parent
$isWorktree = (Split-Path $clientParent -Leaf) -eq '.worktrees'
$serverRoot = if ($isWorktree) {
    $workspaceRoot = Split-Path (Split-Path $clientParent -Parent) -Parent
    $worktreeName = Split-Path $clientRoot -Leaf
    Join-Path $workspaceRoot "game-server-go\.worktrees\$worktreeName"
}
else {
    Join-Path $clientParent 'game-server-go'
}
$serverVerifierPath = Join-Path $serverRoot 'tools\protobuf\Verify-Protocol.ps1'

Describe 'Invoke-A4BackendIntegration runner ownership' {
    BeforeAll {
        $runner = Get-Content -Raw -LiteralPath $runnerPath
        $serverVerifier = Get-Content -Raw -LiteralPath $serverVerifierPath
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
        $runner | Should Match 'Restore-IntegrationEnvironment -OriginalPath \$originalPathEnvironment'
        $runner | Should Match 'Get-IntegrationPortListeners'
        $runner | Should Match 'Wait-ForHealth -Process \$serverProcess\s*\r?\n\s*Assert-OwnedIntegrationPortsReady'
        $runner | Should Match 'Remove-Item -LiteralPath \$temporaryExecutable -Force'
    }

    It 'runs both generated protocol checks before execution' {
        $runner | Should Match 'Join-Path \$clientRoot ''tools\\protobuf\\Generate-Protocol.ps1'''
        $runner | Should Match 'Join-Path \$backendRoot ''tools\\protobuf\\Generate-Protocol.ps1'''
        $runner | Should Match 'Generate-Protocol\.ps1''\) -Check'
        $runner | Should Match 'Get-RawSha256'
        $runner | Should Match 'Join-Path \$backendRoot ''tools\\protobuf\\Verify-Protocol.ps1'''
        $runner | Should Match 'Join-Path \$clientRoot ''tools\\protobuf\\Verify-GeneratedProtocol.ps1'''
        $runner | Should Match '& go test ./\.\.\. -count=1'
        $runner | Should Match '& go vet ./\.\.\.'
        $runner | Should Match '& go build ./\.\.\.'
    }

    It 'runs the complete three-test Unity fixture after the complete devprobe contract' {
        $runner | Should Match ([regex]::Escape(
            'development session probe passed: sequenced protobuf login found=false typed save typed reload combat duplicate'))
        $runner | Should Match "'-testFilter',\s*'Game\.Tests\.PlayMode\.RealBackendOnlineFlowTests'"
        $runner | Should Match '\$total -ne 3 -or \$passed -ne 3 -or \$failed -ne 0 -or \$skipped -ne 0'
        $runner | Should Not Match 'dataLen'
        $runner | Should Not Match "'-nographics'"
        $runner | Should Not Match "'-quit'"
    }

    It 'publishes exactly the sequenced protocol evidence families' {
        $runner | Should Match 'Write-Host "PROTO_SCHEMA_SHA256_MATCH=1"'
        $runner | Should Match 'Write-Host "DEVPROBE_EVIDENCE=sequenced_protobuf_archive_and_combat:\$probeEvidence"'
        $runner | Should Match 'Write-Host "UNITY_RESULT=total=\$total passed=\$passed failed=\$failed skipped=\$skipped"'
        $runner | Should Not Match 'DEVPROBE_EVIDENCE=protobuf_archive_and_combat'
    }

    It 'requires executable server frame and sequence evidence' {
        $output = @(& $serverVerifierPath -ClientRoot $clientRoot)
        $LASTEXITCODE | Should Be 0
        ($output -contains 'FRAME_TESTS=PASS') | Should Be $true
        ($output -contains 'FRAME_EVIDENCE=header=10 little_endian=1 request_seq_nonzero=1 response_seq_echo=1 pushes_seq_zero=1') | Should Be $true
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
        $runner | Should Match '\$sequencedFrameEvidence -ne 3'
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
