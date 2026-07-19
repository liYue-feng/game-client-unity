# Phase B1 Playable Combat Loop Implementation Plan

> Execute with `subagent-driven-development`: one fresh implementer per task, then independent spec and quality review. The parent agent pushes each reviewed task.

**Base commit:** `91e775aaf34f31ead70d9b7ca7adfe245a0e2daa`

**Goal:** Deliver an Offline battle vertical slice where a light attack damages and kills enemies, parry semantics are safe and source-aware, rejected actions do not consume stamina, time effects compose correctly, and victory/defeat can restart into a clean run.

**Design:** `docs/superpowers/specs/2026-07-19-phase-b1-playable-combat-loop-design.md`

## Global Execution Rules

- Use worktree `E:\Own_project\game-client-unity\.worktrees\phase-b1-playable-combat-loop` on branch `feature/phase-b1-playable-combat-loop`.
- Unity executable: `D:\Unity_Soft\2022\Editor\Unity.exe`.
- Unity test commands that need XML must not use `-quit` on this machine. Launch the command, then poll the XML path until Unity exits or the file appears.
- Follow RED-GREEN-REFACTOR. For Unity compile RED, preserve the exact compiler error log when missing production APIs prevent XML generation.
- Every new Unity asset, folder, asmdef, and C# file must have a unique checked-in `.meta` file before the asset-integrity gate.
- `Game.PlayModeTests` may directly reference `Game.Gameplay`, but it must continue using reflection for adapters in `Assembly-CSharp`.
- Preserve the Offline default, network protocol, A1-A3 lifecycle, and dynamic `BattleSceneSetup` baseline.
- Implementers do not push. After independent review and parent verification, push the task commit before starting the next task.
- Do not copy company XLua, AssetBundle code, protocols, configuration, or assets.

## Preflight

1. Create the isolated worktree from the exact base.
2. Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1`.
3. Run Pester:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"
```

4. Run full EditMode and PlayMode to `Logs/B1-baseline-editmode.xml` and `Logs/B1-baseline-playmode.xml`.
5. Expected baseline from the last verified branch: EditMode 88/88, PlayMode 11/11, Pester 5/5. If current totals differ, stop and explain the concrete drift before implementation.

---

## Task 1: Add The Tested Gameplay Core Assembly

**Files**

- Create `Assets/Scripts/Gameplay.meta`.
- Create `Assets/Scripts/Gameplay/Game.Gameplay.asmdef` and `.meta`.
- Create `Assets/Scripts/Gameplay/CombatHit.cs` and `.meta`.
- Create `Assets/Scripts/Gameplay/AttackTimeline.cs` and `.meta`.
- Create `Assets/Scripts/Gameplay/CombatActionPolicy.cs` and `.meta`.
- Create `Assets/Scripts/Gameplay/TimeScaleRequestSet.cs` and `.meta`.
- Create `Assets/Scripts/Gameplay/BattleRunStateMachine.cs` and `.meta`.
- Modify `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef` to reference `Game.Gameplay`.
- Modify `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef` to reference `Game.Gameplay`.
- Create `Assets/Tests/EditMode/Gameplay.meta`.
- Create `Assets/Tests/EditMode/Gameplay/CombatCoreTests.cs` and `.meta`.

**Required contracts**

- Namespace all new types under `Game.Gameplay`.
- `Game.Gameplay.asmdef` must set `autoReferenced: true` (plus empty platform/constraint lists and `noEngineReferences: false`) so predefined `Assembly-CSharp` can consume the core from Task 2 onward. The dependency is one-way: Assembly-CSharp -> Game.Gameplay.
- `IParryResponder.OnParried()` is the only source callback.
- `CombatHit` is immutable and contains damage, knockback X, knockback force, `IsParryable`, and optional `IParryResponder Source`. Add `CombatHitResult` values Damaged, Parried, and Ignored.
- `AttackTimeline` normalizes invalid durations and evaluates `Windup`, `Active`, `Recovery`, or `Complete` from elapsed time.
- `CombatActionPolicy` accepts only `transitionAllowed`, `onCooldown`, current stamina, and cost. It must not reference `PlayerState` or any `Assembly-CSharp` type.
- `TimeScaleRequestSet` returns a unique token per request, supports same-reason overlap, and exposes the minimum active scale or 1 when empty.
- `BattleRunStateMachine` accepts only the first Victory/Defeat, then supports Restarting and idempotent Dispose.

**TDD**

1. Add tests first for timeline phase boundaries, zero/negative normalization, action rejection/allowance, same-reason token reverse release, and terminal run transitions.
2. Run focused EditMode and record RED for missing `Game.Gameplay` APIs.
3. Implement the smallest pure core. No MonoBehaviour or scene lookup is allowed in this assembly.
4. Run focused EditMode, full EditMode, asset integrity, and `git diff --check`.

**Commit:** `feat: add tested gameplay combat core`

---

## Task 2: Drive Player Attack Frames And Authorize Costs Before Spending

**Files**

- Modify `Assets/Scripts/Game/State/PlayerStateMachine.cs`.
- Modify `Assets/Scripts/Game/Combat/Hitbox.cs` only for idempotent active-window support needed by the timeline.
- Modify `Assets/Scripts/Game/BattleSceneSetup.cs`.
- Create `Assets/Tests/PlayMode/BattleCombatLoopTests.cs` and `.meta`.

**Behavior**

- Add `ConfigureAttackHitbox(Hitbox)` and have `BattleSceneSetup` call it after creating `AttackHitbox`.
- On every attack state, create an `AttackTimeline` from that attack's duration. Enable the Hitbox once on entering Active; disable it on leaving Active, exiting attack, disable, death, and destroy.
- A single active window must not hit one Hurtbox more than once. Existing `MarkHit` continues to unlock combo behavior.
- HeavyAttack, Dash, and Parry must check transition/cooldown with `CombatActionPolicy` before calling `TryUseStamina`. Rejected actions produce no state, stamina, or audio change.

**TDD**

1. Add reflection-based PlayMode tests before implementation:
   - load real `BattleScene`, wait for a Grunt whose GameObject belongs to the active scene and is `activeInHierarchy` (exclude prewarmed inactive pool entries), move/freeze it inside the attack box, set high deterministic HP, request Attack1, require one HP reduction and no second reduction after the active phase;
   - force Hurt, request Heavy/Dash/Parry, and require stamina unchanged;
   - require an allowed action consumes the configured cost once.
2. Run focused PlayMode and record behavioral RED: player hitbox never becomes active and rejected actions spend stamina.
3. Implement timeline and cost ordering.
4. Run focused PlayMode repeatedly (`-testFilter BattleCombatLoopTests`) and full EditMode/PlayMode.

**Commit:** `feat: connect player attack timing and stamina rules`

---

## Task 3: Route Every Attack Through CombatHit And One Parry Entry

**Files**

- Modify `Assets/Scripts/Game/Combat/Hitbox.cs`.
- Modify `Assets/Scripts/Game/Combat/Hurtbox.cs`.
- Delete `Assets/Scripts/Game/Combat/ParryHitbox.cs` and `.meta`.
- Modify `Assets/Scripts/Game/Enemy/EnemyBase.cs`.
- Modify `Assets/Scripts/Game/Enemy/Grunt.cs`.
- Modify `Assets/Scripts/Game/Enemy/Elite.cs`.
- Modify `Assets/Scripts/Game/Enemy/Boss.cs`.
- Modify `Assets/Scripts/Game/Enemy/Projectile.cs`.
- Modify `Assets/Scripts/Game/BattleSceneSetup.cs` to stop creating ParryHitbox.
- Modify `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`.

**Behavior**

- Replace `Hurtbox.ReceiveHit(int, float, float, Hitbox)` with `CombatHitResult ReceiveHit(CombatHit)`.
- Hitbox and every direct enemy/projectile call construct a non-null value contract. No call may pass `null` to express parry semantics.
- `EnemyBase` implements `IParryResponder` and enters Stunned once when parried.
- `Projectile` implements `IParryResponder` and calls its existing `Deflect` behavior once. Its collision path must inspect `CombatHitResult`: on Parried it must skip the original Destroy/return path and remain alive in reflected flight; normal damage still destroys/returns it.
- Player parry succeeds only inside `Hurtbox.ReceiveHit`: no damage, one `OnParrySuccess`, one source callback. Unparryable attacks always follow normal damage.

**TDD**

1. Extend PlayMode tests with a `Game.PlayModeTests` fake `IParryResponder`:
   - parryable hit in the window leaves HP unchanged, enters ParrySuccess, and calls source once;
   - unparryable hit reduces HP and never calls source;
   - a second terminal/contact path does not double-call the responder.
   - a real Projectile colliding during the parry window reverses ownership/direction, remains alive for at least one frame, and is not destroyed by the original collision branch.
2. Run focused PlayMode and record the existing null dereference/missing source callback RED.
3. Implement the canonical pipeline and delete the second ParryHitbox path.
4. Run `rg -n "ReceiveHit\\(" Assets/Scripts/Game` and verify every call supplies `CombatHit`; run focused and full suites.

**Commit:** `fix: unify combat hit and parry semantics`

---

## Task 4: Centralize Battle Time And Hotkey Ownership

**Files**

- Create `Assets/Scripts/Game/Combat/BattleTimeController.cs` and `.meta`.
- Modify `Assets/Scripts/Game/State/PlayerStateMachine.cs`.
- Modify `Assets/Scripts/Game/Visual/HitStopController.cs`.
- Modify `Assets/Scripts/UI/BattleUI/PauseMenuUI.cs`.
- Modify `Assets/Scripts/UI/BattleUI/LevelUpUI.cs`.
- Modify `Assets/Scripts/UI/BattleUI/InventoryUI.cs`.
- Modify `Assets/Scripts/Game/BattleSceneSetup.cs`.
- Modify `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`.

**Behavior**

- `BattleTimeController` is scene-owned and the only production writer of `Time.timeScale`.
- It converts unique `TimeScaleRequestSet` tokens into the effective scale and restores 1 on destroy.
- Player parry slow motion, each overlapping HitStop, Pause, LevelUp, and later BattleResult own and release separate tokens.
- `PauseMenuUI` and `InventoryUI` stop polling Escape/Tab. Only `InputMediator -> BattleSceneSetup` consumes battle hotkeys.
- Add `BattleSceneSetup.BattleHotkeysEnabled`; terminal flow can disable it without disabling cleanup.

**TDD**

1. Add PlayMode tests for Pause + SlowMotion and two overlapping HitStops released in opposite order. The remaining request must keep the effective scale.
2. Add a hotkey ownership assertion that PauseMenuUI/InventoryUI no longer toggle independently of Setup.
3. Run focused PlayMode and record RED from direct `Time.timeScale` writers/double input consumers.
4. Implement explicit initialization from `BattleSceneSetup` to time consumers.
5. Run `rg -n "Time\\.timeScale\\s*=" Assets/Scripts`; only `BattleTimeController` may remain. Run focused and full suites.

**Commit:** `refactor: centralize battle time and hotkeys`

---

## Task 5: Make Pools And Waves Belong To One Battle Run

**Files**

- Modify `Assets/Scripts/Game/Combat/ObjectPool.cs`.
- Modify `Assets/Scripts/Game/Dungeon/WaveSpawner.cs`.
- Modify `Assets/Scripts/Game/BattleSceneSetup.cs`.
- Modify `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`.
- Modify `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`.

**Behavior**

- Remove `DontDestroyOnLoad` from ObjectPool. Pool roots, queued objects, active enemies, weapons, and factories belong to the BattleScene.
- `ObjectPool.OnDestroy` clears collections and resets static `Instance` only when it owns the singleton.
- `WaveSpawner` tracks each enemy death delegate. `Dispose` is idempotent, stops spawn/return coroutines, unbinds every live delegate, clears active state, and releases old factory references.
- `BattleSceneSetup.OnDestroy` invokes WaveSpawner.Dispose before scene objects disappear.

**TDD**

1. Extend reload tests to capture old pool/spawner/enemy instance IDs, reload BattleScene, and require:
   - old pool and all old enemies are destroyed;
   - one new pool exists with a different ID;
   - same keys register against the new spawner;
   - old death delegates do not target the destroyed setup/spawner.
2. Run focused reload tests and record current persistent-pool RED.
3. Implement scene ownership and explicit Dispose.
4. Run reload tests at least five times, then full PlayMode.

**Commit:** `fix: scope combat pools to battle runs`

---

## Task 6: Complete Victory, Defeat, Result UI, And Restart

**Files**

- Create `Assets/Scripts/Game/BattleRunController.cs` and `.meta`.
- Modify `Assets/Scripts/Game/BattleSceneSetup.cs`.
- Modify `Assets/Scripts/Game/Character/PlayerInputBridge.cs`.
- Modify `Assets/Scripts/Game/State/PlayerStateMachine.cs`.
- Modify `Assets/Scripts/Game/Combat/Hurtbox.cs`.
- Modify `Assets/Scripts/UI/BattleUI/GameOverUI.cs`.
- Modify `Assets/Scripts/Managers/SceneTransitionManager.cs` only if a BattleScene restart command is missing.
- Modify `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`.
- Modify `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`.

**Behavior**

- BattleRunController subscribes to `CharacterStats.OnDeath` and `WaveSpawner.OnAllWavesComplete`; first terminal outcome wins.
- CharacterStats.OnDeath is the sole death source. PlayerStateMachine entering Die must not raise death again; Hurtbox must not publish player death directly.
- Add a public read-only `PlayerInputBridge.InputEnabled` gate plus an explicit setter/configuration method; Update returns before consuming actions when disabled. On terminal outcome: force Die when needed, publish legacy `CombatEvents.OnPlayerDeath` once for current achievement/presentation consumers, disable PlayerInputBridge and Setup hotkeys, acquire BattleResult time token, and show one scene-owned GameOverUI.
- GameOverUI is not persistent, does not rebuild twice, clears Instance on destroy, and exposes only Restart while MenuScene remains unimplemented.
- BattleSceneSetup creates exactly one EventSystem with StandaloneInputModule.
- Restart calls idempotent Dispose, restores time/input, disposes waves, then reloads BattleScene. OnDestroy calls the same Dispose path.

**TDD**

1. Add PlayMode tests before implementation:
   - lethal damage produces one Defeat and one result UI despite duplicate follow-up calls;
   - invoke the real WaveSpawner completion handler via reflection and require Victory;
   - terminal state blocks attack and hotkey processing even though Update still runs at scale 0;
   - invoke Restart, wait for reload, require time 1, new Player/Pool/Spawner IDs, no old result UI, and exactly one EventSystem.
2. Run focused PlayMode and record missing result/restart RED.
3. Implement run ownership and UI cleanup.
4. Run the focused tests repeatedly, existing reload tests, and full PlayMode.

**Commit:** `feat: complete battle results and restart loop`

---

## Task 7: Final Regression, Visual Evidence, And Delivery Notes

**Files**

- Create `Assets/Tests/PlayMode/BattleVisualEvidenceTests.cs` and `.meta`.
- Modify `CLAUDE.md`.
- Modify `.claude/memory/project-overview.md`.
- Modify this plan to record exact evidence.

**Visual probes**

- Combat-world probe: load real BattleScene, wait for an active-scene/active-in-hierarchy Grunt, put it in attack range, trigger Attack1, render `Camera.main` into a 960x540 RenderTexture, require nontrivial pixel variance, and write `Logs/phase-b1-combat.png`. This probe validates world framing only; it does not claim to capture ScreenSpaceOverlay HUD.
- Result-UI probe: trigger a real Defeat, find the GameOverUI Canvas, temporarily switch that Canvas from ScreenSpaceOverlay to ScreenSpaceCamera with `Camera.main`, render into a separate 960x540 RenderTexture, require both dark overlay and light result-panel pixel populations, write `Logs/phase-b1-result.png`, and restore Canvas render mode/camera in `finally`.
- The parent agent opens both PNGs. It checks player/enemy/world framing in the combat image and verifies title/buttons fit without overlap in the result image.

**Final gates**

1. Asset integrity passes.
2. Pester passes 5/5.
3. Full EditMode and PlayMode produce fresh XML with zero failures; both visual probe tests pass and write their distinct images.
4. Focused combat tests pass repeatedly.
5. `git diff --check 91e775aaf34f31ead70d9b7ca7adfe245a0e2daa..HEAD` is clean.
6. `rg -n "Time\\.timeScale\\s*=" Assets/Scripts` reports only BattleTimeController.
7. `rg -n "ReceiveHit\\(" Assets/Scripts/Game` reports only the CombatHit signature/callers.
8. No `ParryHitbox` script/component remains.
9. Scene reload leaves one EventSystem, one scene-owned ObjectPool, one current Player, no old result UI, and time scale 1.
10. Record exact XML totals, log paths, screenshot path, commit SHAs, skipped/manual checks, and residual Phase B2/C work.

**Commit:** `docs: record phase b1 combat verification`

## Final Branch Gate

- Request a whole-branch review from base `91e775a` to HEAD.
- Fix every Critical/Important finding and re-review.
- Re-run asset integrity, Pester, full EditMode, full PlayMode, both screenshot pixel checks, and `git diff --check` after the final fix.
- Fast-forward merge to client master and push only when local and remote SHAs can be verified equal.
