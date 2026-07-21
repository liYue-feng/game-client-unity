function Get-IntegrationPortListeners {
    $networkProperties = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    return @($networkProperties.GetActiveTcpListeners() | Where-Object { $_.Port -in @(8080, 8081) })
}
function Assert-IntegrationPortsFree {
    if (@(Get-IntegrationPortListeners).Count -ne 0) {
        throw 'Port 8080 or 8081 is already in use; refusing to stop or reuse an unowned process.'
    }
}

function Restore-IntegrationEnvironment {
    param(
        [AllowNull()][string]$OriginalPath,
        [AllowNull()][string]$OriginalIntegrationEnvironment
    )
    $env:PATH = $OriginalPath
    [Environment]::SetEnvironmentVariable(
        'GAME_BACKEND_INTEGRATION',
        $OriginalIntegrationEnvironment,
        'Process')
}

function Read-UnityTestResult {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Unity test XML does not exist: $Path"
    }
    $content = Get-Content -Raw -LiteralPath $Path
    if ($content -notmatch '</test-run>') {
        throw "Unity test XML is incomplete: $Path"
    }
    [xml]$document = $content
    $run = $document.'test-run'
    return [pscustomobject]@{
        Document = $document
        Total = [int]$run.total
        Passed = [int]$run.passed
        Failed = [int]$run.failed
        Skipped = [int]$run.skipped
    }
}
