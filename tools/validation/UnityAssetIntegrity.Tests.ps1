$modulePath = Join-Path $PSScriptRoot 'UnityAssetIntegrity.psm1'
$wrapperPath = Join-Path $PSScriptRoot 'Test-UnityAssetIntegrity.ps1'
Import-Module $modulePath -Force

Describe 'Test-UnityAssetIntegrity' {
    BeforeEach {
        $projectRoot = Join-Path $TestDrive 'Project'
        Remove-Item -LiteralPath $projectRoot -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path `
            (Join-Path $projectRoot 'Assets/Scripts'), `
            (Join-Path $projectRoot 'Assets/Scenes'), `
            (Join-Path $projectRoot 'ProjectSettings') | Out-Null

        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scripts/Example.cs') -Value 'public class Example {}'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scripts/Example.cs.meta') -Value "fileFormatVersion: 2`nguid: 11111111111111111111111111111111"
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scenes/Test.unity') -Value '  m_Script: {fileID: 11500000, guid: 11111111111111111111111111111111, type: 3}'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scenes/Test.unity.meta') -Value "fileFormatVersion: 2`nguid: 22222222222222222222222222222222"
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Value '    path: Assets/Scenes/Test.unity'
    }

    It 'accepts unique GUIDs, valid script references, and existing build scenes' {
        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        $result.IsValid | Should Be $true
        @($result.DuplicateGuids).Count | Should Be 0
        @($result.InvalidScriptReferences).Count | Should Be 0
        @($result.MissingBuildScenes).Count | Should Be 0
        $missingProperty = $result.PSObject.Properties['MissingGuidReferences']
        $missingProperty | Should Not BeNullOrEmpty
        if ($null -ne $missingProperty) {
            @($missingProperty.Value).Count | Should Be 0
        }
    }

    It 'reports every path that shares a duplicate GUID' {
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Duplicate.meta') -Value "fileFormatVersion: 2`nguid: 11111111111111111111111111111111"

        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        $result.IsValid | Should Be $false
        @($result.DuplicateGuids).Count | Should Be 1
        @($result.DuplicateGuids[0].Paths).Count | Should Be 2
    }

    It 'rejects an m_Script GUID that does not resolve to exactly one C# meta file' {
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scenes/Test.unity') -Value '  m_Script: {fileID: 11500000, guid: 33333333333333333333333333333333, type: 3}'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Folder.meta') -Value "fileFormatVersion: 2`nguid: 33333333333333333333333333333333"

        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        $result.IsValid | Should Be $false
        @($result.InvalidScriptReferences).Count | Should Be 1
        $result.InvalidScriptReferences[0].AssetPath | Should Be 'Assets/Scenes/Test.unity'
    }

    It 'reports a build scene path that does not exist' {
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Value '    path: Assets/Scenes/Missing.unity'

        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        $result.IsValid | Should Be $false
        @($result.MissingBuildScenes).Count | Should Be 1
        $result.MissingBuildScenes[0] | Should Be 'Assets/Scenes/Missing.unity'
    }

    It 'reports unresolved GUIDs, ignores Unity built-ins, and inventories every resource type' {
        Remove-Item -LiteralPath (Join-Path $projectRoot 'Assets/Scenes/Test.unity') -Force
        Remove-Item -LiteralPath (Join-Path $projectRoot 'Assets/Scenes/Test.unity.meta') -Force
        New-Item -ItemType Directory -Force -Path `
            (Join-Path $projectRoot 'Assets/Prefabs'), `
            (Join-Path $projectRoot 'Assets/Resources/Sprites'), `
            (Join-Path $projectRoot 'Assets/Resources/Materials'), `
            (Join-Path $projectRoot 'Assets/Resources/Animations'), `
            (Join-Path $projectRoot 'Assets/Resources/Controllers'), `
            (Join-Path $projectRoot 'Assets/Resources/Sounds'), `
            (Join-Path $projectRoot 'Assets/Resources/Fonts'), `
            (Join-Path $projectRoot 'Assets/Data') | Out-Null

        $validSpriteGuid = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Resources/Sprites/Hero.png') -Value 'png fixture'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Resources/Sprites/Hero.png.meta') -Value "fileFormatVersion: 2`nguid: $validSpriteGuid"

        $battleScene = @(
            "  m_Sprite: {fileID: 21300000, guid: $validSpriteGuid, type: 3}"
            '  m_AudioClip: {fileID: 8300000, guid: 22222222222222222222222222222222, type: 3}'
            '  m_Zero: {fileID: 0, guid: 00000000000000000000000000000000, type: 0}'
            '  m_BuiltinMaterial: {fileID: 10303, guid: 0000000000000000e000000000000000, type: 0}'
            '  m_DefaultResource: {fileID: 10202, guid: 0000000000000000f000000000000000, type: 0}'
        )
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scenes/BattleScene.unity') -Value $battleScene
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Scenes/BattleScene.unity.meta') -Value "fileFormatVersion: 2`nguid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Value '    path: Assets/Scenes/BattleScene.unity'

        $validSerializedFixtures = @(
            'Assets/Prefabs/Enemy.prefab'
            'Assets/Data/Combat.asset'
            'Assets/Resources/Materials/Enemy.mat'
            'Assets/Resources/Animations/Attack.anim'
            'Assets/Resources/Controllers/Enemy.controller'
        )
        $fixtureGuidIndex = 12
        foreach ($relativePath in $validSerializedFixtures) {
            Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot $relativePath) -Value "  m_Sprite: {fileID: 21300000, guid: $validSpriteGuid, type: 3}"
            $fixtureGuid = ('{0:x32}' -f $fixtureGuidIndex)
            Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot "$relativePath.meta") -Value "fileFormatVersion: 2`nguid: $fixtureGuid"
            $fixtureGuidIndex++
        }
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Resources/Sounds/Hit.wav') -Value 'wav fixture'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Resources/Sounds/Hit.wav.meta') -Value "fileFormatVersion: 2`nguid: cccccccccccccccccccccccccccccccc"
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Resources/Fonts/Main.ttf') -Value 'font fixture'
        Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Resources/Fonts/Main.ttf.meta') -Value "fileFormatVersion: 2`nguid: dddddddddddddddddddddddddddddddd"

        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        $missingProperty = $result.PSObject.Properties['MissingGuidReferences']
        $missingProperty | Should Not BeNullOrEmpty
        $missingGuidReferences = @()
        if ($null -ne $missingProperty) {
            $missingGuidReferences = @($missingProperty.Value)
        }
        $missingGuidReferences.Count | Should Be 1
        if ($missingGuidReferences.Count -eq 1) {
            $missingGuidReferences[0].Guid | Should Be '22222222222222222222222222222222'
            $missingGuidReferences[0].AssetPath | Should Match 'BattleScene.unity'
            $missingGuidReferences[0].Line | Should Be 2
        }
        $missingGuids = @($missingGuidReferences.Guid)
        ($missingGuids -contains '00000000000000000000000000000000') | Should Be $false
        ($missingGuids -contains '0000000000000000e000000000000000') | Should Be $false
        ($missingGuids -contains '0000000000000000f000000000000000') | Should Be $false
        $result.ResourceInventory.Scene | Should Be 1
        $result.ResourceInventory.Prefab | Should Be 1
        $result.ResourceInventory.SpriteTexture | Should Be 1
        $result.ResourceInventory.Material | Should Be 1
        $result.ResourceInventory.AnimationClip | Should Be 1
        $result.ResourceInventory.AnimatorController | Should Be 1
        $result.ResourceInventory.AudioClip | Should Be 1
        $result.ResourceInventory.Font | Should Be 1
        $result.IsValid | Should Be $false
    }

    It 'scans every supported serialized Unity extension for GUID references' {
        $serializedFixtures = @(
            @{ Path = 'Assets/Scenes/Missing.unity'; Guid = '30000000000000000000000000000001' }
            @{ Path = 'Assets/Missing.prefab'; Guid = '30000000000000000000000000000002' }
            @{ Path = 'Assets/Missing.asset'; Guid = '30000000000000000000000000000003' }
            @{ Path = 'Assets/Missing.mat'; Guid = '30000000000000000000000000000004' }
            @{ Path = 'Assets/Missing.anim'; Guid = '30000000000000000000000000000005' }
            @{ Path = 'Assets/Missing.controller'; Guid = '30000000000000000000000000000006' }
        )
        foreach ($fixture in $serializedFixtures) {
            Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot $fixture.Path) -Value "  m_Reference: {fileID: 1, guid: $($fixture.Guid), type: 3}"
        }
        Add-Content -Encoding UTF8 -Path (Join-Path $projectRoot 'Assets/Missing.asset') -Value '  m_ExtraReferences: [{fileID: 1, guid: 30000000000000000000000000000007, type: 3}, {fileID: 0, guid: 0000000000000000e000000000000000, type: 0}]'

        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        @($result.MissingGuidReferences).Count | Should Be 7
        foreach ($fixture in $serializedFixtures) {
            @($result.MissingGuidReferences | Where-Object { $_.Guid -eq $fixture.Guid -and $_.AssetPath -eq $fixture.Path }).Count | Should Be 1
        }
        @($result.MissingGuidReferences | Where-Object { $_.Guid -eq '30000000000000000000000000000007' -and $_.Line -eq 2 }).Count | Should Be 1
    }

    It 'combines alternate sprite, audio, and font extensions in inventory categories' {
        New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'Assets/Resources') | Out-Null
        foreach ($path in @('Icon.jpg', 'Portrait.jpeg', 'Sheet.psd', 'Music.mp3', 'Voice.ogg', 'Title.otf')) {
            Set-Content -Encoding UTF8 -Path (Join-Path $projectRoot "Assets/Resources/$path") -Value 'fixture'
        }

        $result = Test-UnityAssetIntegrity -ProjectRoot $projectRoot

        $result.ResourceInventory.SpriteTexture | Should Be 3
        $result.ResourceInventory.AudioClip | Should Be 2
        $result.ResourceInventory.Font | Should Be 1
    }

    It 'resolves the project root when the command wrapper uses its default parameter' {
        $fixtureTools = Join-Path $projectRoot 'tools/validation'
        New-Item -ItemType Directory -Force -Path $fixtureTools | Out-Null
        Copy-Item -LiteralPath $modulePath -Destination $fixtureTools
        Copy-Item -LiteralPath $wrapperPath -Destination $fixtureTools

        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixtureTools 'Test-UnityAssetIntegrity.ps1') 2>&1

        $LASTEXITCODE | Should Be 0
        $inventoryLines = @($output | Where-Object { "$_" -match '^\w+: \d+$' })
        $inventoryLines.Count | Should Be 8
        ($inventoryLines -join ',') | Should Be 'AnimationClip: 0,AnimatorController: 0,AudioClip: 0,Font: 0,Material: 0,Prefab: 0,Scene: 1,SpriteTexture: 0'
        ($output -join "`n") | Should Match 'Unity asset integrity check passed.'
    }
}
