using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Gameplay;
using Game.Network;
using Game.Online;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Online
{
    public sealed class BattleSettlementCoordinatorTests
    {
        private NetworkClient _client;
        private FakeWebSocketTransport _transport;
        private ArchiveSessionService _archive;
        private BattleSettlementCoordinator _coordinator;
        private PlayerArchive _appliedArchive;

        [SetUp]
        public void SetUp()
        {
            _appliedArchive = null;
            _client = new NetworkClient();
            _transport = new FakeWebSocketTransport();
            _client.SetTransport(_transport);
            _transport.RaiseOpened();
            _archive = new ArchiveSessionService(_client);
            _coordinator = new BattleSettlementCoordinator(
                _client,
                _archive,
                archive => _appliedArchive = archive?.Clone());
            _coordinator.SetSessionState(OnlineSessionState.Ready, 1);
        }

        [TearDown]
        public void TearDown()
        {
            _coordinator?.Dispose();
            _archive?.Dispose();
            _client?.Dispose();
            NetworkClient.ResetStaticState();
        }

        [Test]
        public void SettleSuppressesDuplicateTerminalAndSavesMatchingResponseArchive()
        {
            var results = new List<BattleSettlementResult>();

            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), results.Add);
            var firstRequest = DecodeLastCombatRequest();
            _coordinator.Settle(BattleRunOutcome.Defeat, ResultData(), results.Add);

            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(firstRequest.RunId, Is.Not.Empty);

            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = firstRequest.RunId,
                RewardGold = 13,
                RewardExp = 21,
                Archive = new PlayerArchive { Gold = 34, TalentPoints = 5 }
            }));

            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.SaveArchiveReq));
            var save = DecodeLastSaveRequest();
            Assert.That(save.Archive.Gold, Is.EqualTo(34));
            Assert.That(save.Archive.TalentPoints, Is.EqualTo(5));
            Assert.That(_appliedArchive, Is.Null,
                "Progress must not change before the archive save acknowledgement.");

            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(BattleSettlementState.Saved));
            Assert.That(results[0].RewardGold, Is.EqualTo(13));
            Assert.That(results[0].RewardExp, Is.EqualTo(21));
            Assert.That(_appliedArchive?.Gold, Is.EqualTo(34));
            Assert.That(_appliedArchive?.TalentPoints, Is.EqualTo(5));
        }

        [Test]
        public void ResponseWithOldNewOrLateRunIdIsIgnoredUntilTheActiveRunMatches()
        {
            var results = new List<BattleSettlementResult>();
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), results.Add);
            var active = DecodeLastCombatRequest();
            Assert.That(Codec.TryDecode(
                _transport.SentPayloads.Last(), out _, out var activeSeq, out _), Is.True);

            var mismatchedRunIds = new[] { "old-run", "new-run", "late-run" };
            for (var index = 0; index < mismatchedRunIds.Length; index++)
            {
                LogAssert.Expect(LogType.Warning, new Regex("unknown seq"));
                _client.ReceiveFrame(Codec.Encode(
                    MsgID.CombatResultResp, activeSeq + (uint)index + 1, new CombatResultResp
                {
                    Success = true,
                    RunId = mismatchedRunIds[index],
                    Archive = new PlayerArchive { Gold = 99 }
                }));
            }

            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(results, Is.Empty);
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.CombatResultReq));

            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = active.RunId,
                Archive = new PlayerArchive { Gold = 7 }
            }));

            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.SaveArchiveReq));
        }

        [Test]
        public void SaveFailureRetriesOnlyArchiveSaveWithoutResendingCombat()
        {
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });
            var request = DecodeLastCombatRequest();
            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 8 }
            }));
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = false }));

            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_coordinator.Retry(), Is.True);

            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(DecodeLastMessageId(), Is.EqualTo(MsgID.SaveArchiveReq));
        }

        [Test]
        public void ReadyAfterReconnectResendsSameActiveRunOnlyWhileResponseIsMissing()
        {
            _coordinator.Settle(BattleRunOutcome.Defeat, ResultData(), _ => { });
            var first = DecodeLastCombatRequest();

            _coordinator.SetSessionState(OnlineSessionState.Reconnecting, 2);
            _coordinator.SetSessionState(OnlineSessionState.Ready, 2);
            var resent = DecodeLastCombatRequest();

            Assert.That(CombatRequestCount(), Is.EqualTo(2));
            Assert.That(resent.RunId, Is.EqualTo(first.RunId));
        }

        [Test]
        public void DuplicateResponseAndLateFramesAfterDisposeAreIgnored()
        {
            var results = new List<BattleSettlementResult>();
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), results.Add);
            var response = new CombatResultResp
            {
                Success = true,
                Duplicate = true,
                RunId = DecodeLastCombatRequest().RunId,
                Archive = new PlayerArchive { Gold = 8 }
            };

            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, response));
            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, response));
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));
            _coordinator.Dispose();
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Duplicate, Is.True);
        }

        [Test]
        public void OfflineGatewayCompletesImmediately()
        {
            var gateway = new OfflineBattleSettlementGateway();
            BattleSettlementResult result = default;

            gateway.Settle(BattleRunOutcome.Victory, ResultData(), value => result = value);

            Assert.That(result.State, Is.EqualTo(BattleSettlementState.Saved));
        }

        [Test]
        public void TerminalDuringReconnectQueuesUntilReadyAndKeepsOneRunId()
        {
            _coordinator.SetSessionState(OnlineSessionState.Reconnecting, 2);

            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });

            Assert.That(CombatRequestCount(), Is.Zero);
            var runId = _coordinator.ActiveRunId;
            Assert.That(runId, Is.Not.Empty);

            _coordinator.SetSessionState(OnlineSessionState.Ready, 2);

            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(DecodeLastCombatRequest().RunId, Is.EqualTo(runId));
        }

        [Test]
        public void TerminalAfterSessionFailureFailsImmediatelyWithoutLosingRunId()
        {
            var results = new List<BattleSettlementResult>();
            _coordinator.SetSessionState(OnlineSessionState.Failed, 2);

            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), results.Add);

            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_coordinator.ActiveRunId, Is.Not.Empty);
            Assert.That(CombatRequestCount(), Is.Zero);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(results[0].RewardGold, Is.Zero);
            Assert.That(results[0].RewardExp, Is.Zero);
        }

        [Test]
        public void RetryAfterSessionFailureRecoversThenSendsSameRunWhenReady()
        {
            var recoverCalls = 0;
            RecreateCoordinator(() =>
            {
                recoverCalls++;
                _coordinator.SetSessionState(OnlineSessionState.Connecting, 3);
                return true;
            });
            _coordinator.SetSessionState(OnlineSessionState.Failed, 2);
            _coordinator.Settle(BattleRunOutcome.Defeat, ResultData(), _ => { });
            var runId = _coordinator.ActiveRunId;

            Assert.That(_coordinator.Retry(), Is.True);

            Assert.That(recoverCalls, Is.EqualTo(1));
            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Pending));
            Assert.That(CombatRequestCount(), Is.Zero);
            _coordinator.SetSessionState(OnlineSessionState.Ready, 3);
            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(DecodeLastCombatRequest().RunId, Is.EqualTo(runId));
        }

        [Test]
        public void RetryPreservesFailureWhenRecoveryIsRejectedOrSessionIsStopped()
        {
            var recoverCalls = 0;
            RecreateCoordinator(() =>
            {
                recoverCalls++;
                return false;
            });
            _coordinator.SetSessionState(OnlineSessionState.Failed, 2);
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });

            Assert.That(_coordinator.Retry(), Is.False);
            Assert.That(recoverCalls, Is.EqualTo(1));
            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));

            _coordinator.SetSessionState(OnlineSessionState.Stopped, 3);
            Assert.That(_coordinator.Retry(), Is.False);
            Assert.That(recoverCalls, Is.EqualTo(1));
            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));
        }

        [Test]
        public void RetryAfterSaveStageSessionFailureWaitsForReadyThenResendsOnlyArchive()
        {
            RecreateCoordinator(() =>
            {
                _coordinator.SetSessionState(OnlineSessionState.Connecting, 3);
                return true;
            });
            _coordinator.SetSessionState(OnlineSessionState.Ready, 1);
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });
            var request = DecodeLastCombatRequest();
            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 17 }
            }));
            Assert.That(SaveRequestCount(), Is.EqualTo(1));

            _coordinator.SetSessionState(OnlineSessionState.Failed, 2);
            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_coordinator.Retry(), Is.True);
            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(SaveRequestCount(), Is.EqualTo(1));

            _coordinator.SetSessionState(OnlineSessionState.Ready, 3);

            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(SaveRequestCount(), Is.EqualTo(2));
            Assert.That(_coordinator.ActiveRunId, Is.EqualTo(request.RunId));
        }

        [Test]
        public void CombatFailureRetryResendsTheSameRunInsteadOfCreatingAnother()
        {
            _coordinator.Settle(BattleRunOutcome.Defeat, ResultData(), _ => { });
            var first = DecodeLastCombatRequest();
            _client.ReceiveFrame(EncodeResponse(MsgID.Error, new ErrorResp { Code = 4001, Msg = "failed" }));

            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_coordinator.Retry(), Is.True);

            Assert.That(CombatRequestCount(), Is.EqualTo(2));
            Assert.That(DecodeLastCombatRequest().RunId, Is.EqualTo(first.RunId));
        }

        [Test]
        public void RetryCancelsOldSeqReusesRunIdAndIgnoresLateOldSuccessOrError()
        {
            RecreateCoordinator(() =>
            {
                _coordinator.SetSessionState(OnlineSessionState.Connecting, 3);
                _coordinator.SetSessionState(OnlineSessionState.Ready, 3);
                return true;
            });
            _coordinator.SetSessionState(OnlineSessionState.Ready, 1);
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });
            Assert.That(Codec.TryDecode(
                _transport.SentPayloads.Single(), out _, out var firstSeq, out var firstBody), Is.True);
            var firstRequest = CombatResultReq.Parser.ParseFrom(firstBody);

            _coordinator.SetSessionState(OnlineSessionState.Failed, 2);
            Assert.That(_coordinator.Retry(), Is.True);
            Assert.That(Codec.TryDecode(
                _transport.SentPayloads.Last(), out _, out var secondSeq, out var secondBody), Is.True);
            var secondRequest = CombatResultReq.Parser.ParseFrom(secondBody);
            Assert.That(firstSeq, Is.Not.Zero);
            Assert.That(secondSeq, Is.Not.Zero.And.Not.EqualTo(firstSeq));
            Assert.That(secondRequest.RunId, Is.EqualTo(firstRequest.RunId));

            LogAssert.Expect(LogType.Warning, new Regex("unknown seq"));
            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, firstSeq,
                SuccessfulCombatResponse(firstRequest.RunId)));
            LogAssert.Expect(LogType.Warning, new Regex("unknown seq"));
            _client.ReceiveFrame(Codec.Encode(MsgID.Error, firstSeq,
                new ErrorResp { Code = 4001, Msg = "late failure" }));
            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Pending));
            Assert.That(SaveRequestCount(), Is.Zero);

            _client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, secondSeq,
                SuccessfulCombatResponse(secondRequest.RunId)));
            Assert.That(SaveRequestCount(), Is.EqualTo(1));
        }

        [Test]
        public void SavedRunReleasesTheCoordinatorForTheNextBattle()
        {
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });
            var first = DecodeLastCombatRequest();
            CompleteSettlement(first.RunId);

            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });
            var second = DecodeLastCombatRequest();

            Assert.That(CombatRequestCount(), Is.EqualTo(2));
            Assert.That(second.RunId, Is.Not.EqualTo(first.RunId));
        }

        [Test]
        public void ReconnectDuringArchiveSaveFailsThenRetrySendsOnlyTheArchiveAgain()
        {
            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), _ => { });
            var request = DecodeLastCombatRequest();
            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = request.RunId,
                Archive = new PlayerArchive { Gold = 12 }
            }));

            _coordinator.SetSessionState(OnlineSessionState.Reconnecting, 2);
            _coordinator.SetSessionState(OnlineSessionState.Ready, 2);

            Assert.That(_coordinator.State, Is.EqualTo(BattleSettlementState.Failed));
            Assert.That(_coordinator.Retry(), Is.True);
            Assert.That(CombatRequestCount(), Is.EqualTo(1));
            Assert.That(SaveRequestCount(), Is.EqualTo(2));
        }

        [Test]
        public void ThrowingArchiveApplyStillCompletesAndReleasesCoordinatorForNextRun()
        {
            _coordinator.Dispose();
            _coordinator = new BattleSettlementCoordinator(
                _client,
                _archive,
                _ => throw new InvalidOperationException("apply failed"));
            _coordinator.SetSessionState(OnlineSessionState.Ready, 1);
            BattleSettlementResult result = null;
            LogAssert.Expect(LogType.Exception, new Regex("apply failed"));

            _coordinator.Settle(BattleRunOutcome.Victory, ResultData(), value => result = value);
            var first = DecodeLastCombatRequest();
            CompleteSettlement(first.RunId);

            Assert.That(result?.State, Is.EqualTo(BattleSettlementState.Saved));
            Assert.That(_coordinator.ActiveRunId, Is.Null);
            _coordinator.Settle(BattleRunOutcome.Defeat, ResultData(), _ => { });
            var second = DecodeLastCombatRequest();
            Assert.That(second.RunId, Is.Not.EqualTo(first.RunId));
            Assert.That(CombatRequestCount(), Is.EqualTo(2));
        }

        private CombatResultReq DecodeLastCombatRequest()
        {
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var messageId, out _, out var body), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.CombatResultReq));
            return CombatResultReq.Parser.ParseFrom(body);
        }

        private SaveArchiveReq DecodeLastSaveRequest()
        {
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var messageId, out _, out var body), Is.True);
            Assert.That(messageId, Is.EqualTo(MsgID.SaveArchiveReq));
            return SaveArchiveReq.Parser.ParseFrom(body);
        }

        private ushort DecodeLastMessageId()
        {
            Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out var messageId, out _, out _), Is.True);
            return messageId;
        }

        private int CombatRequestCount()
        {
            return _transport.SentPayloads.Count(frame =>
            {
                Codec.TryDecode(frame, out var messageId, out _, out _);
                return messageId == MsgID.CombatResultReq;
            });
        }

        private int SaveRequestCount()
        {
            return _transport.SentPayloads.Count(frame =>
            {
                Codec.TryDecode(frame, out var messageId, out _, out _);
                return messageId == MsgID.SaveArchiveReq;
            });
        }

        private void CompleteSettlement(string runId)
        {
            _client.ReceiveFrame(EncodeResponse(MsgID.CombatResultResp, new CombatResultResp
            {
                Success = true,
                RunId = runId,
                Archive = new PlayerArchive()
            }));
            _client.ReceiveFrame(EncodeResponse(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));
        }

        private static CombatResultResp SuccessfulCombatResponse(string runId)
        {
            return new CombatResultResp
            {
                Success = true,
                RunId = runId,
                Archive = new PlayerArchive { Gold = 12 }
            };
        }

        private byte[] EncodeResponse<T>(ushort responseId, T response)
            where T : class, Google.Protobuf.IMessage<T>
        {
            var requestId = responseId == MsgID.CombatResultResp
                ? MsgID.CombatResultReq
                : responseId == MsgID.SaveArchiveResp
                    ? MsgID.SaveArchiveReq
                    : LastRequestId();
            for (var index = _transport.SentPayloads.Count - 1; index >= 0; index--)
            {
                Assert.That(Codec.TryDecode(
                    _transport.SentPayloads[index], out var messageId, out var seq, out _), Is.True);
                if (messageId == requestId)
                {
                    return Codec.Encode(responseId, seq, response);
                }
            }

            throw new InvalidOperationException($"No request found for response {responseId}.");
        }

        private ushort LastRequestId()
        {
            Assert.That(_transport.SentPayloads, Is.Not.Empty);
            Assert.That(Codec.TryDecode(
                _transport.SentPayloads.Last(), out var requestId, out _, out _), Is.True);
            return requestId;
        }

        private void RecreateCoordinator(Func<bool> recoverSession)
        {
            _coordinator.Dispose();
            _coordinator = new BattleSettlementCoordinator(
                _client,
                _archive,
                archive => _appliedArchive = archive?.Clone(),
                recoverSession);
        }

        private static CombatResultData ResultData()
        {
            return new CombatResultData
            {
                killCount = 3,
                playerLevel = 2,
                survivalTime = 12
            };
        }
    }
}
