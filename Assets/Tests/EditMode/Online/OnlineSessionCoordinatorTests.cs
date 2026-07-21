using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Gameplay;
using Game.Network;
using Game.Online;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using Game.Tests.EditMode.Online.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Online
{
    public sealed class OnlineSessionCoordinatorTests
    {
        private const string ServerUrl = "ws://127.0.0.1:8080/ws";

        private NetworkClient _client;
        private FakeWebSocketTransport _transport;
        private FakeOnlineConnection _connection;
        private FakeLoginCodeProvider _provider;
        private LoginSessionService _login;
        private ArchiveSessionService _archive;
        private OnlineSessionCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _client = new NetworkClient();
            _transport = new FakeWebSocketTransport();
            _client.SetTransport(_transport);
            _transport.RaiseOpened();
            NetworkClient.RegisterInstance(_client);

            _connection = new FakeOnlineConnection();
            _provider = new FakeLoginCodeProvider();
            _login = new LoginSessionService(_client);
            _archive = new ArchiveSessionService(_client);
            _coordinator = new OnlineSessionCoordinator(
                _connection,
                _provider,
                _login,
                _archive,
                _client,
                ServerUrl);
        }

        [TearDown]
        public void TearDown()
        {
            _coordinator?.Dispose();
            _archive?.Dispose();
            _login?.Dispose();
            NetworkClient.ResetStaticState();
        }

        [Test]
        public void StartAuthenticatesLoadsArchiveAndBecomesReady()
        {
            var states = new List<OnlineSessionState>();
            _coordinator.StateChanged += states.Add;

            CompleteInitialSession("ink-user", new PlayerArchive { SchemaVersion = 3 });

            Assert.That(states, Is.EqualTo(new[]
            {
                OnlineSessionState.Connecting,
                OnlineSessionState.Authenticating,
                OnlineSessionState.LoadingArchive,
                OnlineSessionState.Ready
            }));
            Assert.That(_connection.LastUrl, Is.EqualTo(ServerUrl));
            Assert.That(_connection.BeginAuthenticationCalls, Is.EqualTo(1));
            Assert.That(_connection.MarkReadyCalls, Is.EqualTo(1));
            Assert.That(_coordinator.Nickname, Is.EqualTo("ink-user"));
            Assert.That(_coordinator.Archive, Is.Not.Null);
            Assert.That(_coordinator.Progress.SchemaVersion, Is.EqualTo(3));
        }

        [Test]
        public void TransportReconnectReauthenticatesWithoutCallingConnectAgain()
        {
            CompleteInitialSession("first", new PlayerArchive { SchemaVersion = 1 });

            _connection.RaiseDisconnected();

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Reconnecting));
            Assert.That(_client.IsLoggedIn, Is.False);
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));

            _connection.RaiseConnected();
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Authenticating));
            Assert.That(_provider.RequestCount, Is.EqualTo(2));
            _provider.Succeed(1);
            ReceiveLogin("second");
            ReceiveArchive(new PlayerArchive { SchemaVersion = 2 });

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_coordinator.Nickname, Is.EqualTo("second"));
            Assert.That(_coordinator.Archive, Is.Not.Null);
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1),
                "A3 owns reconnect and the coordinator must wait for Connected.");
            Assert.That(_connection.BeginAuthenticationCalls, Is.EqualTo(2));
            Assert.That(_connection.MarkReadyCalls, Is.EqualTo(2));
        }

        [Test]
        public void ProviderFailureAndServerErrorBecomeFailed()
        {
            _coordinator.Start();
            _connection.RaiseConnected();
            _provider.Fail(0, "platform unavailable");

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("platform unavailable"));
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));

            _coordinator.Retry();
            _connection.RaiseConnected();
            _provider.Succeed(1);
            _client.ReceiveFrame(EncodeResponse(MsgID.Error,
                new ErrorResp { Code = 9999, Msg = "login denied" }));

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("[9999] login denied"));
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(2));
        }

        [Test]
        public void FailedSessionIgnoresLaterDisconnectAndConnectedUntilRetry()
        {
            _coordinator.Start();
            _connection.RaiseConnected();
            _provider.Fail(0, "platform unavailable");
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));

            _connection.RaiseDisconnected();
            _connection.RaiseConnected();

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("platform unavailable"));
            Assert.That(_provider.RequestCount, Is.EqualTo(1));
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
        }

        [Test]
        public void IdleConnectionNotificationsAreIgnoredAndDoNotBlockStart()
        {
            _connection.RaiseError("pre-start error");
            _connection.RaiseDisconnected();

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Idle));
            Assert.That(_coordinator.FailureReason, Is.Null);

            _coordinator.Start();

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Connecting));
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));
        }

        [Test]
        public void ArchiveServerErrorBecomesFailed()
        {
            _coordinator.Start();
            _connection.RaiseConnected();
            _provider.Succeed(0);
            ReceiveLogin("ink-user");
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.LoadingArchive));

            _client.ReceiveFrame(EncodeResponse(MsgID.Error,
                new ErrorResp { Code = 9999, Msg = "archive unavailable" }));

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("[9999] archive unavailable"));
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
        }

        [Test]
        public void RetryIgnoresDelayedProviderCallbackFromOlderGeneration()
        {
            _coordinator.Start();
            _connection.RaiseConnected();
            Assert.That(_provider.RequestCount, Is.EqualTo(1));

            _connection.RaiseError("socket failed");
            _coordinator.Retry();

            Assert.That(_connection.ConnectCalls, Is.EqualTo(2));
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Connecting));

            _connection.RaiseConnected();
            Assert.That(_provider.RequestCount, Is.EqualTo(2));
            _provider.Succeed(0, "dev:stale");
            Assert.That(_transport.SentPayloads, Is.Empty,
                "a callback captured by an older generation must be inert");

            _provider.Succeed(1, "dev:fresh");
            Assert.That(_transport.SentPayloads, Has.Count.EqualTo(1));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.LoginReq));
        }

        [Test]
        public void StopDisconnectsUnsubscribesAndMakesLaterCallbacksInert()
        {
            var stateChanges = 0;
            _coordinator.StateChanged += _ => stateChanges++;
            _coordinator.Start();
            _connection.RaiseConnected();
            _provider.Succeed(0);
            Assert.That(_transport.SentPayloads, Has.Count.EqualTo(1));

            _coordinator.Stop();
            var changesAfterStop = stateChanges;

            _client.ReceiveFrame(EncodeResponse(MsgID.LoginResp,
                new LoginResp { Uid = 7, Nickname = "stale", Token = "stale-token" }));
            _connection.RaiseConnected();
            _connection.RaiseDisconnected();
            _connection.RaiseError("late error");

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Stopped));
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
            Assert.That(stateChanges, Is.EqualTo(changesAfterStop));
            Assert.That(_client.IsLoggedIn, Is.False);
        }

        [Test]
        public void StopMakesDelayedProviderCallbackInert()
        {
            _coordinator.Start();
            _connection.RaiseConnected();
            Assert.That(_provider.RequestCount, Is.EqualTo(1));

            _coordinator.Stop();
            _provider.Succeed(0, "dev:late");

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Stopped));
            Assert.That(_transport.SentPayloads, Is.Empty);
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
        }

        [Test]
        public void SaveAndReloadAreReadyOnlyAndKeepReadyState()
        {
            Assert.That(_coordinator.SaveArchive(), Is.False);
            Assert.That(_coordinator.ReloadArchive(), Is.False);
            CompleteInitialSession("ink-user", new PlayerArchive { Gold = 1 });

            var saved = 0;
            _coordinator.ArchiveSaved += () => saved++;
            Assert.That(_coordinator.SaveArchive(new PlayerArchive { Gold = 2 }), Is.True);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.SaveArchiveReq));
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp,
                new SaveArchiveResp { Success = true }));
            Assert.That(saved, Is.EqualTo(1));

            Assert.That(_coordinator.ReloadArchive(), Is.True);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.LoadArchiveReq));
            _client.ReceiveFrame(EncodeResponse(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 2 } }));

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_coordinator.Archive.Gold, Is.EqualTo(2));
            Assert.That(_connection.MarkReadyCalls, Is.EqualTo(1));
        }

        [Test]
        public void AcceptedReloadPublishesAfterProgressHydrationAndIgnoresDuplicateResponse()
        {
            var reloads = 0;
            var observedGold = 0;
            _coordinator.ArchiveReloaded += () =>
            {
                reloads++;
                observedGold = _coordinator.Progress.Gold;
            };
            CompleteInitialSession("ink-user", new PlayerArchive { Gold = 1 });

            Assert.That(reloads, Is.Zero, "initial login hydration is not an explicit reload");
            Assert.That(_coordinator.ReloadArchive(), Is.True);
            Assert.That(reloads, Is.Zero, "reload completion waits for the matching response");

            var response = EncodeResponse(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 9 } });
            _client.ReceiveFrame(response);

            Assert.That(reloads, Is.EqualTo(1));
            Assert.That(observedGold, Is.EqualTo(9));
            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(9));

            _client.ReceiveFrame(response);
            Assert.That(reloads, Is.EqualTo(1), "a duplicate response has no active reload to complete");
        }

        [Test]
        public void BusyRejectedReloadDoesNotPublishCompletionForLateLoadResponse()
        {
            CompleteInitialSession("ink-user", new PlayerArchive { Gold = 1 });
            var reloads = 0;
            _coordinator.ArchiveReloaded += () => reloads++;

            Assert.That(_coordinator.SaveArchive(new PlayerArchive { Gold = 2 }), Is.True);
            Assert.That(_coordinator.ReloadArchive(), Is.False, "the archive service is busy saving");
            _client.ReceiveFrame(EncodeResponse(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 99 } }));
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp,
                new SaveArchiveResp { Success = true }));

            Assert.That(reloads, Is.Zero);
            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(2));
        }

        [Test]
        public void StoppedCoordinatorIgnoresLateAcceptedReloadResponse()
        {
            CompleteInitialSession("ink-user", new PlayerArchive { Gold = 1 });
            var reloads = 0;
            _coordinator.ArchiveReloaded += () => reloads++;
            Assert.That(_coordinator.ReloadArchive(), Is.True);

            _coordinator.Stop();
            _client.ReceiveFrame(EncodeResponse(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive { Gold = 9 } }));

            Assert.That(reloads, Is.Zero);
            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(1));
        }

        [Test]
        public void HydrationPrecedesReadyAndArchivesAreDetachedWhileSavesSynchronizeOnAcknowledgement()
        {
            var loaded = new PlayerArchive { Gold = 7, UnlockedStyles = { 1, 3 } };
            CompleteInitialSession("ink-user", loaded);
            loaded.Gold = 99;
            loaded.UnlockedStyles[0] = 99;

            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(7));
            Assert.That(_coordinator.Progress.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));
            var exposedArchive = _coordinator.Archive;
            exposedArchive.Gold = 55;
            exposedArchive.UnlockedStyles[0] = 55;
            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(7));
            Assert.That(_coordinator.Progress.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));

            Assert.That(_coordinator.SaveArchive(new PlayerArchive { Gold = 11, UnlockedStyles = { 2, 4 } }), Is.True);
            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(7), "progress changes only after save success");
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));
            Assert.That(_coordinator.Progress.Gold, Is.EqualTo(11));
            Assert.That(_coordinator.Progress.UnlockedStyles, Is.EqualTo(new[] { 2, 4 }));
        }

        [Test]
        public void ConnectionAdapterForwardsEventsDelegatesCommandsAndUnsubscribes()
        {
            var root = new GameObject("online-adapter-test-root");
            var factory = new FakeWebSocketTransportFactory();
            var dispatcher = new FakeNetworkDispatcher();
            var settings = NetworkTestSettings.Create();
            var host = NetworkConnectionControllerHost.Install(
                root.transform,
                _client,
                factory,
                settings,
                dispatcher);
            host.Initialize();
            var adapter = new OnlineConnectionAdapter(_client, host);
            try
            {
                var connected = 0;
                var errors = 0;
                adapter.Connected += () => connected++;
                adapter.Error += _ => errors++;

                adapter.Connect(settings.ServerUrl);
                factory.LastTransport.RaiseOpened();
                dispatcher.PumpAll();
                Assert.That(connected, Is.EqualTo(1));
                Assert.That(adapter.State, Is.EqualTo(NetworkConnectionState.Connected));

                adapter.BeginAuthentication();
                Assert.That(adapter.State, Is.EqualTo(NetworkConnectionState.Authenticating));
                adapter.MarkReady();
                Assert.That(adapter.State, Is.EqualTo(NetworkConnectionState.Ready));

                InvokeClientNotification(_client, "NotifyError", "expected");
                Assert.That(errors, Is.EqualTo(1));
                adapter.Dispose();
                InvokeClientNotification(_client, "NotifyError", "late");
                Assert.That(errors, Is.EqualTo(1));
            }
            finally
            {
                adapter.Dispose();
                host.Shutdown();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void RetryAfterLoginErrorStartsFreshTransportAndCompletesSession()
        {
            var root = new GameObject("online-retry-real-stack-test-root");
            var client = new NetworkClient();
            var factory = new FakeWebSocketTransportFactory();
            var dispatcher = new FakeNetworkDispatcher();
            var settings = NetworkTestSettings.Create();
            var host = NetworkConnectionControllerHost.Install(
                root.transform,
                client,
                factory,
                settings,
                dispatcher);
            var adapter = new OnlineConnectionAdapter(client, host);
            var provider = new FakeLoginCodeProvider();
            var login = new LoginSessionService(client);
            var archive = new ArchiveSessionService(client);
            var coordinator = new OnlineSessionCoordinator(
                adapter,
                provider,
                login,
                archive,
                client,
                settings.ServerUrl);
            var states = new List<OnlineSessionState>();
            coordinator.StateChanged += states.Add;

            try
            {
                host.Initialize();
                coordinator.Start();
                var failedTransport = factory.LastTransport;
                failedTransport.RaiseOpened();
                dispatcher.PumpAll();
                provider.Succeed(0, "dev:first");
                failedTransport.RaiseMessage(EncodeResponse(failedTransport, MsgID.Error,
                    new ErrorResp { Code = 9999, Msg = "login denied" }));
                dispatcher.PumpAll();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
                Assert.That(coordinator.FailureReason, Is.EqualTo("[9999] login denied"));

                coordinator.Retry();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Connecting),
                    "retry must not be failed by the replaced transport's synchronous disconnect notification");
                Assert.That(factory.Created, Has.Count.EqualTo(2));
                Assert.That(failedTransport.CloseCalls, Has.Count.EqualTo(1));
                Assert.That(failedTransport.CloseCalls[0].Reason, Is.EqualTo("Client disconnect"));
                Assert.That(failedTransport.DisposeCalls, Is.EqualTo(1));

                var retryTransport = factory.LastTransport;
                retryTransport.RaiseOpened();
                dispatcher.PumpAll();
                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Authenticating));
                Assert.That(provider.RequestCount, Is.EqualTo(2));
                provider.Succeed(1, "dev:retry");

                failedTransport.RaiseMessage(EncodeResponse(failedTransport, MsgID.LoginResp,
                    new LoginResp { Uid = 1, Nickname = "stale", Token = "stale-token" }));
                Assert.That(Codec.TryDecode(
                    failedTransport.SentPayloads.Last(), out _, out var staleSeq, out _), Is.True);
                failedTransport.RaiseMessage(Codec.Encode(MsgID.LoadArchiveResp, staleSeq,
                    new LoadArchiveResp { Found = true, Archive = new PlayerArchive { SchemaVersion = 1 } }));
                dispatcher.PumpAll();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Authenticating));
                Assert.That(coordinator.Nickname, Is.Null);
                Assert.That(DecodeMessageIds(retryTransport.SentPayloads),
                    Is.EqualTo(new[] { MsgID.LoginReq }));

                retryTransport.RaiseMessage(EncodeResponse(retryTransport, MsgID.LoginResp,
                    new LoginResp { Uid = 2, Nickname = "retry-user", Token = "retry-token" }));
                dispatcher.PumpAll();
                retryTransport.RaiseMessage(EncodeResponse(retryTransport, MsgID.LoadArchiveResp,
                    new LoadArchiveResp { Found = true, Archive = new PlayerArchive { SchemaVersion = 2 } }));
                dispatcher.PumpAll();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
                Assert.That(coordinator.Nickname, Is.EqualTo("retry-user"));
                Assert.That(coordinator.Archive.SchemaVersion, Is.EqualTo(2));
                Assert.That(DecodeMessageIds(retryTransport.SentPayloads),
                    Is.EqualTo(new[] { MsgID.LoginReq, MsgID.LoadArchiveReq }));
                Assert.That(states.Count(state => state == OnlineSessionState.Authenticating), Is.EqualTo(2),
                    "each connection generation must own exactly one authentication subscription path");
                Assert.That(states.Count(state => state == OnlineSessionState.Ready), Is.EqualTo(1));
            }
            finally
            {
                coordinator.Dispose();
                adapter.Dispose();
                archive.Dispose();
                login.Dispose();
                host.Shutdown();
                client.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ExhaustedTransportErrorThenStopAndDisposeRemainIdempotent()
        {
            var root = new GameObject("online-exhausted-error-test-root");
            var client = new NetworkClient();
            var factory = new FakeWebSocketTransportFactory();
            var dispatcher = new FakeNetworkDispatcher();
            var settings = NetworkTestSettings.Create(maxAttempts: 0);
            var host = NetworkConnectionControllerHost.Install(
                root.transform,
                client,
                factory,
                settings,
                dispatcher);
            var adapter = new OnlineConnectionAdapter(client, host);
            var provider = new FakeLoginCodeProvider();
            var login = new LoginSessionService(client);
            var archive = new ArchiveSessionService(client);
            var coordinator = new OnlineSessionCoordinator(
                adapter,
                provider,
                login,
                archive,
                client,
                settings.ServerUrl);
            var errors = 0;
            var disconnected = 0;
            client.OnError += _ => errors++;
            client.OnDisconnected += () => disconnected++;

            try
            {
                host.Initialize();
                coordinator.Start();
                var failedTransport = factory.LastTransport;
                LogAssert.Expect(LogType.Error,
                    "[NetworkConnectionController] WebSocket error in state Connecting generation 1: exhausted");
                failedTransport.RaiseError("exhausted");
                dispatcher.PumpAll();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
                Assert.That(coordinator.FailureReason, Is.EqualTo("exhausted"));
                Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Disconnected));
                Assert.That(failedTransport.DisposeCalls, Is.EqualTo(1));
                Assert.That(errors, Is.EqualTo(1));
                Assert.That(disconnected, Is.Zero);

                coordinator.Stop();
                coordinator.Stop();
                coordinator.Dispose();
                coordinator.Dispose();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Stopped));
                Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Disconnected));
                Assert.That(failedTransport.DisposeCalls, Is.EqualTo(1));
                Assert.That(errors, Is.EqualTo(1));
                Assert.That(disconnected, Is.Zero);
            }
            finally
            {
                coordinator.Dispose();
                adapter.Dispose();
                archive.Dispose();
                login.Dispose();
                host.Shutdown();
                client.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        private void CompleteInitialSession(string nickname, PlayerArchive archive)
        {
            _coordinator.Start();
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Connecting));
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));
            _connection.RaiseConnected();
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Authenticating));
            _provider.Succeed(0);
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.LoginReq));
            ReceiveLogin(nickname);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.LoadingArchive));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.LoadArchiveReq));
            ReceiveArchive(archive);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
        }

        private void ReceiveLogin(string nickname)
        {
            _client.ReceiveFrame(EncodeResponse(MsgID.LoginResp,
                new LoginResp { Uid = 42, Nickname = nickname, Token = "session-token" }));
        }

        private void ReceiveArchive(PlayerArchive archive)
        {
            _client.ReceiveFrame(EncodeResponse(MsgID.LoadArchiveResp,
                new LoadArchiveResp { Found = true, Archive = archive }));
        }

        [Test]
        public void ActiveCombatSurvivesActualDisconnectReconnectingAndReadyWithSameRunId()
        {
            var root = new GameObject("combat-disconnect-recovery-test-root");
            var client = new NetworkClient();
            var factory = new FakeWebSocketTransportFactory();
            var dispatcher = new FakeNetworkDispatcher();
            var settings = NetworkTestSettings.Create(initialBackoff: 1f);
            var host = NetworkConnectionControllerHost.Install(
                root.transform, client, factory, settings, dispatcher);
            var controller = (NetworkConnectionController)typeof(NetworkConnectionControllerHost)
                .GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(host);
            var adapter = new OnlineConnectionAdapter(client, host);
            var provider = new FakeLoginCodeProvider();
            var login = new LoginSessionService(client);
            var archive = new ArchiveSessionService(client);
            var coordinator = new OnlineSessionCoordinator(
                adapter, provider, login, archive, client, settings.ServerUrl);
            var battle = new BattleSettlementCoordinator(client, archive);
            coordinator.StateChanged += state =>
                battle.SetSessionState(state, coordinator.Generation);

            try
            {
                host.Initialize();
                coordinator.Start();
                var firstTransport = factory.LastTransport;
                firstTransport.RaiseOpened();
                dispatcher.PumpAll();
                provider.Succeed(0, "dev:first");
                firstTransport.RaiseMessage(EncodeResponse(firstTransport, MsgID.LoginResp,
                    new LoginResp { Uid = 1, Nickname = "first", Token = "first-token" }));
                dispatcher.PumpAll();
                firstTransport.RaiseMessage(EncodeResponse(firstTransport, MsgID.LoadArchiveResp,
                    new LoadArchiveResp { Found = true, Archive = new PlayerArchive() }));
                dispatcher.PumpAll();
                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Ready));

                battle.Settle(BattleRunOutcome.Victory,
                    new CombatResultData { killCount = 2, survivalTime = 3, playerLevel = 4 },
                    _ => { });
                var firstRequest = DecodeLastCombatRequest(firstTransport);

                firstTransport.RaiseClosed();
                dispatcher.PumpAll();
                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Reconnecting));
                Assert.That(battle.State, Is.EqualTo(BattleSettlementState.Pending));

                controller.Tick(1f);
                var retryTransport = factory.LastTransport;
                retryTransport.RaiseOpened();
                dispatcher.PumpAll();
                provider.Succeed(1, "dev:retry");
                retryTransport.RaiseMessage(EncodeResponse(retryTransport, MsgID.LoginResp,
                    new LoginResp { Uid = 1, Nickname = "retry", Token = "retry-token" }));
                dispatcher.PumpAll();
                retryTransport.RaiseMessage(EncodeResponse(retryTransport, MsgID.LoadArchiveResp,
                    new LoadArchiveResp { Found = true, Archive = new PlayerArchive() }));
                dispatcher.PumpAll();

                Assert.That(coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
                var retriedRequest = DecodeLastCombatRequest(retryTransport);
                Assert.That(retriedRequest.RunId, Is.EqualTo(firstRequest.RunId));
            }
            finally
            {
                battle.Dispose();
                coordinator.Dispose();
                archive.Dispose();
                login.Dispose();
                adapter.Dispose();
                host.Shutdown();
                client.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        private byte[] EncodeResponse<T>(ushort responseId, T response)
            where T : class, Google.Protobuf.IMessage<T>
        {
            return EncodeResponse(_transport, responseId, response);
        }

        private static byte[] EncodeResponse<T>(
            FakeWebSocketTransport transport,
            ushort responseId,
            T response)
            where T : class, Google.Protobuf.IMessage<T>
        {
            var requestId = responseId == MsgID.LoginResp
                ? MsgID.LoginReq
                : responseId == MsgID.LoadArchiveResp
                    ? MsgID.LoadArchiveReq
                    : responseId == MsgID.SaveArchiveResp
                        ? MsgID.SaveArchiveReq
                        : LastRequestId(transport);
            for (var index = transport.SentPayloads.Count - 1; index >= 0; index--)
            {
                Assert.That(Codec.TryDecode(
                    transport.SentPayloads[index], out var messageId, out var seq, out _), Is.True);
                if (messageId == requestId)
                {
                    return Codec.Encode(responseId, seq, response);
                }
            }

            throw new InvalidOperationException($"No request found for response {responseId}.");
        }

        private static ushort LastRequestId(FakeWebSocketTransport transport)
        {
            Assert.That(transport.SentPayloads, Is.Not.Empty);
            Assert.That(Codec.TryDecode(
                transport.SentPayloads.Last(), out var requestId, out _, out _), Is.True);
            return requestId;
        }

        private static CombatResultReq DecodeLastCombatRequest(FakeWebSocketTransport transport)
        {
            for (var index = transport.SentPayloads.Count - 1; index >= 0; index--)
            {
                Assert.That(Codec.TryDecode(
                    transport.SentPayloads[index], out var messageId, out _, out var body), Is.True);
                if (messageId == MsgID.CombatResultReq)
                {
                    return CombatResultReq.Parser.ParseFrom(body);
                }
            }

            throw new InvalidOperationException("No combat request found.");
        }

        private ushort DecodeLastMessageId()
        {
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var msgId, out _, out _), Is.True);
            return msgId;
        }

        private static ushort[] DecodeMessageIds(IEnumerable<byte[]> payloads)
        {
            return payloads.Select(payload =>
            {
                Assert.That(Codec.TryDecode(payload, out var msgId, out _, out _), Is.True);
                return msgId;
            }).ToArray();
        }

        private static void InvokeClientNotification(NetworkClient client, string methodName, string argument)
        {
            typeof(NetworkClient)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(client, new object[] { argument });
        }
    }
}
