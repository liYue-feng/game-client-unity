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
