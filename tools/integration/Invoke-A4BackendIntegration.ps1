[CmdletBinding()]
param(
    [string]$BackendRoot,
    [string]$UnityEditor = 'D:\Unity_Soft\2022\Editor\Unity.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\protobuf\PeerRootResolver.ps1')
. (Join-Path $PSScriptRoot 'BackendIntegrationSupport.ps1')

function Resolve-ExistingDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description does not exist: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Get-RawSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Wait-ForHealth {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "Backend exited before health became ready (exit $($Process.ExitCode))."
        }

        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8080/health' -TimeoutSec 2
            if ($response.StatusCode -eq 200 -and $response.Content.Trim() -eq 'ok') {
                return
            }
        }
        catch {
            # The server may still be binding its listener.
        }

        Start-Sleep -Milliseconds 250
    }

    throw 'Backend health endpoint did not become ready within 30 seconds.'
}

function Wait-ForProcessExit {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 900
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) {
            $Process.WaitForExit()
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Process $($Process.Id) did not exit within $TimeoutSeconds seconds."
}

function Wait-ForCompleteUnityXml {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $content = Get-Content -Raw -LiteralPath $Path
            if ($content -match '</test-run>') {
                return
            }
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Unity test XML was not created or did not close completely: $Path"
}

function Quote-CommandLineArgument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value.Contains('"')) {
        throw "Command-line argument cannot contain a quote: $Value"
    }

    return '"' + $Value + '"'
}

$clientRoot = Resolve-ExistingDirectory -Path (Join-Path $PSScriptRoot '..\..') -Description 'Unity client root'
if ([string]::IsNullOrWhiteSpace($BackendRoot)) {
    $BackendRoot = Resolve-PeerRepositoryRoot -CurrentRoot $clientRoot `
        -PeerRepositoryName 'game-server-go' -PeerDescription 'server'
}
else {
    $BackendRoot = Resolve-PeerRepositoryRoot -CurrentRoot $clientRoot -ExplicitPeerRoot $BackendRoot `
        -PeerRepositoryName 'game-server-go' -PeerDescription 'server'
}

$backendRoot = Resolve-ExistingDirectory -Path $BackendRoot -Description 'Go backend root'
if (-not (Test-Path -LiteralPath (Join-Path $clientRoot 'Assets') -PathType Container)) {
    throw "Resolved Unity root is invalid: $clientRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $backendRoot 'go.mod') -PathType Leaf)) {
    throw "Resolved backend root is invalid: $backendRoot"
}
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity editor does not exist: $UnityEditor"
}
Assert-IntegrationPortsFree

$backendLogs = Join-Path $backendRoot 'logs'
New-Item -ItemType Directory -Force -Path $backendLogs | Out-Null
$runId = [Guid]::NewGuid().ToString('N')
$serverExecutable = Join-Path $backendLogs "a4-integration-server-$runId.exe"
$probeExecutable = Join-Path $backendLogs "a4-integration-devprobe-$runId.exe"
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$serverStandardOutput = Join-Path $backendLogs "a4-integration-server-$stamp.stdout.log"
$serverStandardError = Join-Path $backendLogs "a4-integration-server-$stamp.stderr.log"
$probeStandardOutput = Join-Path $backendLogs "a4-integration-devprobe-$stamp.stdout.log"
$probeStandardError = Join-Path $backendLogs "a4-integration-devprobe-$stamp.stderr.log"
$unityResults = Join-Path $clientRoot "Logs\A4-real-backend-$stamp.xml"
$unityLog = Join-Path $clientRoot "Logs\A4-real-backend-$stamp.log"
$serverProcess = $null
$probeProcess = $null
$unityProcess = $null
$operationError = $null
$cleanupErrors = New-Object 'System.Collections.Generic.List[string]'
$originalIntegrationEnvironment = [Environment]::GetEnvironmentVariable('GAME_BACKEND_INTEGRATION', 'Process')
$originalPathEnvironment = $env:PATH
$total = 0
$passed = 0
$failed = 0
$skipped = 0
$archiveLoginEvidence = 0
$victoryLoginEvidence = 0
$defeatLoginEvidence = 0
$probeEvidence = 0
$archiveRoundTripEvidence = 0
$victoryPersistenceEvidence = 0
$defeatSettlementEvidence = 0
$sequencedFrameEvidence = 0
$protoSchemaSha256 = $null

try {
    & (Join-Path $clientRoot 'tools\protobuf\Generate-Protocol.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Client protocol generator check failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $backendRoot 'tools\protobuf\Generate-Protocol.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Server protocol generator check failed with exit code $LASTEXITCODE."
    }

    $clientSchemaPath = Join-Path $clientRoot 'proto\game.proto'
    $serverSchemaPath = Join-Path $backendRoot 'proto\game.proto'
    $clientSchemaSha256 = Get-RawSha256 -Path $clientSchemaPath
    $serverSchemaSha256 = Get-RawSha256 -Path $serverSchemaPath
    if ($clientSchemaSha256 -ne $serverSchemaSha256) {
        throw "Client/server proto SHA256 mismatch: client=$clientSchemaSha256 server=$serverSchemaSha256."
    }
    $protoSchemaSha256 = $clientSchemaSha256

    & (Join-Path $backendRoot 'tools\protobuf\Verify-Protocol.ps1') -ClientRoot $clientRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Server protocol verification failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $clientRoot 'tools\protobuf\Verify-GeneratedProtocol.ps1') -BackendRoot $backendRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Client protocol verification failed with exit code $LASTEXITCODE."
    }

    Push-Location $backendRoot
    try {
        & go test ./... -count=1
        if ($LASTEXITCODE -ne 0) {
            throw "go test ./... -count=1 failed with exit code $LASTEXITCODE."
        }

        & go vet ./...
        if ($LASTEXITCODE -ne 0) {
            throw "go vet ./... failed with exit code $LASTEXITCODE."
        }

        & go build ./...
        if ($LASTEXITCODE -ne 0) {
            throw "go build ./... failed with exit code $LASTEXITCODE."
        }

        & go build -o $serverExecutable ./cmd/server
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $serverExecutable -PathType Leaf)) {
            throw "Go backend build failed with exit code $LASTEXITCODE."
        }

        & go build -o $probeExecutable ./cmd/devprobe
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $probeExecutable -PathType Leaf)) {
            throw "Go devprobe build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    $serverProcess = Start-Process `
        -FilePath $serverExecutable `
        -ArgumentList @('-config', 'configs/config.dev.yaml') `
        -WorkingDirectory $backendRoot `
        -RedirectStandardOutput $serverStandardOutput `
        -RedirectStandardError $serverStandardError `
        -PassThru `
        -WindowStyle Hidden
    Write-Host "BACKEND_PID=$($serverProcess.Id)"
    Wait-ForHealth -Process $serverProcess
    Assert-OwnedIntegrationPortsReady

    $probeProcess = Start-Process `
        -FilePath $probeExecutable `
        -WorkingDirectory $backendRoot `
        -RedirectStandardOutput $probeStandardOutput `
        -RedirectStandardError $probeStandardError `
        -PassThru `
        -Wait `
        -WindowStyle Hidden
    Write-Host "DEVPROBE_PID=$($probeProcess.Id)"
    if ($probeProcess.ExitCode -ne 0) {
        throw "Go devprobe failed with exit code $($probeProcess.ExitCode)."
    }
    $probeOutput = [string](Get-Content -Raw -LiteralPath $probeStandardOutput -Encoding UTF8)
    $probeEvidence = ([regex]::Matches(
        $probeOutput,
        'development session probe passed: sequenced protobuf login found=false typed save typed reload combat duplicate')).Count
    if ($probeEvidence -ne 1) {
        throw 'Go devprobe did not prove protobuf login, found=false, typed save, typed reload, and duplicate combat settlement.'
    }

    [Environment]::SetEnvironmentVariable('GAME_BACKEND_INTEGRATION', '1', 'Process')
    $unityArguments = @(
        '-batchmode',
        '-projectPath', (Quote-CommandLineArgument $clientRoot),
        '-runTests',
        '-testPlatform', 'PlayMode',
        '-testFilter', 'Game.Tests.PlayMode.RealBackendOnlineFlowTests',
        '-testResults', (Quote-CommandLineArgument $unityResults),
        '-logFile', (Quote-CommandLineArgument $unityLog)
    )
    $unityProcess = Start-Process `
        -FilePath $UnityEditor `
        -ArgumentList $unityArguments `
        -PassThru `
        -WindowStyle Hidden
    Write-Host "UNITY_PID=$($unityProcess.Id)"
    Wait-ForProcessExit -Process $unityProcess
    if ($unityProcess.ExitCode -ne 0) {
        throw "Unity test process exited with code $($unityProcess.ExitCode)."
    }

    $serverProcess.Refresh()
    if ($serverProcess.HasExited) {
        throw "Backend exited during Unity integration run (exit $($serverProcess.ExitCode))."
    }

    Wait-ForCompleteUnityXml -Path $unityResults

    $unityResult = Read-UnityTestResult -Path $unityResults
    $testDocument = $unityResult.Document
    $total = $unityResult.Total
    $passed = $unityResult.Passed
    $failed = $unityResult.Failed
    $skipped = $unityResult.Skipped
    if ($total -ne 3 -or $passed -ne 3 -or $failed -ne 0 -or $skipped -ne 0) {
        $failureText = @($testDocument.SelectNodes('//failure/message') | ForEach-Object { $_.InnerText }) -join ' | '
        throw "Unity integration result was total=$total passed=$passed failed=$failed skipped=$skipped. $failureText"
    }

    if (-not (Test-Path -LiteralPath $unityLog -PathType Leaf)) {
        throw "Unity log was not created: $unityLog"
    }
    $unityOutput = [string](Get-Content -Raw -LiteralPath $unityLog -Encoding UTF8)
    $archiveRoundTripEvidence = ([regex]::Matches(
        $unityOutput,
        [regex]::Escape('[REAL_BACKEND] ARCHIVE_ROUND_TRIP_OK'))).Count
    $victoryPersistenceEvidence = ([regex]::Matches(
        $unityOutput,
        [regex]::Escape('[REAL_BACKEND] VICTORY_PERSISTENCE_OK'))).Count
    $defeatSettlementEvidence = ([regex]::Matches(
        $unityOutput,
        [regex]::Escape('[REAL_BACKEND] DEFEAT_SETTLEMENT_OK'))).Count
    $sequencedFrameEvidence = ([regex]::Matches(
        $unityOutput,
        [regex]::Escape('[REAL_BACKEND] SEQUENCED_FRAMES_OK'))).Count
    if ($archiveRoundTripEvidence -ne 1 -or
        $victoryPersistenceEvidence -ne 1 -or
        $defeatSettlementEvidence -ne 1 -or
        $sequencedFrameEvidence -ne 3) {
        throw "Unity completion evidence was archive=$archiveRoundTripEvidence victory=$victoryPersistenceEvidence defeat=$defeatSettlementEvidence sequenced_frames=$sequencedFrameEvidence; expected business markers once and sequenced frames three times."
    }

    if (-not (Test-Path -LiteralPath $serverStandardOutput -PathType Leaf)) {
        throw "Backend stdout log was not created: $serverStandardOutput"
    }

    $serverOutput = [string](Get-Content -Raw -LiteralPath $serverStandardOutput -Encoding UTF8)
    $archiveLoginEvidence = ([regex]::Matches($serverOutput, 'dev:integration-client')).Count
    $victoryLoginEvidence = ([regex]::Matches($serverOutput, 'dev:integration-battle-victory')).Count
    $defeatLoginEvidence = ([regex]::Matches($serverOutput, 'dev:integration-battle-defeat')).Count
    if ($archiveLoginEvidence -lt 1 -or $victoryLoginEvidence -lt 1 -or $defeatLoginEvidence -lt 1) {
        throw "Backend login evidence was archive=$archiveLoginEvidence victory=$victoryLoginEvidence defeat=$defeatLoginEvidence; expected every fixed identity at least once."
    }

}
catch {
    $operationError = $_
}
finally {
    try {
        Restore-IntegrationEnvironment -OriginalPath $originalPathEnvironment `
            -OriginalIntegrationEnvironment $originalIntegrationEnvironment
    }
    catch {
        [void]$cleanupErrors.Add("Restore PATH: $($_.Exception.Message)")
    }

    if ($unityProcess -ne $null) {
        try {
            $unityProcess.Refresh()
            if (-not $unityProcess.HasExited) {
                Stop-Process -Id $unityProcess.Id -Force
                $unityProcess.WaitForExit()
            }
        }
        catch {
            [void]$cleanupErrors.Add("Stop Unity PID $($unityProcess.Id): $($_.Exception.Message)")
        }
    }

    if ($serverProcess -ne $null) {
        try {
            $serverProcess.Refresh()
            if (-not $serverProcess.HasExited) {
                Stop-Process -Id $serverProcess.Id -Force
                $serverProcess.WaitForExit()
            }
        }
        catch {
            [void]$cleanupErrors.Add("Stop backend PID $($serverProcess.Id): $($_.Exception.Message)")
        }
    }

    try {
        $portDeadline = (Get-Date).AddSeconds(10)
        while ((Get-Date) -lt $portDeadline -and @(Get-IntegrationPortListeners).Count -ne 0) {
            Start-Sleep -Milliseconds 250
        }
        if (@(Get-IntegrationPortListeners).Count -ne 0) {
            [void]$cleanupErrors.Add('Port 8080 or 8081 is still listening after captured backend process cleanup.')
        }
    }
    catch {
        [void]$cleanupErrors.Add("Verify port 8080/8081 cleanup: $($_.Exception.Message)")
    }

    if ($probeProcess -ne $null) {
        try {
            $probeProcess.Refresh()
            if (-not $probeProcess.HasExited) {
                Stop-Process -Id $probeProcess.Id -Force
                $probeProcess.WaitForExit()
            }
        }
        catch {
            [void]$cleanupErrors.Add("Stop devprobe PID $($probeProcess.Id): $($_.Exception.Message)")
        }
    }

    foreach ($temporaryExecutable in @($serverExecutable, $probeExecutable)) {
        try {
            if (Test-Path -LiteralPath $temporaryExecutable -PathType Leaf) {
                Remove-Item -LiteralPath $temporaryExecutable -Force
            }
        }
        catch {
            [void]$cleanupErrors.Add("Remove temporary executable ${temporaryExecutable}: $($_.Exception.Message)")
        }
    }
}

if ($operationError -ne $null) {
    foreach ($cleanupError in $cleanupErrors) {
        Write-Warning "Cleanup after integration failure: $cleanupError"
    }

    $PSCmdlet.ThrowTerminatingError($operationError)
}

if ($cleanupErrors.Count -ne 0) {
    throw "Integration cleanup failed: $($cleanupErrors -join '; ')"
}

Write-Host "GO_TESTS=PASS"
Write-Host "PROTO_SCHEMA_SHA256_MATCH=1"
Write-Host "PROTO_SCHEMA_SHA256=$protoSchemaSha256"
Write-Host "UNITY_RESULT=total=$total passed=$passed failed=$failed skipped=$skipped"
Write-Host "DEVPROBE_EVIDENCE=sequenced_protobuf_archive_and_combat:$probeEvidence"
Write-Host "UNITY_EVIDENCE=archive:$archiveRoundTripEvidence victory:$victoryPersistenceEvidence defeat:$defeatSettlementEvidence"
Write-Host "UNITY_FRAME_EVIDENCE=sequenced_flows:$sequencedFrameEvidence"
Write-Host "SERVER_EVIDENCE=archive_login:$archiveLoginEvidence victory_login:$victoryLoginEvidence defeat_login:$defeatLoginEvidence"
Write-Host "UNITY_XML=$unityResults"
Write-Host "UNITY_LOG=$unityLog"
Write-Host "SERVER_LOG=$serverStandardOutput"
