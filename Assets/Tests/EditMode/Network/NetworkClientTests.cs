using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Network;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
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
    }
}
