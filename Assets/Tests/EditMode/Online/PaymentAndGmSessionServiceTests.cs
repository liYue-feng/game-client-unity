using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Network;
using Game.Online;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using Google.Protobuf;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Online
{
    public sealed class PaymentAndGmSessionServiceTests
    {
        [TearDown]
        public void TearDown() => NetworkClient.ResetStaticState();

        [Test]
        public void PaymentCreateOrderUsesRequestSeq()
        {
            var client = CreateConnectedClient(out var transport);
            using (var service = new PaymentSessionService(client))
            {
                CreateOrderResp received = null;
                PayResultNotify pushed = null;
                service.PaymentResult += value => pushed = value;

                Assert.That(service.CreateOrder(42, value => received = value,
                    reason => Assert.Fail(reason), out var seq), Is.True);
                Assert.That(Codec.TryDecode(transport.SentPayloads.Single(), out var id, out var sentSeq,
                    out var body), Is.True);
                Assert.That(id, Is.EqualTo(MsgID.CreateOrderReq));
                Assert.That(sentSeq, Is.EqualTo(seq).And.Not.Zero);
                Assert.That(CreateOrderReq.Parser.ParseFrom(body).ProductId, Is.EqualTo(42));

                ReceiveUnknown(client, MsgID.CreateOrderResp, seq + 1,
                    new CreateOrderResp { OrderNo = "old" });
                Assert.That(received, Is.Null);
                client.ReceiveFrame(Codec.Encode(MsgID.CreateOrderResp, seq,
                    new CreateOrderResp { OrderNo = "current" }));
                Assert.That(received?.OrderNo, Is.EqualTo("current"));
                Assert.That(pushed, Is.Null, "a correlated response must not be delivered as a push");
            }
        }

        [Test]
        public void PaymentDisabledErrorCompletesOnlyMatchingRequestOnce()
        {
            var client = CreateConnectedClient(out _);
            using (var service = new PaymentSessionService(client))
            {
                var successes = 0;
                var failures = 0;
                var failureReason = string.Empty;
                var pushes = 0;
                service.PaymentResult += _ => pushes++;

                Assert.That(service.CreateOrder(1, _ => successes++, reason =>
                {
                    failures++;
                    failureReason = reason;
                }, out var seq), Is.True);

                ReceiveUnknown(client, MsgID.Error, seq + 1,
                    new ErrorResp { Code = 60001, Msg = "payment is disabled" });
                Assert.That(failures, Is.Zero);

                client.ReceiveFrame(Codec.Encode(MsgID.Error, seq,
                    new ErrorResp { Code = 60001, Msg = "payment is disabled" }));
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(failureReason, Is.EqualTo("[60001] payment is disabled"));
                Assert.That(successes, Is.Zero);
                Assert.That(pushes, Is.Zero);

                ReceiveUnknown(client, MsgID.Error, seq,
                    new ErrorResp { Code = 60001, Msg = "payment is disabled" });
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(successes, Is.Zero);
                Assert.That(pushes, Is.Zero);
            }
        }

        [Test]
        public void PaymentNotificationUsesZeroSeqPush()
        {
            var client = CreateConnectedClient(out _);
            using (var service = new PaymentSessionService(client))
            {
                PayResultNotify received = null;
                service.PaymentResult += value => received = value;
                ReceiveUnknown(client, MsgID.PayResultNotify, 77,
                    new PayResultNotify { OrderNo = "wrong", Status = "success", ProductId = 1 });
                Assert.That(received, Is.Null);

                client.ReceiveFrame(Codec.Encode(MsgID.PayResultNotify, 0,
                    new PayResultNotify { OrderNo = "push", Status = "success", ProductId = 2 }));
                Assert.That(received?.OrderNo, Is.EqualTo("push"));
            }
        }

        [Test]
        public void GmCommandResponseUsesRequestSeq()
        {
            var client = CreateConnectedClient(out var transport);
            using (var service = new GmCommandService(client))
            {
                GMCommandResp received = null;
                GMCommandResp pushed = null;
                service.BroadcastReceived += value => pushed = value;
                var args = new byte[] { 1, 2, 3 };

                Assert.That(service.Execute("grant", args, value => received = value,
                    reason => Assert.Fail(reason), out var seq),
                    Is.True);
                Assert.That(Codec.TryDecode(transport.SentPayloads.Single(), out var id, out var sentSeq,
                    out var body), Is.True);
                Assert.That(id, Is.EqualTo(MsgID.GMCommandReq));
                Assert.That(sentSeq, Is.EqualTo(seq).And.Not.Zero);
                var request = GMCommandReq.Parser.ParseFrom(body);
                Assert.That(request.Cmd, Is.EqualTo("grant"));
                CollectionAssert.AreEqual(args, request.ArgsJson.ToByteArray());

                ReceiveUnknown(client, MsgID.GMCommandResp, seq + 1,
                    new GMCommandResp { Cmd = "grant", Result = "old" });
                Assert.That(received, Is.Null);
                client.ReceiveFrame(Codec.Encode(MsgID.GMCommandResp, seq,
                    new GMCommandResp { Cmd = "grant", Result = "current" }));
                Assert.That(received?.Result, Is.EqualTo("current"));
                Assert.That(pushed, Is.Null, "a correlated response must not be delivered as a broadcast");
            }
        }

        [Test]
        public void GmBroadcastUsesZeroSeqPush()
        {
            var client = CreateConnectedClient(out _);
            using (var service = new GmCommandService(client))
            {
                GMCommandResp received = null;
                service.BroadcastReceived += value => received = value;
                ReceiveUnknown(client, MsgID.GMCommandResp, 88,
                    new GMCommandResp { Cmd = "notice", Result = "wrong" });
                Assert.That(received, Is.Null);

                client.ReceiveFrame(Codec.Encode(MsgID.GMCommandResp, 0,
                    new GMCommandResp { Cmd = "notice", Result = "push" }));
                Assert.That(received?.Result, Is.EqualTo("push"));
            }
        }

        [Test]
        public void PushSubscriptionsDisposeAndIsolateSubscriberExceptions()
        {
            var client = CreateConnectedClient(out _);
            var payment = new PaymentSessionService(client);
            var gm = new GmCommandService(client);
            var paymentCalls = 0;
            var gmCalls = 0;
            payment.PaymentResult += _ => throw new InvalidOperationException("payment observer failed");
            payment.PaymentResult += _ => paymentCalls++;
            gm.BroadcastReceived += _ => throw new InvalidOperationException("gm observer failed");
            gm.BroadcastReceived += _ => gmCalls++;

            LogAssert.Expect(LogType.Exception, new Regex("payment observer failed"));
            client.ReceiveFrame(Codec.Encode(MsgID.PayResultNotify, 0, new PayResultNotify()));
            LogAssert.Expect(LogType.Exception, new Regex("gm observer failed"));
            client.ReceiveFrame(Codec.Encode(MsgID.GMCommandResp, 0, new GMCommandResp()));
            Assert.That(paymentCalls, Is.EqualTo(1));
            Assert.That(gmCalls, Is.EqualTo(1));

            payment.Dispose();
            gm.Dispose();
            client.ReceiveFrame(Codec.Encode(MsgID.PayResultNotify, 0, new PayResultNotify()));
            client.ReceiveFrame(Codec.Encode(MsgID.GMCommandResp, 0, new GMCommandResp()));
            Assert.That(paymentCalls, Is.EqualTo(1));
            Assert.That(gmCalls, Is.EqualTo(1));
        }

        [Test]
        public void PaymentDisposeCancelsConcurrentRequestsAndSuppressesLateFrames()
        {
            var client = CreateConnectedClient(out var transport);
            var service = new PaymentSessionService(client);
            var successes = 0;
            var failures = 0;
            var pushes = 0;
            service.PaymentResult += _ => pushes++;
            Assert.That(service.CreateOrder(1, _ => successes++, _ => failures++, out var successSeq), Is.True);
            Assert.That(service.CreateOrder(2, _ => successes++, _ => failures++, out var errorSeq), Is.True);
            Assert.That(transport.SentPayloads, Has.Count.EqualTo(2));

            service.Dispose();

            Assert.That(failures, Is.Zero, "dispose cancellation must not escape as a business failure");
            ReceiveUnknown(client, MsgID.CreateOrderResp, successSeq,
                new CreateOrderResp { OrderNo = "late" });
            ReceiveUnknown(client, MsgID.Error, errorSeq,
                new ErrorResp { Code = 500, Msg = "late error" });
            client.ReceiveFrame(Codec.Encode(MsgID.PayResultNotify, 0,
                new PayResultNotify { OrderNo = "late push" }));
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.Zero);
            Assert.That(pushes, Is.Zero);
        }

        [Test]
        public void GmDisposeCancelsConcurrentRequestsAndSuppressesLateFrames()
        {
            var client = CreateConnectedClient(out var transport);
            var service = new GmCommandService(client);
            var successes = 0;
            var failures = 0;
            var pushes = 0;
            service.BroadcastReceived += _ => pushes++;
            Assert.That(service.Execute("one", Array.Empty<byte>(), _ => successes++, _ => failures++,
                out var successSeq), Is.True);
            Assert.That(service.Execute("two", Array.Empty<byte>(), _ => successes++, _ => failures++,
                out var errorSeq), Is.True);
            Assert.That(transport.SentPayloads, Has.Count.EqualTo(2));

            service.Dispose();

            Assert.That(failures, Is.Zero, "dispose cancellation must not escape as a business failure");
            ReceiveUnknown(client, MsgID.GMCommandResp, successSeq,
                new GMCommandResp { Cmd = "one", Result = "late" });
            ReceiveUnknown(client, MsgID.Error, errorSeq,
                new ErrorResp { Code = 500, Msg = "late error" });
            client.ReceiveFrame(Codec.Encode(MsgID.GMCommandResp, 0,
                new GMCommandResp { Cmd = "broadcast", Result = "late push" }));
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.Zero);
            Assert.That(pushes, Is.Zero);
        }

        [Test]
        public void PaymentDisposeDuringSendCancelsTheReentrantPendingRequest()
        {
            var client = CreateConnectedClient(out var transport);
            PaymentSessionService service = null;
            service = new PaymentSessionService(client);
            var successes = 0;
            var failures = 0;
            transport.SendAction = _ => service.Dispose();

            Assert.That(service.CreateOrder(7, _ => successes++, _ => failures++, out var seq), Is.True);

            Assert.That(client.CancelRequest(seq), Is.False, "the disposed service must leave no pending request");
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.Zero);
            ReceiveUnknown(client, MsgID.CreateOrderResp, seq, new CreateOrderResp { OrderNo = "late" });
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.Zero);
        }

        [Test]
        public void GmDisposeDuringSendCancelsTheReentrantPendingRequest()
        {
            var client = CreateConnectedClient(out var transport);
            GmCommandService service = null;
            service = new GmCommandService(client);
            var successes = 0;
            var failures = 0;
            transport.SendAction = _ => service.Dispose();

            Assert.That(service.Execute("dispose", Array.Empty<byte>(), _ => successes++, _ => failures++,
                out var seq), Is.True);

            Assert.That(client.CancelRequest(seq), Is.False, "the disposed service must leave no pending request");
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.Zero);
            ReceiveUnknown(client, MsgID.GMCommandResp, seq,
                new GMCommandResp { Cmd = "dispose", Result = "late" });
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.Zero);
        }

        [Test]
        public void PaymentHandlesSynchronousResponseAndSendFailureOnce()
        {
            var client = CreateConnectedClient(out var transport);
            var service = new PaymentSessionService(client);
            var successes = 0;
            var failures = 0;
            transport.SendAction = frame =>
            {
                Assert.That(Codec.TryDecode(frame, out _, out var seq, out _), Is.True);
                client.ReceiveFrame(Codec.Encode(MsgID.CreateOrderResp, seq,
                    new CreateOrderResp { OrderNo = "sync" }));
            };

            Assert.That(service.CreateOrder(8, _ => successes++, _ => failures++, out var completedSeq), Is.True);
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(failures, Is.Zero);
            Assert.That(client.CancelRequest(completedSeq), Is.False);

            transport.SendAction = null;
            transport.SendException = new InvalidOperationException("send failed");
            Assert.That(service.CreateOrder(9, _ => successes++, _ => failures++, out var failedSeq), Is.False);
            Assert.That(failedSeq, Is.Not.Zero);
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(client.CancelRequest(failedSeq), Is.False);
            service.Dispose();
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void GmHandlesSynchronousErrorOnce()
        {
            var client = CreateConnectedClient(out var transport);
            var service = new GmCommandService(client);
            var successes = 0;
            var failures = 0;
            transport.SendAction = frame =>
            {
                Assert.That(Codec.TryDecode(frame, out _, out var seq, out _), Is.True);
                client.ReceiveFrame(Codec.Encode(MsgID.Error, seq,
                    new ErrorResp { Code = 403, Msg = "denied" }));
            };

            Assert.That(service.Execute("deny", Array.Empty<byte>(), _ => successes++, _ => failures++,
                out var seq), Is.True);
            Assert.That(successes, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(client.CancelRequest(seq), Is.False);
            service.Dispose();
            Assert.That(failures, Is.EqualTo(1));
        }

        private static NetworkClient CreateConnectedClient(out FakeWebSocketTransport transport)
        {
            transport = new FakeWebSocketTransport();
            transport.RaiseOpened();
            var client = new NetworkClient();
            client.SetTransport(transport);
            return client;
        }

        private static void ReceiveUnknown<T>(NetworkClient client, ushort responseId, uint seq, T response)
            where T : class, IMessage<T>
        {
            LogAssert.Expect(LogType.Warning, new Regex($"Dropped response for unknown seq={seq}"));
            client.ReceiveFrame(Codec.Encode(responseId, seq, response));
        }
    }
}
