using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 验证真实战斗场景可以在不启动联网流程的情况下完成最小初始化。
    /// </summary>
    public sealed class BattleSceneOfflineSmokeTests
    {
        /// <summary>
        /// 加载 Build Settings 中的战斗场景并检查核心对象。
        /// </summary>
        /// <returns>等待场景和延迟一帧初始化完成的枚举器。</returns>
        [UnityTest]
        public IEnumerator BattleSceneStartsOfflineAndCreatesCoreObjects()
        {
            var failures = new List<string>();
            void CaptureFailure(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    failures.Add($"{type}: {condition}\n{stackTrace}");
                }
            }

            Application.logMessageReceived += CaptureFailure;
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return WaitForApplicationReady();

                Assert.That(failures, Is.Empty, string.Join("\n\n", failures));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"),
                    "Offline startup must leave BattleScene active.");
                Assert.That(GetApplicationState(), Is.EqualTo("Ready"),
                    "GameApplication must be Ready before BattleScene is exercised.");
                Assert.That(GameObject.Find("Ground"), Is.Not.Null, "战斗场景必须创建地面");
                Assert.That(GameObject.Find("Player"), Is.Not.Null, "战斗场景必须创建玩家");
                Assert.That(GameObject.Find("WaveSpawner"), Is.Not.Null, "战斗场景必须创建刷怪器");
                Assert.That(GameObject.Find("[BattleHUD]"), Is.Not.Null, "战斗场景必须创建战斗 HUD");
                Assert.That(GameObject.Find("[NetworkClient]"), Is.Null, "离线场景不得创建网络客户端");
                Assert.That(GameObject.Find("[LoginManager]"), Is.Null, "离线场景不得启动登录流程");
                Assert.That(GameObject.Find("[GameBootstrap]"), Is.Null, "离线场景不得启动在线 Bootstrap");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                Application.logMessageReceived -= CaptureFailure;
            }
        }

        [UnityTest]
        public IEnumerator BattleSceneReloadPreservesApplicationAndServiceOwners()
        {
            var failures = new List<string>();
            void CaptureFailure(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    failures.Add($"{type}: {condition}\n{stackTrace}");
                }
            }

            Application.logMessageReceived += CaptureFailure;
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return WaitForApplicationReady();

                var application = GameObject.Find("[GameApplication]");
                var services = GameObject.Find("[GameServices]");
                var player = GameObject.Find("Player");
                Assert.That(application, Is.Not.Null);
                Assert.That(services, Is.Not.Null);
                Assert.That(player, Is.Not.Null);

                var applicationId = application.GetInstanceID();
                var servicesId = services.GetInstanceID();
                var playerId = player.GetInstanceID();
                var serviceNames = new[]
                {
                    "[MainThreadDispatcher]",
                    "[SceneTransitionManager]",
                    "[AudioManager]",
                    "[LoadingScreen]",
                    "[AchievementManager]"
                };
                var serviceIds = new Dictionary<string, int>();
                foreach (var serviceName in serviceNames)
                {
                    var serviceObject = GameObject.Find(serviceName);
                    Assert.That(serviceObject, Is.Not.Null, $"{serviceName} must be installed before reload.");
                    serviceIds.Add(serviceName, serviceObject.GetInstanceID());
                }

                var setupType = GetApplicationComponent(application).GetType().Assembly.GetType("BattleSceneSetup");
                var combatEventsType = setupType.Assembly.GetType("CombatEvents");
                var inventoryType = setupType.Assembly.GetType("Inventory");
                var inventory = inventoryType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetValue(null);
                var expectedHandlerCounts = new Dictionary<string, int>
                {
                    { "OnHitLanded", 3 },
                    { "OnDamageTaken", 1 },
                    { "OnParrySuccess", 1 },
                    { "OnPlayerDeath", 1 },
                    { "OnEnemyDeath", 1 },
                    { "Inventory.OnItemChanged", 1 }
                };
                var expectedPauseHandlerCounts = new Dictionary<string, int>
                {
                    { "OnBackToMenu", 1 },
                    { "OnSettings", 1 }
                };
                var originalPauseMenus = FindComponents("PauseMenuUI");
                Assert.That(originalPauseMenus.Count, Is.EqualTo(1),
                    "Test Runner scene replacement must leave exactly one current BattleScene pause menu.");
                var originalPauseMenu = originalPauseMenus.Single();
                var originalPauseHandlerCounts = GetPauseMenuHandlerCounts(
                    setupType, originalPauseMenu, expectedPauseHandlerCounts.Keys);
                CollectionAssert.AreEquivalent(expectedPauseHandlerCounts, originalPauseHandlerCounts);
                var originalHandlerCounts = GetBattleSceneHandlerCounts(
                    setupType, combatEventsType, inventory, expectedHandlerCounts.Keys);
                CollectionAssert.AreEquivalent(expectedHandlerCounts, originalHandlerCounts);

                yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return WaitForApplicationReady();

                var reloadedServices = GameObject.Find("[GameServices]");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
                Assert.That(FindAll("[GameApplication]").Count, Is.EqualTo(1));
                Assert.That(FindAll("[GameServices]").Count, Is.EqualTo(1));
                Assert.That(GameObject.Find("[GameApplication]").GetInstanceID(), Is.EqualTo(applicationId));
                Assert.That(reloadedServices.GetInstanceID(), Is.EqualTo(servicesId));
                Assert.That(GameObject.Find("Player").GetInstanceID(), Is.Not.EqualTo(playerId));
                foreach (var serviceName in serviceNames)
                {
                    var serviceObject = GameObject.Find(serviceName);
                    Assert.That(serviceObject, Is.Not.Null, $"{serviceName} must survive BattleScene reload.");
                    Assert.That(FindAll(serviceName).Count, Is.EqualTo(1),
                        $"{serviceName} must remain unique after BattleScene reload.");
                    Assert.That(serviceObject.GetInstanceID(), Is.EqualTo(serviceIds[serviceName]),
                        $"{serviceName} must preserve its installed instance across scene reload.");
                    Assert.That(serviceObject.transform.parent, Is.SameAs(reloadedServices.transform),
                        $"{serviceName} must remain owned by the persistent service root.");

                    var serviceComponent = serviceObject.GetComponents<Component>()
                        .First(component => component != null && component.GetType().Name == serviceName.Trim('[', ']'));
                    var staticOwner = serviceComponent.GetType()
                        .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                        ?.GetValue(null);
                    Assert.That(staticOwner, Is.SameAs(serviceComponent),
                        $"{serviceName}.Instance must remain bound to the surviving owner.");
                }

                foreach (var prohibitedTypeName in new[]
                         {
                             "NetworkClient",
                             "LoginManager",
                             "GameBootstrap",
                             "ArchiveManager",
                             "RankManager",
                             "HeartbeatManager",
                             "ReconnectionManager"
                         })
                {
                    Assert.That(FindComponents(prohibitedTypeName), Is.Empty,
                        $"BattleScene reload must not create {prohibitedTypeName} in Offline mode.");
                }

                var currentSetup = FindComponents("BattleSceneSetup").Single();
                var reloadedHandlerCounts = GetBattleSceneHandlerCounts(
                    setupType, combatEventsType, inventory, expectedHandlerCounts.Keys);
                CollectionAssert.AreEquivalent(originalHandlerCounts, reloadedHandlerCounts,
                    "BattleScene reload must not grow persistent publisher handler counts.");
                var reloadedPauseHandlerCounts = GetPauseMenuHandlerCounts(
                    setupType,
                    FindUniquePauseMenu("BattleScene reload must replace the scene-owned pause menu."),
                    expectedPauseHandlerCounts.Keys);
                CollectionAssert.AreEquivalent(originalPauseHandlerCounts, reloadedPauseHandlerCounts,
                    "BattleScene reload must install handlers only on the current pause menu.");
                AssertCurrentBattleSceneHandlers(combatEventsType, null, setupType, currentSetup,
                    expectedHandlerCounts.Keys.Where(name => !name.StartsWith("Inventory.")));
                AssertCurrentBattleSceneHandlers(inventoryType, inventory, setupType, currentSetup,
                    new[] { "OnItemChanged" });
                var currentPauseMenu = FindUniquePauseMenu(
                    "BattleScene reload must leave exactly one current pause menu.");
                AssertCurrentBattleSceneHandlers(currentPauseMenu.GetType(), currentPauseMenu, setupType, currentSetup,
                    expectedPauseHandlerCounts.Keys);

                var probeInvocations = 0;
                Action<Vector3, int> probe = (position, damage) => probeInvocations++;
                var hitLandedEvent = combatEventsType.GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
                hitLandedEvent.AddEventHandler(null, probe);
                try
                {
                    combatEventsType.GetMethod("InvokeHitLanded", BindingFlags.Static | BindingFlags.Public)
                        .Invoke(null, new object[] { Vector3.zero, 0 });
                    yield return null;
                }
                finally
                {
                    hitLandedEvent.RemoveEventHandler(null, probe);
                }

                Assert.That(probeInvocations, Is.EqualTo(1),
                    "The safe hit signal must dispatch once through the current event list.");
                Assert.That(failures, Is.Empty, string.Join("\n\n", failures));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                Application.logMessageReceived -= CaptureFailure;
            }
        }

        private static IEnumerator WaitForApplicationReady()
        {
            const int maxFrames = 120;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (GetApplicationState() == "Ready")
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("GameApplication did not reach Ready within 120 frames.");
        }

        private static string GetApplicationState()
        {
            var applicationObject = GameObject.Find("[GameApplication]");
            var application = applicationObject == null
                ? null
                : applicationObject.GetComponents<Component>()
                    .FirstOrDefault(component => component != null && component.GetType().Name == "GameApplication");
            return application?.GetType().GetProperty("State")?.GetValue(application)?.ToString();
        }

        private static Component GetApplicationComponent(GameObject applicationObject)
        {
            return applicationObject.GetComponents<Component>()
                .First(component => component != null && component.GetType().Name == "GameApplication");
        }

        private static Dictionary<string, int> GetBattleSceneHandlerCounts(
            Type setupType,
            Type combatEventsType,
            object inventory,
            IEnumerable<string> eventNames)
        {
            return eventNames.ToDictionary(
                eventName => eventName,
                eventName =>
                {
                    var isInventoryEvent = eventName.StartsWith("Inventory.", StringComparison.Ordinal);
                    var publisherType = isInventoryEvent ? inventory.GetType() : combatEventsType;
                    var publisher = isInventoryEvent ? inventory : null;
                    var backingFieldName = isInventoryEvent ? eventName.Substring("Inventory.".Length) : eventName;
                    return GetEventHandlers(publisherType, publisher, backingFieldName)
                        .Count(handler => IsDeclaredBy(setupType, handler));
                });
        }

        private static void AssertCurrentBattleSceneHandlers(
            Type publisherType,
            object publisher,
            Type setupType,
            Component currentSetup,
            IEnumerable<string> eventNames)
        {
            foreach (var eventName in eventNames)
            {
                foreach (var handler in GetEventHandlers(publisherType, publisher, eventName)
                             .Where(handler => IsDeclaredBy(setupType, handler)))
                {
                    AssertLiveSceneReference(handler.Target, currentSetup, eventName, "delegate target");
                    if (handler.Target == null)
                    {
                        continue;
                    }

                    foreach (var field in handler.Target.GetType()
                                 .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                 .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)))
                    {
                        AssertLiveSceneReference(field.GetValue(handler.Target), currentSetup, eventName, field.Name);
                    }
                }
            }
        }

        private static Dictionary<string, int> GetPauseMenuHandlerCounts(
            Type setupType,
            Component pauseMenu,
            IEnumerable<string> eventNames)
        {
            return eventNames.ToDictionary(
                eventName => eventName,
                eventName => GetEventHandlers(pauseMenu.GetType(), pauseMenu, eventName)
                    .Count(handler => IsDeclaredBy(setupType, handler)));
        }

        private static Component FindUniquePauseMenu(string message)
        {
            var pauseMenus = FindComponents("PauseMenuUI");
            Assert.That(pauseMenus.Count, Is.EqualTo(1), message);
            return pauseMenus.Single();
        }

        private static void AssertLiveSceneReference(
            object value,
            Component currentSetup,
            string eventName,
            string referenceName)
        {
            if (!(value is UnityEngine.Object unityObject) || ReferenceEquals(unityObject, null))
            {
                return;
            }

            Assert.That(unityObject != null, Is.True,
                $"{eventName} {referenceName} must not reference a destroyed scene object.");
            if (unityObject.GetType().Name == "BattleSceneSetup")
            {
                Assert.That(unityObject, Is.SameAs(currentSetup),
                    $"{eventName} must target the current BattleSceneSetup.");
            }
        }

        private static List<Delegate> GetEventHandlers(Type publisherType, object publisher, string eventName)
        {
            var bindingFlags = BindingFlags.NonPublic |
                               (publisher == null ? BindingFlags.Static : BindingFlags.Instance);
            var backingDelegate = publisherType.GetField(eventName, bindingFlags)?.GetValue(publisher) as Delegate;
            return backingDelegate?.GetInvocationList().ToList() ?? new List<Delegate>();
        }

        private static bool IsDeclaredBy(Type ownerType, Delegate handler)
        {
            var declaringType = handler.Method.DeclaringType;
            return declaringType == ownerType || declaringType?.DeclaringType == ownerType;
        }

        private static List<GameObject> FindAll(string objectName)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene.IsValid() && item.name == objectName)
                .ToList();
        }

        private static List<Component> FindComponents(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>()
                .Where(item => item != null && item.gameObject.scene.IsValid() && item.GetType().Name == typeName)
                .ToList();
        }
    }
}
