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
            MovePersistentEnemiesIntoActiveScene();
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return WaitForApplicationReady();
        }

        private static void MovePersistentEnemiesIntoActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return;
            }

            var persistentEnemies = Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null
                                    && component.gameObject.activeInHierarchy
                                    && IsEnemyType(component.GetType())
                                    && component.gameObject.scene != activeScene)
                .ToArray();
            foreach (var enemy in persistentEnemies)
            {
                enemy.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(enemy.gameObject, activeScene);
            }
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

        private static IEnumerator WaitForActiveSceneComponent(string typeName, Action<Component> found)
        {
            var activeScene = SceneManager.GetActiveScene();
            for (var frame = 0; frame < 240; frame++)
            {
                MoveActiveSpawnIntoScene(typeName, activeScene);
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

        private static void MoveActiveSpawnIntoScene(string typeName, Scene activeScene)
        {
            var spawnedComponent = Resources.FindObjectsOfTypeAll<Component>()
                .FirstOrDefault(item => item != null
                                        && item.GetType().Name == typeName
                                        && item.gameObject.activeInHierarchy);
            if (spawnedComponent == null || spawnedComponent.gameObject.scene == activeScene)
            {
                return;
            }

            spawnedComponent.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(spawnedComponent.gameObject, activeScene);
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
