using System;
using System.Collections;
using System.Reflection;
using Game.Network;
using Game.Online;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class RealBackendOnlineFlowTests
    {
        private const string IntegrationEnvironmentVariable = "GAME_BACKEND_INTEGRATION";
        private const string ExpectedArchive = "{\"phase\":\"a4\",\"coins\":7}";
        private const int MaxWaitFrames = 600;

        [UnityTest]
        public IEnumerator OnlineApplication_LoginSaveAndReloadArchiveAgainstRealBackend()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable), "1", StringComparison.Ordinal))
            {
                Assert.Ignore($"Set {IntegrationEnvironmentVariable}=1 to run the real backend integration test.");
            }

            var settings = Resources.Load("GameRuntimeSettings");
            Assert.That(settings, Is.Not.Null);
            var runtimeModeField = FindField(settings, "_runtimeMode");
            var onlineSceneField = FindField(settings, "_onlineStartupSceneName");
            var serverUrlField = FindField(settings, "_serverUrl");
            var identityField = FindField(settings, "_editorLoginIdentity");
            var timeoutField = FindField(settings, "_onlineSessionTimeoutSeconds");
            var originalRuntimeMode = runtimeModeField.GetValue(settings);
            var originalOnlineScene = onlineSceneField.GetValue(settings);
            var originalServerUrl = serverUrlField.GetValue(settings);
            var originalIdentity = identityField.GetValue(settings);
            var originalTimeout = timeoutField.GetValue(settings);
            var application = FindApplication();
            Assert.That(application, Is.Not.Null);
            var applicationAssembly = application.GetType().Assembly;
            OnlineSessionHost host = null;
            Action archiveSavedHandler = null;
            var archiveSaved = false;
            var saveAccepted = false;
            var reloadAccepted = false;
            var applicationState = string.Empty;
            var failureStage = string.Empty;
            var failureReason = string.Empty;
            var activeScene = string.Empty;
            var onlineState = OnlineSessionState.Idle;
            var networkState = NetworkConnectionState.Disconnected;
            long uid = 0;
            string token = null;
            string nickname = null;
            string archiveData = null;

            try
            {
                runtimeModeField.SetValue(settings, Enum.Parse(runtimeModeField.FieldType, "Online"));
                onlineSceneField.SetValue(settings, "MenuScene");
                serverUrlField.SetValue(settings, "ws://127.0.0.1:8080/ws");
                identityField.SetValue(settings, "integration-client");
                timeoutField.SetValue(settings, 10f);

                ShutdownApplication(application);
                yield return null;
                InvokeEnsureApplication(applicationAssembly);
                yield return WaitForApplicationTerminalState();

                application = FindApplication();
                applicationState = GetApplicationProperty(application, "State")?.ToString();
                failureStage = GetApplicationProperty(application, "FailureStage") as string;
                failureReason = GetApplicationProperty(application, "FailureReason") as string;
                activeScene = SceneManager.GetActiveScene().name;
                host = OnlineSessionHost.Instance;
                if (host != null)
                {
                    onlineState = host.State;
                    nickname = host.Nickname;
                }

                var client = NetworkClient.Instance;
                if (client != null)
                {
                    networkState = client.ConnectionState;
                    uid = client.UID;
                    token = client.Token;
                }

                if (host != null && host.State == OnlineSessionState.Ready)
                {
                    archiveSavedHandler = () => archiveSaved = true;
                    host.ArchiveSaved += archiveSavedHandler;
                    saveAccepted = host.SaveArchive(ExpectedArchive);
                    yield return WaitUntil(() => archiveSaved);
                    reloadAccepted = host.ReloadArchive();
                    yield return WaitUntil(() => string.Equals(host.ArchiveData, ExpectedArchive, StringComparison.Ordinal));
                    archiveData = host.ArchiveData;
                }
            }
            finally
            {
                if (host != null && archiveSavedHandler != null)
                {
                    host.ArchiveSaved -= archiveSavedHandler;
                }

                runtimeModeField.SetValue(settings, originalRuntimeMode);
                onlineSceneField.SetValue(settings, originalOnlineScene);
                serverUrlField.SetValue(settings, originalServerUrl);
                identityField.SetValue(settings, originalIdentity);
                timeoutField.SetValue(settings, originalTimeout);
                ShutdownApplication(FindApplication());
                InvokeEnsureApplication(applicationAssembly);
            }

            yield return WaitForOfflineApplication();

            Assert.That(applicationState, Is.EqualTo("Ready"),
                $"Online GameApplication stopped at {failureStage}: {failureReason}");
            Assert.That(activeScene, Is.EqualTo("MenuScene"));
            Assert.That(onlineState, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(networkState, Is.EqualTo(NetworkConnectionState.Ready));
            Assert.That(uid, Is.Positive);
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            Assert.That(nickname, Is.Not.Null.And.Not.Empty);
            Assert.That(saveAccepted, Is.True);
            Assert.That(archiveSaved, Is.True);
            Assert.That(reloadAccepted, Is.True);
            Assert.That(archiveData, Is.EqualTo(ExpectedArchive));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
            Assert.That(GetApplicationProperty(FindApplication(), "State")?.ToString(), Is.EqualTo("Ready"));
        }

        private static IEnumerator WaitForApplicationTerminalState()
        {
            for (var frame = 0; frame < MaxWaitFrames; frame++)
            {
                var state = GetApplicationProperty(FindApplication(), "State")?.ToString();
                if (string.Equals(state, "Ready", StringComparison.Ordinal) ||
                    string.Equals(state, "Failed", StringComparison.Ordinal))
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            for (var frame = 0; frame < MaxWaitFrames && !predicate(); frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForOfflineApplication()
        {
            for (var frame = 0; frame < MaxWaitFrames; frame++)
            {
                if (SceneManager.GetActiveScene().name == "BattleScene" &&
                    string.Equals(GetApplicationProperty(FindApplication(), "State")?.ToString(), "Ready", StringComparison.Ordinal))
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static Component FindApplication()
        {
            var applicationObject = GameObject.Find("[GameApplication]");
            if (applicationObject == null)
            {
                return null;
            }

            foreach (var component in applicationObject.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "GameApplication")
                {
                    return component;
                }
            }

            return null;
        }

        private static object GetApplicationProperty(Component application, string propertyName)
        {
            return application?.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(application);
        }

        private static void ShutdownApplication(Component application)
        {
            application?.GetType()
                .GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(application, null);
        }

        private static void InvokeEnsureApplication(Assembly applicationAssembly)
        {
            applicationAssembly.GetType("Game.RuntimeBootstrap")
                ?.GetMethod("EnsureApplication", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }

        private static FieldInfo FindField(UnityEngine.Object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
            return field;
        }
    }
}
