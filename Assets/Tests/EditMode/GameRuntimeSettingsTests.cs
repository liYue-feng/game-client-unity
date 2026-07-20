using System;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class GameRuntimeSettingsTests
    {
        private GameRuntimeSettings _settings;

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
            {
                UnityEngine.Object.DestroyImmediate(_settings);
            }
        }

        [Test]
        public void OfflineSettingsAcceptAConfiguredBuildScene()
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);

            Assert.That(settings.TryValidate(scene => scene == "BattleScene", out var error), Is.True, error);
            Assert.That(error, Is.Null);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void MainThreadBudgetMustBePositive(int budget)
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", budget);

            Assert.That(settings.TryValidate(_ => true, out var error), Is.False);
            StringAssert.Contains("MainThreadMaxTasksPerFrame", error);
        }

        [Test]
        public void LifecycleRejectsReadyBeforeInitialization()
        {
            var lifecycle = new GameApplicationLifecycle();

            Assert.Throws<InvalidOperationException>(() => lifecycle.MarkReady());
        }

        [Test]
        public void LifecycleAllowsTheCompleteReadyShutdownSequence()
        {
            var lifecycle = new GameApplicationLifecycle();

            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.Created));
            lifecycle.BeginInitialization();
            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.Initializing));
            lifecycle.MarkReady();
            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.Ready));
            lifecycle.BeginShutdown();
            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.ShuttingDown));
            lifecycle.MarkStopped();
            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.Stopped));
        }

        [Test]
        public void LifecycleAllowsFailureThenShutdown()
        {
            var lifecycle = new GameApplicationLifecycle();

            lifecycle.BeginInitialization();
            lifecycle.MarkFailed();
            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.Failed));
            lifecycle.BeginShutdown();
            lifecycle.MarkStopped();

            Assert.That(lifecycle.State, Is.EqualTo(GameApplicationState.Stopped));
        }

        [TestCase(GameApplicationState.Created)]
        [TestCase(GameApplicationState.Initializing)]
        [TestCase(GameApplicationState.ShuttingDown)]
        [TestCase(GameApplicationState.Stopped)]
        public void LifecycleRejectsShutdownOutsideReadyOrFailed(GameApplicationState targetState)
        {
            var lifecycle = CreateLifecycleInState(targetState);

            Assert.Throws<InvalidOperationException>(() => lifecycle.BeginShutdown());
        }

        [Test]
        public void SerializedDefaultsAreValidAndExposedReadOnly()
        {
            _settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();

            Assert.That(_settings.RuntimeMode, Is.EqualTo(RuntimeMode.Offline));
            Assert.That(_settings.OfflineStartupSceneName, Is.EqualTo("BattleScene"));
            Assert.That(_settings.OnlineStartupSceneName, Is.EqualTo("MenuScene"));
            Assert.That(_settings.StartupSceneName, Is.EqualTo("BattleScene"));
            Assert.That(_settings.EditorLoginIdentity, Is.EqualTo("editor-001"));
            Assert.That(_settings.OnlineSessionTimeoutSeconds, Is.EqualTo(20f));
            Assert.That(_settings.ServerUrl, Is.EqualTo("ws://localhost:8080/ws"));
            Assert.That(_settings.HeartbeatIntervalSeconds, Is.EqualTo(30f));
            Assert.That(_settings.ConnectionTimeoutSeconds, Is.EqualTo(10f));
            Assert.That(_settings.MaxReconnectAttempts, Is.EqualTo(5));
            Assert.That(_settings.InitialReconnectBackoffSeconds, Is.EqualTo(1f));
            Assert.That(_settings.MaxReconnectBackoffSeconds, Is.EqualTo(30f));
            Assert.That(_settings.MainThreadMaxTasksPerFrame, Is.EqualTo(64));
            Assert.That(_settings.TryValidate(_ => true, out var error), Is.True, error);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ModeSpecificSettingsSelectTheConfiguredStartupScene()
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);
            SetString(settings, "_onlineStartupSceneName", "MenuScene");
            SetString(settings, "_editorLoginIdentity", "editor-001");
            SetFloat(settings, "_onlineSessionTimeoutSeconds", 20f);

            Assert.That(settings.OfflineStartupSceneName, Is.EqualTo("BattleScene"));
            Assert.That(settings.OnlineStartupSceneName, Is.EqualTo("MenuScene"));
            Assert.That(settings.StartupSceneName, Is.EqualTo("BattleScene"));
            Assert.That(settings.EditorLoginIdentity, Is.EqualTo("editor-001"));
            Assert.That(settings.EditorLoginIdentity, Does.Not.StartWith("dev:"));
            Assert.That(settings.OnlineSessionTimeoutSeconds, Is.EqualTo(20f));
            Assert.That(settings.TryValidate(scene => scene == "BattleScene", out var offlineError), Is.True, offlineError);

            SetInteger(settings, "_runtimeMode", (int)RuntimeMode.Online);

            Assert.That(settings.StartupSceneName, Is.EqualTo("MenuScene"));
            Assert.That(settings.TryValidate(scene => scene == "MenuScene", out var onlineError), Is.True, onlineError);
            AssertValidationFailure(settings, scene => scene == "BattleScene", "MenuScene");
        }

        [Test]
        public void BlankEditorIdentityIsRejectedOnlyForOnlineMode()
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);
            SetString(settings, "_editorLoginIdentity", " ");

            Assert.That(settings.TryValidate(_ => true, out var offlineError), Is.True, offlineError);

            SetInteger(settings, "_runtimeMode", (int)RuntimeMode.Online);
            SetString(settings, "_onlineStartupSceneName", "MenuScene");
            AssertValidationFailure(settings, _ => true, "EditorLoginIdentity");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void OnlineSessionTimeoutMustBeFiniteAndGreaterThanZero(float timeout)
        {
            var settings = CreateSettings(RuntimeMode.Online, "BattleScene", "ws://localhost:8080/ws", 64);
            SetString(settings, "_onlineStartupSceneName", "MenuScene");
            SetFloat(settings, "_onlineSessionTimeoutSeconds", timeout);

            AssertValidationFailure(settings, _ => true, "OnlineSessionTimeoutSeconds");
        }

        [Test]
        public void UndefinedRuntimeModeIsRejectedFirst()
        {
            var settings = CreateSettings((RuntimeMode)999, string.Empty, "not a url", 0);

            AssertValidationFailure(settings, _ => false, "RuntimeMode");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void StartupSceneNameMustNotBeBlank(string sceneName)
        {
            var settings = CreateSettings(RuntimeMode.Offline, sceneName, "ws://localhost:8080/ws", 64);

            AssertValidationFailure(settings, _ => true, "StartupSceneName");
        }

        [Test]
        public void SceneAvailabilityCheckIsRequired()
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);

            AssertValidationFailure(settings, null, "scene availability check required");
        }

        [Test]
        public void StartupSceneMustBeAvailable()
        {
            var settings = CreateSettings(RuntimeMode.Offline, "MissingScene", "ws://localhost:8080/ws", 64);

            AssertValidationFailure(settings, _ => false, "StartupSceneName");
        }

        [TestCase(RuntimeMode.Offline, "not a url")]
        [TestCase(RuntimeMode.Online, "not a url")]
        [TestCase(RuntimeMode.Offline, "/relative")]
        [TestCase(RuntimeMode.Online, "https://localhost/ws")]
        public void ServerUrlMustBeAnAbsoluteWebSocketUri(RuntimeMode mode, string serverUrl)
        {
            var settings = CreateSettings(mode, "BattleScene", serverUrl, 64);

            AssertValidationFailure(settings, _ => true, "ServerUrl");
        }

        [TestCase("ws://localhost:8080/ws")]
        [TestCase("wss://example.test/ws")]
        public void WebSocketServerSchemesAreAccepted(string serverUrl)
        {
            var settings = CreateSettings(RuntimeMode.Online, "BattleScene", serverUrl, 64);

            Assert.That(settings.TryValidate(_ => true, out var error), Is.True, error);
        }

        [TestCase("_heartbeatIntervalSeconds", "HeartbeatIntervalSeconds")]
        [TestCase("_connectionTimeoutSeconds", "ConnectionTimeoutSeconds")]
        [TestCase("_initialReconnectBackoffSeconds", "InitialReconnectBackoffSeconds")]
        [TestCase("_maxReconnectBackoffSeconds", "MaxReconnectBackoffSeconds")]
        public void PositiveFloatSettingsRejectZero(string fieldName, string propertyName)
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);
            SetFloat(settings, fieldName, 0f);

            AssertValidationFailure(settings, _ => true, propertyName);
        }

        [TestCase("_heartbeatIntervalSeconds", "HeartbeatIntervalSeconds")]
        [TestCase("_connectionTimeoutSeconds", "ConnectionTimeoutSeconds")]
        [TestCase("_initialReconnectBackoffSeconds", "InitialReconnectBackoffSeconds")]
        [TestCase("_maxReconnectBackoffSeconds", "MaxReconnectBackoffSeconds")]
        public void PositiveFloatSettingsRejectNaN(string fieldName, string propertyName)
        {
            var settings = CreateSettings(RuntimeMode.Offline, "BattleScene", "ws://localhost:8080/ws", 64);
            SetFloat(settings, fieldName, float.NaN);

            AssertValidationFailure(settings, _ => true, propertyName);
        }

        [Test]
        public void MaxReconnectAttemptsMayBeZero()
        {
            var settings = CreateSettings(RuntimeMode.Online, "BattleScene", "ws://localhost:8080/ws", 64);
            SetInteger(settings, "_maxReconnectAttempts", 0);

            Assert.That(settings.TryValidate(_ => true, out var error), Is.True, error);
        }

        [Test]
        public void MaxReconnectAttemptsMustNotBeNegative()
        {
            var settings = CreateSettings(RuntimeMode.Online, "BattleScene", "ws://localhost:8080/ws", 64);
            SetInteger(settings, "_maxReconnectAttempts", -1);

            AssertValidationFailure(settings, _ => true, "MaxReconnectAttempts");
        }

        [Test]
        public void MaxReconnectBackoffMustNotBeLessThanInitialBackoff()
        {
            var settings = CreateSettings(RuntimeMode.Online, "BattleScene", "ws://localhost:8080/ws", 64);
            SetFloat(settings, "_initialReconnectBackoffSeconds", 5f);
            SetFloat(settings, "_maxReconnectBackoffSeconds", 4f);

            AssertValidationFailure(settings, _ => true, "MaxReconnectBackoffSeconds");
        }

        private GameRuntimeSettings CreateSettings(RuntimeMode mode, string sceneName, string serverUrl, int budget)
        {
            _settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();
            var serializedSettings = new SerializedObject(_settings);
            serializedSettings.FindProperty("_runtimeMode").intValue = (int)mode;
            serializedSettings.FindProperty("_offlineStartupSceneName").stringValue = sceneName;
            serializedSettings.FindProperty("_serverUrl").stringValue = serverUrl;
            serializedSettings.FindProperty("_mainThreadMaxTasksPerFrame").intValue = budget;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            return _settings;
        }

        private static void SetFloat(GameRuntimeSettings settings, string fieldName, float value)
        {
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty(fieldName).floatValue = value;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(GameRuntimeSettings settings, string fieldName, int value)
        {
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty(fieldName).intValue = value;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(GameRuntimeSettings settings, string fieldName, string value)
        {
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty(fieldName).stringValue = value;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertValidationFailure(
            GameRuntimeSettings settings,
            Func<string, bool> canLoadScene,
            string expectedErrorText)
        {
            Assert.That(settings.TryValidate(canLoadScene, out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            StringAssert.Contains(expectedErrorText, error);
        }

        private static GameApplicationLifecycle CreateLifecycleInState(GameApplicationState targetState)
        {
            var lifecycle = new GameApplicationLifecycle();
            if (targetState == GameApplicationState.Created)
            {
                return lifecycle;
            }

            lifecycle.BeginInitialization();
            if (targetState == GameApplicationState.Initializing)
            {
                return lifecycle;
            }

            lifecycle.MarkReady();
            lifecycle.BeginShutdown();
            if (targetState == GameApplicationState.ShuttingDown)
            {
                return lifecycle;
            }

            lifecycle.MarkStopped();
            return lifecycle;
        }
    }
}
