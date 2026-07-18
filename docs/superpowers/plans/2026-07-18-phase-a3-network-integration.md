# Phase A3 Network Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the prototype `NetworkClient` MonoBehaviour with a tested, service-owned network stack that preserves temporary compatibility for existing managers and UI.

**Architecture:** `WebSocketTransport` owns only raw bytes. `NetworkClient` owns protocol encode/decode, subscriptions, login data, and a fail-closed compatibility facade; connection methods forward through an injected `INetworkConnectionGateway` without owning that gateway. `NetworkConnectionController` owns connection state, heartbeat, timeout, reconnect, and stale-generation protection, while `NetworkConnectionControllerHost` binds that controller to the facade, ticks it from Unity, and shuts it down through `IGameService`.

**Tech Stack:** Unity 2022, C#, Unity Test Framework EditMode/PlayMode, WebSocketSharp, existing `Game.Core` services, existing `Game.Protocol` codec.

## Global Constraints

- Work only in `E:\Own_project\game-client-unity\.worktrees\phase-a3-network-integration` on branch `feature/phase-a3-network-integration`.
- Implementer subagents commit their task after GREEN verification, then the root coordinator runs task review; only after review is clean does the root coordinator run `git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration`.
- Every task commit must include Unity-generated `.meta` files for newly created assets and directories.
- Use RED-GREEN-REFACTOR for runtime behavior: write a failing EditMode or PlayMode test, run the exact filtered command, implement the minimum behavior, and rerun the exact filtered command.
- Do not copy source from `E:\client\zhetian_client`; use it only as a read-only architectural reference.
- Do not change protocol message ids or wire format.
- Do not introduce a third-party dependency.
- Do not make `NetworkClient.Instance` create a `GameObject` or call `DontDestroyOnLoad`.
- Every transport callback must enqueue through `INetworkDispatcher`, whose production implementation delegates to `MainThreadDispatcher.Enqueue`, before mutating state or invoking public/business callbacks.
- Invalidate the current generation before handling intentional disconnect, timeout, retry exhaustion, or the first `Error`/`Closed` terminal event so queued callbacks from that transport become stale.
- Online runtime mode remains fail-closed in A3: log the exact A3 error and do not install or connect a network host.
- Test doubles live only below `Assets/Tests/EditMode/Network/TestDoubles`; PlayMode tests use the real application lifecycle and inspect public runtime state, so `Game.PlayModeTests.asmdef` never references EditMode test code.

---

## File Structure

### Runtime files

- Create `Assets/Scripts/Network/Game.Network.asmdef`: owns network and protocol compilation and references `Game.Core`.
- Create `Assets/Scripts/Protocol/Game.Network.asmref`: compiles existing protocol files into `Game.Network` without moving them.
- Create `Assets/Scripts/Network/NetworkCloseInfo.cs`: immutable close code/reason value.
- Create `Assets/Scripts/Network/IWebSocketTransport.cs`: raw byte transport contract.
- Create `Assets/Scripts/Network/IWebSocketTransportFactory.cs`: URL-to-transport factory contract.
- Create `Assets/Scripts/Network/WebSocketTransport.cs`: sole WebSocketSharp adapter and factory implementation.
- Create `Assets/Scripts/Network/INetworkConnectionGateway.cs`: connection facade forwarding contract.
- Create `Assets/Scripts/Network/NoOpNetworkConnectionGateway.cs`: fail-closed gateway used before host registration and after shutdown.
- Replace `Assets/Scripts/Network/NetworkClient.cs`: plain disposable codec/router/session service and compatibility facade.
- Create `Assets/Scripts/Network/NetworkConnectionState.cs`: `Disconnected`, `Connecting`, `Connected`, `Authenticating`, `Ready`, `Reconnecting`, and `Failed` states.
- Create `Assets/Scripts/Network/INetworkDispatcher.cs`: enqueue abstraction used by transport callbacks.
- Create `Assets/Scripts/Network/MainThreadNetworkDispatcher.cs`: production adapter over `MainThreadDispatcher.Enqueue`.
- Create `Assets/Scripts/Network/NetworkConnectionController.cs`: sole connection, generation, timeout, heartbeat, and retry owner.
- Create `Assets/Scripts/Network/NetworkConnectionControllerHost.cs`: Unity `MonoBehaviour`/`IGameService` wrapper and facade gateway binding.
- Create `Assets/Scripts/Network/NetworkStatusAdapter.cs`: pure mapping from controller state to legacy `NetworkStatus` and `ReconnectState`.
- Modify `Assets/Scripts/Application/GameServices.cs`: install, initialize, and shut down client/host in lifecycle order.
- Modify `Assets/Scripts/Application/GameApplication.cs`: preserve A3 Online fail-closed behavior and exact error text.
- Modify `Assets/Scripts/Network/HeartbeatManager.cs`: obsolete status-only wrapper with no heartbeat scheduling.
- Modify `Assets/Scripts/Network/ReconnectionManager.cs`: obsolete status-only wrapper with no coroutine/retry ownership.
- Modify `Assets/Scripts/Managers/LoginManager.cs`: own and dispose the `LoginResp` token and clear the active singleton.
- Modify `Assets/Scripts/Managers/ArchiveManager.cs`: own and dispose `SaveArchiveResp` and `LoadArchiveResp` tokens and clear the active singleton.
- Modify `Assets/Scripts/Managers/RankManager.cs`: own and dispose `GetRankResp` and `SubmitScoreResp` tokens and clear the active singleton.
- Modify `Assets/Scripts/Managers/CombatManager.cs`: own and dispose all seven combat/config/stat response tokens and clear the active singleton.

### Test and assembly files

- Modify `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef`: add the `Game.Network` reference.
- Modify `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef`: add the `Game.Network` reference; do not reference `Game.Core.EditModeTests`.
- Create `Assets/Tests/EditMode/Network/TestDoubles/FakeWebSocketTransport.cs`.
- Create `Assets/Tests/EditMode/Network/TestDoubles/FakeWebSocketTransportFactory.cs`.
- Create `Assets/Tests/EditMode/Network/TestDoubles/FakeNetworkConnectionGateway.cs`.
- Create `Assets/Tests/EditMode/Network/TestDoubles/FakeNetworkDispatcher.cs`.
- Create `Assets/Tests/EditMode/Network/TestDoubles/NetworkTestSettings.cs`.
- Create `Assets/Tests/EditMode/Network/WebSocketTransportContractTests.cs`.
- Create `Assets/Tests/EditMode/Network/NetworkClientTests.cs`.
- Create `Assets/Tests/EditMode/Network/NetworkConnectionControllerTests.cs`.
- Create `Assets/Tests/EditMode/Network/NetworkConnectionControllerHostTests.cs`.
- Create `Assets/Tests/EditMode/Network/LegacyNetworkCompatibilityTests.cs`.
- Create `Assets/Tests/EditMode/Network/ManagerNetworkSubscriptionTests.cs`.
- Modify `Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs`: prove real `GameServices` registration/shutdown and A3 Online fail-closed behavior.
- Modify `docs/superpowers/plans/2026-07-18-phase-a3-network-integration.md`: record final verification evidence in Task 6.

## Shared Test Double Contracts

The following test-only APIs are defined once and reused by Tasks 1-5. They remain in the EditMode test assembly.

```csharp
// Assets/Tests/EditMode/Network/TestDoubles/FakeWebSocketTransport.cs
internal sealed class FakeWebSocketTransport : IWebSocketTransport
{
    public event Action Opened;
    public event Action<byte[]> MessageReceived;
    public event Action<NetworkCloseInfo> Closed;
    public event Action<string> Error;

    public bool IsAlive { get; private set; }
    public int ConnectCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public List<byte[]> SentPayloads { get; } = new List<byte[]>();
    public List<NetworkCloseInfo> CloseCalls { get; } = new List<NetworkCloseInfo>();

    public void ConnectAsync() => ConnectCalls++;
    public void Send(byte[] payload) => SentPayloads.Add(payload);
    public void Close(ushort code, string reason)
    {
        IsAlive = false;
        CloseCalls.Add(new NetworkCloseInfo(code, reason));
    }
    public void Dispose() => DisposeCalls++;

    public void RaiseOpened() { IsAlive = true; Opened?.Invoke(); }
    public void RaiseMessage(byte[] frame) => MessageReceived?.Invoke(frame);
    public void RaiseClosed(ushort code = 1006, string reason = "closed")
    {
        IsAlive = false;
        Closed?.Invoke(new NetworkCloseInfo(code, reason));
    }
    public void RaiseError(string message) => Error?.Invoke(message);
}

// Assets/Tests/EditMode/Network/TestDoubles/FakeWebSocketTransportFactory.cs
internal sealed class FakeWebSocketTransportFactory : IWebSocketTransportFactory
{
    private readonly List<FakeWebSocketTransport> _created = new List<FakeWebSocketTransport>();
    public IReadOnlyList<FakeWebSocketTransport> Created => _created;
    public FakeWebSocketTransport LastTransport => _created[_created.Count - 1];

    public IWebSocketTransport Create(string url)
    {
        var transport = new FakeWebSocketTransport();
        _created.Add(transport);
        return transport;
    }
}

// Assets/Tests/EditMode/Network/TestDoubles/FakeNetworkConnectionGateway.cs
internal sealed class FakeNetworkConnectionGateway : INetworkConnectionGateway
{
    public NetworkConnectionState State { get; set; } = NetworkConnectionState.Disconnected;
    public bool IsConnected => State == NetworkConnectionState.Connected ||
                               State == NetworkConnectionState.Authenticating ||
                               State == NetworkConnectionState.Ready;
    public int ConnectCalls { get; private set; }
    public int DisconnectCalls { get; private set; }
    public string LastUrl { get; private set; }
    public void Connect(string url) { ConnectCalls++; LastUrl = url; }
    public void Disconnect() => DisconnectCalls++;
}

// Assets/Tests/EditMode/Network/TestDoubles/FakeNetworkDispatcher.cs
internal sealed class FakeNetworkDispatcher : INetworkDispatcher
{
    private readonly Queue<Action> _queue = new Queue<Action>();
    public int PendingCount => _queue.Count;
    public bool Enqueue(Action action) { _queue.Enqueue(action); return true; }
    public void PumpOne() => _queue.Dequeue().Invoke();
    public void PumpAll() { while (_queue.Count > 0) PumpOne(); }
}

// Assets/Tests/EditMode/Network/TestDoubles/NetworkTestSettings.cs
internal static class NetworkTestSettings
{
    public static GameRuntimeSettings Create(
        float heartbeat = 10f,
        float timeout = 5f,
        int maxAttempts = 3,
        float initialBackoff = 1f,
        float maxBackoff = 4f)
    {
        var settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();
        var serialized = new UnityEditor.SerializedObject(settings);
        serialized.FindProperty("_serverUrl").stringValue = "ws://unit.test/ws";
        serialized.FindProperty("_heartbeatIntervalSeconds").floatValue = heartbeat;
        serialized.FindProperty("_connectionTimeoutSeconds").floatValue = timeout;
        serialized.FindProperty("_maxReconnectAttempts").intValue = maxAttempts;
        serialized.FindProperty("_initialReconnectBackoffSeconds").floatValue = initialBackoff;
        serialized.FindProperty("_maxReconnectBackoffSeconds").floatValue = maxBackoff;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return settings;
    }
}
```

### Task 1: Assembly Boundary and Raw Transport

**Files:**
- Create: `Assets/Scripts/Network/Game.Network.asmdef`
- Create: `Assets/Scripts/Protocol/Game.Network.asmref`
- Modify: `Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef`
- Create: `Assets/Scripts/Network/NetworkCloseInfo.cs`
- Create: `Assets/Scripts/Network/IWebSocketTransport.cs`
- Create: `Assets/Scripts/Network/IWebSocketTransportFactory.cs`
- Create: `Assets/Scripts/Network/WebSocketTransport.cs`
- Create: `Assets/Tests/EditMode/Network/TestDoubles/FakeWebSocketTransport.cs`
- Create: `Assets/Tests/EditMode/Network/TestDoubles/FakeWebSocketTransportFactory.cs`
- Test: `Assets/Tests/EditMode/Network/WebSocketTransportContractTests.cs`

**Interfaces:**
- Consumes: `WebSocketSharp.WebSocket` from `Assets/Plugins/websocket-sharp.dll` and `Game.Core`.
- Produces: `NetworkCloseInfo(ushort code, string reason)`, `IWebSocketTransport`, `IWebSocketTransportFactory.Create(string url)`, and `WebSocketTransportFactory`.

- [ ] **Step 1: Write the failing contract tests and EditMode fakes**

Create the two test doubles exactly as defined in **Shared Test Double Contracts**, then create:

```csharp
using NUnit.Framework;
using Game.Network;

namespace Game.Tests.EditMode.Network
{
    public sealed class WebSocketTransportContractTests
    {
        [Test]
        public void CloseInfoStoresCodeAndReason()
        {
            var close = new NetworkCloseInfo(1000, "normal");
            Assert.That(close.Code, Is.EqualTo(1000));
            Assert.That(close.Reason, Is.EqualTo("normal"));
        }

        [Test]
        public void FactoryCreatesTransportWithoutConnecting()
        {
            using (var transport = new WebSocketTransportFactory().Create("ws://localhost:8080/ws"))
            {
                Assert.That(transport.IsAlive, Is.False);
            }
        }
    }
}
```

- [ ] **Step 2: Run RED for the missing assembly/contracts**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.WebSocketTransportContractTests -testResults Logs\A3-task1-red.xml -logFile Logs\A3-task1-red.log`

Expected: non-zero exit and compiler errors naming `NetworkCloseInfo`, `IWebSocketTransport`, or `WebSocketTransportFactory`.

- [ ] **Step 3: Create the assembly boundary and exact raw contracts**

Use these definitions:

```csharp
namespace Game.Network
{
    public readonly struct NetworkCloseInfo
    {
        public NetworkCloseInfo(ushort code, string reason) { Code = code; Reason = reason ?? string.Empty; }
        public ushort Code { get; }
        public string Reason { get; }
    }

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

    public interface IWebSocketTransportFactory
    {
        IWebSocketTransport Create(string url);
    }
}
```

Set `Game.Network.asmdef` to `name: Game.Network`, `rootNamespace: Game.Network`, reference `Game.Core`, keep `overrideReferences: false` so `websocket-sharp.dll` is visible, and set `autoReferenced: true`. Set `Game.Network.asmref` to reference `Game.Network`. Add `Game.Network` to `Game.Core.EditModeTests.asmdef.references`.

For `Assets/Scripts/Network/Game.Network.asmdef`:

```json
{
  "name": "Game.Network",
  "rootNamespace": "Game.Network",
  "references": ["Game.Core"],
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

For `Assets/Scripts/Protocol/Game.Network.asmref`:

```json
{ "reference": "Game.Network" }
```

- [ ] **Step 4: Implement the WebSocketSharp adapter with no policy logic**

Implement `Assets/Scripts/Network/WebSocketTransport.cs` with these members and event mappings:

```csharp
public sealed class WebSocketTransportFactory : IWebSocketTransportFactory
{
    public IWebSocketTransport Create(string url) => new WebSocketTransport(url);
}

public sealed class WebSocketTransport : IWebSocketTransport
{
    private readonly WebSocket _socket;
    private bool _disposed;

    public WebSocketTransport(string url)
    {
        _socket = new WebSocket(url ?? throw new ArgumentNullException(nameof(url)));
        _socket.OnOpen += HandleOpen;
        _socket.OnMessage += HandleMessage;
        _socket.OnClose += HandleClose;
        _socket.OnError += HandleError;
    }

    public event Action Opened;
    public event Action<byte[]> MessageReceived;
    public event Action<NetworkCloseInfo> Closed;
    public event Action<string> Error;
    public bool IsAlive => !_disposed && _socket.IsAlive;
    public void ConnectAsync() => _socket.ConnectAsync();
    public void Send(byte[] payload) => _socket.Send(payload);
    public void Close(ushort code, string reason) =>
        _socket.Close((CloseStatusCode)code, reason ?? string.Empty);

    private void HandleOpen(object sender, EventArgs args) => Opened?.Invoke();
    private void HandleMessage(object sender, MessageEventArgs args)
    {
        if (args.IsBinary) MessageReceived?.Invoke(args.RawData);
        else Error?.Invoke("Received a non-binary WebSocket message.");
    }
    private void HandleClose(object sender, CloseEventArgs args) =>
        Closed?.Invoke(new NetworkCloseInfo(args.Code, args.Reason));
    private void HandleError(object sender, ErrorEventArgs args) => Error?.Invoke(args.Message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _socket.OnOpen -= HandleOpen;
        _socket.OnMessage -= HandleMessage;
        _socket.OnClose -= HandleClose;
        _socket.OnError -= HandleError;
        _socket.Dispose();
    }
}
```

The file contains no codec call, timer, coroutine, retry counter, `GameObject`, or `MainThreadDispatcher` call.

- [ ] **Step 5: Run GREEN and asset integrity**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.WebSocketTransportContractTests -testResults Logs\A3-task1-green.xml -logFile Logs\A3-task1-green.log`

Expected: exit code 0 and both contract tests pass.

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\validation\Test-UnityAssetIntegrity.ps1 -ProjectRoot .`

Expected: `Unity asset integrity check passed.`

- [ ] **Step 6: Commit Task 1**

Run:

```powershell
git add Assets/Scripts/Network Assets/Scripts/Protocol/Game.Network.asmref Assets/Scripts/Protocol/Game.Network.asmref.meta Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef Assets/Tests/EditMode/Game.Core.EditModeTests.asmdef.meta Assets/Tests/EditMode/Network.meta Assets/Tests/EditMode/Network
git commit -m "feat: add raw websocket transport boundary"
```

Root coordinator action after clean task review: `git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration`.

### Task 2: Plain NetworkClient, Facade Gateway, and Disposable Subscriptions

**Files:**
- Create: `Assets/Scripts/Network/NetworkConnectionState.cs`
- Create: `Assets/Scripts/Network/INetworkConnectionGateway.cs`
- Create: `Assets/Scripts/Network/NoOpNetworkConnectionGateway.cs`
- Replace: `Assets/Scripts/Network/NetworkClient.cs`
- Create: `Assets/Tests/EditMode/Network/TestDoubles/FakeNetworkConnectionGateway.cs`
- Test: `Assets/Tests/EditMode/Network/NetworkClientTests.cs`

**Interfaces:**
- Consumes: `Codec.Encode(ushort, string)`, `Codec.Encode<T>(ushort, T)`, `Codec.TryDecode(byte[], out ushort, out string)`, and `IWebSocketTransport`.
- Produces: `NetworkConnectionState`; `INetworkConnectionGateway`; `NoOpNetworkConnectionGateway`; constructible `NetworkClient : IDisposable`; static `Instance`, `RegisterInstance`, `UnregisterInstance`, and `ResetStaticState`; disposable typed/raw subscriptions; compatibility connection members.
- Ownership rule: `NetworkClient` stores but never closes or disposes `INetworkConnectionGateway`; Task 4 host binds/unbinds the real gateway. `NetworkClient.SetTransport` only changes the byte-send target; Task 3 controller closes/disposes transports.

- [ ] **Step 1: Write RED tests for routing, disposal, and gateway forwarding**

Create `FakeNetworkConnectionGateway` from **Shared Test Double Contracts**. `NetworkClientTests.cs` imports `System.Linq`, `System.Text.RegularExpressions`, `Game.Network`, `Game.Protocol`, `NUnit.Framework`, `UnityEngine`, and `UnityEngine.TestTools`, then contains these cases:

```csharp
[TearDown]
public void TearDown() => NetworkClient.ResetStaticState();

[Test]
public void ConnectAndDisconnectForwardWithoutOwningGateway()
{
    var gateway = new FakeNetworkConnectionGateway();
    var client = new NetworkClient();
    client.serverUrl = "ws://forward.test/ws";
    client.BindConnectionGateway(gateway);

    client.Connect();
    client.Disconnect();
    client.Dispose();

    Assert.That(gateway.ConnectCalls, Is.EqualTo(1));
    Assert.That(gateway.LastUrl, Is.EqualTo("ws://forward.test/ws"));
    Assert.That(gateway.DisconnectCalls, Is.EqualTo(1));
}

[Test]
public void InstanceGetterCreatesNoUnityObject()
{
    var before = Resources.FindObjectsOfTypeAll<GameObject>()
        .Count(item => item.name == "[NetworkClient]");
    var facade = NetworkClient.Instance;
    var after = Resources.FindObjectsOfTypeAll<GameObject>()
        .Count(item => item.name == "[NetworkClient]");
    Assert.That(facade, Is.Not.Null);
    Assert.That(after, Is.EqualTo(before));
}

[Test]
public void SendEncodesFrameToOpenTransport()
{
    var transport = new FakeWebSocketTransport();
    var client = new NetworkClient();
    client.SetTransport(transport);
    transport.RaiseOpened();

    Assert.That(client.Send(MsgID.LoginReq, new LoginReq { code = "abc" }), Is.True);
    Assert.That(Codec.TryDecode(transport.SentPayloads.Single(), out var id, out _), Is.True);
    Assert.That(id, Is.EqualTo(MsgID.LoginReq));
}

[Test]
public void DisposedTypedSubscriptionStopsReceivingMessages()
{
    var client = new NetworkClient();
    var count = 0;
    var token = client.On<LoginResp>(MsgID.LoginResp, _ => count++);
    token.Dispose();
    client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, new LoginResp { uid = 7, token = "t" }));
    Assert.That(count, Is.Zero);
}

[Test]
public void HandlerCanDisposeItselfDuringSnapshotDispatch()
{
    var client = new NetworkClient();
    var count = 0;
    IDisposable token = null;
    token = client.On(MsgID.HeartbeatResp, _ => { count++; token.Dispose(); });
    var frame = Codec.Encode(MsgID.HeartbeatResp, "{}");
    client.ReceiveFrame(frame);
    client.ReceiveFrame(frame);
    Assert.That(count, Is.EqualTo(1));
}

[Test]
public void DisconnectedSendFailsClosedWithoutTouchingTransport()
{
    var transport = new FakeWebSocketTransport();
    var client = new NetworkClient();
    client.SetTransport(transport);
    Assert.That(client.Send(MsgID.HeartbeatReq, new HeartbeatReq()), Is.False);
    Assert.That(transport.SentPayloads, Is.Empty);
}

[Test]
public void SendWhileDisconnectedReturnsFalseAndLogsConciseWarning()
{
    var client = new NetworkClient();
    LogAssert.Expect(LogType.Warning,
        new Regex(@"\[NetworkClient\] Send dropped because transport is disconnected\. msgId=1001"));

    var sent = client.Send(MsgID.LoginReq, new LoginReq { code = "abc" });

    Assert.That(sent, Is.False);
}

[Test]
public void MalformedFrameAndTypedDeserializationFailureDoNotBlockOtherHandlers()
{
    var client = new NetworkClient();
    var rawCount = 0;
    client.On<LoginResp>(MsgID.LoginResp, _ => Assert.Fail("invalid JSON must not invoke typed handler"));
    client.On(MsgID.LoginResp, _ => rawCount++);
    client.ReceiveFrame(new byte[] { 1, 2, 3 });
    LogAssert.Expect(LogType.Error,
        new Regex(@"\[NetworkClient\] Failed to deserialize message 1002 as LoginResp:"));
    client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, "{"));
    Assert.That(rawCount, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run Task 2 RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkClientTests -testResults Logs\A3-task2-red.xml -logFile Logs\A3-task2-red.log`

Expected: non-zero exit because `NetworkClient` is a `MonoBehaviour`, subscription methods return `void`, and the gateway types do not exist.

- [ ] **Step 3: Define the facade/controller boundary before controller code exists**

Create these exact public contracts in Task 2 so this task compiles independently:

```csharp
namespace Game.Network
{
    public enum NetworkConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Authenticating,
        Ready,
        Reconnecting,
        Failed
    }

    public interface INetworkConnectionGateway
    {
        NetworkConnectionState State { get; }
        bool IsConnected { get; }
        void Connect(string url);
        void Disconnect();
    }

    internal sealed class NoOpNetworkConnectionGateway : INetworkConnectionGateway
    {
        internal static readonly NoOpNetworkConnectionGateway Instance = new NoOpNetworkConnectionGateway();
        public NetworkConnectionState State => NetworkConnectionState.Disconnected;
        public bool IsConnected => false;
        public void Connect(string url) => Debug.LogWarning("[NetworkClient] No connection host is registered; Connect was ignored.");
        public void Disconnect() { }
    }
}
```

Expose these exact `NetworkClient` members:

```csharp
public sealed class NetworkClient : IDisposable
{
    public static NetworkClient Instance { get; }
    public static void RegisterInstance(NetworkClient client);
    public static void UnregisterInstance(NetworkClient client);
    public static void ResetStaticState();

    public NetworkConnectionState ConnectionState { get; }
    public bool IsConnected { get; }
    public bool IsLoggedIn { get; }
    public long UID { get; }
    public string Token { get; }
    public string serverUrl { get; set; }
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public void BindConnectionGateway(INetworkConnectionGateway gateway);
    public void UnbindConnectionGateway(INetworkConnectionGateway gateway);
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
    public void Dispose();

    internal void NotifyConnected();
    internal void NotifyDisconnected();
    internal void NotifyError(string message);
}
```

`BindConnectionGateway` rejects null and replaces only the no-op gateway or the currently bound gateway. `UnbindConnectionGateway` restores the no-op only when `ReferenceEquals` matches. `Dispose` clears handlers/session/transport references but does not invoke `Disconnect` and does not dispose the gateway or transport.

- [ ] **Step 4: Add the corrected facade migration regression before implementing migration**

Add this sequence; the first dispatch proves migration worked before disposal, and the second proves the original token removes the migrated handler:

```csharp
[Test]
public void FacadeTokenRemainsAuthoritativeAfterMigration()
{
    var count = 0;
    var token = NetworkClient.Instance.On<LoginResp>(MsgID.LoginResp, _ => count++);
    var explicitClient = new NetworkClient();
    NetworkClient.RegisterInstance(explicitClient);
    var frame = Codec.Encode(MsgID.LoginResp, new LoginResp { uid = 9, token = "migrated" });

    explicitClient.ReceiveFrame(frame);
    Assert.That(count, Is.EqualTo(1), "the pre-registration handler must migrate exactly once");

    token.Dispose();
    explicitClient.ReceiveFrame(frame);
    Assert.That(count, Is.EqualTo(1), "disposing the original token must remove the migrated handler");
}
```

- [ ] **Step 5: Run facade migration RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkClientTests.FacadeTokenRemainsAuthoritativeAfterMigration -testResults Logs\A3-task2-migration-red.xml -logFile Logs\A3-task2-migration-red.log`

Expected: non-zero exit because pre-registration facade subscriptions are not migrated to the explicit client and the original token cannot remove the migrated handler.

- [ ] **Step 6: Implement exact subscription identity and facade migration**

Store each registration as a record containing `msgId`, the raw `Action<string>` wrapper, and an active flag shared by the returned token. Dispatch a copied array. `Send` while disconnected logs exactly `[NetworkClient] Send dropped because transport is disconnected. msgId={msgId}` and returns `false`. Malformed frames log one concise warning and return. A typed JSON failure logs `[NetworkClient] Failed to deserialize message {msgId} as {typeof(T).Name}: {exception.Message}` and continues with the remaining snapshot handlers. `RegisterInstance` moves every active facade registration to the explicit instance while preserving that shared token state. Disposing the original token removes its migrated wrapper. `UnregisterInstance` changes the active static reference only when the argument is the active explicit instance. `ResetStaticState` disposes the active explicit client, disposes the facade, creates a clean facade, and leaves the no-op gateway active.

- [ ] **Step 7: Run Task 2 GREEN**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkClientTests -testResults Logs\A3-task2-green.xml -logFile Logs\A3-task2-green.log`

Expected: exit code 0 and all `NetworkClientTests` pass.

- [ ] **Step 8: Commit Task 2**

Run:

```powershell
git add Assets/Scripts/Network Assets/Tests/EditMode/Network
git commit -m "feat: make network client service owned"
```

Root coordinator action after clean task review: `git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration`.

### Task 3: Connection Controller State Machine

**Files:**
- Create: `Assets/Scripts/Network/INetworkDispatcher.cs`
- Create: `Assets/Scripts/Network/MainThreadNetworkDispatcher.cs`
- Create: `Assets/Scripts/Network/NetworkConnectionController.cs`
- Create: `Assets/Tests/EditMode/Network/TestDoubles/FakeNetworkDispatcher.cs`
- Create: `Assets/Tests/EditMode/Network/TestDoubles/NetworkTestSettings.cs`
- Test: `Assets/Tests/EditMode/Network/NetworkConnectionControllerTests.cs`

**Interfaces:**
- Consumes: `NetworkClient`, `NetworkConnectionState`, `IWebSocketTransportFactory`, `IWebSocketTransport`, `GameRuntimeSettings`, and `INetworkDispatcher.Enqueue(Action)`.
- Produces: `NetworkConnectionController : IDisposable` with `Connect(string)`, `Disconnect()`, `Tick(float)`, `BeginAuthentication()`, `MarkReady()`, `State`, `StateChanged`, and the exact `INetworkConnectionGateway` contract from Task 2.

- [ ] **Step 1: Create dispatcher/settings fakes and write the first RED transition test**

Create `FakeNetworkDispatcher` and `NetworkTestSettings` from **Shared Test Double Contracts**, then add:

```csharp
[Test]
public void OpenMovesConnectingToConnectedOnlyAfterDispatcherPump()
{
    using (var fixture = ControllerFixture.Create())
    {
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        fixture.Factory.LastTransport.RaiseOpened();
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connecting));
        Assert.That(fixture.Dispatcher.PendingCount, Is.EqualTo(1));
        fixture.Dispatcher.PumpAll();
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connected));
    }
}
```

- [ ] **Step 2: Run the controller RED test**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkConnectionControllerTests.OpenMovesConnectingToConnectedOnlyAfterDispatcherPump -testResults Logs\A3-task3-red.xml -logFile Logs\A3-task3-red.log`

Expected: non-zero exit because the dispatcher seam and controller do not exist.

- [ ] **Step 3: Implement controller construction, generation capture, and state transitions**

Use these signatures:

```csharp
public interface INetworkDispatcher { bool Enqueue(Action action); }

public sealed class MainThreadNetworkDispatcher : INetworkDispatcher
{
    public bool Enqueue(Action action) => MainThreadDispatcher.Enqueue(action);
}

public sealed class NetworkConnectionController : INetworkConnectionGateway, IDisposable
{
    public NetworkConnectionController(NetworkClient client, IWebSocketTransportFactory factory,
        INetworkDispatcher dispatcher, GameRuntimeSettings settings);
    public NetworkConnectionState State { get; }
    public bool IsConnected { get; }
    public event Action<NetworkConnectionState> StateChanged;
    public void Connect(string url);
    public void Disconnect();
    public void Tick(float deltaSeconds);
    public void BeginAuthentication();
    public void MarkReady();
    public void Dispose();
}
```

For the first GREEN only, implement `Connect` to create a fresh transport, call `client.SetTransport(transport)`, set `State` to `Connecting`, capture `var callbackGeneration = _generation` in the `Opened` delegate, enqueue the opened handler through `INetworkDispatcher`, compare `callbackGeneration` with `_generation`, set `State` to `Connected`, reset heartbeat remaining seconds, and call `client.NotifyConnected()`. `Disconnect`, `Tick`, `BeginAuthentication`, `MarkReady`, `Error`, `Closed`, and `MessageReceived` may keep minimal non-throwing bodies until the expanded RED tests below are written and run.

- [ ] **Step 4: Run the first controller GREEN**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkConnectionControllerTests.OpenMovesConnectingToConnectedOnlyAfterDispatcherPump -testResults Logs\A3-task3-first-green.xml -logFile Logs\A3-task3-first-green.log`

Expected: exit code 0 and `OpenMovesConnectingToConnectedOnlyAfterDispatcherPump` passes.

- [ ] **Step 5: Add RED tests for backoff doubling/clamping and timeout closure**

```csharp
[Test]
public void ReconnectBackoffDoublesAndClamps()
{
    using (var fixture = ControllerFixture.Create(initialBackoff: 2f, maxBackoff: 3f))
    {
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        fixture.Factory.LastTransport.RaiseError("first");
        fixture.Dispatcher.PumpAll();
        fixture.Controller.Tick(1.99f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(1));
        fixture.Controller.Tick(0.01f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));

        fixture.Factory.LastTransport.RaiseError("second");
        fixture.Dispatcher.PumpAll();
        fixture.Controller.Tick(2.99f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
        fixture.Controller.Tick(0.01f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(3));

        fixture.Factory.LastTransport.RaiseError("third");
        fixture.Dispatcher.PumpAll();
        fixture.Controller.Tick(3f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(4));
    }
}

[Test]
public void ConnectionTimeoutClosesTransportAndStartsReconnectDelay()
{
    using (var fixture = ControllerFixture.Create(timeout: 5f, initialBackoff: 2f))
    {
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        var timedOut = fixture.Factory.LastTransport;
        fixture.Controller.Tick(5f);
        Assert.That(timedOut.CloseCalls.Single().Code, Is.EqualTo(1001));
        Assert.That(timedOut.DisposeCalls, Is.EqualTo(1));
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Reconnecting));
        fixture.Controller.Tick(2f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
    }
}
```

Declare this exact fixture inside `NetworkConnectionControllerTests.cs`; it owns the temporary `ScriptableObject`, while production code receives only `GameRuntimeSettings`:

```csharp
private sealed class ControllerFixture : IDisposable
{
    public GameRuntimeSettings Settings { get; private set; }
    public NetworkClient Client { get; private set; }
    public FakeWebSocketTransportFactory Factory { get; private set; }
    public FakeNetworkDispatcher Dispatcher { get; private set; }
    public NetworkConnectionController Controller { get; private set; }

    public static ControllerFixture Create(
        float heartbeat = 10f,
        float timeout = 5f,
        int maxAttempts = 3,
        float initialBackoff = 1f,
        float maxBackoff = 4f)
    {
        var fixture = new ControllerFixture
        {
            Settings = NetworkTestSettings.Create(heartbeat, timeout, maxAttempts, initialBackoff, maxBackoff),
            Client = new NetworkClient(),
            Factory = new FakeWebSocketTransportFactory(),
            Dispatcher = new FakeNetworkDispatcher()
        };
        fixture.Controller = new NetworkConnectionController(
            fixture.Client, fixture.Factory, fixture.Dispatcher, fixture.Settings);
        return fixture;
    }

    public void Dispose()
    {
        Controller.Dispose();
        Client.Dispose();
        UnityEngine.Object.DestroyImmediate(Settings);
    }
}
```

- [ ] **Step 6: Add RED tests for terminal races and retry exhaustion**

```csharp
[Test]
public void ErrorThenCloseSchedulesOneReconnect()
{
    using (var fixture = ControllerFixture.Create(initialBackoff: 1f))
    {
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        var first = fixture.Factory.LastTransport;
        first.RaiseError("boom");
        first.RaiseClosed();
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connecting),
            "Error and Closed callbacks must not run inline before the dispatcher pumps them");
        Assert.That(fixture.Dispatcher.PendingCount, Is.EqualTo(2));
        fixture.Dispatcher.PumpAll();
        fixture.Controller.Tick(1f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
        fixture.Factory.LastTransport.RaiseOpened();
        fixture.Dispatcher.PumpAll();
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connected));
        fixture.Controller.Tick(10f);
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
    }
}

[Test]
public void WebSocketErrorLogsStateAndGenerationAfterDispatcherPump()
{
    using (var fixture = ControllerFixture.Create(initialBackoff: 1f))
    {
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        var first = fixture.Factory.LastTransport;
        first.RaiseError("socket down");
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connecting),
            "Error callback must not run inline before the dispatcher pumps it");

        LogAssert.Expect(LogType.Error,
            new Regex(@"\[NetworkConnectionController\] WebSocket error in state Connecting generation 1: socket down"));
        fixture.Dispatcher.PumpAll();
    }
}

[Test]
public void RetryExhaustionInvalidatesAlreadyQueuedOpenCallback()
{
    using (var fixture = ControllerFixture.Create(maxAttempts: 0))
    {
        var errors = 0;
        fixture.Client.OnError += _ => errors++;
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        var first = fixture.Factory.LastTransport;
        first.RaiseError("final");
        first.RaiseOpened();
        fixture.Dispatcher.PumpAll();
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Failed));
        Assert.That(fixture.Factory.Created.Count, Is.EqualTo(1));
        Assert.That(errors, Is.EqualTo(1));
    }
}

[Test]
public void IntentionalDisconnectInvalidatesQueuedOpenAndMessage()
{
    using (var fixture = ControllerFixture.Create())
    {
        var messages = 0;
        fixture.Client.On(MsgID.HeartbeatResp, _ => messages++);
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        var first = fixture.Factory.LastTransport;
        first.RaiseOpened();
        first.RaiseMessage(Codec.Encode(MsgID.HeartbeatResp, "{}"));
        fixture.Controller.Disconnect();
        fixture.Dispatcher.PumpAll();
        Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Disconnected));
        Assert.That(messages, Is.Zero);
    }
}
```

- [ ] **Step 7: Add RED tests for heartbeat cadence and status gating**

```csharp
[Test]
public void HeartbeatUsesConfiguredCadenceOnlyInConnectedAuthenticationAndReady()
{
    using (var fixture = ControllerFixture.Create(heartbeat: 3f))
    {
        fixture.Controller.Connect(fixture.Settings.ServerUrl);
        var transport = fixture.Factory.LastTransport;
        fixture.Controller.Tick(30f);
        Assert.That(transport.SentPayloads, Is.Empty);

        transport.RaiseOpened();
        fixture.Dispatcher.PumpAll();
        fixture.Controller.Tick(2.99f);
        Assert.That(transport.SentPayloads, Is.Empty);
        fixture.Controller.Tick(0.01f);
        Assert.That(DecodeIds(transport.SentPayloads), Is.EqualTo(new[] { MsgID.HeartbeatReq }));

        fixture.Controller.BeginAuthentication();
        fixture.Controller.Tick(3f);
        fixture.Controller.MarkReady();
        fixture.Controller.Tick(3f);
        Assert.That(DecodeIds(transport.SentPayloads), Is.EqualTo(new[]
        {
            MsgID.HeartbeatReq, MsgID.HeartbeatReq, MsgID.HeartbeatReq
        }));

        fixture.Controller.Disconnect();
        fixture.Controller.Tick(30f);
        Assert.That(transport.SentPayloads.Count, Is.EqualTo(3));
    }
}

[Test]
public void HeartbeatIsSuppressedDuringReconnectingAndFailed()
{
    using (var reconnecting = ControllerFixture.Create(heartbeat: 1f, initialBackoff: 5f))
    {
        reconnecting.Controller.Connect(reconnecting.Settings.ServerUrl);
        var transport = reconnecting.Factory.LastTransport;
        transport.RaiseOpened();
        reconnecting.Dispatcher.PumpAll();
        transport.RaiseError("offline");
        reconnecting.Dispatcher.PumpAll();
        reconnecting.Controller.Tick(1f);
        Assert.That(reconnecting.Controller.State, Is.EqualTo(NetworkConnectionState.Reconnecting));
        Assert.That(transport.SentPayloads, Is.Empty);
    }

    using (var failed = ControllerFixture.Create(heartbeat: 1f, maxAttempts: 0))
    {
        failed.Controller.Connect(failed.Settings.ServerUrl);
        var transport = failed.Factory.LastTransport;
        transport.RaiseError("exhausted");
        failed.Dispatcher.PumpAll();
        failed.Controller.Tick(10f);
        Assert.That(failed.Controller.State, Is.EqualTo(NetworkConnectionState.Failed));
        Assert.That(transport.SentPayloads, Is.Empty);
    }
}
```

`DecodeIds` calls `Codec.TryDecode` for every payload and returns the decoded `ushort[]`. Heartbeat payload is `new HeartbeatReq { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }`.

- [ ] **Step 8: Run expanded controller RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkConnectionControllerTests -testResults Logs\A3-task3-expanded-red.xml -logFile Logs\A3-task3-expanded-red.log`

Expected: non-zero exit because the minimal Step 3 controller does not yet implement backoff doubling/clamping, timeout close/dispose, retry exhaustion, queued callback invalidation, duplicate terminal suppression, and heartbeat cadence.

- [ ] **Step 9: Implement controller timers, terminal invalidation, reconnect, and heartbeat**

Implement these concrete fields inside `NetworkConnectionController`: `_state`, `_generation`, `_transport`, `_url`, `_intentionalClose`, `_terminalHandledForGeneration`, `_attempt`, `_timeoutRemaining`, `_reconnectDelayRemaining`, `_nextBackoffSeconds`, `_heartbeatRemaining`, and `_disposed`.

`Tick(float deltaSeconds)` executes in this order: return when disposed or `deltaSeconds <= 0`; decrement `_timeoutRemaining` only in `Connecting` and call the timeout terminal path when it reaches zero; decrement `_reconnectDelayRemaining` only in `Reconnecting` and call `Connect(_url)` when it reaches zero; decrement `_heartbeatRemaining` only in `Connected`, `Authenticating`, or `Ready` and send `MsgID.HeartbeatReq` while resetting `_heartbeatRemaining` to `settings.HeartbeatIntervalSeconds`.

The timeout terminal path increments `_generation`, closes the old transport with code `1001` and reason `Connection timeout`, disposes it, sets client transport to null, then either schedules reconnect or enters `Failed` according to `MaxReconnectAttempts`. The non-intentional `Error` and `Closed` paths share the same scheduling function and set `_terminalHandledForGeneration = true` before any state change. `Error` logs exactly `[NetworkConnectionController] WebSocket error in state {State} generation {generation}: {message}` from inside the queued handler. `Disconnect()` sets `_intentionalClose = true`, increments `_generation`, closes with code `1000`, disposes transport, clears client transport, sets `Disconnected`, and calls `client.NotifyDisconnected()` exactly once when leaving an open state.

Every transport delegate must call `_dispatcher.Enqueue(() => HandleX(callbackGeneration, payload))`; the handler starts with `if (callbackGeneration != _generation || _disposed) return;`. The first `Error` or `Closed` for a generation increments `_generation` before scheduling reconnect so any queued sibling callback becomes stale.

- [ ] **Step 10: Run all controller tests GREEN**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkConnectionControllerTests -testResults Logs\A3-task3-green.xml -logFile Logs\A3-task3-green.log`

Expected: exit code 0 and all state, dispatcher, backoff, timeout, race, exhaustion, and heartbeat tests pass.

- [ ] **Step 11: Commit Task 3**

Run:

```powershell
git add Assets/Scripts/Network Assets/Tests/EditMode/Network
git commit -m "feat: add network connection controller"
```

Root coordinator action after clean task review: `git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration`.

### Task 4: Host, GameServices Integration, Online Gate, and Legacy Retirement

**Files:**
- Create: `Assets/Scripts/Network/NetworkConnectionControllerHost.cs`
- Create: `Assets/Scripts/Network/NetworkStatusAdapter.cs`
- Modify: `Assets/Scripts/Application/GameServices.cs`
- Modify: `Assets/Scripts/Application/GameApplication.cs`
- Modify: `Assets/Scripts/Network/HeartbeatManager.cs`
- Modify: `Assets/Scripts/Network/ReconnectionManager.cs`
- Modify: `Assets/Tests/PlayMode/Game.PlayModeTests.asmdef`
- Test: `Assets/Tests/EditMode/Network/NetworkConnectionControllerHostTests.cs`
- Test: `Assets/Tests/EditMode/Network/LegacyNetworkCompatibilityTests.cs`
- Test: `Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs`

**Interfaces:**
- Consumes: `IGameService`, `GameServiceCollection`, `MainThreadDispatcher`, `GameRuntimeSettings`, Task 2 gateway binding, and Task 3 controller.
- Produces: `NetworkConnectionControllerHost.Install`, host `Initialize/Shutdown`, real `GameServices` registration/unregistration, exact legacy state mapping, and A3 Online fail-closed startup.

- [ ] **Step 1: Write a RED host lifecycle test that calls real Initialize/Shutdown**

```csharp
[Test]
public void HostInitializeBindsGatewayUpdateTicksAndShutdownUnbinds()
{
    var root = new GameObject("host-test-root");
    var client = new NetworkClient();
    var factory = new FakeWebSocketTransportFactory();
    var dispatcher = new FakeNetworkDispatcher();
    var settings = NetworkTestSettings.Create(timeout: 2f);
    var host = NetworkConnectionControllerHost.Install(
        root.transform, client, factory, settings, dispatcher, () => 2f);
    try
    {
        host.Initialize();
        client.Connect(settings.ServerUrl);
        Assert.That(factory.Created.Count, Is.EqualTo(1), "Initialize must bind the facade gateway");

        host.SendMessage("Update");
        Assert.That(factory.Created[0].CloseCalls.Single().Code, Is.EqualTo(1001),
            "Update must tick the controller with the injected delta provider");

        var queued = factory.Created[0];
        queued.RaiseOpened();
        host.Shutdown();
        dispatcher.PumpAll();
        host.SendMessage("Update");
        Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Disconnected));
        Assert.That(factory.Created.Count, Is.EqualTo(1),
            "Shutdown must cancel the reconnect delay that timeout scheduled");

        client.Connect(settings.ServerUrl);
        Assert.That(factory.Created.Count, Is.EqualTo(1), "Shutdown must restore the no-op gateway");
    }
    finally
    {
        host.Shutdown();
        client.Dispose();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(settings);
    }
}
```

- [ ] **Step 2: Run host RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkConnectionControllerHostTests -testResults Logs\A3-task4-host-red.xml -logFile Logs\A3-task4-host-red.log`

Expected: non-zero exit because `NetworkConnectionControllerHost` does not exist.

- [ ] **Step 3: Implement host binding, ticking, and shutdown order**

Use this install contract:

```csharp
public sealed class NetworkConnectionControllerHost : MonoBehaviour, IGameService, INetworkConnectionGateway
{
    public static NetworkConnectionControllerHost Install(
        Transform parent,
        NetworkClient client,
        IWebSocketTransportFactory factory,
        GameRuntimeSettings settings,
        INetworkDispatcher dispatcher = null,
        Func<float> deltaSecondsProvider = null);

    public string ServiceName => nameof(NetworkConnectionControllerHost);
    public NetworkConnectionState State => _controller.State;
    public bool IsConnected => _controller.IsConnected;
    public void Initialize();
    public void Shutdown();
    public void Connect(string url);
    public void Disconnect();
}
```

`Install` creates `[NetworkConnectionControllerHost]` below the supplied parent and constructs the controller. `Initialize` calls `client.BindConnectionGateway(this)` once. `Update` calls `_controller.Tick((_deltaSecondsProvider ?? DefaultDeltaProvider)())`, where `DefaultDeltaProvider` returns `Time.deltaTime`. `Shutdown` invalidates the controller, disconnects/disposes transport state, then calls `client.UnbindConnectionGateway(this)`. `OnDestroy` delegates to idempotent `Shutdown`. Do not modify `GameServices` or `GameApplication` in this step.

- [ ] **Step 4: Add PlayMode RED tests that exercise real GameServices.Create/Shutdown through GameApplication**

Add `Game.Network` to `Game.PlayModeTests.asmdef.references`; do not add any EditMode test assembly reference. Extend `ApplicationOfflineStartupTests` with:

```csharp
[UnityTest]
public IEnumerator OfflineStartupRegistersClientAndShutdownUnregistersIt()
{
    yield return WaitForReady();
    var registered = NetworkClient.Instance;
    Assert.That(FindAll("[NetworkConnectionControllerHost]").Count, Is.EqualTo(1));

    var application = GetApplicationComponent(GameObject.Find("[GameApplication]"));
    application.GetType().GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public)
        .Invoke(application, null);
    yield return null;

    Assert.That(FindAll("[NetworkConnectionControllerHost]").Count, Is.Zero);
    Assert.That(NetworkClient.Instance, Is.Not.SameAs(registered),
        "GameServices.Shutdown must unregister the explicit client and expose a fresh fail-closed facade");
    InvokeEnsureApplication(application.GetType().Assembly);
    yield return WaitForReady();
}
```

Update the existing Online test to expect `Online runtime flow is not implemented in Phase A3`, assert `FailureStage == "Mode.Select"`, assert no `[NetworkConnectionControllerHost]` exists, and assert `NetworkClient.Instance.IsConnected` is false. This test invokes `GameApplication.Awake`, whose Offline path calls `GameServices.Create`, and its cleanup invokes public `GameApplication.Shutdown`, which calls `GameServices.Shutdown`.

- [ ] **Step 5: Run GameServices/Online RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests -testResults Logs\A3-task4-services-red.xml -logFile Logs\A3-task4-services-red.log`

Expected: non-zero exit because the host is not installed, the explicit client is not registered/unregistered, and Online still reports Phase A2.

- [ ] **Step 6: Implement GameServices registration, shutdown, and Online A3 gate**

In `GameServices.Create`, create and `NetworkClient.RegisterInstance(client)` immediately after installing `MainThreadDispatcher`, install `NetworkConnectionControllerHost`, and append host after dispatcher in `GameServiceCollection` so reverse shutdown reaches host before dispatcher. In `GameServices.Shutdown`, call lifecycle shutdown, then `NetworkClient.UnregisterInstance(_networkClient)`, `_networkClient.Dispose()`, `NetworkClient.ResetStaticState()`, and only then `MainThreadDispatcher.ResetStaticState()`.

In `GameApplication`, make `RuntimeMode.Online` set failure state with `FailureStage = "Mode.Select"` and error text containing `Online runtime flow is not implemented in Phase A3`; do not call `GameServices.Create` and do not connect a socket.

- [ ] **Step 7: Run GameServices/Online GREEN before legacy changes**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests -testResults Logs\A3-task4-services-green.xml -logFile Logs\A3-task4-services-green.log`

Expected: exit code 0; Offline registers/unregisters the explicit client through real startup/shutdown and Online fails closed without a connection.

- [ ] **Step 8: Write RED legacy retirement and mapping tests**

```csharp
[Test]
public void LegacyTypesAreObsoleteAndOwnNoTimersOrCoroutines()
{
    Assert.That(Attribute.IsDefined(typeof(HeartbeatManager), typeof(ObsoleteAttribute)), Is.True);
    Assert.That(Attribute.IsDefined(typeof(ReconnectionManager), typeof(ObsoleteAttribute)), Is.True);
    Assert.That(typeof(ReconnectionManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Any(field => field.FieldType == typeof(Coroutine)), Is.False);
    Assert.That(typeof(ReconnectionManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Any(method => typeof(IEnumerator).IsAssignableFrom(method.ReturnType)), Is.False);
}

[Test]
public void HeartbeatCompatibilityMethodsAreNoOps()
{
    var gateway = new FakeNetworkConnectionGateway();
    var client = new NetworkClient();
    client.BindConnectionGateway(gateway);
    var go = new GameObject("heartbeat-legacy-test");
    var manager = go.AddComponent<HeartbeatManager>();
    manager.StartHeartbeat(client);
    manager.StopHeartbeat();
    Assert.That(gateway.ConnectCalls, Is.Zero);
    Assert.That(gateway.DisconnectCalls, Is.Zero);
    Object.DestroyImmediate(go);
    client.Dispose();
}

[TestCase(NetworkConnectionState.Disconnected, NetworkStatus.Disconnected, ReconnectState.Idle)]
[TestCase(NetworkConnectionState.Connecting, NetworkStatus.Unstable, ReconnectState.Idle)]
[TestCase(NetworkConnectionState.Connected, NetworkStatus.Connected, ReconnectState.Connected)]
[TestCase(NetworkConnectionState.Authenticating, NetworkStatus.Connected, ReconnectState.Connected)]
[TestCase(NetworkConnectionState.Ready, NetworkStatus.Connected, ReconnectState.Connected)]
[TestCase(NetworkConnectionState.Reconnecting, NetworkStatus.Reconnecting, ReconnectState.Reconnecting)]
[TestCase(NetworkConnectionState.Failed, NetworkStatus.Disconnected, ReconnectState.Failed)]
public void StatusAdapterUsesExactLegacyMapping(
    NetworkConnectionState state, NetworkStatus networkStatus, ReconnectState reconnectState)
{
    Assert.That(NetworkStatusAdapter.ToNetworkStatus(state), Is.EqualTo(networkStatus));
    Assert.That(NetworkStatusAdapter.ToReconnectState(state), Is.EqualTo(reconnectState));
}
```

- [ ] **Step 9: Run legacy retirement RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.LegacyNetworkCompatibilityTests -testResults Logs\A3-task4-legacy-red.xml -logFile Logs\A3-task4-legacy-red.log`

Expected: non-zero exit because legacy wrappers are not obsolete, `ReconnectionManager` still owns coroutine state, and `NetworkStatusAdapter` does not exist.

- [ ] **Step 10: Retire legacy owners while preserving compatibility calls**

Implement `NetworkStatusAdapter` as a pure switch with the test-case mappings above:

```csharp
public static class NetworkStatusAdapter
{
    public static NetworkStatus ToNetworkStatus(NetworkConnectionState state)
    {
        switch (state)
        {
            case NetworkConnectionState.Connected:
            case NetworkConnectionState.Authenticating:
            case NetworkConnectionState.Ready: return NetworkStatus.Connected;
            case NetworkConnectionState.Connecting: return NetworkStatus.Unstable;
            case NetworkConnectionState.Reconnecting: return NetworkStatus.Reconnecting;
            default: return NetworkStatus.Disconnected;
        }
    }

    public static ReconnectState ToReconnectState(NetworkConnectionState state)
    {
        switch (state)
        {
            case NetworkConnectionState.Connected:
            case NetworkConnectionState.Authenticating:
            case NetworkConnectionState.Ready: return ReconnectState.Connected;
            case NetworkConnectionState.Reconnecting: return ReconnectState.Reconnecting;
            case NetworkConnectionState.Failed: return ReconnectState.Failed;
            default: return ReconnectState.Idle;
        }
    }
}
```

Mark both legacy classes `[Obsolete("NetworkConnectionController owns connection policy.")]`. `HeartbeatManager.StartHeartbeat(NetworkClient)` and `StopHeartbeat()` remain empty methods and are also marked obsolete. `ReconnectionManager.Register`, `StartReconnect`, and `StopReconnect` remain empty compatibility methods and never call `Connect`; `State` returns `NetworkStatusAdapter.ToReconnectState(NetworkClient.Instance.ConnectionState)` and `AttemptCount` always returns `0`. Both wrappers unsubscribe their event handlers on destroy and clear their static active instance only when the destroyed object is that instance.

- [ ] **Step 11: Run Task 4 GREEN across EditMode and PlayMode**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.NetworkConnectionControllerHostTests -testResults Logs\A3-task4-host-green.xml -logFile Logs\A3-task4-host-green.log`

Expected: exit code 0 and host bind/tick/shutdown tests pass.

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.LegacyNetworkCompatibilityTests -testResults Logs\A3-task4-legacy-green.xml -logFile Logs\A3-task4-legacy-green.log`

Expected: exit code 0 and obsolete/no-op/no-coroutine/mapping tests pass.

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests -testResults Logs\A3-task4-play-green.xml -logFile Logs\A3-task4-play-green.log`

Expected: exit code 0; Offline registers and unregisters the explicit client, host shutdown removes the host, and Online fails closed without a connection.

- [ ] **Step 12: Commit Task 4**

Run:

```powershell
git add Assets/Scripts/Network Assets/Scripts/Application/GameServices.cs Assets/Scripts/Application/GameServices.cs.meta Assets/Scripts/Application/GameApplication.cs Assets/Scripts/Application/GameApplication.cs.meta Assets/Tests/EditMode/Network Assets/Tests/PlayMode/Game.PlayModeTests.asmdef Assets/Tests/PlayMode/Game.PlayModeTests.asmdef.meta Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs Assets/Tests/PlayMode/ApplicationOfflineStartupTests.cs.meta
git commit -m "feat: integrate network host into game services"
```

Root coordinator action after clean task review: `git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration`.

### Task 5: Complete Business Manager Subscription Cleanup

**Files:**
- Modify: `Assets/Scripts/Managers/LoginManager.cs`
- Modify: `Assets/Scripts/Managers/ArchiveManager.cs`
- Modify: `Assets/Scripts/Managers/RankManager.cs`
- Modify: `Assets/Scripts/Managers/CombatManager.cs`
- Test: `Assets/Tests/EditMode/Network/ManagerNetworkSubscriptionTests.cs`

**Interfaces:**
- Consumes: `NetworkClient.On<T>(ushort, Action<T>)` returning `IDisposable`.
- Produces: one owned token in Login, two in Archive, two in Rank, seven in Combat; idempotent `OnDestroy` disposal; active singleton clearing without a production test hook.

- [ ] **Step 1: Write one RED cleanup test covering every manager subscription**

Create managers through `new GameObject(...).AddComponent<T>()`, attach counters to every public response event, destroy all four objects, and dispatch all twelve response ids:

```csharp
[Test]
public void DestroyedManagersReleaseAllTwelveSubscriptionsAndClearSingletons()
{
    var client = new NetworkClient();
    NetworkClient.RegisterInstance(client);
    var login = new GameObject("login-test").AddComponent<LoginManager>();
    var archive = new GameObject("archive-test").AddComponent<ArchiveManager>();
    var rank = new GameObject("rank-test").AddComponent<RankManager>();
    var combat = new GameObject("combat-test").AddComponent<CombatManager>();
    var callbacks = 0;

    login.OnLoginSuccess += _ => callbacks++;
    archive.OnSaveSuccess += () => callbacks++;
    archive.OnLoadSuccess += _ => callbacks++;
    rank.OnRankLoaded += _ => callbacks++;
    rank.OnScoreSubmitted += _ => callbacks++;
    combat.OnCombatResult += _ => callbacks++;
    combat.OnEnemyConfigsLoaded += _ => callbacks++;
    combat.OnDungeonConfigLoaded += _ => callbacks++;
    combat.OnStyleConfigsLoaded += _ => callbacks++;
    combat.OnStyleUnlocked += _ => callbacks++;
    combat.OnPlayerStatsLoaded += _ => callbacks++;
    combat.OnError += _ => callbacks++;

    Object.DestroyImmediate(login.gameObject);
    Object.DestroyImmediate(archive.gameObject);
    Object.DestroyImmediate(rank.gameObject);
    Object.DestroyImmediate(combat.gameObject);

    Dispatch(client, MsgID.LoginResp, new LoginResp { uid = 1, token = "x" });
    Dispatch(client, MsgID.SaveArchiveResp, new SaveArchiveResp { success = true });
    Dispatch(client, MsgID.LoadArchiveResp, new LoadArchiveResp { data = "{}" });
    Dispatch(client, MsgID.GetRankResp, new GetRankResp { ranks = new RankItem[0] });
    Dispatch(client, MsgID.SubmitScoreResp, new SubmitScoreResp { success = true, best_score = 8 });
    Dispatch(client, MsgID.CombatResultResp, new CombatResultResp());
    Dispatch(client, MsgID.GetEnemyConfigsResp, new GetEnemyConfigsResp());
    Dispatch(client, MsgID.GetDungeonConfigResp, new GetDungeonConfigResp());
    Dispatch(client, MsgID.GetStyleConfigsResp, new GetStyleConfigsResp());
    Dispatch(client, MsgID.UnlockStyleResp, new UnlockStyleResp());
    Dispatch(client, MsgID.GetPlayerStatsResp, new GetPlayerStatsResp());
    Dispatch(client, MsgID.UpdatePlayerStatsResp, new UpdatePlayerStatsResp { success = false });

    Assert.That(callbacks, Is.Zero, "no destroyed manager callback may remain registered");
    AssertSingletonCleared(typeof(LoginManager), "_instance");
    AssertSingletonCleared(typeof(ArchiveManager), "_instance");
    AssertSingletonCleared(typeof(RankManager), "_instance");
    AssertSingletonCleared(typeof(CombatManager), "_instance");
}

private static void Dispatch<T>(NetworkClient client, ushort id, T payload) =>
    client.ReceiveFrame(Codec.Encode(id, payload));

private static void AssertSingletonCleared(Type type, string fieldName) =>
    Assert.That(type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
```

Use `[TearDown]` to destroy any remaining named manager objects and call `NetworkClient.ResetStaticState()`. Reflection inspects the existing singleton field; no runtime-only-for-tests property or reset method is added.

- [ ] **Step 2: Run manager cleanup RED**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.ManagerNetworkSubscriptionTests -testResults Logs\A3-task5-red.xml -logFile Logs\A3-task5-red.log`

Expected: non-zero exit or failed assertions because callbacks remain registered and `_instance` remains set after destruction.

- [ ] **Step 3: Store and dispose every subscription explicitly**

Add `private readonly List<IDisposable> _networkSubscriptions = new List<IDisposable>();` to each manager. Replace each registration statement with `_networkSubscriptions.Add(client.On<T>(id, handler));`; the exact registration counts are Login 1, Archive 2, Rank 2, and Combat 7. Add this exact cleanup shape to each class:

```csharp
private void OnDestroy()
{
    foreach (var subscription in _networkSubscriptions)
    {
        subscription.Dispose();
    }
    _networkSubscriptions.Clear();
    if (ReferenceEquals(_instance, this))
    {
        _instance = null;
    }
}
```

Do not recreate delegates to unsubscribe and do not add manager reset methods.

- [ ] **Step 4: Run manager cleanup GREEN**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.Network.ManagerNetworkSubscriptionTests -testResults Logs\A3-task5-green.xml -logFile Logs\A3-task5-green.log`

Expected: exit code 0; all twelve post-destroy dispatches produce zero callbacks and all four private singleton fields are null.

- [ ] **Step 5: Commit Task 5**

Run:

```powershell
git add Assets/Scripts/Managers/LoginManager.cs Assets/Scripts/Managers/LoginManager.cs.meta Assets/Scripts/Managers/ArchiveManager.cs Assets/Scripts/Managers/ArchiveManager.cs.meta Assets/Scripts/Managers/RankManager.cs Assets/Scripts/Managers/RankManager.cs.meta Assets/Scripts/Managers/CombatManager.cs Assets/Scripts/Managers/CombatManager.cs.meta Assets/Tests/EditMode/Network/ManagerNetworkSubscriptionTests.cs Assets/Tests/EditMode/Network/ManagerNetworkSubscriptionTests.cs.meta
git commit -m "fix: dispose all manager network subscriptions"
```

Root coordinator action after clean task review: `git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration`.

### Task 6: Full Regression and Branch Readiness

**Files:**
- Modify: `docs/superpowers/plans/2026-07-18-phase-a3-network-integration.md`: check completed boxes and append command result counts/paths below this task.

**Interfaces:**
- Consumes: committed Tasks 1-5.
- Produces: asset-integrity, Pester, full EditMode, full PlayMode, diff hygiene, branch review, commit, and remote branch evidence.

- [ ] **Step 1: Run asset integrity**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\validation\Test-UnityAssetIntegrity.ps1 -ProjectRoot .`

Expected: exit code 0 and `Unity asset integrity check passed.`

- [ ] **Step 2: Run all Pester validation**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module Pester; Invoke-Pester -Script 'tools/validation/UnityAssetIntegrity.Tests.ps1' -EnableExit"`

Expected: exit code 0 and `Passed: 5 Failed: 0`.

- [ ] **Step 3: Run the complete EditMode suite**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform EditMode -testResults Logs\A3-final-editmode-results.xml -logFile Logs\A3-final-editmode.log`

Expected: exit code 0, `Logs\A3-final-editmode-results.xml` reports zero failed tests, and `Logs\A3-final-editmode.log` contains no compiler error.

- [ ] **Step 4: Run the complete PlayMode suite**

Run: `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -runTests -testPlatform PlayMode -testResults Logs\A3-final-playmode-results.xml -logFile Logs\A3-final-playmode.log`

Expected: exit code 0, `Logs\A3-final-playmode-results.xml` reports zero failed tests, and `Logs\A3-final-playmode.log` contains no compiler error.

- [ ] **Step 5: Run exact branch hygiene checks and request whole-branch review**

Run:

```powershell
git diff --check master...HEAD
git status --short
git diff --stat master...HEAD
```

Expected: `git diff --check master...HEAD` exits 0 with no output; `git status --short` lists only the plan evidence edit before the Task 6 commit; the stat contains only A3 files listed in **File Structure**.

Invoke `superpowers:requesting-code-review` against merge base `master` and current `HEAD`. A Critical or Important finding returns execution to the task that owns the cited file; rerun that task's named filtered GREEN command plus Steps 1-4 after the correction.

- [ ] **Step 6: Record evidence and commit Task 6**

Under this step, record the asset-integrity exit code, Pester passed/failed counts, EditMode total/failed counts, PlayMode total/failed counts, and reviewer result. Then run:

```powershell
git add docs/superpowers/plans/2026-07-18-phase-a3-network-integration.md
git commit -m "docs: record phase a3 verification evidence"
git status --short
git log -1 --oneline
```

Expected: final `git status --short` prints nothing and `git log -1 --oneline` shows `docs: record phase a3 verification evidence`.

Root coordinator action after clean final task review:

```powershell
git -c http.version=HTTP/1.1 push origin feature/phase-a3-network-integration
git ls-remote --heads origin feature/phase-a3-network-integration
```

Expected: push exits 0 and `git ls-remote` reports the same HEAD hash for `refs/heads/feature/phase-a3-network-integration`.

- [ ] **Step 7: Hand off the verified branch**

Invoke `superpowers:finishing-a-development-branch` and present its integration choices. Do not merge or remove the worktree until the user selects an integration option.
