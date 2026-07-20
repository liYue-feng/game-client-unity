# Phase A4 Online Session And Main Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Online mode connect to the real Go development server, authenticate, load/save archive data, recover after reconnect, enter a real MenuScene, and preserve the existing Offline battle path.

**Architecture:** A new `Game.Online` assembly owns login/archive protocol services and an `OnlineSessionCoordinator`; it consumes the A3 network host through a narrow adapter and never owns heartbeat, reconnect, or transport lifetime. `GameServices` installs an `OnlineSessionHost` only in Online mode, `GameApplication` waits for the host before loading `MenuScene`, and an opt-in PlayMode test proves the complete flow against the real `game-server-go` process.

**Tech Stack:** Unity 2022.3.47f1, C#, Unity Test Framework 1.1.33, NUnit, WebSocketSharp, PowerShell 5.1, Go backend at `E:/Own_project/game-server-go`.

## Global Constraints

- Preserve the A3 protocol: 4-byte little-endian total length, 2-byte little-endian message ID, JSON body.
- `NetworkConnectionController` remains the single owner of transport, heartbeat, timeout, and reconnect.
- Default `Assets/Resources/GameRuntimeSettings.asset` remains `RuntimeMode.Offline`.
- Offline startup creates no `OnlineSessionHost`, `LoginManager`, or `ArchiveManager` and still enters `BattleScene`.
- Online reaches `Ready` only after connection, login, and archive load, then loads `MenuScene`.
- The Editor credential is `dev:<identity>` and contains no production token or secret.
- No company XLua, AssetBundle, SDK, private code, resource, or configuration is copied.
- Every new production behavior follows RED-GREEN-REFACTOR. Each task ends in one local commit; after task review the parent agent pushes the feature branch.
- Unity commands use `D:/Unity_Soft/2022/Editor/Unity.exe`; test commands that need XML omit `-quit` in this environment.

## File Structure

- `Assets/Scripts/Core/GameRuntimeSettings.cs`: mode-specific scene and Editor login settings.
- `Assets/Scripts/Network/NetworkConnectionControllerHost.cs`: authentication-state forwarding only.
- `Assets/Scripts/Online/`: online protocol services, connection adapter, coordinator, and Unity host.
- `Assets/Scripts/Application/GameServices.cs`: conditional Online service ownership.
- `Assets/Scripts/Application/GameApplication.cs`: Offline/Online startup gate.
- `Assets/Scripts/Game/MenuSceneSetup.cs`, `Assets/Scripts/UI/Menu/MainMenuUI.cs`: scene-local menu presentation only.
- `Assets/Editor/MenuSceneAssetBuilder.cs`: deterministic MenuScene and Build Settings creation.
- `Assets/Tests/EditMode/Online/`: pure service/coordinator tests and fakes.
- `Assets/Tests/PlayMode/OnlineStartupAndMenuTests.cs`: application and scene lifecycle tests.
- `Assets/Tests/PlayMode/RealBackendOnlineFlowTests.cs`: opt-in real Go WebSocket integration.
- `tools/integration/Invoke-A4BackendIntegration.ps1`: starts/stops the exact Go server process and runs the focused Unity test.

---

### Task 1: Add Mode-Specific Runtime Settings And Authentication State Commands

**Files:**
- Modify: `Assets/Tests/EditMode/GameRuntimeSettingsTests.cs`
- Modify: `Assets/Scripts/Core/GameRuntimeSettings.cs`
- Modify: `Assets/Resources/GameRuntimeSettings.asset`
- Modify: `Assets/Scripts/Network/NetworkConnectionControllerHost.cs`
- Modify: `Assets/Tests/EditMode/Network/NetworkConnectionControllerHostTests.cs`
- Modify: `Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs`

**Interfaces:**
- Produces: `OfflineStartupSceneName`, `OnlineStartupSceneName`, mode-selected `StartupSceneName`, `EditorLoginIdentity`, `OnlineSessionTimeoutSeconds`.
- Produces: `NetworkConnectionControllerHost.BeginAuthentication()` and `MarkReady()` delegating to A3 controller methods.

- [x] **Step 1: Write failing settings and host tests**

Add tests that create settings with Offline scene `BattleScene`, Online scene `MenuScene`, identity `editor-001`, and timeout 20 seconds. Assert Offline selects BattleScene, Online selects MenuScene, identity is exposed without `dev:` prefix, timeout must be finite and greater than zero, and both scenes must pass the build-scene callback. Add a host test that opens a fake transport, pumps the dispatcher, calls `BeginAuthentication()` then `MarkReady()`, and observes `Connected -> Authenticating -> Ready`.

- [x] **Step 2: Verify RED**

Run:

~~~powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -nographics -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testFilter 'Game.Tests.EditMode.GameRuntimeSettingsTests' -testResults 'Logs\A4-task1-red.xml' -logFile 'Logs\A4-task1-red.log'
~~~

Expected: FAIL because mode-specific fields/properties are absent. Run the host filter separately and expect missing command methods.

- [x] **Step 3: Implement the settings contract**

Use `FormerlySerializedAs("_startupSceneName")` on `_offlineStartupSceneName` so the existing asset retains `BattleScene`. Add:

~~~csharp
[SerializeField] private string _onlineStartupSceneName = "MenuScene";
[SerializeField] private string _editorLoginIdentity = "editor-001";
[SerializeField] private float _onlineSessionTimeoutSeconds = 20f;

public string OfflineStartupSceneName => _offlineStartupSceneName;
public string OnlineStartupSceneName => _onlineStartupSceneName;
public string StartupSceneName => _runtimeMode == RuntimeMode.Online
    ? _onlineStartupSceneName
    : _offlineStartupSceneName;
public string EditorLoginIdentity => _editorLoginIdentity;
public float OnlineSessionTimeoutSeconds => _onlineSessionTimeoutSeconds;
~~~

Validation checks the mode-selected `StartupSceneName`; this keeps each intermediate Offline commit runnable before Task 6 creates MenuScene. Online settings therefore validate MenuScene, while Offline settings validate BattleScene. Reject blank Editor identity only in Online mode, and reject non-positive/NaN/infinite timeout. Keep ServerUrl validation for both modes. Update the asset with Online scene `MenuScene`, identity `editor-001`, timeout `20`, and `_runtimeMode: 0`.

Remove `OnlineMode_FailsClosedBeforeCreatingServices` from the PlayMode baseline because its exact Phase A3 error stage is invalid once Online uses a separate scene setting. Do not remove the production `NotSupportedException`; Task 7's real integration test will reproduce it before the replacement implementation.

Add host methods that only delegate when not shutdown:

~~~csharp
public void BeginAuthentication() => _controller?.BeginAuthentication();
public void MarkReady() => _controller?.MarkReady();
~~~

- [x] **Step 4: Verify GREEN and commit**

Run both focused test filters; expect all pass. Run `tools/validation/Test-UnityAssetIntegrity.ps1`; at this task it may report missing `MenuScene` only if validation inspects the new serialized field, otherwise it must pass.

Commit:

~~~powershell
git add Assets/Scripts/Core/GameRuntimeSettings.cs Assets/Scripts/Network/NetworkConnectionControllerHost.cs Assets/Resources/GameRuntimeSettings.asset Assets/Tests/EditMode/GameRuntimeSettingsTests.cs Assets/Tests/EditMode/Network/NetworkConnectionControllerHostTests.cs Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs
git commit -m "feat: define online runtime settings"
~~~

### Task 2: Add Login And Archive Protocol Services In Game.Online

**Files:**
- Create: `Assets/Scripts/Online/Game.Online.asmdef` and `.meta`
- Create: `Assets/Scripts/Online/ILoginCodeProvider.cs` and `.meta`
- Create: `Assets/Scripts/Online/EditorLoginCodeProvider.cs` and `.meta`
- Create: `Assets/Scripts/Online/LoginSessionService.cs` and `.meta`
- Create: `Assets/Scripts/Online/ArchiveSessionService.cs` and `.meta`
- Create: `Assets/Tests/EditMode/Online/LoginAndArchiveSessionServiceTests.cs` and `.meta`
- Modify: `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef`

**Interfaces:**

~~~csharp
public interface ILoginCodeProvider
{
    void RequestCode(Action<string> succeeded, Action<string> failed);
}
public sealed class EditorLoginCodeProvider : ILoginCodeProvider
public sealed class LoginSessionService : IDisposable
{
    public event Action<LoginResp> Succeeded;
    public event Action<string> Failed;
    public bool Begin(string code);
}
public sealed class ArchiveSessionService : IDisposable
{
    public string CurrentData { get; }
    public event Action<string> Loaded;
    public event Action Saved;
    public event Action<string> Failed;
    public bool Load();
    public bool Save(string data);
}
~~~

- [x] **Step 1: Write failing service tests**

Reference `Game.Online` from the EditMode test assembly. Test that the Editor provider returns exactly `dev:editor-001`; blank identity fails. Instantiate a registered `NetworkClient` with `FakeWebSocketTransport`, create both services, and assert:

- disconnected Begin/Load/Save return false and emit a stable error;
- connected Begin sends `LoginReq` with exact code;
- `LoginResp` stores UID/token in the client and emits once;
- load emits exact JSON and updates CurrentData;
- save emits only after `SaveArchiveResp.success == true`;
- Error message 9999 routes to the active operation failure;
- Dispose makes later frames inert.

- [x] **Step 2: Verify RED**

Run the focused EditMode filter. Expected: compile failure because `Game.Online` and the services do not exist.

- [x] **Step 3: Implement minimal services**

`Game.Online.asmdef` references `Game.Core` and `Game.Network`. Provider prepends `dev:` exactly once. Each service owns its `NetworkClient.On<T>` tokens in a list and disposes them. `LoginSessionService.Begin` refuses blank code or a disconnected client, marks one request active, sends MsgID.LoginReq, and on response calls `SetLoginInfo` before `Succeeded`. `ArchiveSessionService` similarly serializes one active load/save at a time. Both subscribe to `MsgID.Error` as `ErrorResp` and format `[{code}] {msg}`.

- [x] **Step 4: Verify GREEN and commit**

Run the focused filter, then full EditMode. Expected: service tests and existing tests pass with no unexpected logs.

Commit all new Online service files, metas, test, and asmdef change as `feat: add online login and archive services`.

### Task 3: Add The Generation-Safe Online Session Coordinator

**Files:**
- Create: `Assets/Scripts/Online/OnlineSessionState.cs` and `.meta`
- Create: `Assets/Scripts/Online/IOnlineConnection.cs` and `.meta`
- Create: `Assets/Scripts/Online/OnlineConnectionAdapter.cs` and `.meta`
- Create: `Assets/Scripts/Online/OnlineSessionCoordinator.cs` and `.meta`
- Create: `Assets/Tests/EditMode/Online/TestDoubles/FakeLoginCodeProvider.cs` and `.meta`
- Create: `Assets/Tests/EditMode/Online/TestDoubles/FakeOnlineConnection.cs` and `.meta`
- Create: `Assets/Tests/EditMode/Online/OnlineSessionCoordinatorTests.cs` and `.meta`

**Interfaces:**

~~~csharp
public enum OnlineSessionState { Idle, Connecting, Authenticating, LoadingArchive, Ready, Reconnecting, Failed, Stopped }
public interface IOnlineConnection
{
    NetworkConnectionState State { get; }
    event Action Connected;
    event Action Disconnected;
    event Action<string> Error;
    void Connect(string url);
    void BeginAuthentication();
    void MarkReady();
    void Disconnect();
}
public sealed class OnlineSessionCoordinator : IDisposable
{
    public OnlineSessionState State { get; }
    public string FailureReason { get; }
    public string Nickname { get; }
    public string ArchiveData { get; }
    public event Action<OnlineSessionState> StateChanged;
    public event Action ArchiveSaved;
    public void Start();
    public void Retry();
    public bool SaveArchive(string data);
    public bool ReloadArchive();
    public void Stop();
}
~~~

- [x] **Step 1: Write coordinator RED tests**

Cover: normal `Start -> Connecting -> Authenticating -> LoadingArchive -> Ready`; transport reconnect causes `Reconnecting -> Authenticating -> LoadingArchive -> Ready`; provider failure and server error become Failed; Retry increments generation and reconnects once; a delayed provider callback from an old generation is ignored; Stop disconnects, disposes subscriptions, becomes Stopped, and later callbacks do nothing.

- [x] **Step 2: Verify RED**

Run `Game.Tests.EditMode.Online.OnlineSessionCoordinatorTests`. Expected: compile failure for missing coordinator types.

- [x] **Step 3: Implement the state machine**

`OnlineConnectionAdapter` forwards `NetworkClient.OnConnected/OnDisconnected/OnError` and delegates commands to `NetworkConnectionControllerHost`; it unsubscribes delegates in Dispose. Coordinator stores `int _generation`; every asynchronous provider callback closes over the current generation and returns when stale or stopped. Connected calls host BeginAuthentication, requests code, then begins login. Login success starts archive load. Archive load calls host MarkReady and sets Ready. Disconnected after first connection sets Reconnecting and waits for A3 to reconnect; it does not call Connect itself. Connection Error, login error, archive error, or invalid command result sets FailureReason and Failed. Retry clears login info, increments generation, and calls Connect exactly once.

`SaveArchive` and `ReloadArchive` are accepted only in Ready. They delegate to `ArchiveSessionService`, keep session state Ready, update ArchiveData on reload, and forward the service `Saved` event as `ArchiveSaved`.

- [x] **Step 4: Verify GREEN and commit**

Run focused then full EditMode. Commit coordinator, adapter, fakes, tests, and metas as `feat: orchestrate online session recovery`.

### Task 4: Install OnlineSessionHost Under GameServices

**Files:**
- Create: `Assets/Scripts/Online/OnlineSessionHost.cs` and `.meta`
- Create: `Assets/Tests/EditMode/Online/OnlineSessionHostTests.cs` and `.meta`
- Modify: `Assets/Scripts/Application/GameServices.cs`
- Modify: `Assets/Scripts/Application/GameApplication.cs` static reset path
- Modify: `Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs`
- Modify: `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef`

**Interfaces:**

~~~csharp
public sealed class OnlineSessionHost : MonoBehaviour, IGameService
{
    public static OnlineSessionHost Instance { get; }
    public OnlineSessionState State { get; }
    public string FailureReason { get; }
    public string Nickname { get; }
    public string ArchiveData { get; }
    public event Action<OnlineSessionState> StateChanged;
    public event Action ArchiveSaved;
    public void StartSession();
    public void Retry();
    public bool SaveArchive(string data);
    public bool ReloadArchive();
}
~~~

- [x] **Step 1: Write host/service-ownership RED tests**

Host EditMode tests assert Install parents `[OnlineSessionHost]`, Initialize wires one coordinator, Shutdown is idempotent, clears Instance, disconnects, and destroys callback effects. Update PlayMode offline assertions to require no `[OnlineSessionHost]`. Add an Online service-construction test through reflection that changes the settings mode, calls `GameServices.Create`, and asserts one host under `[GameServices]`, then Shutdown leaves none; inject fake connection dependencies through an internal Install overload rather than opening a real socket.

- [x] **Step 2: Verify RED**

Run focused host tests and offline PlayMode filter. Expected: missing host or ownership assertions fail.

- [x] **Step 3: Implement conditional ownership**

Host Install creates the provider from `settings.EditorLoginIdentity`, services from the registered client, adapter from client/network host, and coordinator from settings ServerUrl. `Initialize` does not start a connection; `GameApplication` owns the start decision. `Shutdown` disposes coordinator, adapter, login/archive services in that order and clears Instance.

Extend the composition seam as:

~~~csharp
internal static GameServices Create(
    Transform applicationRoot,
    GameRuntimeSettings settings,
    IWebSocketTransportFactory transportFactory = null,
    ILoginCodeProvider loginCodeProvider = null)
~~~

Production uses `transportFactory ?? new WebSocketTransportFactory()` and lets OnlineSessionHost create its default Editor provider when `loginCodeProvider` is null. Tests pass fakes through reflection; no global test hook is added.

In `GameServices.Create`, add OnlineSessionHost immediately after NetworkConnectionControllerHost only when `settings.RuntimeMode == RuntimeMode.Online`; add it to the lifecycle list after the network host. Expose `internal OnlineSessionHost OnlineSession { get; private set; }`. Clear the static in both service and application reset paths. Offline still installs the A3 network host but never connects and never installs online services.

- [x] **Step 4: Verify GREEN and commit**

Run focused EditMode, full EditMode, and offline PlayMode. Commit as `feat: own online session under game services`.

### Task 5: Add A Testable Online Startup Decision

**Files:**
- Create: `Assets/Tests/EditMode/Online/OnlineStartupDecisionTests.cs` and `.meta`
- Create: `Assets/Scripts/Online/OnlineStartupDecision.cs` and `.meta`

**Interfaces:**

~~~csharp
public enum OnlineStartupResult { Waiting, Ready, Failed, TimedOut }
public sealed class OnlineStartupDecision
{
    public OnlineStartupResult Evaluate(OnlineSessionState state, float elapsedSeconds, float timeoutSeconds);
}
~~~

- [x] **Step 1: Write startup decision RED tests**

Pure tests require Idle/Connecting/Authenticating/LoadingArchive/Reconnecting -> Waiting, Ready -> Ready, Failed -> Failed, and elapsed >= timeout -> TimedOut. Do not modify GameApplication; Task 7 needs its old Phase A3 fail-closed production behavior for the real integration RED run.

- [x] **Step 2: Verify RED**

Run the focused EditMode test. Expected: missing decision type.

- [x] **Step 3: Implement only the pure decision**

Implement `Evaluate` with this order so a terminal state wins before timeout:

~~~csharp
if (state == OnlineSessionState.Ready) return OnlineStartupResult.Ready;
if (state == OnlineSessionState.Failed || state == OnlineSessionState.Stopped) return OnlineStartupResult.Failed;
if (elapsedSeconds >= timeoutSeconds) return OnlineStartupResult.TimedOut;
return OnlineStartupResult.Waiting;
~~~

- [x] **Step 4: Verify GREEN and commit**

Run startup decision, offline full PlayMode, and full EditMode. Commit as `feat: add online startup decision`.

### Task 6: Build MenuScene And Scene-Local Main Menu UI

**Files:**
- Modify: `Assets/Scripts/Game/MenuSceneSetup.cs`
- Modify: `Assets/Scripts/UI/Menu/MainMenuUI.cs`
- Modify: `Assets/Scripts/UI/Menu/LoginUI.cs`
- Modify: `Assets/Scripts/Managers/SceneTransitionManager.cs`
- Create: `Assets/Editor/MenuSceneAssetBuilder.cs` and `.meta`
- Create: `Assets/Scenes/MenuScene.unity` and `.meta` through the builder
- Modify: `ProjectSettings/EditorBuildSettings.asset` through the builder
- Create: `Assets/Tests/PlayMode/OnlineStartupAndMenuTests.cs` and `.meta`

**Interfaces:**
- `MenuSceneSetup` owns only scene presentation.
- `MainMenuUI` reads `OnlineSessionHost.Instance`, unbinds StateChanged in OnDestroy, and starts `BattleScene` directly.
- `SceneTransitionManager.GoToMainMenu()` loads `MenuScene`.

- [x] **Step 1: Write failing MenuScene tests**

PlayMode test loads `MenuScene`, expects `[MenuScene]`, `MenuCanvas`, `BtnStart`, `BtnSettings`, and no LoginManager/ArchiveManager/GameBootstrap components. Invoke BtnStart and wait for `BattleScene`. Then call `GoToMainMenu`, wait for `MenuScene`, and assert only one MenuCanvas. Add an EditMode asset assertion that both MenuScene and BattleScene are enabled in Build Settings.

- [x] **Step 2: Verify RED**

Run asset integrity and focused PlayMode. Expected: MenuScene missing from disk/build and old MainMenu start references LobbyScene.

- [x] **Step 3: Implement deterministic scene and UI**

`MenuSceneSetup.Start` creates a single `[MenuScene]` presentation root and adds MainMenuUI only; remove all manager/network creation. LoginUI becomes a compatibility component that delegates Retry to OnlineSessionHost and never creates managers. MainMenu Start loads `BattleScene`; status/player labels read host state and nickname; failure displays a Retry command. OnDestroy removes StateChanged and button listeners.

`MenuSceneAssetBuilder.Build` creates an empty scene, adds one `[MenuScene]` GameObject with MenuSceneSetup, saves `Assets/Scenes/MenuScene.unity`, and writes EditorBuildSettings scenes in order MenuScene then BattleScene without duplicates. Run it:

~~~powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -quit -projectPath (Get-Location).Path -executeMethod MenuSceneAssetBuilder.Build -logFile 'Logs\A4-menu-scene-build.log'
~~~

- [x] **Step 4: Verify GREEN and commit**

Run asset integrity, focused PlayMode, and full PlayMode. Commit scene, metas, UI/setup/transition code, builder, Build Settings and test as `feat: add online main menu scene`.

### Task 7: Prove The Full Client Against The Real Go WebSocket Server

**Files:**
- Create: `Assets/Tests/PlayMode/RealBackendOnlineFlowTests.cs` and `.meta`
- Create: `tools/integration/Invoke-A4BackendIntegration.ps1`
- Modify: `Assets/Scripts/Application/GameApplication.cs`
- Modify: `.gitignore` only if the generated backend executable/logs are not already ignored

**Interfaces:**
- Environment gate: `GAME_BACKEND_INTEGRATION=1`.
- Backend URL: `ws://127.0.0.1:8080/ws`.
- Credential identity: `integration-client` -> request `dev:integration-client`.

- [x] **Step 1: Write the opt-in integration test**

When the gate is absent, call `Assert.Ignore` before mutating settings. When present, save every changed private field, set Online mode, MenuScene, URL, identity, 10-second timeout, shut down/recreate GameApplication, and wait up to 600 frames for Ready. Require active MenuScene, Network state Ready, positive UID, nonempty token, and nonempty nickname. Subscribe once to `OnlineSessionHost.ArchiveSaved`, call `SaveArchive("{\"phase\":\"a4\",\"coins\":7}")`, wait for that event, call `ReloadArchive()`, and require exact round-trip ArchiveData. In finally remove the event handler, restore every asset field in memory, shut down the Online application, recreate Offline application, and wait for BattleScene Ready.

- [x] **Step 2: Verify the test is safely skipped**

Run the focused PlayMode filter without the environment variable. Expected: one skipped test, no connection attempt, and Offline application restored.

- [x] **Step 3: Implement the PowerShell runner**

The script resolves both repo roots, verifies port 8080 is unused, creates backend `logs`, runs `go test ./...`, builds `logs/a4-integration-server.exe`, starts that exact executable hidden with `-config configs/config.dev.yaml`, polls `http://127.0.0.1:8080/health`, sets `GAME_BACKEND_INTEGRATION=1`, runs Unity PlayMode with the focused test and absolute XML/log paths, parses XML for zero failures, and in `finally` stops only the captured process ID and clears the environment variable. It must fail when health, Unity XML creation, or test count fails.

- [x] **Step 4: Run the real application RED**

After the reviewed backend branch is pushed/merged, run:

~~~powershell
& .\tools\integration\Invoke-A4BackendIntegration.ps1 -BackendRoot 'E:\Own_project\game-server-go'
~~~

Expected: FAIL because GameApplication still throws `Online runtime flow is not implemented in Phase A3`. This proves the full-application test reaches the old behavior through a real server.

- [x] **Step 5: Implement the Online coroutine gate**

Delete the Online `NotSupportedException`. After services initialize, `Start` branches:

~~~csharp
if (_settings.RuntimeMode == RuntimeMode.Online)
{
    FailureStage = "OnlineSession.Start";
    _services.OnlineSession.StartSession();
    var elapsed = 0f;
    var decision = new OnlineStartupDecision();
    while (true)
    {
        var result = decision.Evaluate(_services.OnlineSession.State, elapsed, _settings.OnlineSessionTimeoutSeconds);
        if (result == OnlineStartupResult.Ready) break;
        if (result == OnlineStartupResult.Failed) { FailInitialization(new InvalidOperationException(_services.OnlineSession.FailureReason)); yield break; }
        if (result == OnlineStartupResult.TimedOut) { FailInitialization(new TimeoutException("Online session startup timed out.")); yield break; }
        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }
}
~~~

Then load mode-selected `StartupSceneName` with the existing guarded scene code and mark application Ready. Offline bypasses the gate exactly as before.

- [x] **Step 6: Run real GREEN and normal regression**

Run the runner again. Expected: backend Go suite passes; Unity XML reports one passed, zero failed; server log contains login and archive save/load; no process remains listening on 8080. Then run normal full PlayMode without the integration environment; expect zero failures and the integration case skipped.

- [x] **Step 7: Commit and push task**

Commit GameApplication, real integration test/meta, runner, and scoped ignore change as `feat: enable real online application flow`. After task review, parent pushes and verifies remote SHA.

### Task 8: Final Regression, Review, And Delivery Evidence

**Files:**
- Modify: `CLAUDE.md`
- Modify: `docs/superpowers/plans/2026-07-18-phase-a4-online-session-main-menu.md` checkboxes/evidence

- [x] **Step 1: Run full repository gates**

Run asset integrity and Pester. Run full EditMode and PlayMode to fresh XML files. Run `git diff --check master...HEAD`. Expected: asset pass, Pester 5/5, all EditMode/PlayMode tests pass with only the opt-in backend test skipped in the normal suite, and no whitespace errors.

- [x] **Step 2: Re-run real integration**

Run `Invoke-A4BackendIntegration.ps1` again after the final code commit. Expected: one real integration test passes and port 8080 is free afterward.

- [x] **Step 3: Update delivery evidence**

Record exact test totals, XML/log paths, backend commit SHA, client commit SHA, and the real integration result in this plan. Update CLAUDE.md: A3 completed, A4 Online route, MenuScene, dev backend command, Offline default, and remaining A5/Phase B/C work.

- [x] **Step 4: Commit documentation**

The initial evidence was committed as `docs: record phase a4 verification evidence`. After final review remediation, refresh it as `docs: refresh phase a4 final evidence`; push only after the parent delivery gate confirms the reviewed branch.

### Task 8 Final Delivery Evidence (2026-07-20 17:00-17:05 +08:00)

- Reviewed delivery code head before the final evidence update: client `1b9b7d8897db95146ecf9d990dc3bee661b4c096`; backend local and `origin/master` `88cae827262c4648e35dd74496e5a368eb9c1030`. This documentation commit intentionally does not predict its own SHA, and the parent still owns the final client push.
- Final remediation delivered scene-local Rank/Quit menu commands, one-shot terminal return to `MenuScene`, fail-closed scene navigation, configured-size `InkPanel` rebuilds, component-owned texture cleanup that preserves external textures, and recoverable Online Retry through a fresh A3 transport generation. Whole-branch specification and quality reviews were both APPROVED at client head `1b9b7d8`.
- Asset integrity wrapper: `tools/validation/Test-UnityAssetIntegrity.ps1` passed.
- Pester asset contract: `tools/validation/UnityAssetIntegrity.Tests.ps1` passed `5/5`, failed `0`, skipped `0`.
- Full EditMode: `Logs/A4-retry-recovery-full-editmode.xml`, total `210`, passed `210`, failed `0`, skipped `0`, duration `0.5025737` seconds; Unity exit code `0`; `Logs/A4-retry-recovery-full-editmode.log` error markers `0`.
- Normal full PlayMode with `GAME_BACKEND_INTEGRATION` absent: `Logs/A4-retry-recovery-full-playmode.xml`, total `99`, passed `98`, failed `0`, skipped `1`, duration `68.4876785` seconds; Unity exit code `0`; `Logs/A4-retry-recovery-full-playmode.log` error markers `0`. The only skipped test was `Game.Tests.PlayMode.RealBackendOnlineFlowTests.OnlineApplication_LoginSaveAndReloadArchiveAgainstRealBackend`.
- Real integration command: `& .\tools\integration\Invoke-A4BackendIntegration.ps1 -BackendRoot 'E:\Own_project\game-server-go'`. Go `go test ./...` passed; `Logs/A4-real-backend-20260720-170438.xml` reported total `1`, passed `1`, failed `0`, skipped `0`, duration `1.6402187` seconds, with Unity exit code `0`; `Logs/A4-real-backend-20260720-170438.log` error markers `0`.
- Real server evidence: `E:/Own_project/game-server-go/logs/a4-integration-server-20260720-170438.stdout.log` recorded one `dev:integration-client` login, initial archive load `dataLen=0`, save `dataLen=24`, and reload `dataLen=24`; stderr was empty and the Unity assertion used exact archive JSON `{"phase":"a4","coins":7}`.
- Cleanup evidence: captured backend PID `40268` and Unity PID `41320` exited; remaining relevant processes `0`; listeners on ports `8080` and `8081` were both `0`; `GAME_BACKEND_INTEGRATION` was empty after the runner.
- Hygiene: `git diff --check master...HEAD` passed at reviewed code head `1b9b7d8`; both the working documentation diff and final committed range are checked again before handoff.

## Final Acceptance Checklist

- [x] Normal Unity suite has zero failures; real integration test is skipped only when its environment gate is absent.
- [x] Real Go process plus real Unity WebSocket proves login, archive save, archive load, and full Online application startup to MenuScene.
- [x] Offline default still reaches BattleScene and creates no online host or legacy login/archive managers.
- [x] MenuScene and BattleScene are valid enabled build scenes; no code references LobbyScene as the main start path.
- [x] Reconnect re-authenticates through one generation-safe coordinator without duplicate subscriptions.
- [ ] Client and backend branches have clean task reviews, final reviews, pushed remote SHAs, and no process left on ports 8080/8081.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-18-phase-a4-online-session-main-menu.md`. The user already selected Subagent-Driven execution and requested no confirmation, so execution proceeds continuously after plan review.
