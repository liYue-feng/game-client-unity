using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Network;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Network
{
    public sealed class NetworkConnectionControllerTests
    {
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

        [Test]
        public void ReconnectBackoffDoublesAndClamps()
        {
            using (var fixture = ControllerFixture.Create(initialBackoff: 2f, maxBackoff: 3f))
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                fixture.Factory.LastTransport.RaiseError("first");
                ExpectWebSocketError(NetworkConnectionState.Connecting, 1, "first");
                fixture.Dispatcher.PumpAll();
                fixture.Controller.Tick(1.99f);
                Assert.That(fixture.Factory.Created.Count, Is.EqualTo(1));
                fixture.Controller.Tick(0.01f);
                Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));

                fixture.Factory.LastTransport.RaiseError("second");
                ExpectWebSocketError(NetworkConnectionState.Connecting, 3, "second");
                fixture.Dispatcher.PumpAll();
                fixture.Controller.Tick(2.99f);
                Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
                fixture.Controller.Tick(0.01f);
                Assert.That(fixture.Factory.Created.Count, Is.EqualTo(3));

                fixture.Factory.LastTransport.RaiseError("third");
                ExpectWebSocketError(NetworkConnectionState.Connecting, 5, "third");
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

        [Test]
        public void ErrorThenCloseSchedulesOneReconnect()
        {
            using (var fixture = ControllerFixture.Create(initialBackoff: 1f))
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var first = fixture.Factory.LastTransport;
                first.RaiseError("boom");
                first.RaiseClosed();
                ExpectWebSocketError(NetworkConnectionState.Connecting, 1, "boom");
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
                ExpectWebSocketError(NetworkConnectionState.Connecting, 1, "final");
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
                fixture.Client.On<HeartbeatResp>(MsgID.HeartbeatResp, _ => messages++);
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var first = fixture.Factory.LastTransport;
                first.RaiseOpened();
                first.RaiseMessage(Codec.Encode(MsgID.HeartbeatResp, 0, new HeartbeatResp()));
                fixture.Controller.Disconnect();
                fixture.Dispatcher.PumpAll();
                Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Disconnected));
                Assert.That(messages, Is.Zero);
            }
        }

        [Test]
        public void ConnectWhileConnectingReplacesAndDisposesPreviousTransport()
        {
            using (var fixture = ControllerFixture.Create())
            {
                var disconnected = 0;
                fixture.Client.OnDisconnected += () => disconnected++;
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var first = fixture.Factory.LastTransport;

                fixture.Controller.Connect(fixture.Settings.ServerUrl);

                Assert.That(first.CloseCalls, Has.Count.EqualTo(1));
                Assert.That(first.CloseCalls[0].Code, Is.EqualTo(1000));
                Assert.That(first.CloseCalls[0].Reason, Is.EqualTo("Connection replaced"));
                Assert.That(first.DisposeCalls, Is.EqualTo(1));
                Assert.That(disconnected, Is.Zero);
                Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
                Assert.That(fixture.Factory.LastTransport.ConnectCalls, Is.EqualTo(1));
                Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connecting));
            }
        }

        [Test]
        public void ReplacingAliveTransportBeforeQueuedOpenDrainsPendingExactlyOnce()
        {
            using (var fixture = ControllerFixture.Create())
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var first = fixture.Factory.LastTransport;
                first.RaiseOpened();
                var failures = 0;
                Assert.That(fixture.Client.Request<LoginReq, LoginResp>(
                    MsgID.LoginReq, MsgID.LoginResp, new LoginReq { Code = "pending" },
                    _ => { }, _ => failures++, out _), Is.True);

                fixture.Controller.Connect("ws://replacement.example/ws");
                first.RaiseClosed();
                fixture.Dispatcher.PumpAll();

                Assert.That(failures, Is.EqualTo(1));
            }
        }

        [Test]
        public void DirectCloseBeforeQueuedOpenDispatchDrainsPendingExactlyOnce()
        {
            using (var fixture = ControllerFixture.Create())
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var transport = fixture.Factory.LastTransport;
                transport.RaiseOpened();
                var failures = 0;
                Assert.That(fixture.Client.Request<LoginReq, LoginResp>(
                    MsgID.LoginReq, MsgID.LoginResp, new LoginReq { Code = "pending" },
                    _ => { }, _ => failures++, out _), Is.True);

                transport.RaiseClosed();
                fixture.Dispatcher.PumpLast();
                Assert.That(failures, Is.EqualTo(1));

                fixture.Dispatcher.PumpAll();
                Assert.That(failures, Is.EqualTo(1));
            }
        }

        [Test]
        public void TimeoutBeforeQueuedOpenDispatchDrainsPendingExactlyOnce()
        {
            using (var fixture = ControllerFixture.Create(timeout: 5f))
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                fixture.Factory.LastTransport.RaiseOpened();
                var failures = 0;
                Assert.That(fixture.Client.Request<LoginReq, LoginResp>(
                    MsgID.LoginReq, MsgID.LoginResp, new LoginReq { Code = "pending" },
                    _ => { }, _ => failures++, out _), Is.True);

                fixture.Controller.Tick(5f);
                Assert.That(failures, Is.EqualTo(1));

                fixture.Dispatcher.PumpAll();
                Assert.That(failures, Is.EqualTo(1));
            }
        }

        [Test]
        public void ConnectWhileReadyNotifiesDisconnectAndIgnoresReplacedTransportCallbacks()
        {
            using (var fixture = ControllerFixture.Create())
            {
                var disconnected = 0;
                var messages = 0;
                fixture.Client.OnDisconnected += () => disconnected++;
                fixture.Client.On<HeartbeatResp>(MsgID.HeartbeatResp, _ => messages++);
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var first = fixture.Factory.LastTransport;
                first.RaiseOpened();
                fixture.Dispatcher.PumpAll();
                fixture.Controller.BeginAuthentication();
                fixture.Controller.MarkReady();

                fixture.Controller.Connect("ws://replacement.example/ws");
                first.RaiseOpened();
                first.RaiseMessage(Codec.Encode(MsgID.HeartbeatResp, 0, new HeartbeatResp()));
                first.RaiseClosed();
                first.RaiseError("stale error");
                fixture.Dispatcher.PumpAll();

                Assert.That(first.CloseCalls, Has.Count.EqualTo(1));
                Assert.That(first.CloseCalls[0].Code, Is.EqualTo(1000));
                Assert.That(first.CloseCalls[0].Reason, Is.EqualTo("Connection replaced"));
                Assert.That(first.DisposeCalls, Is.EqualTo(1));
                Assert.That(disconnected, Is.EqualTo(1));
                Assert.That(messages, Is.Zero);
                Assert.That(fixture.Factory.Created.Count, Is.EqualTo(2));
                Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connecting));
            }
        }

        [Test]
        public void ReentrantConnectFromDisconnectedHandlerKeepsNestedTransportActive()
        {
            using (var fixture = ControllerFixture.Create())
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var first = fixture.Factory.LastTransport;
                first.RaiseOpened();
                fixture.Dispatcher.PumpAll();
                fixture.Controller.BeginAuthentication();
                fixture.Controller.MarkReady();

                Action connectNested = () => fixture.Controller.Connect("ws://nested.example/ws");
                fixture.Client.OnDisconnected += connectNested;
                fixture.Controller.Connect("ws://outer.example/ws");
                fixture.Client.OnDisconnected -= connectNested;

                Assert.That(fixture.Factory.Created, Has.Count.EqualTo(3));
                var outer = fixture.Factory.Created[1];
                var nested = fixture.Factory.Created[2];
                Assert.That(first.CloseCalls, Has.Count.EqualTo(1));
                Assert.That(first.DisposeCalls, Is.EqualTo(1));
                Assert.That(outer.CloseCalls, Has.Count.EqualTo(1));
                Assert.That(outer.CloseCalls[0].Reason, Is.EqualTo("Connection replaced"));
                Assert.That(outer.DisposeCalls, Is.EqualTo(1));
                Assert.That(nested.CloseCalls, Is.Empty);
                Assert.That(nested.DisposeCalls, Is.Zero);

                first.RaiseClosed();
                outer.RaiseClosed();
                fixture.Dispatcher.PumpAll();

                Assert.That(nested.CloseCalls, Is.Empty);
                Assert.That(nested.DisposeCalls, Is.Zero);
                Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connecting));
                nested.RaiseOpened();
                fixture.Dispatcher.PumpAll();
                Assert.That(fixture.Controller.State, Is.EqualTo(NetworkConnectionState.Connected));
                Assert.That(fixture.Client.Request<HeartbeatReq, HeartbeatResp>(
                    MsgID.HeartbeatReq,
                    MsgID.HeartbeatResp,
                    new HeartbeatReq(),
                    _ => { },
                    _ => { },
                    out var seq), Is.True);
                Assert.That(seq, Is.Not.Zero);
                Assert.That(outer.SentPayloads, Is.Empty);
                Assert.That(nested.SentPayloads, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void HeartbeatUsesConfiguredCadenceOnlyInConnectedAuthenticationAndReady()
        {
            using (var fixture = ControllerFixture.Create(heartbeat: 3f, timeout: 31f))
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
                CompleteLastHeartbeat(fixture.Client, transport);

                fixture.Controller.BeginAuthentication();
                fixture.Controller.Tick(3f);
                CompleteLastHeartbeat(fixture.Client, transport);
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
        public void HeartbeatUsesNonZeroSeqWithoutRequestTimer()
        {
            using (var fixture = ControllerFixture.Create(heartbeat: 1f))
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var transport = fixture.Factory.LastTransport;
                transport.RaiseOpened();
                fixture.Dispatcher.PumpAll();

                fixture.Controller.Tick(1f);
                fixture.Controller.Tick(1f);

                Assert.That(transport.SentPayloads, Has.Count.EqualTo(1));
                Assert.That(Codec.TryDecode(
                    transport.SentPayloads[0], out _, out var firstSeq, out _), Is.True);
                Assert.That(firstSeq, Is.Not.Zero);

                LogAssert.Expect(LogType.Warning, new Regex("unknown seq"));
                fixture.Client.ReceiveFrame(Codec.Encode(
                    MsgID.HeartbeatResp, firstSeq + 1, new HeartbeatResp()));
                fixture.Controller.Tick(1f);
                Assert.That(transport.SentPayloads, Has.Count.EqualTo(1));

                fixture.Client.ReceiveFrame(Codec.Encode(
                    MsgID.HeartbeatResp, firstSeq, new HeartbeatResp()));
                fixture.Controller.Tick(1f);
                Assert.That(transport.SentPayloads, Has.Count.EqualTo(2));
                Assert.That(Codec.TryDecode(
                    transport.SentPayloads[1], out _, out var secondSeq, out _), Is.True);
                Assert.That(secondSeq, Is.Not.Zero.And.Not.EqualTo(firstSeq));
                Assert.That(fixture.Client.CancelRequest(secondSeq), Is.True);
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
                ExpectWebSocketError(NetworkConnectionState.Connected, 1, "offline");
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
                ExpectWebSocketError(NetworkConnectionState.Connecting, 1, "exhausted");
                failed.Dispatcher.PumpAll();
                failed.Controller.Tick(10f);
                Assert.That(failed.Controller.State, Is.EqualTo(NetworkConnectionState.Failed));
                Assert.That(transport.SentPayloads, Is.Empty);
            }
        }

        [Test]
        public void RemoteCloseFailsPendingOnce()
        {
            using (var fixture = ControllerFixture.Create())
            {
                fixture.Controller.Connect(fixture.Settings.ServerUrl);
                var transport = fixture.Factory.LastTransport;
                transport.RaiseOpened();
                fixture.Dispatcher.PumpAll();
                var failures = 0;
                fixture.Client.Request<LoginReq, LoginResp>(
                    MsgID.LoginReq, MsgID.LoginResp, new LoginReq { Code = "pending" },
                    _ => { }, _ => failures++, out _);

                transport.RaiseClosed();
                fixture.Dispatcher.PumpAll();

                Assert.That(failures, Is.EqualTo(1));
            }
        }

        private static ushort[] DecodeIds(IEnumerable<byte[]> payloads)
        {
            return payloads.Select(payload =>
            {
                Assert.That(Codec.TryDecode(payload, out var msgId, out _, out _), Is.True);
                return msgId;
            }).ToArray();
        }

        private static void CompleteLastHeartbeat(
            NetworkClient client,
            FakeWebSocketTransport transport)
        {
            Assert.That(Codec.TryDecode(
                transport.SentPayloads.Last(), out var messageId, out var seq, out _), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.HeartbeatReq));
            client.ReceiveFrame(Codec.Encode(MsgID.HeartbeatResp, seq, new HeartbeatResp()));
        }

        private static void ExpectWebSocketError(
            NetworkConnectionState state,
            int generation,
            string message)
        {
            LogAssert.Expect(LogType.Error, new Regex(
                $@"\[NetworkConnectionController\] WebSocket error in state {state} generation {generation}: {Regex.Escape(message)}"));
        }

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
                    Settings = NetworkTestSettings.Create(
                        heartbeat, timeout, maxAttempts, initialBackoff, maxBackoff),
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
    }
}
