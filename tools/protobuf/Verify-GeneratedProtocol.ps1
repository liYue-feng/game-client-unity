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
$protoFiles = @(Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter '*.proto')
if ($protoFiles.Count -ne 0) {
    throw "Client must not own a duplicate .proto source: $($protoFiles.FullName -join ', ')"
}

Write-Output "C# output SHA256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $generatedPath).Hash)"
