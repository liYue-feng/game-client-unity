using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class ApplicationOfflineStartupTests
    {
        [UnityTest]
        public IEnumerator AutomaticStartup_CreatesOfflineApplicationAndServices()
        {
            const int maxFrames = 120;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var applicationObject = GameObject.Find("[GameApplication]");
                var state = GetApplicationProperty(applicationObject, "State");
                if (string.Equals(state?.ToString(), "Ready", System.StringComparison.Ordinal))
                {
                    break;
                }

                yield return null;
            }

            Assert.That(FindAll("[GameApplication]").Count, Is.EqualTo(1));
            Assert.That(GetApplicationProperty(GameObject.Find("[GameApplication]"), "State")?.ToString(), Is.EqualTo("Ready"));
            Assert.That(FindAll("[GameServices]").Count, Is.EqualTo(1));
            Assert.That(GameObject.Find("[MainThreadDispatcher]"), Is.Not.Null);
            Assert.That(GameObject.Find("[SceneTransitionManager]"), Is.Not.Null);
            Assert.That(GameObject.Find("[AudioManager]"), Is.Not.Null);
            Assert.That(GameObject.Find("[LoadingScreen]"), Is.Not.Null);
            Assert.That(GameObject.Find("[AchievementManager]"), Is.Not.Null);
            Assert.That(GameObject.Find("[NetworkClient]"), Is.Null);
            Assert.That(GameObject.Find("[LoginManager]"), Is.Null);
            Assert.That(GameObject.Find("[GameBootstrap]"), Is.Null);
        }

        [UnityTest]
        public IEnumerator Shutdown_RemovesApplicationAndAllServices()
        {
            yield return WaitForReady();

            var application = GetApplicationComponent(GameObject.Find("[GameApplication]"));
            var shutdownMethod = application?.GetType().GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public);
            List<string> remainingObjects;

            try
            {
                shutdownMethod?.Invoke(application, null);
                yield return null;

                remainingObjects = new[]
                    {
                        "[GameApplication]",
                        "[GameServices]",
                        "[MainThreadDispatcher]",
                        "[SceneTransitionManager]",
                        "[AudioManager]",
                        "[LoadingScreen]",
                        "[AchievementManager]"
                    }
                    .Where(objectName => FindAll(objectName).Count != 0)
                    .ToList();
            }
            finally
            {
                InvokeEnsureApplication(application?.GetType().Assembly);
            }

            yield return WaitForReady();
            Assert.That(application, Is.Not.Null);
            Assert.That(shutdownMethod, Is.Not.Null, "GameApplication must expose public Shutdown().");
            Assert.That(remainingObjects, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ShutdownThenImmediateEnsure_CreatesFreshServiceInstances()
        {
            yield return WaitForReady();

            var serviceNames = new[]
            {
                "[MainThreadDispatcher]",
                "[SceneTransitionManager]",
                "[AudioManager]",
                "[LoadingScreen]",
                "[AchievementManager]"
            };
            var oldServiceIds = serviceNames.ToDictionary(
                objectName => objectName,
                objectName => GameObject.Find(objectName).GetInstanceID());
            var application = GetApplicationComponent(GameObject.Find("[GameApplication]"));
            var shutdownMethod = application.GetType().GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public);
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                shutdownMethod.Invoke(application, null);
                InvokeEnsureApplication(application.GetType().Assembly);
                yield return WaitForReady();
                yield return null;
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(FindAll("[GameApplication]").Count, Is.EqualTo(1));
            Assert.That(FindAll("[GameServices]").Count, Is.EqualTo(1));
            var replacementRoot = GameObject.Find("[GameServices]").transform;
            foreach (var objectName in serviceNames)
            {
                var replacement = GameObject.Find(objectName);
                Assert.That(replacement, Is.Not.Null, $"{objectName} must survive immediate application recreation.");
                Assert.That(replacement.GetInstanceID(), Is.Not.EqualTo(oldServiceIds[objectName]));
                Assert.That(replacement.transform.parent, Is.SameAs(replacementRoot));
            }
        }

        private static object GetApplicationProperty(GameObject applicationObject, string propertyName)
        {
            var application = GetApplicationComponent(applicationObject);
            return application?.GetType().GetProperty(propertyName)?.GetValue(application);
        }

        private static Component GetApplicationComponent(GameObject applicationObject)
        {
            return applicationObject == null
                ? null
                : applicationObject.GetComponents<Component>()
                    .FirstOrDefault(component => component != null && component.GetType().Name == "GameApplication");
        }

        private static IEnumerator WaitForReady()
        {
            const int maxFrames = 120;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var state = GetApplicationProperty(GameObject.Find("[GameApplication]"), "State");
                if (string.Equals(state?.ToString(), "Ready", System.StringComparison.Ordinal))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("GameApplication did not reach Ready within 120 frames.");
        }

        private static void InvokeEnsureApplication(Assembly applicationAssembly)
        {
            var bootstrapType = applicationAssembly?.GetType("Game.RuntimeBootstrap");
            var ensureMethod = bootstrapType?.GetMethod("EnsureApplication", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ensureMethod, Is.Not.Null, "RuntimeBootstrap.EnsureApplication must be available for test cleanup.");
            ensureMethod.Invoke(null, null);
        }

        private static List<GameObject> FindAll(string objectName)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene.IsValid() && item.name == objectName)
                .ToList();
        }
    }
}
