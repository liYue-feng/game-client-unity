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
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, new LoginResp { Uid = 7, Token = "t" }));
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void HandlerCanDisposeItselfDuringSnapshotDispatch()
        {
            var client = new NetworkClient();
            var count = 0;
            IDisposable token = null;
            token = client.On<HeartbeatResp>(MsgID.HeartbeatResp, _ => { count++; token.Dispose(); });
            var frame = Codec.Encode(MsgID.HeartbeatResp, new HeartbeatResp());
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
            var frame = Codec.Encode(MsgID.LoginResp, new LoginResp { Uid = 9, Token = "migrated" });
            explicitClient.ReceiveFrame(frame);
            Assert.That(count, Is.EqualTo(1));
            token.Dispose();
            explicitClient.ReceiveFrame(frame);
            Assert.That(count, Is.EqualTo(1));
        }
    }
}
