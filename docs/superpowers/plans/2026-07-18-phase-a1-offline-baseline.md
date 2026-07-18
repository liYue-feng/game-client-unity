# Phase A1 Offline Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair Unity asset identity, make `BattleScene` execute its installer offline, and prove the scene creates its core battle objects without starting networking.

**Architecture:** A repository-level PowerShell validator provides deterministic checks even when Unity cannot start. The Unity scene keeps its current dynamic installer for A1; a PlayMode smoke test loads the real scene and verifies the minimum offline object graph. Network, Manager, gameplay, and UI architecture remain unchanged until later phases.

**Tech Stack:** Unity 2022.3.47f1, C#, Unity Test Framework 1.1.33, NUnit, PowerShell 5.1, Pester 3.4.0, Git.

## Global Constraints

- Modify only `E:/Own_project/game-client-unity`; keep `E:/client/zhetian_client/Unity` read-only.
- Do not copy company code, assets, private packages, configuration, XLua, or AssetBundle infrastructure.
- A1 does not redesign networking, migrate Managers, refactor combat behavior, or convert UI to Prefabs.
- Preserve the existing Unity editor version `2022.3.47f1`.
- Use `D:/Unity_Soft/2022/Editor/Unity.exe` for Unity commands.
- Unity batch verification is valid only when its log reaches project import and successful batch exit. The current baseline stops during licensing with `Access token is unavailable` and is not compile evidence.
- Write beginner-friendly Chinese comments that explain design reasons.
- Follow RED-GREEN-REFACTOR for every behavior change.
- Run `requesting-code-review` after each task and fix accepted findings before starting the next task.
- Commit each independently verified task. Push only after the final A1 verification.

---

## File Map

- `tools/validation/UnityAssetIntegrity.psm1`: Pure PowerShell scanner for duplicate meta GUIDs, invalid serialized script references, and missing build scenes.
- `tools/validation/UnityAssetIntegrity.Tests.ps1`: Fixture-based Pester coverage for valid and invalid Unity project layouts.
- `tools/validation/Test-UnityAssetIntegrity.ps1`: Human/CI command entry that prints actionable failures and returns process exit code 0 or 1.
- `Assets/Scenes/BattleScene.unity`: Replace the invalid `BattleSceneSetup` script GUID.
- `Assets/Resources/Sounds.meta`: Assign a unique directory GUID.
- `Assets/Resources/Sprites/Characters.meta`: Assign a unique directory GUID.
- `Assets/Scripts/Game/Weapons.meta`: Assign a unique directory GUID.
- `Packages/manifest.json`: Promote Unity Test Framework 1.1.33 to a direct project dependency and remove the unused Input System package that blocks PlayMode startup.
- `Packages/packages-lock.json`: Resolve the direct test dependency and remove the unused Input System dependency graph.
- `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef`: PlayMode test assembly independent of the predefined gameplay assembly.
- `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`: Loads the real scene and checks the offline battle object graph by GameObject name.
- `Assets/Tests.meta`, `Assets/Tests/PlayMode.meta`, and the two test-file `.meta` files: Unity-generated identities created during the successful import preflight.
- `Assets/Scripts/Game/BattleSceneSetup.cs`: Split creation and player-dependent initialization of `UpgradeManager` to remove the proven null dereference.
- `.claude/memory/project-overview.md`: Record the verified A1 result and remaining Phase A work.

---

### Task 1: Repository Asset Integrity Validator

**Files:**
- Create: `tools/validation/UnityAssetIntegrity.Tests.ps1`
- Create: `tools/validation/UnityAssetIntegrity.psm1`
- Create: `tools/validation/Test-UnityAssetIntegrity.ps1`

**Interfaces:**
- Produces: `Test-UnityAssetIntegrity -ProjectRoot <string>` returning an object with `IsValid`, `DuplicateGuids`, `InvalidScriptReferences`, and `MissingBuildScenes`.
- Produces: `tools/validation/Test-UnityAssetIntegrity.ps1 -ProjectRoot <path>` with exit code 0 for a valid project and 1 for integrity failures.

- [x] **Step 1: Write the failing Pester tests**

Create `tools/validation/UnityAssetIntegrity.Tests.ps1`:

```powershell
$modulePath = Join-Path $PSScriptRoot 'UnityAssetIntegrity.psm1'
Import-Module $modulePath -Force

Describe 'Test-UnityAssetIntegrity' {
    BeforeEach {
        $projectRoot = Join-Path $TestDrive 'Project'
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
}
```

- [x] **Step 2: Run the tests and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"
```

Expected: FAIL during `Import-Module` because `UnityAssetIntegrity.psm1` does not exist.

- [x] **Step 3: Implement the minimum scanner**

Create `tools/validation/UnityAssetIntegrity.psm1`:

```powershell
Set-StrictMode -Version Latest

function Test-UnityAssetIntegrity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $assetsRoot = Join-Path $resolvedRoot 'Assets'
    $guidPattern = '^guid:\s*([0-9a-fA-F]{32})\s*$'
    $scriptPattern = 'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})'

    $metaRecords = foreach ($metaFile in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Filter '*.meta') {
        $match = Select-String -LiteralPath $metaFile.FullName -Pattern $guidPattern | Select-Object -First 1
        if ($null -ne $match) {
            [PSCustomObject]@{
                Guid = $match.Matches[0].Groups[1].Value.ToLowerInvariant()
                Path = $metaFile.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
            }
        }
    }

    $duplicateGuids = @($metaRecords |
        Group-Object Guid |
        Where-Object Count -gt 1 |
        ForEach-Object {
            [PSCustomObject]@{
                Guid = $_.Name
                Paths = @($_.Group.Path | Sort-Object)
            }
        })

    $metaByGuid = @{}
    foreach ($record in $metaRecords) {
        if (-not $metaByGuid.ContainsKey($record.Guid)) {
            $metaByGuid[$record.Guid] = @()
        }
        $metaByGuid[$record.Guid] += $record.Path
    }

    $serializedAssets = Get-ChildItem -LiteralPath $assetsRoot -Recurse -File |
        Where-Object { $_.Extension -in '.unity', '.prefab', '.asset' }
    $invalidScriptReferences = foreach ($asset in $serializedAssets) {
        foreach ($match in Select-String -LiteralPath $asset.FullName -Pattern $scriptPattern) {
            $guid = $match.Matches[0].Groups[1].Value.ToLowerInvariant()
            $targets = if ($metaByGuid.ContainsKey($guid)) { @($metaByGuid[$guid]) } else { @() }
            $scriptTargets = @($targets | Where-Object { $_ -like '*.cs.meta' })
            if ($scriptTargets.Count -ne 1 -or $targets.Count -ne 1) {
                [PSCustomObject]@{
                    AssetPath = $asset.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                    Line = $match.LineNumber
                    Guid = $guid
                    Targets = $targets
                }
            }
        }
    }

    $buildSettingsPath = Join-Path $resolvedRoot 'ProjectSettings/EditorBuildSettings.asset'
    $missingBuildScenes = @()
    if (Test-Path -LiteralPath $buildSettingsPath) {
        $missingBuildScenes = @(Select-String -LiteralPath $buildSettingsPath -Pattern '^\s*path:\s*(.+?)\s*$' |
            ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() } |
            Where-Object { -not (Test-Path -LiteralPath (Join-Path $resolvedRoot $_)) })
    }

    [PSCustomObject]@{
        IsValid = $duplicateGuids.Count -eq 0 -and @($invalidScriptReferences).Count -eq 0 -and $missingBuildScenes.Count -eq 0
        DuplicateGuids = $duplicateGuids
        InvalidScriptReferences = @($invalidScriptReferences)
        MissingBuildScenes = $missingBuildScenes
    }
}

Export-ModuleMember -Function Test-UnityAssetIntegrity
```

Create `tools/validation/Test-UnityAssetIntegrity.ps1`:

```powershell
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

Import-Module (Join-Path $PSScriptRoot 'UnityAssetIntegrity.psm1') -Force
$result = Test-UnityAssetIntegrity -ProjectRoot $ProjectRoot

foreach ($duplicate in $result.DuplicateGuids) {
    Write-Error "Duplicate GUID $($duplicate.Guid): $($duplicate.Paths -join ', ')"
}
foreach ($reference in $result.InvalidScriptReferences) {
    Write-Error "Invalid m_Script reference $($reference.Guid) at $($reference.AssetPath):$($reference.Line); targets: $($reference.Targets -join ', ')"
}
foreach ($scene in $result.MissingBuildScenes) {
    Write-Error "Build scene does not exist: $scene"
}

if (-not $result.IsValid) {
    exit 1
}

Write-Output 'Unity asset integrity check passed.'
exit 0
```

- [x] **Step 4: Run the tests and verify GREEN**

Run the same Pester command from Step 2.

Expected: `Tests Passed: 4, Failed: 0` and process exit code 0.

- [x] **Step 5: Validate script formatting and commit**

Run:

```powershell
git diff --check
git add -- tools/validation/UnityAssetIntegrity.psm1 tools/validation/UnityAssetIntegrity.Tests.ps1 tools/validation/Test-UnityAssetIntegrity.ps1
git commit -m "test: 增加 Unity 资源完整性检查"
```

Expected: commit succeeds with only the three validation files.

---

### Task 2: Repair Asset GUIDs and Scene Script Identity

**Files:**
- Modify: `Assets/Scenes/BattleScene.unity:259`
- Modify: `Assets/Resources/Sounds.meta:2`
- Modify: `Assets/Resources/Sprites/Characters.meta:2`
- Modify: `Assets/Scripts/Game/Weapons.meta:2`

**Interfaces:**
- Consumes: `tools/validation/Test-UnityAssetIntegrity.ps1` from Task 1.
- Produces: A project with unique asset GUIDs and one valid `BattleSceneSetup` script reference.

- [x] **Step 1: Run the repository validator and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
```

Expected: exit code 1. Output reports duplicate GUID `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6` and an invalid `m_Script` reference in `Assets/Scenes/BattleScene.unity:259`.

- [x] **Step 2: Replace the duplicate directory GUIDs**

Set the `guid:` line in each file to the exact value below:

```text
Assets/Resources/Sounds.meta
guid: 6f842b14ea9c4dcaa4583b9d755b3230

Assets/Resources/Sprites/Characters.meta
guid: 04db0ca9c52d48178982513773775b66

Assets/Scripts/Game/Weapons.meta
guid: 531c314bfb2944b8a565476385337aec
```

- [x] **Step 3: Point the scene component to `BattleSceneSetup.cs.meta`**

Replace the `m_Script` line in `Assets/Scenes/BattleScene.unity` with:

```yaml
  m_Script: {fileID: 11500000, guid: 534dec71e1d54924aba9bbd4233d1f93, type: 3}
```

- [x] **Step 4: Run the repository validator and verify GREEN**

Run the same command from Step 1.

Expected: `Unity asset integrity check passed.` and process exit code 0.

- [x] **Step 5: Commit the asset repair**

```powershell
git diff --check
git add -- Assets/Scenes/BattleScene.unity Assets/Resources/Sounds.meta Assets/Resources/Sprites/Characters.meta Assets/Scripts/Game/Weapons.meta
git commit -m "fix: 修复 Unity 资源 GUID 与场景脚本引用"
```

Expected: commit succeeds with exactly four asset serialization files.

---

### Task 3: Offline Battle PlayMode Smoke Test

> Execution note: The first PlayMode RED run exposed an unused `com.unity.inputsystem` editor-initialization failure before scene loading. Source search confirmed all active input code uses `UnityEngine.Input`; Task 3 therefore removes that unused package and defers a correctly configured Input System to the later platform-input phase.
>
> Subsequent RED runs exposed and fixed four additional baseline blockers: invalid TagManager serialization and missing gameplay tags, the obsolete `m_ActiveInputHandler` property name, `Hitbox` being added before its required Collider2D, and `WaveSpawner` marking an empty pre-configuration pool scan as complete. These are required for the real scene smoke test to reach GREEN.

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Packages/packages-lock.json`
- Create: `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef`
- Create: `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`
- Modify: `Assets/Scripts/Game/BattleSceneSetup.cs:45-70,343-369`
- Modify: `Assets/Scripts/Game/Dungeon/WaveSpawner.cs:44-72`
- Modify: `ProjectSettings/TagManager.asset`
- Modify: `ProjectSettings/ProjectSettings.asset`
- Modify: `.claude/memory/project-overview.md`

**Interfaces:**
- Consumes: Build scene `BattleScene` and corrected `BattleSceneSetup` reference from Task 2.
- Produces: `BattleSceneOfflineSmokeTests.BattleSceneStartsOfflineAndCreatesCoreObjects()`.
- Produces: `BattleSceneSetup.InitializeUpgradeManager()` which performs player-dependent initialization after `CreatePlayer()`.

- [x] **Step 1: Promote the test framework to a direct dependency**

Replace the unused `com.unity.inputsystem` dependency in `Packages/manifest.json` with:

```json
"com.unity.test-framework": "1.1.33",
```

In `Packages/packages-lock.json`, remove the `com.unity.inputsystem` entry, change `com.unity.test-framework.depth` from `3` to `0`, and accept Unity Package Manager's recalculated depths for its remaining transitive dependencies.

- [x] **Step 2: Write the PlayMode smoke test**

Create `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef`:

```json
{
  "name": "Game.PlayModeTests",
  "rootNamespace": "Game.Tests.PlayMode",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [
    "UNITY_INCLUDE_TESTS"
  ],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
```

Create `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 验证真实战斗场景可以在不启动联网流程的情况下完成最小初始化。
    /// </summary>
    public sealed class BattleSceneOfflineSmokeTests
    {
        /// <summary>
        /// 加载 Build Settings 中的战斗场景并检查核心对象。
        /// </summary>
        /// <returns>等待场景和延迟一帧初始化完成的枚举器。</returns>
        [UnityTest]
        public IEnumerator BattleSceneStartsOfflineAndCreatesCoreObjects()
        {
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.That(GameObject.Find("Ground"), Is.Not.Null, "战斗场景必须创建地面");
            Assert.That(GameObject.Find("Player"), Is.Not.Null, "战斗场景必须创建玩家");
            Assert.That(GameObject.Find("WaveSpawner"), Is.Not.Null, "战斗场景必须创建刷怪器");
            Assert.That(GameObject.Find("[BattleHUD]"), Is.Not.Null, "战斗场景必须创建战斗 HUD");
            Assert.That(GameObject.Find("[NetworkClient]"), Is.Null, "离线场景不得创建网络客户端");
            Assert.That(GameObject.Find("[LoginManager]"), Is.Null, "离线场景不得启动登录流程");
            Assert.That(GameObject.Find("[GameBootstrap]"), Is.Null, "离线场景不得启动在线 Bootstrap");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
```

- [x] **Step 3: Run Unity licensing and import preflight**

Run:

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'E:\Own_project\game-client-unity' -logFile 'E:\Own_project\game-client-unity\Logs\A1-preflight.log'
$log = Get-Content -Raw 'Logs\A1-preflight.log'
if ($log -notmatch 'Exiting batchmode successfully') { throw 'Unity did not complete project import; inspect Logs/A1-preflight.log and restore the Unity Hub license before continuing.' }
```

Expected: the log contains a successful project import and `Exiting batchmode successfully`. If it still ends at `Access token is unavailable`, stop Unity-dependent steps and restore the license through Unity Hub; do not treat exit code 0 as success.

- [x] **Step 4: Run the PlayMode test and verify RED**

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.BattleSceneOfflineSmokeTests' -testResults 'E:\Own_project\game-client-unity\Logs\A1-playmode-red.xml' -logFile 'E:\Own_project\game-client-unity\Logs\A1-playmode-red.log'
```

Expected: FAIL. `BattleSceneSetup.Start()` throws because `CreateUpgradeManager()` accesses `_player` before `CreatePlayer()` assigns it; the object assertions therefore do not all pass.

- [x] **Step 5: Separate creation from player-dependent initialization**

Change the relevant part of `BattleSceneSetup.Start()` to:

```csharp
CreateCamera();
CreateGround();
CreateUpgradeManager();
CreatePlayer();
InitializeUpgradeManager();
_inputMediator = _player.GetComponent<InputMediator>();
```

Keep `CreateUpgradeManager()` responsible only for creating the component:

```csharp
/// <summary>
/// 创建升级管理器，使玩家创建期间的武器系统能够找到它。
/// </summary>
private void CreateUpgradeManager()
{
    var managerObj = new GameObject("UpgradeManager");
    _upgradeManager = managerObj.AddComponent<UpgradeManager>();
}
```

Move the existing player-dependent code into this new method without changing its behavior:

```csharp
/// <summary>
/// 在玩家创建完成后绑定角色属性和升级追踪。
/// </summary>
private void InitializeUpgradeManager()
{
    var stats = _player.GetComponent<CharacterStats>();
    if (stats != null)
    {
        _upgradeManager.Initialize(stats);
    }

    _upgradeManager.OnBeforeGenerateOptions = options => { };
    Inventory.Instance.OnItemChanged += (slot, item) =>
    {
        if (item == null) return;
        if (item.category.Contains("elemental") || item.id.StartsWith("elem_"))
        {
            _elementalUpgradeCount = Mathf.Max(_elementalUpgradeCount, CountCategoryInInventory("elemental"));
        }
        if (item.category.Contains("summon") || item.id.StartsWith("summon_"))
        {
            _summonUpgradeCount = Mathf.Max(_summonUpgradeCount, CountCategoryInInventory("summon"));
        }
    };
}
```

- [x] **Step 6: Run the focused PlayMode test and verify GREEN**

Repeat the Unity command from Step 4 with result file `Logs/A1-playmode-green.xml` and log file `Logs/A1-playmode-green.log`.

Expected: test result XML reports 1 passed, 0 failed; the log contains no compilation error or unhandled exception.

- [x] **Step 7: Update project memory with the verified A1 state**

In `.claude/memory/project-overview.md`, replace the two resolved issue bullets with:

```markdown
- Phase A1 已修复战斗场景脚本引用和重复资源 GUID，并由自动资源检查验证
- `BattleScene` 已通过离线 PlayMode 冒烟测试，可创建地面、玩家、刷怪器和战斗 HUD，且不会启动网络或登录流程
```

Replace the stale test-status bullet with:

```markdown
- 当前仍没有 Prefab；A1 已建立 PlayMode 测试程序集，网络和玩法的完整自动测试将在后续阶段补齐
```

Keep the remaining network and architecture risks unchanged.

- [x] **Step 8: Commit the offline baseline**

```powershell
git diff --check
git status --short
git add -- Packages/manifest.json Packages/packages-lock.json Assets/Tests Assets/Scripts/Game/BattleSceneSetup.cs .claude/memory/project-overview.md
git commit -m "test: 建立战斗场景离线运行基线"
```

Expected: commit includes the direct test dependency, generated test `.meta` files, PlayMode test, initialization-order fix, and project-memory update. Do not commit `Library`, `Logs`, `Temp`, or test-result XML files.

---

### Task 4: A1 Full Verification and Delivery

**Files:**
- Verify only; no source file is created by this task.

**Interfaces:**
- Consumes: Validator, repaired assets, and PlayMode smoke test from Tasks 1-3.
- Produces: Verified A1 commits on the remote branch.

- [x] **Step 1: Run the complete non-Unity validation**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
```

Expected: 5 Pester tests pass and the repository integrity command prints `Unity asset integrity check passed.`

- [x] **Step 2: Run a fresh Unity compile/import check**

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'E:\Own_project\game-client-unity' -logFile 'E:\Own_project\game-client-unity\Logs\A1-final-compile.log'
```

Expected: successful batch exit after project import, with no `error CS` or `Scripts have compiler errors` in the log.

- [x] **Step 3: Run the complete PlayMode test assembly**

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform PlayMode -testResults 'E:\Own_project\game-client-unity\Logs\A1-final-playmode.xml' -logFile 'E:\Own_project\game-client-unity\Logs\A1-final-playmode.log'
```

Expected: all PlayMode tests pass with 0 failures.

- [x] **Step 4: Verify repository state and commit history**

```powershell
git diff --check
git status --short --branch
git log -4 --oneline
```

Expected: no uncommitted tracked files; the three A1 commits are present after the design commit.

- [x] **Step 5: Push and verify the remote**

```powershell
git push origin HEAD
git rev-parse HEAD
git rev-parse origin/master
```

Expected: push succeeds and both revisions are identical when implementing directly on `master`. If execution uses an isolated worktree branch, finish through `finishing-a-development-branch` instead of pushing that branch as `master` without review.
