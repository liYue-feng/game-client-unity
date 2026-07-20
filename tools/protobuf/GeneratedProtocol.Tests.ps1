$generatedPath = Join-Path $PSScriptRoot 'generated\Messages.cs'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

Describe 'Generated protobuf protocol staging contract' {
    It 'stages the generated Game.Protocol source outside Assets' {
        (Test-Path -LiteralPath $generatedPath -PathType Leaf) | Should Be $true
        $generatedPath | Should Not Match '[\\/]Assets[\\/]'
    }

    It 'uses Google.Protobuf generated messages without JsonUtility annotations' {
        $content = Get-Content -LiteralPath $generatedPath -Raw
        $content | Should Match 'namespace Game\.Protocol'
        $content | Should Match 'public sealed partial class LoginReq'
        $content | Should Not Match 'JsonUtility'
        $content | Should Not Match '\[Serializable\]'
    }

    It 'does not introduce a client-side proto source of truth' {
        $clientProtoFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..\..') -Recurse -Filter '*.proto')
        $clientProtoFiles.Count | Should Be 0
    }

    It 'tracks the vendored protobuf runtime binaries' {
        foreach ($relativePath in @(
            'Assets/Plugins/Google.Protobuf/Google.Protobuf.dll',
            'Assets/Plugins/Google.Protobuf/System.Runtime.CompilerServices.Unsafe.dll')) {
            $trackedPath = & git -C $repoRoot ls-files --error-unmatch -- $relativePath 2>$null
            $LASTEXITCODE | Should Be 0
            $trackedPath | Should Be $relativePath
        }
    }
}
