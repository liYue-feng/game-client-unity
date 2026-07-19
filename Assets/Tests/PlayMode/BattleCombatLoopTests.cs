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
    public sealed class BattleCombatLoopTests
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
            FreezeEnemyAt(grunt, attackHitboxObject.transform.position);
            AddSecondTargetCollider(grunt);
            SetIntField(grunt, "maxHp", 500);
            SetIntField(grunt, "hp", 500);
            yield return new WaitForFixedUpdate();

            Assert.That(GetFieldValue(stateMachine, "_attackHitbox"), Is.SameAs(attackHitbox),
                "BattleSceneSetup must configure the real AttackHitbox on PlayerStateMachine.");

            var sawActiveHitbox = false;
            var sawHitMark = false;
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
                    sawActiveHitbox |= GetBoolProperty(attackHitbox, "IsActive");
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

        private static void Invoke(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(methodName, InstanceFlags);
            Assert.That(method, Is.Not.Null, $"Expected {component.GetType().Name}.{methodName}().");
            method.Invoke(component, null);
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
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            return field.GetValue(component);
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
