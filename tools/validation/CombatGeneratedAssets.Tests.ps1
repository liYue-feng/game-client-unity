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

function Get-WriteIfMissingMethodRange([string]$source) {
    $signature = [regex]::Match(
        $source,
        'public\s+static\s+bool\s+WriteIfMissing\s*\([^)]*\)')
    if (-not $signature.Success) { return $null }

    $openingBrace = $source.IndexOf('{', $signature.Index + $signature.Length)
    if ($openingBrace -lt 0) { return $null }

    $depth = 0
    for ($index = $openingBrace; $index -lt $source.Length; $index++) {
        if ($source[$index] -eq '{') {
            $depth++
        }
        elseif ($source[$index] -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return [pscustomobject]@{
                    Start = $signature.Index
                    Length = $index - $signature.Index + 1
                    Text = $source.Substring($signature.Index, $index - $signature.Index + 1)
                }
            }
        }
    }

    return $null
}

function Test-ContainsForbiddenResourceWriter(
    [string]$source,
    [bool]$allowSingleWriteAllBytes = $false) {
    $scan = $source
    $allowedWriter = [regex]'(?:System\s*\.\s*IO\s*\.\s*)?File\s*\.\s*WriteAllBytes\s*\('
    if ($allowSingleWriteAllBytes) {
        if ($allowedWriter.Matches($scan).Count -ne 1) { return $true }
        $scan = $allowedWriter.Replace($scan, 'AllowedResourceWrite(', 1)
    }

    $directWriterPattern =
        '(?<![A-Za-z0-9_])(?:System\s*\.\s*IO\s*\.\s*)?File\s*\.\s*' +
        '(?:Write[A-Za-z0-9_]*|Append[A-Za-z0-9_]*|Create|CreateText|OpenWrite)\s*\('
    if ($scan -match $directWriterPattern) { return $true }

    $writeModes = 'FileMode\s*\.\s*(?:Create|CreateNew|OpenOrCreate|Truncate|Append)'
    $writeAccess = 'FileAccess\s*\.\s*(?:Write|ReadWrite)'
    $openMode = 'FileMode\s*\.\s*Open\b'
    $readOnlyAccess = 'FileAccess\s*\.\s*Read\b'
    $fileOpenPattern =
        '(?s)(?:System\s*\.\s*IO\s*\.\s*)?File\s*\.\s*Open\s*\((?<args>.*?)\)'
    foreach ($match in [regex]::Matches($scan, $fileOpenPattern)) {
        $arguments = $match.Groups['args'].Value
        if ($arguments -match $writeModes -or
            $arguments -match $writeAccess -or
            ($arguments -match $openMode -and $arguments -notmatch $readOnlyAccess)) {
            return $true
        }
    }

    $fileStreamPattern =
        '(?s)new\s+(?:System\s*\.\s*IO\s*\.\s*)?FileStream\s*\((?<args>.*?)\)'
    foreach ($match in [regex]::Matches($scan, $fileStreamPattern)) {
        $arguments = $match.Groups['args'].Value
        if ($arguments -match $writeModes -or
            $arguments -match $writeAccess -or
            ($arguments -match $openMode -and $arguments -notmatch $readOnlyAccess)) {
            return $true
        }
    }

    $fileInfoWriter = '(?:OpenWrite|Create|CreateText|AppendText)'
    $directFileInfoPattern =
        'new\s+(?:System\s*\.\s*IO\s*\.\s*)?FileInfo\s*\([^)]*\)\s*\.\s*' +
        $fileInfoWriter + '\s*\('
    if ($scan -match $directFileInfoPattern) { return $true }

    $fileInfoVariablePattern =
        '(?:var|(?:System\s*\.\s*IO\s*\.\s*)?FileInfo)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)' +
        '\s*=\s*new\s+(?:System\s*\.\s*IO\s*\.\s*)?FileInfo\s*\('
    foreach ($match in [regex]::Matches($scan, $fileInfoVariablePattern)) {
        $name = [regex]::Escape($match.Groups['name'].Value)
        if ($scan -match "\b$name\s*\.\s*$fileInfoWriter\s*\(") { return $true }
    }

    $memoryStreamVariables = @(
        [regex]::Matches(
            $scan,
            '(?:var|(?:System\s*\.\s*IO\s*\.\s*)?MemoryStream)\s+' +
            '(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+' +
            '(?:System\s*\.\s*IO\s*\.\s*)?MemoryStream\s*\(') |
            ForEach-Object { $_.Groups['name'].Value }
    )
    $streamWriterPattern =
        '(?s)new\s+(?:System\s*\.\s*IO\s*\.\s*)?StreamWriter\s*\((?<args>.*?)\)'
    foreach ($match in [regex]::Matches($scan, $streamWriterPattern)) {
        $firstArgument = ($match.Groups['args'].Value -split ',', 2)[0].Trim()
        $constructsMemoryStream =
            $firstArgument -match '^new\s+(?:System\s*\.\s*IO\s*\.\s*)?MemoryStream\s*\('
        if (-not $constructsMemoryStream -and $memoryStreamVariables -notcontains $firstArgument) {
            return $true
        }
    }

    return $false
}

function Test-UsesOnlyGuardedResourceWrites([string]$source) {
    $writeMethod = Get-WriteIfMissingMethodRange $source
    if ($null -eq $writeMethod) { return $false }
    if ($writeMethod.Text -notmatch 'File\s*\.\s*Exists\s*\(') { return $false }
    if (([regex]::Matches($writeMethod.Text, 'File\s*\.\s*WriteAllBytes\s*\(')).Count -ne 1) {
        return $false
    }
    if (Test-ContainsForbiddenResourceWriter $writeMethod.Text $true) { return $false }

    $outsideMethod = $source.Remove($writeMethod.Start, $writeMethod.Length)
    return -not (Test-ContainsForbiddenResourceWriter $outsideMethod)
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
        (Test-UsesOnlyGuardedResourceWrites $source) | Should Be $true
    }

    It 'rejects alternate file writers outside WriteIfMissing' {
        $validSource = @'
public static bool WriteIfMissing(string path, byte[] bytes)
{
    if (File.Exists(path)) return false;
    File.WriteAllBytes(path, bytes);
    return true;
}
private static byte[] GenerateEnemyPng() { return null; }
private static byte[] BuildWav()
{
    using (var output = new MemoryStream())
    using (var writer = new BinaryWriter(output))
    {
        writer.Write((short)1);
        return output.ToArray();
    }
}
private static void ReadOnly(string path)
{
    using (var stream = File.Open(path, FileMode.Open, FileAccess.Read)) { }
}
'@
        (Test-UsesOnlyGuardedResourceWrites $validSource) | Should Be $true

        $mutations = @(
            "$validSource`nprivate static void Bypass(string path) { File.WriteAllText(path, `"x`"); }",
            "$validSource`nprivate static void Bypass(string path) { File.AppendAllText(path, `"x`"); }",
            "$validSource`nprivate static void Bypass(string path) { File.Create(path); }",
            "$validSource`nprivate static void Bypass(string path) { File.OpenWrite(path); }",
            "$validSource`nprivate static void Bypass(string path) { File.Open(path, FileMode.Create); }",
            "$validSource`nprivate static void Bypass(string path) { File.Open(path, FileMode.Open, FileAccess.Write); }",
            "$validSource`nprivate static void Bypass(string path) { File.Open(path, FileMode.Open); }",
            "$validSource`nprivate static void Bypass(string path) { new FileStream(path, FileMode.Create, FileAccess.Write); }",
            "$validSource`nprivate static void Bypass(string path) { new FileStream(path, FileMode.Open, FileAccess.Write); }",
            "$validSource`nprivate static void Bypass(string path) { new FileStream(path, FileMode.Open); }",
            "$validSource`nprivate static void Bypass(string path) { new FileInfo(path).OpenWrite(); }",
            "$validSource`nprivate static void Bypass(string path) { var info = new FileInfo(path); info.Create(); }",
            "$validSource`nprivate static void Bypass(string path) { new StreamWriter(path); }",
            ($validSource -replace 'File\.WriteAllBytes\(path, bytes\);',
                'File.Create(path); File.WriteAllBytes(path, bytes);')
        )
        foreach ($mutation in $mutations) {
            (Test-UsesOnlyGuardedResourceWrites $mutation) | Should Be $false
        }
    }

    It 'allows StreamWriter when its target is a known MemoryStream' {
        $memoryWriterSource = @'
public static bool WriteIfMissing(string path, byte[] bytes)
{
    if (File.Exists(path)) return false;
    File.WriteAllBytes(path, bytes);
    return true;
}
private static byte[] GenerateEnemyPng() { return null; }
private static byte[] BuildText()
{
    using (var memoryStream = new MemoryStream())
    using (var writer = new StreamWriter(memoryStream))
    {
        writer.Write("placeholder");
        writer.Flush();
        return memoryStream.ToArray();
    }
}
'@
        (Test-UsesOnlyGuardedResourceWrites $memoryWriterSource) | Should Be $true
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
