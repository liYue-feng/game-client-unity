# Phase A2 Application Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build one automatic Offline application entry that owns cross-scene services, enters `BattleScene`, survives scene reloads, and shuts down without stale static state.

**Architecture:** A small `Game.Core` assembly contains settings validation, lifecycle state, service sequencing, and the bounded main-thread dispatcher. `GameApplication` remains in the predefined runtime assembly so it can compose existing managers under one persistent root without forcing a broad assembly migration. Runtime initialization creates the application before scene `Awake`, while legacy bootstraps become inert compatibility components.

**Tech Stack:** Unity 2022.3.47f1, C#, Unity Test Framework 1.1.33, NUnit, Pester 3.4.0, WebSocketSharp retained but unused by Offline startup.

## Global Constraints

- Editor default is `RuntimeMode.Offline` with startup scene exactly `BattleScene`.
- A2 must not create `NetworkClient`, `LoginManager`, `ArchiveManager`, `RankManager`, `HeartbeatManager`, or `ReconnectionManager` in Offline mode.
- A2-owned services are exactly `MainThreadDispatcher`, `SceneTransitionManager`, `AudioManager`, `LoadingScreen`, and `AchievementManager`.
- Initialization is forward order; rollback and shutdown are reverse order and idempotent.
- Existing gameplay pools, inventory, talent, summon, elemental, and combat singletons remain outside A2.
- Do not modify `E:/client/zhetian_client`; it is read-only reference material.
- Every Unity conclusion requires a fresh Unity batch run and parsed test-result XML.
- Do not commit `Library`, `Logs`, `Temp`, generated solution files, or test-result XML.

## File Map

- Create `Assets/Scripts/Core/Game.Core.asmdef`: isolated lifecycle assembly.
- Create `Assets/Scripts/Core/RuntimeMode.cs`: Offline/Online mode enum.
- Create `Assets/Scripts/Core/GameApplicationState.cs`: application states and guarded transition object.
- Create `Assets/Scripts/Core/GameRuntimeSettings.cs`: runtime configuration and deterministic validation.
- Create `Assets/Scripts/Core/IGameService.cs`: service lifecycle contract.
- Create `Assets/Scripts/Core/GameServiceCollection.cs`: ordered initialization, rollback, and idempotent shutdown.
- Move `Assets/Scripts/Network/MainThreadDispatcher.cs` and its `.meta` to `Assets/Scripts/Core/`: preserve GUID while placing dispatcher in `Game.Core`.
- Create `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef`: EditMode test assembly referencing `Game.Core`.
- Create `Assets/Tests/EditMode/GameRuntimeSettingsTests.cs`: settings and state transition tests.
- Create `Assets/Tests/EditMode/GameServiceCollectionTests.cs`: ordering, rollback, and idempotency tests.
- Create `Assets/Tests/EditMode/MainThreadDispatcherTests.cs`: queue budget, isolation, and shutdown tests.
- Create `Assets/Scripts/Application/GameServices.cs`: default-assembly composition root for existing services.
- Create `Assets/Scripts/Application/GameApplication.cs`: application coordinator and scene flow.
- Create `Assets/Scripts/Application/RuntimeBootstrap.cs`: runtime initialization hooks and static reset.
- Create `Assets/Editor/GameRuntimeSettingsAssetCreator.cs`: deterministic creation of the committed settings asset.
- Create `Assets/Resources/GameRuntimeSettings.asset`: default Offline settings.
- Modify the five A2 service classes: explicit install, `IGameService`, idempotent shutdown, no lazy object creation.
- Modify `Assets/Scripts/GameBootstrap.cs` and `Assets/Scripts/Game/MenuSceneSetup.cs`: inert when the new application exists.
- Modify `Assets/Scripts/Game/BattleSceneSetup.cs`: consume preinstalled global services without prewarming them.
- Modify `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`: verify automatic startup and reload stability.
- Modify `.claude/memory/project-overview.md` and `CLAUDE.md`: record the verified A2 architecture.

---

### Task 1: Core Settings, State, and Service Ordering

**Files:**
- Create: `Assets/Scripts/Core/Game.Core.asmdef`
- Create: `Assets/Scripts/Core/RuntimeMode.cs`
- Create: `Assets/Scripts/Core/GameApplicationState.cs`
- Create: `Assets/Scripts/Core/GameRuntimeSettings.cs`
- Create: `Assets/Scripts/Core/IGameService.cs`
- Create: `Assets/Scripts/Core/GameServiceCollection.cs`
- Create: `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef`
- Create: `Assets/Tests/EditMode/GameRuntimeSettingsTests.cs`
- Create: `Assets/Tests/EditMode/GameServiceCollectionTests.cs`

**Interfaces:**
- Produces: `RuntimeMode`, `GameApplicationState`, `GameApplicationLifecycle`, `GameRuntimeSettings.TryValidate(Func<string,bool>, out string)`, `IGameService`, and `GameServiceCollection`.
- Consumes: Unity `ScriptableObject`, `Uri`, and NUnit only in tests.

- [x] **Step 1: Create assembly definitions and write failing settings/state tests**

Create `Game.Core.asmdef`:

```json
{
  "name": "Game.Core",
  "rootNamespace": "Game.Core",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

Create `Game.Core.EditModeTests.asmdef` with `references: ["Game.Core"]`, `includePlatforms: ["Editor"]`, `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, and `optionalUnityReferences: ["TestAssemblies"]`.

Write tests that instantiate settings and assert:

```csharp
[Test]
public void OfflineSettingsAcceptAConfiguredBuildScene()
{
    var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);

    Assert.That(settings.TryValidate(scene => scene == "BattleScene", out var error), Is.True, error);
}

[TestCase(0)]
[TestCase(-1)]
public void MainThreadBudgetMustBePositive(int budget)
{
    var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", budget);

    Assert.That(settings.TryValidate(_ => true, out var error), Is.False);
    StringAssert.Contains("MainThreadMaxTasksPerFrame", error);
}

[Test]
public void LifecycleRejectsReadyBeforeInitialization()
{
    var lifecycle = new GameApplicationLifecycle();
    Assert.Throws<InvalidOperationException>(() => lifecycle.MarkReady());
}
```

The EditMode test helper uses `UnityEditor.SerializedObject` to set `_runtimeMode`, `_startupSceneName`, `_serverUrl`, and `_mainThreadMaxTasksPerFrame`, then calls `ApplyModifiedPropertiesWithoutUndo`. It leaves all other fields at their valid serialized defaults; production code receives no test-only setter.

- [x] **Step 2: Run EditMode tests and verify RED**

Run Unity with `-runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.GameRuntimeSettingsTests -testResults Logs/A2-task1-red.xml -logFile Logs/A2-task1-red.log`.

Expected: compilation/test failure because the `Game.Core` types do not exist.

- [x] **Step 3: Implement settings and guarded lifecycle**

Implement the exact public surface:

```csharp
namespace Game.Core
{
    public enum RuntimeMode { Offline = 0, Online = 1 }
    public enum GameApplicationState { Created, Initializing, Ready, Failed, ShuttingDown, Stopped }

    public sealed class GameApplicationLifecycle
    {
        public GameApplicationState State { get; private set; } = GameApplicationState.Created;
        public void BeginInitialization() => Transition(GameApplicationState.Created, GameApplicationState.Initializing);
        public void MarkReady() => Transition(GameApplicationState.Initializing, GameApplicationState.Ready);
        public void MarkFailed() => Transition(GameApplicationState.Initializing, GameApplicationState.Failed);
        public void BeginShutdown()
        {
            if (State != GameApplicationState.Ready && State != GameApplicationState.Failed)
                throw new InvalidOperationException($"Cannot shut down from {State}.");
            State = GameApplicationState.ShuttingDown;
        }
        public void MarkStopped() => Transition(GameApplicationState.ShuttingDown, GameApplicationState.Stopped);
        private void Transition(GameApplicationState expected, GameApplicationState next)
        {
            if (State != expected) throw new InvalidOperationException($"Expected {expected}, actual {State}.");
            State = next;
        }
    }
}
```

`GameRuntimeSettings` must expose serialized read-only properties for mode, scene, URL, heartbeat, connection timeout, reconnect count, initial/max backoff, and main-thread budget. `TryValidate` checks every rule from the design and returns the first actionable error. Do not add public setters or conditional test code to the runtime assembly.

Declare the serialized defaults explicitly: Offline, `BattleScene`, `ws://localhost:8080/ws`, heartbeat `30`, timeout `10`, retries `5`, initial backoff `1`, max backoff `30`, and main-thread budget `64`. Expose getters only.

- [x] **Step 4: Write failing service order and rollback tests**

Use a `RecordingService : IGameService` with optional initialization failure. Assert exact order:

```csharp
CollectionAssert.AreEqual(
    new[] { "init:a", "init:b", "shutdown:b", "shutdown:a" },
    events);
Assert.That(collection.IsInitialized, Is.False);
```

Also call `ShutdownAll()` twice after success and assert every service receives one shutdown call.

- [x] **Step 5: Implement `IGameService` and `GameServiceCollection`**

```csharp
public interface IGameService
{
    string ServiceName { get; }
    void Initialize();
    void Shutdown();
}
```

`GameServiceCollection.InitializeAll()` initializes in list order. On failure it shuts down only the successful prefix in reverse order, preserves the original exception as `InnerException`, records the failed service name, and leaves `IsInitialized == false`. `ShutdownAll()` is idempotent, continues after individual shutdown errors, and returns the collected exceptions for logging by `GameApplication`.

- [x] **Step 6: Run the complete EditMode assembly and verify GREEN**

Run Unity with `-runTests -testPlatform EditMode -testFilter Game.Tests.EditMode -testResults Logs/A2-task1-green.xml -logFile Logs/A2-task1-green.log`.

Expected: all Task 1 tests pass with 0 failures and no C# compiler errors.

- [x] **Step 7: Import Unity-generated `.meta` files and commit**

Run a Unity `-batchmode -nographics -quit` import, run `tools/validation/Test-UnityAssetIntegrity.ps1`, then commit only Task 1 files and their generated `.meta` files:

```powershell
git commit -m "feat: 建立应用生命周期核心模型"
```

---

### Task 2: Bounded Main-Thread Dispatcher

**Files:**
- Move: `Assets/Scripts/Network/MainThreadDispatcher.cs` -> `Assets/Scripts/Core/MainThreadDispatcher.cs`
- Move: `Assets/Scripts/Network/MainThreadDispatcher.cs.meta` -> `Assets/Scripts/Core/MainThreadDispatcher.cs.meta`
- Create: `Assets/Tests/EditMode/MainThreadDispatcherTests.cs`

**Interfaces:**
- Consumes: `IGameService` from Task 1.
- Produces: public `MainThreadDispatcher.Install(Transform, int)`, `bool Enqueue(Action)`, `PendingCount`, and public `ResetStaticState()`.

- [x] **Step 1: Preserve the dispatcher GUID while moving it into `Game.Core`**

Use `Move-Item` for both `.cs` and `.meta`; do not create a new `.meta`. Keep namespace `Game.Network` so existing call sites remain source-compatible.

- [x] **Step 2: Write failing dispatcher lifecycle tests**

Create a GameObject, add/install the dispatcher, initialize with budget `2`, enqueue three counters, invoke one processing tick through an explicit testable `ProcessPending()` method, and assert two ran and one remains. Add tests that one throwing task does not block the next and `Shutdown()` clears the queue and makes `Enqueue` return `false`.

- [x] **Step 3: Run focused EditMode tests and verify RED**

Expected: failure because install, budget, pending count, shutdown, and bounded processing are not implemented.

- [x] **Step 4: Implement bounded processing without invoking actions under the queue lock**

Use this processing shape:

```csharp
public void ProcessPending()
{
    for (var processed = 0; processed < _maxTasksPerFrame; processed++)
    {
        Action action;
        lock (QueueLock)
        {
            if (Queue.Count == 0) return;
            action = Queue.Dequeue();
        }
        try { action?.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}
```

`Initialize` enables acceptance, `Shutdown` disables acceptance and clears the queue, `Update` calls `ProcessPending`, and `ResetStaticState` clears both static instance and queue. `Instance` returns the installed instance and logs an error instead of creating a GameObject.

`Install` and `ResetStaticState` are public because their callers live in predefined `Assembly-CSharp` and the EditMode test assembly, while the dispatcher itself lives in `Game.Core`.

- [x] **Step 5: Run focused and complete EditMode tests, compile, and commit**

Expected: dispatcher tests and all Task 1 tests pass; Unity compile succeeds; resource validator passes.

```powershell
git commit -m "refactor: 收束主线程调度器生命周期"
```

---

### Task 3: Automatic Application and Explicit Service Root

**Files:**
- Create: `Assets/Scripts/Application/GameServices.cs`
- Create: `Assets/Scripts/Application/GameApplication.cs`
- Create: `Assets/Scripts/Application/RuntimeBootstrap.cs`
- Create: `Assets/Editor/GameRuntimeSettingsAssetCreator.cs`
- Create: `Assets/Resources/GameRuntimeSettings.asset`
- Modify: `Assets/Scripts/Managers/SceneTransitionManager.cs:9-31,115-end`
- Modify: `Assets/Scripts/Managers/AudioManager.cs:22-84`
- Modify: `Assets/Scripts/UI/Common/LoadingScreen.cs:9-49`
- Modify: `Assets/Scripts/Managers/AchievementManager.cs:30-61`
- Modify: `Assets/Scripts/GameBootstrap.cs:32-69`
- Modify: `Assets/Scripts/Game/MenuSceneSetup.cs:9-36`
- Create: `Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs`

**Interfaces:**
- Consumes: Task 1 lifecycle/settings/service collection and Task 2 dispatcher.
- Produces: `GameApplication.Instance`, `HasInstance`, `State`, `FailureStage`, `FailureReason`, `Shutdown()`, and one persistent `[GameServices]` hierarchy.

- [x] **Step 1: Write the failing automatic-start PlayMode test**

Because the test assembly cannot reference predefined `Assembly-CSharp`, inspect `GameApplication` state through `Component.GetType().GetProperty("State")`. Wait up to 120 frames for `Ready`, then assert these exact objects:

```csharp
Assert.That(FindAll("[GameApplication]").Count, Is.EqualTo(1));
Assert.That(FindAll("[GameServices]").Count, Is.EqualTo(1));
Assert.That(GameObject.Find("[MainThreadDispatcher]"), Is.Not.Null);
Assert.That(GameObject.Find("[SceneTransitionManager]"), Is.Not.Null);
Assert.That(GameObject.Find("[AudioManager]"), Is.Not.Null);
Assert.That(GameObject.Find("[LoadingScreen]"), Is.Not.Null);
Assert.That(GameObject.Find("[AchievementManager]"), Is.Not.Null);
Assert.That(GameObject.Find("[NetworkClient]"), Is.Null);
Assert.That(GameObject.Find("[LoginManager]"), Is.Null);
Assert.That(GameObject.Find("[GameBootstrap]"), Is.Null);
```

Define the helper in the test file so inactive duplicates are also detected:

```csharp
private static List<GameObject> FindAll(string objectName)
{
    return Resources.FindObjectsOfTypeAll<GameObject>()
        .Where(item => item.scene.IsValid() && item.name == objectName)
        .ToList();
}
```

- [x] **Step 2: Run the PlayMode test and verify RED**

Expected: no `[GameApplication]` exists.

- [x] **Step 3: Refactor the four existing services to explicit lifecycle ownership**

For each service, replace lazy creation with an installed-instance getter:

```csharp
public static AudioManager Instance
{
    get
    {
        if (_instance == null) Debug.LogError("[AudioManager] Service is not installed by GameApplication.");
        return _instance;
    }
}

internal static AudioManager Install(Transform parent)
{
    if (_instance != null) return _instance;
    var go = new GameObject("[AudioManager]");
    go.transform.SetParent(parent, false);
    return go.AddComponent<AudioManager>();
}
```

Apply the same pattern to `LoadingScreen` and `AchievementManager`; use the existing public static property for `SceneTransitionManager` plus an internal `Install`. Remove service-level `DontDestroyOnLoad`; only `[GameApplication]` is persistent. Move heavy setup and event registration from `Awake` into idempotent `Initialize`, and undo them in `Shutdown`. Every `OnDestroy` clears its static reference only when `ReferenceEquals(_instance, this)`.

Use these exact lifecycle boundaries:

| Service | `Initialize` | `Shutdown` |
|---|---|---|
| `SceneTransitionManager` | subscribe `SceneManager.sceneLoaded` | stop active coroutines, unsubscribe, destroy `_overlayTex`, clear callbacks |
| `AudioManager` | create BGM/SFX sources and load clips | stop sources, clear pools/caches, destroy generated runtime clips |
| `LoadingScreen` | call `BuildUI`, start hidden | stop coroutines and clear UI references |
| `AchievementManager` | call `InitializeAchievements` then `LoadFromPrefs` | persist current progress once and clear public event delegates |

Every service tracks `_initialized`; repeated `Initialize` or `Shutdown` is a no-op.

- [x] **Step 4: Implement `GameServices`**

`GameServices.Create(Transform applicationRoot, GameRuntimeSettings settings)` creates `[GameServices]`, installs the five services in the design order, adds them to `GameServiceCollection`, and calls `InitializeAll`. `Shutdown` delegates to the collection, logs every returned shutdown exception, destroys the service root, and is idempotent.

The ordered construction is explicit, not reflection-based:

```csharp
var dispatcher = MainThreadDispatcher.Install(root, settings.MainThreadMaxTasksPerFrame);
var sceneTransition = SceneTransitionManager.Install(root);
var audio = AudioManager.Install(root);
var loading = LoadingScreen.Install(root);
var achievements = AchievementManager.Install(root);
_lifecycle = new GameServiceCollection(new IGameService[]
{
    dispatcher, sceneTransition, audio, loading, achievements
});
_lifecycle.InitializeAll();
```

- [x] **Step 5: Implement runtime bootstrap and application state flow**

`RuntimeBootstrap` uses:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetRuntime() => GameApplication.ResetStaticState();

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void CreateApplication()
{
    if (GameApplication.HasInstance) return;
    new GameObject("[GameApplication]").AddComponent<GameApplication>();
}
```

`GameApplication.Awake` establishes the singleton, calls `DontDestroyOnLoad`, loads `Resources/GameRuntimeSettings`, validates it with `Application.CanStreamedLevelBeLoaded`, begins lifecycle initialization, and synchronously installs services before scene components run. `Start` loads `StartupSceneName` only when it is not already active, waits for activation, then marks Ready. On error it records exact stage/reason, rolls back services, and marks Failed.

`GameApplication.ResetStaticState` sets its instance to null and calls `ResetStaticState` on all five A2 service types. Each service reset only clears static state; it does not access or destroy Unity objects during `SubsystemRegistration`.

`Shutdown` uses a re-entry guard. Explicit shutdown releases services, transitions to Stopped, clears the singleton, and destroys the application GameObject. `OnDestroy` calls the same core release path without scheduling another destroy.

- [x] **Step 6: Make legacy bootstraps inert under the new application**

At the first line of both legacy entry methods:

```csharp
if (GameApplication.HasInstance)
{
    Debug.LogWarning($"[{nameof(GameBootstrap)}] Disabled because GameApplication owns startup.");
    enabled = false;
    return;
}
```

Use `nameof(MenuSceneSetup)` in the menu component. Do not delete their legacy code in A2.

- [x] **Step 7: Add deterministic settings asset creation**

The editor utility creates `Assets/Resources/GameRuntimeSettings.asset` only when absent using `ScriptableObject.CreateInstance<GameRuntimeSettings>()` and `AssetDatabase.CreateAsset`. Expose both a `MenuItem("Game/Create Default Runtime Settings")` and public static `CreateDefaultAsset` for batch `-executeMethod`.

Run Unity import once, invoke the execute method, and confirm the asset serializes Offline, `BattleScene`, `ws://localhost:8080/ws`, positive timeout values, non-negative retries, and main-thread budget `64`.

- [x] **Step 8: Run PlayMode startup test and verify GREEN**

Expected: application reaches Ready, one service root exists, all five services exist, and no network/login/bootstrap object exists.

Add a second PlayMode test that invokes `GameApplication.Shutdown` by reflection, waits one frame, and asserts the application, service root, and five service objects are gone. In `finally`, invoke the internal static `RuntimeBootstrap.EnsureApplication` by reflection and wait for Ready so the test does not pollute later tests.

- [x] **Step 9: Run all EditMode tests, compile, resource validation, and commit**

```powershell
git commit -m "feat: 建立统一应用启动与服务根"
```

---

### Task 4: Battle Scene Reload Stability and A2 Delivery

**Files:**
- Modify: `Assets/Scripts/Game/BattleSceneSetup.cs:46-67`
- Modify: `Assets/Tests/PlayMode/BattleSceneOfflineSmokeTests.cs`
- Modify: `Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs`
- Modify: `.claude/memory/project-overview.md`
- Modify: `CLAUDE.md`
- Modify: `docs/superpowers/plans/2026-07-18-phase-a2-application-lifecycle.md`

**Interfaces:**
- Consumes: automatic application and service root from Task 3.
- Produces: verified scene reload behavior and final A2 evidence.

- [x] **Step 1: Extend the PlayMode test to verify scene reload stability**

Capture instance IDs of `[GameApplication]`, `[GameServices]`, and all five services. Capture the current `Player` instance ID, reload `BattleScene` with `SceneManager.LoadSceneAsync`, wait two frames and for application Ready, then assert:

```csharp
Assert.That(GameObject.Find("[GameApplication]").GetInstanceID(), Is.EqualTo(applicationId));
Assert.That(GameObject.Find("[GameServices]").GetInstanceID(), Is.EqualTo(servicesId));
Assert.That(GameObject.Find("Player").GetInstanceID(), Is.Not.EqualTo(playerId));
Assert.That(FindAll("[AudioManager]").Count, Is.EqualTo(1));
Assert.That(GameObject.Find("[NetworkClient]"), Is.Null);
```

Repeat uniqueness assertions for every A2 service.

- [x] **Step 2: Run the reload integration test and record the actual result**

Expected after Task 3: PASS. If it fails, preserve the XML and log, trace the duplicate or stale owner, and apply the smallest lifecycle fix before continuing. Do not change the assertion to accept duplicate services.

- [x] **Step 3: Remove cross-scene prewarming from `BattleSceneSetup`**

Keep `AiSpriteLoader.PreloadAllSprites`, gameplay pools, and `SummonManager`. Remove the standalone `AchievementManager.Instance` prewarm. Continue calling preinstalled `AudioManager.Instance.PlayBGM` and `LoadingScreen.Instance.Hide`; if either is unexpectedly null, throw an initialization error naming the missing service instead of creating it.

- [x] **Step 4: Preserve and strengthen the original offline smoke test**

Keep assertions for Ground, Player, WaveSpawner, HUD, and absence of network/login/bootstrap. Preserve `LogAssert.ignoreFailingMessages` state and capture `Error`, `Exception`, and `Assert` logs. Add application Ready and active scene assertions so the original A1 contract remains covered by the A2 entry.

- [x] **Step 5: Run complete EditMode and PlayMode suites**

Run separate fresh Unity processes and XML files:

```powershell
Unity.exe -batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testResults Logs/A2-final-editmode.xml -logFile Logs/A2-final-editmode.log
Unity.exe -batchmode -nographics -projectPath <root> -runTests -testPlatform PlayMode -testResults Logs/A2-final-playmode.xml -logFile Logs/A2-final-playmode.log
```

Expected: every XML root reports `result="Passed"` and `failed="0"`; logs contain no `error CS`, `Scripts have compiler errors`, `NullReferenceException`, ProjectSettings parse failure, or batchmode abort.

- [x] **Step 6: Run non-Unity validation and a fresh compile**

Run Pester and assert exactly 5 passed, run `Test-UnityAssetIntegrity.ps1`, run `git diff --check`, then a fresh Unity `-quit` compile with a success marker.

- [x] **Step 7: Update project documentation with verified facts only**

Record the automatic Offline entry, five owned services, EditMode/PlayMode counts, and remaining A3 network work. Do not claim manual visual play unless it was actually performed.

Verified 2026-07-18: reload integration `1/1`, complete EditMode `54/54`, complete PlayMode `10/10`, Pester `5/5`, asset integrity and fresh Unity compile passed. No manual visual play was performed; Phase A3 network lifecycle work remains open.

- [x] **Step 8: Commit A2 integration**

```powershell
git commit -m "test: 验证应用生命周期与场景重载"
```

- [x] **Step 9: Review, integrate, and push**

Perform an inline spec/code review because subagents require explicit user authorization in this environment. Use `verification-before-completion`, then `finishing-a-development-branch`. After the chosen integration path, verify `git rev-parse HEAD`, `git rev-parse origin/master`, and `git ls-remote origin refs/heads/master` are identical before reporting completion.
