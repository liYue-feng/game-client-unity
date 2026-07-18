[CmdletBinding()]
param(
    [string]$ProjectRoot
)

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
}

Import-Module (Join-Path $PSScriptRoot 'UnityAssetIntegrity.psm1') -Force
$result = Test-UnityAssetIntegrity -ProjectRoot $ProjectRoot

foreach ($duplicate in $result.DuplicateGuids) {
    Write-Error "Duplicate GUID $($duplicate.Guid): $($duplicate.Paths -join ', ')"
}
foreach ($reference in $result.InvalidScriptReferences) {
    Write-Error "Invalid m_Script reference $($reference.Guid) at $($reference.AssetPath):$($reference.Line); targets: $($reference.Targets -join ', ')"
}
foreach ($scene in $result.MissingBuildScenes) {
    Write-Error "Build scene does not exist: $scene"
}

if (-not $result.IsValid) {
    exit 1
}

Write-Output 'Unity asset integrity check passed.'
exit 0
