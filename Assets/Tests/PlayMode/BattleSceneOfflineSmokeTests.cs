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
                    { "OnHitResolved", 1 },
                    { "OnHitLanded", 0 },
                    { "OnDamageTaken", 0 },
                    { "OnParrySuccess", 0 },
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

        [UnityTest]
        public IEnumerator FiveBattleReloadsReplacePoolSpawnerEnemiesAndDelegates()
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

                var currentPool = FindUniqueLoadedComponent("ObjectPool");
                var currentSpawner = FindUniqueActiveSceneComponent("WaveSpawner");
                var currentRun = FindUniqueActiveSceneComponent("BattleRunController");
                var currentGameOver = FindUniqueActiveSceneComponent("GameOverUI");
                List<GameObject> currentEnemies = null;
                yield return WaitForSpawnerEnemies(currentSpawner, enemies => currentEnemies = enemies);
                AssertBattleRunSceneOwnership(currentRun, currentGameOver, currentSpawner);
                AssertSingleSceneEventSystem();

                var expectedKeys = new[] { "archer", "boss", "elite", "grunt" };
                for (var iteration = 0; iteration < 5; iteration++)
                {
                    var oldPool = currentPool;
                    var oldSpawner = currentSpawner;
                    var oldRun = currentRun;
                    var oldGameOver = currentGameOver;
                    var oldEnemies = currentEnemies.ToArray();
                    var oldPoolId = oldPool.GetInstanceID();
                    var oldSpawnerId = oldSpawner.GetInstanceID();
                    var oldRunId = oldRun.GetInstanceID();
                    var oldGameOverId = oldGameOver.GetInstanceID();
                    var oldEnemyIds = oldEnemies.Select(enemy => enemy.GetInstanceID()).ToArray();
                    Assert.That(oldEnemies, Is.Not.Empty,
                        $"Reload iteration {iteration + 1} must capture at least one active enemy.");

                    yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
                    yield return null;
                    yield return null;
                    yield return WaitForApplicationReady();

                    Assert.That(oldPool == null, Is.True,
                        $"Reload iteration {iteration + 1} must destroy old ObjectPool {oldPoolId}.");
                    Assert.That(oldSpawner == null, Is.True,
                        $"Reload iteration {iteration + 1} must destroy old WaveSpawner {oldSpawnerId}.");
                    Assert.That(oldRun == null, Is.True,
                        $"Reload iteration {iteration + 1} must destroy old BattleRunController {oldRunId}.");
                    Assert.That(oldGameOver == null, Is.True,
                        $"Reload iteration {iteration + 1} must destroy old GameOverUI {oldGameOverId}.");
                    for (var enemyIndex = 0; enemyIndex < oldEnemies.Length; enemyIndex++)
                    {
                        Assert.That(oldEnemies[enemyIndex] == null, Is.True,
                            $"Reload iteration {iteration + 1} must destroy old enemy {oldEnemyIds[enemyIndex]}.");
                    }

                    var activeScene = SceneManager.GetActiveScene();
                    currentPool = FindUniqueActiveSceneComponent("ObjectPool");
                    currentSpawner = FindUniqueActiveSceneComponent("WaveSpawner");
                    currentRun = FindUniqueActiveSceneComponent("BattleRunController");
                    currentGameOver = FindUniqueActiveSceneComponent("GameOverUI");
                    Assert.That(FindComponents("ObjectPool"), Has.Count.EqualTo(1));
                    Assert.That(FindComponents("WaveSpawner"), Has.Count.EqualTo(1));
                    Assert.That(FindComponents("BattleRunController"), Has.Count.EqualTo(1));
                    Assert.That(FindComponents("GameOverUI"), Has.Count.EqualTo(1));
                    Assert.That(currentPool.GetInstanceID(), Is.Not.EqualTo(oldPoolId));
                    Assert.That(currentSpawner.GetInstanceID(), Is.Not.EqualTo(oldSpawnerId));
                    Assert.That(currentRun.GetInstanceID(), Is.Not.EqualTo(oldRunId));
                    Assert.That(currentGameOver.GetInstanceID(), Is.Not.EqualTo(oldGameOverId));

                    List<GameObject> nextEnemies = null;
                    yield return WaitForSpawnerEnemies(currentSpawner, enemies => nextEnemies = enemies);
                    currentEnemies = nextEnemies;
                    Assert.That(currentEnemies, Is.Not.Empty);
                    AssertPoolBelongsToScene(currentPool, activeScene);
                    Assert.That(
                        currentEnemies.All(enemy => enemy != null
                                                    && enemy.scene == activeScene
                                                    && enemy.activeInHierarchy),
                        Is.True,
                        "Checked-out enemies must belong to the current BattleScene.");

                    CollectionAssert.AreEquivalent(expectedKeys, GetDictionaryKeys(currentPool, "_factories"));
                    CollectionAssert.AreEquivalent(
                        expectedKeys,
                        GetEnumerableFieldValues(currentSpawner, "_registeredPoolKeys").Cast<string>());
                    AssertFactoryOwners(currentPool, currentSpawner);
                    AssertEnemyDeathOwners(currentEnemies, currentSpawner);
                    AssertBattleRunSceneOwnership(currentRun, currentGameOver, currentSpawner);
                    AssertSingleSceneEventSystem();
                    Assert.That(failures, Is.Empty, string.Join("\n\n", failures));
                }
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

        private static IEnumerator WaitForSpawnerEnemies(
            Component spawner,
            Action<List<GameObject>> found)
        {
            for (var frame = 0; frame < 240; frame++)
            {
                var enemies = GetEnumerableFieldValues(spawner, "_aliveEnemies")
                    .OfType<GameObject>()
                    .Where(enemy => enemy != null && enemy.activeInHierarchy)
                    .ToList();
                if (enemies.Count > 0)
                {
                    found(enemies);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("WaveSpawner did not expose an active run enemy within 240 frames.");
        }

        private static void AssertPoolBelongsToScene(Component pool, Scene activeScene)
        {
            Assert.That(pool.gameObject.scene, Is.EqualTo(activeScene));
            var roots = GetFieldValue(pool, "_poolRoots") as IDictionary;
            var pools = GetFieldValue(pool, "_pools") as IDictionary;
            Assert.That(roots, Is.Not.Null);
            Assert.That(pools, Is.Not.Null);
            foreach (DictionaryEntry entry in roots)
            {
                var root = entry.Value as Transform;
                Assert.That(root, Is.Not.Null);
                Assert.That(root.gameObject.scene, Is.EqualTo(activeScene),
                    $"Pool root {entry.Key} must belong to the active BattleScene.");
            }

            foreach (DictionaryEntry entry in pools)
            {
                var queuedObjects = entry.Value as IEnumerable;
                Assert.That(queuedObjects, Is.Not.Null);
                foreach (var queuedObject in queuedObjects.Cast<GameObject>())
                {
                    Assert.That(queuedObject.scene, Is.EqualTo(activeScene),
                        $"Queued object for {entry.Key} must belong to the active BattleScene.");
                }
            }
        }

        private static void AssertFactoryOwners(Component pool, Component currentSpawner)
        {
            var factories = GetFieldValue(pool, "_factories") as IDictionary;
            Assert.That(factories, Is.Not.Null);
            foreach (DictionaryEntry entry in factories)
            {
                var factory = entry.Value as Delegate;
                Assert.That(factory, Is.Not.Null);
                Assert.That(DelegateReferencesOwner(factory, currentSpawner), Is.True,
                    $"Factory {entry.Key} must capture the current WaveSpawner.");
                AssertNoStaleUnityReferences(factory, currentSpawner, $"factory {entry.Key}");
            }
        }

        private static void AssertEnemyDeathOwners(
            IEnumerable<GameObject> enemies,
            Component currentSpawner)
        {
            foreach (var enemyObject in enemies)
            {
                var enemy = enemyObject.GetComponents<Component>()
                    .First(component => IsEnemyType(component.GetType()));
                var ownedHandlers = GetInstanceEventHandlers(enemy, "OnDeath")
                    .Where(handler => IsDeclaredWithin(currentSpawner.GetType(), handler))
                    .ToList();
                Assert.That(ownedHandlers, Has.Count.EqualTo(1),
                    $"Enemy {enemyObject.GetInstanceID()} must have exactly one current-run death callback.");
                foreach (var handler in ownedHandlers)
                {
                    Assert.That(DelegateReferencesOwner(handler, currentSpawner), Is.True);
                    AssertNoStaleUnityReferences(handler, currentSpawner, "enemy death callback");
                }
            }
        }

        private static void AssertNoStaleUnityReferences(
            Delegate handler,
            Component currentSpawner,
            string context)
        {
            if (handler.Target == null)
            {
                return;
            }

            foreach (var field in handler.Target.GetType()
                         .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!(field.GetValue(handler.Target) is UnityEngine.Object unityObject)
                    || ReferenceEquals(unityObject, null))
                {
                    continue;
                }

                Assert.That(unityObject != null, Is.True, $"{context} must not capture a destroyed Unity object.");
                if (unityObject.GetType().Name == "WaveSpawner")
                {
                    Assert.That(unityObject, Is.SameAs(currentSpawner),
                        $"{context} must not capture an old WaveSpawner.");
                }
            }
        }

        private static bool DelegateReferencesOwner(Delegate handler, Component owner)
        {
            if (ReferenceEquals(handler.Target, owner))
            {
                return true;
            }

            return handler.Target != null
                   && handler.Target.GetType()
                       .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(field => ReferenceEquals(field.GetValue(handler.Target), owner));
        }

        private static List<Delegate> GetInstanceEventHandlers(Component publisher, string eventName)
        {
            for (var type = publisher.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    eventName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field?.GetValue(publisher) is Delegate backingDelegate)
                {
                    return backingDelegate.GetInvocationList().ToList();
                }
            }

            return new List<Delegate>();
        }

        private static bool IsDeclaredWithin(Type ownerType, Delegate handler)
        {
            for (var type = handler.Method.DeclaringType; type != null; type = type.DeclaringType)
            {
                if (type == ownerType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEnemyType(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.Name == "EnemyBase")
                {
                    return true;
                }
            }

            return false;
        }

        private static Component FindUniqueLoadedComponent(string typeName)
        {
            var matches = FindComponents(typeName);
            Assert.That(matches, Has.Count.EqualTo(1), $"Expected exactly one loaded {typeName}.");
            return matches.Single();
        }

        private static void AssertBattleRunSceneOwnership(
            Component run,
            Component gameOver,
            Component spawner)
        {
            var activeScene = SceneManager.GetActiveScene();
            Assert.That(run.gameObject.scene, Is.EqualTo(activeScene));
            Assert.That(gameOver.gameObject.scene, Is.EqualTo(activeScene));
            Assert.That(GetEventHandlers(spawner.GetType(), spawner, "OnAllWavesComplete")
                    .Count(handler => IsDeclaredWithin(run.GetType(), handler)),
                Is.EqualTo(1));
            Assert.That(GetEventHandlers(gameOver.GetType(), gameOver, "OnRestart")
                    .Count(handler => IsDeclaredWithin(run.GetType(), handler)),
                Is.EqualTo(1));

            var stats = GameObject.Find("Player")?.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "CharacterStats");
            Assert.That(stats, Is.Not.Null);
            Assert.That(GetEventHandlers(stats.GetType(), stats, "OnDeath")
                    .Count(handler => IsDeclaredWithin(run.GetType(), handler)),
                Is.EqualTo(1));

            var instance = gameOver.GetType().GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.GetValue(null);
            Assert.That(instance, Is.SameAs(gameOver));
        }

        private static void AssertSingleSceneEventSystem()
        {
            var eventSystem = FindUniqueActiveSceneComponent("EventSystem");
            Assert.That(FindComponents("EventSystem"), Has.Count.EqualTo(1));
            Assert.That(eventSystem.GetComponents<Component>()
                    .Count(component => component.GetType().Name == "StandaloneInputModule"),
                Is.EqualTo(1));
        }

        private static Component FindUniqueActiveSceneComponent(string typeName)
        {
            var activeScene = SceneManager.GetActiveScene();
            var matches = FindComponents(typeName)
                .Where(component => component.gameObject.scene == activeScene)
                .ToList();
            Assert.That(matches, Has.Count.EqualTo(1),
                $"Expected exactly one active-scene {typeName}.");
            return matches.Single();
        }

        private static object GetFieldValue(Component component, string fieldName)
        {
            var field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            return field.GetValue(component);
        }

        private static List<object> GetEnumerableFieldValues(Component component, string fieldName)
        {
            var enumerable = GetFieldValue(component, fieldName) as IEnumerable;
            Assert.That(enumerable, Is.Not.Null);
            return enumerable.Cast<object>().ToList();
        }

        private static List<string> GetDictionaryKeys(Component component, string fieldName)
        {
            var dictionary = GetFieldValue(component, fieldName) as IDictionary;
            Assert.That(dictionary, Is.Not.Null);
            return dictionary.Keys.Cast<object>().Select(key => key.ToString()).ToList();
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
                        .Count(handler => IsBattleSceneHandler(setupType, backingFieldName, handler));
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
                             .Where(handler => IsBattleSceneHandler(setupType, eventName, handler)))
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

        private static bool IsBattleSceneHandler(Type setupType, string eventName, Delegate handler)
        {
            return IsDeclaredBy(setupType, handler) ||
                   (eventName == "OnHitResolved" &&
                    handler.Method.DeclaringType?.Name == "CombatFeedbackController");
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
            else if (unityObject is Component component &&
                     component.GetType().Name == "CombatFeedbackController")
            {
                Assert.That(component.gameObject.scene, Is.EqualTo(currentSetup.gameObject.scene),
                    $"{eventName} must target the current scene feedback controller.");
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
