$generatedPath = Join-Path $PSScriptRoot 'generated\Game.cs'
$generatorPath = Join-Path $PSScriptRoot 'Generate-Protocol.ps1'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $PSScriptRoot 'PeerRootResolver.ps1')

function Get-RawSha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Get-CrlfNormalizedFingerprint([string]$Path) {
    [byte[]]$source = [IO.File]::ReadAllBytes($Path)
    $normalized = New-Object 'System.Collections.Generic.List[byte]'
    for ($index = 0; $index -lt $source.Length; $index++) {
        if ($source[$index] -eq 0x0D -and $index + 1 -lt $source.Length -and $source[$index + 1] -eq 0x0A) {
            continue
        }
        $normalized.Add($source[$index])
    }

    return [Convert]::ToBase64String($normalized.ToArray())
}

$repoParent = Split-Path $repoRoot -Parent
$isWorktree = (Split-Path $repoParent -Leaf) -eq '.worktrees'
$explicitBackendRoot = if ($isWorktree) { $null } else { Join-Path $repoParent 'game-server-go' }
$backendRoot = Resolve-PeerRepositoryRoot -CurrentRoot $repoRoot -ExplicitPeerRoot $explicitBackendRoot `
    -PeerRepositoryName 'game-server-go' -PeerDescription 'server'

$transportContract = 'Transport contract: 10-byte little-endian [Length uint32][MsgID uint16][Seq uint32]; Length includes the 10-byte header; request seq is nonzero; responses and errors echo the exact request seq; pushes use seq 0; Body is protobuf binary.'

Describe 'Authoritative transport documentation' {
    It 'documents the sequenced protobuf frame in CLAUDE.md' {
        $content = [IO.File]::ReadAllText((Join-Path $repoRoot 'CLAUDE.md'))
        $content | Should Match ([regex]::Escape($transportContract))
        $content | Should Not Match '(?i)6[- ]byte|six[- ]byte|6\s*字节|六字节|Length\s*=\s*6\s*\+|4B长度\s*\+\s*2B'
        $content | Should Not Match '(?im)^(?=.*Length)(?=.*MsgID)(?!.*Seq).*$'
    }
}

Describe 'Canonical schema ownership' {
    It 'owns one local game.proto and rejects the old source names' {
        Test-Path (Join-Path $repoRoot 'proto\game.proto') | Should Be $true
        @(Get-ChildItem (Join-Path $repoRoot 'proto') -Recurse -Filter '*.proto').Count | Should Be 1
        Test-Path (Join-Path $repoRoot ('proto\game\v1\' + 'messages' + '.proto')) | Should Be $false
        Test-Path (Join-Path $repoRoot 'tools\protobuf\generated\Game.cs') | Should Be $true
        Test-Path (Join-Path $repoRoot 'Assets\Scripts\Protocol\Generated\Game.cs') | Should Be $true
        Test-Path (Join-Path $repoRoot ('tools\protobuf\generated\' + 'Messages' + '.cs')) | Should Be $false
        Test-Path (Join-Path $repoRoot ('Assets\Scripts\Protocol\Generated\' + 'Messages' + '.cs')) | Should Be $false
    }

    It 'matches the sibling server schema byte-for-byte' {
        $clientSchema = Join-Path $repoRoot 'proto\game.proto'
        $serverSchema = Join-Path $backendRoot 'proto\game.proto'
        Test-Path -LiteralPath $clientSchema -PathType Leaf | Should Be $true
        Test-Path -LiteralPath $serverSchema -PathType Leaf | Should Be $true
        (Get-RawSha256 -Path $clientSchema) | Should Be (Get-RawSha256 -Path $serverSchema)
    }

    It 'keeps staging and runtime Game.cs equal after CRLF-only normalization' {
        $stagingPath = Join-Path $repoRoot 'tools\protobuf\generated\Game.cs'
        $runtimePath = Join-Path $repoRoot 'Assets\Scripts\Protocol\Generated\Game.cs'
        (Get-CrlfNormalizedFingerprint -Path $stagingPath) | Should Be (Get-CrlfNormalizedFingerprint -Path $runtimePath)
    }
}

Describe 'Generated protobuf protocol staging contract' {
    It 'stages the generated Game.Protocol source outside Assets' {
        (Test-Path -LiteralPath $generatedPath -PathType Leaf) | Should Be $true
        $generatedPath | Should Not Match '[\\/]Assets[\\/]'
        (Test-Path -LiteralPath $generatorPath -PathType Leaf) | Should Be $true
    }

    It 'uses Google.Protobuf generated messages without JsonUtility annotations' {
        $content = Get-Content -LiteralPath $generatedPath -Raw
        $content | Should Match 'namespace Game\.Protocol'
        $content | Should Match 'public sealed partial class LoginReq'
        $content | Should Not Match 'JsonUtility'
        $content | Should Not Match '\[Serializable\]'
    }

    It 'rejects a runtime generated source that differs from staging' {
        $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("GeneratedProtocolFixture-{0}" -f [guid]::NewGuid())
        $fixtureToolsPath = Join-Path $fixtureRoot 'tools\protobuf'
        $fixtureGeneratedPath = Join-Path $fixtureToolsPath 'generated\Game.cs'
        $fixtureRuntimePath = Join-Path $fixtureRoot 'Assets\Scripts\Protocol\Generated\Game.cs'
        $fixtureVerifier = Join-Path $fixtureToolsPath 'Verify-GeneratedProtocol.ps1'
        $fixtureSchemaPath = Join-Path $fixtureRoot 'proto\game.proto'
        $fixtureSource = @'
using Google.Protobuf;
namespace Game.Protocol {
    public sealed partial class LoginReq { }
}
'@

        try {
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureGeneratedPath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntimePath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureSchemaPath) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-GeneratedProtocol.ps1') -Destination $fixtureVerifier
            Copy-Item -LiteralPath (Join-Path $repoRoot 'proto\game.proto') -Destination $fixtureSchemaPath
            [IO.File]::WriteAllText($fixtureGeneratedPath, $fixtureSource)
            [IO.File]::WriteAllText($fixtureRuntimePath, "$fixtureSource// stale runtime source`n")

            { & $fixtureVerifier } | Should Throw
        }
        finally {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'rejects a runtime generated source with a standalone carriage return' {
        $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("GeneratedProtocolFixture-{0}" -f [guid]::NewGuid())
        $fixtureToolsPath = Join-Path $fixtureRoot 'tools\protobuf'
        $fixtureGeneratedPath = Join-Path $fixtureToolsPath 'generated\Game.cs'
        $fixtureRuntimePath = Join-Path $fixtureRoot 'Assets\Scripts\Protocol\Generated\Game.cs'
        $fixtureVerifier = Join-Path $fixtureToolsPath 'Verify-GeneratedProtocol.ps1'
        $fixtureSchemaPath = Join-Path $fixtureRoot 'proto\game.proto'
        $fixtureSource = @'
using Google.Protobuf;
namespace Game.Protocol {
    public sealed partial class LoginReq { }
}
'@

        try {
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureGeneratedPath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntimePath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureSchemaPath) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-GeneratedProtocol.ps1') -Destination $fixtureVerifier
            Copy-Item -LiteralPath (Join-Path $repoRoot 'proto\game.proto') -Destination $fixtureSchemaPath
            [IO.File]::WriteAllText($fixtureGeneratedPath, $fixtureSource, (New-Object Text.UTF8Encoding($false)))
            $firstLineFeed = $fixtureSource.IndexOf("`n")
            $firstLineFeed | Should BeGreaterThan -1
            $runtimeSource = $fixtureSource.Substring(0, $firstLineFeed) + "`r" + $fixtureSource.Substring($firstLineFeed + 1)
            [IO.File]::WriteAllText($fixtureRuntimePath, $runtimeSource, (New-Object Text.UTF8Encoding($false)))

            { & $fixtureVerifier } | Should Throw
        }
        finally {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'rejects a runtime generated source with a UTF-8 BOM' {
        $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("GeneratedProtocolFixture-{0}" -f [guid]::NewGuid())
        $fixtureToolsPath = Join-Path $fixtureRoot 'tools\protobuf'
        $fixtureGeneratedPath = Join-Path $fixtureToolsPath 'generated\Game.cs'
        $fixtureRuntimePath = Join-Path $fixtureRoot 'Assets\Scripts\Protocol\Generated\Game.cs'
        $fixtureVerifier = Join-Path $fixtureToolsPath 'Verify-GeneratedProtocol.ps1'
        $fixtureSchemaPath = Join-Path $fixtureRoot 'proto\game.proto'
        $fixtureSource = @'
using Google.Protobuf;
namespace Game.Protocol {
    public sealed partial class LoginReq { }
}
'@

        try {
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureGeneratedPath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntimePath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureSchemaPath) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-GeneratedProtocol.ps1') -Destination $fixtureVerifier
            Copy-Item -LiteralPath (Join-Path $repoRoot 'proto\game.proto') -Destination $fixtureSchemaPath
            [IO.File]::WriteAllText($fixtureGeneratedPath, $fixtureSource, (New-Object Text.UTF8Encoding($false)))
            $content = [IO.File]::ReadAllBytes($fixtureGeneratedPath)
            $withBom = New-Object byte[] ($content.Length + 3)
            $withBom[0] = 0xEF
            $withBom[1] = 0xBB
            $withBom[2] = 0xBF
            [Array]::Copy($content, 0, $withBom, 3, $content.Length)
            [IO.File]::WriteAllBytes($fixtureRuntimePath, $withBom)

            { & $fixtureVerifier } | Should Throw
        }
        finally {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'does not introduce a client-side proto source of truth' {
        $clientProtoFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..\..') -Recurse -Filter '*.proto')
        $clientProtoFiles.Count | Should Be 1
        $clientProtoFiles[0].FullName | Should Be (Join-Path $repoRoot 'proto\game.proto')
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
