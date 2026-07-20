using System;
using System.Linq;
using System.Reflection;
using Game.Network;
using Game.Protocol;
using NUnit.Framework;
using UnityEngine;
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
        }

        [Test]
        public void DestroyedManagersReleaseAllTwelveSubscriptionsAndClearSingletons()
        {
            var client = new NetworkClient();
            NetworkClient.RegisterInstance(client);
            var loginType = RequireType("Game.Managers.LoginManager");
            var archiveType = RequireType("Game.Managers.ArchiveManager");
            var rankType = RequireType("Game.Managers.RankManager");
            var combatType = RequireType("CombatManager");
            var login = new GameObject("login-test").AddComponent(loginType);
            var archive = new GameObject("archive-test").AddComponent(archiveType);
            var rank = new GameObject("rank-test").AddComponent(rankType);
            var combat = new GameObject("combat-test").AddComponent(combatType);

            AddCallback(login, "OnLoginSuccess");
            AddCallback(archive, "OnSaveSuccess");
            AddCallback(archive, "OnLoadSuccess");
            AddCallback(rank, "OnRankLoaded");
            AddCallback(rank, "OnScoreSubmitted");
            AddCallback(combat, "OnCombatResult");
            AddCallback(combat, "OnEnemyConfigsLoaded");
            AddCallback(combat, "OnDungeonConfigLoaded");
            AddCallback(combat, "OnStyleConfigsLoaded");
            AddCallback(combat, "OnStyleUnlocked");
            AddCallback(combat, "OnPlayerStatsLoaded");
            AddCallback(combat, "OnError");

            Object.DestroyImmediate(login.gameObject);
            Object.DestroyImmediate(archive.gameObject);
            Object.DestroyImmediate(rank.gameObject);
            Object.DestroyImmediate(combat.gameObject);

            client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, new LoginResp { Uid = 1, Token = "x" }));
            client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));
            client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, new LoadArchiveResp { Found = true, Archive = new PlayerArchive() }));
            client.ReceiveFrame(Codec.Encode(MsgID.GetRankResp, new GetRankResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.SubmitScoreResp, new SubmitScoreResp { Success = true, BestScore = 8 }));
            client.ReceiveFrame(Codec.Encode(MsgID.CombatResultResp, new CombatResultResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetEnemyConfigsResp, new GetEnemyConfigsResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetDungeonConfigResp, new GetDungeonConfigResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetStyleConfigsResp, new GetStyleConfigsResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.UnlockStyleResp, new UnlockStyleResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.GetPlayerStatsResp, new GetPlayerStatsResp()));
            client.ReceiveFrame(Codec.Encode(MsgID.UpdatePlayerStatsResp, new UpdatePlayerStatsResp { Success = false }));

            Assert.That(_callbacks, Is.Zero, "no destroyed manager callback may remain registered");
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
