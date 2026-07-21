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
foreach ($reference in $result.MissingGuidReferences) {
    Write-Error "Missing GUID reference $($reference.Guid) at $($reference.AssetPath):$($reference.Line)"
}
foreach ($scene in $result.MissingBuildScenes) {
    Write-Error "Build scene does not exist: $scene"
}

if (-not $result.IsValid) {
    exit 1
}

foreach ($property in $result.ResourceInventory.PSObject.Properties | Sort-Object Name) {
    Write-Output "$($property.Name): $($property.Value)"
}
Write-Output 'Unity asset integrity check passed.'
exit 0
