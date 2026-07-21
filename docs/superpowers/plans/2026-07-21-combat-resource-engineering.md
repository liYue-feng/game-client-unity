# Combat Resource Engineering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make combat resources reproducible, auditable, visually testable, and explicit about the remaining external art/licensing work.

**Architecture:** PowerShell performs repository-wide serialized GUID and resource-inventory checks without opening Unity. A deterministic, create-only Unity editor generator creates only missing enemy sprites and SoundCatalog placeholder WAV assets; any existing generated or externally replaced PNG/WAV is skipped and preserved. Runtime and PlayMode validation prove the final resources load through `Resources` at the required import settings and produce nonblank, correctly framed battle and result screens.

**Tech Stack:** Unity 2022.3 Editor APIs, C#, PowerShell 5.1/Pester 3.4, NUnit PlayMode, PNG, PCM WAV 44.1 kHz mono 16-bit.

## Global Constraints

- Work only in `E:/Own_project/game-client-unity`; each reviewed task is committed and pushed from `master`.
- Do not overwrite the existing Player, TitleCharacter, Grunt, or Boss PNG assets.
- Generated resources must be deterministic and idempotent: running `Game.Editor.CombatAssetGenerator.GenerateAll` twice produces identical bytes.
- Generated enemy PNG files use point filtering, clamp wrap, no mipmaps, uncompressed texture import, and sprite pixels-per-unit 64.
- Generated audio uses PCM WAV, 44,100 Hz, mono, 16-bit, normalized below clipping. These WAVs are generated placeholders, not licensed imports; they remain only until licensed replacement files are imported at the same paths.
- `GenerateAll` is create-only: it writes a target only when that exact PNG/WAV path is missing, logs `Skipped existing <path>` for every existing target, and never overwrites an existing generated or licensed replacement file. No manifest is required for this safety policy; the second run skips all existing targets and preserves their hashes.
- `ConfigureSpriteImporter` runs only for PNGs created by the current generator run and ends with `importer.SaveAndReimport()`. After import, both generated and pre-existing enemy resources must pass `Resources.Load<Sprite>` and `pixelsPerUnit == 64f` validation; an incorrectly imported external replacement fails the validation instead of being rewritten.
- Audit Scene, Prefab, Sprite/Texture, Material, AnimationClip, AnimatorController, AudioClip, Font, and every serialized GUID reference.
- Do not claim external art is complete. Every source/licensing/art-direction gap must contain an exact target path, consumer, dimensions/import settings, production prompt/steps, import command, test, and provenance owner.
- Existing battle, enemy, settlement UI, and pixel-evidence tests remain authoritative; no resource may overlap UI or make the scene blank.
- Use RED-GREEN-REFACTOR. Implementers commit but do not push; the controller pushes after spec and quality review.

---

### Task 1: Expand Static Asset Integrity Coverage

**Files:**
- Modify: `tools/validation/UnityAssetIntegrity.psm1`
- Modify: `tools/validation/UnityAssetIntegrity.Tests.ps1`
- Modify: `tools/validation/Test-UnityAssetIntegrity.ps1`

**Interfaces:**
- Consumes: Unity `.meta` files, YAML scenes/prefabs/assets, ProjectSettings build scenes, and resource file extensions.
- Produces: `MissingGuidReferences`, `DuplicateGuids`, `InvalidScriptReferences`, `MissingBuildScenes`, and per-type `ResourceInventory`.

- [ ] **Step 1: Write failing Pester fixtures**

Create fixture meta/YAML files containing one valid sprite GUID and one absent audio GUID. Include serialized references to the all-zero GUID, Unity's built-in material GUID `0000000000000000e000000000000000`, and Unity's built-in default-resource GUID `0000000000000000f000000000000000`; these three fixture references must not produce missing-reference records. Require this result shape:

```powershell
$result.MissingGuidReferences.Count | Should Be 1
$result.MissingGuidReferences[0].Guid | Should Be '22222222222222222222222222222222'
$result.MissingGuidReferences[0].AssetPath | Should Match 'BattleScene.unity'
$result.ResourceInventory.Scene | Should Be 1
$result.ResourceInventory.SpriteTexture | Should Be 1
$result.IsValid | Should Be $false
```

Add an explicit ignored-built-ins assertion so a future regex or index change cannot regress this boundary:

```powershell
$result.MissingGuidReferences.Guid | Should Not Contain '00000000000000000000000000000000'
$result.MissingGuidReferences.Guid | Should Not Contain '0000000000000000e000000000000000'
$result.MissingGuidReferences.Guid | Should Not Contain '0000000000000000f000000000000000'
```

Add a valid fixture for every audited type extension: `.unity`, `.prefab`, `.png`, `.mat`, `.anim`, `.controller`, `.wav`, and `.ttf`.

- [ ] **Step 2: Run Pester and verify RED**

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/UnityAssetIntegrity.Tests.ps1 -EnableExit"
```

Expected: failures because the current module checks only duplicate GUIDs, script GUIDs, and build scenes.

- [ ] **Step 3: Implement general GUID resolution and inventory**

Index every `guid:` value from `.meta` files, then inspect serialized Unity text files using this exact reference regex:

```powershell
$guidPattern = [regex]'guid:\s*([0-9a-fA-F]{32})'
$serializedExtensions = @('.unity', '.prefab', '.asset', '.mat', '.anim', '.controller')
```

Exclude exactly these Unity built-in/non-project GUIDs before checking the meta index: `00000000000000000000000000000000`, `0000000000000000e000000000000000`, and `0000000000000000f000000000000000`. Any other serialized GUID missing from the meta index becomes one `MissingGuidReferences` record with GUID, asset path, and 1-based line. Inventory keys are `Scene`, `Prefab`, `SpriteTexture`, `Material`, `AnimationClip`, `AnimatorController`, `AudioClip`, and `Font`; count `.png/.jpg/.jpeg/.psd`, `.wav/.mp3/.ogg`, and `.ttf/.otf` in their combined categories.

Update the wrapper to emit one `Write-Error` per missing GUID and print a stable sorted inventory before returning exit 0.

- [ ] **Step 4: Run focused and real-project validation**

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/UnityAssetIntegrity.Tests.ps1 -EnableExit"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
```

Expected: fixture tests pass; the real project has no duplicate or unresolved serialized GUID and prints all eight categories, including zero counts.

- [ ] **Step 5: Commit**

```powershell
git add tools/validation
git commit -m "test: audit all Unity combat resource references"
```

### Task 2: Generate Deterministic Enemy Sprites and SoundCatalog Audio

**Files:**
- Create: `Assets/Editor/CombatAssetGenerator.cs`
- Create: `Assets/Editor/CombatAssetGeneratorTests.cs`
- Create after generator run: `Assets/Resources/Sprites/Enemies/Archer.png`
- Create after generator run: `Assets/Resources/Sprites/Enemies/Elite.png`
- Create after generator run: `Assets/Resources/Sounds/*.wav`
- Modify: `Assets/Scripts/Game/Visual/AiSpriteLoader.cs`
- Create: `Assets/Tests/PlayMode/GeneratedCombatResourceTests.cs`
- Create: `tools/validation/CombatGeneratedAssets.Tests.ps1`

**Interfaces:**
- Consumes: `SoundCatalog.Catalog` target filenames and existing runtime `AiSpriteLoader` resource paths.
- Produces: `Game.Editor.CombatAssetGenerator.GenerateAll` and deterministic committed assets.

- [ ] **Step 1: Write failing generated-asset tests**

Pester loads the catalog filenames directly from `SoundCatalog.cs` and requires every `suggestedFile` below `Assets/Resources/Sounds`. It also requires the two enemy PNGs and validates file headers:

```powershell
[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($wav), 0, 4) | Should Be 'RIFF'
[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($wav), 8, 4) | Should Be 'WAVE'
$pngBytes = [IO.File]::ReadAllBytes($png)
($pngBytes[0..7] -join ',') | Should Be '137,80,78,71,13,10,26,10'
```

Require `AiSpriteLoader` to contain `Sprites/Enemies/Archer` and `Sprites/Enemies/Elite` rather than aliasing Grunt/Boss.

Create `Assets/Editor/CombatAssetGeneratorTests.cs` with `WriteIfMissing_DoesNotOverwriteExistingPngOrWav`. The test loops over `.png` and `.wav`, creates one unique temp path per extension, writes sentinel bytes `{ 0x53, 0x45, 0x4E, 0x54 }`, calls `CombatAssetGenerator.WriteIfMissing(tempPath, new byte[] { 0x4F, 0x56, 0x45, 0x52 })`, asserts the return value is `false`, and asserts the file still contains the sentinel bytes before deleting it in `finally`. This is the required existing PNG/WAV safety fixture without mutating real project resources.

Pester also reads `CombatAssetGenerator.cs` and requires the `Skipped existing` log, `SaveAndReimport()`, and exactly one `File.WriteAllBytes` call in the entire generator source; that sole call must be inside `WriteIfMissing`. This prevents a future resource writer from bypassing the create-only guard.

```powershell
$generatorSource = Get-Content -Raw 'Assets/Editor/CombatAssetGenerator.cs'
$generatorSource | Should Match 'Skipped existing'
$generatorSource | Should Match 'importer\.SaveAndReimport\(\)'
([regex]::Matches($generatorSource, 'File\.WriteAllBytes')).Count | Should Be 1
$generatorSource | Should Match 'WriteIfMissing'
```

Create `GeneratedCombatResourceTests.cs` with failing PlayMode coverage for both runtime resource types. It must load each enemy by its real `Resources` path, invoke `AiSpriteLoader.ArcherSprite()` and `AiSpriteLoader.EliteSprite()`, and assert the returned sprites are non-null, distinct, and reference the loaded sprites. Render both sprites through a transparent-background test camera to an ARGB32 `RenderTexture`, copy it with `Texture2D.ReadPixels`, and calculate the alpha bounding box from rendered pixels where `a > 0`; require that bounding box to be non-empty and wholly inside the viewport with a one-pixel margin. Assert `sprite.pixelsPerUnit == 64f` and the generated native dimensions are respectively `64x64` (Archer) and `96x96` (Elite).

The same PlayMode file must iterate every `SoundCatalog.Catalog` entry after a test `AudioManager.LoadAllSounds()` call. For each `(key, entry)`, derive `Sounds/<suggestedFile without extension>`, require `AudioManager.IsLoadedFromResources(key) == true`, then use `Resources.Load<AudioClip>` to require a non-null clip with `samples > 0` and `length > 0f`. This verifies committed generated WAVs through the production loader without adding a mutable test hook.

- [ ] **Step 2: Run Pester and verify RED**

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/CombatGeneratedAssets.Tests.ps1 -EnableExit"
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.GeneratedCombatResourceTests' -testResults 'Logs\generated-combat-resource-red.xml' -logFile 'Logs\generated-combat-resource-red.log'
```

Expected: Pester and the PlayMode test fail because the generator, distinct enemy PNGs, and committed WAV files do not exist; the PlayMode failure must name the missing Archer/Elite resource or SoundCatalog entry.

Run the editor sentinel test in the same red cycle:

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform EditMode -testFilter 'Game.Editor.CombatAssetGeneratorTests.WriteIfMissing_DoesNotOverwriteExistingPngOrWav' -testResults 'Logs\combat-asset-generator-red.xml' -logFile 'Logs\combat-asset-generator-red.log'
```

Expected: the sentinel test is RED until `WriteIfMissing` exists; it must never create or modify a file below `Assets/Resources`.

- [ ] **Step 3: Implement the deterministic Unity generator**

The public batch entry point is exact:

```csharp
namespace Game.Editor
{
    public static class CombatAssetGenerator
    {
        [MenuItem("Tools/Game/Generate Combat Assets")]
        public static void GenerateAll()
        {
            var createdSpritePaths = new List<string>();
            if (WriteIfMissing("Assets/Resources/Sprites/Enemies/Archer.png", GenerateEnemyPng("Archer", 64, 64, ArcherPalette, 4101)))
                createdSpritePaths.Add("Assets/Resources/Sprites/Enemies/Archer.png");
            if (WriteIfMissing("Assets/Resources/Sprites/Enemies/Elite.png", GenerateEnemyPng("Elite", 96, 96, ElitePalette, 4201)))
                createdSpritePaths.Add("Assets/Resources/Sprites/Enemies/Elite.png");
            GenerateSoundCatalogWavsIfMissing();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in createdSpritePaths)
                ConfigureSpriteImporter(path);
            AssetDatabase.SaveAssets();
            AssertLoadedSprite("Sprites/Enemies/Archer", "Assets/Resources/Sprites/Enemies/Archer.png");
            AssertLoadedSprite("Sprites/Enemies/Elite", "Assets/Resources/Sprites/Enemies/Elite.png");
        }

        public static bool WriteIfMissing(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[CombatAssetGenerator] Skipped existing {path}");
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
            return true;
        }
    }
}
```

Use a fixed integer seed, explicit RGBA palettes, and integer pixel geometry; never use `UnityEngine.Random`. Archer is a 64x64 green/black bow silhouette. Elite is a 96x96 crimson/gold armored silhouette. Transparent pixels remain `(0,0,0,0)`. Route every PNG and WAV write through `WriteIfMissing`; do not call `File.WriteAllBytes` for a resource anywhere else. Existing targets must log `Skipped existing <path>` and keep their original bytes, including a licensed replacement at a catalog path.

Generate exactly the filenames declared by `SoundCatalog.Catalog`, but call `WriteIfMissing` for each path. Reuse its semantic categories with deterministic tone/noise/sweep recipes: impact clips 0.04-0.5 s, ambient clips 2 s, and BGM clips 3 s. Each WAV writer emits RIFF/WAVE `fmt ` PCM metadata and `data` samples at 44,100 Hz mono 16-bit. Noise uses an explicit per-file seed; all samples clamp to `[-0.95, 0.95]`.

The committed WAVs are generated placeholders only, not licensed imports. The generator must not claim a source license or change the catalog's later licensed-replacement workflow.

Update `AiSpriteLoader` to cache `_archerSprite` and `_eliteSprite` separately in `PreloadAllSprites` using the exact paths below. `TryLoadSprite` must use `Resources.Load<Sprite>(path)` directly; remove the `Resources.Load<Texture2D>` plus `Sprite.Create(..., 100f)` conversion so imported 64-PPU sprite metadata is preserved.

```csharp
_archerSprite = TryLoadSprite("Sprites/Enemies/Archer");
_eliteSprite = TryLoadSprite("Sprites/Enemies/Elite");

private static Sprite TryLoadSprite(string path)
{
    return Resources.Load<Sprite>(path);
}
```

`ArcherSprite()` returns `_archerSprite` (or `PlaceholderSpriteFactory.ArcherSprite()`), and `EliteSprite()` returns `_eliteSprite` (or `PlaceholderSpriteFactory.EliteSprite()`).

`ConfigureSpriteImporter` sets:

```csharp
importer.textureType = TextureImporterType.Sprite;
importer.spriteImportMode = SpriteImportMode.Single;
importer.spritePixelsPerUnit = 64f;
importer.filterMode = FilterMode.Point;
importer.wrapMode = TextureWrapMode.Clamp;
importer.mipmapEnabled = false;
importer.textureCompression = TextureImporterCompression.Uncompressed;
importer.SaveAndReimport();
```

`AssertLoadedSprite(resourcePath, assetPath)` must call `Resources.Load<Sprite>(resourcePath)`, fail if the result is `null`, fail if `sprite.pixelsPerUnit != 64f`, and include both paths in the failure message. Call it after `SaveAndReimport()` for Archer and Elite on every generator invocation, including when both paths were skipped; this proves existing external replacements are already correctly imported without rewriting them.

```csharp
private static void AssertLoadedSprite(string resourcePath, string assetPath)
{
    var sprite = Resources.Load<Sprite>(resourcePath);
    if (sprite == null)
        throw new InvalidOperationException($"Sprite failed Resources.Load<Sprite>: {resourcePath} ({assetPath})");
    if (!Mathf.Approximately(sprite.pixelsPerUnit, 64f))
        throw new InvalidOperationException($"Sprite must use 64 PPU: {resourcePath} ({assetPath}), actual={sprite.pixelsPerUnit}");
}
```

- [ ] **Step 4: Run the generator twice and prove byte idempotence**

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'E:\Own_project\game-client-unity' `
  -executeMethod Game.Editor.CombatAssetGenerator.GenerateAll `
  -logFile 'E:\Own_project\game-client-unity\Logs\combat-assets-first.log'
$before = Get-ChildItem Assets\Resources\Sprites\Enemies,Assets\Resources\Sounds -File -Recurse | Sort-Object FullName | ForEach-Object { "$($_.FullName)|$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'E:\Own_project\game-client-unity' `
  -executeMethod Game.Editor.CombatAssetGenerator.GenerateAll `
  -logFile 'E:\Own_project\game-client-unity\Logs\combat-assets-second.log'
$after = Get-ChildItem Assets\Resources\Sprites\Enemies,Assets\Resources\Sounds -File -Recurse | Sort-Object FullName | ForEach-Object { "$($_.FullName)|$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
$difference = @(Compare-Object $before $after)
if ($difference.Count -ne 0) { throw "Combat asset generation is not byte-idempotent: $($difference | Out-String)" }
$skipLog = Get-Content 'Logs\combat-assets-second.log' -Raw
$soundFiles = Select-String -Path 'Assets\Scripts\Managers\SoundCatalog.cs' -Pattern 'suggestedFile\s*=\s*"([^"]+)"' -AllMatches | ForEach-Object { $_.Matches.Groups[1].Value }
$targets = @('Assets/Resources/Sprites/Enemies/Archer.png', 'Assets/Resources/Sprites/Enemies/Elite.png') + @($soundFiles | ForEach-Object { "Assets/Resources/Sounds/$_" })
foreach ($target in $targets) {
  if ($skipLog -notmatch [regex]::Escape("Skipped existing $target")) { throw "Missing skip log for $target" }
}
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/CombatGeneratedAssets.Tests.ps1 -EnableExit"
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform EditMode -testFilter 'Game.Editor.CombatAssetGeneratorTests.WriteIfMissing_DoesNotOverwriteExistingPngOrWav' -testResults 'Logs\combat-asset-generator-green.xml' -logFile 'Logs\combat-asset-generator-green.log'
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.GeneratedCombatResourceTests' -testResults 'Logs\generated-combat-resource-green.xml' -logFile 'Logs\generated-combat-resource-green.log'
```

Expected: both generator runs exit 0, the second-run log contains `Skipped existing` for every target, byte hashes are identical, both `.png` and `.wav` sentinel cases remain byte-for-byte unchanged, and Pester plus EditMode and `GeneratedCombatResourceTests` pass. `GeneratedCombatResourceTests` proves `Resources.Load<Sprite>` returns Archer and Elite at 64 PPU after the create-only run.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Editor/CombatAssetGenerator.cs Assets/Editor/CombatAssetGenerator.cs.meta Assets/Editor/CombatAssetGeneratorTests.cs Assets/Editor/CombatAssetGeneratorTests.cs.meta Assets/Resources/Sprites/Enemies Assets/Resources/Sounds Assets/Scripts/Game/Visual/AiSpriteLoader.cs Assets/Tests/PlayMode/GeneratedCombatResourceTests.cs Assets/Tests/PlayMode/GeneratedCombatResourceTests.cs.meta tools/validation/CombatGeneratedAssets.Tests.ps1
git commit -m "feat: generate deterministic combat resources"
```

### Task 3: Publish Resource Gaps and Verify Combat Visual Evidence

**Files:**
- Create: `docs/combat-resource-gap-report.md`
- Verify: `Assets/Tests/PlayMode/BattleVisualEvidenceTests.cs`
- Verify: `Assets/Tests/PlayMode/BattleEnemyVisualEvidenceTests.cs`
- Verify: `Assets/Tests/PlayMode/BattleSettlementUiTests.cs`
- Verify: `Assets/Tests/PlayMode/GeneratedCombatResourceTests.cs`

**Interfaces:**
- Consumes: Tasks 1-2 audit/generator outputs and existing battle scenes.
- Produces: an actionable gap report plus fresh screenshots/pixel evidence.

- [ ] **Step 1: Write the exact gap report inventory**

The report table has these columns for every row:

```text
id | status | consumer | target | type/dimensions/import | gameplay state/fallback | generation or production steps | command | validation | license/provenance owner
```

Record generated rows for Archer and Elite. Record every generated SoundCatalog WAV row with the exact status `placeholder-generated`; this status means deterministic local placeholder audio and never licensed/imported audio. Record source-needed rows for:

- `Assets/Art/Characters/Player/Player.controller` plus idle/run/attack/hurt/death clips, 8-direction sprite sheets, 64 px per frame, Point/Clamp/no mipmaps.
- `Assets/Art/Enemies/{Grunt,Archer,Elite,Boss}/<Enemy>.controller` and state clips, matching current collider silhouettes and attack telegraphs.
- `Assets/Resources/Fonts/ZhetianUIFont.ttf`, Simplified Chinese UI glyph coverage, licensed embedding.
- Final licensed replacements under the exact `Assets/Resources/Sounds/<SoundCatalog suggestedFile>` paths; replacing generated WAVs must not change runtime code.

For sprite-sheet source rows, give a reproducible prompt describing transparent-background Chinese ink character sheets, orthographic readable silhouettes, consistent pivot, and no UI/text. The import command remains the `GenerateAll` batch command followed by Unity import and the listed PlayMode tests.

- [ ] **Step 2: Run asset/static and combat suites**

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/UnityAssetIntegrity.Tests.ps1,tools/validation/CombatGeneratedAssets.Tests.ps1 -EnableExit"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
$runStartedUtc = [DateTime]::UtcNow
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode `
  -testFilter 'Game.Tests.PlayMode.BattleCombatLoopTests;Game.Tests.PlayMode.BattleEnemyExperienceTests;Game.Tests.PlayMode.OnlineBattleCompletionTests;Game.Tests.PlayMode.BattleSettlementUiTests;Game.Tests.PlayMode.BattleVisualEvidenceTests;Game.Tests.PlayMode.BattleEnemyVisualEvidenceTests;Game.Tests.PlayMode.GeneratedCombatResourceTests' `
  -testResults 'Logs\combat-resource-playmode.xml' -logFile 'Logs\combat-resource-playmode.log'
$screenshots = @('phase-b1-combat.png', 'phase-b1-result.png', 'phase-b2-wave-combat.png', 'phase-b2-boss-telegraph.png', 'task-6-ui-pending.png', 'task-6-ui-saved.png', 'task-6-ui-failed.png')
foreach ($name in $screenshots) {
  $file = Get-Item (Join-Path 'Logs' $name) -ErrorAction Stop
  if ($file.LastWriteTimeUtc -le $runStartedUtc) { throw "Stale combat screenshot: $name" }
}
```

Expected: all tests pass; screenshots exist for exactly these seven outputs: `phase-b1-combat.png`, `phase-b1-result.png`, `phase-b2-wave-combat.png`, `phase-b2-boss-telegraph.png`, `task-6-ui-pending.png`, `task-6-ui-saved.png`, and `task-6-ui-failed.png`.

- [ ] **Step 3: Inspect screenshots and pixel assertions**

The Step 2 loop requires each of the following seven `Logs` files to exist and to have `LastWriteTimeUtc -gt $runStartedUtc`: `phase-b1-combat.png`, `phase-b1-result.png`, `phase-b2-wave-combat.png`, `phase-b2-boss-telegraph.png`, `task-6-ui-pending.png`, `task-6-ui-saved.png`, and `task-6-ui-failed.png`. This freshness check prevents approval of stale screenshots.

Open all seven fresh images under `Logs`. Confirm actual generated player/enemy assets render, the frame is nonblank, the player/enemies/telegraph are inside the viewport, and HUD/result text does not overlap. Existing tests must retain their pixel diversity, ROI delta, viewport, label-fit, and panel-overlap assertions.

- [ ] **Step 4: Run full Unity suites after any evidence fix**

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testResults Logs\combat-resource-editmode.xml -logFile Logs\combat-resource-editmode.log
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode -testResults Logs\combat-resource-full-playmode.xml -logFile Logs\combat-resource-full-playmode.log
git diff --check
$placeholderPattern = ('\bTO' + 'DO\b|\bT' + 'BD\b|imple' + 'ment later|fill in ' + 'details')
$placeholderMatches = Select-String -Path docs/superpowers/plans/2026-07-21-combat-resource-engineering.md -Pattern $placeholderPattern
if ($placeholderMatches) { throw "Plan placeholder scan failed: $placeholderMatches" }
```

Expected: full EditMode and PlayMode pass with no malformed YAML/meta or missing GUID; `git diff --check` emits no whitespace errors; the placeholder scan returns no matches.

- [ ] **Step 5: Commit**

```powershell
git add docs/combat-resource-gap-report.md Assets/Tests/PlayMode
git commit -m "docs: publish combat resource production gaps"
```

## Final Acceptance

- [ ] Static validation covers all required resource types and serialized GUIDs.
- [ ] The generator command is deterministic, idempotent, and succeeds in batch mode.
- [ ] The second generator run skips every existing PNG/WAV, preserves every hash, logs each skipped path, and the temp `.png`/`.wav` sentinel test proves `WriteIfMissing` never overwrites external content.
- [ ] Archer and Elite no longer reuse Grunt/Boss sprites.
- [ ] `ConfigureSpriteImporter` ends with `SaveAndReimport()`, and both generator validation and PlayMode tests prove Archer/Elite load via `Resources.Load<Sprite>` at 64 PPU.
- [ ] Every SoundCatalog filename resolves to a committed generated WAV until a licensed replacement is imported.
- [ ] Generated WAVs are documented and verified as generated placeholders, never represented as licensed imports.
- [ ] The gap report names every remaining source/licensing requirement with an exact path and reproducible production/import procedure.
- [ ] Battle loop, enemy experience, online completion, settlement UI, visual evidence, full EditMode, and full PlayMode tests pass.
- [ ] All seven fresh screenshots are nonblank, correctly framed, and free of incoherent UI overlap.

## Self-Review

- Spec coverage: all required asset types, all three exempt Unity GUIDs, create-only deterministic generation, existing-resource sentinel protection, real Sprite/AudioClip resource loading, missing-resource reporting, battle tests, seven fresh screenshots, pixel checks, and generation commands are covered.
- Placeholder scan: source-needed rows are explicit production deliverables with fixed targets, not deferred unnamed work; generated WAVs are expressly non-licensed placeholders.
- Type consistency: PNG settings and WAV format are fixed once and reused by generator, audit, and validation; Archer/Elite retain their imported 64-PPU Sprite metadata rather than recreating sprites at 100 PPU.
