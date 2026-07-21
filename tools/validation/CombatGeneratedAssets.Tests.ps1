$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$catalogPath = Join-Path $projectRoot 'Assets/Scripts/Managers/SoundCatalog.cs'
$generatorPath = Join-Path $projectRoot 'Assets/Editor/CombatAssetGenerator.cs'
$spriteLoaderPath = Join-Path $projectRoot 'Assets/Scripts/Game/Visual/AiSpriteLoader.cs'
$soundsRoot = Join-Path $projectRoot 'Assets/Resources/Sounds'

function Get-CatalogEntries {
    $catalogSource = Get-Content -Raw $catalogPath
    $entryPattern = [regex]::new(
        '\["(?<key>[^"]+)"\]\s*=\s*new SoundEntry\s*\{.*?suggestedFile\s*=\s*"(?<file>[^"]+)"',
        [Text.RegularExpressions.RegexOptions]::Singleline)

    return @($entryPattern.Matches($catalogSource) | ForEach-Object {
        [pscustomobject]@{
            Key = $_.Groups['key'].Value
            File = $_.Groups['file'].Value
        }
    })
}

function Get-BigEndianInt32([byte[]]$bytes, [int]$offset) {
    return (($bytes[$offset] -shl 24) -bor
        ($bytes[$offset + 1] -shl 16) -bor
        ($bytes[$offset + 2] -shl 8) -bor
        $bytes[$offset + 3])
}

Describe 'Generated combat assets' {
    It 'declares a deterministic create-only generator with a single guarded writer' {
        $generatorPath | Should Exist
        if (-not (Test-Path -LiteralPath $generatorPath)) { return }

        $source = Get-Content -Raw $generatorPath
        $source | Should Match 'public\s+static\s+void\s+GenerateAll\s*\('
        $source | Should Match 'public\s+static\s+bool\s+WriteIfMissing\s*\('
        $source | Should Match 'Skipped existing'
        $source | Should Match 'importer\.SaveAndReimport\(\)'
        $source | Should Match 'AssetDatabase\.Refresh\(ImportAssetOptions\.ForceSynchronousImport\)'
        $source | Should Match 'AssertLoadedSprite\("Sprites/Enemies/Archer"'
        $source | Should Match 'AssertLoadedSprite\("Sprites/Enemies/Elite"'
        $source | Should Match '4101'
        $source | Should Match '4201'
        $source | Should Match 'new\s+Color32'
        $source | Should Not Match 'UnityEngine\.Random'
        ([regex]::Matches($source, 'File\.WriteAllBytes')).Count | Should Be 1

        $writeMethod = [regex]::Match(
            $source,
            'public\s+static\s+bool\s+WriteIfMissing\s*\([^)]*\)\s*\{(?<body>.*?)(?=\n\s*private\s+static\s+byte\[\]\s+GenerateEnemyPng)',
            [Text.RegularExpressions.RegexOptions]::Singleline)
        $writeMethod.Success | Should Be $true
        if ($writeMethod.Success) {
            $writeMethod.Groups['body'].Value | Should Match 'File\.Exists'
            $writeMethod.Groups['body'].Value | Should Match 'File\.WriteAllBytes'
        }
    }

    It 'uses distinct imported Sprite resources for Archer and Elite' {
        $source = Get-Content -Raw $spriteLoaderPath
        $source | Should Match '_archerSprite\s*=\s*TryLoadSprite\("Sprites/Enemies/Archer"\)'
        $source | Should Match '_eliteSprite\s*=\s*TryLoadSprite\("Sprites/Enemies/Elite"\)'
        $source | Should Match 'return\s+Resources\.Load<Sprite>\(path\)'
        $source | Should Not Match 'Resources\.Load<Texture2D>\(path\)'
        $source | Should Not Match 'Sprite\.Create\('
    }

    It 'commits valid native-size Archer and Elite PNG files' {
        $targets = @(
            @{ Path = 'Assets/Resources/Sprites/Enemies/Archer.png'; Width = 64; Height = 64 }
            @{ Path = 'Assets/Resources/Sprites/Enemies/Elite.png'; Width = 96; Height = 96 }
        )

        foreach ($target in $targets) {
            $path = Join-Path $projectRoot $target.Path
            $path | Should Exist
            if (-not (Test-Path -LiteralPath $path)) { continue }

            $bytes = [IO.File]::ReadAllBytes($path)
            ($bytes[0..7] -join ',') | Should Be '137,80,78,71,13,10,26,10'
            (Get-BigEndianInt32 $bytes 16) | Should Be $target.Width
            (Get-BigEndianInt32 $bytes 20) | Should Be $target.Height
        }
    }

    It 'commits every SoundCatalog target as bounded PCM 44.1 kHz mono 16-bit WAV' {
        $entries = @(Get-CatalogEntries)
        $entries.Count | Should BeGreaterThan 0

        foreach ($entry in $entries) {
            $wav = Join-Path $soundsRoot $entry.File
            $wav | Should Exist
            if (-not (Test-Path -LiteralPath $wav)) { continue }

            $bytes = [IO.File]::ReadAllBytes($wav)
            $bytes.Length | Should BeGreaterThan 44
            [Text.Encoding]::ASCII.GetString($bytes, 0, 4) | Should Be 'RIFF'
            [Text.Encoding]::ASCII.GetString($bytes, 8, 4) | Should Be 'WAVE'
            [Text.Encoding]::ASCII.GetString($bytes, 12, 4) | Should Be 'fmt '
            [BitConverter]::ToInt16($bytes, 20) | Should Be 1
            [BitConverter]::ToInt16($bytes, 22) | Should Be 1
            [BitConverter]::ToInt32($bytes, 24) | Should Be 44100
            [BitConverter]::ToInt16($bytes, 34) | Should Be 16
            [Text.Encoding]::ASCII.GetString($bytes, 36, 4) | Should Be 'data'

            $dataLength = [BitConverter]::ToInt32($bytes, 40)
            $duration = ($dataLength / 2.0) / 44100.0
            if ($entry.Key -like 'ambient_*') {
                [Math]::Abs($duration - 2.0) | Should BeLessThan 0.0001
            }
            elseif ($entry.Key -like 'bgm_*') {
                [Math]::Abs($duration - 3.0) | Should BeLessThan 0.0001
            }
            else {
                $duration | Should Not BeLessThan 0.04
                $duration | Should Not BeGreaterThan 0.5
            }

            $peak = 0
            for ($offset = 44; $offset -lt $bytes.Length; $offset += 2) {
                $sample = [Math]::Abs([int][BitConverter]::ToInt16($bytes, $offset))
                if ($sample -gt $peak) { $peak = $sample }
            }
            $peak | Should BeGreaterThan 0
            $peak | Should Not BeGreaterThan 31128
        }
    }
}
