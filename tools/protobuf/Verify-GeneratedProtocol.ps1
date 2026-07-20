[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$generatedPath = Join-Path $PSScriptRoot 'generated\Messages.cs'
if (-not (Test-Path -LiteralPath $generatedPath -PathType Leaf)) {
    throw "Generated C# protocol is missing: $generatedPath"
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

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtimeGeneratedPath = Join-Path $projectRoot 'Assets\Scripts\Protocol\Generated\Messages.cs'
if (-not (Test-Path -LiteralPath $runtimeGeneratedPath -PathType Leaf)) {
    throw "Unity runtime generated C# protocol is missing: $runtimeGeneratedPath"
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

$protoFiles = @(Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter '*.proto')
if ($protoFiles.Count -ne 0) {
    throw "Client must not own a duplicate .proto source: $($protoFiles.FullName -join ', ')"
}

Write-Output "C# output SHA256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $generatedPath).Hash)"
