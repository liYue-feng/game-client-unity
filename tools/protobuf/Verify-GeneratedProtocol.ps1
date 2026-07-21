[CmdletBinding()]
param([string]$BackendRoot)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$schemaPath = Join-Path $projectRoot 'proto\game.proto'
$generatedPath = Join-Path $PSScriptRoot 'generated\Game.cs'
$runtimeGeneratedPath = Join-Path $projectRoot 'Assets\Scripts\Protocol\Generated\Game.cs'

function Get-RawSha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
    throw "Canonical schema is missing: $schemaPath"
}
$protoFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'proto') -Recurse -Filter '*.proto')
if ($protoFiles.Count -ne 1 -or $protoFiles[0].FullName -ne $schemaPath) {
    throw "Client must own only proto/game.proto: $($protoFiles.FullName -join ', ')"
}

$legacyStagingPath = Join-Path $projectRoot ('tools\protobuf\generated\' + 'Messages' + '.cs')
$legacyRuntimePath = Join-Path $projectRoot ('Assets\Scripts\Protocol\Generated\' + 'Messages' + '.cs')
foreach ($legacyPath in @($legacyStagingPath, $legacyRuntimePath)) {
    if (Test-Path -LiteralPath $legacyPath) {
        throw "Old generated C# source must be removed: $legacyPath"
    }
}

if (-not (Test-Path -LiteralPath $generatedPath -PathType Leaf)) {
    throw "Generated C# protocol is missing: $generatedPath"
}
if (-not (Test-Path -LiteralPath $runtimeGeneratedPath -PathType Leaf)) {
    throw "Unity runtime generated C# protocol is missing: $runtimeGeneratedPath"
}

$content = Get-Content -LiteralPath $generatedPath -Raw
foreach ($expected in @('namespace Game.Protocol', 'public sealed partial class LoginReq', 'Google.Protobuf')) {
    if ($content -notmatch [regex]::Escape($expected)) {
        throw "Generated C# protocol is missing expected text: $expected"
    }
}
foreach ($forbidden in @('JsonUtility', '[Serializable]')) {
    if ($content -match [regex]::Escape($forbidden)) {
        throw "Generated C# protocol contains forbidden handwritten JSON text: $forbidden"
    }
}

function Get-NormalizedGeneratedFingerprint {
    param([string]$Path)

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

if ((Get-NormalizedGeneratedFingerprint -Path $generatedPath) -cne (Get-NormalizedGeneratedFingerprint -Path $runtimeGeneratedPath)) {
    throw "Unity runtime generated C# protocol differs from staging: $runtimeGeneratedPath"
}

if ([string]::IsNullOrWhiteSpace($BackendRoot)) {
    $clientParent = Split-Path $projectRoot -Parent
    $isWorktree = (Split-Path $clientParent -Leaf) -eq '.worktrees'
    $workspaceRoot = if ($isWorktree) {
        Split-Path (Split-Path $clientParent -Parent) -Parent
    }
    else {
        $clientParent
    }
    $worktreeName = Split-Path $projectRoot -Leaf
    $candidates = if ($isWorktree) {
        @(
            (Join-Path $workspaceRoot "game-server-go\.worktrees\$worktreeName"),
            (Join-Path $workspaceRoot 'game-server-go')
        )
    }
    else {
        @((Join-Path $workspaceRoot 'game-server-go'))
    }
    $BackendRoot = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
}

if (-not [string]::IsNullOrWhiteSpace($BackendRoot)) {
    $BackendRoot = (Resolve-Path $BackendRoot).Path
    $serverSchemaPath = Join-Path $BackendRoot 'proto\game.proto'
    if (-not (Test-Path -LiteralPath $serverSchemaPath -PathType Leaf)) {
        throw "Sibling server schema is missing: $serverSchemaPath"
    }
    if ((Get-RawSha256 -Path $schemaPath) -ne (Get-RawSha256 -Path $serverSchemaPath)) {
        throw "Client and server schema SHA256 values differ: '$schemaPath' versus '$serverSchemaPath'."
    }
}

& (Join-Path $PSScriptRoot 'Generate-Protocol.ps1') -Check
if ($LASTEXITCODE -ne 0) { throw 'Generated C# protocol drift check failed.' }

Write-Output "Schema SHA256=$(Get-RawSha256 -Path $schemaPath)"
Write-Output "C# output SHA256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $generatedPath).Hash)"
