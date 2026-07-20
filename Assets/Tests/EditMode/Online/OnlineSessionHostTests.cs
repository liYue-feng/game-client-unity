using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Gameplay;
using Game.Network;
using Game.Online;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using Game.Tests.EditMode.Online.TestDoubles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Online
{
    public sealed class OnlineSessionHostTests
    {
        private const string ServerUrl = "ws://127.0.0.1:8080/ws";

        private GameObject _root;
        private NetworkClient _client;
        private FakeWebSocketTransport _transport;
        private FakeOnlineConnection _connection;
        private FakeLoginCodeProvider _provider;
        private OnlineSessionHost _host;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("online-session-host-test-root");
            _client = new NetworkClient();
            _transport = new FakeWebSocketTransport();
            _client.SetTransport(_transport);
            _transport.RaiseOpened();
            _connection = new FakeOnlineConnection();
            _provider = new FakeLoginCodeProvider();
            _host = InvokeInjectedHostInstall(
                _root.transform,
                _client,
                ServerUrl,
                _connection,
                _provider);
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Shutdown();
            _client?.Dispose();
            Object.DestroyImmediate(_root);
            NetworkClient.ResetStaticState();
        }

        [Test]
        public void InstallParentsHostAndInitializeWiresExactlyOneCoordinatorWithoutConnecting()
        {
            Assert.That(_host.gameObject.name, Is.EqualTo("[OnlineSessionHost]"));
            Assert.That(_host.transform.parent, Is.SameAs(_root.transform));
            Assert.That(GetCoordinator(_host), Is.Not.Null);

            _host.Initialize();
            _host.Initialize();

            Assert.That(GetCoordinator(_host), Is.Not.Null);
            Assert.That(_connection.ConnectCalls, Is.Zero,
                "GameApplication owns the online start decision; Initialize only wires the session.");
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Idle));
        }

        [Test]
        public void HostPublishesImmutableProgressBeforeTheOnlineSessionIsReady()
        {
            _host.Initialize();
            _host.StartSession();
            _connection.RaiseConnected();
            _provider.Succeed(0);
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { Uid = 42, Nickname = "ink-user", Token = "session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 7, UnlockedStyles = { 1, 3 } } }));

            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_host.Progress.Gold, Is.EqualTo(7));
            Assert.That(_host.Progress.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void ReloadArchiveForwardsCompletionOnceAfterProgressHydrationAndIsolatesObservers()
        {
            var laterObservers = 0;
            var observedGold = 0;
            _host.ArchiveReloaded += () => throw new InvalidOperationException("reload observer failed");
            _host.ArchiveReloaded += () =>
            {
                laterObservers++;
                observedGold = _host.Progress.Gold;
            };
            CompleteOnlineSession(new PlayerArchive { Gold = 7 });

            Assert.That(laterObservers, Is.Zero, "initial login hydration is not forwarded as a reload");
            Assert.That(_host.ReloadArchive(), Is.True);
            Assert.That(laterObservers, Is.Zero);
            LogAssert.Expect(LogType.Exception, new Regex("reload observer failed"));

            var response = Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 44 } });
            _client.ReceiveFrame(response);

            Assert.That(laterObservers, Is.EqualTo(1));
            Assert.That(observedGold, Is.EqualTo(44));
            Assert.That(_host.Progress.Gold, Is.EqualTo(44));

            _client.ReceiveFrame(response);
            Assert.That(laterObservers, Is.EqualTo(1));
        }

        [Test]
        public void ShutdownClearsReloadObserversAndMakesLateResponseInert()
        {
            CompleteOnlineSession(new PlayerArchive { Gold = 7 });
            var reloads = 0;
            _host.ArchiveReloaded += () => reloads++;
            Assert.That(_host.ReloadArchive(), Is.True);

            _host.Shutdown();
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 44 } }));

            Assert.That(reloads, Is.Zero);
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Stopped));
            Assert.That(_host.Progress.Gold, Is.EqualTo(7));
        }

        [Test]
        public void BattleSettlementAppliesServerArchiveOnlyAfterSaveAcknowledgement()
        {
            _host.Initialize();
            _host.StartSession();
            _connection.RaiseConnected();
            _provider.Succeed(0);
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { Uid = 42, Nickname = "ink-user", Token = "session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 7 } }));
            BattleSettlementResult result = null;
            var archiveSavedEvents = 0;
            var observedGold = 0;
            _host.ArchiveSaved += () =>
            {
                archiveSavedEvents++;
                observedGold = _host.Progress.Gold;
            };

            _host.BattleSettlement.Settle(
                BattleRunOutcome.Victory,
                new CombatResultData { killCount = 2, playerLevel = 1 },
                value => result = value);
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var messageId, out var body), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.CombatResultReq));
            var request = CombatResultReq.Parser.ParseFrom(body);
            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 44, TalentPoints = 3 }
            }));

            Assert.That(_host.Progress.Gold, Is.EqualTo(7));
            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));

            Assert.That(result?.State, Is.EqualTo(BattleSettlementState.Saved));
            Assert.That(_host.Progress.Gold, Is.EqualTo(44));
            Assert.That(_host.Progress.TalentPoints, Is.EqualTo(3));
            Assert.That(archiveSavedEvents, Is.EqualTo(1));
            Assert.That(observedGold, Is.EqualTo(44));
        }

        [Test]
        public void BattleRetryRecoversFailedSessionAndSendsSameRunAfterRelogin()
        {
            CompleteOnlineSession(new PlayerArchive { Gold = 7 });
            _connection.RaiseError("socket failed");
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Failed));
            BattleSettlementResult result = null;

            _host.BattleSettlement.Settle(
                BattleRunOutcome.Victory,
                new CombatResultData { killCount = 2, playerLevel = 1 },
                value => result = value);
            var coordinator = (BattleSettlementCoordinator)_host.BattleSettlement;
            var runId = coordinator.ActiveRunId;

            Assert.That(result?.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(runId, Is.Not.Empty);
            Assert.That(CombatRequestCount(), Is.Zero);
            Assert.That(_host.BattleSettlement.Retry(), Is.True);
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Connecting));
            Assert.That(_connection.ConnectCalls, Is.EqualTo(2));

            _connection.RaiseConnected();
            _provider.Succeed(1);
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { Uid = 42, Nickname = "ink-user", Token = "new-session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 7 } }));

            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(DecodeLastCombatRequest().RunId, Is.EqualTo(runId));
        }

        [Test]
        public void BattleArchiveFailureRemainsRetryableWithoutFailingTheOnlineSession()
        {
            _host.Initialize();
            _host.StartSession();
            _connection.RaiseConnected();
            _provider.Succeed(0);
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { Uid = 42, Nickname = "ink-user", Token = "session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 7 } }));
            BattleSettlementResult result = null;

            _host.BattleSettlement.Settle(
                BattleRunOutcome.Victory,
                new CombatResultData { killCount = 2, playerLevel = 1 },
                value => result = value);
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var messageId, out var body), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.CombatResultReq));
            var request = CombatResultReq.Parser.ParseFrom(body);
            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 44 }
            }));

            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = false }));

            Assert.That(result?.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_connection.DisconnectCalls, Is.Zero);
            var combatRequestsBeforeRetry = _transport.SentPayloads.Count(frame =>
            {
                Codec.TryDecode(frame, out var id, out _);
                return id == MsgID.CombatResultReq;
            });

            Assert.That(_host.BattleSettlement.Retry(), Is.True);
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out messageId, out _), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.SaveArchiveReq));
            Assert.That(_transport.SentPayloads.Count(frame =>
            {
                Codec.TryDecode(frame, out var id, out _);
                return id == MsgID.CombatResultReq;
            }), Is.EqualTo(combatRequestsBeforeRetry));
        }

        [Test]
        public void MainArchiveSaveOwnsItsAcknowledgementWhileBattleWaitsForArchiveRetry()
        {
            CompleteOnlineSession(new PlayerArchive { Gold = 7 });
            BattleSettlementResult battleResult = null;

            Assert.That(_host.SaveArchive(new PlayerArchive { Gold = 21 }), Is.True);
            _host.BattleSettlement.Settle(BattleRunOutcome.Victory,
                new CombatResultData { killCount = 2, playerLevel = 1 }, value => battleResult = value);
            var request = DecodeLastCombatRequest();
            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 44 }
            }));

            Assert.That(battleResult?.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(SaveRequestCount(), Is.EqualTo(1));
            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));
            Assert.That(_host.Progress.Gold, Is.EqualTo(21));

            var combatRequestsBeforeRetry = CombatRequestCount();
            Assert.That(_host.BattleSettlement.Retry(), Is.True);
            Assert.That(CombatRequestCount(), Is.EqualTo(combatRequestsBeforeRetry));
            Assert.That(SaveRequestCount(), Is.EqualTo(2));
            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));

            Assert.That(battleResult?.State, Is.EqualTo(BattleSettlementState.Saved));
            Assert.That(_host.Progress.Gold, Is.EqualTo(44));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
        }

        [Test]
        public void BattleArchiveSaveOwnsItsAcknowledgementWhileMainSaveIsRejectedWithoutPoisoningSession()
        {
            CompleteOnlineSession(new PlayerArchive { Gold = 7 });
            BattleSettlementResult battleResult = null;
            _host.BattleSettlement.Settle(BattleRunOutcome.Victory,
                new CombatResultData { killCount = 2, playerLevel = 1 }, value => battleResult = value);
            var request = DecodeLastCombatRequest();
            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 44 }
            }));

            Assert.That(_host.SaveArchive(new PlayerArchive { Gold = 21 }), Is.False);
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_host.Progress.Gold, Is.EqualTo(7));
            Assert.That(SaveRequestCount(), Is.EqualTo(1));
            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));

            Assert.That(battleResult?.State, Is.EqualTo(BattleSettlementState.Saved));
            Assert.That(_host.Progress.Gold, Is.EqualTo(44));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_connection.DisconnectCalls, Is.Zero);
        }

        [Test]
        public void ThrowingArchiveSavedObserverDoesNotBlockOtherObserversCompletionOrNextBattle()
        {
            CompleteOnlineSession(new PlayerArchive { Gold = 7 });
            BattleSettlementResult firstResult = null;
            var laterObservers = 0;
            _host.ArchiveSaved += () => throw new InvalidOperationException("observer failed");
            _host.ArchiveSaved += () => laterObservers++;
            LogAssert.Expect(LogType.Exception, new Regex("observer failed"));

            _host.BattleSettlement.Settle(BattleRunOutcome.Victory,
                new CombatResultData { killCount = 2, playerLevel = 1 }, value => firstResult = value);
            var first = DecodeLastCombatRequest();
            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = first.RunId,
                Archive = new PlayerArchive { Gold = 44 }
            }));
            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));

            Assert.That(laterObservers, Is.EqualTo(1));
            Assert.That(firstResult?.State, Is.EqualTo(BattleSettlementState.Saved));
            _host.BattleSettlement.Settle(BattleRunOutcome.Defeat,
                new CombatResultData { killCount = 1, playerLevel = 1 }, _ => { });
            var second = DecodeLastCombatRequest();
            Assert.That(second.RunId, Is.Not.EqualTo(first.RunId));
            Assert.That(CombatRequestCount(), Is.EqualTo(2));
        }

        [Test]
        public void ShutdownIsIdempotentClearsInstanceDisconnectsAndMakesCallbacksInert()
        {
            var stateChanges = 0;
            _host.StateChanged += _ => stateChanges++;
            _host.Initialize();
            _host.StartSession();
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));

            _host.Shutdown();
            _host.Shutdown();
            var changesAfterShutdown = stateChanges;
            _connection.RaiseConnected();
            _connection.RaiseError("late error");

            Assert.That(OnlineSessionHost.Instance, Is.Null);
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Stopped));
            Assert.That(stateChanges, Is.EqualTo(changesAfterShutdown));
        }

        [Test]
        public void ShutdownFromConnectingListenerPreventsTheConnectionCommand()
        {
            _host.Initialize();
            _host.StateChanged += state =>
            {
                if (state == OnlineSessionState.Connecting)
                {
                    _host.Shutdown();
                }
            };

            _host.StartSession();

            Assert.That(_connection.ConnectCalls, Is.Zero);
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Stopped));
            Assert.That(OnlineSessionHost.Instance, Is.Null);
        }

        [Test]
        public void FailedDuplicateGameServicesCompositionDoesNotClearExistingHostOwnership()
        {
            var applicationRoot = new GameObject("duplicate-online-game-services-root");
            var settings = CreateOnlineSettings();
            var transportFactory = new FakeWebSocketTransportFactory();
            var provider = new FakeLoginCodeProvider();
            try
            {
                var invocation = Assert.Throws<TargetInvocationException>(() =>
                    InvokeGameServicesCreate(applicationRoot.transform, settings, transportFactory, provider));

                Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(OnlineSessionHost.Instance, Is.SameAs(_host));
                Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Idle));
                Assert.That(FindObjectsNamed("[OnlineSessionHost]"), Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(applicationRoot);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void OnlineGameServicesCreateInstallsHostUnderServiceRootAndShutdownRemovesIt()
        {
            RemoveFixtureHost();

            var applicationRoot = new GameObject("online-game-services-root");
            var settings = CreateOnlineSettings();
            var transportFactory = new FakeWebSocketTransportFactory();
            var provider = new FakeLoginCodeProvider();
            object services = null;
            try
            {
                services = InvokeGameServicesCreate(applicationRoot.transform, settings, transportFactory, provider);
                var onlineSession = services.GetType()
                    .GetProperty("OnlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(services) as OnlineSessionHost;

                Assert.That(onlineSession, Is.Not.Null);
                Assert.That(onlineSession.transform.parent.gameObject.name, Is.EqualTo("[GameServices]"));
                Assert.That(FindObjectsNamed("[OnlineSessionHost]").Count, Is.EqualTo(1));
                Assert.That(transportFactory.Created, Is.Empty,
                    "Creating Online services must not open a socket before GameApplication starts a session.");

                InvokeShutdown(services);
                services = null;

                Assert.That(OnlineSessionHost.Instance, Is.Null);
                Assert.That(FindObjectsNamed("[OnlineSessionHost]"), Is.Empty);
            }
            finally
            {
                if (services != null)
                {
                    InvokeShutdown(services);
                }

                Object.DestroyImmediate(applicationRoot);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void DuplicateOnlineGameServicesPreflightPreservesTheLiveServiceGraph()
        {
            RemoveFixtureHost();
            var firstRoot = new GameObject("first-online-game-services-root");
            var secondRoot = new GameObject("second-online-game-services-root");
            var firstSettings = CreateOnlineSettings();
            var secondSettings = CreateOnlineSettings();
            object firstServices = null;
            try
            {
                firstServices = InvokeGameServicesCreate(
                    firstRoot.transform,
                    firstSettings,
                    new FakeWebSocketTransportFactory(),
                    new FakeLoginCodeProvider());
                var dispatcher = MainThreadDispatcher.Instance;
                var networkHost = NetworkConnectionControllerHost.Instance;
                var client = NetworkClient.Instance;
                var onlineHost = OnlineSessionHost.Instance;

                var invocation = Assert.Throws<TargetInvocationException>(() =>
                    InvokeGameServicesCreate(
                        secondRoot.transform,
                        secondSettings,
                        new FakeWebSocketTransportFactory(),
                        new FakeLoginCodeProvider()));

                Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(MainThreadDispatcher.Instance, Is.SameAs(dispatcher));
                Assert.That(NetworkConnectionControllerHost.Instance, Is.SameAs(networkHost));
                Assert.That(NetworkClient.Instance, Is.SameAs(client));
                Assert.That(OnlineSessionHost.Instance, Is.SameAs(onlineHost));
                Assert.That(FindObjectsNamed("[GameServices]"), Has.Count.EqualTo(1));
            }
            finally
            {
                if (firstServices != null)
                {
                    InvokeShutdown(firstServices);
                }

                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
                Object.DestroyImmediate(firstSettings);
                Object.DestroyImmediate(secondSettings);
            }
        }

        private static OnlineSessionCoordinator GetCoordinator(OnlineSessionHost host)
        {
            return typeof(OnlineSessionHost)
                .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(host) as OnlineSessionCoordinator;
        }

        private void CompleteOnlineSession(PlayerArchive archive)
        {
            _host.Initialize();
            _host.StartSession();
            _connection.RaiseConnected();
            _provider.Succeed(0);
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { Uid = 42, Nickname = "ink-user", Token = "session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = archive }));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Ready));
        }

        private CombatResultReq DecodeLastCombatRequest()
        {
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var messageId, out var body), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.CombatResultReq));
            return CombatResultReq.Parser.ParseFrom(body);
        }

        private int CombatRequestCount()
        {
            return _transport.SentPayloads.Count(frame =>
            {
                Codec.TryDecode(frame, out var messageId, out _);
                return messageId == MsgID.CombatResultReq;
            });
        }

        private int SaveRequestCount()
        {
            return _transport.SentPayloads.Count(frame =>
            {
                Codec.TryDecode(frame, out var messageId, out _);
                return messageId == MsgID.SaveArchiveReq;
            });
        }

        private void RemoveFixtureHost()
        {
            _host.Shutdown();
            Object.DestroyImmediate(_host.gameObject);
            _host = null;
        }

        private static OnlineSessionHost InvokeInjectedHostInstall(
            Transform parent,
            NetworkClient client,
            string serverUrl,
            IOnlineConnection connection,
            ILoginCodeProvider provider)
        {
            var install = typeof(OnlineSessionHost)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SingleOrDefault(method =>
                {
                    if (method.Name != "Install")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 5 && parameters[3].ParameterType == typeof(IOnlineConnection);
                });

            Assert.That(install, Is.Not.Null,
                "OnlineSessionHost must expose an internal fake-connection composition seam.");
            return (OnlineSessionHost)install.Invoke(
                null,
                new object[] { parent, client, serverUrl, connection, provider });
        }

        private static GameRuntimeSettings CreateOnlineSettings()
        {
            var settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_runtimeMode").enumValueIndex = (int)RuntimeMode.Online;
            serialized.FindProperty("_serverUrl").stringValue = ServerUrl;
            serialized.FindProperty("_editorLoginIdentity").stringValue = "editor-001";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static object InvokeGameServicesCreate(
            Transform applicationRoot,
            GameRuntimeSettings settings,
            IWebSocketTransportFactory transportFactory,
            ILoginCodeProvider provider)
        {
            var servicesType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Game.GameServices", false))
                .FirstOrDefault(type => type != null);
            var create = servicesType?.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 4);

            Assert.That(create, Is.Not.Null,
                "GameServices.Create must expose transport and login provider injection for Online composition tests.");
            return create.Invoke(null, new object[] { applicationRoot, settings, transportFactory, provider });
        }

        private static void InvokeShutdown(object services)
        {
            services.GetType()
                .GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(services, null);
        }

        private static System.Collections.Generic.List<GameObject> FindObjectsNamed(string name)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene.IsValid() && item.name == name)
                .ToList();
        }
    }
}
