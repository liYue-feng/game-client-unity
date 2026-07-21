Set-StrictMode -Version Latest

function Test-UnityAssetIntegrity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $assetsRoot = Join-Path $resolvedRoot 'Assets'
    $metaGuidPattern = '^guid:\s*([0-9a-fA-F]{32})\s*$'
    $guidPattern = [regex]'guid:\s*([0-9a-fA-F]{32})'
    $scriptPattern = 'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})'
    $serializedExtensions = @('.unity', '.prefab', '.asset', '.mat', '.anim', '.controller')
    $ignoredGuids = @(
        '00000000000000000000000000000000'
        '0000000000000000e000000000000000'
        '0000000000000000f000000000000000'
    )

    $metaRecords = foreach ($metaFile in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Filter '*.meta' | Sort-Object FullName) {
        $match = Select-String -LiteralPath $metaFile.FullName -Pattern $metaGuidPattern | Select-Object -First 1
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

    $serializedAssets = @(Get-ChildItem -LiteralPath $assetsRoot -Recurse -File |
        Where-Object { $_.Extension.ToLowerInvariant() -in $serializedExtensions } |
        Sort-Object FullName)

    $missingGuidReferences = foreach ($asset in $serializedAssets) {
        $assetPath = $asset.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
        foreach ($lineMatch in Select-String -LiteralPath $asset.FullName -Pattern $guidPattern) {
            foreach ($match in $lineMatch.Matches) {
                $guid = $match.Groups[1].Value.ToLowerInvariant()
                if ($guid -notin $ignoredGuids -and -not $metaByGuid.ContainsKey($guid)) {
                    [PSCustomObject]@{
                        Guid = $guid
                        AssetPath = $assetPath
                        Line = $lineMatch.LineNumber
                    }
                }
            }
        }
    }

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

    $resourceCounts = @{
        AnimationClip = 0
        AnimatorController = 0
        AudioClip = 0
        Font = 0
        Material = 0
        Prefab = 0
        Scene = 0
        SpriteTexture = 0
    }
    foreach ($assetFile in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File) {
        switch ($assetFile.Extension.ToLowerInvariant()) {
            '.unity' { $resourceCounts.Scene++; break }
            '.prefab' { $resourceCounts.Prefab++; break }
            { $_ -in '.png', '.jpg', '.jpeg', '.psd' } { $resourceCounts.SpriteTexture++; break }
            '.mat' { $resourceCounts.Material++; break }
            '.anim' { $resourceCounts.AnimationClip++; break }
            '.controller' { $resourceCounts.AnimatorController++; break }
            { $_ -in '.wav', '.mp3', '.ogg' } { $resourceCounts.AudioClip++; break }
            { $_ -in '.ttf', '.otf' } { $resourceCounts.Font++; break }
        }
    }
    $resourceInventory = [PSCustomObject][ordered]@{
        Scene = $resourceCounts.Scene
        Prefab = $resourceCounts.Prefab
        SpriteTexture = $resourceCounts.SpriteTexture
        Material = $resourceCounts.Material
        AnimationClip = $resourceCounts.AnimationClip
        AnimatorController = $resourceCounts.AnimatorController
        AudioClip = $resourceCounts.AudioClip
        Font = $resourceCounts.Font
    }

    [PSCustomObject]@{
        IsValid = $duplicateGuids.Count -eq 0 -and @($invalidScriptReferences).Count -eq 0 -and @($missingGuidReferences).Count -eq 0 -and $missingBuildScenes.Count -eq 0
        DuplicateGuids = $duplicateGuids
        InvalidScriptReferences = @($invalidScriptReferences)
        MissingGuidReferences = @($missingGuidReferences)
        MissingBuildScenes = $missingBuildScenes
        ResourceInventory = $resourceInventory
    }
}

Export-ModuleMember -Function Test-UnityAssetIntegrity
