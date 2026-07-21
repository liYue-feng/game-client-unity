using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Gameplay;
using Game.Network;
using Game.Online;
using Game.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public sealed class RealBackendOnlineFlowTests
    {
        private const string IntegrationEnvironmentVariable = "GAME_BACKEND_INTEGRATION";
        private const float MaxWaitSeconds = 15f;
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private RuntimeSettingsSnapshot _activeSettings;
        private OnlineSessionHost _onlineHost;

        [UnityTest]
        public IEnumerator OnlineApplication_LoginSaveAndReloadArchiveAgainstRealBackend()
        {
            RequireIntegrationOptIn();
            OnlineSessionHost host = null;
            Action archiveSavedHandler = null;
            Action archiveReloadedHandler = null;
            var archiveSaved = false;
            var archiveReloaded = false;
            PlayerArchive reloadedArchive = null;
            var expectedArchive = CreateExpectedArchive();

            try
            {
                yield return StartOnlineSession("integration-client");
                host = _onlineHost;
                AssertNewOnlineSession(host);
                AssertLiveRequestsUseNonZeroSequences();

                archiveSavedHandler = () => archiveSaved = true;
                archiveReloadedHandler = () =>
                {
                    archiveReloaded = true;
                    reloadedArchive = host.Archive;
                };
                host.ArchiveSaved += archiveSavedHandler;
                host.ArchiveReloaded += archiveReloadedHandler;

                Assert.That(host.SaveArchive(expectedArchive), Is.True);
                yield return WaitUntilRealtime(() => archiveSaved, "archive save acknowledgement");
                Assert.That(host.ReloadArchive(), Is.True);
                yield return WaitUntilRealtime(() => archiveReloaded, "ArchiveReloaded response event");

                Assert.That(ArchiveMatches(reloadedArchive, expectedArchive), Is.True);
                AssertLiveRequestsUseNonZeroSequences();
                Debug.Log("[REAL_BACKEND] ARCHIVE_ROUND_TRIP_OK");
            }
            finally
            {
                if (host != null)
                {
                    if (archiveSavedHandler != null)
                    {
                        host.ArchiveSaved -= archiveSavedHandler;
                    }

                    if (archiveReloadedHandler != null)
                    {
                        host.ArchiveReloaded -= archiveReloadedHandler;
                    }
                }

                RestoreOfflineRuntime();
            }

            yield return FinishOfflineCleanup();
        }

        [UnityTest]
        public IEnumerator OnlineVictory_PersistsTwoRealWavesAndReloadsSameSession()
        {
            RequireIntegrationOptIn();
            OnlineSessionHost host = null;
            Action archiveSavedHandler = null;
            Action archiveReloadedHandler = null;
            var archiveSaveCount = 0;
            var archiveReloaded = false;

            try
            {
                yield return StartOnlineSession("integration-battle-victory");
                host = _onlineHost;
                AssertNewOnlineSession(host);
                AssertLiveRequestsUseNonZeroSequences();
                var client = NetworkClient.Instance;
                var uid = client.UID;
                var token = client.Token;
                archiveSavedHandler = () => archiveSaveCount++;
                host.ArchiveSaved += archiveSavedHandler;

                var startButton = FindActiveSceneButton("BtnStart");
                Assert.That(startButton.interactable, Is.True);
                startButton.onClick.Invoke();
                yield return WaitForScene("BattleScene");
                yield return WaitForBattleRuntime();

                var setup = FindActiveSceneComponent("BattleSceneSetup");
                var spawner = FindActiveSceneComponent("WaveSpawner");
                var run = FindActiveSceneComponent("BattleRunController");
                var gameOver = FindActiveSceneComponent("GameOverUI");
                yield return ResetRunningWaves(spawner, setup);
                SetField(spawner, "waves", CreateTwoWaveConfiguration(spawner.GetType().Assembly));
                SetField(spawner, "waveDelay", 0f);
                Invoke(spawner, "StartWaves");

                yield return WaitForWaveAndAliveCount(spawner, 0, 1);
                var firstEnemy = GetOnlyAliveEnemy(spawner);
                Assert.That(firstEnemy.GetType().Name, Is.EqualTo("Grunt"));
                DamageToDeath(firstEnemy);

                yield return WaitForWaveAndAliveCount(spawner, 1, 1);
                var secondEnemy = GetOnlyAliveEnemy(spawner);
                Assert.That(secondEnemy.GetType().Name, Is.EqualTo("Archer"));
                DamageToDeath(secondEnemy);

                var pendingObserved = false;
                yield return WaitForSavedSettlement(
                    gameOver,
                    () => pendingObserved = true,
                    "victory settlement");

                Assert.That(pendingObserved, Is.True,
                    "Victory must expose Pending before the backend save acknowledgement reaches the UI.");
                Assert.That(GetProperty(run, "State").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetProperty(run, "Outcome").ToString(), Is.EqualTo("Victory"));
                Assert.That(archiveSaveCount, Is.EqualTo(1), "Victory must persist exactly one settlement archive.");
                AssertProgress(host.Progress, 10, 20, 200, 2, 1, 1);
                AssertSavedRewardUi(gameOver, 10, 20);

                var menuButton = FindDescendant(gameOver.transform, "BtnMainMenu").GetComponent<Button>();
                Assert.That(menuButton.interactable, Is.True);
                menuButton.onClick.Invoke();
                yield return WaitForScene("MenuScene");

                archiveReloadedHandler = () => archiveReloaded = true;
                host.ArchiveReloaded += archiveReloadedHandler;
                Assert.That(host.ReloadArchive(), Is.True);
                yield return WaitUntilRealtime(() => archiveReloaded, "victory archive reload event");

                AssertProgress(host.Progress, 10, 20, 200, 2, 1, 1);
                AssertSettlementMetadata(host.Progress);
                Assert.That(NetworkClient.Instance.UID, Is.EqualTo(uid));
                Assert.That(NetworkClient.Instance.Token, Is.EqualTo(token));
                AssertLiveRequestsUseNonZeroSequences();
                Debug.Log("[REAL_BACKEND] VICTORY_PERSISTENCE_OK");
            }
            finally
            {
                if (host != null)
                {
                    if (archiveSavedHandler != null)
                    {
                        host.ArchiveSaved -= archiveSavedHandler;
                    }

                    if (archiveReloadedHandler != null)
                    {
                        host.ArchiveReloaded -= archiveReloadedHandler;
                    }
                }

                RestoreOfflineRuntime();
            }

            yield return FinishOfflineCleanup();
        }

        [UnityTest]
        public IEnumerator OnlineDefeat_PersistsOneLethalHurtboxSettlementWithoutVictoryProgress()
        {
            RequireIntegrationOptIn();
            OnlineSessionHost host = null;
            Action archiveSavedHandler = null;
            var archiveSaveCount = 0;

            try
            {
                yield return StartOnlineSession("integration-battle-defeat");
                host = _onlineHost;
                AssertNewOnlineSession(host);
                AssertLiveRequestsUseNonZeroSequences();
                archiveSavedHandler = () => archiveSaveCount++;
                host.ArchiveSaved += archiveSavedHandler;

                FindActiveSceneButton("BtnStart").onClick.Invoke();
                yield return WaitForScene("BattleScene");
                yield return WaitForBattleRuntime();

                var run = FindActiveSceneComponent("BattleRunController");
                var gameOver = FindActiveSceneComponent("GameOverUI");
                var playerHurtbox = FindComponent(GameObject.Find("Player"), "Hurtbox");
                Assert.That(playerHurtbox, Is.Not.Null);
                var hitResult = Invoke(
                    playerHurtbox,
                    "ReceiveHit",
                    new CombatHit(int.MaxValue, 0f, 0f, false));

                Assert.That(hitResult, Is.EqualTo(CombatHitResult.Damaged));
                Assert.That(GetProperty(gameOver, "SettlementState").ToString(), Is.EqualTo("Pending"));
                var pendingObserved = true;
                yield return WaitForSavedSettlement(
                    gameOver,
                    () => pendingObserved = true,
                    "defeat settlement");

                Assert.That(pendingObserved, Is.True);
                Assert.That(GetProperty(run, "State").ToString(), Is.EqualTo("Defeat"));
                Assert.That(GetProperty(run, "Outcome").ToString(), Is.EqualTo("Defeat"));
                Assert.That(archiveSaveCount, Is.EqualTo(1), "Defeat must persist exactly one settlement archive.");
                AssertProgress(host.Progress, 0, 0, 0, 0, 1, 0);
                AssertSavedRewardUi(gameOver, 0, 0);
                AssertLiveRequestsUseNonZeroSequences();
                Debug.Log("[REAL_BACKEND] DEFEAT_SETTLEMENT_OK");
            }
            finally
            {
                if (host != null && archiveSavedHandler != null)
                {
                    host.ArchiveSaved -= archiveSavedHandler;
                }

                RestoreOfflineRuntime();
            }

            yield return FinishOfflineCleanup();
        }

        [UnityTearDown]
        public IEnumerator RestoreOfflineAfterEachTest()
        {
            if (_activeSettings != null)
            {
                RestoreOfflineRuntime();
                yield return WaitForOfflineApplication();
                _activeSettings = null;
                _onlineHost = null;
            }

            Time.timeScale = 1f;
        }

        private IEnumerator StartOnlineSession(string identity)
        {
            Time.timeScale = 1f;
            var settings = Resources.Load("GameRuntimeSettings");
            Assert.That(settings, Is.Not.Null);
            var application = FindApplication();
            Assert.That(application, Is.Not.Null);
            _activeSettings = new RuntimeSettingsSnapshot(settings, application.GetType().Assembly);
            _activeSettings.ConfigureOnline(identity);

            ShutdownApplication(application);
            yield return null;
            InvokeEnsureApplication(_activeSettings.ApplicationAssembly);
            yield return WaitForApplicationTerminalState();

            application = FindApplication();
            Assert.That(GetApplicationProperty(application, "State")?.ToString(), Is.EqualTo("Ready"),
                $"Online GameApplication stopped at {GetApplicationProperty(application, "FailureStage")}: " +
                GetApplicationProperty(application, "FailureReason"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MenuScene"));
            _onlineHost = OnlineSessionHost.Instance;
            Assert.That(_onlineHost, Is.Not.Null);
            Assert.That(_onlineHost.State, Is.EqualTo(OnlineSessionState.Ready));
            Assert.That(NetworkClient.Instance.ConnectionState, Is.EqualTo(NetworkConnectionState.Ready));
        }

        private static void AssertNewOnlineSession(OnlineSessionHost host)
        {
            Assert.That(NetworkClient.Instance.UID, Is.Positive);
            Assert.That(NetworkClient.Instance.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(host.Nickname, Is.Not.Null.And.Not.Empty);
            AssertProgress(host.Progress, 0, 0, 0, 0, 0, 0);
        }

        private static void AssertLiveRequestsUseNonZeroSequences()
        {
            var client = NetworkClient.Instance;
            var nextSequence = (uint)typeof(NetworkClient)
                .GetField("_nextSeq", InstanceFlags)
                ?.GetValue(client);
            Assert.That(nextSequence, Is.Not.Zero, "The live request allocator must reserve seq=0 for pushes.");

            var pending = typeof(NetworkClient)
                .GetField("_pending", InstanceFlags)
                ?.GetValue(client) as IDictionary;
            Assert.That(pending, Is.Not.Null);
            foreach (DictionaryEntry entry in pending)
            {
                Assert.That((uint)entry.Key, Is.Not.Zero,
                    "Every ordinary request pending against the real backend must use a nonzero seq.");
            }
        }

        private static IEnumerator ResetRunningWaves(Component spawner, Component setup)
        {
            ((MonoBehaviour)spawner).StopAllCoroutines();
            foreach (var enemy in GetAliveEnemies(spawner))
            {
                DamageToDeath(enemy);
            }

            Assert.That((int)GetProperty(spawner, "AliveEnemyCount"), Is.Zero,
                "The scene-started wave must be retired before the integration waves begin.");
            yield return WaitForRealtimeDelay(0.7f);
            ((MonoBehaviour)spawner).StopAllCoroutines();
            SetField(setup, "_killCount", 0);
            SetField(setup, "_bossKills", 0);
            SetField(setup, "_startTime", Time.time);
        }

        private static Array CreateTwoWaveConfiguration(Assembly assembly)
        {
            var waveType = assembly.GetType("EnemySpawnGroup");
            var entryType = assembly.GetType("EnemySpawnEntry");
            Assert.That(waveType, Is.Not.Null);
            Assert.That(entryType, Is.Not.Null);
            var waves = Array.CreateInstance(waveType, 2);
            waves.SetValue(CreateWave(waveType, entryType, "grunt"), 0);
            waves.SetValue(CreateWave(waveType, entryType, "archer"), 1);
            return waves;
        }

        private static object CreateWave(Type waveType, Type entryType, string enemyType)
        {
            var wave = Activator.CreateInstance(waveType);
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("enemyType").SetValue(entry, enemyType);
            entryType.GetField("count").SetValue(entry, 1);
            var entries = Array.CreateInstance(entryType, 1);
            entries.SetValue(entry, 0);
            waveType.GetField("enemies").SetValue(wave, entries);
            waveType.GetField("spawnDelay").SetValue(wave, 0f);
            return wave;
        }

        private static IEnumerator WaitForWaveAndAliveCount(
            Component spawner,
            int expectedWave,
            int expectedAlive)
        {
            yield return WaitUntilRealtime(
                () => (int)GetProperty(spawner, "CurrentWaveIndex") == expectedWave &&
                      (int)GetProperty(spawner, "AliveEnemyCount") == expectedAlive,
                $"wave {expectedWave} alive count {expectedAlive}");
        }

        private static IEnumerator WaitForSavedSettlement(
            Component gameOver,
            Action pendingObserved,
            string description)
        {
            var deadline = Time.realtimeSinceStartup + MaxWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var state = GetProperty(gameOver, "SettlementState").ToString();
                if (state == "Pending")
                {
                    pendingObserved?.Invoke();
                }

                if (state == "Saved")
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Timed out waiting for {description}. UI={GetProperty(gameOver, "SettlementState")}, " +
                $"Online={OnlineSessionHost.Instance?.State}, Failure={OnlineSessionHost.Instance?.FailureReason}.");
        }

        private static void AssertProgress(
            PlayerProgressState progress,
            int gold,
            int experience,
            long bestScore,
            long totalKills,
            long totalGames,
            int highestClearedDungeon)
        {
            Assert.That(progress.Gold, Is.EqualTo(gold));
            Assert.That(progress.Exp, Is.EqualTo(experience));
            Assert.That(progress.BestScore, Is.EqualTo(bestScore));
            Assert.That(progress.TotalKills, Is.EqualTo(totalKills));
            Assert.That(progress.TotalGames, Is.EqualTo(totalGames));
            Assert.That(progress.HighestClearedDungeon, Is.EqualTo(highestClearedDungeon));
        }

        private static void AssertSettlementMetadata(PlayerProgressState progress)
        {
            Assert.That(progress.SchemaVersion, Is.EqualTo(1));
            Assert.That(progress.TalentPoints, Is.Zero);
            Assert.That(progress.UnlockedStyles, Is.Empty);
            Assert.That(progress.LastStyleId, Is.EqualTo(1));
        }

        private static void AssertSavedRewardUi(Component gameOver, int gold, int experience)
        {
            Assert.That(GetProperty(gameOver, "SettlementState").ToString(), Is.EqualTo("Saved"));
            Assert.That(
                FindDescendant(gameOver.transform, "Reward").GetComponent<Text>().text,
                Is.EqualTo($"\u91d1\u5e01 {gold}  \u7ecf\u9a8c {experience}"));
            Assert.That(FindDescendant(gameOver.transform, "BtnRestart").GetComponent<Button>().interactable, Is.True);
            Assert.That(FindDescendant(gameOver.transform, "BtnMainMenu").GetComponent<Button>().interactable, Is.True);
            Assert.That(FindDescendant(gameOver.transform, "BtnRetry").activeSelf, Is.False);
        }

        private static List<Component> GetAliveEnemies(Component spawner)
        {
            return ((IEnumerable)GetField(spawner, "_aliveEnemies"))
                .Cast<GameObject>()
                .Where(enemy => enemy != null)
                .Select(enemy => enemy.GetComponents<Component>()
                    .FirstOrDefault(component => IsEnemyType(component?.GetType())))
                .Where(enemy => enemy != null && !(bool)GetProperty(enemy, "IsDead"))
                .ToList();
        }

        private static Component GetOnlyAliveEnemy(Component spawner)
        {
            var alive = GetAliveEnemies(spawner);
            Assert.That(alive, Has.Count.EqualTo(1));
            return alive.Single();
        }

        private static void DamageToDeath(Component enemy)
        {
            ((Behaviour)enemy).enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            Invoke(enemy, "TakeDamage", int.MaxValue, 0f, 0f);
            Assert.That((bool)GetProperty(enemy, "IsDead"), Is.True);
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
                   actualStyles.Cast<int>().SequenceEqual(expectedStyles.Cast<int>());
        }

        private void RestoreOfflineRuntime()
        {
            Time.timeScale = 1f;
            if (_activeSettings == null || _activeSettings.Restored)
            {
                return;
            }

            _activeSettings.Restore();
            ShutdownApplication(FindApplication());
            InvokeEnsureApplication(_activeSettings.ApplicationAssembly);
        }

        private IEnumerator FinishOfflineCleanup()
        {
            yield return WaitForOfflineApplication();
            _activeSettings = null;
            _onlineHost = null;
        }

        private static void RequireIntegrationOptIn()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore($"Set {IntegrationEnvironmentVariable}=1 to run the real backend integration test.");
            }
        }

        private static IEnumerator WaitForApplicationTerminalState()
        {
            yield return WaitUntilRealtime(
                () =>
                {
                    var state = GetApplicationProperty(FindApplication(), "State")?.ToString();
                    return state == "Ready" || state == "Failed";
                },
                "GameApplication Ready or Failed");
        }

        private static IEnumerator WaitForBattleRuntime()
        {
            yield return WaitUntilRealtime(
                () => SceneManager.GetActiveScene().name == "BattleScene" &&
                      FindActiveSceneComponentOrNull("BattleRunController") != null &&
                      FindActiveSceneComponentOrNull("GameOverUI") != null,
                "BattleScene runtime");
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            yield return WaitUntilRealtime(
                () => SceneManager.GetActiveScene().name == sceneName,
                $"scene {sceneName}");
        }

        private static IEnumerator WaitForOfflineApplication()
        {
            yield return WaitUntilRealtime(
                () => SceneManager.GetActiveScene().name == "BattleScene" &&
                      GetApplicationProperty(FindApplication(), "State")?.ToString() == "Ready",
                "offline BattleScene Ready cleanup");
        }

        private static IEnumerator WaitForRealtimeDelay(float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntilRealtime(Func<bool> predicate, string description)
        {
            var deadline = Time.realtimeSinceStartup + MaxWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Timed out waiting for {description}. Application={GetApplicationProperty(FindApplication(), "State")}, " +
                $"Online={OnlineSessionHost.Instance?.State}, Scene={SceneManager.GetActiveScene().name}.");
        }

        private static Component FindActiveSceneComponent(string typeName)
        {
            var component = FindActiveSceneComponentOrNull(typeName);
            Assert.That(component, Is.Not.Null, $"Expected one active-scene {typeName}.");
            return component;
        }

        private static Component FindActiveSceneComponentOrNull(string typeName)
        {
            var activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<Component>()
                .SingleOrDefault(component => component != null &&
                                              component.GetType().Name == typeName &&
                                              component.gameObject.scene == activeScene);
        }

        private static Button FindActiveSceneButton(string objectName)
        {
            var activeScene = SceneManager.GetActiveScene();
            var matches = Resources.FindObjectsOfTypeAll<Button>()
                .Where(button => button != null &&
                                 button.name == objectName &&
                                 button.gameObject.scene == activeScene)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), $"Expected exactly one active-scene {objectName}.");
            return matches.Single();
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            var match = root.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(candidate => candidate.name == objectName);
            Assert.That(match, Is.Not.Null, $"Expected {objectName} below {root.name}.");
            return match.gameObject;
        }

        private static object GetField(Component component, string fieldName)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected field {component.GetType().Name}.{fieldName}.");
            return field.GetValue(component);
        }

        private static object GetProperty(Component component, string propertyName)
        {
            var property = component.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null, $"Expected property {component.GetType().Name}.{propertyName}.");
            return property.GetValue(component);
        }

        private static object Invoke(Component component, string methodName, params object[] arguments)
        {
            var methods = component.GetType().GetMethods(InstanceFlags)
                .Where(method => method.Name == methodName &&
                                 method.GetParameters().Length == arguments.Length)
                .Where(method => method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Zip(arguments, (parameterType, argument) =>
                        argument == null || parameterType.IsInstanceOfType(argument))
                    .All(matches => matches))
                .ToArray();
            Assert.That(methods, Has.Length.EqualTo(1),
                $"Expected one compatible {component.GetType().Name}.{methodName} overload.");
            return methods.Single().Invoke(component, arguments);
        }

        private static Component FindComponent(GameObject owner, string typeName)
        {
            return owner?.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected field {component.GetType().Name}.{fieldName}.");
            field.SetValue(component, value);
        }

        private static Component FindApplication()
        {
            var applicationObject = GameObject.Find("[GameApplication]");
            return applicationObject == null
                ? null
                : applicationObject.GetComponents<Component>()
                    .FirstOrDefault(component => component != null && component.GetType().Name == "GameApplication");
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

        private sealed class RuntimeSettingsSnapshot
        {
            private readonly UnityEngine.Object _settings;
            private readonly FieldInfo _runtimeMode;
            private readonly FieldInfo _onlineScene;
            private readonly FieldInfo _serverUrl;
            private readonly FieldInfo _identity;
            private readonly FieldInfo _timeout;
            private readonly object _originalRuntimeMode;
            private readonly object _originalOnlineScene;
            private readonly object _originalServerUrl;
            private readonly object _originalIdentity;
            private readonly object _originalTimeout;

            public RuntimeSettingsSnapshot(UnityEngine.Object settings, Assembly applicationAssembly)
            {
                _settings = settings;
                ApplicationAssembly = applicationAssembly;
                _runtimeMode = FindField(settings, "_runtimeMode");
                _onlineScene = FindField(settings, "_onlineStartupSceneName");
                _serverUrl = FindField(settings, "_serverUrl");
                _identity = FindField(settings, "_editorLoginIdentity");
                _timeout = FindField(settings, "_onlineSessionTimeoutSeconds");
                _originalRuntimeMode = _runtimeMode.GetValue(settings);
                _originalOnlineScene = _onlineScene.GetValue(settings);
                _originalServerUrl = _serverUrl.GetValue(settings);
                _originalIdentity = _identity.GetValue(settings);
                _originalTimeout = _timeout.GetValue(settings);
            }

            public Assembly ApplicationAssembly { get; }
            public bool Restored { get; private set; }

            public void ConfigureOnline(string identity)
            {
                _runtimeMode.SetValue(_settings, Enum.Parse(_runtimeMode.FieldType, "Online"));
                _onlineScene.SetValue(_settings, "MenuScene");
                _serverUrl.SetValue(_settings, "ws://127.0.0.1:8080/ws");
                _identity.SetValue(_settings, identity);
                _timeout.SetValue(_settings, 10f);
            }

            public void Restore()
            {
                _runtimeMode.SetValue(_settings, _originalRuntimeMode);
                _onlineScene.SetValue(_settings, _originalOnlineScene);
                _serverUrl.SetValue(_settings, _originalServerUrl);
                _identity.SetValue(_settings, _originalIdentity);
                _timeout.SetValue(_settings, _originalTimeout);
                Restored = true;
            }

            private static FieldInfo FindField(UnityEngine.Object target, string fieldName)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
                return field;
            }
        }
    }
}
