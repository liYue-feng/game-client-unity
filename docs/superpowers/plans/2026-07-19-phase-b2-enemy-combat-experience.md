# Phase B2 Enemy Combat Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a deterministic offline enemy-combat vertical slice with reachable waves, bounded camera tracking, readable and cancellable attacks, accurate resolved-hit feedback, scene-owned ink effects, and event-driven Wave/Boss HUD.

**Architecture:** Pure arena, wave-scaling, attack-plan, hit-outcome, lease, and HUD-formatting rules extend the existing `Game.Gameplay` assembly. Existing scene-owned MonoBehaviours adapt those rules without replacing B1 combat/time/run authority, changing the generic ObjectPool activation API, or importing the company project's frameworks.

**Tech Stack:** Unity 2022.3 LTS, C#, NUnit EditMode/PlayMode tests, Unity Legacy Input, programmatic SpriteRenderer/LineRenderer/UI, PowerShell asset integrity and Pester gates.

## Global Constraints

- Delivery base is design commit `e64475b543bb37d2cf3c3becdb4f78e9c109f5bf`; execute in an isolated worktree on branch `feature/phase-b2-enemy-combat-experience`.
- Unity executable is `D:\Unity_Soft\2022\Editor\Unity.exe`. Never add `-quit` to XML-producing test commands on this machine.
- Unity runs are serial: delete stale result/log files, launch one process, poll until `</test-run>` exists, verify failed count, then wait for every Unity process to exit before starting the next run.
- Preserve `Hurtbox.ReceiveHit(CombatHit) -> CombatHitResult`; production callers migrate to `ResolveHit(CombatHit) -> CombatHitOutcome` while ReceiveHit remains a delegating compatibility surface.
- Do not change the generic `ObjectPool.Get/Return` activation contract. `WaveSpawner` calls `PrepareForSpawn` in the same stack before the first physics step or Update.
- Reuse `Game.Gameplay`, `CombatEvents`, `BossHPBar`, `CameraShaker`, `HitStopController`, `DamageNumberPool`, and the B1 BattleRun/TimeScale authorities.
- Do not copy company code, paths, Lua, events, pools, shaders, configuration tables, or resources.
- A4 Online/MainMenu and Phase C Prefab, Animator, Addressables/AssetBundle, resource-cache, and generic-pool engineering are out of scope.
- Every task follows RED -> minimal GREEN -> focused verification -> specification review -> code-quality review -> final focused/full verification -> one commit -> one push -> exact local/remote SHA equality.
- Specification review must pass before code-quality review starts. Fix all Critical/Important findings before committing.

## Serial Unity Test Command

Open PowerShell in the feature worktree and define this function once per shell. Every Unity command in this plan calls it; do not launch a second Unity run while it is polling or while a Unity process remains.

```powershell
function Invoke-UnityTests {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('EditMode', 'PlayMode')]
        [string]$Platform,
        [string]$Filter = '',
        [Parameter(Mandatory = $true)]
        [string]$ResultPath,
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [switch]$ExpectFailure,
        [string]$ExpectedFailureMessage = '',
        [int]$TimeoutMinutes = 10
    )

    $unity = 'D:\Unity_Soft\2022\Editor\Unity.exe'
    $project = (git rev-parse --show-toplevel).Trim()
    $result = Join-Path $project $ResultPath
    $log = Join-Path $project $LogPath

    if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
        throw 'A Unity process is already running. Finish or close it before starting a serial test run.'
    }

    New-Item -ItemType Directory -Force (Split-Path $result) | Out-Null
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    Remove-Item -LiteralPath $result, $log -Force -ErrorAction SilentlyContinue

    $arguments = @(
        '-batchmode',
        '-projectPath', $project,
        '-runTests',
        '-testPlatform', $Platform,
        '-testResults', $result,
        '-logFile', $log
    )
    if ($Filter) {
        $arguments += @('-testFilter', $Filter)
    }

    $process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path -LiteralPath $result) -and
            (Select-String -LiteralPath $result -SimpleMatch '</test-run>' -Quiet)) {
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not (Test-Path -LiteralPath $result) -or
        -not (Select-String -LiteralPath $result -SimpleMatch '</test-run>' -Quiet)) {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 120 }
        throw "Unity did not produce complete XML: $ResultPath"
    }

    while ((Get-Process -Name Unity -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
    if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
        throw 'Unity completed XML but did not exit before the serial-run deadline.'
    }

    [xml]$document = Get-Content -LiteralPath $result -Raw
    $run = $document.'test-run'
    $failed = [int]$run.failed
    if ($ExpectFailure) {
        if ($failed -eq 0) { throw "Expected RED but run passed: $ResultPath" }
        if ($ExpectedFailureMessage -and
            -not (Select-String -LiteralPath $result -SimpleMatch $ExpectedFailureMessage -Quiet)) {
            throw "RED failed for an unexpected reason; missing marker '$ExpectedFailureMessage' in $ResultPath"
        }
    } elseif ($failed -ne 0) {
        throw "Unity tests failed ($failed failures): $ResultPath"
    }

    [pscustomobject]@{
        Result = [string]$run.result
        Total = [int]$run.testcasecount
        Passed = [int]$run.passed
        Failed = $failed
        Xml = $ResultPath
        Log = $LogPath
    }
}
```

## Baseline Gate

- [x] Create the isolated worktree, confirm the exact base, and define the serial runner.

```powershell
git worktree add .worktrees/phase-b2-enemy-combat-experience -b feature/phase-b2-enemy-combat-experience e64475b543bb37d2cf3c3becdb4f78e9c109f5bf
Set-Location .worktrees/phase-b2-enemy-combat-experience
git status --short --branch
git rev-parse HEAD
```

Expected: clean feature branch and exact HEAD `e64475b543bb37d2cf3c3becdb4f78e9c109f5bf`.

- [x] Run asset integrity, Pester, full EditMode, and full PlayMode serially.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-baseline-editmode.xml' -LogPath 'Logs/B2-baseline-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-baseline-playmode.xml' -LogPath 'Logs/B2-baseline-playmode.log'
```

Expected: asset integrity passes, Pester `5/5`, EditMode `111/111`, PlayMode `47/47`. Stop and explain any drift before Task 1.

## Per-Task Review And Push Gate

After each task's GREEN implementation, use a fresh specification reviewer against that task and the committed design. Only after specification PASS, use a fresh code-quality reviewer. Fix every Critical/Important finding, repeat the affected focused tests, and obtain both PASS verdicts before the task commit.

Each task then makes exactly its listed commit and one push. Verify the remote branch SHA instead of trusting push output:

```powershell
git push -u origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Local/remote mismatch: $local != $remote" }
Write-Output "verified=$local"
```

---

### Task 1: Add Pure Arena And Immutable Wave-Scaling Models

**Files:**
- Create: `Assets/Scripts/Gameplay/BattleArena.cs`
- Create: `Assets/Scripts/Gameplay/BattleArena.cs.meta`
- Create: `Assets/Scripts/Gameplay/EnemyWaveScaling.cs`
- Create: `Assets/Scripts/Gameplay/EnemyWaveScaling.cs.meta`
- Create: `Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs`
- Create: `Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs.meta`

**Interfaces:**
- Produces: `BattleArenaBounds(float minX, float maxX)`, `ArenaSpawnSide`, and `ArenaSpawnPlanner.PlanX(BattleArenaBounds, float playerX, ArenaSpawnSide, float cameraHalfWidth, float spawnMargin, float chaseRange)`.
- Produces: immutable `EnemyStatBaseline`, `EnemyWaveMultipliers`, `EnemyWaveStats`, and `EnemyWaveScaling.Calculate(EnemyStatBaseline, int waveIndex, EnemyWaveMultipliers)`.
- Constraint: these types stay under `Game.Gameplay`; they contain no MonoBehaviour, scene lookup, mutable static state, or company-framework dependency.

- [x] **Step 1: Write a behavioral RED for the current compounding formula**

Create `EnemyExperienceCoreTests.cs` with a reflection-based characterization test that compiles before the new core APIs exist:

```csharp
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    public sealed class EnemyExperienceCoreTests
    {
        [Test]
        public void ApplyingTheSameWaveTwiceMustNotCompoundEnemyStats()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(candidate => candidate.GetType("WaveSpawner") != null);
            var spawnerType = assembly.GetType("WaveSpawner");
            var gruntType = assembly.GetType("Grunt");
            var spawnerObject = new GameObject("B2_RED_Spawner");
            var enemyObject = new GameObject("B2_RED_Grunt");
            try
            {
                var spawner = spawnerObject.AddComponent(spawnerType);
                var enemy = enemyObject.AddComponent(gruntType);
                gruntType.GetField("maxHp").SetValue(enemy, 100);
                gruntType.GetField("hp").SetValue(enemy, 100);
                gruntType.GetField("damage").SetValue(enemy, 10);
                gruntType.GetField("moveSpeed").SetValue(enemy, 2f);
                var scale = spawnerType.GetMethod("ApplyWaveScaling", BindingFlags.Instance | BindingFlags.NonPublic);

                scale.Invoke(spawner, new object[] { enemy, 1 });
                var firstHp = (int)gruntType.GetField("maxHp").GetValue(enemy);
                scale.Invoke(spawner, new object[] { enemy, 1 });
                var secondHp = (int)gruntType.GetField("maxHp").GetValue(enemy);

                Assert.That(secondHp, Is.EqualTo(firstHp),
                    "The same wave must be calculated from one immutable baseline, not the previous spawn.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(spawnerObject);
            }
        }
    }
}
```

- [x] **Step 2: Run focused EditMode RED**

```powershell
Invoke-UnityTests -Platform EditMode -Filter 'Game.Tests.EditMode.Gameplay.EnemyExperienceCoreTests' -ResultPath 'Logs/B2-task1-red.xml' -LogPath 'Logs/B2-task1-red.log' -ExpectFailure
```

Expected: one assertion failure showing the second wave-1 HP is larger than the first. The RED must be this observed compounding behavior, not a missing-type compiler error.

- [x] **Step 3: Implement the minimal pure arena model**

Implement normalized bounds and a deterministic fallback. Reachability and arena bounds outrank staying off-screen:

```csharp
namespace Game.Gameplay
{
    public enum ArenaSpawnSide { Left = -1, Right = 1 }

    public readonly struct BattleArenaBounds
    {
        public BattleArenaBounds(float minX, float maxX)
        {
            MinX = System.Math.Min(minX, maxX);
            MaxX = System.Math.Max(minX, maxX);
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float Width => MaxX - MinX;
    }

    public static class ArenaSpawnPlanner
    {
        public static float PlanX(
            BattleArenaBounds bounds,
            float playerX,
            ArenaSpawnSide preferredSide,
            float cameraHalfWidth,
            float spawnMargin,
            float chaseRange)
        {
            var margin = System.Math.Max(0f, spawnMargin);
            var safeMin = bounds.MinX + margin;
            var safeMax = bounds.MaxX - margin;
            if (safeMin > safeMax) return bounds.CenterX;

            playerX = System.Math.Max(safeMin, System.Math.Min(safeMax, playerX));
            var reachable = System.Math.Max(0f, chaseRange);
            var desired = System.Math.Min(reachable,
                System.Math.Max(0f, cameraHalfWidth) + margin);
            var requiredSeparation = System.Math.Min(reachable,
                System.Math.Max(0f, cameraHalfWidth));

            float Candidate(ArenaSpawnSide side) => System.Math.Max(
                safeMin,
                System.Math.Min(safeMax, playerX + (int)side * desired));
            bool Satisfies(float candidate, ArenaSpawnSide side)
            {
                var delta = candidate - playerX;
                return System.Math.Sign(delta) == (int)side &&
                    System.Math.Abs(delta) >= requiredSeparation - 0.001f &&
                    System.Math.Abs(delta) <= reachable + 0.001f;
            }

            var preferred = Candidate(preferredSide);
            var opposite = Candidate(preferredSide == ArenaSpawnSide.Left
                ? ArenaSpawnSide.Right
                : ArenaSpawnSide.Left);
            if (Satisfies(preferred, preferredSide)) return preferred;
            var oppositeSide = preferredSide == ArenaSpawnSide.Left
                ? ArenaSpawnSide.Right
                : ArenaSpawnSide.Left;
            if (Satisfies(opposite, oppositeSide)) return opposite;
            return System.Math.Abs(opposite - playerX) > System.Math.Abs(preferred - playerX)
                ? opposite
                : preferred;
        }
    }
}
```

- [x] **Step 4: Implement immutable wave scaling**

Use readonly values and calculate every spawn from baseline:

```csharp
namespace Game.Gameplay
{
    public readonly struct EnemyStatBaseline
    {
        public EnemyStatBaseline(int maxHp, int damage, float moveSpeed,
            float damageReduction, float telegraphDuration, float attackDuration)
        {
            MaxHp = System.Math.Max(1, maxHp);
            Damage = System.Math.Max(0, damage);
            MoveSpeed = System.Math.Max(0f, moveSpeed);
            DamageReduction = System.Math.Max(0f, System.Math.Min(1f, damageReduction));
            TelegraphDuration = System.Math.Max(0f, telegraphDuration);
            AttackDuration = System.Math.Max(0f, attackDuration);
        }

        public int MaxHp { get; }
        public int Damage { get; }
        public float MoveSpeed { get; }
        public float DamageReduction { get; }
        public float TelegraphDuration { get; }
        public float AttackDuration { get; }
    }

    public readonly struct EnemyWaveMultipliers
    {
        public EnemyWaveMultipliers(float hp, float damage, float speed)
        {
            Hp = System.Math.Max(0f, hp);
            Damage = System.Math.Max(0f, damage);
            Speed = System.Math.Max(0f, speed);
        }
        public float Hp { get; }
        public float Damage { get; }
        public float Speed { get; }
    }

    public readonly struct EnemyWaveStats
    {
        public EnemyWaveStats(int maxHp, int damage, float moveSpeed)
        { MaxHp = maxHp; Damage = damage; MoveSpeed = moveSpeed; }
        public int MaxHp { get; }
        public int Damage { get; }
        public float MoveSpeed { get; }
    }

    public static class EnemyWaveScaling
    {
        public static EnemyWaveStats Calculate(
            EnemyStatBaseline baseline, int waveIndex, EnemyWaveMultipliers multipliers)
        {
            var wave = System.Math.Max(0, waveIndex);
            return new EnemyWaveStats(
                System.Math.Max(1, (int)System.Math.Round(baseline.MaxHp * System.Math.Pow(multipliers.Hp, wave))),
                System.Math.Max(0, (int)System.Math.Round(baseline.Damage * System.Math.Pow(multipliers.Damage, wave))),
                baseline.MoveSpeed * (float)System.Math.Pow(multipliers.Speed, wave));
        }
    }
}
```

- [x] **Step 5: Replace the RED fixture with permanent typed core tests**

Keep the same test class, remove the temporary reflection test, and cover idempotence plus arena fallbacks:

```csharp
[Test]
public void WaveScalingDependsOnBaselineAndWaveOnly()
{
    var baseline = new EnemyStatBaseline(100, 10, 2f, 0f, 0.5f, 0.3f);
    var multipliers = new EnemyWaveMultipliers(1.15f, 1.1f, 1.05f);
    var first = EnemyWaveScaling.Calculate(baseline, 3, multipliers);
    var afterArbitraryReuse = EnemyWaveScaling.Calculate(baseline, 3, multipliers);
    Assert.That(afterArbitraryReuse.MaxHp, Is.EqualTo(first.MaxHp));
    Assert.That(afterArbitraryReuse.Damage, Is.EqualTo(first.Damage));
    Assert.That(afterArbitraryReuse.MoveSpeed, Is.EqualTo(first.MoveSpeed).Within(0.0001f));
}

[TestCase(-15f, 15f, 0f, ArenaSpawnSide.Right, 8.5f)]
[TestCase(-15f, 15f, 14f, ArenaSpawnSide.Right, 5.5f)]
[TestCase(-3f, 3f, 0f, ArenaSpawnSide.Left, 10f)]
public void SpawnPlannerReturnsAnInBoundsReachablePoint(
    float min, float max, float playerX, ArenaSpawnSide side, float cameraHalfWidth)
{
    var bounds = new BattleArenaBounds(min, max);
    var spawnX = ArenaSpawnPlanner.PlanX(bounds, playerX, side, cameraHalfWidth, 0.5f, 8f);
    Assert.That(spawnX, Is.InRange(bounds.MinX + 0.5f, bounds.MaxX - 0.5f));
    Assert.That(System.Math.Abs(spawnX - playerX), Is.LessThanOrEqualTo(8.001f));
}
```

- [x] **Step 6: Run focused GREEN and full EditMode**

```powershell
Invoke-UnityTests -Platform EditMode -Filter 'Game.Tests.EditMode.Gameplay.EnemyExperienceCoreTests' -ResultPath 'Logs/B2-task1-green.xml' -LogPath 'Logs/B2-task1-green.log'
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-task1-full-editmode.xml' -LogPath 'Logs/B2-task1-full-editmode.log'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
git diff --check
```

Expected: focused tests pass, full EditMode is baseline `111` plus the new tests with zero failures, asset integrity passes, and diff check is clean.

- [x] **Step 7: Run specification review, then code-quality review**

Specification reviewer checks exact planner priority, immutable baseline behavior, normalization, namespace, and Phase C exclusions. After specification PASS, quality reviewer checks value semantics, numeric boundaries, test independence, and absence of Unity scene dependencies.

- [x] **Step 8: Commit once and push once**

```powershell
git add Assets/Scripts/Gameplay/BattleArena.cs Assets/Scripts/Gameplay/BattleArena.cs.meta Assets/Scripts/Gameplay/EnemyWaveScaling.cs Assets/Scripts/Gameplay/EnemyWaveScaling.cs.meta Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs.meta
git commit -m "feat: add deterministic enemy experience models"
git push -u origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 1 push mismatch" }
```

**Commit:** `feat: add deterministic enemy experience models`

### Task 2: Integrate Reachable Spawns, Pool Reset, And Camera Rig

**Files:**
- Create: `Assets/Scripts/Game/Visual/BattleCameraRig.cs`
- Create: `Assets/Scripts/Game/Visual/BattleCameraRig.cs.meta`
- Modify: `Assets/Scripts/Game/BattleSceneSetup.cs`
- Modify: `Assets/Scripts/Game/BattleRunController.cs`
- Modify: `Assets/Scripts/Game/Dungeon/WaveSpawner.cs`
- Modify: `Assets/Scripts/Game/Enemy/EnemyBase.cs`
- Modify: `Assets/Scripts/Game/Enemy/Archer.cs`
- Modify: `Assets/Scripts/Game/Enemy/Elite.cs`
- Modify: `Assets/Scripts/Game/Enemy/Boss.cs`
- Create: `Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs`
- Create: `Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs.meta`

**Interfaces:**
- Consumes: Task 1 `BattleArenaBounds`, `ArenaSpawnPlanner`, `EnemyStatBaseline`, `EnemyWaveMultipliers`, `EnemyWaveStats`, and `EnemyWaveScaling.Calculate`.
- Produces: `EnemyBase.InitializeCombatBaseline()`, `EnemyBase.PrepareForSpawn(EnemyWaveStats)`, protected `ResetSubclassState()`, and read-only `Baseline`.
- Produces: `WaveSpawner.ConfigureArena(BattleArenaBounds bounds, Transform player, Camera camera)` and `EnemySpawnEntry.preferredSide`.
- Produces: `BattleCameraRig.Configure(Transform target, BattleArenaBounds bounds, Camera camera)`, `SetFollowEnabled(bool)`, and `IsFollowing`.
- Constraint: do not modify `Assets/Scripts/Game/Combat/ObjectPool.cs`; current Enemy types must finish preparation before their first physics step/Update and must not add runtime-state logic to `OnEnable`.

- [x] **Step 1: Write deterministic PlayMode RED tests against current spawn and camera behavior**

Create `BattleEnemyExperienceTests.cs`. Include local reflection helpers (`LoadBattleScene`, `FindActiveSceneComponent`, `GetField`, `SetField`, `Invoke`) following the existing PlayMode convention, then add these two tests:

```csharp
[UnityTest]
public IEnumerator ForcedRightSpawnMustStartInsideItsChaseRange()
{
    yield return LoadBattleScene();
    var spawner = FindActiveSceneComponent("WaveSpawner");
    ((MonoBehaviour)spawner).StopAllCoroutines();
    var player = GameObject.Find("Player");
    var waves = (Array)GetField(spawner, "waves");
    var firstWave = waves.GetValue(0);
    var entries = (Array)firstWave.GetType().GetField("enemies").GetValue(firstWave);
    var entry = entries.GetValue(0);
    entry.GetType().GetField("enemyType").SetValue(entry, "grunt");
    entry.GetType().GetField("spawnX").SetValue(entry, 8f);

    Invoke(spawner, "SpawnEnemy", entry);
    var alive = ((IEnumerable)GetField(spawner, "_aliveEnemies")).Cast<GameObject>().ToList();
    var spawned = alive[alive.Count - 1];
    var enemy = spawned.GetComponent(spawner.GetType().Assembly.GetType("EnemyBase"));
    var chaseRange = (float)enemy.GetType().GetField("chaseRange").GetValue(enemy);

    Assert.That(Mathf.Abs(spawned.transform.position.x - player.transform.position.x),
        Is.LessThanOrEqualTo(chaseRange + 0.001f));
}

[UnityTest]
public IEnumerator CameraKeepsPlayerVisibleAndClampsInsideGround()
{
    yield return LoadBattleScene();
    var player = GameObject.Find("Player");
    player.transform.position = new Vector3(12f, player.transform.position.y, 0f);
    yield return null;

    var camera = Camera.main;
    var viewport = camera.WorldToViewportPoint(player.transform.position);
    Assert.That(viewport.x, Is.InRange(0f, 1f));
    Assert.That(camera.transform.parent, Is.Not.Null);
    Assert.That(camera.transform.parent.name, Is.EqualTo("[BattleCameraRig]"));
}
```

Use these helper implementations in the new class so it is self-contained:

```csharp
private static IEnumerator LoadBattleScene()
{
    yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
    yield return null;
    yield return null;
}

private static Component FindActiveSceneComponent(string typeName) =>
    Resources.FindObjectsOfTypeAll<Component>().Single(component =>
        component != null && component.GetType().Name == typeName &&
        component.gameObject.scene == SceneManager.GetActiveScene() &&
        component.gameObject.activeInHierarchy);

private static object GetField(Component component, string name) =>
    component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .GetValue(component);

private static object Invoke(Component component, string name, params object[] args) =>
    component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Invoke(component, args);
```

- [x] **Step 2: Run focused PlayMode RED**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task2-red.xml' -LogPath 'Logs/B2-task2-red.log' -ExpectFailure
```

Expected: the forced Grunt is at world x=14 and outside chase range, and the camera has no `[BattleCameraRig]` parent/does not keep the moved player visible.

- [x] **Step 3: Capture and restore one immutable baseline per Enemy instance**

In `EnemyBase`, replace Start-time stat mutation with explicit factory initialization:

```csharp
public EnemyStatBaseline Baseline { get; private set; }
private bool _baselineInitialized;
private Color _baselineColor;

public void InitializeCombatBaseline()
{
    if (_baselineInitialized) return;
    RecalculateStats();
    hp = maxHp;
    _baselineColor = _sprite != null ? _sprite.color : Color.white;
    Baseline = new EnemyStatBaseline(maxHp, damage, moveSpeed, damageReduction,
        telegraphDuration, attackDuration);
    _baselineInitialized = true;
}

public void PrepareForSpawn(EnemyWaveStats stats)
{
    if (!_baselineInitialized) InitializeCombatBaseline();
    StopAllCoroutines(); // Task 4 narrows this to the owned attack/telegraph handles.
    IsDead = false;
    CurrentState = EnemyState.Idle;
    maxHp = stats.MaxHp;
    hp = stats.MaxHp;
    damage = stats.Damage;
    moveSpeed = stats.MoveSpeed;
    damageReduction = Baseline.DamageReduction;
    telegraphDuration = Baseline.TelegraphDuration;
    attackDuration = Baseline.AttackDuration;
    _stateTimer = 0f;
    _decisionTimer = 0f;
    _facingDirection = 1;
    if (_sprite != null) { _sprite.color = _baselineColor; _sprite.flipX = false; }
    if (_rb != null) { _rb.velocity = Vector2.zero; _rb.angularVelocity = 0f; }
    var collider = GetComponent<Collider2D>();
    if (collider != null) collider.enabled = true;
    ResetSubclassState();
}

protected virtual void ResetSubclassState() { }
```

`EnemyBase.Start` only resolves the Player. Override `ResetSubclassState` in Archer (`_shootCooldownTimer = 0f`), Elite (`_currentCombo = 0; _isHeavyAttack = false`), and Boss (`_isEnraged = false; _attackPattern = 0`). Keep `ResetForPool()` as a compatibility wrapper that applies wave-0 stats from Baseline.

- [x] **Step 4: Make WaveSpawner plan world positions and scale from baseline**

Add `using Game.Gameplay;`, store configured arena/player/camera, set the Spawner transform to x=0, and use this flow inside `SpawnEnemy` without yielding:

```csharp
public void ConfigureArena(BattleArenaBounds bounds, Transform player, Camera camera)
{
    _arenaBounds = bounds;
    _player = player;
    _camera = camera;
    _arenaConfigured = player != null && camera != null;
}

private void SpawnEnemy(EnemySpawnEntry entry)
{
    if (_disposed || !_arenaConfigured) return;
    var enemyObject = ObjectPool.Instance.Get(entry.enemyType);
    if (enemyObject == null) return;
    var enemy = enemyObject.GetComponent<EnemyBase>();
    enemy.InitializeCombatBaseline();
    var waveStats = EnemyWaveScaling.Calculate(enemy.Baseline, _currentWave,
        new EnemyWaveMultipliers(enemyHpMultiplier, enemyDamageMultiplier, enemySpeedMultiplier));
    enemy.PrepareForSpawn(waveStats);
    var halfWidth = _camera.orthographicSize * _camera.aspect;
    var spawnX = ArenaSpawnPlanner.PlanX(_arenaBounds, _player.position.x,
        entry.preferredSide, halfWidth, 0.5f, enemy.chaseRange);
    enemyObject.transform.SetPositionAndRotation(
        new Vector3(spawnX, transform.position.y, 0f), Quaternion.identity);
    // Bind death before adding to alive state, then publish later in Task 6.
}
```

Call `enemyBase.InitializeCombatBaseline()` at the end of `CreateEnemy`. Replace `EnemySpawnEntry.spawnX` with `ArenaSpawnSide preferredSide`, configure deterministic alternating sides in `BattleSceneSetup.ConfigureWaves`, and delete legacy `ApplyWaveScaling`.

- [x] **Step 5: Add a parent camera rig that composes with CameraShaker**

Create `BattleCameraRig.cs`:

```csharp
using Game.Gameplay;
using UnityEngine;

public sealed class BattleCameraRig : MonoBehaviour
{
    private Transform _target;
    private Camera _camera;
    private BattleArenaBounds _bounds;
    public bool IsFollowing { get; private set; }

    public void Configure(Transform target, BattleArenaBounds bounds, Camera camera)
    {
        _target = target;
        _bounds = bounds;
        _camera = camera;
        IsFollowing = target != null && camera != null;
        SnapToTarget();
    }

    public void SetFollowEnabled(bool enabled) =>
        IsFollowing = enabled && _target != null && _camera != null;

    private void LateUpdate() { if (IsFollowing) SnapToTarget(); }

    private void SnapToTarget()
    {
        var halfWidth = _camera.orthographicSize * _camera.aspect;
        var min = _bounds.MinX + halfWidth;
        var max = _bounds.MaxX - halfWidth;
        var x = min > max ? _bounds.CenterX : Mathf.Clamp(_target.position.x, min, max);
        transform.position = new Vector3(x, 0f, 0f);
    }
}
```

`BattleSceneSetup.CreateCamera` creates `[BattleCameraRig]`, parents `Main Camera` under it with local `(0,0,-10)`, then adds/reuses `CameraShaker` on the child. After Player creation, configure the rig and WaveSpawner from one `BattleArenaBounds(-groundWidth/2, groundWidth/2)`. Do not change the existing `BattleRunController.Configure` signature. Add a narrow, one-time `ConfigureCameraRig(BattleCameraRig rig)` post-configuration injection; BattleSceneSetup calls it immediately after the existing Configure call. Terminal completion and Dispose call `SetFollowEnabled(false)` before time scale reaches zero.

- [x] **Step 6: Convert RED fixtures to final integration regressions**

Update the spawn test to set `preferredSide = ArenaSpawnSide.Right` and assert the spawned Grunt both starts reachable and reduces horizontal distance over real AI frames without moving/freezing it. Add `ObjectPoolReusePreparesTheSameBossBeforeItsFirstPhysicsStep` using a unique one-object pool key and the real sequence `ObjectPool.Register -> Get -> PrepareForSpawn -> mutate -> Return -> Get -> PrepareForSpawn`. Assert the two GameObjects are the same instance. Immediately after the second Prepare and before the first `WaitForFixedUpdate`, assert baseline-derived HP/damage/speed, Boss enrage/pattern reset, zero Rigidbody velocity/angularVelocity, baseline Sprite color/flip, enabled Collider, and no active attack/telegraph. Then cross one FixedUpdate and assert the state remains clean. Clear the unique key in `finally`.

- [x] **Step 7: Run focused and full GREEN gates**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task2-green.xml' -LogPath 'Logs/B2-task2-green.log'
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-task2-full-editmode.xml' -LogPath 'Logs/B2-task2-full-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-task2-full-playmode.xml' -LogPath 'Logs/B2-task2-full-playmode.log'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
git diff --check
git diff -- Assets/Scripts/Game/Combat/ObjectPool.cs
```

Expected: focused/full tests pass, asset integrity passes, diff check is clean, and the ObjectPool diff is empty.

- [x] **Step 8: Run specification review, then code-quality review**

Specification review checks planner integration, exact baseline capture timing, no `ObjectPool` API change, camera/shake Transform ownership, terminal freeze, and restart replacement. Quality review starts only after PASS and checks coroutine cleanup, event bindings, null handling, and deterministic tests.

- [x] **Step 9: Commit once and push once**

```powershell
git add Assets/Scripts/Game/Visual/BattleCameraRig.cs Assets/Scripts/Game/Visual/BattleCameraRig.cs.meta Assets/Scripts/Game/BattleSceneSetup.cs Assets/Scripts/Game/BattleRunController.cs Assets/Scripts/Game/Dungeon/WaveSpawner.cs Assets/Scripts/Game/Enemy/EnemyBase.cs Assets/Scripts/Game/Enemy/Archer.cs Assets/Scripts/Game/Enemy/Elite.cs Assets/Scripts/Game/Enemy/Boss.cs Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs.meta
git commit -m "feat: stabilize battle spawns and camera tracking"
git push origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 2 push mismatch" }
```

**Commit:** `feat: stabilize battle spawns and camera tracking`

### Task 3: Add The Pure Enemy Attack Plan Core

**Files:**
- Create: `Assets/Scripts/Gameplay/EnemyAttackPlan.cs`
- Create: `Assets/Scripts/Gameplay/EnemyAttackPlan.cs.meta`
- Modify: `Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs`

**Interfaces:**
- Produces: `EnemyTelegraphShape { Box, Circle }`, `EnemyAttackPhase { Telegraph, Commit, Recovery, Complete }`, immutable `EnemyAttackPlan`, and `EnemyAttackTimeline`.
- `EnemyAttackPlan` exposes `AttackId`, phase durations, `IsParryable`, shape/geometry, frozen facing/aim, hit count/interval, damage, knockback, `TotalDuration`, and `IsValid`.
- `EnemyAttackTimeline.Evaluate(float elapsed)` is the only pure phase evaluator consumed by Task 4.

- [x] **Step 1: Write normalization and phase tests plus a compile-only API seam**

Add the tests first. To obtain behavioral XML rather than a missing-type compile failure, add the exact public declarations in `EnemyAttackPlan.cs` with raw field assignment and a temporary `Evaluate` that returns `Complete`; do not add normalization or real phase behavior yet.

```csharp
[Test]
public void AttackPlanNormalizesGeometryTimingAndComboWindow()
{
    var plan = EnemyAttackPlan.Box(
        "elite_combo", -1f, 0.1f, -2f, true,
        new Vector2(-0.7f, 0.2f), new Vector2(-1f, -0.8f),
        -1, new Vector2(-4f, 0f), 3, 0.4f, 20, 5f);

    Assert.That(plan.TelegraphDuration, Is.Zero);
    Assert.That(plan.CommitDuration, Is.EqualTo(0.8f).Within(0.0001f));
    Assert.That(plan.RecoveryDuration, Is.Zero);
    Assert.That(plan.Size, Is.EqualTo(new Vector2(1f, 0.8f)));
    Assert.That(plan.FacingDirection, Is.EqualTo(-1));
    Assert.That(plan.AimDirection, Is.EqualTo(Vector2.left));
    Assert.That(plan.HitCount, Is.EqualTo(3));
    Assert.That(plan.IsValid, Is.True);
}

[Test]
public void AttackTimelineTraversesPreparedDurationsExactly()
{
    var plan = EnemyAttackPlan.Circle(
        "boss_aoe", 0.6f, 0.2f, 0.3f, false,
        Vector2.zero, 4f, 1, Vector2.right, 1, 0f, 20, 8f);
    var timeline = new EnemyAttackTimeline(plan);

    Assert.That(timeline.Evaluate(0.599f), Is.EqualTo(EnemyAttackPhase.Telegraph));
    Assert.That(timeline.Evaluate(0.6f), Is.EqualTo(EnemyAttackPhase.Commit));
    Assert.That(timeline.Evaluate(0.8f), Is.EqualTo(EnemyAttackPhase.Recovery));
    Assert.That(timeline.Evaluate(1.1f), Is.EqualTo(EnemyAttackPhase.Complete));
}
```

The compile seam must have the final signatures, including these factories:

```csharp
public static EnemyAttackPlan Box(string attackId, float telegraphDuration,
    float commitDuration, float recoveryDuration, bool isParryable,
    Vector2 localOffset, Vector2 size, int facingDirection, Vector2 aimDirection,
    int hitCount, float hitInterval, int damage, float knockback);

public static EnemyAttackPlan Circle(string attackId, float telegraphDuration,
    float commitDuration, float recoveryDuration, bool isParryable,
    Vector2 localOffset, float radius, int facingDirection, Vector2 aimDirection,
    int hitCount, float hitInterval, int damage, float knockback);
```

- [x] **Step 2: Run focused behavioral RED**

```powershell
Invoke-UnityTests -Platform EditMode -Filter 'Game.Tests.EditMode.Gameplay.EnemyExperienceCoreTests' -ResultPath 'Logs/B2-task3-red.xml' -LogPath 'Logs/B2-task3-red.log' -ExpectFailure
```

Expected: normalization assertions fail on negative duration/size and the timeline returns Complete instead of Telegraph. The XML must be complete; a compiler failure is not accepted as this task's RED.

- [x] **Step 3: Implement the immutable plan and timeline**

Use one private constructor behind `Box` and `Circle`. Apply these exact rules:

```csharp
AttackId = attackId ?? string.Empty;
TelegraphDuration = Mathf.Max(0f, telegraphDuration);
HitCount = Mathf.Max(1, hitCount);
HitInterval = Mathf.Max(0f, hitInterval);
CommitDuration = Mathf.Max(Mathf.Max(0f, commitDuration), (HitCount - 1) * HitInterval);
RecoveryDuration = Mathf.Max(0f, recoveryDuration);
FacingDirection = facingDirection < 0 ? -1 : 1;
AimDirection = aimDirection.sqrMagnitude > 0f
    ? aimDirection.normalized
    : new Vector2(FacingDirection, 0f);
LocalOffset = localOffset;
Size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
Radius = Mathf.Max(0f, radius);
Damage = Mathf.Max(0, damage);
Knockback = Mathf.Max(0f, knockback);
IsValid = AttackId.Length > 0 &&
    (Shape == EnemyTelegraphShape.Box ? Size.x > 0f && Size.y > 0f : Radius > 0f);
```

Implement the evaluator without mutable state:

```csharp
public EnemyAttackPhase Evaluate(float elapsed)
{
    var time = float.IsNaN(elapsed) ? 0f : Mathf.Max(0f, elapsed);
    if (time < _plan.TelegraphDuration) return EnemyAttackPhase.Telegraph;
    time -= _plan.TelegraphDuration;
    if (time < _plan.CommitDuration) return EnemyAttackPhase.Commit;
    time -= _plan.CommitDuration;
    if (time < _plan.RecoveryDuration) return EnemyAttackPhase.Recovery;
    return EnemyAttackPhase.Complete;
}
```

- [x] **Step 4: Add edge tests**

Add tests for invalid empty ID, zero Box size, zero Circle radius, zero aim fallback, `HitCount=1`, elapsed NaN, and exact zero-duration phase transitions. Assert every property is read-only via reflection.

- [x] **Step 5: Run focused and full GREEN gates**

```powershell
Invoke-UnityTests -Platform EditMode -Filter 'Game.Tests.EditMode.Gameplay.EnemyExperienceCoreTests' -ResultPath 'Logs/B2-task3-green.xml' -LogPath 'Logs/B2-task3-green.log'
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-task3-full-editmode.xml' -LogPath 'Logs/B2-task3-full-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-task3-full-playmode.xml' -LogPath 'Logs/B2-task3-full-playmode.log'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
git diff --check
```

- [x] **Step 6: Run specification review, then code-quality review**

Specification review checks every committed plan field, frozen aim, combo-window normalization, only Box/Circle, and no MonoBehaviour. Quality review then checks immutability, finite-number handling, exact boundaries, and table-test completeness.

- [x] **Step 7: Commit once and push once**

```powershell
git add Assets/Scripts/Gameplay/EnemyAttackPlan.cs Assets/Scripts/Gameplay/EnemyAttackPlan.cs.meta Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs
git commit -m "feat: define enemy attack plans"
git push origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 3 push mismatch" }
```

**Commit:** `feat: define enemy attack plans`

### Task 4: Drive Four Enemy Types Through Owned Attacks And Telegraph Views

**Files:**
- Create: `Assets/Scripts/Game/Visual/AttackTelegraphView.cs`
- Create: `Assets/Scripts/Game/Visual/AttackTelegraphView.cs.meta`
- Modify: `Assets/Scripts/Game/Enemy/EnemyBase.cs`
- Modify: `Assets/Scripts/Game/Enemy/Grunt.cs`
- Modify: `Assets/Scripts/Game/Enemy/Archer.cs`
- Modify: `Assets/Scripts/Game/Enemy/Elite.cs`
- Modify: `Assets/Scripts/Game/Enemy/Boss.cs`
- Modify: `Assets/Scripts/Game/Enemy/Projectile.cs`
- Modify: `Assets/Scripts/Game/Dungeon/WaveSpawner.cs`
- Modify: `Assets/Scripts/Game/BattleRunController.cs`
- Modify: `Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs`
- Modify: `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`

**Interfaces:**
- Consumes: Task 3 `EnemyAttackPlan`, `EnemyAttackPhase`, and `EnemyAttackTimeline`.
- Produces on EnemyBase: read-only `CurrentAttackPlan`, `CurrentAttackPhase`, protected abstract `EnemyAttackPlan PrepareAttackPlan()`, `IEnumerator ExecuteAttackPlan(EnemyAttackPlan)`, protected `TryStartPreparedAttack()` / `CancelOwnedAttack()`, and public idempotent `CancelCombatActions()` for scene lifecycle owners.
- Produces: idempotent `WaveSpawner.CancelActiveCombatActions()`, which forwards cancellation to every active Enemy tracked in `_aliveEnemies`.
- Produces: terminal ordering in `BattleRunController.Complete`: cancel all active enemy combat after the completion race is won and before BattleResult freezes time or GameOverUI displays the result.
- Produces: `AttackTelegraphView.Show(EnemyAttackPlan)`, `SetProgress(float)`, `Hide()`, `IsVisible`, `CurrentShape`, and read-only rendered bounds for tests.
- Constraint: attack view has no Collider; all physical queries use the same plan snapshot. No enemy may call `StartCoroutine` for an attack outside EnemyBase ownership.

- [x] **Step 1: Write RED tests for late planning and orphaned combos**

Add two real-behavior tests to `BattleEnemyExperienceTests`:

```csharp
[UnityTest]
public IEnumerator EliteHeavyChoiceMustExtendTelegraphBeforeAttackBegins()
{
    yield return LoadBattleScene();
    var elite = CreateEnemyProbe("Elite", "B2_RED_Elite");
    SetField(elite, "heavyAttackChance", 1f);
    SetField(elite, "heavyTelegraphDuration", 0.3f);
    SetField(elite, "_currentCombo", 0);
    Invoke(elite, "ChangeState", Enum.Parse(elite.GetType().Assembly.GetType("EnemyState"), "Telegraph"));
    SetField(elite, "_stateTimer", 0f);
    Invoke(elite, "UpdateTelegraph");

    Assert.That(GetProperty(elite, "CurrentState").ToString(), Is.EqualTo("Telegraph"));
    Assert.That((float)GetField(elite, "_stateTimer"), Is.EqualTo(0.3f).Within(0.01f));
}

[UnityTest]
public IEnumerator EliteMustNotReturnToChaseBeforeItsOwnedComboEnds()
{
    yield return LoadBattleScene();
    var elite = CreateEnemyProbe("Elite", "B2_RED_Combo");
    SetField(elite, "comboCount", 3);
    SetField(elite, "comboInterval", 0.1f);
    SetField(elite, "attackDuration", 0.05f);
    Invoke(elite, "ChangeState", Enum.Parse(elite.GetType().Assembly.GetType("EnemyState"), "Attack"));
    yield return new WaitForSeconds(0.06f);
    Invoke(elite, "UpdateAttack");
    Assert.That(GetProperty(elite, "CurrentState").ToString(), Is.EqualTo("Attack"));
}
```

`CreateEnemyProbe` creates an inactive GameObject, adds SpriteRenderer/Rigidbody2D/BoxCollider2D and the requested Enemy type, activates it beside the real Player, initializes baseline, and returns the Enemy Component. Clean it in `finally`.

- [x] **Step 2: Run focused PlayMode RED**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task4-red.xml' -LogPath 'Logs/B2-task4-red.log' -ExpectFailure
```

Expected: Elite changes directly to Attack instead of applying the heavy Telegraph, and its generic 0.05-second Attack returns to Chase before the three-hit coroutine completes.

- [x] **Step 3: Implement the independent Box/Circle telegraph view**

`AttackTelegraphView` owns one child LineRenderer with `useWorldSpace=false`, no Collider, and a scene-owned material destroyed in `OnDestroy`. Use 5 points for Box and 33 points for Circle. The view stores the plan snapshot and generates positions from its geometry:

```csharp
public void Show(EnemyAttackPlan plan)
{
    _plan = plan;
    _line.enabled = true;
    _line.startColor = _line.endColor = plan.IsParryable
        ? new Color(0.85f, 0.7f, 0.1f, 0.25f)
        : new Color(0.75f, 0.15f, 0.15f, 0.25f);
    if (plan.Shape == EnemyTelegraphShape.Box) BuildBox(plan.LocalOffset, plan.Size);
    else BuildCircle(plan.LocalOffset, plan.Radius, 32);
}

public void SetProgress(float progress)
{
    var color = _plan.IsParryable ? ParryableColor : UnparryableColor;
    color.a = Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01(progress));
    _line.startColor = _line.endColor = color;
}

public void Hide()
{
    if (_line != null) _line.enabled = false;
}
```

Expose computed local min/max bounds for tests. `Hide`, `OnDisable`, and `OnDestroy` are idempotent.

- [x] **Step 4: Make EnemyBase the sole owner of attack sequencing**

Remove generic timer-driven Telegraph->Attack and Attack->Chase transitions. Implement one routine:

```csharp
protected bool TryStartPreparedAttack()
{
    if (_attackRoutine != null || IsDead) return false;
    var plan = PrepareAttackPlan();
    if (!plan.IsValid) return false;
    CurrentAttackPlan = plan;
    _attackRoutine = StartCoroutine(RunOwnedAttack(plan));
    return true;
}

private IEnumerator RunOwnedAttack(EnemyAttackPlan plan)
{
    CurrentAttackPhase = EnemyAttackPhase.Telegraph;
    ChangeState(EnemyState.Telegraph);
    _telegraphView.Show(plan);
    for (var elapsed = 0f; elapsed < plan.TelegraphDuration; elapsed += Time.deltaTime)
    {
        _telegraphView.SetProgress(plan.TelegraphDuration <= 0f ? 1f : elapsed / plan.TelegraphDuration);
        yield return null;
    }
    _telegraphView.Hide();
    CurrentAttackPhase = EnemyAttackPhase.Commit;
    ChangeState(EnemyState.Attack);
    var commitStartedAt = Time.time;
    yield return ExecuteAttackPlan(plan);
    var remainingCommit = plan.CommitDuration - (Time.time - commitStartedAt);
    if (remainingCommit > 0f) yield return new WaitForSeconds(remainingCommit);
    CurrentAttackPhase = EnemyAttackPhase.Recovery;
    if (plan.RecoveryDuration > 0f) yield return new WaitForSeconds(plan.RecoveryDuration);
    _attackRoutine = null;
    CurrentAttackPhase = EnemyAttackPhase.Complete;
    ChangeState(EnemyState.Chase);
}

protected void CancelOwnedAttack()
{
    if (_attackRoutine != null) StopCoroutine(_attackRoutine);
    _attackRoutine = null;
    CurrentAttackPhase = EnemyAttackPhase.Complete;
    if (_telegraphView != null) _telegraphView.Hide();
}

public void CancelCombatActions()
{
    CancelOwnedAttack();
    if (_telegraphView != null) _telegraphView.Hide();
}
```

EnemyBase internal Hurt, Stun/OnParried, Die, OnDisable, and `PrepareForSpawn` paths call protected `CancelOwnedAttack`. WaveSpawner and other lifecycle owners call only public `CancelCombatActions()`. Replace the temporary Task 2 `StopAllCoroutines` spawn cleanup with targeted attack/telegraph cancellation plus explicit death-fade cleanup. Keep death fade as a separate handle.

Add the scene-owner cancellation boundary without exposing attack-start internals:

```csharp
public void CancelActiveCombatActions()
{
    for (var index = _aliveEnemies.Count - 1; index >= 0; index--)
    {
        var enemyObject = _aliveEnemies[index];
        if (enemyObject == null) continue;
        var enemy = enemyObject.GetComponent<EnemyBase>();
        if (enemy != null) enemy.CancelCombatActions();
    }
}
```

The method does not remove or return enemies and is safe to call repeatedly. In `BattleRunController.Complete`, call `_waveSpawner.CancelActiveCombatActions()` immediately after `_runState.TryComplete(outcome)` succeeds, before requesting the BattleResult zero-scale token and before `DisplayGameOver`. This applies identically to Victory and Defeat.

- [x] **Step 5: Implement plans and execution for all four enemy types**

- Grunt: parryable Box, offset `(facing*0.6, 0.2)`, size `(0.8,0.6)`, one hit.
- Archer: parryable narrow Box aiming along the frozen Player direction; Commit launches one Projectile with `plan.AimDirection` and starts cooldown.
- Elite combo: parryable Box, three hits at `comboInterval`; Elite heavy: choose before Telegraph, larger Box, `heavyTelegraphDuration`, one `heavyDamage` hit.
- Boss slash/charge: Box plans; slam/AoE: Circle plans. AoE is the only unparryable red plan. Choose pattern before entering Telegraph.

Each `ExecuteAttackPlan` uses `plan.LocalOffset/Size/Radius`, `plan.HitCount`, `plan.HitInterval`, `plan.Damage`, and `plan.Knockback`; remove duplicate geometry literals from the physical query. Projectile launch must not recalculate Player direction at Commit.

- [x] **Step 6: Update and extend PlayMode regressions**

Replace the two RED tests with persistent assertions on `CurrentAttackPlan` and `CurrentAttackPhase`. Add:

- Box and Circle view bounds equal the physical plan bounds and the view has no Collider.
- Yellow view always produces `IsParryable=true`; red Boss AoE produces false.
- Player HP is unchanged throughout Telegraph and changes only during Commit.
- Parry/Hurt/Die/PrepareForSpawn cancel the owned routine, hide the view, and prevent later combo hits.
- Update `ParryCancelsActiveEliteComboAndLaterAttacksStillWork` in `BattleCombatLoopTests` to use the existing reflection helper to invoke protected `TryStartPreparedAttack` instead of removed `OnAttackStart`. Do not add a public or test-only attack-start API. The protected call still enters `RunOwnedAttack`, so the regression exercises the real owned routine and its Telegraph/Commit phases.
- Add `VictoryCancelsEliteCommitBeforeBattleResultFreeze`: spawn an Elite through the real WaveSpawner, invoke protected `TryStartPreparedAttack` through the same reflection helper, wait until the first combo hit leaves it in Commit, then use the existing wave-completion helper to trigger the subscribed Victory path. Assert `CurrentAttackPhase=Complete`, `_attackRoutine=null`, and the Telegraph view hidden.
- Add `DefeatCancelsBossTelegraphBeforeBattleResultFreeze`: spawn a Boss through the real WaveSpawner, deterministically prepare its AoE, invoke protected `TryStartPreparedAttack` through reflection, wait for visible Telegraph, then raise the real CharacterStats death event to trigger the subscribed Defeat path. Assert the Boss routine is Complete/null and its view is hidden.
- In both terminal tests, record Player HP at completion, release only the captured `_battleResultToken` through `BattleTimeController.ReleaseTimeScale`, set the controller field to default through the existing test helper so cleanup stays idempotent, advance longer than the remaining plan duration, and assert Player HP never changes. This proves cancellation rather than relying on time scale 0 to mask an orphaned hit.
- Extend `RestartButtonReloadsFreshRunningBattleScene`: after the replacement BattleScene is ready, spawn a new Enemy through its replacement WaveSpawner, invoke protected `TryStartPreparedAttack` through reflection, observe Telegraph then Commit/a resolved hit, and assert no terminal cancellation state or hidden-view state leaked from the old scene.

- [x] **Step 7: Run focused and full GREEN gates**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task4-green.xml' -LogPath 'Logs/B2-task4-green.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleCombatLoopTests' -ResultPath 'Logs/B2-task4-combat-regression.xml' -LogPath 'Logs/B2-task4-combat-regression.log'
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-task4-full-editmode.xml' -LogPath 'Logs/B2-task4-full-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-task4-full-playmode.xml' -LogPath 'Logs/B2-task4-full-playmode.log'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
rg -n "StartCoroutine\(" Assets/Scripts/Game/Enemy
rg -n "CancelActiveCombatActions" Assets/Scripts/Game/Dungeon/WaveSpawner.cs Assets/Scripts/Game/BattleRunController.cs
git diff --check
```

Expected static result: Enemy attack coroutines start only through EnemyBase ownership; death fade may retain its separately tracked coroutine. WaveSpawner owns one public active-combat cancellation boundary, and BattleRunController invokes it before the BattleResult time-scale request.

- [x] **Step 8: Run specification review, then code-quality review**

Specification review checks prepare-before-Telegraph, frozen aim, shared visual/physics geometry, Box/Circle only, attack cancellation, terminal Victory/Defeat ordering, restart recovery, and all four archetypes. Quality review then checks coroutine reentrancy, idempotent active-enemy traversal, state transitions, material destruction, zero-duration behavior, and B1 compatibility.

- [x] **Step 9: Commit once and push once**

```powershell
git add Assets/Scripts/Game/Visual/AttackTelegraphView.cs Assets/Scripts/Game/Visual/AttackTelegraphView.cs.meta Assets/Scripts/Game/Enemy/EnemyBase.cs Assets/Scripts/Game/Enemy/Grunt.cs Assets/Scripts/Game/Enemy/Archer.cs Assets/Scripts/Game/Enemy/Elite.cs Assets/Scripts/Game/Enemy/Boss.cs Assets/Scripts/Game/Enemy/Projectile.cs Assets/Scripts/Game/Dungeon/WaveSpawner.cs Assets/Scripts/Game/BattleRunController.cs Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs Assets/Tests/PlayMode/BattleCombatLoopTests.cs
git commit -m "feat: synchronize enemy telegraphs and attacks"
git push origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 4 push mismatch" }
```

**Commit:** `feat: synchronize enemy telegraphs and attacks`

### Task 5: Resolve Actual Damage And Own Combat Feedback Lifetimes

**Files:**
- Create: `Assets/Scripts/Gameplay/CombatHitOutcome.cs`
- Create: `Assets/Scripts/Gameplay/CombatHitOutcome.cs.meta`
- Create: `Assets/Scripts/Gameplay/ParticleLeaseRegistry.cs`
- Create: `Assets/Scripts/Gameplay/ParticleLeaseRegistry.cs.meta`
- Modify: `Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs`
- Create: `Assets/Scripts/Game/Combat/CombatFeedbackContext.cs`
- Create: `Assets/Scripts/Game/Combat/CombatFeedbackContext.cs.meta`
- Create: `Assets/Scripts/Game/Combat/CombatHitResolver.cs`
- Create: `Assets/Scripts/Game/Combat/CombatHitResolver.cs.meta`
- Create: `Assets/Scripts/Game/Visual/CombatFeedbackController.cs`
- Create: `Assets/Scripts/Game/Visual/CombatFeedbackController.cs.meta`
- Modify: `Assets/Scripts/Game/Combat/CombatEvents.cs`
- Modify: `Assets/Scripts/Game/Combat/Hurtbox.cs`
- Modify: `Assets/Scripts/Game/Combat/Hitbox.cs`
- Modify: `Assets/Scripts/Game/Combat/SummonAI.cs`
- Modify: `Assets/Scripts/Game/Combat/ElementalEffect.cs`
- Modify: `Assets/Scripts/Game/Enemy/Grunt.cs`
- Modify: `Assets/Scripts/Game/Enemy/Elite.cs`
- Modify: `Assets/Scripts/Game/Enemy/Boss.cs`
- Modify: `Assets/Scripts/Game/Enemy/Projectile.cs`
- Modify: `Assets/Scripts/Game/Weapons/AutoWeapon.cs`
- Modify: `Assets/Scripts/Game/Style/Impl/BladeStyle.cs`
- Modify: `Assets/Scripts/Game/Style/Impl/PoisonStyle.cs`
- Modify: `Assets/Scripts/Game/Style/Impl/SealStyle.cs`
- Modify: `Assets/Scripts/Game/Style/Impl/SwordStyle.cs`
- Modify: `Assets/Scripts/Game/Visual/HitEffectPlayer.cs`
- Modify: `Assets/Scripts/Game/Visual/InkParticlePool.cs`
- Modify: `Assets/Scripts/Game/Visual/InkHitEffect.cs`
- Modify: `Assets/Scripts/Game/Visual/InkSlashEffect.cs`
- Modify: `Assets/Scripts/Game/Visual/CameraShaker.cs`
- Modify: `Assets/Scripts/Game/Visual/HitStopController.cs`
- Modify: `Assets/Scripts/Game/BattleSceneSetup.cs`
- Modify: `Assets/Scripts/Game/BattleRunController.cs`
- Modify: `Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs`
- Modify: `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`

**Interfaces:**
- Produces: immutable `CombatHitOutcome(CombatHitResult result, int appliedDamage)`.
- Produces: `Hurtbox.ResolveHit(CombatHit) -> CombatHitOutcome`; existing `ReceiveHit` remains and returns only `.Result`.
- Produces: pure `ParticleLeaseToken` and `ParticleLeaseRegistry.Acquire(int slot)`, `TryRelease(token)`, `IsActive(token)`, `InvalidateAll()`.
- Produces: `CombatFeedbackContext` with source/target, `CombatFeedbackSourceKind`, `CombatFeedbackTargetKind`, `CombatFeedbackStrength`, applied damage, and facing; produces `CombatEvents.OnHitResolved`.
- Produces: `CombatHitResolver.ResolveAndPublish(Hurtbox target, CombatHit hit, GameObject source, CombatFeedbackSourceKind sourceKind, CombatFeedbackStrength strength, int facingDirection)` so every production source resolves and publishes exactly once.
- Produces: scene-owned `CombatFeedbackController.Configure(...)`, `Handle(CombatFeedbackContext)`, `ClearTransient()`, and `Dispose()`.

- [x] **Step 1: Write PlayMode RED tests for inaccurate and missing feedback**

Add three tests to `BattleEnemyExperienceTests`:

```csharp
[UnityTest]
public IEnumerator EnemyDamageFeedbackMustEqualTheActualHpDelta()
{
    yield return LoadBattleScene();
    var player = GameObject.Find("Player");
    var playerHurtbox = player.GetComponents<Component>()
        .Single(component => component.GetType().Name == "Hurtbox");
    var gameAssembly = playerHurtbox.GetType().Assembly;
    var hitbox = GameObject.Find("AttackHitbox").GetComponent(gameAssembly.GetType("Hitbox"));
    var grunt = FindFirstActiveEnemy("Grunt");
    ((Behaviour)grunt).enabled = false;
    SetField(grunt, "maxHp", 500);
    SetField(grunt, "hp", 500);
    SetField(grunt, "damageReduction", 0.5f);
    grunt.transform.position = hitbox.transform.position;
    Physics2D.SyncTransforms();

    var reported = -1;
    Action<Vector3, int> probe = (_, damage) => reported = damage;
    CombatEvent("OnHitLanded").AddEventHandler(null, probe);
    try
    {
        var before = (int)GetField(grunt, "hp");
        Invoke(hitbox, "EnableHitbox");
        yield return new WaitForFixedUpdate();
        var after = (int)GetField(grunt, "hp");
        Assert.That(reported, Is.EqualTo(before - after),
            "B2_RED_ACTUAL_DAMAGE: feedback must equal the target HP delta");
    }
    finally { CombatEvent("OnHitLanded").RemoveEventHandler(null, probe); }
}

[UnityTest]
public IEnumerator PlayerDamageMustTriggerItsInstalledHitFlash()
{
    yield return LoadBattleScene();
    var player = GameObject.Find("Player");
    player.GetComponent<SpriteRenderer>().color = Color.blue;
    var original = Color.blue;
    var hurtbox = player.GetComponents<Component>()
        .Single(component => component.GetType().Name == "Hurtbox");
    InvokeCombatHit(hurtbox, 7, false);
    yield return null;
    Assert.That(player.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white));
    Assert.That(player.GetComponent<SpriteRenderer>().color, Is.Not.EqualTo(original));
}

[UnityTest]
public IEnumerator InkParticlePoolMustInitializeExactlyOnce()
{
    yield return LoadBattleScene();
    var player = GameObject.Find("Player");
    var gameAssembly = player.GetComponents<Component>()
        .Single(component => component.GetType().Name == "Hurtbox").GetType().Assembly;
    var type = gameAssembly.GetType("InkParticlePool");
    var instance = type.GetProperty("Instance").GetValue(null) as Component;
    var all = (ICollection)GetField(instance, "_allParticles");
    var poolSize = (int)GetField(instance, "poolSize");
    Assert.That(all.Count, Is.EqualTo(poolSize));
    Assert.That(instance.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
}
```

- [x] **Step 2: Run focused PlayMode RED**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task5-red.xml' -LogPath 'Logs/B2-task5-red.log' -ExpectFailure -ExpectedFailureMessage 'B2_RED_ACTUAL_DAMAGE'
```

Expected: reported enemy damage exceeds the actual HP delta, Player does not flash white, and InkParticlePool either has twice `poolSize` entries or is persistent.

- [x] **Step 3: Add pure hit outcome and generation-safe lease rules with EditMode tests**

Implement:

```csharp
public readonly struct CombatHitOutcome
{
    public CombatHitOutcome(CombatHitResult result, int appliedDamage)
    {
        Result = result;
        AppliedDamage = result == CombatHitResult.Damaged
            ? System.Math.Max(0, appliedDamage)
            : 0;
    }
    public CombatHitResult Result { get; }
    public int AppliedDamage { get; }
}

public readonly struct ParticleLeaseToken : System.IEquatable<ParticleLeaseToken>
{
    public ParticleLeaseToken(int slot, uint generation)
    { Slot = slot; Generation = generation; }
    public int Slot { get; }
    public uint Generation { get; }
    // Implement value equality and hash code.
}
```

`ParticleLeaseRegistry` stores the active generation per slot. `Acquire(slot)` increments that slot's generation and replaces any active lease. `TryRelease` succeeds only for the active matching generation and removes it. `InvalidateAll` clears active leases and increments known generations so every old token stays invalid.

Add EditMode tests proving Damaged preserves the HP delta, Parried/Ignored use zero, duplicate Return fails, reacquiring one slot invalidates its prior token, and InvalidateAll invalidates every slot.

- [x] **Step 4: Preserve ReceiveHit and add one resolving implementation**

Refactor `Hurtbox` without duplicating logic:

```csharp
public CombatHitResult ReceiveHit(CombatHit hit) => ResolveHit(hit).Result;

public CombatHitOutcome ResolveHit(CombatHit hit)
{
    if (ReceiverIsDeadOrMissing())
        return new CombatHitOutcome(CombatHitResult.Ignored, 0);
    if (stateMachine != null && stateMachine.IsInParryWindow && hit.IsParryable)
    {
        stateMachine.OnParrySuccess();
        hit.Source?.OnParried();
        return new CombatHitOutcome(CombatHitResult.Parried, 0);
    }

    var hpBefore = stats != null ? stats.currentHp : enemy.hp;
    ApplyDamageAndKnockback(hit);
    var hpAfter = stats != null ? stats.currentHp : enemy.hp;
    return new CombatHitOutcome(CombatHitResult.Damaged, Mathf.Max(0, hpBefore - hpAfter));
}
```

Keep the exact B1 enum and compatibility tests. Implement `CombatHitResolver.ResolveAndPublish` as the only production adapter: it calls `target.ResolveHit(hit)`, creates one `CombatFeedbackContext`, publishes it once, and returns the outcome. Migrate every production caller in the Files list to this adapter. Elemental ticks/bounces and Blade/Poison/Seal/Sword style damage resolve through the target Hurtbox instead of calling `CharacterStats.TakeDamage` or `EnemyBase.TakeDamage` and spawning guessed numbers. BloodStyle's self-paid HP cost remains a resource cost, not an enemy hit, and stays outside resolved-hit feedback.

- [x] **Step 5: Add one resolved-hit event and one feedback owner**

`CombatEvents.InvokeHitResolved` invokes `OnHitResolved` once and maps damaged enemy/player outcomes to the legacy `OnHitLanded`/`OnDamageTaken` events using `AppliedDamage`. Do not re-fire legacy `OnParrySuccess`; PlayerStateMachine already owns that compatibility event.

Remove EnemyBase's direct `_hitEffect.PlayHitEffect()` call so the new controller does not flash an enemy twice.

`CombatFeedbackController` is the only new-event presentation subscriber:

```csharp
private void HandleResolvedHit(CombatFeedbackContext context)
{
    if (context.Result == CombatHitResult.Parried)
    {
        DamageNumberPool.SpawnText("弹反", context.Position, DamageType.Parry);
        _hitStop.DoHitStop(_hitStop.parryHitStopDuration);
        _cameraShaker.CustomShake(_cameraShaker.parryShakeIntensity,
            _cameraShaker.parryShakeDuration);
        return;
    }
    if (context.Result != CombatHitResult.Damaged) return;

    context.Target?.GetComponent<HitEffectPlayer>()?.PlayHitEffect();
    DamageNumberPool.Spawn(context.AppliedDamage, context.Position, DamageType.Normal);
    if (context.TargetKind == CombatFeedbackTargetKind.Enemy)
        _inkHitEffect.PlayAt(context.Position, _inkParticlePool);
    if (context.SourceKind == CombatFeedbackSourceKind.PlayerMelee)
        _inkSlashEffect.Play(_player.transform.position, context.FacingDirection);
    ApplyExistingHitStopAndShake(context.Strength);
}
```

Remove BattleSceneSetup's old ink/slash/audio/damage/parry handlers and migrate CameraShaker/HitStopController away from legacy event subscriptions so no feedback plays twice. ComboCounter may keep `OnHitLanded`, now carrying AppliedDamage.

- [x] **Step 6: Make InkParticlePool scene-owned and generation-safe**

`BattleSceneSetup` explicitly creates one `[InkParticlePool]`; remove all `DontDestroyOnLoad` and double initialization. `InkParticlePool.Instance` returns the installed scene instance and never initializes twice. Pair each particle slot with `ParticleLeaseToken`:

```csharp
public InkParticleHandle Get()
{
    var slot = _available.Count > 0 ? _available.Dequeue() : NextReuseSlot();
    var token = _leases.Acquire(slot);
    var particle = _allParticles[slot];
    particle.SetActive(true);
    return new InkParticleHandle(particle, token);
}

public bool Return(InkParticleHandle handle)
{
    if (!_leases.TryRelease(handle.Token)) return false;
    ResetParticle(handle.Particle);
    handle.Particle.SetActive(false);
    _available.Enqueue(handle.Token.Slot);
    return true;
}
```

`InkHitEffect` stores handles, not bare GameObjects. `ClearAll` cancels its splash routine and returns active valid handles. Pool OnDestroy invalidates leases and clears static ownership only when it owns Instance. `HitEffectPlayer.OnDisable` restores its saved color; InkSlashEffect exposes idempotent `Hide`.

- [x] **Step 7: Clear transient feedback before BattleResult freezes time**

Do not change `BattleRunController.Configure`. Add a narrow, one-time `ConfigureCombatFeedback(CombatFeedbackController controller)` post-configuration injection, called by BattleSceneSetup after the existing Configure call. In the first terminal path call `ClearTransient()` before requesting the zero-scale BattleResult token. Dispose calls controller Dispose before scene pools disappear. Restart tests must capture old ink pool/particles and assert they are destroyed before accepting the new scene.

- [x] **Step 8: Convert RED tests and add permanent compatibility/lifecycle coverage**

Update tests to assert `CombatFeedbackContext.AppliedDamage` equals HP delta exactly, one target flash/number/ink feedback per hit, player flash, one parry feedback with no HP loss, and no duplicate legacy callbacks. Add `ResolveHitReturnsOutcomeWhileReceiveHitPreservesB1Result` and restart-during-splash generation tests.

- [x] **Step 9: Run focused and full GREEN gates**

```powershell
Invoke-UnityTests -Platform EditMode -Filter 'Game.Tests.EditMode.Gameplay.EnemyExperienceCoreTests' -ResultPath 'Logs/B2-task5-core-green.xml' -LogPath 'Logs/B2-task5-core-green.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task5-green.xml' -LogPath 'Logs/B2-task5-green.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleCombatLoopTests' -ResultPath 'Logs/B2-task5-combat-regression.xml' -LogPath 'Logs/B2-task5-combat-regression.log'
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-task5-full-editmode.xml' -LogPath 'Logs/B2-task5-full-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-task5-full-playmode.xml' -LogPath 'Logs/B2-task5-full-playmode.log'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
rg -n "ReceiveHit\(" Assets/Scripts/Game
rg -n "\.TakeDamage\(" Assets/Scripts/Game
rg -n "DontDestroyOnLoad" Assets/Scripts/Game/Visual/InkParticlePool.cs
git diff --check
```

Expected static result: production Game code calls `CombatHitResolver.ResolveAndPublish`; `ReceiveHit` remains only as the Hurtbox wrapper. Direct `enemy.TakeDamage`/`stats.TakeDamage` remain only inside Hurtbox plus the documented BloodStyle self-cost; ElementalEffect and Blade/Poison/Seal/Sword have no bypass. InkParticlePool has no `DontDestroyOnLoad`.

- [x] **Step 10: Run specification review, then code-quality review**

Specification review checks actual HP delta, compatibility wrapper, single feedback event, no double presentation, generation-safe reuse, terminal-before-freeze cleanup, and scene ownership. Quality review then checks all producers, event unsubscription, stale coroutine returns, static owner cleanup, and failure isolation.

- [x] **Step 11: Commit once and push once**

```powershell
git add Assets/Scripts/Gameplay/CombatHitOutcome.cs Assets/Scripts/Gameplay/CombatHitOutcome.cs.meta Assets/Scripts/Gameplay/ParticleLeaseRegistry.cs Assets/Scripts/Gameplay/ParticleLeaseRegistry.cs.meta Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs Assets/Scripts/Game/Combat/CombatFeedbackContext.cs Assets/Scripts/Game/Combat/CombatFeedbackContext.cs.meta Assets/Scripts/Game/Combat/CombatHitResolver.cs Assets/Scripts/Game/Combat/CombatHitResolver.cs.meta Assets/Scripts/Game/Visual/CombatFeedbackController.cs Assets/Scripts/Game/Visual/CombatFeedbackController.cs.meta Assets/Scripts/Game/Combat/CombatEvents.cs Assets/Scripts/Game/Combat/Hurtbox.cs Assets/Scripts/Game/Combat/Hitbox.cs Assets/Scripts/Game/Combat/SummonAI.cs Assets/Scripts/Game/Combat/ElementalEffect.cs Assets/Scripts/Game/Enemy/Grunt.cs Assets/Scripts/Game/Enemy/Elite.cs Assets/Scripts/Game/Enemy/Boss.cs Assets/Scripts/Game/Enemy/Projectile.cs Assets/Scripts/Game/Weapons/AutoWeapon.cs Assets/Scripts/Game/Style/Impl/BladeStyle.cs Assets/Scripts/Game/Style/Impl/PoisonStyle.cs Assets/Scripts/Game/Style/Impl/SealStyle.cs Assets/Scripts/Game/Style/Impl/SwordStyle.cs Assets/Scripts/Game/Visual/HitEffectPlayer.cs Assets/Scripts/Game/Visual/InkParticlePool.cs Assets/Scripts/Game/Visual/InkHitEffect.cs Assets/Scripts/Game/Visual/InkSlashEffect.cs Assets/Scripts/Game/Visual/CameraShaker.cs Assets/Scripts/Game/Visual/HitStopController.cs Assets/Scripts/Game/BattleSceneSetup.cs Assets/Scripts/Game/BattleRunController.cs Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs Assets/Tests/PlayMode/BattleCombatLoopTests.cs
git commit -m "feat: unify resolved hit feedback"
git push origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 5 push mismatch" }
```

**Commit:** `feat: unify resolved hit feedback`

### Task 6: Add Event-Driven Wave And Boss HUD

**Files:**
- Create: `Assets/Scripts/Gameplay/WaveObjectiveState.cs`
- Create: `Assets/Scripts/Gameplay/WaveObjectiveState.cs.meta`
- Modify: `Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs`
- Create: `Assets/Scripts/UI/BattleUI/WaveObjectiveView.cs`
- Create: `Assets/Scripts/UI/BattleUI/WaveObjectiveView.cs.meta`
- Modify: `Assets/Scripts/UI/BattleUI/BattleHUD.cs`
- Modify: `Assets/Scripts/UI/BattleUI/BossHPBar.cs`
- Modify: `Assets/Scripts/Game/Dungeon/WaveSpawner.cs`
- Modify: `Assets/Scripts/Game/Enemy/EnemyBase.cs`
- Modify: `Assets/Scripts/Game/Enemy/Boss.cs`
- Modify: `Assets/Scripts/Game/BattleSceneSetup.cs`
- Modify: `Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs`
- Modify: `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`

**Interfaces:**
- Produces pure `WaveObjectiveState(int zeroBasedWave, int totalWaves, int aliveEnemies)` with normalized `DisplayWave`, `TotalWaves`, `AliveEnemies`, `WaveText`, and `AliveText`.
- Produces WaveSpawner events `OnWaveStarted(int zeroBasedWave, int totalWaves)`, `OnAliveEnemyCountChanged(int alive)`, `OnBossSpawned(Boss)`, and `OnBossRemoved(Boss)`.
- Produces EnemyBase `OnHealthChanged(int current, int max)` and Boss `CurrentPhase` / `OnPhaseChanged(int)`.
- Produces `BattleHUD.InitializeForBattle(CharacterStats, WaveSpawner)`, scene-owned `WaveObjectiveView`, and event-bound `BossHPBar.BindBoss/UnbindBoss`.

- [x] **Step 1: Write PlayMode RED for absent objective and Boss HUD wiring**

Add two tests:

```csharp
[UnityTest]
public IEnumerator BattleHudMustExposeCurrentWaveAndAliveEnemyCount()
{
    yield return LoadBattleScene();
    Assert.That(FindLoadedComponents("WaveObjectiveView"), Has.Count.EqualTo(1));
    var view = FindActiveSceneComponent("WaveObjectiveView");
    Assert.That(GetText(view, "waveText"), Is.EqualTo("波次 1/10"));
    Assert.That(GetText(view, "aliveText"), Does.StartWith("剩余 "));
}

[UnityTest]
public IEnumerator RealBossSpawnMustBindOneVisibleBossHpBar()
{
    yield return LoadBattleScene();
    var spawner = FindActiveSceneComponent("WaveSpawner");
    ((MonoBehaviour)spawner).StopAllCoroutines();
    SpawnBossThroughSpawner(spawner); // Build an EnemySpawnEntry by reflection on the current baseline.
    yield return null;
    var bars = FindLoadedComponents("BossHPBar");
    Assert.That(bars, Has.Count.EqualTo(1));
    Assert.That(bars[0].gameObject.activeInHierarchy, Is.True);
}
```

- [x] **Step 2: Run focused PlayMode RED**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task6-red.xml' -LogPath 'Logs/B2-task6-red.log' -ExpectFailure
```

Expected: no WaveObjectiveView exists and no BossHPBar is created/bound after a real pooled Boss spawn.

- [x] **Step 3: Add the pure HUD formatting model and EditMode tests**

Implement exactly one 0-based-to-user-facing conversion:

```csharp
namespace Game.Gameplay
{
    public readonly struct WaveObjectiveState
    {
        public WaveObjectiveState(int zeroBasedWave, int totalWaves, int aliveEnemies)
        {
            TotalWaves = System.Math.Max(0, totalWaves);
            DisplayWave = TotalWaves == 0 ? 0 :
                System.Math.Min(TotalWaves, System.Math.Max(0, zeroBasedWave) + 1);
            AliveEnemies = System.Math.Max(0, aliveEnemies);
        }
        public int DisplayWave { get; }
        public int TotalWaves { get; }
        public int AliveEnemies { get; }
        public string WaveText => $"波次 {DisplayWave}/{TotalWaves}";
        public string AliveText => $"剩余 {AliveEnemies}";
    }
}
```

Add tests for first/last wave, negative input, zero total, over-range wave, and negative alive count. Add a Boss phase idempotence PlayMode test rather than duplicating Unity event behavior in the pure model.

- [x] **Step 4: Publish current-run wave, alive, and Boss events**

In WaveSpawner:

```csharp
public event Action<int, int> OnWaveStarted;
public event Action<int> OnAliveEnemyCountChanged;
public event Action<Boss> OnBossSpawned;
public event Action<Boss> OnBossRemoved;
```

Publish wave started before the first spawn. After `PrepareForSpawn`, death binding, and `_aliveEnemies.Add`, publish alive count, then Boss spawned with final scaled MaxHp. On confirmed death remove once, publish alive count, and publish Boss removed once. Dispose publishes removal for any bound Boss before clearing delegates. Keep B1 `OnWaveStart` as a delegating compatibility event until all old tests/callers migrate; do not publish two internal wave truths.

- [x] **Step 5: Publish health and phase truth from Enemy/Boss**

EnemyBase raises `OnHealthChanged(hp,maxHp)` after PrepareForSpawn and after every actual HP change. Boss exposes `CurrentPhase` initialized to 1; `EnterEnrage` changes it to 2 and raises `OnPhaseChanged(2)` once. Reset returns phase to 1 before `OnBossSpawned`. UI must never infer phase from an independent 50% calculation.

- [x] **Step 6: Create event-driven WaveObjectiveView and BossHPBar**

`WaveObjectiveView` owns two `Text` references and only renders `WaveObjectiveState`:

```csharp
public void Render(WaveObjectiveState state)
{
    waveText.text = state.WaveText;
    aliveText.text = state.AliveText;
}
```

BattleHUD creates the compact view in the existing Canvas and subscribes to the new Spawner events through `InitializeForBattle`. Create one existing `BossHPBar` tree at the top center, inactive until Boss spawn.

Refactor BossHPBar to remove Update polling. `BindBoss` first calls `UnbindBoss`, subscribes health/phase events, renders current values, and activates. `UnbindBoss` removes every handler, clears the reference, and hides. Boss removed, HUD disable/destroy, and replacement binding all call UnbindBoss idempotently.

- [x] **Step 7: Update scene wiring and lifecycle regressions**

Change BattleSceneSetup's HUD coroutine to call `InitializeForBattle(stats, _waveSpawner)`. Extend restart/reload tests to capture old WaveObjectiveView/BossHPBar IDs and delegate targets; after reload require one new HUD pair, no old objects, and no delegates targeting the destroyed HUD.

Convert the Boss RED helper to set Task 2 `preferredSide`, then assert the bar's max/value equal the scaled Boss values, one phase event at enrage, immediate HP update, and hide/unbind after death/return. Assert alive count never becomes negative or decrements twice.

- [x] **Step 8: Run focused and full GREEN gates**

```powershell
Invoke-UnityTests -Platform EditMode -Filter 'Game.Tests.EditMode.Gameplay.EnemyExperienceCoreTests' -ResultPath 'Logs/B2-task6-core-green.xml' -LogPath 'Logs/B2-task6-core-green.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyExperienceTests' -ResultPath 'Logs/B2-task6-green.xml' -LogPath 'Logs/B2-task6-green.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleSceneOfflineSmokeTests' -ResultPath 'Logs/B2-task6-smoke.xml' -LogPath 'Logs/B2-task6-smoke.log'
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-task6-full-editmode.xml' -LogPath 'Logs/B2-task6-full-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-task6-full-playmode.xml' -LogPath 'Logs/B2-task6-full-playmode.log'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
git diff --check
```

- [x] **Step 9: Run specification review, then code-quality review**

Specification review checks event order, 1-based display, final scaled Boss HP, event-owned phase, single Boss scope, terminal/restart behavior, and no polling. Quality review then checks handler identity/unsubscription, duplicate death protection, UI layout stability, and no static HUD leaks.

- [x] **Step 10: Commit once and push once**

```powershell
git add Assets/Scripts/Gameplay/WaveObjectiveState.cs Assets/Scripts/Gameplay/WaveObjectiveState.cs.meta Assets/Tests/EditMode/Gameplay/EnemyExperienceCoreTests.cs Assets/Scripts/UI/BattleUI/WaveObjectiveView.cs Assets/Scripts/UI/BattleUI/WaveObjectiveView.cs.meta Assets/Scripts/UI/BattleUI/BattleHUD.cs Assets/Scripts/UI/BattleUI/BossHPBar.cs Assets/Scripts/Game/Dungeon/WaveSpawner.cs Assets/Scripts/Game/Enemy/EnemyBase.cs Assets/Scripts/Game/Enemy/Boss.cs Assets/Scripts/Game/BattleSceneSetup.cs Assets/Tests/PlayMode/BattleEnemyExperienceTests.cs Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs
git commit -m "feat: add wave and boss battle hud"
git push origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 6 push mismatch" }
```

**Commit:** `feat: add wave and boss battle hud`

### Task 7: Capture Visual Evidence, Run Full Gates, And Record Delivery

**Files:**
- Create: `Assets/Tests/PlayMode/BattleEnemyVisualEvidenceTests.cs`
- Create: `Assets/Tests/PlayMode/BattleEnemyVisualEvidenceTests.cs.meta`
- Modify: `CLAUDE.md`
- Modify: `.claude/memory/project-overview.md`
- Modify: `docs/superpowers/specs/2026-07-19-phase-b2-enemy-combat-experience-design.md` only to record approved implementation evidence without changing scope.
- Modify: `docs/superpowers/plans/2026-07-19-phase-b2-enemy-combat-experience.md` to record exact final totals, image metrics, review verdicts, and commit SHAs.

**Interfaces:**
- Produces: `Logs/phase-b2-wave-combat.png` and `Logs/phase-b2-boss-telegraph.png`, both exactly 960x540 and freshly generated.
- Produces no runtime API. Visual fixtures use the real BattleScene, Task 2 camera/spawns, Task 4 telegraph, Task 5 feedback, and Task 6 HUD.

- [x] **Step 1: Write visual evidence RED tests and stale-output guards**

Create `BattleEnemyVisualEvidenceTests.cs` under `Game.Tests.PlayMode`. Before scene load each test deletes its output and asserts it is absent. Add:

```csharp
[UnityTest]
public IEnumerator RealWaveEngagementShowsReachableEnemyFeedbackAndObjectiveHud()
{
    var output = PrepareOutput("phase-b2-wave-combat.png");
    yield return LoadBattleScene();
    var player = GameObject.Find("Player");
    var grunt = default(Component);
    yield return WaitForActiveEnemy("Grunt", found => grunt = found);
    yield return WaitUntilPlayerAndEnemyShareCamera(player, grunt.gameObject, 300);
    TriggerRealPlayerAttackWhenInRange(player, grunt);
    yield return WaitForResolvedHitFeedback(grunt, 180);
    PixelMetrics metrics;
    var captureState = ConfigureBattleHudCanvasForCapture(Camera.main);
    try
    {
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;
        metrics = CaptureWorldAndBattleHudSynchronous(Camera.main, output, 960, 540);
    }
    finally
    {
        RestoreBattleHudCanvas(captureState);
    }
    AssertWaveFrame(metrics, player, grunt);
}

[UnityTest]
public IEnumerator RealBossCircleTelegraphShowsBossHudAndReadableDangerArea()
{
    var output = PrepareOutput("phase-b2-boss-telegraph.png");
    yield return LoadBattleScene();
    var boss = SpawnRealBossThroughCurrentSpawner();
    SetDeterministicBossAoeSeedAndAssertPreparedAttackId(boss, "boss_aoe");
    StartRealPreparedAttack(boss);
    yield return WaitForVisibleTelegraph(boss, "Circle", 180);
    PixelMetrics metrics;
    var captureState = ConfigureBattleHudCanvasForCapture(Camera.main);
    try
    {
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;
        metrics = CaptureWorldAndBattleHudSynchronous(Camera.main, output, 960, 540);
    }
    finally
    {
        RestoreBattleHudCanvas(captureState);
    }
    AssertBossFrame(metrics, boss);
}
```

`ConfigureBattleHudCanvasForCapture` synchronously saves the BattleHUD Canvas state, switches it from ScreenSpaceOverlay to ScreenSpaceCamera, and assigns Camera.main. The `[UnityTest]` coroutine, not the capture helper, then forces Canvas layout and crosses two ordinary frame boundaries before calling synchronous `CaptureWorldAndBattleHudSynchronous`. Do not use `WaitForEndOfFrame`: the B1 BatchMode evidence showed it can hang when the battle result owns time scale 0. The synchronous helper renders to a 960x540 RenderTexture and encodes PNG through the same reflection-safe ImageConversion approach as B1. `finally` must restore Canvas, Camera, active render target, and camera target texture on every path.

Preserve RED in a separate `B2BaselineVisualContractTests` fixture created only in the disposable `e64475b5` worktree. It must compile without any Phase B2 production symbol: discover business types through `AppDomain.CurrentDomain.GetAssemblies()`, discover scene components by comparing `component.GetType().Name`, and obtain the optional `Circle` value only through `Enum.Parse` on the reflected `EnemyTelegraphShape` type. Do not directly reference `EnemyTelegraphShape`, `WaveObjectiveView`, `BossHPBar`, or `AttackTelegraphView` anywhere in this baseline fixture. Assert behaviors rather than mere type existence: the wave test must fail because no active named wave-objective component displays non-empty wave/alive values; the boss test must fail because the pooled Boss has neither an active named boss-HP component bound to its current/max HP nor an active named telegraph component reporting visible `Circle` state. Use the exact assertion markers `B2_RED_WAVE_HUD` and `B2_RED_BOSS_TELEGRAPH`.

After defining the serial `Invoke-UnityTests` helper from Preparation in that disposable worktree, record both intentional failures:

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.B2BaselineVisualContractTests.BaselineWavePresentationContractFailsBehaviorally' -ResultPath 'Logs/B2-task7-red-wave.xml' -LogPath 'Logs/B2-task7-red-wave.log' -ExpectFailure -ExpectedFailureMessage 'B2_RED_WAVE_HUD'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.B2BaselineVisualContractTests.BaselineBossPresentationContractFailsBehaviorally' -ResultPath 'Logs/B2-task7-red-boss.xml' -LogPath 'Logs/B2-task7-red-boss.log' -ExpectFailure -ExpectedFailureMessage 'B2_RED_BOSS_TELEGRAPH'
```

The current Task 7 `BattleEnemyVisualEvidenceTests` is the post-Tasks-1-6 GREEN fixture shown above. Its string-shaped telegraph wait may resolve the final enum and component internally through reflection, but acceptance remains behavioral: visible warning geometry, bound HUD values, resolved hit feedback, and fresh output files.

- [x] **Step 2: Run focused visual tests three times serially**

```powershell
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyVisualEvidenceTests' -ResultPath 'Logs/B2-task7-visual-1.xml' -LogPath 'Logs/B2-task7-visual-1.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyVisualEvidenceTests' -ResultPath 'Logs/B2-task7-visual-2.xml' -LogPath 'Logs/B2-task7-visual-2.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleEnemyVisualEvidenceTests' -ResultPath 'Logs/B2-task7-visual-3.xml' -LogPath 'Logs/B2-task7-visual-3.log'
```

Expected: each run is `2/2`, writes both fresh images, exits normally, and produces no native crash. Hashes may differ only if intentionally documented random particles remain; structural pixel metrics and projection bounds must pass every run.

- [x] **Step 3: Apply objective visual gates**

Wave frame must prove:

- Player and a real non-repositioned Grunt are both within viewport bounds with projected sprite height >=24 px.
- At least one active DamageNumber/ink feedback element belongs to the just-resolved hit; no prior-run text or particle is present.
- Wave and alive text are non-empty; player status HUD does not overlap them.

Boss frame must prove:

- A real Boss, circular Telegraph, and ground are visible.
- Telegraph rendered bounds cover the plan's circle bounds and use the unparryable red color.
- Boss HP max/value match the spawned Boss; phase, wave, and alive texts fit their containers.
- Quantized color count, dark/light/chromatic populations, and luminance variance are nontrivial. Do not approve a technically nonblank but unreadable frame.

- [x] **Step 4: Parent opens and approves the exact final PNGs**

Open `Logs/phase-b2-wave-combat.png` and `Logs/phase-b2-boss-telegraph.png` after the third run. Record APPROVED only after checking framing, warning-to-hit geometry, text fit, no overlap, no stale particle/number, and no blank/solid render. If either image fails, fix the owning runtime/test code, rerun focused tests three times, and repeat both specification and quality review for the changed task surface before continuing.

- [x] **Step 5: Run complete regression gates serially**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"
Invoke-UnityTests -Platform EditMode -ResultPath 'Logs/B2-final-full-editmode.xml' -LogPath 'Logs/B2-final-full-editmode.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-final-full-playmode-1.xml' -LogPath 'Logs/B2-final-full-playmode-1.log'
Invoke-UnityTests -Platform PlayMode -ResultPath 'Logs/B2-final-full-playmode-2.xml' -LogPath 'Logs/B2-final-full-playmode-2.log'
Invoke-UnityTests -Platform PlayMode -Filter 'Game.Tests.PlayMode.BattleSceneOfflineSmokeTests' -ResultPath 'Logs/B2-final-smoke.xml' -LogPath 'Logs/B2-final-smoke.log'
```

Expected: asset integrity and Pester `5/5` pass; full EditMode and both full PlayMode runs have identical totals and zero failures; smoke passes with fresh scene-owned CameraRig, Enemy pools, InkParticlePool, HUD, and delegates.

- [x] **Step 6: Run static and repository hygiene gates**

```powershell
rg -n "Time\.timeScale\s*=" Assets/Scripts
rg -n "ReceiveHit\(" Assets/Scripts/Game
rg -n "ResolveHit\(" Assets/Scripts/Game
rg -n "DontDestroyOnLoad" Assets/Scripts/Game/Visual/InkParticlePool.cs
rg -n "StartCoroutine\(" Assets/Scripts/Game/Enemy
git diff e64475b543bb37d2cf3c3becdb4f78e9c109f5bf -- Assets/Scripts/Game/Combat/ObjectPool.cs
git diff --check e64475b543bb37d2cf3c3becdb4f78e9c109f5bf
git status --short
```

Expected: only BattleTimeController writes time scale; ReceiveHit is only the Hurtbox compatibility wrapper; production hit producers use `CombatHitResolver.ResolveAndPublish`, with direct `TakeDamage` limited to the documented Hurtbox compatibility path and BloodStyle self-cost; InkParticlePool is not persistent; enemy attack coroutine ownership is centralized; ObjectPool diff is empty; diff check is clean; no transient Unity scenes, crash dumps, generated PNG/XML/log files, or ignored SDD reports are staged.

- [x] **Step 7: Run Task 7 specification review, then quality review**

Specification review checks real-path captures, stale-output deletion, Canvas/Camera restoration, exact 960x540 outputs, both required visual stories, parent inspection, documentation truth, and Phase C/A4 exclusions. Only after PASS, quality review checks cleanup in every exception path, deterministic waiting, pixel/projection gates, and non-flaky test timing.

- [x] **Step 8: Run an independent whole-branch review**

Review `e64475b543bb37d2cf3c3becdb4f78e9c109f5bf` through the complete working tree. Fix every Critical/Important finding before commit, rerun the affected focused suites plus both full suites, and repeat whole-branch review until PASS. Do not defer an observed combat, lifecycle, or stale-state bug to Phase C merely because it appears during presentation testing.

- [x] **Step 9: Record exact evidence without placeholders**

Update CLAUDE, project overview, the B2 design evidence note, and this plan with actual XML totals/paths, Pester result, image byte sizes/SHA-256/pixel metrics, parent visual verdict, review verdicts, final task commit SHAs, skipped checks, and remaining A4/Phase C work. Do not write predicted totals, placeholder markers, or a commit's own not-yet-known SHA.

- [ ] **Step 10: Commit once and push once**

```powershell
git add Assets/Tests/PlayMode/BattleEnemyVisualEvidenceTests.cs Assets/Tests/PlayMode/BattleEnemyVisualEvidenceTests.cs.meta CLAUDE.md .claude/memory/project-overview.md docs/superpowers/specs/2026-07-19-phase-b2-enemy-combat-experience-design.md docs/superpowers/plans/2026-07-19-phase-b2-enemy-combat-experience.md
git commit -m "test: add phase b2 visual evidence"
git push origin feature/phase-b2-enemy-combat-experience
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote origin refs/heads/feature/phase-b2-enemy-combat-experience) -split '\s+')[0]
if ($local -ne $remote) { throw "Task 7 push mismatch" }
```

- [ ] **Step 11: Fast-forward master and verify final remote delivery**

After invoking `superpowers:finishing-a-development-branch`, return to the primary worktree and fast-forward only after it is clean:

```powershell
Set-Location E:\Own_project\game-client-unity
git status --short --branch
git fetch origin
git merge --ff-only feature/phase-b2-enemy-combat-experience
git push origin master
$localMaster = (git rev-parse master).Trim()
$remoteMaster = ((git ls-remote origin refs/heads/master) -split '\s+')[0]
if ($localMaster -ne $remoteMaster) { throw "Master delivery mismatch" }
Write-Output "delivered=$localMaster"
```

Remove the Phase B2 worktree only after master and origin/master equal the reviewed feature head and the worktree is clean. This final fast-forward adds no commit; the seven task commits remain the reviewable history.

**Commit:** `test: add phase b2 visual evidence`

## Phase B2 最终交付证据（2026-07-20）

Task 1-6 的真实提交为：

| Task | SHA | 交付 |
|---|---|---|
| 1 | `d05cd073cca600cd2aaabe482bccab392d5f6be2` | deterministic enemy/wave models |
| 2 | `613487771d9a1c0c65bf0ac460da0c507d365460` | two-sided spawn and camera framing |
| 3 | `c4b3e83445f8bfc2a36b2e4bdb28c6e3289f07f5` | frozen attack plans |
| 4 | `02396ace5562c2471a4ac667d7e52f311241b9a2` | Telegraph/Commit/cancel/parry |
| 5 | `5bcf2a29c2c3ec8c1903ac1364a8540c8f0b9289` | resolved hit feedback and scene Ink lifecycle |
| 6 | `06b8c93d823b6961f590286bf6851635027b0fe5` | wave objective and Boss HUD |

Task 7 由包含本记录的提交交付。Git 提交无法在自身内容中稳定记录自身 SHA，因此这里不写自引用 SHA 或占位符；Step 10/11 的 commit、push、master fast-forward 与清理结果由提交后的本地/远端 SHA 核验及最终交付报告证明。

### RED 与复审修复

- disposable `e64475b5` baseline：`Logs/B2-task7-red-wave.xml` 和 `B2-task7-red-boss.xml` 均为 `1/0/1`，分别命中 `B2_RED_WAVE_HUD`、`B2_RED_BOSS_TELEGRAPH`。
- Task 7 运行时/视觉 RED：`B2-task7-camera-red.xml`、`B2-task7-layout-red-wave.xml`、`B2-task7-layout-red-boss2.xml` 均闭合并验证真实相机/HUD/Circle 缺口。
- 质量审查 RED：`B2-task7-random-state-red.xml`、`B2-task7-feedback-pixels-red.xml`、`B2-task7-telegraph-pixels-red.xml` 均为 `1/0/1`；修复后保存/恢复 `Random.state`，DamageNumber/Ink/Circle 都有 active/renderable/viewport 与可归属 ROI 像素差。
- 完整分支审查 RED：Boss charge/slam 结算原点、Telegraph 下一物理步漂移和 Poison 旧租约分别记录在 `B2-whole-review-boss-charge-red.xml`、`B2-whole-review-boss-slam-red.xml`、`B2-whole-review-telegraph-drift-red.xml`、`B2-whole-review-poison-lease-red.xml`，均 `1/0/1`；对应 GREEN 均 `1/1`。

### 最终 GREEN

- visual：`Logs/B2-task7-quality-visual-1.xml`、`-2.xml`、`-3.xml` 均 `2/2`。
- focused：`Logs/B2-final-review-core-green.xml` `49/49`；`B2-final-review-enemy-green.xml` `39/39`；`B2-final-review-combat-green.xml` `37/37`。
- full：`Logs/B2-final-reviewed-full-editmode.xml` `160/160`；`B2-final-reviewed-full-playmode-1.xml` 与 `-2.xml` 均 `92/92`；`B2-final-reviewed-smoke.xml` `3/3`。
- 所有最终 XML 完整且 skipped `0`；对应日志 compiler error `0`、native crash marker `0`，Unity 正常退出。
- Asset integrity PASS；Pester `5/5` PASS；唯一 `Time.timeScale` 写入、canonical `ResolveHit`、Enemy coroutine ownership、scene-owned Ink、ObjectPool 无基线改动、GUID 唯一和 `git diff --check` 静态门禁均 PASS。

### 最终视觉与审查

- 父级 APPROVED `Logs/phase-b2-wave-combat.png`：960x540，101674 bytes，opaque `518272`、dark `8473`、light `506624`、chromatic `73868`、colors `112`、variance `560.11`、Player `29.12px`、Grunt `24.27px`、damage `20`、ink `7`，SHA-256 `2AEABB48FDB548F7F8E3CA072B0ECB2AA5999CCC7B83250A0BC7A07B33B74DF0`。
- 父级 APPROVED `Logs/phase-b2-boss-telegraph.png`：960x540，122543 bytes，opaque `518400`、dark `12998`、light `500312`、chromatic `86814`、colors `139`、variance `809.23`、Boss `48.54px`、Circle `485.39px`、radius `4.00`，SHA-256 `68B6022A192CE43FBF69EAB5265B7A695A52CE6F19AB84125445FE570DD37350`。
- Task 7 specification review PASS；quality re-review PASS；最终 independent whole-branch review PASS，Critical/Important/Minor 均为 0。
- 要求的测试和审查没有跳过。剩余产品范围是 Phase A4 Online/MainMenu/真实后端联调，以及 Phase C Prefab/Animator/Addressables/AssetBundle/资源缓存和打包工程化。
