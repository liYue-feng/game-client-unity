using System;
using Game.Network;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Protocol
{
    public sealed class ProtobufGoldenFrameTests
    {
        [TearDown]
        public void TearDown() => NetworkClient.ResetStaticState();

        [Test]
        public void LoginRequestEncodesToCanonicalLittleEndianProtobufFrame()
        {
            var frame = Codec.Encode(MsgID.LoginReq, 1, new LoginReq { Code = "abc" });

            CollectionAssert.AreEqual(
                new byte[] { 0x0F, 0, 0, 0, 0xE9, 0x03, 1, 0, 0, 0, 0x0A, 0x03, 0x61, 0x62, 0x63 },
                frame);
        }

        [Test]
        public void DecoderReturnsRawProtobufBytesAndRejectsOversizedFrames()
        {
            var frame = Codec.Encode(MsgID.LoginReq, 77, new LoginReq { Code = "abc" });

            Assert.That(Codec.TryDecode(frame, out var id, out var seq, out byte[] body), Is.True);
            Assert.That(id, Is.EqualTo(MsgID.LoginReq));
            Assert.That(seq, Is.EqualTo(77));
            CollectionAssert.AreEqual(new byte[] { 0x0A, 0x03, 0x61, 0x62, 0x63 }, body);

            var oversized = new byte[Codec.MaxFrameSize + 1];
            Assert.That(Codec.TryDecode(oversized, out _, out _, out byte[] _), Is.False);
            Assert.That(Codec.TryDecode(new byte[] { 6, 0, 0, 0, 0xEB, 0x03 }, out _, out _, out _), Is.False);
        }

        [Test]
        public void RegistryCoversEveryCanonicalRouteAndFailsClosedForMalformedOrWrongTypes()
        {
            Assert.That(ProtocolMessageRegistry.Count, Is.EqualTo(MsgID.CanonicalRouteCount));
            Assert.That(ProtocolMessageRegistry.TryParse(MsgID.LoginReq, new byte[] { 0x0A, 0x03, 0x61, 0x62, 0x63 }, out LoginReq request), Is.True);
            Assert.That(request.Code, Is.EqualTo("abc"));
            Assert.That(ProtocolMessageRegistry.TryParse(MsgID.LoginReq, new byte[] { 0x0A }, out LoginReq _), Is.False);
            Assert.That(ProtocolMessageRegistry.TryParse(MsgID.LoginReq, Array.Empty<byte>(), out HeartbeatReq _), Is.False);
            Assert.That(ProtocolMessageRegistry.TryParse(MsgID.HeartbeatReq, Array.Empty<byte>(), out HeartbeatReq empty), Is.True);
            Assert.That(empty.Timestamp, Is.Zero);
        }

        [Test]
        public void RegistryMapsEveryCanonicalRouteToItsGeneratedMessageType()
        {
            AssertRegistryType(MsgID.LoginReq, typeof(LoginReq));
            AssertRegistryType(MsgID.LoginResp, typeof(LoginResp));
            AssertRegistryType(MsgID.HeartbeatReq, typeof(HeartbeatReq));
            AssertRegistryType(MsgID.HeartbeatResp, typeof(HeartbeatResp));
            AssertRegistryType(MsgID.SaveArchiveReq, typeof(SaveArchiveReq));
            AssertRegistryType(MsgID.SaveArchiveResp, typeof(SaveArchiveResp));
            AssertRegistryType(MsgID.LoadArchiveReq, typeof(LoadArchiveReq));
            AssertRegistryType(MsgID.LoadArchiveResp, typeof(LoadArchiveResp));
            AssertRegistryType(MsgID.GetRankReq, typeof(GetRankReq));
            AssertRegistryType(MsgID.GetRankResp, typeof(GetRankResp));
            AssertRegistryType(MsgID.SubmitScoreReq, typeof(SubmitScoreReq));
            AssertRegistryType(MsgID.SubmitScoreResp, typeof(SubmitScoreResp));
            AssertRegistryType(MsgID.CombatResultReq, typeof(CombatResultReq));
            AssertRegistryType(MsgID.CombatResultResp, typeof(CombatResultResp));
            AssertRegistryType(MsgID.GetEnemyConfigsReq, typeof(GetEnemyConfigsReq));
            AssertRegistryType(MsgID.GetEnemyConfigsResp, typeof(GetEnemyConfigsResp));
            AssertRegistryType(MsgID.GetDungeonConfigReq, typeof(GetDungeonConfigReq));
            AssertRegistryType(MsgID.GetDungeonConfigResp, typeof(GetDungeonConfigResp));
            AssertRegistryType(MsgID.GetStyleConfigsReq, typeof(GetStyleConfigsReq));
            AssertRegistryType(MsgID.GetStyleConfigsResp, typeof(GetStyleConfigsResp));
            AssertRegistryType(MsgID.UnlockStyleReq, typeof(UnlockStyleReq));
            AssertRegistryType(MsgID.UnlockStyleResp, typeof(UnlockStyleResp));
            AssertRegistryType(MsgID.GetPlayerStatsReq, typeof(GetPlayerStatsReq));
            AssertRegistryType(MsgID.GetPlayerStatsResp, typeof(GetPlayerStatsResp));
            AssertRegistryType(MsgID.UpdatePlayerStatsReq, typeof(UpdatePlayerStatsReq));
            AssertRegistryType(MsgID.UpdatePlayerStatsResp, typeof(UpdatePlayerStatsResp));
            AssertRegistryType(MsgID.CreateOrderReq, typeof(CreateOrderReq));
            AssertRegistryType(MsgID.CreateOrderResp, typeof(CreateOrderResp));
            AssertRegistryType(MsgID.PayResultNotify, typeof(PayResultNotify));
            AssertRegistryType(MsgID.GMCommandReq, typeof(GMCommandReq));
            AssertRegistryType(MsgID.GMCommandResp, typeof(GMCommandResp));
            AssertRegistryType(MsgID.Error, typeof(ErrorResp));
        }

        [Test]
        public void TypedSubscriptionsMoveDisposeAndIgnoreLateOrMalformedFrames()
        {
            var client = new NetworkClient();
            var received = 0;
            var subscription = client.On<LoginResp>(MsgID.LoginResp, response =>
            {
                received++;
                Assert.That(response.Uid, Is.EqualTo(7));
            });

            Assert.Throws<ArgumentException>(() => client.On<HeartbeatResp>(MsgID.LoginResp, _ => { }));
            client.ReceiveFrame(new byte[] { 7, 0, 0, 0, 0xEA, 0x03, 0xFF });

            NetworkClient.RegisterInstance(client);
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, 0, new LoginResp { Uid = 7, Token = "session" }));
            subscription.Dispose();
            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, 0, new LoginResp { Uid = 7, Token = "late" }));

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void TypedRequestFailsClosedWhileDisconnected()
        {
            var transport = new FakeWebSocketTransport();
            var client = new NetworkClient();
            client.SetTransport(transport);

            LogAssert.Expect(LogType.Warning,
                "[NetworkClient] Send dropped because transport is disconnected. msgId=1001");
            Assert.That(client.Request<LoginReq, LoginResp>(
                MsgID.LoginReq,
                MsgID.LoginResp,
                new LoginReq { Code = "abc" },
                _ => { },
                _ => { },
                out var seq), Is.False);
            Assert.That(seq, Is.Zero);
            Assert.That(transport.SentPayloads, Is.Empty);
        }

        private static void AssertRegistryType(ushort msgID, Type expectedType)
        {
            Assert.That(ProtocolMessageRegistry.TryGetMessageType(msgID, out var actualType), Is.True);
            Assert.That(actualType, Is.EqualTo(expectedType));
        }
    }
}
