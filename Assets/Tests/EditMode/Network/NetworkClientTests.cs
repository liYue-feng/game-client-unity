using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Network;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Network
{
    public sealed class NetworkClientTests
    {
        [TearDown]
        public void TearDown() => NetworkClient.ResetStaticState();

        [Test]
        public void ConnectAndDisconnectForwardWithoutOwningGateway()
        {
            var gateway = new FakeNetworkConnectionGateway();
            var client = new NetworkClient { serverUrl = "ws://forward.test/ws" };
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
            var before = Resources.FindObjectsOfTypeAll<GameObject>().Count(item => item.name == "[NetworkClient]");
            Assert.That(NetworkClient.Instance, Is.Not.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(item => item.name == "[NetworkClient]"), Is.EqualTo(before));
        }

        [Test]
        public void SendEncodesFrameToOpenTransport()
        {
            var transport = new FakeWebSocketTransport();
            var client = new NetworkClient();
            client.SetTransport(transport);
            transport.RaiseOpened();
            Assert.That(client.Send(MsgID.LoginReq, new LoginReq { Code = "abc" }), Is.True);
            Assert.That(Codec.TryDecode(transport.SentPayloads.Single(), out var id, out var seq, out _), Is.True);
            Assert.That(id, Is.EqualTo(MsgID.LoginReq));
            Assert.That(seq, Is.Not.Zero);
        }

        [Test]
        public void DisposedTypedSubscriptionStopsReceivingMessages()
        {
            var client = new NetworkClient();
            var count = 0;
            var token = client.On<LoginResp>(MsgID.LoginResp, _ => count++);
            token.Dispose();
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, 0, new LoginResp { Uid = 7, Token = "t" }));
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void HandlerCanDisposeItselfDuringSnapshotDispatch()
        {
            var client = new NetworkClient();
            var count = 0;
            IDisposable token = null;
            token = client.On<HeartbeatResp>(MsgID.HeartbeatResp, _ => { count++; token.Dispose(); });
            var frame = Codec.Encode(MsgID.HeartbeatResp, 0, new HeartbeatResp());
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
            LogAssert.Expect(LogType.Warning, new Regex(@"\[NetworkClient\] Send dropped because transport is disconnected\. msgId=1001"));
            Assert.That(client.Send(MsgID.LoginReq, new LoginReq { Code = "abc" }), Is.False);
        }

        [Test]
        public void MalformedFrameDoesNotInvokeTypedHandler()
        {
            var client = new NetworkClient();
            var count = 0;
            client.On<LoginResp>(MsgID.LoginResp, _ => count++);
            client.ReceiveFrame(new byte[] { 7, 0, 0, 0, 0xEA, 0x03, 0xFF });
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void FacadeTokenRemainsAuthoritativeAfterMigration()
        {
            var count = 0;
            var token = NetworkClient.Instance.On<LoginResp>(MsgID.LoginResp, _ => count++);
            var explicitClient = new NetworkClient();
            NetworkClient.RegisterInstance(explicitClient);
            var frame = Codec.Encode(MsgID.LoginResp, 0, new LoginResp { Uid = 9, Token = "migrated" });
            explicitClient.ReceiveFrame(frame);
            Assert.That(count, Is.EqualTo(1));
            token.Dispose();
            explicitClient.ReceiveFrame(frame);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void RequestRegistersPendingBeforeSynchronousResponseCanArrive()
        {
            var client = CreateConnectedClient(out var transport);
            var successes = 0;
            var failures = 0;
            transport.SendAction = payload =>
            {
                Assert.That(Codec.TryDecode(payload, out _, out var sentSeq, out _), Is.True);
                client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, sentSeq,
                    new LoginResp { Uid = 17, Token = "sync" }));
            };

            Assert.That(RequestLogin(client, "sync", _ => successes++, _ => failures++, out var seq), Is.True);
            Assert.That(seq, Is.EqualTo(1));
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(failures, Is.Zero);
        }

        [Test]
        public void RequestSerializationFailureLeavesSeqZeroNoPendingAndDoesNotConsumeSequence()
        {
            var client = CreateConnectedClient(out var transport);
            var failures = 0;

            Assert.That(client.Request<ThrowingMessage, LoginResp>(
                MsgID.LoginReq, MsgID.LoginResp, new ThrowingMessage(), _ => { }, _ => failures++, out var failedSeq),
                Is.False);
            Assert.That(failedSeq, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));

            Assert.That(RequestLogin(client, "next", _ => { }, _ => { }, out var nextSeq), Is.True);
            Assert.That(nextSeq, Is.EqualTo(1));
            Assert.That(transport.SentPayloads, Has.Count.EqualTo(1));
        }

        [Test]
        public void RequestSynchronousSendFailureCompletesOnceAndDoesNotLeakPending()
        {
            var client = CreateConnectedClient(out var transport);
            transport.SendException = new InvalidOperationException("send failed");
            var successes = 0;
            var failures = 0;

            Assert.That(RequestLogin(client, "fail", _ => successes++, _ => failures++, out var seq), Is.False);
            Assert.That(seq, Is.EqualTo(1));
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));

            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=1"));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 99 }));
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void SequencesIncrementWrapAndSkipPending()
        {
            var client = CreateConnectedClient(out _);
            Assert.That(RequestLogin(client, "one", _ => { }, _ => { }, out var first), Is.True);
            SetNextSequence(client, uint.MaxValue);
            Assert.That(RequestLogin(client, "max", _ => { }, _ => { }, out var second), Is.True);
            Assert.That(RequestLogin(client, "wrapped", _ => { }, _ => { }, out var third), Is.True);

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(uint.MaxValue));
            Assert.That(third, Is.EqualTo(2));
        }

        [Test]
        public void OutOfOrderSameResponseIdRequestsCompleteBySeq()
        {
            var client = CreateConnectedClient(out _);
            long firstUid = 0;
            long secondUid = 0;
            RequestLogin(client, "first", response => firstUid = response.Uid, _ => { }, out var firstSeq);
            RequestLogin(client, "second", response => secondUid = response.Uid, _ => { }, out var secondSeq);

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, secondSeq, new LoginResp { Uid = 22 }));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, firstSeq, new LoginResp { Uid = 11 }));

            Assert.That(firstUid, Is.EqualTo(11));
            Assert.That(secondUid, Is.EqualTo(22));
        }

        [Test]
        public void ErrorResponseCompletesOnlyMatchingSeq()
        {
            var client = CreateConnectedClient(out _);
            var firstSuccesses = 0;
            var firstFailures = 0;
            var secondFailures = 0;
            RequestLogin(client, "first", _ => firstSuccesses++, _ => firstFailures++, out var firstSeq);
            RequestLogin(client, "second", _ => { }, _ => secondFailures++, out var secondSeq);

            client.ReceiveFrame(Codec.Encode(MsgID.Error, secondSeq,
                new ErrorResp { Code = 777, Msg = "denied" }));
            Assert.That(firstSuccesses, Is.Zero);
            Assert.That(firstFailures, Is.Zero);
            Assert.That(secondFailures, Is.EqualTo(1));

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, firstSeq, new LoginResp { Uid = 1 }));
            Assert.That(firstSuccesses, Is.EqualTo(1));
        }

        [Test]
        public void WrongResponseIdFailsOnce()
        {
            var client = CreateConnectedClient(out _);
            var successes = 0;
            var failures = 0;
            RequestLogin(client, "wrong", _ => successes++, _ => failures++, out var seq);

            client.ReceiveFrame(Codec.Encode(MsgID.HeartbeatResp, seq, new HeartbeatResp()));
            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=1"));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 1 }));

            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void MalformedBodyFailsOnce()
        {
            var client = CreateConnectedClient(out _);
            var successes = 0;
            var failures = 0;
            RequestLogin(client, "malformed", _ => successes++, _ => failures++, out var seq);

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new byte[] { 0x0A }));
            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=1"));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 1 }));

            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void UnknownAndDuplicateSeqAreDropped()
        {
            var client = CreateConnectedClient(out _);
            var successes = 0;
            RequestLogin(client, "known", _ => successes++, _ => { }, out var seq);
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 1 }));

            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=1"));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 2 }));
            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=999"));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, 999, new LoginResp { Uid = 3 }));

            Assert.That(successes, Is.EqualTo(1));
        }

        [Test]
        public void ZeroSeqPushDoesNotCompletePending()
        {
            var client = CreateConnectedClient(out _);
            var pushes = 0;
            var successes = 0;
            client.On<LoginResp>(MsgID.LoginResp, _ => pushes++);
            RequestLogin(client, "pending", _ => successes++, _ => { }, out var seq);

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, 0, new LoginResp { Uid = 7 }));
            Assert.That(pushes, Is.EqualTo(1));
            Assert.That(successes, Is.Zero);

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 8 }));
            Assert.That(pushes, Is.EqualTo(1));
            Assert.That(successes, Is.EqualTo(1));
        }

        [Test]
        public void CancelIgnoresLateSuccessAndError()
        {
            var client = CreateConnectedClient(out _);
            var successes = 0;
            var failures = 0;
            RequestLogin(client, "cancel", _ => successes++, _ => failures++, out var seq);

            Assert.That(client.CancelRequest(seq), Is.True);
            Assert.That(client.CancelRequest(seq), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=1"));
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, seq, new LoginResp { Uid = 1 }));
            LogAssert.Expect(LogType.Warning, new Regex(@"Dropped response for unknown seq=1"));
            client.ReceiveFrame(Codec.Encode(MsgID.Error, seq, new ErrorResp { Code = 1, Msg = "late" }));

            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectAndDisposeFailPendingOnce()
        {
            var gateway = new FakeNetworkConnectionGateway { State = NetworkConnectionState.Ready };
            var disconnecting = CreateConnectedClient(out _);
            disconnecting.BindConnectionGateway(gateway);
            var disconnectFailures = 0;
            RequestLogin(disconnecting, "disconnect", _ => { }, _ => disconnectFailures++, out _);
            disconnecting.Disconnect();
            Assert.That(disconnectFailures, Is.EqualTo(1));

            var disposing = CreateConnectedClient(out _);
            var disposeFailures = 0;
            RequestLogin(disposing, "dispose", _ => { }, _ => disposeFailures++, out _);
            disposing.Dispose();
            disposing.Dispose();
            Assert.That(disposeFailures, Is.EqualTo(1));
        }

        private static NetworkClient CreateConnectedClient(out FakeWebSocketTransport transport)
        {
            transport = new FakeWebSocketTransport();
            transport.RaiseOpened();
            var client = new NetworkClient();
            client.SetTransport(transport);
            return client;
        }

        private static bool RequestLogin(
            NetworkClient client,
            string code,
            Action<LoginResp> onSuccess,
            Action<string> onFailure,
            out uint seq)
        {
            return client.Request<LoginReq, LoginResp>(
                MsgID.LoginReq, MsgID.LoginResp, new LoginReq { Code = code },
                onSuccess, onFailure, out seq);
        }

        private static void SetNextSequence(NetworkClient client, uint value)
        {
            var field = typeof(NetworkClient).GetField(
                "_nextSeq", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(client, value);
        }

        private sealed class ThrowingMessage : IMessage<ThrowingMessage>
        {
            public MessageDescriptor Descriptor => null;

            public ThrowingMessage Clone() => new ThrowingMessage();

            public bool Equals(ThrowingMessage other) => ReferenceEquals(this, other);

            public int CalculateSize() => throw new InvalidOperationException("serialization failed");

            public void MergeFrom(ThrowingMessage message) { }

            public void MergeFrom(CodedInputStream input) { }

            public void WriteTo(CodedOutputStream output) { }
        }
    }
}
