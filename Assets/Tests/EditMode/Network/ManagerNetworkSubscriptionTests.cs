using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Network;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Network
{
    public sealed class ManagerNetworkSubscriptionTests
    {
        private int _callbacks;

        [TearDown]
        public void TearDown()
        {
            DestroyNamedManagerObject("login-test");
            DestroyNamedManagerObject("archive-test");
            DestroyNamedManagerObject("rank-test");
            DestroyNamedManagerObject("combat-test");
            NetworkClient.ResetStaticState();
            _callbacks = 0;
        }

        [Test]
        public void ManagerRequestsUseNonZeroCorrelation()
        {
            var client = CreateConnectedClient(out var transport);
            NetworkClient.RegisterInstance(client);
            client.SetLoginInfo(7, "token");

            var login = CreateManager("login-test", "Game.Managers.LoginManager");
            var archive = CreateManager("archive-test", "Game.Managers.ArchiveManager");
            var rank = CreateManager("rank-test", "Game.Managers.RankManager");
            var combat = CreateManager("combat-test", "CombatManager");
            AddCallback(login, "OnLoginSuccess");
            AddCallback(archive, "OnLoadSuccess");
            AddCallback(rank, "OnRankLoaded");
            AddCallback(combat, "OnEnemyConfigsLoaded");

            Invoke(login, "SendLoginReq", "manager-code");
            Invoke(archive, "LoadArchive");
            Invoke(rank, "GetRank", 1, 0, 20);
            Invoke(combat, "RequestEnemyConfigs");

            Assert.That(transport.SentPayloads, Has.Count.EqualTo(4));
            var loginSeq = RequireRequestSeq(transport, MsgID.LoginReq);
            var archiveSeq = RequireRequestSeq(transport, MsgID.LoadArchiveReq);
            var rankSeq = RequireRequestSeq(transport, MsgID.GetRankReq);
            var combatSeq = RequireRequestSeq(transport, MsgID.GetEnemyConfigsReq);

            ReceiveUnknownResponse(client, MsgID.LoginResp, loginSeq + 1000, new LoginResp());
            ReceiveUnknownResponse(client, MsgID.LoadArchiveResp, archiveSeq + 1000,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive() });
            ReceiveUnknownResponse(client, MsgID.GetRankResp, rankSeq + 1000, new GetRankResp());
            ReceiveUnknownResponse(client, MsgID.GetEnemyConfigsResp, combatSeq + 1000,
                new GetEnemyConfigsResp());
            Assert.That(_callbacks, Is.Zero, "unrelated sequences must not complete manager requests");

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, loginSeq, new LoginResp { Uid = 7 }));
            client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, archiveSeq,
                new LoadArchiveResp { Found = true, Archive = new PlayerArchive() }));
            client.ReceiveFrame(Codec.Encode(MsgID.GetRankResp, rankSeq, new GetRankResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetEnemyConfigsResp, combatSeq,
                new GetEnemyConfigsResp()));
            Assert.That(_callbacks, Is.EqualTo(4));
        }

        [Test]
        public void CombatManagerCanUpdatePlayerStats()
        {
            var client = CreateConnectedClient(out var transport);
            NetworkClient.RegisterInstance(client);
            var combat = CreateManager("combat-test", "CombatManager");
            AddCallback(combat, "OnError");
            var stats = new PlayerStatsData
            {
                Level = 8,
                Exp = 90,
                Gold = 123,
                MaxHp = 456,
                MaxStamina = 78,
                AttackPower = 34
            };
            stats.UnlockedStyles.Add(new[] { 2, 5 });

            Invoke(combat, "UpdatePlayerStats", stats);

            Assert.That(transport.SentPayloads, Has.Count.EqualTo(1));
            Assert.That(Codec.TryDecode(transport.SentPayloads.Single(), out var id, out var seq, out var body),
                Is.True);
            Assert.That(id, Is.EqualTo(MsgID.UpdatePlayerStatsReq));
            Assert.That(seq, Is.Not.Zero);
            var request = UpdatePlayerStatsReq.Parser.ParseFrom(body);
            Assert.That(request.Level, Is.EqualTo(stats.Level));
            Assert.That(request.Exp, Is.EqualTo(stats.Exp));
            Assert.That(request.Gold, Is.EqualTo(stats.Gold));
            Assert.That(request.MaxHp, Is.EqualTo(stats.MaxHp));
            Assert.That(request.MaxStamina, Is.EqualTo(stats.MaxStamina));
            Assert.That(request.AttackPower, Is.EqualTo(stats.AttackPower));
            CollectionAssert.AreEqual(stats.UnlockedStyles, request.UnlockedStyles);

            ReceiveUnknownResponse(client, MsgID.UpdatePlayerStatsResp, seq + 1,
                new UpdatePlayerStatsResp { Success = false });
            Assert.That(_callbacks, Is.Zero);
            client.ReceiveFrame(Codec.Encode(MsgID.UpdatePlayerStatsResp, seq,
                new UpdatePlayerStatsResp { Success = false }));
            Assert.That(_callbacks, Is.EqualTo(1));
        }

        [Test]
        public void ManagerDestroyedDuringSendCancelsTheReentrantPendingRequest()
        {
            var client = CreateConnectedClient(out var transport);
            NetworkClient.RegisterInstance(client);
            var login = CreateManager("login-test", "Game.Managers.LoginManager");
            AddCallback(login, "OnLoginSuccess");
            AddCallback(login, "OnLoginFailed");
            transport.SendAction = _ => DestroyManager(login);

            Invoke(login, "SendLoginReq", "destroy-during-send");

            var seq = RequireRequestSeq(transport, MsgID.LoginReq);
            Assert.That(client.CancelRequest(seq), Is.False,
                "destroy-during-send must not add the returned sequence after OnDestroy");
            Assert.That(_callbacks, Is.Zero);
            ReceiveUnknownResponse(client, MsgID.LoginResp, seq, new LoginResp { Uid = 99 });
            ReceiveUnknownResponse(client, MsgID.Error, seq,
                new ErrorResp { Code = 500, Msg = "late" });
            Assert.That(client.IsLoggedIn, Is.False);
            Assert.That(_callbacks, Is.Zero);
        }

        [Test]
        public void ConcurrentRankRequestsCompleteByTheirOwnSequenceOutOfOrder()
        {
            var client = CreateConnectedClient(out var transport);
            NetworkClient.RegisterInstance(client);
            var rank = CreateManager("rank-test", "Game.Managers.RankManager");
            var arrivals = new System.Collections.Generic.List<string>();
            rank.GetType().GetEvent("OnRankLoaded").AddEventHandler(
                rank,
                new Action<RankItem[]>(items => arrivals.Add(items.Single().Nickname)));

            Invoke(rank, "GetRank", 1, 0, 1);
            Invoke(rank, "GetRank", 1, 1, 1);

            var sequences = transport.SentPayloads.Select(frame =>
            {
                Assert.That(Codec.TryDecode(frame, out var id, out var seq, out _), Is.True);
                Assert.That(id, Is.EqualTo(MsgID.GetRankReq));
                Assert.That(seq, Is.Not.Zero);
                return seq;
            }).ToArray();
            Assert.That(sequences[0], Is.Not.EqualTo(sequences[1]));

            client.ReceiveFrame(Codec.Encode(MsgID.GetRankResp, sequences[1], RankResponse("second")));
            client.ReceiveFrame(Codec.Encode(MsgID.GetRankResp, sequences[0], RankResponse("first")));
            Assert.That(arrivals, Is.EqualTo(new[] { "second", "first" }));
        }

        [Test]
        public void DestroyedManagersReleaseAllSubscriptionsAndClearSingletons()
        {
            var client = CreateConnectedClient(out var transport);
            NetworkClient.RegisterInstance(client);
            var loginType = RequireType("Game.Managers.LoginManager");
            var archiveType = RequireType("Game.Managers.ArchiveManager");
            var rankType = RequireType("Game.Managers.RankManager");
            var combatType = RequireType("CombatManager");
            var login = CreateManager("login-test", "Game.Managers.LoginManager");
            var archive = CreateManager("archive-test", "Game.Managers.ArchiveManager");
            var rank = CreateManager("rank-test", "Game.Managers.RankManager");
            var combat = CreateManager("combat-test", "CombatManager");
            var lateLoginSuccesses = 0;
            var lateLoginFailures = 0;

            AddCallback(login, "OnLoginSuccess");
            login.GetType().GetEvent("OnLoginSuccess").AddEventHandler(
                login, new Action<LoginResp>(_ => lateLoginSuccesses++));
            login.GetType().GetEvent("OnLoginFailed").AddEventHandler(
                login, new Action<string>(_ => lateLoginFailures++));
            AddCallback(archive, "OnSaveSuccess");
            AddCallback(archive, "OnLoadSuccess");
            AddCallback(rank, "OnRankLoaded");
            AddCallback(rank, "OnScoreSubmitted");
            AddCallback(combat, "OnEnemyConfigsLoaded");
            AddCallback(combat, "OnDungeonConfigLoaded");
            AddCallback(combat, "OnStyleConfigsLoaded");
            AddCallback(combat, "OnStyleUnlocked");
            AddCallback(combat, "OnPlayerStatsLoaded");
            AddCallback(combat, "OnError");

            Invoke(login, "SendLoginReq", "destroy-before-response");
            var loginSeq = RequireRequestSeq(transport, MsgID.LoginReq);
            Assert.That(_callbacks, Is.Zero, "sending must not publish a manager event");
            Assert.That(PendingRequestCount(login), Is.EqualTo(1),
                "the manager must own the pending sequence before destruction");

            DestroyManager(login);
            Assert.That(_callbacks, Is.Zero, "destroy cancellation must not publish a login failure");
            AssertSingletonCleared(loginType, "_instance");
            Assert.That(PendingRequestCount(login), Is.Zero,
                "OnDestroy must clear the manager-owned sequence set");
            DestroyManager(archive);
            DestroyManager(rank);
            DestroyManager(combat);
            Assert.That(_callbacks, Is.Zero, "destroying idle managers must not publish events");
            Assert.That(client.CancelRequest(loginSeq), Is.False,
                "OnDestroy must remove the manager-owned pending request");

            ReceiveUnknownResponse(client, MsgID.LoginResp, loginSeq,
                new LoginResp { Uid = 44, Token = "late" });
            Assert.That(lateLoginSuccesses, Is.Zero, "late success reached OnLoginSuccess");
            Assert.That(lateLoginFailures, Is.Zero, "destroy cancellation reached OnLoginFailed");
            Assert.That(_callbacks, Is.Zero, "late success must not publish manager events");
            ReceiveUnknownResponse(client, MsgID.Error, loginSeq,
                new ErrorResp { Code = 500, Msg = "late" });
            Assert.That(_callbacks, Is.Zero, "late pending responses must not publish manager events");

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, 0, new LoginResp { Uid = 1, Token = "x" }));
            client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, 0, new SaveArchiveResp { Success = true }));
            client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, 0, new LoadArchiveResp { Found = true, Archive = new PlayerArchive() }));
            client.ReceiveFrame(Codec.Encode(MsgID.GetRankResp, 0, new GetRankResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.SubmitScoreResp, 0, new SubmitScoreResp { Success = true, BestScore = 8 }));
            client.ReceiveFrame(Codec.Encode(MsgID.GetEnemyConfigsResp, 0, new GetEnemyConfigsResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetDungeonConfigResp, 0, new GetDungeonConfigResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetStyleConfigsResp, 0, new GetStyleConfigsResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.UnlockStyleResp, 0, new UnlockStyleResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetPlayerStatsResp, 0, new GetPlayerStatsResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.UpdatePlayerStatsResp, 0, new UpdatePlayerStatsResp { Success = false }));

            Assert.That(_callbacks, Is.Zero, "no destroyed manager callback may remain registered");
            Assert.That(client.IsLoggedIn, Is.False, "a destroyed login manager must not mutate login state");
            AssertSingletonCleared(loginType, "_instance");
            AssertSingletonCleared(archiveType, "_instance");
            AssertSingletonCleared(rankType, "_instance");
            AssertSingletonCleared(combatType, "_instance");
        }

        private static Type RequireType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(item => item != null);
            Assert.That(type, Is.Not.Null, $"{fullName} must be loaded.");
            return type;
        }

        private static Component CreateManager(string objectName, string typeName)
        {
            return new GameObject(objectName).AddComponent(RequireType(typeName));
        }

        private static void DestroyManager(Component component)
        {
            InvokeLifecycle(component, "OnDestroy");
            Object.DestroyImmediate(component.gameObject);
        }

        private static void InvokeLifecycle(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, null);
        }

        private static NetworkClient CreateConnectedClient(out FakeWebSocketTransport transport)
        {
            transport = new FakeWebSocketTransport();
            transport.RaiseOpened();
            var client = new NetworkClient();
            client.SetTransport(transport);
            return client;
        }

        private static uint RequireRequestSeq(FakeWebSocketTransport transport, ushort requestId)
        {
            foreach (var frame in transport.SentPayloads)
            {
                Assert.That(Codec.TryDecode(frame, out var id, out var seq, out _), Is.True);
                if (id == requestId)
                {
                    Assert.That(seq, Is.Not.Zero);
                    return seq;
                }
            }

            Assert.Fail($"No request frame found for msgId={requestId}.");
            return 0;
        }

        private static GetRankResp RankResponse(string nickname)
        {
            var response = new GetRankResp();
            response.Ranks.Add(new RankItem { Nickname = nickname });
            return response;
        }

        private static int PendingRequestCount(Component component)
        {
            var field = component.GetType().GetField("_pendingRequests", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var count = field.FieldType.GetProperty("Count").GetValue(field.GetValue(component));
            return (int)count;
        }

        private static void ReceiveUnknownResponse<T>(
            NetworkClient client,
            ushort responseId,
            uint seq,
            T response)
            where T : class, Google.Protobuf.IMessage<T>
        {
            LogAssert.Expect(LogType.Warning, new Regex($"Dropped response for unknown seq={seq}"));
            client.ReceiveFrame(Codec.Encode(responseId, seq, response));
        }

        private static void Invoke(Component component, string methodName, params object[] arguments)
        {
            var method = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(candidate => candidate.Name == methodName &&
                                     candidate.GetParameters().Length == arguments.Length);
            method.Invoke(component, arguments);
        }

        private static void AssertSingletonCleared(Type type, string fieldName) =>
            Assert.That(type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);

        private void AddCallback(Component component, string eventName)
        {
            var eventInfo = component.GetType().GetEvent(eventName);
            Assert.That(eventInfo, Is.Not.Null, $"{component.GetType().Name}.{eventName} must exist.");
            MethodInfo callbackMethod;
            if (eventInfo.EventHandlerType == typeof(Action))
            {
                callbackMethod = GetType().GetMethod(nameof(Increment), BindingFlags.Instance | BindingFlags.NonPublic);
            }
            else
            {
                var argumentType = eventInfo.EventHandlerType
                    .GetMethod("Invoke")
                    .GetParameters()
                    .Single()
                    .ParameterType;
                callbackMethod = GetType()
                    .GetMethod(nameof(IncrementGeneric), BindingFlags.Instance | BindingFlags.NonPublic)
                    .MakeGenericMethod(argumentType);
            }

            eventInfo.AddEventHandler(component, Delegate.CreateDelegate(eventInfo.EventHandlerType, this, callbackMethod));
        }

        private void Increment()
        {
            _callbacks++;
        }

        private void IncrementGeneric<T>(T _)
        {
            _callbacks++;
        }

        private static void DestroyNamedManagerObject(string objectName)
        {
            foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.name == objectName)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
