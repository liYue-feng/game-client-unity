using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public sealed class BattleCombatLoopTests
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class RecordingParryResponder : IParryResponder
        {
            public int CallCount { get; private set; }

            public void OnParried()
            {
                CallCount++;
            }
        }

        [UnityTest]
        public IEnumerator LethalHurtboxCompletesDefeatExactlyOnce()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var run = FindActiveSceneComponent("BattleRunController");
            var setup = FindActiveSceneComponent("BattleSceneSetup");
            var timeController = FindActiveSceneComponent("BattleTimeController");
            var player = GameObject.Find("Player");
            var stats = FindComponent(player, "CharacterStats");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var hurtbox = FindComponent(player, "Hurtbox");
            var inputBridge = FindComponent(player, "PlayerInputBridge");
            var playerController = FindComponent(player, "PlayerController");
            var playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.velocity = new Vector2(8f, 4f);
            var combatEventsType = stateMachine.GetType().Assembly.GetType("CombatEvents");
            var deathEvent = combatEventsType?.GetEvent("OnPlayerDeath", BindingFlags.Static | BindingFlags.Public);
            Assert.That(deathEvent, Is.Not.Null);
            var legacyDeathCount = 0;
            Action legacyDeathProbe = () => legacyDeathCount++;
            deathEvent.AddEventHandler(null, legacyDeathProbe);

            try
            {
                Assert.That(ReceiveCombatHit(
                    hurtbox,
                    new CombatHit(100000, 1f, 3f, false, new RecordingParryResponder())),
                    Is.EqualTo(CombatHitResult.Damaged));
                yield return new WaitForSecondsRealtime(0.12f);

                var gameOver = FindActiveSceneComponent("GameOverUI");
                Assert.That(GetPropertyValue(run, "State").ToString(), Is.EqualTo("Defeat"));
                Assert.That(GetPropertyValue(run, "Outcome").ToString(), Is.EqualTo("Defeat"));
                Assert.That(GetStateName(stateMachine), Is.EqualTo("Die"));
                Assert.That(playerBody.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(((Behaviour)playerController).enabled, Is.False);
                Assert.That(legacyDeathCount, Is.EqualTo(1));
                Assert.That(GetBoolProperty(inputBridge, "InputEnabled"), Is.False);
                Assert.That((bool)GetPropertyValue(setup, "BattleHotkeysEnabled"), Is.False);
                Assert.That((float)GetPropertyValue(timeController, "EffectiveScale"), Is.EqualTo(0f));
                Assert.That((int)GetPropertyValue(timeController, "ActiveRequestCount"), Is.EqualTo(1));
                Assert.That(GetFieldValue(gameOver, "_overlay") as GameObject, Is.Not.Null);
                Assert.That(((GameObject)GetFieldValue(gameOver, "_overlay")).activeInHierarchy, Is.True);
                Assert.That(gameOver.transform.Cast<Transform>().Count(child => child.name == "OverlayCanvas"), Is.EqualTo(1));

                var resultToken = GetFieldValue(run, "_battleResultToken");
                Invoke(stats, "RaiseDeathEvent");
                ReceiveCombatHit(hurtbox, new CombatHit(100000, 1f, 3f, false, null));
                Invoke(stateMachine, "ForceDie");
                yield return null;

                Assert.That(GetPropertyValue(run, "State").ToString(), Is.EqualTo("Defeat"));
                Assert.That(legacyDeathCount, Is.EqualTo(1));
                Assert.That(FindLoadedComponents("GameOverUI"), Has.Count.EqualTo(1));
                Assert.That(gameOver.transform.Cast<Transform>().Count(child => child.name == "OverlayCanvas"), Is.EqualTo(1));
                Assert.That(GetFieldValue(run, "_battleResultToken"), Is.EqualTo(resultToken));
                Assert.That((int)GetPropertyValue(timeController, "ActiveRequestCount"), Is.EqualTo(1));
            }
            finally
            {
                deathEvent.RemoveEventHandler(null, legacyDeathProbe);
            }
        }

        [UnityTest]
        public IEnumerator WaveCompletionWinsBeforeLaterPlayerDeath()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var run = FindActiveSceneComponent("BattleRunController");
            var spawner = FindActiveSceneComponent("WaveSpawner");
            var player = GameObject.Find("Player");
            var stats = FindComponent(player, "CharacterStats");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var hurtbox = FindComponent(player, "Hurtbox");
            var combatEventsType = stateMachine.GetType().Assembly.GetType("CombatEvents");
            var deathEvent = combatEventsType?.GetEvent("OnPlayerDeath", BindingFlags.Static | BindingFlags.Public);
            Assert.That(deathEvent, Is.Not.Null);
            var legacyDeathCount = 0;
            Action legacyDeathProbe = () => legacyDeathCount++;
            deathEvent.AddEventHandler(null, legacyDeathProbe);

            try
            {
                InvokeRunWaveCompletion(spawner, run);

                Assert.That(GetPropertyValue(run, "State").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetPropertyValue(run, "Outcome").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetStateName(stateMachine), Is.Not.EqualTo("Die"));
                Assert.That(legacyDeathCount, Is.Zero);
                Assert.That(FindLoadedComponents("GameOverUI"), Has.Count.EqualTo(1));
                Assert.That(GetFieldValue(FindActiveSceneComponent("GameOverUI"), "_overlay") as GameObject, Is.Not.Null);

                Assert.That(ReceiveCombatHit(
                    hurtbox,
                    new CombatHit(100000, 1f, 3f, false, new RecordingParryResponder())),
                    Is.EqualTo(CombatHitResult.Damaged));
                yield return null;

                Assert.That(GetPropertyValue(run, "State").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetPropertyValue(run, "Outcome").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetStateName(stateMachine), Is.Not.EqualTo("Die"));
                Assert.That(legacyDeathCount, Is.Zero);
                Assert.That(FindLoadedComponents("GameOverUI"), Has.Count.EqualTo(1));
            }
            finally
            {
                deathEvent.RemoveEventHandler(null, legacyDeathProbe);
            }
        }

        [UnityTest]
        public IEnumerator TerminalResultBlocksPlayerAndBattleHotkeysAtZeroScale()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var run = FindActiveSceneComponent("BattleRunController");
            var spawner = FindActiveSceneComponent("WaveSpawner");
            var setup = FindActiveSceneComponent("BattleSceneSetup");
            var player = GameObject.Find("Player");
            var stats = FindComponent(player, "CharacterStats");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var inputBridge = FindComponent(player, "PlayerInputBridge");
            var inputMediator = FindComponent(player, "InputMediator");
            var playerController = FindComponent(player, "PlayerController");
            var playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.velocity = new Vector2(7f, 3f);

            InvokeRunWaveCompletion(spawner, run);
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GetBoolProperty(inputBridge, "InputEnabled"), Is.False);
            Assert.That((bool)GetPropertyValue(setup, "BattleHotkeysEnabled"), Is.False);
            Assert.That(GetStateName(stateMachine), Is.EqualTo("Idle"));
            Assert.That(playerBody.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(((Behaviour)playerController).enabled, Is.False);
            var staminaBefore = GetIntField(stats, "currentStamina");
            var facingBefore = (int)GetPropertyValue(playerController, "FacingDirection");
            var stateBefore = GetStateName(stateMachine);
            var positionBefore = player.transform.position;

            SetFieldValue(inputMediator, "<AttackPressed>k__BackingField", true);
            SetFieldValue(inputMediator, "<HeavyAttackPressed>k__BackingField", true);
            SetFieldValue(inputMediator, "<DashPressed>k__BackingField", true);
            SetFieldValue(inputMediator, "<ParryPressed>k__BackingField", true);
            Invoke(inputBridge, "Update");
            SetFieldValue(inputMediator, "<PausePressed>k__BackingField", true);
            SetFieldValue(inputMediator, "<InventoryPressed>k__BackingField", true);
            Invoke(setup, "Update");
            SetFieldValue(inputMediator, "<MoveInput>k__BackingField", (float)-facingBefore);
            ((Behaviour)inputMediator).enabled = false;
            yield return null;
            yield return null;

            Assert.That((int)GetPropertyValue(playerController, "FacingDirection"), Is.EqualTo(facingBefore),
                "Terminal movement input must not flip the player.");
            Assert.That(GetStateName(stateMachine), Is.EqualTo(stateBefore),
                "The public input gate must block attack consumption independently of time scale.");
            Assert.That(player.transform.position, Is.EqualTo(positionBefore));
            Assert.That(playerBody.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(staminaBefore),
                "The terminal input gate must block heavy attack, dash, and parry stamina consumption.");
            Assert.That((bool)GetFieldValue(setup, "_isPaused"), Is.False);
            Assert.That((bool)GetFieldValue(setup, "_isInventoryOpen"), Is.False);
        }

        [UnityTest]
        public IEnumerator RestartButtonReloadsFreshRunningBattleScene()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var oldRun = FindActiveSceneComponent("BattleRunController");
            var oldSpawner = FindActiveSceneComponent("WaveSpawner");
            var oldPool = FindUniqueLoadedComponent("ObjectPool");
            var oldPlayer = GameObject.Find("Player");
            var oldPlayerController = FindComponent(oldPlayer, "PlayerController");
            var oldRunId = oldRun.GetInstanceID();
            var oldSpawnerId = oldSpawner.GetInstanceID();
            var oldPoolId = oldPool.GetInstanceID();
            var oldPlayerId = oldPlayer.GetInstanceID();

            InvokeRunWaveCompletion(oldSpawner, oldRun);
            Assert.That(((Behaviour)oldPlayerController).enabled, Is.False,
                "Terminal completion must disable the old PlayerController before restart.");
            var oldGameOver = FindActiveSceneComponent("GameOverUI");
            var restartObject = FindDescendant(oldGameOver.transform, "BtnRestart");
            Assert.That(restartObject, Is.Not.Null);
            var restartButton = restartObject.GetComponent<Button>();
            Assert.That(restartButton, Is.Not.Null);

            restartButton.onClick.Invoke();
            Assert.That(((Behaviour)oldPlayerController).enabled, Is.True,
                "Restart Dispose must restore the old PlayerController before scene unload.");
            restartButton.onClick.Invoke();

            Component newRun = null;
            yield return WaitForFreshBattleRun(oldRunId, found => newRun = found);
            yield return WaitForApplicationReady();
            yield return WaitForSceneTransitionComplete();

            var newSpawner = FindActiveSceneComponent("WaveSpawner");
            var newPool = FindUniqueLoadedComponent("ObjectPool");
            var newPlayer = GameObject.Find("Player");
            var newSetup = FindActiveSceneComponent("BattleSceneSetup");
            var newInputBridge = FindComponent(newPlayer, "PlayerInputBridge");
            var newPlayerController = FindComponent(newPlayer, "PlayerController");
            var newGameOver = FindActiveSceneComponent("GameOverUI");

            Assert.That(oldRun == null, Is.True);
            Assert.That(oldSpawner == null, Is.True);
            Assert.That(oldPool == null, Is.True);
            Assert.That(oldPlayer == null, Is.True);
            Assert.That(oldGameOver == null, Is.True);
            Assert.That(newRun.GetInstanceID(), Is.Not.EqualTo(oldRunId));
            Assert.That(newSpawner.GetInstanceID(), Is.Not.EqualTo(oldSpawnerId));
            Assert.That(newPool.GetInstanceID(), Is.Not.EqualTo(oldPoolId));
            Assert.That(newPlayer.GetInstanceID(), Is.Not.EqualTo(oldPlayerId));
            Assert.That(GetPropertyValue(newRun, "State").ToString(), Is.EqualTo("Running"));
            Assert.That(GetPropertyValue(newRun, "Outcome").ToString(), Is.EqualTo("None"));
            Assert.That(GetBoolProperty(newInputBridge, "InputEnabled"), Is.True);
            Assert.That(((Behaviour)newPlayerController).enabled, Is.True);
            Assert.That((bool)GetPropertyValue(newSetup, "BattleHotkeysEnabled"), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(GetFieldValue(newGameOver, "_overlay"), Is.Null);
            Assert.That(FindLoadedComponents("GameOverUI"), Has.Count.EqualTo(1));
            Assert.That(FindLoadedComponents("EventSystem"), Has.Count.EqualTo(1));
            var eventSystem = FindUniqueLoadedComponent("EventSystem");
            Assert.That(eventSystem.GetComponents<Component>().Count(item => item.GetType().Name == "StandaloneInputModule"),
                Is.EqualTo(1));
            var instance = newGameOver.GetType().GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.GetValue(null);
            Assert.That(instance, Is.SameAs(newGameOver));
        }

        [UnityTest]
        public IEnumerator StaticGameOverShowNeverCreatesOrDuplicatesSceneUi()
        {
            yield return LoadBattleScene();

            var gameOver = FindActiveSceneComponent("GameOverUI");
            var gameOverType = gameOver.GetType();
            var show = gameOverType.GetMethod("Show", BindingFlags.Static | BindingFlags.Public);
            Assert.That(show, Is.Not.Null);

            show.Invoke(null, new object[] { true, null });
            show.Invoke(null, new object[] { false, null });
            Assert.That(FindLoadedComponents("GameOverUI"), Has.Count.EqualTo(1));
            Assert.That(gameOver.transform.Cast<Transform>().Count(child => child.name == "OverlayCanvas"),
                Is.EqualTo(1));

            UnityEngine.Object.Destroy(gameOver.gameObject);
            yield return null;
            Assert.That(FindLoadedComponents("GameOverUI"), Is.Empty);

            LogAssert.Expect(LogType.Error, "[GameOverUI] Scene-owned instance is not installed.");
            show.Invoke(null, new object[] { true, null });
            yield return null;

            Assert.That(FindLoadedComponents("GameOverUI"), Is.Empty,
                "Static Show must not lazily create a UI outside BattleSceneSetup ownership.");
            Assert.That(gameOverType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    ?.GetValue(null),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator BattleSceneOwnsAndConfiguresOneBattleTimeController()
        {
            yield return LoadBattleScene();

            var activeScene = SceneManager.GetActiveScene();
            var controllers = Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null
                                    && component.GetType().Name == "BattleTimeController"
                                    && component.gameObject.scene == activeScene)
                .ToArray();
            Assert.That(controllers, Has.Length.EqualTo(1),
                "BattleScene must own exactly one BattleTimeController.");

            var controller = controllers.Single();
            var playerState = FindComponent(GameObject.Find("Player"), "PlayerStateMachine");
            var hitStop = FindComponent(Camera.main.gameObject, "HitStopController");
            var pauseMenu = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI");
            var upgradeManager = FindComponent(GameObject.Find("UpgradeManager"), "UpgradeManager");
            var levelUpUI = GetFieldValue(upgradeManager, "levelUpUI") as Component;

            Assert.That(GetFieldValue(playerState, "_battleTimeController"), Is.SameAs(controller));
            Assert.That(GetFieldValue(hitStop, "_battleTimeController"), Is.SameAs(controller));
            Assert.That(GetFieldValue(pauseMenu, "_battleTimeController"), Is.SameAs(controller));
            Assert.That(levelUpUI, Is.Not.Null, "UpgradeManager must create its LevelUpUI during initialization.");
            Assert.That(GetFieldValue(levelUpUI, "_battleTimeController"), Is.SameAs(controller));
        }

        [UnityTest]
        public IEnumerator PauseRequestSurvivesParrySlowMotionRelease()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var playerState = FindComponent(GameObject.Find("Player"), "PlayerStateMachine");
            var pauseMenu = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI");
            SetFieldValue(playerState, "slowMoDuration", 0.05f);

            Invoke(pauseMenu, "Pause");
            Invoke(playerState, "OnParrySuccess");
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(GetBoolProperty(playerState, "IsSlowMoActive"), Is.False,
                "The real-time parry slow-motion request must finish while Pause remains active.");
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f),
                "Releasing ParrySlowMotion must not release Pause.");

            Invoke(pauseMenu, "Resume");
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator DestroyingReplacedTimeControllerDoesNotOverrideCurrentRequest()
        {
            yield return LoadBattleScene();

            var sceneController = FindActiveSceneComponent("BattleTimeController");
            var pauseType = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI").GetType();
            var controllerAObject = new GameObject("BattleTimeController_A");
            var controllerA = controllerAObject.AddComponent(sceneController.GetType());
            var pauseObject = new GameObject("PauseMenu_AuthorityProbe");
            var pauseMenu = pauseObject.AddComponent(pauseType);
            InvokeWithArguments(pauseMenu, "ConfigureBattleTimeController", controllerA);
            Invoke(pauseMenu, "Pause");
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

            var controllerBObject = new GameObject("BattleTimeController_B");
            var controllerB = controllerBObject.AddComponent(sceneController.GetType());
            InvokeWithArguments(pauseMenu, "ConfigureBattleTimeController", controllerB);
            Assert.That((float)GetPropertyValue(controllerB, "EffectiveScale"), Is.EqualTo(0f));
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

            UnityEngine.Object.Destroy(controllerAObject);
            yield return null;

            Assert.That((float)GetPropertyValue(controllerB, "EffectiveScale"), Is.EqualTo(0f),
                "The replacement controller must retain the transferred Pause request.");
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f),
                "Destroying an old controller must not override the authoritative controller.");

            Invoke(pauseMenu, "Resume");
            Assert.That((float)GetPropertyValue(controllerB, "EffectiveScale"), Is.EqualTo(1f));
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
            UnityEngine.Object.Destroy(pauseObject);
            UnityEngine.Object.Destroy(controllerBObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingPausedMenuClosesCanvasAndSynchronizesSetupOnce()
        {
            yield return LoadBattleScene();

            var setup = FindActiveSceneComponent("BattleSceneSetup");
            var pauseMenu = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI");
            var canvas = GetFieldValue(pauseMenu, "_canvas") as Canvas;
            var resumeEvent = pauseMenu.GetType().GetEvent("OnResume", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(resumeEvent, Is.Not.Null);
            var resumeCalls = 0;
            Action resumeProbe = () => resumeCalls++;
            resumeEvent.AddEventHandler(pauseMenu, resumeProbe);
            try
            {
                Invoke(setup, "TogglePause");
                Assert.That((bool)GetFieldValue(setup, "_isPaused"), Is.True);
                Assert.That(canvas.enabled, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

                ((Behaviour)pauseMenu).enabled = false;

                Assert.That(canvas.enabled, Is.False,
                    "Disabling PauseMenuUI must not leave its Canvas visible.");
                Assert.That((bool)GetFieldValue(setup, "_isPaused"), Is.False,
                    "PauseMenuUI disable must synchronize BattleSceneSetup through OnResume.");
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(resumeCalls, Is.EqualTo(1));

                ((Behaviour)pauseMenu).enabled = false;
                yield return null;
                Assert.That(resumeCalls, Is.EqualTo(1),
                    "An already-disabled menu must not publish duplicate resume events.");
            }
            finally
            {
                if (pauseMenu != null)
                {
                    resumeEvent.RemoveEventHandler(pauseMenu, resumeProbe);
                    ((Behaviour)pauseMenu).enabled = true;
                }
            }
        }

        [UnityTest]
        public IEnumerator OverlappingHitStopsOwnUniqueTokensUntilEachRealtimeDurationEnds()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var hitStop = FindComponent(Camera.main.gameObject, "HitStopController");
            SetFieldValue(hitStop, "hitStopTimeScale", 0.05f);

            InvokeWithArguments(hitStop, "DoHitStop", 0.25f);
            yield return new WaitForSecondsRealtime(0.02f);
            InvokeWithArguments(hitStop, "DoHitStop", 0.05f);

            var overlappingTokens = GetEnumerableFieldValues(hitStop, "_activeHitStopTokens");
            Assert.That(overlappingTokens, Has.Count.EqualTo(2));
            Assert.That(overlappingTokens.Distinct().Count(), Is.EqualTo(2),
                "Each hit-stop coroutine must own a unique request token.");

            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(GetBoolProperty(hitStop, "IsInHitStop"), Is.True,
                "The longer, earlier hit stop must remain after the shorter, later hit stop releases.");
            Assert.That(GetEnumerableFieldValues(hitStop, "_activeHitStopTokens"), Has.Count.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(0.05f).Within(0.0001f));

            yield return new WaitForSecondsRealtime(0.18f);

            Assert.That(GetBoolProperty(hitStop, "IsInHitStop"), Is.False);
            Assert.That(GetEnumerableFieldValues(hitStop, "_activeHitStopTokens"), Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator SetupExclusivelyOwnsBattleHotkeysAndResumeSynchronizesPauseState()
        {
            yield return LoadBattleScene();

            var setup = FindActiveSceneComponent("BattleSceneSetup");
            var pauseMenu = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI");
            var inventoryUI = FindComponent(GameObject.Find("[InventoryUI]"), "InventoryUI");
            Assert.That(
                pauseMenu.GetType().GetMethod("Update", InstanceFlags | BindingFlags.DeclaredOnly),
                Is.Null,
                "PauseMenuUI must not independently poll Escape.");
            Assert.That(
                inventoryUI.GetType().GetMethod("Update", InstanceFlags | BindingFlags.DeclaredOnly),
                Is.Null,
                "InventoryUI must not independently poll Tab.");

            var hotkeyGate = setup.GetType().GetProperty("BattleHotkeysEnabled", InstanceFlags);
            Assert.That(hotkeyGate, Is.Not.Null, "BattleSceneSetup must expose the battle hotkey gate.");
            Assert.That(hotkeyGate.GetValue(setup), Is.True, "Battle hotkeys must be enabled by default.");

            Invoke(setup, "TogglePause");
            Assert.That((bool)GetFieldValue(setup, "_isPaused"), Is.True);
            Invoke(pauseMenu, "Resume");
            Assert.That((bool)GetFieldValue(setup, "_isPaused"), Is.False,
                "The Resume button path must synchronize BattleSceneSetup pause state.");

            hotkeyGate.SetValue(setup, false);
            var inputMediator = GetFieldValue(setup, "_inputMediator") as Component;
            SetFieldValue(inputMediator, "<PausePressed>k__BackingField", true);
            Invoke(setup, "Update");
            Assert.That((bool)GetFieldValue(setup, "_isPaused"), Is.False,
                "Disabled battle hotkeys must ignore a Pause pulse without disabling lifecycle cleanup.");
        }

        [UnityTest]
        public IEnumerator LevelUpHideDoesNotReleasePauseRequest()
        {
            yield return LoadBattleScene();

            var pauseMenu = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI");
            var upgradeManager = FindComponent(GameObject.Find("UpgradeManager"), "UpgradeManager");
            var levelUpUI = GetFieldValue(upgradeManager, "levelUpUI") as Component;
            Assert.That(levelUpUI, Is.Not.Null);

            Invoke(pauseMenu, "Pause");
            var itemDataType = levelUpUI.GetType().Assembly.GetType("ItemData");
            Assert.That(itemDataType, Is.Not.Null);
            var emptyOptions = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemDataType));
            InvokeWithArguments(levelUpUI, "Show", emptyOptions);
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

            Invoke(levelUpUI, "Hide");
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f),
                "LevelUpUI must release only its own request while Pause remains active.");

            Invoke(pauseMenu, "Resume");
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator DisablingOpenLevelUpClearsUiAndReleasesOnlyItsRequest()
        {
            yield return LoadBattleScene();

            var pauseMenu = FindComponent(GameObject.Find("[PauseMenu]"), "PauseMenuUI");
            var upgradeManager = FindComponent(GameObject.Find("UpgradeManager"), "UpgradeManager");
            var levelUpUI = GetFieldValue(upgradeManager, "levelUpUI") as Component;
            Assert.That(levelUpUI, Is.Not.Null);

            var itemDataType = levelUpUI.GetType().Assembly.GetType("ItemData");
            Assert.That(itemDataType, Is.Not.Null);
            var option = ScriptableObject.CreateInstance(itemDataType);
            var options = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemDataType));
            options.Add(option);
            Invoke(pauseMenu, "Pause");
            InvokeWithArguments(levelUpUI, "Show", options);
            var panel = GetFieldValue(levelUpUI, "panel") as GameObject;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(GetEnumerableFieldValues(levelUpUI, "_optionObjects"), Has.Count.EqualTo(1));

            try
            {
                ((Behaviour)levelUpUI).enabled = false;

                Assert.That(GetBoolProperty(levelUpUI, "IsOpen"), Is.False);
                Assert.That(panel.activeSelf, Is.False,
                    "Disabling LevelUpUI must hide its panel.");
                Assert.That(GetEnumerableFieldValues(levelUpUI, "_currentOptions"), Is.Empty,
                    "Disabling LevelUpUI must clear its option state.");
                Assert.That(GetEnumerableFieldValues(levelUpUI, "_optionObjects"), Is.Empty,
                    "Disabling LevelUpUI must clear generated option objects.");
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f),
                    "Disabling LevelUpUI must release only LevelUp while Pause remains.");

                Invoke(pauseMenu, "Resume");
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                if (levelUpUI != null)
                {
                    ((Behaviour)levelUpUI).enabled = true;
                }

                if (option != null)
                {
                    UnityEngine.Object.Destroy(option);
                }
            }
        }

        [UnityTest]
        public IEnumerator DisposingWaveSpawnerTwiceReleasesRunStateWithoutCreatingPool()
        {
            yield return LoadBattleScene();

            var spawner = FindActiveSceneComponent("WaveSpawner");
            var pool = FindUniqueLoadedComponent("ObjectPool");
            var waves = GetFieldValue(spawner, "waves") as Array;
            Assert.That(waves, Is.Not.Null.And.Not.Empty);
            var firstWave = waves.GetValue(0);
            var entries = firstWave.GetType().GetField("enemies", InstanceFlags)?.GetValue(firstWave) as Array;
            Assert.That(entries, Is.Not.Null.And.Not.Empty);
            var firstEntry = entries.GetValue(0);
            InvokeWithArguments(spawner, "SpawnEnemy", firstEntry);
            InvokeWithArguments(spawner, "SpawnEnemy", firstEntry);

            var liveObjects = GetEnumerableFieldValues(spawner, "_aliveEnemies")
                .OfType<GameObject>()
                .Where(item => item != null)
                .ToList();
            Assert.That(liveObjects.Count, Is.GreaterThanOrEqualTo(2));
            var liveEnemies = liveObjects
                .Select(item => item.GetComponents<Component>().First(component => IsEnemyType(component.GetType())))
                .ToList();
            var registeredKeys = GetEnumerableFieldValues(spawner, "_registeredPoolKeys")
                .Cast<string>()
                .OrderBy(key => key)
                .ToArray();
            CollectionAssert.AreEquivalent(new[] { "archer", "boss", "elite", "grunt" }, registeredKeys);
            Assert.That(GetEnumerableFieldValues(spawner, "_deathHandlers"), Is.Not.Empty);

            const string unrelatedPoolKey = "test-unrelated-weapon";
            Func<GameObject> unrelatedFactory = () => new GameObject("UnrelatedWeaponPoolItem");
            Assert.That(
                (bool)InvokeWithArguments(pool, "Register", unrelatedPoolKey, unrelatedFactory, 1),
                Is.True,
                "The teardown sentinel pool must be owned outside WaveSpawner.");

            var waveStartCalls = 0;
            var allCompleteCalls = 0;
            Action<int> waveStartProbe = wave => waveStartCalls++;
            Action allCompleteProbe = () => allCompleteCalls++;
            spawner.GetType().GetEvent("OnWaveStart", BindingFlags.Instance | BindingFlags.Public)
                .AddEventHandler(spawner, waveStartProbe);
            spawner.GetType().GetEvent("OnAllWavesComplete", BindingFlags.Instance | BindingFlags.Public)
                .AddEventHandler(spawner, allCompleteProbe);

            InvokeWithArguments(liveEnemies[0], "TakeDamage", 100000, 0f, 0f);
            var dispose = spawner.GetType().GetMethod("Dispose", InstanceFlags);
            Assert.That(dispose, Is.Not.Null, "WaveSpawner must expose public idempotent Dispose().");
            Assert.DoesNotThrow(() => dispose.Invoke(spawner, null));
            Assert.DoesNotThrow(() => dispose.Invoke(spawner, null));

            Assert.That((bool)GetFieldValue(spawner, "_disposed"), Is.True);
            Assert.That(GetEnumerableFieldValues(spawner, "_aliveEnemies"), Is.Empty);
            Assert.That(GetEnumerableFieldValues(spawner, "_deathHandlers"), Is.Empty);
            Assert.That(GetEnumerableFieldValues(spawner, "_registeredPoolKeys"), Is.Empty);
            Assert.That((int)GetFieldValue(spawner, "_currentWave"), Is.Zero);
            Assert.That(GetEventHandlers(spawner, "OnWaveStart"), Is.Empty);
            Assert.That(GetEventHandlers(spawner, "OnAllWavesComplete"), Is.Empty);
            foreach (var enemy in liveEnemies.Where(enemy => enemy != null))
            {
                Assert.That(
                    GetEventHandlers(enemy, "OnDeath").Where(handler => IsDeclaredWithin(spawner.GetType(), handler)),
                    Is.Empty,
                    "Dispose must unbind every live EnemyBase.OnDeath callback owned by this spawner.");
            }

            var factoryKeys = GetDictionaryKeys(pool, "_factories");
            Assert.That(factoryKeys.Intersect(registeredKeys), Is.Empty,
                "Dispose must release every enemy factory closure registered by this spawner.");
            Assert.That(factoryKeys, Does.Contain(unrelatedPoolKey),
                "Dispose must preserve pools that this WaveSpawner did not register.");

            UnityEngine.Object.Destroy(pool.gameObject);
            yield return null;
            Assert.DoesNotThrow(() => dispose.Invoke(spawner, null));
            Assert.DoesNotThrow(() => Invoke(spawner, "StartWaves"));
            yield return new WaitForSecondsRealtime(0.8f);

            var existingInstance = pool.GetType()
                .GetProperty("ExistingInstance", BindingFlags.Static | BindingFlags.Public);
            Assert.That(existingInstance, Is.Not.Null,
                "ObjectPool must expose non-creating existing-instance access for teardown.");
            Assert.That(existingInstance.GetValue(null), Is.Null,
                "Disposed WaveSpawner paths must not lazily create a replacement ObjectPool.");
            Assert.That(FindLoadedComponents("ObjectPool"), Is.Empty);
            Assert.That(liveObjects.All(item => item == null), Is.True,
                "Clearing registered pool roots must destroy queued and checked-out run enemies.");
            Assert.That(waveStartCalls, Is.Zero);
            Assert.That(allCompleteCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LightAttackActivatesHitboxAndDamagesOneActiveGruntOnce()
        {
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var attackHitboxObject = GameObject.Find("AttackHitbox");
            var attackHitbox = FindComponent(attackHitboxObject, "Hitbox");
            Component grunt = null;
            yield return WaitForActiveSceneComponent("Grunt", component => grunt = component);

            DisableActiveEnemyBehaviours();
            yield return WaitForPlayerIdle(stateMachine);
            FreezeEnemyAt(grunt, attackHitboxObject.transform.position + Vector3.right * 5f);
            AddSecondTargetCollider(grunt);
            SetIntField(grunt, "maxHp", 500);
            SetIntField(grunt, "hp", 500);
            yield return new WaitForFixedUpdate();

            Assert.That(GetFieldValue(stateMachine, "_attackHitbox"), Is.SameAs(attackHitbox),
                "BattleSceneSetup must configure the real AttackHitbox on PlayerStateMachine.");

            var sawActiveHitbox = false;
            var sawHitMark = false;
            var enteredActiveHitbox = false;
            var observedPhases = new HashSet<string>();
            var activePhaseHitCallbacks = 0;
            var combatEventsType = stateMachine.GetType().Assembly.GetType("CombatEvents");
            var hitLandedEvent = combatEventsType?.GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
            Assert.That(hitLandedEvent, Is.Not.Null, "CombatEvents must expose OnHitLanded.");
            Action<Vector3, int> hitProbe = (position, damage) =>
            {
                if (GetFieldValue(stateMachine, "_attackPhase")?.ToString() == "Active"
                    && (position - grunt.transform.position).sqrMagnitude < 0.0001f)
                {
                    activePhaseHitCallbacks++;
                }
            };
            hitLandedEvent.AddEventHandler(null, hitProbe);
            try
            {
                Invoke(stateMachine, "RequestAttack");

                Assert.That(GetStateName(stateMachine), Is.EqualTo("Attack1"),
                    "The test must enter Attack1 before observing its timeline.");
                for (var sample = 0; sample < 200; sample++)
                {
                    var isActive = GetBoolProperty(attackHitbox, "IsActive");
                    sawActiveHitbox |= isActive;
                    if (isActive && !enteredActiveHitbox)
                    {
                        grunt.transform.position = attackHitboxObject.transform.position;
                        Physics2D.SyncTransforms();
                        enteredActiveHitbox = true;
                        yield return new WaitForFixedUpdate();
                    }

                    sawHitMark |= GetBoolProperty(stateMachine, "HasHitThisAttack");
                    observedPhases.Add(GetFieldValue(stateMachine, "_attackPhase")?.ToString());
                    if (sawActiveHitbox && GetStateName(stateMachine) == "Idle")
                    {
                        break;
                    }

                    yield return new WaitForSeconds(0.01f);
                }
            }
            finally
            {
                hitLandedEvent.RemoveEventHandler(null, hitProbe);
            }

            Assert.That(sawActiveHitbox, Is.True,
                $"AttackHitbox must become active during Attack1's deterministic active phase. " +
                $"Observed phases: {string.Join(", ", observedPhases)}; final state: {GetStateName(stateMachine)}; " +
                $"elapsed: {GetFieldValue(stateMachine, "_attackElapsed")}; " +
                $"timer: {GetFieldValue(stateMachine, "_stateTimer")}; enabled: {(stateMachine as Behaviour)?.enabled}; " +
                $"timeScale: {Time.timeScale}.");
            Assert.That(GetIntField(grunt, "hp"), Is.LessThan(500),
                "Attack1 must damage the active-scene Grunt placed at the real AttackHitbox position.");
            Assert.That(sawHitMark, Is.True,
                "A real Hurtbox hit must call MarkHit so combo cancellation remains available.");
            Assert.That(activePhaseHitCallbacks, Is.EqualTo(1),
                "Two colliders resolving to one Hurtbox must emit exactly one hit callback in an active window.");

            var hpAfterActivePhase = GetIntField(grunt, "hp");
            for (var sample = 0; sample < 30; sample++)
            {
                yield return new WaitForSeconds(0.01f);
            }

            Assert.That(GetBoolProperty(attackHitbox, "IsActive"), Is.False,
                "AttackHitbox must be disabled after the active phase completes.");
            Assert.That(GetIntField(grunt, "hp"), Is.EqualTo(hpAfterActivePhase),
                "One active window must damage the same Hurtbox only once.");
        }

        [UnityTest]
        public IEnumerator HurtStateRejectsHeavyDashAndParryWithoutSpendingStamina()
        {
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            SetIntField(stats, "currentStamina", 100);

            Invoke(stateMachine, "ForceHurt");
            Invoke(stateMachine, "RequestHeavyAttack");
            Invoke(stateMachine, "RequestDash");
            Invoke(stateMachine, "RequestParry");

            Assert.That(GetStateName(stateMachine), Is.EqualTo("Hurt"));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(100),
                "Rejected actions must not spend stamina before transition authorization.");
        }

        [UnityTest]
        public IEnumerator AllowedParrySpendsFifteenStaminaExactlyOnce()
        {
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            SetIntField(stats, "currentStamina", 100);

            Invoke(stateMachine, "RequestParry");

            Assert.That(GetStateName(stateMachine), Is.EqualTo("Parry"));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(85));
            yield return null;
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(85),
                "One allowed parry request must spend its configured cost only once.");
        }

        [UnityTest]
        public IEnumerator CounterWindowAuthorizesHeavyOnlyOnce()
        {
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            SetIntField(stats, "currentStamina", 100);
            OpenCounterWindow(stateMachine);

            Invoke(stateMachine, "RequestHeavyAttack");

            Assert.That(GetStateName(stateMachine), Is.EqualTo("HeavyAttack"));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(70));
            Assert.That(GetBoolProperty(stateMachine, "IsInCounterWindow"), Is.False,
                "An accepted counter attack must consume the counter authorization.");
            var timerAfterAcceptedHeavy = (float)GetFieldValue(stateMachine, "_stateTimer");

            Invoke(stateMachine, "RequestHeavyAttack");

            Assert.That(GetStateName(stateMachine), Is.EqualTo("HeavyAttack"));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(70),
                "A repeated Heavy request in the locked Heavy state must not spend again.");
            Assert.That((float)GetFieldValue(stateMachine, "_stateTimer"), Is.EqualTo(timerAfterAcceptedHeavy),
                "A rejected repeated Heavy request must not re-enter or restart the state.");
        }

        [UnityTest]
        public IEnumerator CounterWindowCannotBypassHurtState()
        {
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            SetIntField(stats, "currentStamina", 100);
            OpenCounterWindow(stateMachine);

            Invoke(stateMachine, "ForceHurt");
            Invoke(stateMachine, "RequestHeavyAttack");

            Assert.That(GetStateName(stateMachine), Is.EqualTo("Hurt"));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(100));
            Assert.That(GetBoolProperty(stateMachine, "IsInCounterWindow"), Is.False,
                "Hurt must revoke stale counter authorization.");
        }

        [UnityTest]
        public IEnumerator CounterWindowCannotBypassDieState()
        {
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            SetIntField(stats, "currentStamina", 100);

            Invoke(stateMachine, "ForceDie");
            OpenCounterWindow(stateMachine);
            Invoke(stateMachine, "RequestHeavyAttack");

            Assert.That(GetStateName(stateMachine), Is.EqualTo("Die"));
            Assert.That(GetIntField(stats, "currentStamina"), Is.EqualTo(100));
            Assert.That(GetBoolProperty(stateMachine, "IsInCounterWindow"), Is.False,
                "Die must revoke counter authorization even if stale state tries to reopen it.");
        }

        [UnityTest]
        public IEnumerator ParryableCombatHitUsesOnlyHurtboxDecisionAndCallsSourceOnce()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            var hurtbox = FindComponent(player, "Hurtbox");
            var source = new RecordingParryResponder();
            var hpBefore = GetIntField(stats, "currentHp");

            Invoke(stateMachine, "RequestParry");
            var firstResult = ReceiveCombatHit(
                hurtbox,
                new CombatHit(12, -1f, 3f, true, source));

            Assert.That(firstResult, Is.EqualTo(CombatHitResult.Parried));
            Assert.That(GetIntField(stats, "currentHp"), Is.EqualTo(hpBefore));
            Assert.That(GetStateName(stateMachine), Is.EqualTo("ParrySuccess"));
            Assert.That(source.CallCount, Is.EqualTo(1));

            var secondResult = ReceiveCombatHit(
                hurtbox,
                new CombatHit(12, -1f, 3f, true, source));

            Assert.That(secondResult, Is.EqualTo(CombatHitResult.Damaged),
                "Once the live parry window is consumed, a second contact must follow normal damage.");
            Assert.That(source.CallCount, Is.EqualTo(1),
                "A terminal second contact must not notify the same source again.");
        }

        [UnityTest]
        public IEnumerator UnparryableCombatHitDamagesInsideParryWindowWithoutCallingSource()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            var hurtbox = FindComponent(player, "Hurtbox");
            var source = new RecordingParryResponder();
            var hpBefore = GetIntField(stats, "currentHp");

            Invoke(stateMachine, "RequestParry");
            var result = ReceiveCombatHit(
                hurtbox,
                new CombatHit(9, 1f, 2f, false, source));

            Assert.That(result, Is.EqualTo(CombatHitResult.Damaged));
            Assert.That(GetIntField(stats, "currentHp"), Is.EqualTo(hpBefore - 9));
            Assert.That(GetStateName(stateMachine), Is.EqualTo("Hurt"));
            Assert.That(source.CallCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LivingEnemyResponderEntersStunnedWithoutResettingDuplicateParry()
        {
            yield return LoadBattleScene();

            Component grunt = null;
            yield return WaitForActiveSceneComponent("Grunt", component => grunt = component);
            DisableActiveEnemyBehaviours();
            Assert.That(grunt, Is.InstanceOf<IParryResponder>(),
                "EnemyBase must expose the gameplay parry responder contract.");
            var responder = (IParryResponder)grunt;

            responder.OnParried();
            Assert.That(GetPropertyValue(grunt, "CurrentState")?.ToString(), Is.EqualTo("Stunned"));

            SetFieldValue(grunt, "_stateTimer", 0.4f);
            responder.OnParried();

            Assert.That(GetPropertyValue(grunt, "CurrentState")?.ToString(), Is.EqualTo("Stunned"));
            Assert.That((float)GetFieldValue(grunt, "_stateTimer"), Is.EqualTo(0.4f),
                "A duplicate callback while already stunned must not restart the stun duration.");
        }

        [UnityTest]
        public IEnumerator HitboxResolvesOwnerResponderAndKeepsPerHurtboxDeduplication()
        {
            yield return LoadBattleScene();

            Component grunt = null;
            yield return WaitForActiveSceneComponent("Grunt", component => grunt = component);
            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var playerCollider = player.GetComponent<Collider2D>();
            var hitboxObject = new GameObject("EnemyHitboxProbe");
            hitboxObject.transform.position = new Vector3(100f, 100f, 0f);
            hitboxObject.AddComponent<BoxCollider2D>().isTrigger = true;
            var hitboxType = stateMachine.GetType().Assembly.GetType("Hitbox");
            var hitbox = hitboxObject.AddComponent(hitboxType);
            SetFieldValue(hitbox, "owner", grunt.gameObject);
            SetFieldValue(hitbox, "damage", 7);
            SetFieldValue(hitbox, "isParryable", true);

            Invoke(stateMachine, "RequestParry");
            Invoke(hitbox, "EnableHitbox");
            InvokeWithArguments(hitbox, "OnTriggerEnter2D", playerCollider);

            Assert.That(GetStateName(stateMachine), Is.EqualTo("ParrySuccess"));
            Assert.That(GetPropertyValue(grunt, "CurrentState")?.ToString(), Is.EqualTo("Stunned"));
            SetFieldValue(grunt, "_stateTimer", 0.4f);

            InvokeWithArguments(hitbox, "OnTriggerEnter2D", playerCollider);
            Assert.That((float)GetFieldValue(grunt, "_stateTimer"), Is.EqualTo(0.4f),
                "The same Hurtbox must remain deduplicated for one active Hitbox window.");

            UnityEngine.Object.Destroy(hitboxObject);
        }

        [UnityTest]
        public IEnumerator ParriedProjectileReversesSurvivesThenDamagesEnemy()
        {
            yield return LoadBattleScene();

            Component grunt = null;
            yield return WaitForActiveSceneComponent("Grunt", component => grunt = component);
            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var playerCollider = player.GetComponent<Collider2D>();
            var projectileOwner = new GameObject("ProjectileOwner");
            var projectile = CreateProjectile(stateMachine, Vector2.right, projectileOwner);

            Invoke(stateMachine, "RequestParry");
            InvokeWithArguments(projectile, "OnTriggerEnter2D", playerCollider);

            Assert.That(GetStateName(stateMachine), Is.EqualTo("ParrySuccess"));
            Assert.That(GetFieldValue(projectile, "owner"), Is.Null);
            Assert.That(projectile.gameObject.tag, Is.EqualTo("PlayerProjectile"));
            Assert.That(projectile.GetComponent<Rigidbody2D>().velocity.x, Is.LessThan(0f));

            yield return null;
            Assert.That(projectile != null && projectile.gameObject.activeInHierarchy, Is.True,
                "A parried projectile must survive the original player-contact branch for reflected flight.");

            FreezeEnemyAt(grunt, new Vector3(50f, 50f, 0f));
            SetIntField(grunt, "maxHp", 500);
            SetIntField(grunt, "hp", 500);
            InvokeWithArguments(projectile, "OnTriggerEnter2D", grunt.GetComponent<Collider2D>());

            Assert.That(GetIntField(grunt, "hp"), Is.LessThan(500),
                "A reflected projectile must route enemy damage through CombatHit.");
            yield return null;
            Assert.That(projectile == null, Is.True,
                "A reflected projectile must end after damaging an enemy.");
            UnityEngine.Object.Destroy(projectileOwner);
        }

        [UnityTest]
        public IEnumerator OrdinaryProjectileDamageDestroysAfterPlayerContact()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            var hpBefore = GetIntField(stats, "currentHp");
            var projectileOwner = new GameObject("ProjectileOwner");
            var projectile = CreateProjectile(stateMachine, Vector2.left, projectileOwner);

            InvokeWithArguments(projectile, "OnTriggerEnter2D", player.GetComponent<Collider2D>());

            Assert.That(GetIntField(stats, "currentHp"), Is.LessThan(hpBefore));
            yield return null;
            Assert.That(projectile == null, Is.True,
                "An ordinary damaging player contact must retain the existing terminal lifecycle.");
            UnityEngine.Object.Destroy(projectileOwner);
        }

        [UnityTest]
        public IEnumerator HurtboxWithoutDamageReceiverReturnsIgnored()
        {
            yield return LoadBattleScene();

            var application = GameObject.Find("[GameApplication]");
            var assembly = application.GetComponents<Component>()
                .First(component => component != null && component.GetType().Name == "GameApplication")
                .GetType().Assembly;
            var target = new GameObject("ReceiverlessHurtbox");
            var hurtbox = target.AddComponent(assembly.GetType("Hurtbox"));

            var result = ReceiveCombatHit(
                hurtbox,
                new CombatHit(5, 1f, 1f, false, new RecordingParryResponder()));

            Assert.That(result, Is.EqualTo(CombatHitResult.Ignored));
            UnityEngine.Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ParryCancelsActiveEliteComboAndLaterAttacksStillWork()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var stats = FindComponent(player, "CharacterStats");
            SetFieldValue(stateMachine, "slowMoDuration", 0.05f);
            SetFieldValue(stateMachine, "counterWindowDuration", 0.05f);
            var elite = CreateEnemyProbe(stateMachine, "Elite", "EliteComboProbe");
            elite.transform.position = player.transform.position - new Vector3(0.7f, 0.2f, 0f);
            SetFieldValue(elite, "comboCount", 3);
            SetFieldValue(elite, "comboInterval", 0.05f);
            Physics2D.SyncTransforms();

            var combatEventsType = stateMachine.GetType().Assembly.GetType("CombatEvents");
            var damageTakenEvent = combatEventsType.GetEvent("OnDamageTaken", BindingFlags.Static | BindingFlags.Public);
            var damageCallbacks = 0;
            Action<Vector3, int> damageProbe = (position, damage) => damageCallbacks++;
            damageTakenEvent.AddEventHandler(null, damageProbe);
            try
            {
                var hpBeforeParry = GetIntField(stats, "currentHp");
                Invoke(stateMachine, "RequestParry");
                Invoke(elite, "OnAttackStart");

                Assert.That(GetStateName(stateMachine), Is.EqualTo("ParrySuccess"),
                    "The first real Elite combo strike must be consumed by the live parry window.");
                Assert.That(GetPropertyValue(elite, "CurrentState")?.ToString(), Is.EqualTo("Stunned"));

                yield return new WaitForSecondsRealtime(0.8f);

                Assert.That(GetIntField(stats, "currentHp"), Is.EqualTo(hpBeforeParry),
                    "Queued Elite combo strikes must not execute after the source accepts parry.");
                Assert.That(damageCallbacks, Is.Zero,
                    "Cancelled combo strikes must not emit later player-damage callbacks.");

                elite.transform.position = new Vector3(100f, 100f, 0f);
                (elite as Behaviour).enabled = true;
                yield return WaitForEnemyToLeaveState(elite, "Stunned", 240);
                yield return WaitForPlayerIdle(stateMachine);

                elite.transform.position = player.transform.position - new Vector3(0.7f, 0.2f, 0f);
                SetFieldValue(elite, "comboCount", 1);
                elite.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                Invoke(elite, "FacePlayer");
                (elite as Behaviour).enabled = false;
                Physics2D.SyncTransforms();
                var facingDirection = (int)GetFieldValue(elite, "_facingDirection");
                var attackCenter = (Vector2)elite.transform.position
                                   + new Vector2(facingDirection * 0.7f, 0.2f);
                Assert.That(
                    Physics2D.OverlapBoxAll(attackCenter, new Vector2(1f, 0.8f), 0f)
                        .Any(collider => collider.gameObject == player),
                    Is.True,
                    "The recovered Elite's real melee attack box must overlap the Player fixture.");
                var hpBeforeLaterAttack = GetIntField(stats, "currentHp");
                Invoke(elite, "OnAttackStart");
                yield return null;

                Assert.That(GetIntField(stats, "currentHp"), Is.LessThan(hpBeforeLaterAttack),
                    "Stopping the parried coroutine must not permanently disable future attacks after recovery.");
                Assert.That(damageCallbacks, Is.EqualTo(1));
            }
            finally
            {
                damageTakenEvent.RemoveEventHandler(null, damageProbe);
                if (elite != null)
                {
                    UnityEngine.Object.Destroy(elite.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator DeadEnemyCanonicalContactReturnsIgnored()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var grunt = CreateEnemyProbe(stateMachine, "Grunt", "DeadGruntCanonicalProbe");
            SetIntField(grunt, "expValue", 0);
            InvokeWithArguments(grunt, "TakeDamage", 100000, 0f, 0f);
            Assert.That(GetBoolProperty(grunt, "IsDead"), Is.True);

            var hurtbox = FindComponent(grunt.gameObject, "Hurtbox");
            var directResult = ReceiveCombatHit(
                hurtbox,
                new CombatHit(10, 1f, 3f, false, new RecordingParryResponder()));
            Assert.That(directResult, Is.EqualTo(CombatHitResult.Ignored),
                "A dead EnemyBase is no longer a valid damage receiver.");
            UnityEngine.Object.Destroy(grunt.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeadEnemyHitboxContactHasNoCombatOrElementalSideEffects()
        {
            yield return LoadBattleScene();

            DisableActiveEnemyBehaviours();
            var player = GameObject.Find("Player");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var grunt = CreateEnemyProbe(stateMachine, "Grunt", "DeadGruntHitboxProbe");
            SetIntField(grunt, "expValue", 0);
            grunt.gameObject.AddComponent(grunt.GetType().Assembly.GetType("CharacterStats"));
            InvokeWithArguments(grunt, "TakeDamage", 100000, 0f, 0f);
            Assert.That(GetBoolProperty(grunt, "IsDead"), Is.True);

            var hitbox = FindComponent(GameObject.Find("AttackHitbox"), "Hitbox");
            var inventory = ConfigureElementalInventory(stateMachine.GetType().Assembly, "elem_burn");
            var combatEventsType = stateMachine.GetType().Assembly.GetType("CombatEvents");
            var hitLandedEvent = combatEventsType.GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
            var hitCallbacks = 0;
            Action<Vector3, int> hitProbe = (position, damage) => hitCallbacks++;
            hitLandedEvent.AddEventHandler(null, hitProbe);
            try
            {
                Invoke(hitbox, "EnableHitbox");
                InvokeWithArguments(hitbox, "OnTriggerEnter2D", grunt.GetComponent<Collider2D>());

                var observedSideEffects = new List<string>();
                if (GetBoolProperty(stateMachine, "HasHitThisAttack"))
                    observedSideEffects.Add("MarkHit");
                if (hitCallbacks != 0)
                    observedSideEffects.Add($"OnHitLanded:{hitCallbacks}");
                if (FindComponentByName(grunt.gameObject, "ActiveEffect") != null)
                    observedSideEffects.Add("ActiveEffect");

                Assert.That(observedSideEffects, Is.Empty,
                    "Corpse contact must not unlock combo, emit hit events, or apply elemental effects.");
            }
            finally
            {
                hitLandedEvent.RemoveEventHandler(null, hitProbe);
                inventory.GetType().GetMethod("Reset", InstanceFlags).Invoke(inventory, null);
                if (grunt != null)
                {
                    UnityEngine.Object.Destroy(grunt.gameObject);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeadCharacterStatsReceiverReturnsIgnored()
        {
            yield return LoadBattleScene();

            var application = GameObject.Find("[GameApplication]");
            var assembly = application.GetComponents<Component>()
                .First(component => component != null && component.GetType().Name == "GameApplication")
                .GetType().Assembly;
            var target = new GameObject("DeadStatsReceiver");
            target.transform.position = new Vector3(100f, 100f, 0f);
            target.AddComponent<Rigidbody2D>().gravityScale = 0f;
            target.AddComponent<BoxCollider2D>();
            var stats = target.AddComponent(assembly.GetType("CharacterStats"));
            var hurtbox = target.AddComponent(assembly.GetType("Hurtbox"));
            SetFieldValue(hurtbox, "stats", stats);
            InvokeWithArguments(stats, "TakeDamage", 100000);
            Assert.That(GetBoolProperty(stats, "IsDead"), Is.True);

            var result = ReceiveCombatHit(
                hurtbox,
                new CombatHit(10, 1f, 3f, false, new RecordingParryResponder()));

            Assert.That(result, Is.EqualTo(CombatHitResult.Ignored));
            UnityEngine.Object.Destroy(target);
        }

        private static IEnumerator LoadBattleScene()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return WaitForApplicationReady();
        }

        private static IEnumerator WaitForApplicationReady()
        {
            for (var frame = 0; frame < 120; frame++)
            {
                var applicationObject = GameObject.Find("[GameApplication]");
                var application = applicationObject == null
                    ? null
                    : applicationObject.GetComponents<Component>()
                        .FirstOrDefault(component => component != null && component.GetType().Name == "GameApplication");
                var state = application?.GetType().GetProperty("State")?.GetValue(application)?.ToString();
                if (state == "Ready")
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("GameApplication did not reach Ready within 120 frames.");
        }

        private static IEnumerator WaitForFreshBattleRun(int oldRunId, Action<Component> found)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var activeScene = SceneManager.GetActiveScene();
                var run = Resources.FindObjectsOfTypeAll<Component>()
                    .FirstOrDefault(item => item != null
                                            && item.GetType().Name == "BattleRunController"
                                            && item.gameObject.scene == activeScene
                                            && item.GetInstanceID() != oldRunId);
                if (run != null)
                {
                    found(run);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("BattleScene restart did not create a fresh BattleRunController within 10 realtime seconds.");
        }

        private static IEnumerator WaitForSceneTransitionComplete()
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var transitionManager = FindLoadedComponents("SceneTransitionManager").SingleOrDefault();
                if (transitionManager != null && !(bool)GetFieldValue(transitionManager, "_isTransitioning"))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("SceneTransitionManager did not finish the restart transition within 10 realtime seconds.");
        }

        private static void InvokeRunWaveCompletion(Component spawner, Component run)
        {
            var handlers = GetEventHandlers(spawner, "OnAllWavesComplete")
                .Where(handler => IsDeclaredWithin(run.GetType(), handler))
                .ToArray();
            Assert.That(handlers, Has.Length.EqualTo(1),
                "WaveSpawner must expose exactly one BattleRunController completion handler.");
            handlers.Single().DynamicInvoke();
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            return root.Cast<Transform>()
                .SelectMany(child => new[] { child }.Concat(child.GetComponentsInChildren<Transform>(true)))
                .FirstOrDefault(child => child.name == objectName)
                ?.gameObject;
        }

        private static IEnumerator WaitForActiveSceneComponent(string typeName, Action<Component> found)
        {
            var activeScene = SceneManager.GetActiveScene();
            for (var frame = 0; frame < 240; frame++)
            {
                var component = Resources.FindObjectsOfTypeAll<Component>()
                    .FirstOrDefault(item => item != null
                                            && item.GetType().Name == typeName
                                            && item.gameObject.scene == activeScene
                                            && item.gameObject.activeInHierarchy);
                if (component != null)
                {
                    found(component);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"No active-scene {typeName} became available within 240 frames.");
        }

        private static void FreezeEnemyAt(Component enemy, Vector3 worldPosition)
        {
            if (enemy is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }

            var body = enemy.GetComponent<Rigidbody2D>();
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            enemy.transform.position = worldPosition;
            Physics2D.SyncTransforms();
        }

        private static void DisableActiveEnemyBehaviours()
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component is MonoBehaviour spawner
                    && component.GetType().Name == "WaveSpawner"
                    && component.gameObject.activeInHierarchy)
                {
                    spawner.StopAllCoroutines();
                    spawner.enabled = false;
                }
            }

            foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (!(component is Behaviour behaviour)
                    || !component.gameObject.activeInHierarchy
                    || !IsEnemyType(component.GetType()))
                {
                    continue;
                }

                behaviour.enabled = false;
                var body = component.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.velocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
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

        private static IEnumerator WaitForPlayerIdle(Component stateMachine)
        {
            for (var sample = 0; sample < 200; sample++)
            {
                var state = GetStateName(stateMachine);
                if (state == "Idle")
                {
                    yield break;
                }

                if (state == "Run")
                {
                    Invoke(stateMachine, "RequestStop");
                }

                yield return new WaitForSeconds(0.01f);
            }

            Assert.Fail($"Player did not return to Idle before the attack probe. State: {GetStateName(stateMachine)}.");
        }

        private static void AddSecondTargetCollider(Component enemy)
        {
            var existing = enemy.GetComponent<BoxCollider2D>();
            Assert.That(existing, Is.Not.Null, "The real Grunt must have its primary BoxCollider2D.");
            var second = enemy.gameObject.AddComponent<BoxCollider2D>();
            second.size = existing.size;
            second.offset = existing.offset;
            second.isTrigger = existing.isTrigger;
            Physics2D.SyncTransforms();
        }

        private static void OpenCounterWindow(Component stateMachine)
        {
            SetFieldValue(stateMachine, "_isInCounterWindow", true);
            SetFieldValue(stateMachine, "_counterWindowTimer", 1f);
        }

        private static Component FindComponent(GameObject gameObject, string typeName)
        {
            Assert.That(gameObject, Is.Not.Null, $"Expected GameObject {typeName} owner to exist.");
            var component = gameObject.GetComponents<Component>()
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, $"Expected {gameObject.name} to contain {typeName}.");
            return component;
        }

        private static Component FindActiveSceneComponent(string typeName)
        {
            var activeScene = SceneManager.GetActiveScene();
            var matches = Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null
                                    && component.GetType().Name == typeName
                                    && component.gameObject.scene == activeScene)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1),
                $"Expected exactly one active-scene {typeName}.");
            return matches.Single();
        }

        private static Component FindUniqueLoadedComponent(string typeName)
        {
            var matches = FindLoadedComponents(typeName);
            Assert.That(matches, Has.Count.EqualTo(1), $"Expected exactly one loaded {typeName}.");
            return matches.Single();
        }

        private static List<Component> FindLoadedComponents(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null
                                    && component.gameObject.scene.IsValid()
                                    && component.GetType().Name == typeName)
                .ToList();
        }

        private static Component CreateProjectile(Component stateMachine, Vector2 direction, GameObject owner)
        {
            var projectileObject = new GameObject("ProjectileProbe");
            projectileObject.transform.position = new Vector3(100f, 100f, 0f);
            projectileObject.tag = "EnemyProjectile";
            projectileObject.AddComponent<BoxCollider2D>().isTrigger = true;
            var body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var projectileType = stateMachine.GetType().Assembly.GetType("Projectile");
            Assert.That(projectileType, Is.Not.Null);
            var projectile = projectileObject.AddComponent(projectileType);
            InvokeWithArguments(projectile, "Launch", direction, owner);
            return projectile;
        }

        private static Component CreateEnemyProbe(Component stateMachine, string typeName, string objectName)
        {
            var enemyObject = new GameObject(objectName);
            enemyObject.AddComponent<SpriteRenderer>();
            var body = enemyObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            enemyObject.AddComponent<BoxCollider2D>();
            enemyObject.AddComponent(stateMachine.GetType().Assembly.GetType("HitEffectPlayer"));
            enemyObject.tag = "Enemy";
            var enemyType = stateMachine.GetType().Assembly.GetType(typeName);
            Assert.That(enemyType, Is.Not.Null);
            var enemy = enemyObject.AddComponent(enemyType);
            (enemy as Behaviour).enabled = false;
            return enemy;
        }

        private static IEnumerator WaitForEnemyToLeaveState(Component enemy, string stateName, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (GetPropertyValue(enemy, "CurrentState")?.ToString() != stateName)
                {
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.01f);
            }

            Assert.Fail($"{enemy.GetType().Name} did not leave {stateName} within {maxFrames} frames.");
        }

        private static object ConfigureElementalInventory(Assembly assembly, string elementId)
        {
            var inventoryType = assembly.GetType("Inventory");
            var inventory = inventoryType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);
            inventoryType.GetMethod("Reset", InstanceFlags).Invoke(inventory, null);
            var addMethod = inventoryType.GetMethod("AddOrUpgrade", InstanceFlags);
            var parameters = addMethod.GetParameters();
            var arguments = parameters
                .Select(parameter => parameter.DefaultValue == DBNull.Value ? null : parameter.DefaultValue)
                .ToArray();
            arguments[0] = elementId;
            arguments[1] = "Element Probe";
            arguments[2] = "Element Probe";
            arguments[3] = "elemental";
            addMethod.Invoke(inventory, arguments);
            return inventory;
        }

        private static Component FindComponentByName(GameObject gameObject, string typeName)
        {
            return gameObject.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
        }

        private static CombatHitResult ReceiveCombatHit(Component hurtbox, CombatHit hit)
        {
            var method = hurtbox.GetType().GetMethod(
                "ReceiveHit",
                InstanceFlags,
                null,
                new[] { typeof(CombatHit) },
                null);
            Assert.That(method, Is.Not.Null,
                "Hurtbox must expose the canonical ReceiveHit(CombatHit) entry point.");
            return (CombatHitResult)method.Invoke(hurtbox, new object[] { hit });
        }

        private static void Invoke(Component component, string methodName)
        {
            InvokeWithArguments(component, methodName);
        }

        private static object InvokeWithArguments(Component component, string methodName, params object[] arguments)
        {
            var methods = component.GetType().GetMethods(InstanceFlags)
                .Where(method => method.Name == methodName && method.GetParameters().Length == arguments.Length)
                .ToArray();
            Assert.That(methods, Has.Length.EqualTo(1),
                $"Expected one {component.GetType().Name}.{methodName} overload with {arguments.Length} arguments.");
            var method = methods.Single();
            Assert.That(method, Is.Not.Null, $"Expected {component.GetType().Name}.{methodName}().");
            return method.Invoke(component, arguments);
        }

        private static void SetIntField(Component component, string fieldName, int value)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            field.SetValue(component, value);
        }

        private static int GetIntField(Component component, string fieldName)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            return (int)field.GetValue(component);
        }

        private static bool GetBoolProperty(Component component, string propertyName)
        {
            var property = component.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null, $"Expected {component.GetType().Name}.{propertyName}.");
            return (bool)property.GetValue(component);
        }

        private static object GetFieldValue(Component component, string fieldName)
        {
            Assert.That(component, Is.Not.Null, $"Expected owner for field {fieldName}.");
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            return field.GetValue(component);
        }

        private static List<object> GetEnumerableFieldValues(Component component, string fieldName)
        {
            var enumerable = GetFieldValue(component, fieldName) as IEnumerable;
            Assert.That(enumerable, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName} to be enumerable.");
            return enumerable.Cast<object>().ToList();
        }

        private static List<string> GetDictionaryKeys(Component component, string fieldName)
        {
            var dictionary = GetFieldValue(component, fieldName) as IDictionary;
            Assert.That(dictionary, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName} to be a dictionary.");
            return dictionary.Keys.Cast<object>().Select(key => key.ToString()).ToList();
        }

        private static List<Delegate> GetEventHandlers(Component publisher, string eventName)
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

        private static object GetPropertyValue(Component component, string propertyName)
        {
            var property = component.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null, $"Expected {component.GetType().Name}.{propertyName}.");
            return property.GetValue(component);
        }

        private static void SetFieldValue(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            field.SetValue(component, value);
        }

        private static string GetStateName(Component stateMachine)
        {
            var property = stateMachine.GetType().GetProperty("CurrentState", InstanceFlags);
            Assert.That(property, Is.Not.Null, "PlayerStateMachine must expose CurrentState.");
            return property.GetValue(stateMachine)?.ToString();
        }
    }
}
