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

    It 'resolves the project root when the command wrapper uses its default parameter' {
        $fixtureTools = Join-Path $projectRoot 'tools/validation'
        New-Item -ItemType Directory -Force -Path $fixtureTools | Out-Null
        Copy-Item -LiteralPath $modulePath -Destination $fixtureTools
        Copy-Item -LiteralPath $wrapperPath -Destination $fixtureTools

        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixtureTools 'Test-UnityAssetIntegrity.ps1') 2>&1

        $LASTEXITCODE | Should Be 0
        ($output -join "`n") | Should Match 'Unity asset integrity check passed.'
    }
}
