Set-StrictMode -Version Latest

function Test-UnityAssetIntegrity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $assetsRoot = Join-Path $resolvedRoot 'Assets'
    $guidPattern = '^guid:\s*([0-9a-fA-F]{32})\s*$'
    $scriptPattern = 'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})'

    $metaRecords = foreach ($metaFile in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Filter '*.meta') {
        $match = Select-String -LiteralPath $metaFile.FullName -Pattern $guidPattern | Select-Object -First 1
        if ($null -ne $match) {
            [PSCustomObject]@{
                Guid = $match.Matches[0].Groups[1].Value.ToLowerInvariant()
                Path = $metaFile.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
            }
        }
    }

    $duplicateGuids = @($metaRecords |
        Group-Object Guid |
        Where-Object Count -gt 1 |
        ForEach-Object {
            [PSCustomObject]@{
                Guid = $_.Name
                Paths = @($_.Group.Path | Sort-Object)
            }
        })

    $metaByGuid = @{}
    foreach ($record in $metaRecords) {
        if (-not $metaByGuid.ContainsKey($record.Guid)) {
            $metaByGuid[$record.Guid] = @()
        }
        $metaByGuid[$record.Guid] += $record.Path
    }

    $serializedAssets = Get-ChildItem -LiteralPath $assetsRoot -Recurse -File |
        Where-Object { $_.Extension -in '.unity', '.prefab', '.asset' }
    $invalidScriptReferences = foreach ($asset in $serializedAssets) {
        foreach ($match in Select-String -LiteralPath $asset.FullName -Pattern $scriptPattern) {
            $guid = $match.Matches[0].Groups[1].Value.ToLowerInvariant()
            $targets = @()
            if ($metaByGuid.ContainsKey($guid)) {
                $targets = @($metaByGuid[$guid])
            }
            $scriptTargets = @($targets | Where-Object { $_ -like '*.cs.meta' })
            if ($scriptTargets.Count -ne 1 -or $targets.Count -ne 1) {
                [PSCustomObject]@{
                    AssetPath = $asset.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                    Line = $match.LineNumber
                    Guid = $guid
                    Targets = $targets
                }
            }
        }
    }

    $buildSettingsPath = Join-Path $resolvedRoot 'ProjectSettings/EditorBuildSettings.asset'
    $missingBuildScenes = @()
    if (Test-Path -LiteralPath $buildSettingsPath) {
        $missingBuildScenes = @(Select-String -LiteralPath $buildSettingsPath -Pattern '^\s*path:\s*(.+?)\s*$' |
            ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() } |
            Where-Object { -not (Test-Path -LiteralPath (Join-Path $resolvedRoot $_)) })
    }

    [PSCustomObject]@{
        IsValid = $duplicateGuids.Count -eq 0 -and @($invalidScriptReferences).Count -eq 0 -and $missingBuildScenes.Count -eq 0
        DuplicateGuids = $duplicateGuids
        InvalidScriptReferences = @($invalidScriptReferences)
        MissingBuildScenes = $missingBuildScenes
    }
}

Export-ModuleMember -Function Test-UnityAssetIntegrity
