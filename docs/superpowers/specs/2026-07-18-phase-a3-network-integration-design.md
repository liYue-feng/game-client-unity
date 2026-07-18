# Phase A3 Network Integration Design

## Context

Phase A2 made the project boot reliably through `GameApplication` and `GameServices`. The next missing capability is a production-shaped network layer. The current `Assets/Scripts/Network/NetworkClient.cs` is useful as a prototype, but it owns too many responsibilities in one `MonoBehaviour`: WebSocket lifetime, codec routing, subscriptions, heartbeat, reconnect, login state, and static object creation.

The company project at `E:\client\zhetian_client\Unity\Assets\Scripts\Framework\XCoreNet` is a read-only reference. A3 should borrow the separation of raw connection lifetime from message/business ownership, but should not copy its static global listener lists, TCP coupling, or project-specific event bus patterns.

## Goals

- Make WebSocket transport a replaceable low-level adapter.
- Move codec, message routing, and request sending into a plain `NetworkClient` service.
- Move connection state, heartbeat, timeouts, and reconnect policy into one owner: `NetworkConnectionController`.
- Ensure every socket callback crosses `MainThreadDispatcher` before invoking state or business callbacks.
- Use generation/version checks so stale callbacks from older sockets cannot mutate current state.
- Return disposable subscriptions from message registration and update business managers to unsubscribe on destroy.
- Keep `NetworkClient.Instance` and existing `Send` calls as a temporary compatibility facade, but stop creating implicit GameObjects from the getter.
- Keep Online mode fail-closed until the next MainMenu/login/archive/real-backend phase wires the user-facing flow.

## Non-Goals

- Do not implement WeChat SDK login, real account UI, archive UI, or MainMenu flow in A3.
- Do not change protocol message ids or server wire format.
- Do not introduce a new third-party networking dependency.
- Do not replace the existing `MainThreadDispatcher`.
- Do not port company-project static connection registries or TCP abstractions directly.

## Selected Design

### Assembly Boundary

Create `Assets/Scripts/Network/Game.Network.asmdef` with assembly name `Game.Network`.

Keep protocol source files physically under `Assets/Scripts/Protocol` to preserve metas and history, but add `Assets/Scripts/Protocol/Game.Network.asmref` so protocol classes compile into the same assembly as the network layer. `Game.Core.asmdef` continues to own core application services. `Game.Network.asmdef` references `Game.Core` so the controller can depend on `MainThreadDispatcher` and `GameRuntimeSettings`.

### File Responsibilities

- `Assets/Scripts/Network/IWebSocketTransport.cs`: byte transport contract and event surface.
- `Assets/Scripts/Network/IWebSocketTransportFactory.cs`: creates transports from URLs.
- `Assets/Scripts/Network/WebSocketTransport.cs`: `WebSocketSharp` adapter only. It connects, sends bytes, closes, disposes, and reports raw events.
- `Assets/Scripts/Network/NetworkClient.cs`: plain service that encodes outgoing protocol frames, decodes incoming frames, dispatches handlers, stores login session data, and exposes a compatibility facade.
- `Assets/Scripts/Network/NetworkConnectionController.cs`: connection state machine, heartbeat, timeout, and reconnect/backoff owner.
- `Assets/Scripts/Network/NetworkConnectionControllerHost.cs`: Unity `MonoBehaviour` and `IGameService` wrapper that ticks the controller from `Update` and owns runtime shutdown.
- `Assets/Scripts/Network/NetworkStatusAdapter.cs`: temporary compatibility status surface consumed by `HeartbeatManager`, `ReconnectionManager`, and `NetworkStatusUI`.
- `Assets/Scripts/Network/HeartbeatManager.cs`: retired compatibility wrapper with obsolete markers and no ownership of heartbeat timing.
- `Assets/Scripts/Network/ReconnectionManager.cs`: retired compatibility wrapper with obsolete markers and no ownership of reconnect timing.
- `Assets/Tests/EditMode/Network/*.cs`: fake transport and focused tests for routing, subscriptions, threading, and state transitions.

### Transport Contract

`IWebSocketTransport` is deliberately small:

```csharp
public interface IWebSocketTransport : IDisposable
{
    event Action Opened;
    event Action<byte[]> MessageReceived;
    event Action<NetworkCloseInfo> Closed;
    event Action<string> Error;

    bool IsAlive { get; }
    void ConnectAsync();
    void Send(byte[] payload);
    void Close(ushort code, string reason);
}
```

`WebSocketTransport` adapts `WebSocketSharp.WebSocket`. It never decodes protocol messages, never starts coroutines, never invokes business callbacks, and never knows about heartbeat or reconnect.

### Message Client

`NetworkClient` becomes constructible:

```csharp
public sealed class NetworkClient : IDisposable
{
    public static NetworkClient Instance { get; }
    public static void RegisterInstance(NetworkClient client);
    public static void UnregisterInstance(NetworkClient client);
    public static void ResetStaticState();

    public bool IsConnected { get; }
    public bool IsLoggedIn { get; }
    public long UID { get; }
    public string Token { get; }
    public string serverUrl { get; set; }

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;
    public IDisposable On<T>(ushort msgId, Action<T> handler);
    public IDisposable On(ushort msgId, Action<string> handler);
    public bool Send<T>(ushort msgId, T payload);
    public bool Send(ushort msgId, string jsonBody);
    public void Connect(string url = null);
    public void Disconnect();
    public void ReceiveFrame(byte[] frame);
    public void SetTransport(IWebSocketTransport transport);
    public void SetLoginInfo(long uid, string token);
    public void ClearLoginInfo();
}
```

Compatibility behavior:

- `NetworkClient.Instance` returns the explicit instance registered by `GameServices`.
- If no instance has been registered, `Instance` returns a fail-closed facade that logs a warning and drops sends.
- The getter must not create a `GameObject` and must not call `DontDestroyOnLoad`.
- `RegisterInstance` migrates subscriptions created on the fail-closed facade to the explicit instance so managers that wake before `GameServices` do not lose handlers.
- `UnregisterInstance` restores the fail-closed facade only when the unregistering instance is still the active instance.
- `ResetStaticState` disposes the active instance and clears static compatibility state for runtime shutdown and tests.
- `serverUrl`, `Connect`, `Disconnect`, `OnConnected`, `OnDisconnected`, and `OnError` remain as compatibility members for `GameBootstrap`, `HeartbeatManager`, `NetworkStatusUI`, and menu setup code. They forward to `NetworkConnectionControllerHost` when registered and otherwise fail closed with a warning.
- Existing managers can keep using `NetworkClient.Instance` during A3 migration. Later phases should inject the service directly.

Subscriptions:

- `On<T>` and `On` return `IDisposable`.
- Disposing removes exactly the registered callback, including generic wrappers.
- A subscription token created on the fail-closed facade remains valid after `RegisterInstance` migrates it; disposing the original token removes the migrated callback from the explicit instance.
- Dispatch copies the current handler list before invocation so a callback can dispose itself safely.
- Deserialization failures are logged and isolated to the failing handler.

### Connection Controller

`NetworkConnectionController` is installed by `GameServices` and is the only component that owns connection lifecycle.

Runtime shape:

- `NetworkConnectionController` is a plain disposable class with `Connect`, `Disconnect`, and `Tick(float deltaSeconds)`.
- `NetworkConnectionControllerHost` is the Unity-facing `MonoBehaviour`/`IGameService`. Its `Update()` calls `Tick(Time.deltaTime)`.
- Connection timeout, heartbeat cadence, and reconnect delay are stored as remaining seconds inside the controller and advanced by `Tick`; tests call `Tick` directly.
- No connection lifecycle code starts coroutines inside `NetworkClient` or `WebSocketTransport`.
- `Shutdown()` on the host calls `Disconnect()`, then disposes controller-owned transport state before `MainThreadDispatcher` is reset.

State model:

```text
Disconnected -> Connecting -> Connected -> Authenticating -> Ready
Connected/Authenticating/Ready -> Reconnecting
Reconnecting -> Connecting
Reconnecting -> Failed
Any state -> Disconnected on intentional close
```

State ownership rules:

- `Connect()` starts generation `N`, creates a fresh transport, sets `Connecting`, and schedules a timeout.
- `Opened` for generation `N` moves to `Connected`.
- A login/auth flow can move `Connected` to `Authenticating`, then `Ready`. A3 exposes the states and transition methods, but MainMenu/login integration will drive them in the next phase.
- `Closed` or `Error` from generation `N` starts reconnect only when the close was not intentional and retry budget remains.
- `Closed`, `Error`, `Opened`, and `MessageReceived` from stale generations are ignored.
- Any terminal action immediately invalidates the active generation before more queued callbacks can run: intentional `Disconnect`, connection timeout, retry exhaustion, and the first non-intentional `Closed` or `Error`.
- A generation can start reconnect at most once. If `Error` and `Closed` arrive for the same transport, the second callback sees a stale generation and does not schedule another reconnect.
- Retry delay starts at `InitialReconnectBackoffSeconds`, doubles after each failed attempt, and clamps at `MaxReconnectBackoffSeconds`.
- Exhausting `MaxReconnectAttempts` moves to `Failed` and emits the final error.
- `Disconnect()` is intentional and moves to `Disconnected` without scheduling reconnect.

Heartbeat:

- Controller sends `MsgID.HeartbeatReq` via `NetworkClient.Send` every `HeartbeatIntervalSeconds` only in `Connected`, `Authenticating`, or `Ready`.
- Heartbeat timer resets on open and after each heartbeat send.
- Heartbeat is stopped in `Disconnected`, `Reconnecting`, and `Failed`.

Thread boundary:

- Transport may raise events from any thread.
- Controller wraps every transport event in `MainThreadDispatcher.Enqueue`.
- Only the enqueued action may change controller state, dispatch messages, or invoke public events.
- Tests use a fake dispatcher pump to prove callbacks do not run inline from the transport event.
- Tests include queued-callback races: `Disconnect()` followed by an already-enqueued `Opened`, and `Error` followed by `Closed` for the same generation.

### Business Manager Migration

Update these managers to hold subscription disposables and release them in `OnDestroy`:

- `LoginManager`: `LoginResp`
- `ArchiveManager`: `SaveArchiveResp`, `LoadArchiveResp`
- `RankManager`: `GetRankResp`, `SubmitScoreResp`
- `CombatManager`: `CombatResultResp`, `GetEnemyConfigsResp`, `GetDungeonConfigResp`, `GetStyleConfigsResp`, `UnlockStyleResp`, `GetPlayerStatsResp`, `UpdatePlayerStatsResp`

Manager singleton cleanup:

- If a manager destroys the active singleton instance, set `_instance = null`.
- Do not unsubscribe from messages by reconstructing handlers.
- Do not add new implicit network object creation.

Legacy compatibility cleanup:

- `GameBootstrap` keeps compiling through the compatibility members, but it remains disabled whenever `GameApplication.HasInstance` is true.
- `HeartbeatManager` becomes a status adapter around `NetworkClient.OnConnected` and `NetworkClient.OnDisconnected`; `StartHeartbeat` and `StopHeartbeat` remain no-op compatibility methods and do not schedule heartbeat work.
- `ReconnectionManager` remains a no-op compatibility adapter that reports controller state if consumed, but does not own retry loops or coroutines.
- `NetworkStatusUI` and menu setup code keep consuming `HeartbeatManager`/`ReconnectionManager` during A3; no UI behavior is redesigned in this phase.

### Runtime Integration

`GameServices.Create()` creates and owns the network service alongside existing services:

1. Install `MainThreadDispatcher`.
2. Create `NetworkClient`.
3. Register the explicit `NetworkClient` instance for compatibility before other startup-owned managers can subscribe.
4. Create `NetworkConnectionControllerHost` using `GameRuntimeSettings`.
5. Add the controller host to the `GameServiceCollection`.
6. On shutdown, shut down the controller host before resetting `MainThreadDispatcher`, dispose the client, unregister the compatibility instance, and reset network static state.
7. The test assembly at `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef` adds a `Game.Network` reference so existing EditMode tests can cover both core and network assemblies without a second test assembly.

Online mode remains fail-closed in A3. If `RuntimeMode.Online` is selected before MainMenu/login/backend wiring exists, startup emits a clear error and does not start a socket connection. The next phase will replace this with MainMenu/login/backend flow.

## Error Handling

- Send while disconnected returns `false` and logs one concise warning.
- Malformed frames are logged and ignored.
- WebSocket errors include the current state and generation in logs.
- Reconnect exhaustion emits a user-facing error event and enters `Failed`.
- Intentional disconnect does not emit reconnect attempts.

## Testing Strategy

Use EditMode tests for pure networking behavior and lifecycle tests that avoid real sockets:

- `NetworkClient` encodes outbound messages through a fake transport.
- `NetworkClient.ReceiveFrame` decodes frames and invokes typed/raw subscriptions.
- Disposed subscriptions do not receive later messages.
- A handler can dispose itself during dispatch without corrupting iteration.
- `NetworkConnectionController` transitions from `Disconnected` to `Connecting` to `Connected`.
- Connection timeout closes stale transports and starts reconnect when allowed.
- Exponential backoff doubles and clamps.
- Stale generation callbacks are ignored.
- Transport events enqueue work onto the dispatcher before state/business callbacks.
- `GameServices` registers and unregisters the explicit compatibility client.
- Managers that subscribe before registration have their fail-closed facade subscriptions migrated to the explicit client.
- Disposing the original facade-created subscription after migration removes the handler from the explicit client.
- Business managers release subscriptions on destroy.
- Heartbeat sends at `HeartbeatIntervalSeconds` while connected/ready and stops while disconnected/reconnecting/failed.
- Host shutdown disconnects, prevents pending reconnect delays from firing, invalidates queued callbacks, and completes before `MainThreadDispatcher.ResetStaticState()`.
- Disconnect invalidates already-enqueued open/message callbacks for the previous active transport.
- Error followed by close for one generation schedules only one reconnect.
- Online runtime mode logs the A3 fail-closed error and does not connect.

Full verification after implementation:

- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1 -ProjectRoot .`
- `powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"`
- `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testResults Logs\A3-editmode-results.xml -logFile Logs\A3-editmode.log`
- `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode -testResults Logs\A3-playmode-results.xml -logFile Logs\A3-playmode.log`

## Delivery Boundaries

A3 ends when the project has a tested, service-owned network stack with compatibility preserved and no lingering duplicate heartbeat/reconnect owners. The next phase starts from this foundation to build MainMenu, real login/archive calls, and real backend integration.
