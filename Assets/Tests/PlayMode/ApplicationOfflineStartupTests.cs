using System;
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
                    $"Offline startup must not create {prohibitedTypeName} on any scene object.");
            }
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
            shutdownMethod.Invoke(application, null);
            InvokeEnsureApplication(application.GetType().Assembly);
            yield return WaitForReady();
            yield return null;

            Assert.That(FindAll("[GameApplication]").Count, Is.EqualTo(1));
            Assert.That(FindAll("[GameServices]").Count, Is.EqualTo(1));
            var replacementRoot = GameObject.Find("[GameServices]").transform;
            foreach (var objectName in serviceNames)
            {
                var replacement = GameObject.Find(objectName);
                Assert.That(replacement, Is.Not.Null, $"{objectName} must survive immediate application recreation.");
                Assert.That(replacement.GetInstanceID(), Is.Not.EqualTo(oldServiceIds[objectName]));
                Assert.That(replacement.transform.parent, Is.SameAs(replacementRoot));

                var replacementComponent = GetServiceComponent(replacement, objectName.Trim('[', ']'));
                var staticInstance = replacementComponent.GetType()
                    .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    ?.GetValue(null);
                Assert.That(staticInstance, Is.SameAs(replacementComponent),
                    $"{objectName}.Instance must retain the replacement owner after old OnDestroy callbacks.");
            }
        }

        [UnityTest]
        public IEnumerator AudioShutdown_CleansPartiallyInitializedRuntimeState()
        {
            yield return WaitForReady();

            var audio = GetServiceComponent(GameObject.Find("[AudioManager]"), "AudioManager");
            var audioType = audio.GetType();
            audioType.GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public).Invoke(audio, null);

            var generatedClip = AudioClip.Create("partial-generated", 64, 1, 8000, false);
            var resourceReference = AudioClip.Create("partial-resource-reference", 64, 1, 8000, false);
            var source = audio.gameObject.AddComponent<AudioSource>();
            InjectPartialAudioState(audio, generatedClip, resourceReference, source);

            audioType.GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public).Invoke(audio, null);
            yield return null;

            AssertAudioStateCleared(audio, generatedClip, resourceReference, source);
            UnityEngine.Object.Destroy(resourceReference);
            yield return RestartApplication();
        }

        [UnityTest]
        public IEnumerator AudioInitializeFailure_CleansThroughSharedPartialStatePath()
        {
            yield return WaitForReady();

            const string poisonKey = "__a2_partial_initialization_failure__";
            var audio = GetServiceComponent(GameObject.Find("[AudioManager]"), "AudioManager");
            var audioType = audio.GetType();
            audioType.GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public).Invoke(audio, null);

            var generatedClip = AudioClip.Create("failed-init-generated", 64, 1, 8000, false);
            var resourceReference = AudioClip.Create("failed-init-resource-reference", 64, 1, 8000, false);
            var source = audio.gameObject.AddComponent<AudioSource>();
            InjectPartialAudioState(audio, generatedClip, resourceReference, source);

            var catalogType = audioType.Assembly.GetType("SoundCatalog");
            var catalog = (IDictionary)catalogType.GetField("Catalog", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var soundEntryType = catalogType.GetNestedType("SoundEntry", BindingFlags.Public);
            catalog.Add(poisonKey, Activator.CreateInstance(soundEntryType));
            try
            {
                var invocation = Assert.Throws<TargetInvocationException>(() =>
                    audioType.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public).Invoke(audio, null));
                Assert.That(invocation.InnerException, Is.TypeOf<NullReferenceException>());
            }
            finally
            {
                catalog.Remove(poisonKey);
            }

            yield return null;
            AssertAudioStateCleared(audio, generatedClip, resourceReference, source);
            UnityEngine.Object.Destroy(resourceReference);
            yield return RestartApplication();
        }

        [Test]
        public void FailureReasonFormatter_IncludesServiceRootCauseAndRollbackErrors()
        {
            var application = GetApplicationComponent(GameObject.Find("[GameApplication]"));
            var applicationType = application.GetType();
            var coreAssembly = applicationType.GetProperty("State").PropertyType.Assembly;
            var initializationExceptionType = coreAssembly.GetType("Game.Core.GameServiceInitializationException");
            var constructor = initializationExceptionType
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            var exception = (Exception)constructor.Invoke(new object[]
            {
                "AudioManager",
                new InvalidOperationException("audio wrapper", new ArgumentException("decoder exploded")),
                new List<Exception>
                {
                    new InvalidOperationException("dispatcher rollback"),
                    new Exception("scene rollback")
                }.AsReadOnly()
            });
            var formatter = applicationType.GetMethod(
                "FormatFailureReason",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(formatter, Is.Not.Null, "GameApplication must expose a deterministic internal formatter.");
            Assert.That(formatter.Invoke(null, new object[] { exception }), Is.EqualTo(
                "Service 'AudioManager' failed. Root cause: decoder exploded. Rollback errors: dispatcher rollback; scene rollback."));
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

        private static Component GetServiceComponent(GameObject serviceObject, string typeName)
        {
            return serviceObject.GetComponents<Component>()
                .First(component => component != null && component.GetType().Name == typeName);
        }

        private static void InjectPartialAudioState(
            Component audio,
            AudioClip generatedClip,
            AudioClip resourceReference,
            AudioSource source)
        {
            var audioType = audio.GetType();
            source.clip = resourceReference;
            audioType.GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(audio, false);
            audioType.GetField("_bgmSource", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(audio, source);

            var pool = audioType.GetField("_sfxPool", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            pool.GetType().GetMethod("Enqueue").Invoke(pool, new object[] { source });

            var clips = (IDictionary)audioType.GetField("_clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            clips["partial-generated"] = generatedClip;
            clips["partial-resource"] = resourceReference;

            var loaded = audioType.GetField("_loadedFromResources", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            loaded.GetType().GetMethod("Add").Invoke(loaded, new object[] { "partial-resource" });
            var generated = audioType.GetField("_generatedRuntimeClips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            generated.GetType().GetMethod("Add").Invoke(generated, new object[] { generatedClip });
        }

        private static void AssertAudioStateCleared(
            Component audio,
            AudioClip generatedClip,
            AudioClip resourceReference,
            AudioSource source)
        {
            var audioType = audio.GetType();
            var pool = audioType.GetField("_sfxPool", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            var clips = audioType.GetField("_clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            var loaded = audioType.GetField("_loadedFromResources", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            var generated = audioType.GetField("_generatedRuntimeClips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);

            Assert.That(generatedClip == null, Is.True, "Generated runtime clips must be destroyed after partial cleanup.");
            Assert.That(resourceReference, Is.Not.Null, "Resource-owned clips must not be destroyed by AudioManager.");
            Assert.That(source.clip, Is.Null, "Stopped AudioSources must release cached clip references.");
            Assert.That((int)pool.GetType().GetProperty("Count").GetValue(pool), Is.Zero);
            Assert.That((int)clips.GetType().GetProperty("Count").GetValue(clips), Is.Zero);
            Assert.That((int)loaded.GetType().GetProperty("Count").GetValue(loaded), Is.Zero);
            Assert.That((int)generated.GetType().GetProperty("Count").GetValue(generated), Is.Zero);
            Assert.That(audioType.GetField("_bgmSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio), Is.Null);
            Assert.That(audioType.GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio), Is.False);
            Assert.That(audioType.GetField("_initializing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio), Is.False);
        }

        private static IEnumerator RestartApplication()
        {
            var application = GetApplicationComponent(GameObject.Find("[GameApplication]"));
            var applicationAssembly = application.GetType().Assembly;
            application.GetType().GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public).Invoke(application, null);
            yield return null;
            InvokeEnsureApplication(applicationAssembly);
            yield return WaitForReady();
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

        private static List<Component> FindComponents(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>()
                .Where(item => item != null && item.gameObject.scene.IsValid() && item.GetType().Name == typeName)
                .ToList();
        }
    }
}
