using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Network;
using Game.Online;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using Game.Tests.EditMode.Online.TestDoubles;
using NUnit.Framework;
using UnityEngine;
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

            CompleteInitialSession("ink-user", "{\"chapter\":3}");

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
            Assert.That(_coordinator.ArchiveData, Is.EqualTo("{\"chapter\":3}"));
        }

        [Test]
        public void TransportReconnectReauthenticatesWithoutCallingConnectAgain()
        {
            CompleteInitialSession("first", "{\"chapter\":1}");

            _connection.RaiseDisconnected();

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Reconnecting));
            Assert.That(_client.IsLoggedIn, Is.False);
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));

            _connection.RaiseConnected();
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Authenticating));
            Assert.That(_provider.RequestCount, Is.EqualTo(2));
            _provider.Succeed(1);
            ReceiveLogin("second");
            ReceiveArchive("{\"chapter\":2}");

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_coordinator.Nickname, Is.EqualTo("second"));
            Assert.That(_coordinator.ArchiveData, Is.EqualTo("{\"chapter\":2}"));
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

            _coordinator.Retry();
            _connection.RaiseConnected();
            _provider.Succeed(1);
            _client.ReceiveFrame(Codec.Encode(MsgID.Error,
                new ErrorResp { code = 9999, msg = "login denied" }));

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("[9999] login denied"));
        }

        [Test]
        public void FailedSessionIgnoresLaterDisconnectAndConnectedUntilRetry()
        {
            _coordinator.Start();
            _connection.RaiseConnected();
            _provider.Fail(0, "platform unavailable");
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));

            _connection.RaiseDisconnected();
            _connection.RaiseConnected();

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("platform unavailable"));
            Assert.That(_provider.RequestCount, Is.EqualTo(1));
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));
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

            _client.ReceiveFrame(Codec.Encode(MsgID.Error,
                new ErrorResp { code = 9999, msg = "archive unavailable" }));

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Failed));
            Assert.That(_coordinator.FailureReason, Is.EqualTo("[9999] archive unavailable"));
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

            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { uid = 7, nickname = "stale", token = "stale-token" }));
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
            Assert.That(_coordinator.SaveArchive("{}"), Is.False);
            Assert.That(_coordinator.ReloadArchive(), Is.False);
            CompleteInitialSession("ink-user", "{\"gold\":1}");

            var saved = 0;
            _coordinator.ArchiveSaved += () => saved++;
            Assert.That(_coordinator.SaveArchive("{\"gold\":2}"), Is.True);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.SaveArchiveReq));
            _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp,
                new SaveArchiveResp { success = true }));
            Assert.That(saved, Is.EqualTo(1));

            Assert.That(_coordinator.ReloadArchive(), Is.True);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.LoadArchiveReq));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { data = "{\"gold\":2}" }));

            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(_coordinator.ArchiveData, Is.EqualTo("{\"gold\":2}"));
            Assert.That(_connection.MarkReadyCalls, Is.EqualTo(1));
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

        private void CompleteInitialSession(string nickname, string archiveData)
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
            ReceiveArchive(archiveData);
            Assert.That(_coordinator.State, Is.EqualTo(OnlineSessionState.Ready));
        }

        private void ReceiveLogin(string nickname)
        {
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                new LoginResp { uid = 42, nickname = nickname, token = "session-token" }));
        }

        private void ReceiveArchive(string data)
        {
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                new LoadArchiveResp { data = data }));
        }

        private ushort DecodeLastMessageId()
        {
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var msgId, out _), Is.True);
            return msgId;
        }

        private static void InvokeClientNotification(NetworkClient client, string methodName, string argument)
        {
            typeof(NetworkClient)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(client, new object[] { argument });
        }
    }
}
