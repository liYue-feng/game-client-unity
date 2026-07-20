using System;
using System.Collections;
using System.Reflection;
using Game.Network;
using Game.Online;
using Game.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class RealBackendOnlineFlowTests
    {
        private const string IntegrationEnvironmentVariable = "GAME_BACKEND_INTEGRATION";
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
            var initialArchiveWasDefault = false;
            var reloadedArchive = (PlayerArchive)null;
            var expectedArchive = CreateExpectedArchive();

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
                    initialArchiveWasDefault = host.Progress.Gold == 0 &&
                                               host.Progress.Exp == 0 &&
                                               host.Progress.UnlockedStyles.Count == 0;
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
                    saveAccepted = host.SaveArchive(expectedArchive);
                    yield return WaitUntil(() => archiveSaved, "archive save acknowledgement");
                    reloadAccepted = host.ReloadArchive();
                    yield return WaitUntil(
                        () => host.Archive != null && ArchiveMatches(host.Archive, expectedArchive),
                        "reloaded protobuf archive");
                    reloadedArchive = host.Archive;
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
            Assert.That(initialArchiveWasDefault, Is.True,
                "a new development identity must receive LoadArchiveResp(found=false) and default progress");
            Assert.That(saveAccepted, Is.True);
            Assert.That(archiveSaved, Is.True);
            Assert.That(reloadAccepted, Is.True);
            Assert.That(ArchiveMatches(reloadedArchive, expectedArchive), Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
            Assert.That(GetApplicationProperty(FindApplication(), "State")?.ToString(), Is.EqualTo("Ready"));
        }

        private static PlayerArchive CreateExpectedArchive()
        {
            var archive = new PlayerArchive
            {
                SchemaVersion = 2,
                Gold = 7,
                Exp = 11,
                BestScore = 123,
                TotalKills = 17,
                TotalGames = 3,
                HighestClearedDungeon = 4,
                TalentPoints = 5,
                LastStyleId = 3
            };
            AddUnlockedStyles(archive, 1, 3);
            return archive;
        }

        private static bool ArchiveMatches(PlayerArchive actual, PlayerArchive expected)
        {
            return actual != null &&
                   actual.SchemaVersion == expected.SchemaVersion &&
                   actual.Gold == expected.Gold &&
                   actual.Exp == expected.Exp &&
                   actual.BestScore == expected.BestScore &&
                   actual.TotalKills == expected.TotalKills &&
                   actual.TotalGames == expected.TotalGames &&
                   actual.HighestClearedDungeon == expected.HighestClearedDungeon &&
                   actual.TalentPoints == expected.TalentPoints &&
                   actual.LastStyleId == expected.LastStyleId &&
                   ArchiveUnlockedStylesMatch(actual, expected);
        }

        private static void AddUnlockedStyles(PlayerArchive archive, params int[] styles)
        {
            var values = archive.GetType().GetProperty("UnlockedStyles")?.GetValue(archive) as IList;
            Assert.That(values, Is.Not.Null, "PlayerArchive.UnlockedStyles must remain a generated repeated field.");
            foreach (var style in styles)
            {
                values.Add(style);
            }
        }

        private static bool ArchiveUnlockedStylesMatch(PlayerArchive actual, PlayerArchive expected)
        {
            var actualStyles = actual.GetType().GetProperty("UnlockedStyles")?.GetValue(actual) as IList;
            var expectedStyles = expected.GetType().GetProperty("UnlockedStyles")?.GetValue(expected) as IList;
            return actualStyles != null &&
                   expectedStyles != null &&
                   actualStyles.Count == expectedStyles.Count &&
                   actualStyles.Count == 2 &&
                   (int)actualStyles[0] == (int)expectedStyles[0] &&
                   (int)actualStyles[1] == (int)expectedStyles[1];
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

            var application = FindApplication();
            Assert.Fail(
                $"GameApplication did not reach Ready or Failed within {MaxWaitFrames} frames. " +
                $"State={GetApplicationProperty(application, "State")}, " +
                $"FailureStage={GetApplicationProperty(application, "FailureStage")}, " +
                $"FailureReason={GetApplicationProperty(application, "FailureReason")}, " +
                $"ActiveScene={SceneManager.GetActiveScene().name}.");
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string description)
        {
            for (var frame = 0; frame < MaxWaitFrames && !predicate(); frame++)
            {
                yield return null;
            }

            if (!predicate())
            {
                var host = OnlineSessionHost.Instance;
                Assert.Fail(
                    $"Timed out after {MaxWaitFrames} frames waiting for {description}. " +
                    $"ApplicationState={GetApplicationProperty(FindApplication(), "State")}, " +
                    $"OnlineState={host?.State}, FailureReason={host?.FailureReason}.");
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

            Assert.Fail(
                $"Offline cleanup did not reach BattleScene Ready within {MaxWaitFrames} frames. " +
                $"ApplicationState={GetApplicationProperty(FindApplication(), "State")}, " +
                $"ActiveScene={SceneManager.GetActiveScene().name}.");
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
