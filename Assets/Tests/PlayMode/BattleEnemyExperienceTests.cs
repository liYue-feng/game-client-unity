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
    /// <summary>
    /// 验证真实战斗场景中的敌人接敌范围、相机构图和池复用生命周期。
    /// </summary>
    public sealed class BattleEnemyExperienceTests
    {
        private readonly List<string> _task6EventOrder = new List<string>();
        private readonly List<Component> _task6SpawnedBosses = new List<Component>();
        private readonly List<Component> _task6RemovedBosses = new List<Component>();

        [UnityTest]
        public IEnumerator ForcedRightSpawnMustStartInsideItsChaseRange()
        {
            yield return VerifyForcedSpawn(ArenaSpawnSide.Right);
        }

        [UnityTest]
        public IEnumerator ForcedLeftSpawnMustStartInsideItsChaseRange()
        {
            yield return VerifyForcedSpawn(ArenaSpawnSide.Left);
        }

        [UnityTest]
        public IEnumerator EliteHeavyChoiceMustExtendTelegraphBeforeAttackBegins()
        {
            yield return LoadBattleScene();
            var elite = CreateEnemyProbe("Elite", "B2_RED_Elite");
            try
            {
                ((Behaviour)elite).enabled = false;
                SetField(elite, "heavyAttackChance", 1f);
                SetField(elite, "heavyTelegraphDuration", 0.3f);
                SetField(elite, "_currentCombo", 0);
                Invoke(elite, "FacePlayer");

                Assert.That((bool)Invoke(elite, "TryStartPreparedAttack"), Is.True);

                Assert.That(
                    GetProperty(elite, "CurrentState").ToString(),
                    Is.EqualTo("Telegraph"));
                Assert.That(
                    (float)GetField(elite, "_stateTimer"),
                    Is.EqualTo(0.3f).Within(0.01f));
                var plan = (EnemyAttackPlan)GetProperty(elite, "CurrentAttackPlan");
                Assert.That(plan.AttackId, Is.EqualTo("elite_heavy"));
                Assert.That(plan.TelegraphDuration, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(GetProperty(elite, "CurrentAttackPhase").ToString(), Is.EqualTo("Telegraph"));
                Assert.That(GetProperty(GetField(elite, "_telegraphView"), "IsVisible"), Is.True);
            }
            finally
            {
                Invoke(elite, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(elite.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EliteMustNotReturnToChaseBeforeItsOwnedComboEnds()
        {
            yield return LoadBattleScene();
            var elite = CreateEnemyProbe("Elite", "B2_RED_Combo");
            try
            {
                ((Behaviour)elite).enabled = false;
                SetField(elite, "heavyAttackChance", 0f);
                SetField(elite, "telegraphDuration", 0.01f);
                SetField(elite, "comboCount", 3);
                SetField(elite, "comboInterval", 0.1f);
                SetField(elite, "attackDuration", 0.05f);
                Invoke(elite, "FacePlayer");
                Assert.That((bool)Invoke(elite, "TryStartPreparedAttack"), Is.True);
                yield return WaitForAttackPhase(elite, "Commit", 120);

                yield return new WaitForSeconds(0.06f);

                Assert.That(
                    GetProperty(elite, "CurrentState").ToString(),
                    Is.EqualTo("Attack"));
                Assert.That(GetProperty(elite, "CurrentAttackPhase").ToString(), Is.EqualTo("Commit"));
                var plan = (EnemyAttackPlan)GetProperty(elite, "CurrentAttackPlan");
                Assert.That(plan.AttackId, Is.EqualTo("elite_combo"));
                Assert.That(plan.CommitDuration, Is.GreaterThanOrEqualTo(0.2f));
            }
            finally
            {
                Invoke(elite, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(elite.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator BossChargeKeepsMovingThroughoutItsCommitWindow()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var boss = CreateEnemyProbe("Boss", "B2BossChargeMovementProbe");
            try
            {
                boss.transform.position = new Vector3(-10f, 20f, 0f);
                boss.GetComponent<Collider2D>().enabled = false;
                SetField(boss, "_player", player.transform);
                SetField(boss, "_facingDirection", 1);
                SetField(boss, "_attackPattern", 1);
                Assert.That((bool)Invoke(boss, "TryStartPreparedAttack"), Is.True);
                yield return WaitForAttackPhaseWithinRealtime(boss, "Commit", 3f);
                var commitStartX = boss.transform.position.x;

                yield return new WaitForSeconds(0.18f);

                Assert.That(GetProperty(boss, "CurrentAttackPhase").ToString(), Is.EqualTo("Commit"));
                Assert.That(
                    boss.transform.position.x - commitStartX,
                    Is.GreaterThan(0.8f),
                    "B2_TASK4_BOSS_MOVEMENT_RED_CHARGE: charge velocity must survive every Attack update during Commit.");
            }
            finally
            {
                Invoke(boss, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(boss.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator BossSlamKeepsRisingThroughoutItsCommitWindow()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var boss = CreateEnemyProbe("Boss", "B2BossSlamMovementProbe");
            try
            {
                boss.transform.position = new Vector3(10f, 20f, 0f);
                boss.GetComponent<Collider2D>().enabled = false;
                SetField(boss, "_player", player.transform);
                SetField(boss, "_facingDirection", -1);
                SetField(boss, "_attackPattern", 2);
                Assert.That((bool)Invoke(boss, "TryStartPreparedAttack"), Is.True);
                yield return WaitForAttackPhaseWithinRealtime(boss, "Commit", 3f);
                var commitStartY = boss.transform.position.y;

                yield return new WaitForSeconds(0.18f);

                Assert.That(GetProperty(boss, "CurrentAttackPhase").ToString(), Is.EqualTo("Commit"));
                Assert.That(
                    boss.transform.position.y - commitStartY,
                    Is.GreaterThan(0.8f),
                    "B2_TASK4_BOSS_MOVEMENT_RED_SLAM: slam velocity must survive every Attack update during Commit.");
            }
            finally
            {
                Invoke(boss, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(boss.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EnemyDamageFeedbackMustEqualTheActualHpDelta()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var playerHurtbox = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Hurtbox");
            var gameAssembly = playerHurtbox.GetType().Assembly;
            var hitbox = GameObject.Find("AttackHitbox").GetComponent(gameAssembly.GetType("Hitbox"));
            var grunt = FindFirstActiveEnemy("Grunt");
            ((Behaviour)grunt).enabled = false;
            SetField(grunt, "maxHp", 500);
            SetField(grunt, "hp", 500);
            SetField(grunt, "damageReduction", 0.5f);
            grunt.transform.position = hitbox.transform.position;
            Physics2D.SyncTransforms();

            var reported = -1;
            Action<Vector3, int> probe = (_, appliedDamage) => reported = appliedDamage;
            var hitLandedEvent = gameAssembly.GetType("CombatEvents")
                .GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
            hitLandedEvent.AddEventHandler(null, probe);
            try
            {
                var before = (int)GetField(grunt, "hp");
                Invoke(hitbox, "EnableHitbox");
                yield return new WaitForFixedUpdate();
                var after = (int)GetField(grunt, "hp");
                Assert.That(after, Is.LessThan(before), "The real Hitbox must damage the real Enemy fixture.");
                Assert.That(
                    reported,
                    Is.EqualTo(before - after),
                    "B2_RED_ACTUAL_DAMAGE: feedback must equal the target HP delta");
            }
            finally
            {
                hitLandedEvent.RemoveEventHandler(null, probe);
                Invoke(hitbox, "DisableHitbox");
            }
        }

        [UnityTest]
        public IEnumerator ResolveHitReturnsOutcomeWhileReceiveHitPreservesB1Result()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var grunt = CreateEnemyProbe("Grunt", "B2ResolveHitCompatibilityProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                SetField(grunt, "maxHp", 100);
                SetField(grunt, "hp", 100);
                SetField(grunt, "damageReduction", 0.5f);
                var hurtbox = grunt.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Hurtbox");
                var resolve = hurtbox.GetType().GetMethod(
                    "ResolveHit",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(resolve, Is.Not.Null, "B2_TASK5_RED_RESOLVE_HIT_API");
                var hit = new CombatHit(20, 1f, 0f, false, null);
                var before = (int)GetField(grunt, "hp");

                var outcome = (CombatHitOutcome)resolve.Invoke(hurtbox, new object[] { hit });

                var after = (int)GetField(grunt, "hp");
                Assert.That(outcome.Result, Is.EqualTo(CombatHitResult.Damaged));
                Assert.That(outcome.AppliedDamage, Is.EqualTo(before - after));

                SetField(grunt, "hp", 100);
                var legacyResult = Invoke(hurtbox, "ReceiveHit", hit);
                Assert.That(legacyResult, Is.EqualTo(CombatHitResult.Damaged));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator CombatHitResolverPublishesOneResolvedAndOneLegacyCallback()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var grunt = CreateEnemyProbe("Grunt", "B2ResolverSinglePublishProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                SetField(grunt, "maxHp", 100);
                SetField(grunt, "hp", 100);
                SetField(grunt, "damageReduction", 0.5f);
                var hurtbox = grunt.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Hurtbox");
                var assembly = hurtbox.GetType().Assembly;
                grunt.gameObject.AddComponent(assembly.GetType("HitEffectPlayer"));
                grunt.GetComponent<SpriteRenderer>().color = Color.blue;
                var eventsType = assembly.GetType("CombatEvents");
                var resolvedEvent = eventsType.GetEvent("OnHitResolved", BindingFlags.Static | BindingFlags.Public);
                Assert.That(resolvedEvent, Is.Not.Null, "B2_TASK5_RED_RESOLVED_EVENT_API");
                var resolverType = assembly.GetType("CombatHitResolver");
                Assert.That(resolverType, Is.Not.Null, "B2_TASK5_RED_RESOLVER_API");
                var resolve = resolverType.GetMethod("ResolveAndPublish", BindingFlags.Static | BindingFlags.Public);
                Assert.That(resolve, Is.Not.Null, "B2_TASK5_RED_RESOLVER_METHOD");

                var contextType = assembly.GetType("CombatFeedbackContext");
                var captureMethod = typeof(BattleEnemyExperienceTests).GetMethod(
                        nameof(CaptureResolvedHit),
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(contextType);
                var resolvedProbe = Delegate.CreateDelegate(resolvedEvent.EventHandlerType, captureMethod);
                var hitLandedEvent = eventsType.GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
                var legacyCount = 0;
                var legacyDamage = -1;
                Action<Vector3, int> legacyProbe = (_, appliedDamage) =>
                {
                    legacyCount++;
                    legacyDamage = appliedDamage;
                };
                _resolvedHitCount = 0;
                _lastResolvedHit = null;
                resolvedEvent.AddEventHandler(null, resolvedProbe);
                hitLandedEvent.AddEventHandler(null, legacyProbe);
                try
                {
                    var damageNumberCountBefore = Resources.FindObjectsOfTypeAll<Component>()
                        .Count(component =>
                            component != null &&
                            component.GetType().Name == "DamageNumber" &&
                            component.gameObject.activeInHierarchy);
                    var before = (int)GetField(grunt, "hp");
                    var sourceKind = Enum.Parse(assembly.GetType("CombatFeedbackSourceKind"), "PlayerMelee");
                    var strength = Enum.Parse(assembly.GetType("CombatFeedbackStrength"), "Light");
                    var outcome = (CombatHitOutcome)resolve.Invoke(null, new object[]
                    {
                        hurtbox,
                        new CombatHit(20, 1f, 0f, false, null),
                        player,
                        sourceKind,
                        strength,
                        1
                    });
                    var actualDelta = before - (int)GetField(grunt, "hp");

                    Assert.That(outcome.AppliedDamage, Is.EqualTo(actualDelta));
                    Assert.That(_resolvedHitCount, Is.EqualTo(1), "A producer must publish one resolved callback.");
                    Assert.That(legacyCount, Is.EqualTo(1), "The compatibility event must be mapped once.");
                    Assert.That(legacyDamage, Is.EqualTo(actualDelta));
                    Assert.That(ReadRuntimeProperty(_lastResolvedHit, "AppliedDamage"), Is.EqualTo(actualDelta));
                    Assert.That(ReadRuntimeProperty(_lastResolvedHit, "Target"), Is.SameAs(grunt.gameObject));
                    Assert.That(grunt.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white));
                    Assert.That(
                        Resources.FindObjectsOfTypeAll<Component>()
                            .Count(component =>
                                component != null &&
                                component.GetType().Name == "DamageNumber" &&
                                component.gameObject.activeInHierarchy) - damageNumberCountBefore,
                        Is.EqualTo(1),
                        "One resolved hit must produce one damage number.");
                    var inkEffect = player.GetComponents<Component>()
                        .Single(component => component.GetType().Name == "InkHitEffect");
                    var pool = FindActiveSceneComponent("InkParticlePool");
                    Assert.That(
                        ((IEnumerable)GetField(pool, "_allParticles"))
                            .Cast<GameObject>()
                            .Count(particle => particle.activeSelf),
                        Is.EqualTo((int)GetField(inkEffect, "particleCount")),
                        "One resolved enemy hit must produce exactly one ink splash.");
                }
                finally
                {
                    resolvedEvent.RemoveEventHandler(null, resolvedProbe);
                    hitLandedEvent.RemoveEventHandler(null, legacyProbe);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ResolvedFeedbackSubscriberFailureDoesNotInterruptCombatDispatch()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var grunt = CreateEnemyProbe("Grunt", "B2ResolverFailureIsolationProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                SetField(grunt, "maxHp", 100);
                SetField(grunt, "hp", 100);
                var hurtbox = grunt.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Hurtbox");
                var assembly = hurtbox.GetType().Assembly;
                var eventsType = assembly.GetType("CombatEvents");
                var resolvedEvent = eventsType.GetEvent("OnHitResolved", BindingFlags.Static | BindingFlags.Public);
                var contextType = assembly.GetType("CombatFeedbackContext");
                var throwingMethod = typeof(BattleEnemyExperienceTests).GetMethod(
                        nameof(ThrowResolvedHit),
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(contextType);
                var captureMethod = typeof(BattleEnemyExperienceTests).GetMethod(
                        nameof(CaptureResolvedHit),
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(contextType);
                var throwingProbe = Delegate.CreateDelegate(resolvedEvent.EventHandlerType, throwingMethod);
                var captureProbe = Delegate.CreateDelegate(resolvedEvent.EventHandlerType, captureMethod);
                var hitLandedEvent = eventsType.GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
                var legacyCount = 0;
                var legacyDamage = -1;
                Action<Vector3, int> throwingLegacyProbe = (_, __) =>
                    throw new InvalidOperationException("B2_TASK5_LEGACY_HIT_SUBSCRIBER_FAILURE");
                Action<Vector3, int> legacyProbe = (_, appliedDamage) =>
                {
                    legacyCount++;
                    legacyDamage = appliedDamage;
                };
                _resolvedHitCount = 0;
                _lastResolvedHit = null;
                resolvedEvent.AddEventHandler(null, throwingProbe);
                resolvedEvent.AddEventHandler(null, captureProbe);
                hitLandedEvent.AddEventHandler(null, throwingLegacyProbe);
                hitLandedEvent.AddEventHandler(null, legacyProbe);
                try
                {
                    var resolver = assembly.GetType("CombatHitResolver")
                        .GetMethod("ResolveAndPublish", BindingFlags.Static | BindingFlags.Public);
                    CombatHitOutcome outcome = default;
                    var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
                    LogAssert.ignoreFailingMessages = true;
                    try
                    {
                        Assert.DoesNotThrow(() =>
                        {
                            outcome = (CombatHitOutcome)resolver.Invoke(null, new object[]
                            {
                                hurtbox,
                                new CombatHit(10, 1f, 0f, false, null),
                                player,
                                Enum.Parse(assembly.GetType("CombatFeedbackSourceKind"), "PlayerMelee"),
                                Enum.Parse(assembly.GetType("CombatFeedbackStrength"), "Light"),
                                1
                            });
                        }, "B2_TASK5_RED_LEGACY_HIT_FAILURE_ISOLATION");
                    }
                    finally
                    {
                        LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                    }

                    Assert.That(outcome.Result, Is.EqualTo(CombatHitResult.Damaged));
                    Assert.That(_resolvedHitCount, Is.EqualTo(1),
                        "A failing subscriber must not block later resolved feedback subscribers.");
                    Assert.That(legacyCount, Is.EqualTo(1),
                        "A failing legacy subscriber must not block later compatibility subscribers.");
                    Assert.That(legacyDamage, Is.EqualTo(outcome.AppliedDamage));
                }
                finally
                {
                    resolvedEvent.RemoveEventHandler(null, throwingProbe);
                    resolvedEvent.RemoveEventHandler(null, captureProbe);
                    hitLandedEvent.RemoveEventHandler(null, throwingLegacyProbe);
                    hitLandedEvent.RemoveEventHandler(null, legacyProbe);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator LegacyDamageSubscriberFailureDoesNotInterruptProjectileDestruction()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var assembly = stats.GetType().Assembly;
            var eventsType = assembly.GetType("CombatEvents");
            var damageTakenEvent = eventsType.GetEvent("OnDamageTaken", BindingFlags.Static | BindingFlags.Public);
            var healthyCount = 0;
            var reportedDamage = -1;
            Action<Vector3, int> throwingProbe = (_, __) =>
                throw new InvalidOperationException("B2_TASK5_LEGACY_DAMAGE_SUBSCRIBER_FAILURE");
            Action<Vector3, int> healthyProbe = (_, appliedDamage) =>
            {
                healthyCount++;
                reportedDamage = appliedDamage;
            };
            var projectileObject = new GameObject("B2LegacyFailureProjectileProbe");
            try
            {
                projectileObject.AddComponent<SpriteRenderer>();
                projectileObject.AddComponent<CircleCollider2D>().isTrigger = true;
                var projectile = projectileObject.AddComponent(assembly.GetType("Projectile"));
                Invoke(projectile, "Launch", Vector2.left, projectileObject);
                damageTakenEvent.AddEventHandler(null, throwingProbe);
                damageTakenEvent.AddEventHandler(null, healthyProbe);
                var hpBefore = (int)GetField(stats, "currentHp");
                var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    Assert.DoesNotThrow(
                        () => Invoke(projectile, "OnTriggerEnter2D", player.GetComponent<Collider2D>()),
                        "B2_TASK5_RED_LEGACY_DAMAGE_PROJECTILE_LIFECYCLE");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                }

                var hpAfter = (int)GetField(stats, "currentHp");
                Assert.That(healthyCount, Is.EqualTo(1));
                Assert.That(reportedDamage, Is.EqualTo(hpBefore - hpAfter));
                yield return null;
                Assert.That(projectileObject == null, Is.True,
                    "A legacy presentation failure must not skip Projectile destruction.");
            }
            finally
            {
                damageTakenEvent.RemoveEventHandler(null, throwingProbe);
                damageTakenEvent.RemoveEventHandler(null, healthyProbe);
                if (projectileObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(projectileObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator CombatFeedbackControllerRejectsConfigureAfterDisposeWithoutStaticLeak()
        {
            yield return LoadBattleScene();
            var player = GameObject.Find("Player");
            var runtimeAssembly = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Hurtbox")
                .GetType()
                .Assembly;
            var controllerType = runtimeAssembly.GetType("CombatFeedbackController");
            var eventsType = runtimeAssembly.GetType("CombatEvents");
            var resolvedEvent = eventsType.GetEvent("OnHitResolved", BindingFlags.Static | BindingFlags.Public);
            var resolvedBackingField = eventsType.GetField(
                "OnHitResolved",
                BindingFlags.Static | BindingFlags.NonPublic);
            var configure = controllerType.GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public);
            var handle = controllerType.GetMethod("Handle", BindingFlags.Instance | BindingFlags.Public);
            var inkPool = FindActiveSceneComponent("InkParticlePool");
            var inkEffect = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "InkHitEffect");
            var slashEffect = player.GetComponentsInChildren<Component>()
                .Single(component => component.GetType().Name == "InkSlashEffect");
            var cameraShaker = Camera.main.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CameraShaker");
            var hitStop = Camera.main.GetComponents<Component>()
                .Single(component => component.GetType().Name == "HitStopController");
            var configureArguments = new object[]
            {
                player,
                inkPool,
                inkEffect,
                slashEffect,
                cameraShaker,
                hitStop
            };
            var handlerCountBefore = GetStaticEventHandlers(resolvedBackingField).Length;
            GameObject disposedObject = null;
            GameObject normalObject = null;
            Delegate disposedDelegate = null;
            Delegate normalDelegate = null;
            try
            {
                disposedObject = new GameObject("B2DisposedFeedbackControllerProbe");
                var disposedController = disposedObject.AddComponent(controllerType);
                disposedDelegate = Delegate.CreateDelegate(
                    resolvedEvent.EventHandlerType,
                    disposedController,
                    handle);
                Invoke(disposedController, "Dispose");
                Exception configureFailure = null;
                try
                {
                    configure.Invoke(disposedController, configureArguments);
                }
                catch (TargetInvocationException exception)
                {
                    configureFailure = exception.InnerException;
                }

                var handlersAfterRejectedConfigure = GetStaticEventHandlers(resolvedBackingField);
                Assert.That(
                    configureFailure,
                    Is.TypeOf<ObjectDisposedException>(),
                    "B2_TASK5_RED_DISPOSE_BEFORE_CONFIGURE_MUST_REJECT");
                Assert.That(
                    handlersAfterRejectedConfigure.Length,
                    Is.EqualTo(handlerCountBefore),
                    "A rejected Configure must not add an OnHitResolved handler.");
                Assert.That(
                    handlersAfterRejectedConfigure.Any(handler =>
                        ReferenceEquals(handler.Target, disposedController)),
                    Is.False,
                    "A disposed controller must never be retained by the static event.");

                UnityEngine.Object.DestroyImmediate(disposedObject);
                disposedObject = null;

                normalObject = new GameObject("B2NormalFeedbackControllerProbe");
                var normalController = normalObject.AddComponent(controllerType);
                normalDelegate = Delegate.CreateDelegate(
                    resolvedEvent.EventHandlerType,
                    normalController,
                    handle);
                configure.Invoke(normalController, configureArguments);
                Assert.That(
                    GetStaticEventHandlers(resolvedBackingField).Length,
                    Is.EqualTo(handlerCountBefore + 1));
                Invoke(normalController, "Dispose");
                Assert.That(
                    GetStaticEventHandlers(resolvedBackingField).Length,
                    Is.EqualTo(handlerCountBefore),
                    "Normal Configure/Dispose must remove its static subscription exactly once.");
                Invoke(normalController, "Dispose");
                Assert.That(GetStaticEventHandlers(resolvedBackingField).Length, Is.EqualTo(handlerCountBefore));
            }
            finally
            {
                if (disposedDelegate != null)
                {
                    resolvedEvent.RemoveEventHandler(null, disposedDelegate);
                }
                if (normalDelegate != null)
                {
                    resolvedEvent.RemoveEventHandler(null, normalDelegate);
                }
                if (disposedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(disposedObject);
                }
                if (normalObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(normalObject);
                }
            }

            Assert.That(GetStaticEventHandlers(resolvedBackingField).Length, Is.EqualTo(handlerCountBefore));
        }

        [UnityTest]
        public IEnumerator ResolvedParryPublishesOnceWithoutDamageOrLegacyDamageCallbacks()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var stateMachine = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "PlayerStateMachine");
            var hurtbox = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Hurtbox");
            var assembly = hurtbox.GetType().Assembly;
            var eventsType = assembly.GetType("CombatEvents");
            var resolvedEvent = eventsType.GetEvent("OnHitResolved", BindingFlags.Static | BindingFlags.Public);
            var contextType = assembly.GetType("CombatFeedbackContext");
            var captureMethod = typeof(BattleEnemyExperienceTests).GetMethod(
                    nameof(CaptureResolvedHit),
                    BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(contextType);
            var resolvedProbe = Delegate.CreateDelegate(resolvedEvent.EventHandlerType, captureMethod);
            var hitLandedEvent = eventsType.GetEvent("OnHitLanded", BindingFlags.Static | BindingFlags.Public);
            var damageTakenEvent = eventsType.GetEvent("OnDamageTaken", BindingFlags.Static | BindingFlags.Public);
            var parryEvent = eventsType.GetEvent("OnParrySuccess", BindingFlags.Static | BindingFlags.Public);
            var hitCount = 0;
            var damageCount = 0;
            var parryCount = 0;
            Action<Vector3, int> hitProbe = (_, __) => hitCount++;
            Action<Vector3, int> damageProbe = (_, __) => damageCount++;
            Action<Vector3> parryProbe = _ => parryCount++;
            _resolvedHitCount = 0;
            _lastResolvedHit = null;
            resolvedEvent.AddEventHandler(null, resolvedProbe);
            hitLandedEvent.AddEventHandler(null, hitProbe);
            damageTakenEvent.AddEventHandler(null, damageProbe);
            parryEvent.AddEventHandler(null, parryProbe);
            try
            {
                Invoke(stateMachine, "RequestParry");
                var hpBefore = (int)GetField(stats, "currentHp");
                var resolver = assembly.GetType("CombatHitResolver")
                    .GetMethod("ResolveAndPublish", BindingFlags.Static | BindingFlags.Public);

                var outcome = (CombatHitOutcome)resolver.Invoke(null, new object[]
                {
                    hurtbox,
                    new CombatHit(25, -1f, 4f, true, null),
                    null,
                    Enum.Parse(assembly.GetType("CombatFeedbackSourceKind"), "EnemyMelee"),
                    Enum.Parse(assembly.GetType("CombatFeedbackStrength"), "Heavy"),
                    -1
                });

                Assert.That(outcome.Result, Is.EqualTo(CombatHitResult.Parried));
                Assert.That(outcome.AppliedDamage, Is.Zero);
                Assert.That((int)GetField(stats, "currentHp"), Is.EqualTo(hpBefore));
                Assert.That(_resolvedHitCount, Is.EqualTo(1));
                Assert.That(ReadRuntimeProperty(_lastResolvedHit, "Result"), Is.EqualTo(CombatHitResult.Parried));
                Assert.That(parryCount, Is.EqualTo(1), "PlayerStateMachine remains the sole legacy parry publisher.");
                Assert.That(hitCount, Is.Zero);
                Assert.That(damageCount, Is.Zero);
                Assert.That(
                    Resources.FindObjectsOfTypeAll<Component>()
                        .Where(component => component != null && component.GetType().Name == "DamageNumber")
                        .Count(component =>
                        {
                            var text = component.GetComponentInChildren<TextMesh>(true);
                            return component.gameObject.activeInHierarchy &&
                                text != null &&
                                text.text.Contains("\u5f39\u53cd");
                        }),
                    Is.EqualTo(1),
                    "Resolved parry must create exactly one presentation number.");
            }
            finally
            {
                resolvedEvent.RemoveEventHandler(null, resolvedProbe);
                hitLandedEvent.RemoveEventHandler(null, hitProbe);
                damageTakenEvent.RemoveEventHandler(null, damageProbe);
                parryEvent.RemoveEventHandler(null, parryProbe);
            }
        }

        [UnityTest]
        public IEnumerator PlayerDamageMustTriggerItsInstalledHitFlash()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var sprite = player.GetComponent<SpriteRenderer>();
            var original = Color.blue;
            sprite.color = original;
            var hurtbox = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Hurtbox");

            var assembly = hurtbox.GetType().Assembly;
            var resolver = assembly.GetType("CombatHitResolver");
            resolver.GetMethod("ResolveAndPublish", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[]
                {
                    hurtbox,
                    new CombatHit(7, 1f, 0f, false, null),
                    null,
                    Enum.Parse(assembly.GetType("CombatFeedbackSourceKind"), "EnemyMelee"),
                    Enum.Parse(assembly.GetType("CombatFeedbackStrength"), "Light"),
                    1
                });
            yield return null;

            Assert.That(
                sprite.color,
                Is.EqualTo(Color.white),
                "B2_RED_PLAYER_FLASH: resolved player damage must trigger its installed HitEffectPlayer.");
            Assert.That(sprite.color, Is.Not.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator RepeatedHitFlashRestoresTheColorFromBeforeTheFirstFlash()
        {
            yield return LoadBattleScene();
            var player = GameObject.Find("Player");
            var runtimeAssembly = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Hurtbox")
                .GetType()
                .Assembly;
            var probe = new GameObject("B2RepeatedHitFlashProbe");
            try
            {
                var sprite = probe.AddComponent<SpriteRenderer>();
                sprite.color = Color.blue;
                var hitEffect = probe.AddComponent(runtimeAssembly.GetType("HitEffectPlayer"));

                Invoke(hitEffect, "PlayHitEffect");
                yield return null;
                Invoke(hitEffect, "PlayHitEffect");
                yield return new WaitForSeconds(0.15f);

                Assert.That(
                    sprite.color,
                    Is.EqualTo(Color.blue),
                    "B2_TASK5_RED_REPEATED_FLASH_RESTORE: repeated hits must not capture white as the original color.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [UnityTest]
        public IEnumerator InkParticlePoolMustInitializeExactlyOnce()
        {
            yield return LoadBattleScene();
            var player = GameObject.Find("Player");
            var gameAssembly = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Hurtbox")
                .GetType()
                .Assembly;
            var type = gameAssembly.GetType("InkParticlePool");
            var instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null) as Component;
            try
            {
                var all = (ICollection)GetField(instance, "_allParticles");
                var poolSize = (int)GetField(instance, "poolSize");
                Assert.That(
                    all.Count,
                    Is.EqualTo(poolSize),
                    "B2_RED_INK_POOL_ONCE: scene pool must allocate exactly poolSize particles.");
                Assert.That(
                    instance.gameObject.scene,
                    Is.EqualTo(SceneManager.GetActiveScene()),
                    "B2_RED_INK_POOL_SCENE_OWNER: the active BattleScene must own its ink pool.");
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator InkParticlePoolRejectsStaleReturnsAfterReuseAndClear()
        {
            yield return LoadBattleScene();
            var pool = FindActiveSceneComponent("InkParticlePool");
            var poolSize = (int)GetField(pool, "poolSize");
            var handles = new List<object>();
            for (var index = 0; index < poolSize; index++)
            {
                handles.Add(Invoke(pool, "Get"));
            }

            var stale = handles[0];
            var replacement = Invoke(pool, "Get");
            Assert.That(
                (bool)Invoke(pool, "Return", stale),
                Is.False,
                "A reused slot must reject its prior generation.");
            Assert.That((bool)Invoke(pool, "Return", replacement), Is.True);

            var activeBeforeClear = handles[1];
            Invoke(pool, "ClearAll");
            Assert.That(
                (bool)Invoke(pool, "Return", activeBeforeClear),
                Is.False,
                "InvalidateAll must reject every delayed return from the prior scene state.");
            var available = ((IEnumerable)GetField(pool, "_available")).Cast<object>().ToArray();
            Assert.That(available, Has.Length.EqualTo(poolSize));
            Assert.That(available.Distinct().Count(), Is.EqualTo(poolSize));
        }

        [UnityTest]
        public IEnumerator TerminalCompletionClearsTransientFeedbackBeforeBattleResultFreeze()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var run = FindActiveSceneComponent("BattleRunController");
            var timeController = FindActiveSceneComponent("BattleTimeController");
            var pool = FindActiveSceneComponent("InkParticlePool");
            var grunt = CreateEnemyProbe("Grunt", "B2TerminalFeedbackProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                var assembly = grunt.GetType().Assembly;
                grunt.gameObject.AddComponent(assembly.GetType("HitEffectPlayer"));
                var sprite = grunt.GetComponent<SpriteRenderer>();
                sprite.color = Color.blue;
                var hurtbox = grunt.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Hurtbox");
                var resolver = assembly.GetType("CombatHitResolver")
                    .GetMethod("ResolveAndPublish", BindingFlags.Static | BindingFlags.Public);
                resolver.Invoke(null, new object[]
                {
                    hurtbox,
                    new CombatHit(5, 1f, 0f, false, null),
                    player,
                    Enum.Parse(assembly.GetType("CombatFeedbackSourceKind"), "PlayerMelee"),
                    Enum.Parse(assembly.GetType("CombatFeedbackStrength"), "Light"),
                    1
                });

                var particles = ((IEnumerable)GetField(pool, "_allParticles"))
                    .Cast<GameObject>()
                    .ToArray();
                var slash = player.GetComponentInChildren<LineRenderer>();
                Assert.That(particles.Count(particle => particle.activeSelf), Is.GreaterThan(0));
                Assert.That(slash.enabled, Is.True);
                Assert.That(sprite.color, Is.EqualTo(Color.white));

                Invoke(run, "Complete", BattleRunOutcome.Victory);

                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(particles.Count(particle => particle.activeSelf), Is.Zero);
                Assert.That(slash.enabled, Is.False);
                Assert.That(sprite.color, Is.EqualTo(Color.blue));
                Assert.That((int)GetProperty(timeController, "ActiveRequestCount"), Is.EqualTo(1),
                    "Terminal cleanup must release hit-stop before installing the BattleResult request.");
            }
            finally
            {
                Invoke(run, "Dispose");
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator BossTelegraphFreezesBeforeTheNextPhysicsStep()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var originalHp = (int)GetField(stats, "currentHp");
            var boss = CreateEnemyProbe("Boss", "B2BossTelegraphDriftProbe");
            try
            {
                ((Behaviour)boss).enabled = false;
                boss.transform.position = player.transform.position + new Vector3(-2f, 0f, 0f);
                SetField(boss, "_player", player.transform);
                SetField(boss, "_attackPattern", 1);
                Invoke(boss, "FacePlayer");
                var body = boss.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.velocity = new Vector2(5f, 0f);
                Physics2D.SyncTransforms();

                Assert.That((bool)Invoke(boss, "TryStartPreparedAttack"), Is.True);
                Assert.That(GetProperty(boss, "CurrentAttackPhase").ToString(), Is.EqualTo("Telegraph"));
                var plan = (EnemyAttackPlan)GetProperty(boss, "CurrentAttackPlan");
                var line = ((Component)GetField(boss, "_telegraphView"))
                    .GetComponentInChildren<LineRenderer>();
                var beforePhysics = Enumerable.Range(0, line.positionCount)
                    .Select(index => line.transform.TransformPoint(line.GetPosition(index)))
                    .ToArray();

                yield return new WaitForFixedUpdate();

                var afterPhysics = Enumerable.Range(0, line.positionCount)
                    .Select(index => line.transform.TransformPoint(line.GetPosition(index)))
                    .ToArray();
                Assert.That(
                    afterPhysics.Zip(beforePhysics, Vector3.Distance).Max(),
                    Is.LessThan(0.001f),
                    "B2_WHOLE_REVIEW_RED_TELEGRAPH_DRIFT: Telegraph world geometry must freeze before the next physics step.");

                var hpBefore = (int)GetField(stats, "currentHp");
                var strength = Enum.Parse(boss.GetType().Assembly.GetType("CombatFeedbackStrength"), "Heavy");
                Invoke(boss, "ResolvePlanHit", plan, strength);
                Assert.That((int)GetField(stats, "currentHp"), Is.LessThan(hpBefore),
                    "The eventual hit must still consume the frozen Telegraph footprint.");
            }
            finally
            {
                Invoke(boss, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(boss.gameObject);
                SetField(stats, "currentHp", originalHp);
            }
        }

        [UnityTest]
        public IEnumerator BossChargeResolutionKeepsTheTelegraphedWorldFootprintAfterMovement()
        {
            yield return VerifyBossPreparedHitUsesTelegraphedWorldOrigin(
                1,
                new Vector2(0.5f, 0f),
                new Vector2(3f, 0f),
                "B2_WHOLE_REVIEW_RED_BOSS_CHARGE_ORIGIN");
        }

        [UnityTest]
        public IEnumerator BossSlamResolutionKeepsTheTelegraphedWorldFootprintAfterMovement()
        {
            yield return VerifyBossPreparedHitUsesTelegraphedWorldOrigin(
                2,
                new Vector2(1.5f, 0f),
                new Vector2(0f, 3f),
                "B2_WHOLE_REVIEW_RED_BOSS_SLAM_ORIGIN");
        }

        private static IEnumerator VerifyBossPreparedHitUsesTelegraphedWorldOrigin(
            int attackPattern,
            Vector2 playerOffset,
            Vector2 bossMovement,
            string failureMarker)
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var playerBody = player.GetComponent<Rigidbody2D>();
            var originalPlayerPosition = player.transform.position;
            var originalGravityScale = playerBody.gravityScale;
            var originalHp = (int)GetField(stats, "currentHp");
            var boss = CreateEnemyProbe("Boss", $"B2BossOriginProbe{attackPattern}");
            try
            {
                ((Behaviour)boss).enabled = false;
                var attackOrigin = new Vector3(0f, 4f, 0f);
                boss.transform.position = attackOrigin;
                var bossBody = boss.GetComponent<Rigidbody2D>();
                bossBody.gravityScale = 0f;
                bossBody.velocity = Vector2.zero;
                player.transform.position = attackOrigin + (Vector3)playerOffset;
                playerBody.gravityScale = 0f;
                playerBody.velocity = Vector2.zero;
                SetField(boss, "_player", player.transform);
                SetField(boss, "_attackPattern", attackPattern);
                Invoke(boss, "FacePlayer");
                Physics2D.SyncTransforms();

                Assert.That((bool)Invoke(boss, "TryStartPreparedAttack"), Is.True);
                var plan = (EnemyAttackPlan)GetProperty(boss, "CurrentAttackPlan");
                var advertisedCenter = (Vector2)boss.transform.TransformPoint(plan.LocalOffset);
                Assert.That(
                    IsInsidePlanFootprint((Vector2)player.transform.position, advertisedCenter, plan),
                    Is.True,
                    "The player probe must begin inside the geometry shown during Telegraph.");

                boss.transform.position += (Vector3)bossMovement;
                Physics2D.SyncTransforms();
                var hpBefore = (int)GetField(stats, "currentHp");
                var strength = Enum.Parse(boss.GetType().Assembly.GetType("CombatFeedbackStrength"), "Heavy");
                Invoke(boss, "ResolvePlanHit", plan, strength);

                Assert.That(
                    (int)GetField(stats, "currentHp"),
                    Is.LessThan(hpBefore),
                    $"{failureMarker}: Commit movement must not move damage away from the telegraphed world footprint.");
            }
            finally
            {
                Invoke(boss, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(boss.gameObject);
                SetField(stats, "currentHp", originalHp);
                player.transform.position = originalPlayerPosition;
                playerBody.gravityScale = originalGravityScale;
                playerBody.velocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
        }

        private static bool IsInsidePlanFootprint(
            Vector2 point,
            Vector2 center,
            EnemyAttackPlan plan)
        {
            if (plan.Shape == EnemyTelegraphShape.Circle)
            {
                return Vector2.Distance(point, center) <= plan.Radius;
            }

            var forward = plan.AimDirection.normalized;
            var perpendicular = new Vector2(-forward.y, forward.x);
            var delta = point - center;
            return Mathf.Abs(Vector2.Dot(delta, forward)) <= plan.Size.x * 0.5f &&
                   Mathf.Abs(Vector2.Dot(delta, perpendicular)) <= plan.Size.y * 0.5f;
        }

        [UnityTest]
        public IEnumerator TelegraphViewMatchesBoxAndBossCirclePlanBoundsWithoutCollider()
        {
            yield return LoadBattleScene();
            var runtimeAssembly = FindActiveSceneComponent("WaveSpawner").GetType().Assembly;
            var viewObject = new GameObject("B2TelegraphViewProbe");
            var view = viewObject.AddComponent(runtimeAssembly.GetType("AttackTelegraphView"));
            var boss = CreateEnemyProbe("Boss", "B2BossPlanProbe");
            try
            {
                ((Behaviour)boss).enabled = false;
                var box = EnemyAttackPlan.Box(
                    "box_probe",
                    0.1f,
                    0.1f,
                    0.1f,
                    true,
                    new Vector2(1f, 0.25f),
                    new Vector2(2f, 0.5f),
                    1,
                    Vector2.right,
                    1,
                    0f,
                    1,
                    1f);
                Invoke(view, "Show", box);

                Assert.That(GetProperty(view, "CurrentShape").ToString(), Is.EqualTo("Box"));
                Assert.That((Vector2)GetProperty(view, "RenderedLocalMin"),
                    Is.EqualTo(box.LocalOffset - box.Size * 0.5f));
                Assert.That((Vector2)GetProperty(view, "RenderedLocalMax"),
                    Is.EqualTo(box.LocalOffset + box.Size * 0.5f));
                Assert.That(viewObject.GetComponentInChildren<LineRenderer>().positionCount, Is.EqualTo(5));
                Assert.That(viewObject.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(box.IsParryable, Is.True, "Yellow Box plans must always be parryable.");

                SetField(boss, "_attackPattern", 3);
                var bossPlan = (EnemyAttackPlan)Invoke(boss, "PrepareAttackPlan");
                Assert.That(bossPlan.Shape, Is.EqualTo(EnemyTelegraphShape.Circle));
                Assert.That(bossPlan.IsParryable, Is.False, "Boss AoE is the only red attack plan.");
                Invoke(view, "Show", bossPlan);

                var radiusVector = Vector2.one * bossPlan.Radius;
                Assert.That((Vector2)GetProperty(view, "RenderedLocalMin"),
                    Is.EqualTo(bossPlan.LocalOffset - radiusVector));
                Assert.That((Vector2)GetProperty(view, "RenderedLocalMax"),
                    Is.EqualTo(bossPlan.LocalOffset + radiusVector));
                Assert.That(viewObject.GetComponentInChildren<LineRenderer>().positionCount, Is.EqualTo(33));
                Assert.That(viewObject.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(boss.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ArcherCommitUsesAimFrozenBeforeTelegraph()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            player.transform.position = new Vector3(0f, 8f, 0f);
            var playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.velocity = Vector2.zero;
            var archer = CreateEnemyProbe("Archer", "B2ArcherAimProbe");
            GameObject arrow = null;
            try
            {
                ((Behaviour)archer).enabled = false;
                archer.transform.position = player.transform.position + new Vector3(3f, -3f, 0f);
                archer.transform.rotation = Quaternion.Euler(0f, 0f, 37f);
                archer.transform.localScale = new Vector3(-1.75f, 0.6f, 1f);
                SetField(archer, "_player", player.transform);
                SetField(archer, "telegraphDuration", 0.03f);
                Invoke(archer, "FacePlayer");
                Assert.That((bool)Invoke(archer, "TryStartPreparedAttack"), Is.True);
                var frozenPlan = (EnemyAttackPlan)GetProperty(archer, "CurrentAttackPlan");
                Assert.That(frozenPlan.AimDirection.x, Is.LessThan(-0.7f));
                Assert.That(frozenPlan.AimDirection.y, Is.GreaterThan(0.7f));
                var telegraphView = (Component)GetField(archer, "_telegraphView");
                var line = telegraphView.GetComponentInChildren<LineRenderer>();
                var renderedWorldPoints = Enumerable.Range(0, line.positionCount)
                    .Select(index => (Vector2)line.transform.TransformPoint(line.GetPosition(index)))
                    .ToArray();
                var renderedLongAxis = (renderedWorldPoints[2] - renderedWorldPoints[1]).normalized;
                Assert.That(
                    Vector2.Dot(renderedLongAxis, frozenPlan.AimDirection),
                    Is.GreaterThan(0.999f),
                    "B2_TASK4_QUALITY_RED_WORLD_FOOTPRINT: world Box long axis must align with world AimDirection under transformed enemy roots.");
                Assert.That(
                    Vector2.Distance(renderedWorldPoints[1], renderedWorldPoints[2]),
                    Is.EqualTo(frozenPlan.Size.x).Within(0.001f),
                    "B2_TASK4_QUALITY_RED_WORLD_FOOTPRINT: world Box length must ignore enemy root scale.");
                var renderedWorldCenter = (renderedWorldPoints[0] + renderedWorldPoints[2]) * 0.5f;
                Assert.That(
                    renderedWorldCenter.x,
                    Is.EqualTo(archer.transform.position.x + frozenPlan.AimDirection.x * 1.5f).Within(0.001f));
                Assert.That(
                    renderedWorldCenter.y,
                    Is.EqualTo(archer.transform.position.y + frozenPlan.AimDirection.y * 1.5f).Within(0.001f),
                    "B2_TASK4_QUALITY_RED_WORLD_FOOTPRINT: Archer local offset must resolve to its world aim channel.");
                var renderedPoints = Enumerable.Range(0, line.positionCount)
                    .Select(index => (Vector2)line.GetPosition(index))
                    .ToArray();
                var renderedMin = new Vector2(
                    renderedPoints.Min(point => point.x),
                    renderedPoints.Min(point => point.y));
                var renderedMax = new Vector2(
                    renderedPoints.Max(point => point.x),
                    renderedPoints.Max(point => point.y));
                Assert.That((Vector2)GetProperty(telegraphView, "RenderedLocalMin"), Is.EqualTo(renderedMin));
                Assert.That((Vector2)GetProperty(telegraphView, "RenderedLocalMax"), Is.EqualTo(renderedMax));

                player.transform.position = archer.transform.position + new Vector3(3f, 0f, 0f);
                yield return WaitForAttackPhase(archer, "Commit", 120);
                arrow = GameObject.Find("Arrow");
                Assert.That(arrow, Is.Not.Null);
                var projectile = arrow.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Projectile");
                var launchedDirection = (Vector2)GetField(projectile, "_direction");
                Assert.That(launchedDirection.x, Is.EqualTo(frozenPlan.AimDirection.x).Within(0.0001f));
                Assert.That(launchedDirection.y, Is.EqualTo(frozenPlan.AimDirection.y).Within(0.0001f));
                Assert.That((int)GetField(projectile, "damage"), Is.EqualTo(frozenPlan.Damage));
                Assert.That((bool)GetField(projectile, "isParryable"), Is.EqualTo(frozenPlan.IsParryable));

                yield return WaitForAttackPhaseWithinRealtime(archer, "Complete", 2f);
                Assert.That(arrow == null, Is.False,
                    "A normally completed Archer attack must not destroy its flying projectile.");
                Invoke(archer, "CancelCombatActions");
                yield return null;
                Assert.That(arrow == null, Is.True,
                    "B2_TASK4_QUALITY_RED_PROJECTILE_OWNERSHIP: Archer cancellation must destroy its detached projectile.");
            }
            finally
            {
                Invoke(archer, "CancelCombatActions");
                if (arrow != null)
                {
                    UnityEngine.Object.DestroyImmediate(arrow);
                }
                UnityEngine.Object.DestroyImmediate(archer.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ArcherProjectileUsesNonDefaultFrozenKnockbackPlan()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var playerBody = player.GetComponent<Rigidbody2D>();
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var stateMachine = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "PlayerStateMachine");
            var archer = CreateEnemyProbe("Archer", "B2ArcherKnockbackProbe");
            GameObject arrow = null;
            try
            {
                ((Behaviour)archer).enabled = false;
                var plan = EnemyAttackPlan.Box(
                    "archer_knockback_probe",
                    0f,
                    0.1f,
                    0f,
                    false,
                    Vector2.right,
                    new Vector2(2f, 0.5f),
                    1,
                    Vector2.right,
                    1,
                    0f,
                    11,
                    7.25f);
                var execution = (IEnumerator)Invoke(archer, "ExecuteAttackPlan", plan);
                execution.MoveNext();
                arrow = GameObject.Find("Arrow");
                Assert.That(arrow, Is.Not.Null);
                var projectile = arrow.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Projectile");
                var launchDamageField = projectile.GetType().GetField(
                    "_launchDamage",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var launchParryableField = projectile.GetType().GetField(
                    "_launchIsParryable",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var knockbackField = projectile.GetType().GetField(
                    "_launchKnockbackForce",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(
                    launchDamageField,
                    Is.Not.Null,
                    "B2_TASK4_QUALITY_RED_PRIVATE_PROJECTILE_SNAPSHOT: Projectile must privately freeze launch damage.");
                Assert.That(launchParryableField, Is.Not.Null,
                    "B2_TASK4_QUALITY_RED_PRIVATE_PROJECTILE_SNAPSHOT: Projectile must privately freeze launch parryability.");
                Assert.That(knockbackField, Is.Not.Null,
                    "B2_TASK4_QUALITY_RED_PRIVATE_PROJECTILE_SNAPSHOT: Projectile must privately freeze launch knockback.");
                Assert.That((int)launchDamageField.GetValue(projectile), Is.EqualTo(plan.Damage));
                Assert.That((bool)launchParryableField.GetValue(projectile), Is.EqualTo(plan.IsParryable));
                Assert.That((float)knockbackField.GetValue(projectile), Is.EqualTo(7.25f).Within(0.0001f));

                SetField(projectile, "damage", 999);
                SetField(projectile, "isParryable", true);
                playerBody.gravityScale = 0f;
                playerBody.velocity = Vector2.zero;
                Physics2D.SyncTransforms();
                Invoke(stateMachine, "RequestParry");
                var hpBefore = (int)GetField(stats, "currentHp");
                Invoke(projectile, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
                Assert.That((int)GetField(stats, "currentHp"), Is.EqualTo(hpBefore - plan.Damage),
                    "B2_TASK4_QUALITY_RED_PRIVATE_PROJECTILE_SNAPSHOT: contact must ignore mutable public launch defaults.");
                Assert.That(
                    playerBody.velocity.x,
                    Is.EqualTo(7.25f / playerBody.mass).Within(0.05f),
                    "B2_TASK4_SPEC_RED_PROJECTILE_KNOCKBACK: player impulse must consume the frozen non-default plan value.");
            }
            finally
            {
                if (arrow != null)
                {
                    UnityEngine.Object.DestroyImmediate(arrow);
                }
                UnityEngine.Object.DestroyImmediate(archer.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DiagonalBoxPhysicalQueryUsesTheSameFrozenAimRotation()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var grunt = CreateEnemyProbe("Grunt", "B2DiagonalPhysicsProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                var aim = new Vector2(1f, 1f).normalized;
                grunt.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                grunt.transform.localScale = new Vector3(-2f, 0.25f, 1f);
                grunt.transform.position = player.transform.position - (Vector3)(aim * 1.2f);
                var plan = EnemyAttackPlan.Box(
                    "diagonal_physics_probe",
                    0f,
                    0.1f,
                    0f,
                    false,
                    Vector2.zero,
                    new Vector2(3f, 0.4f),
                    1,
                    aim,
                    1,
                    0f,
                    9,
                    1f);
                Physics2D.SyncTransforms();
                var hpBefore = (int)GetField(stats, "currentHp");

                Invoke(grunt, "ResolvePlanHit", plan);

                Assert.That(
                    (int)GetField(stats, "currentHp"),
                    Is.LessThan(hpBefore),
                    "B2_TASK4_QUALITY_RED_WORLD_PHYSICS: world Box query must ignore transformed enemy root axes and scale.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator NonlethalDamagePreservesKnockbackAfterOwnedAttackCancellation()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var grunt = CreateEnemyProbe("Grunt", "B2NonlethalKnockbackProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                SetField(grunt, "_player", player.transform);
                SetField(grunt, "telegraphDuration", 1f);
                Assert.That((bool)Invoke(grunt, "TryStartPreparedAttack"), Is.True);
                var body = grunt.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.velocity = Vector2.zero;

                Invoke(grunt, "TakeDamage", 1, 1f, 6.5f);

                Assert.That(GetProperty(grunt, "CurrentAttackPhase").ToString(), Is.EqualTo("Complete"));
                Assert.That(GetProperty(grunt, "CurrentState").ToString(), Is.EqualTo("Hurt"));
                Assert.That(
                    body.velocity.x,
                    Is.EqualTo(6.5f / body.mass).Within(0.05f),
                    "B2_TASK4_QUALITY_RED_NONLETHAL_KNOCKBACK: cancellation must happen before the surviving hit impulse.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator LethalDamagePreservesKnockbackAfterOwnedAttackCancellation()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var grunt = CreateEnemyProbe("Grunt", "B2LethalKnockbackProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                SetField(grunt, "_player", player.transform);
                SetField(grunt, "telegraphDuration", 1f);
                SetField(grunt, "expValue", 0);
                Assert.That((bool)Invoke(grunt, "TryStartPreparedAttack"), Is.True);
                var body = grunt.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.velocity = Vector2.zero;

                Invoke(grunt, "TakeDamage", 100000, -1f, 6.5f);

                Assert.That(GetProperty(grunt, "CurrentAttackPhase").ToString(), Is.EqualTo("Complete"));
                Assert.That(GetProperty(grunt, "CurrentState").ToString(), Is.EqualTo("Die"));
                Assert.That(
                    body.velocity.x,
                    Is.EqualTo(-6.5f / body.mass).Within(0.05f),
                    "B2_TASK4_QUALITY_RED_LETHAL_KNOCKBACK: lethal cancellation must preserve the legacy hit impulse.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PlayerHealthChangesOnlyAfterTelegraphCommits()
        {
            yield return LoadBattleScene();
            DisableActiveEnemyAutomation();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var grunt = CreateEnemyProbe("Grunt", "B2CommitTimingProbe");
            try
            {
                ((Behaviour)grunt).enabled = false;
                grunt.transform.position = player.transform.position - new Vector3(0.6f, 0.2f, 0f);
                SetField(grunt, "_player", player.transform);
                SetField(grunt, "telegraphDuration", 0.6f);
                SetField(grunt, "attackDuration", 0.6f);
                Invoke(grunt, "FacePlayer");
                Physics2D.SyncTransforms();
                var hpBefore = (int)GetField(stats, "currentHp");

                Assert.That((bool)Invoke(grunt, "TryStartPreparedAttack"), Is.True);
                Assert.That(GetProperty(grunt, "CurrentAttackPhase").ToString(), Is.EqualTo("Telegraph"));
                yield return new WaitForSeconds(0.05f);
                Assert.That(GetProperty(grunt, "CurrentAttackPhase").ToString(), Is.EqualTo("Telegraph"));
                Assert.That((int)GetField(stats, "currentHp"), Is.EqualTo(hpBefore),
                    "Telegraph must be informative only and never deal early damage.");

                yield return WaitForAttackPhaseWithinRealtime(grunt, "Commit", 2f);
                Assert.That((int)GetField(stats, "currentHp"), Is.LessThan(hpBefore),
                    "The shared plan may resolve damage only after entering Commit.");
            }
            finally
            {
                Invoke(grunt, "CancelCombatActions");
                UnityEngine.Object.DestroyImmediate(grunt.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ParryHurtDieAndPrepareForSpawnCancelOwnedComboWithoutLaterHits()
        {
            yield return LoadBattleScene();
            var player = GameObject.Find("Player");
            var stats = player.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            var maxHp = (int)GetField(stats, "maxHp");
            var interruptionNames = new[] { "Parry", "Hurt", "Die", "PrepareForSpawn" };
            foreach (var interruptionName in interruptionNames)
            {
                SetField(stats, "currentHp", maxHp);
                var elite = CreateEnemyProbe("Elite", $"B2Cancel{interruptionName}");
                try
                {
                    ((Behaviour)elite).enabled = false;
                    elite.transform.position = player.transform.position - new Vector3(0.7f, 0.2f, 0f);
                    SetField(elite, "_player", player.transform);
                    SetField(elite, "heavyAttackChance", 0f);
                    SetField(elite, "telegraphDuration", 0.01f);
                    SetField(elite, "comboCount", 3);
                    SetField(elite, "comboInterval", 0.08f);
                    Invoke(elite, "FacePlayer");
                    Physics2D.SyncTransforms();
                    var hpBefore = (int)GetField(stats, "currentHp");
                    Assert.That((bool)Invoke(elite, "TryStartPreparedAttack"), Is.True);
                    yield return WaitForAttackPhase(elite, "Commit", 120);
                    Assert.That((int)GetField(stats, "currentHp"), Is.LessThan(hpBefore));

                    switch (interruptionName)
                    {
                        case "Parry":
                            Invoke(elite, "OnParried");
                            break;
                        case "Hurt":
                            Invoke(elite, "TakeDamage", 1, 0f, 0f);
                            break;
                        case "Die":
                            Invoke(elite, "TakeDamage", 100000, 0f, 0f);
                            break;
                        default:
                            var baseline = (EnemyStatBaseline)GetProperty(elite, "Baseline");
                            Invoke(
                                elite,
                                "PrepareForSpawn",
                                new EnemyWaveStats(baseline.MaxHp, baseline.Damage, baseline.MoveSpeed));
                            ((Behaviour)elite).enabled = false;
                            break;
                    }

                    var hpAfterCancellation = (int)GetField(stats, "currentHp");
                    Assert.That(GetProperty(elite, "CurrentAttackPhase").ToString(), Is.EqualTo("Complete"));
                    Assert.That(GetField(elite, "_attackRoutine"), Is.Null);
                    Assert.That(GetProperty(GetField(elite, "_telegraphView"), "IsVisible"), Is.False);
                    yield return new WaitForSeconds(0.25f);
                    Assert.That((int)GetField(stats, "currentHp"), Is.EqualTo(hpAfterCancellation),
                        $"{interruptionName} must prevent every queued combo hit.");
                }
                finally
                {
                    if (elite != null)
                    {
                        UnityEngine.Object.DestroyImmediate(elite.gameObject);
                    }
                }
            }
        }

        private static IEnumerator VerifyForcedSpawn(ArenaSpawnSide preferredSide)
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            var player = GameObject.Find("Player");
            for (var step = 0; step < 10; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Camera.main.aspect = 16f / 9f;
            var pool = FindActiveSceneComponent("ObjectPool");
            var aliveBeforeForcedSpawn = (IList)GetField(spawner, "_aliveEnemies");
            foreach (var existing in aliveBeforeForcedSpawn.Cast<GameObject>().ToList())
            {
                var existingEnemy = existing.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Grunt");
                Invoke(spawner, "UnbindDeathHandler", existingEnemy);
                Invoke(pool, "Return", "grunt", existing);
            }
            aliveBeforeForcedSpawn.Clear();

            var waves = (Array)GetField(spawner, "waves");
            var firstWave = waves.GetValue(0);
            var entries = (Array)firstWave.GetType().GetField("enemies").GetValue(firstWave);
            var entry = entries.GetValue(0);
            entry.GetType().GetField("enemyType").SetValue(entry, "grunt");
            entry.GetType().GetField("preferredSide").SetValue(entry, preferredSide);

            Invoke(spawner, "SpawnEnemy", entry);
            var alive = ((IEnumerable)GetField(spawner, "_aliveEnemies"))
                .Cast<GameObject>()
                .ToList();
            var spawned = alive[alive.Count - 1];
            var enemy = spawned.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Grunt");
            var chaseRange = (float)enemy.GetType()
                .GetField("chaseRange", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(enemy);
            var initialDistance = Mathf.Abs(
                spawned.transform.position.x - player.transform.position.x);
            var sideLabel = preferredSide.ToString().ToLowerInvariant();
            var reachabilityMarker = preferredSide == ArenaSpawnSide.Right
                ? "B2_RED_FORCED_RIGHT_REACHABILITY"
                : "B2_FORCED_LEFT_REACHABILITY";
            var effectiveRangeMarker = preferredSide == ArenaSpawnSide.Right
                ? "B2_RED_FORCED_RIGHT_EFFECTIVE_RANGE"
                : "B2_FORCED_LEFT_EFFECTIVE_RANGE";

            Assert.That(
                initialDistance,
                Is.LessThanOrEqualTo(chaseRange + 0.001f),
                $"{reachabilityMarker}: forced-{sideLabel} Grunt must spawn within chase range.");
            Assert.That(
                Vector2.Distance(spawned.transform.position, player.transform.position),
                Is.LessThanOrEqualTo(chaseRange),
                $"{effectiveRangeMarker}: forced-{sideLabel} Grunt must enter its real 2D chase range.");
            if (preferredSide == ArenaSpawnSide.Right)
            {
                Assert.That(spawned.transform.position.x, Is.GreaterThan(player.transform.position.x),
                    "Forced-right spawn must stay on the requested side when that side is valid.");
            }
            else
            {
                Assert.That(spawned.transform.position.x, Is.LessThan(player.transform.position.x),
                    "Forced-left spawn must stay on the requested side when that side is valid.");
            }

            var finalDistance = initialDistance;
            for (var step = 0; step < 30 && finalDistance >= initialDistance - 0.1f; step++)
            {
                yield return new WaitForFixedUpdate();
                finalDistance = Mathf.Abs(spawned.transform.position.x - player.transform.position.x);
            }

            Assert.That(finalDistance, Is.LessThan(initialDistance - 0.1f),
                $"A reachable forced-{sideLabel} Grunt must reduce horizontal distance over real AI frames.");
        }

        [UnityTest]
        public IEnumerator CameraKeepsPlayerVisibleAndClampsInsideGround()
        {
            yield return LoadBattleScene();
            var player = GameObject.Find("Player");
            player.transform.position = new Vector3(12f, player.transform.position.y, 0f);
            yield return null;
            yield return null;

            var camera = Camera.main;
            var viewport = camera.WorldToViewportPoint(player.transform.position);
            Assert.That(
                viewport.x,
                Is.InRange(0f, 1f),
                "B2_RED_CAMERA_VISIBILITY: camera must keep the player inside the horizontal viewport.");
            Assert.That(camera.transform.parent, Is.Not.Null,
                "B2_RED_CAMERA_VISIBILITY: Main Camera must be owned by a scene camera rig.");
            Assert.That(camera.transform.parent.name, Is.EqualTo("[BattleCameraRig]"),
                "B2_RED_CAMERA_VISIBILITY: Main Camera must be parented under [BattleCameraRig].");

            var halfWidth = camera.orthographicSize * camera.aspect;
            Assert.That(camera.transform.parent.position.x,
                Is.InRange(-15f + halfWidth, 15f - halfWidth),
                "Camera rig center must remain inside the ground-derived clamp.");

            player.transform.position = new Vector3(-12f, player.transform.position.y, 0f);
            yield return null;
            yield return null;
            viewport = camera.WorldToViewportPoint(player.transform.position);
            Assert.That(viewport.x, Is.InRange(0f, 1f),
                "Camera must keep the player visible at the left ground edge too.");
        }

        [UnityTest]
        public IEnumerator TerminalAndRestartReplaceCameraRigAndFollowTarget()
        {
            yield return LoadBattleScene();
            var oldRig = FindActiveSceneComponent("BattleCameraRig");
            var oldRun = FindActiveSceneComponent("BattleRunController");
            var oldPlayer = GameObject.Find("Player");
            var oldRigId = oldRig.GetInstanceID();
            Assert.That(GetField(oldRig, "_target"), Is.SameAs(oldPlayer.transform));
            Assert.That(GetProperty(oldRig, "IsFollowing"), Is.True);

            var stats = oldPlayer.GetComponents<Component>()
                .Single(component => component.GetType().Name == "CharacterStats");
            Invoke(stats, "TakeDamage", 100000);
            Assert.That(GetProperty(oldRig, "IsFollowing"), Is.False,
                "Terminal completion must stop CameraRig before battle time is frozen.");
            var terminalRigPosition = oldRig.transform.position;
            oldPlayer.transform.position = new Vector3(12f, oldPlayer.transform.position.y, 0f);
            yield return null;
            Assert.That(oldRig.transform.position, Is.EqualTo(terminalRigPosition),
                "A terminal CameraRig must preserve the final composition.");

            Invoke(oldRun, "Restart");
            Component newRig = null;
            yield return WaitForFreshActiveSceneComponent(
                "BattleCameraRig",
                oldRigId,
                found => newRig = found);
            yield return WaitForApplicationReady();
            yield return WaitForSceneTransitionComplete();

            var newPlayer = GameObject.Find("Player");
            Assert.That(oldRig == null, Is.True,
                "Restart must destroy the prior scene CameraRig.");
            Assert.That(oldPlayer == null, Is.True,
                "Restart must destroy the prior CameraRig follow target.");
            Assert.That(newRig, Is.Not.Null);
            Assert.That(newRig.GetInstanceID(), Is.Not.EqualTo(oldRigId));
            Assert.That(GetField(newRig, "_target"), Is.SameAs(newPlayer.transform),
                "The replacement CameraRig must bind only the replacement Player.");
            Assert.That(GetProperty(newRig, "IsFollowing"), Is.True);
            Assert.That(
                Resources.FindObjectsOfTypeAll<Component>().Count(component =>
                    component != null &&
                    component.GetType().Name == "BattleCameraRig" &&
                    component.gameObject.scene == SceneManager.GetActiveScene()),
                Is.EqualTo(1),
                "Restart must create exactly one current-scene CameraRig.");
        }

        [UnityTest]
        public IEnumerator ObjectPoolReusePreparesTheSameBossBeforeItsFirstPhysicsStep()
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            var pool = FindActiveSceneComponent("ObjectPool");
            var assembly = spawner.GetType().Assembly;
            var bossType = assembly.GetType("Boss");
            var hitEffectType = assembly.GetType("HitEffectPlayer");
            var key = $"b2_task2_boss_{Guid.NewGuid():N}";
            GameObject leased = null;

            Func<GameObject> factory = () =>
            {
                var enemyObject = new GameObject("B2Task2Boss");
                enemyObject.SetActive(false);
                enemyObject.tag = "Enemy";
                enemyObject.AddComponent<SpriteRenderer>();
                var body = enemyObject.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                enemyObject.AddComponent<BoxCollider2D>();
                enemyObject.AddComponent(hitEffectType);
                enemyObject.AddComponent(bossType);
                return enemyObject;
            };

            try
            {
                Assert.That((bool)Invoke(pool, "Register", key, factory, 1), Is.True);
                var first = (GameObject)Invoke(pool, "Get", key);
                leased = first;
                var firstBoss = first.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Boss");
                var firstStats = new EnemyWaveStats(450, 41, 4.25f);
                Invoke(firstBoss, "PrepareForSpawn", firstStats);
                var baseline = (EnemyStatBaseline)GetProperty(firstBoss, "Baseline");
                var baselineColor = first.GetComponent<SpriteRenderer>().color;

                SetField(firstBoss, "hp", 1);
                Invoke(firstBoss, "EnterEnrage");
                SetField(firstBoss, "_attackPattern", 3);
                first.GetComponent<SpriteRenderer>().flipX = true;
                first.GetComponent<Collider2D>().enabled = false;
                first.GetComponent<Rigidbody2D>().velocity = new Vector2(9f, -4f);
                first.GetComponent<Rigidbody2D>().angularVelocity = 30f;
                var enemyStateType = assembly.GetType("EnemyState");
                Invoke(firstBoss, "ChangeState", Enum.Parse(enemyStateType, "Telegraph"));

                Invoke(pool, "Return", key, first);
                leased = null;
                var second = (GameObject)Invoke(pool, "Get", key);
                leased = second;
                Assert.That(second, Is.SameAs(first),
                    "The unique one-object pool must return the same Boss instance.");
                second.transform.position = new Vector3(5f, 1f, 0f);

                var secondBoss = second.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Boss");
                var expected = EnemyWaveScaling.Calculate(
                    baseline,
                    2,
                    new EnemyWaveMultipliers(1.15f, 1.1f, 1.05f));
                Invoke(secondBoss, "PrepareForSpawn", expected);

                AssertPreparedBoss(secondBoss, second, expected, baseline, baselineColor, true);
                yield return new WaitForFixedUpdate();
                AssertPreparedBoss(secondBoss, second, expected, baseline, baselineColor, false);
            }
            finally
            {
                if (leased != null)
                {
                    Invoke(pool, "Return", key, leased);
                }

                Invoke(pool, "Clear", key);
            }
        }

        [UnityTest]
        public IEnumerator ObjectPoolReuseDoesNotResumePoisonFromThePreviousEnemyLease()
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            var pool = FindActiveSceneComponent("ObjectPool");
            var assembly = spawner.GetType().Assembly;
            var gruntType = assembly.GetType("Grunt");
            var poisonType = assembly.GetType("PoisonDot");
            var key = $"b2_poison_lease_{Guid.NewGuid():N}";
            var sourceA = new GameObject("B2PoisonLeaseSourceA");
            GameObject leased = null;
            var resolvedEvent = assembly.GetType("CombatEvents")
                .GetEvent("OnHitResolved", BindingFlags.Static | BindingFlags.Public);
            var contextType = assembly.GetType("CombatFeedbackContext");
            var captureMethod = typeof(BattleEnemyExperienceTests).GetMethod(
                    nameof(CaptureResolvedHit),
                    BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(contextType);
            var resolvedProbe = Delegate.CreateDelegate(resolvedEvent.EventHandlerType, captureMethod);

            Func<GameObject> factory = () =>
            {
                var enemyObject = new GameObject("B2PoisonLeaseGrunt");
                enemyObject.SetActive(false);
                enemyObject.tag = "Enemy";
                enemyObject.AddComponent<SpriteRenderer>();
                var body = enemyObject.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                enemyObject.AddComponent<BoxCollider2D>();
                enemyObject.AddComponent(gruntType);
                return enemyObject;
            };

            resolvedEvent.AddEventHandler(null, resolvedProbe);
            try
            {
                Assert.That((bool)Invoke(pool, "Register", key, factory, 1), Is.True);
                var first = (GameObject)Invoke(pool, "Get", key);
                leased = first;
                var firstEnemy = first.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Grunt");
                Invoke(firstEnemy, "PrepareForSpawn", new EnemyWaveStats(100, 10, 2f));
                var poison = first.AddComponent(poisonType);
                Invoke(poison, "AddStack", sourceA);
                Invoke(poison, "AddStack", sourceA);
                Invoke(poison, "AddStack", sourceA);

                Invoke(pool, "Return", key, first);
                leased = null;
                var second = (GameObject)Invoke(pool, "Get", key);
                leased = second;
                Assert.That(second, Is.SameAs(first),
                    "The one-object pool must reuse the poisoned Enemy instance.");
                var secondEnemy = second.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Grunt");
                Invoke(secondEnemy, "PrepareForSpawn", new EnemyWaveStats(120, 12, 2.2f));

                _resolvedHitCount = 0;
                _lastResolvedHit = null;
                var hpBefore = (int)GetField(secondEnemy, "hp");
                yield return new WaitForSeconds(0.6f);

                var failures = new List<string>();
                if ((int)GetField(secondEnemy, "hp") != hpBefore)
                {
                    failures.Add("the new lease lost HP to the old PoisonDot");
                }
                if (_resolvedHitCount != 0)
                {
                    failures.Add($"the new lease published {_resolvedHitCount} stale resolved hit(s)");
                }
                if (_lastResolvedHit != null &&
                    ReferenceEquals(ReadRuntimeProperty(_lastResolvedHit, "Source"), sourceA))
                {
                    failures.Add("the stale resolved hit retained source A");
                }
                Assert.That(
                    failures,
                    Is.Empty,
                    "B2_WHOLE_REVIEW_RED_POISON_LEASE: " + string.Join("; ", failures));
            }
            finally
            {
                resolvedEvent.RemoveEventHandler(null, resolvedProbe);
                _resolvedHitCount = 0;
                _lastResolvedHit = null;
                if (leased != null)
                {
                    Invoke(pool, "Return", key, leased);
                }
                Invoke(pool, "Clear", key);
                UnityEngine.Object.DestroyImmediate(sourceA);
            }
        }

        [UnityTest]
        public IEnumerator SpawnerDisposeCancelsActiveEnemyBeforeClearingPools()
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            var alive = ((IEnumerable)GetField(spawner, "_aliveEnemies"))
                .Cast<GameObject>()
                .ToList();
            Assert.That(alive, Is.Not.Empty, "BattleScene must expose a real active enemy for disposal.");
            var enemyObject = alive[0];
            var enemy = enemyObject.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Grunt");
            var body = enemyObject.GetComponent<Rigidbody2D>();
            SetField(enemy, "telegraphDuration", 5f);
            Invoke(enemy, "ChangeState", Enum.Parse(enemy.GetType().Assembly.GetType("EnemyState"), "Telegraph"));
            body.velocity = new Vector2(6f, 0f);

            Invoke(spawner, "Dispose");

            Assert.That(((Behaviour)enemy).enabled, Is.False,
                "B2_RED_DISPOSE_CANCELS_ACTIVE_ENEMY: Dispose must disable active Enemy behavior before pool cleanup.");
            Assert.That(body.velocity, Is.EqualTo(Vector2.zero),
                "B2_RED_DISPOSE_CANCELS_ACTIVE_ENEMY: Dispose must stop active Enemy motion before releasing scene ownership.");
            Assert.That(enemyObject.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white),
                "B2_RED_DISPOSE_CANCELS_ACTIVE_ENEMY: Dispose must remove an in-progress Telegraph color before scene transition resumes time.");
            yield return null;
            Assert.That(enemy == null, Is.True,
                "Pool cleanup may destroy the cancelled Enemy after its lease has been synchronously closed.");
        }

        [UnityTest]
        public IEnumerator BattleHudMustExposeCurrentWaveAndAliveEnemyCount()
        {
            yield return LoadBattleScene();

            var views = FindLoadedComponents("WaveObjectiveView");
            Assert.That(
                views,
                Has.Count.EqualTo(1),
                "B2_TASK6_RED_WAVE_OBJECTIVE: BattleHUD must own exactly one WaveObjectiveView.");
            var view = views.Single();
            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                ((UnityEngine.UI.Text)GetField(view, "waveText")).text,
                Is.EqualTo("\u6ce2\u6b21 1/10"));
            Assert.That(
                ((UnityEngine.UI.Text)GetField(view, "aliveText")).text,
                Does.StartWith("\u5269\u4f59 "));
        }

        [UnityTest]
        public IEnumerator RealBossSpawnMustBindOneVisibleBossHpBar()
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();

            SpawnBossThroughSpawner(spawner);
            yield return null;

            var bars = FindLoadedComponents("BossHPBar");
            Assert.That(
                bars,
                Has.Count.EqualTo(1),
                "B2_TASK6_RED_BOSS_BAR: BattleHUD must own exactly one BossHPBar.");
            Assert.That(bars[0].gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator SpawnerPublishesOrderedWaveAliveAndSingleBossRemovalTruth()
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            _task6EventOrder.Clear();
            _task6SpawnedBosses.Clear();
            _task6RemovedBosses.Clear();

            var waveStartedEvent = RequireEvent(
                spawner,
                "OnWaveStarted",
                "B2_TASK6_RED_SPAWNER_EVENTS");
            var aliveEvent = RequireEvent(spawner, "OnAliveEnemyCountChanged", "B2_TASK6_RED_SPAWNER_EVENTS");
            var bossSpawnedEvent = RequireEvent(spawner, "OnBossSpawned", "B2_TASK6_RED_SPAWNER_EVENTS");
            var bossRemovedEvent = RequireEvent(spawner, "OnBossRemoved", "B2_TASK6_RED_SPAWNER_EVENTS");
            var legacyWaveEvent = RequireEvent(spawner, "OnWaveStart", "B2_TASK6_RED_SPAWNER_EVENTS");
            Action<int, int> waveStarted = (wave, total) =>
                _task6EventOrder.Add($"wave:{wave}/{total}");
            Action<int> aliveChanged = alive => _task6EventOrder.Add($"alive:{alive}");
            Action<int> legacyWave = wave => _task6EventOrder.Add($"legacy:{wave}");
            var bossType = spawner.GetType().Assembly.GetType("Boss");
            var bossSpawned = CreateTask6BossDelegate(bossSpawnedEvent, nameof(CaptureTask6BossSpawned), bossType);
            var bossRemoved = CreateTask6BossDelegate(bossRemovedEvent, nameof(CaptureTask6BossRemoved), bossType);

            waveStartedEvent.AddEventHandler(spawner, waveStarted);
            aliveEvent.AddEventHandler(spawner, aliveChanged);
            legacyWaveEvent.AddEventHandler(spawner, legacyWave);
            bossSpawnedEvent.AddEventHandler(spawner, bossSpawned);
            bossRemovedEvent.AddEventHandler(spawner, bossRemoved);
            try
            {
                var firstWave = (IEnumerator)Invoke(spawner, "SpawnWaveCoroutine", 0);
                Assert.That(firstWave.MoveNext(), Is.True);
                Assert.That(_task6EventOrder.Count(item => item == "wave:0/10"), Is.EqualTo(1));
                Assert.That(_task6EventOrder.Count(item => item == "legacy:0"), Is.EqualTo(1));
                var firstAliveIndex = _task6EventOrder.FindIndex(item => item.StartsWith("alive:"));
                Assert.That(firstAliveIndex, Is.GreaterThan(_task6EventOrder.IndexOf("wave:0/10")));
                Assert.That(firstAliveIndex, Is.GreaterThan(_task6EventOrder.IndexOf("legacy:0")));

                _task6EventOrder.Clear();
                SetField(spawner, "_currentWave", 2);
                var firstBoss = SpawnBossThroughSpawner(spawner);
                var secondBoss = SpawnBossThroughSpawner(spawner);
                Assert.That(_task6SpawnedBosses, Is.EqualTo(new[] { firstBoss, secondBoss }));
                Assert.That(_task6EventOrder[0], Does.StartWith("alive:"));
                Assert.That(_task6EventOrder[1], Is.EqualTo("boss+"));
                Assert.That(_task6EventOrder[2], Does.StartWith("alive:"));
                Assert.That(_task6EventOrder[3], Is.EqualTo("boss+"));

                var baseline = (EnemyStatBaseline)GetProperty(firstBoss, "Baseline");
                var expected = EnemyWaveScaling.Calculate(
                    baseline,
                    2,
                    new EnemyWaveMultipliers(
                        (float)GetField(spawner, "enemyHpMultiplier"),
                        (float)GetField(spawner, "enemyDamageMultiplier"),
                        (float)GetField(spawner, "enemySpeedMultiplier")));
                Assert.That((int)GetField(firstBoss, "maxHp"), Is.EqualTo(expected.MaxHp));
                Assert.That((int)GetProperty(firstBoss, "CurrentPhase"), Is.EqualTo(1));

                _task6EventOrder.Clear();
                Invoke(firstBoss, "TakeDamage", 100000, 0f, 0f);
                var aliveAfterDeath = ((IEnumerable)GetField(spawner, "_aliveEnemies")).Cast<object>().Count();
                Assert.That(
                    _task6EventOrder,
                    Is.EqualTo(new[] { $"alive:{aliveAfterDeath}", "boss-" }),
                    "Confirmed Boss death must publish alive count before Boss removal.");
                Assert.That(_task6RemovedBosses, Has.Count.EqualTo(1));
                Assert.That(_task6RemovedBosses[0], Is.SameAs(firstBoss));
                Assert.That(_task6EventOrder.Count(item => item == "boss-"), Is.EqualTo(1));
                Assert.That(_task6EventOrder.Last(item => item.StartsWith("alive:")), Is.EqualTo($"alive:{aliveAfterDeath}"));
                Assert.That(aliveAfterDeath, Is.GreaterThanOrEqualTo(0));

                var eventCountAfterDeath = _task6EventOrder.Count;
                Invoke(firstBoss, "TakeDamage", 100000, 0f, 0f);
                Assert.That(_task6EventOrder, Has.Count.EqualTo(eventCountAfterDeath),
                    "Duplicate death must not decrement alive count or remove a Boss twice.");

                Invoke(spawner, "Dispose");
                Assert.That(_task6RemovedBosses, Has.Count.EqualTo(2));
                Assert.That(_task6RemovedBosses[1], Is.SameAs(secondBoss));
                Assert.That(GetInstanceEventHandlers(spawner, "OnWaveStarted"), Is.Empty);
                Assert.That(GetInstanceEventHandlers(spawner, "OnAliveEnemyCountChanged"), Is.Empty);
                Assert.That(GetInstanceEventHandlers(spawner, "OnBossSpawned"), Is.Empty);
                Assert.That(GetInstanceEventHandlers(spawner, "OnBossRemoved"), Is.Empty);
            }
            finally
            {
                waveStartedEvent.RemoveEventHandler(spawner, waveStarted);
                aliveEvent.RemoveEventHandler(spawner, aliveChanged);
                legacyWaveEvent.RemoveEventHandler(spawner, legacyWave);
                bossSpawnedEvent.RemoveEventHandler(spawner, bossSpawned);
                bossRemovedEvent.RemoveEventHandler(spawner, bossRemoved);
            }
        }

        [UnityTest]
        public IEnumerator EnemyHealthAndBossPhaseEventsPublishActualChangesAndReset()
        {
            yield return LoadBattleScene();
            var boss = CreateEnemyProbe("Boss", "B2Task6BossEventProbe");
            ((Behaviour)boss).enabled = false;
            var healthEvent = RequireEvent(boss, "OnHealthChanged", "B2_TASK6_RED_ENEMY_EVENTS");
            var phaseEvent = RequireEvent(boss, "OnPhaseChanged", "B2_TASK6_RED_ENEMY_EVENTS");
            var healthChanges = new List<Vector2Int>();
            var phaseChanges = new List<int>();
            Action<int, int> healthChanged = (current, maximum) =>
                healthChanges.Add(new Vector2Int(current, maximum));
            Action<int> phaseChanged = phaseChanges.Add;
            healthEvent.AddEventHandler(boss, healthChanged);
            phaseEvent.AddEventHandler(boss, phaseChanged);
            try
            {
                var spawnStats = new EnemyWaveStats(600, 30, 3f);
                Invoke(boss, "PrepareForSpawn", spawnStats);
                Assert.That(healthChanges.Last(), Is.EqualTo(new Vector2Int(600, 600)));

                var before = (int)GetField(boss, "hp");
                Invoke(boss, "TakeDamage", 10, 0f, 0f);
                var after = (int)GetField(boss, "hp");
                Assert.That(after, Is.LessThan(before));
                Assert.That(healthChanges.Last(), Is.EqualTo(new Vector2Int(after, 600)));

                Invoke(boss, "EnterEnrage");
                Invoke(boss, "EnterEnrage");
                Assert.That((int)GetProperty(boss, "CurrentPhase"), Is.EqualTo(2));
                Assert.That(phaseChanges, Is.EqualTo(new[] { 2 }),
                    "EnterEnrage must publish phase 2 exactly once.");

                Invoke(boss, "PrepareForSpawn", spawnStats);
                Assert.That((int)GetProperty(boss, "CurrentPhase"), Is.EqualTo(1));
                Assert.That(phaseChanges, Is.EqualTo(new[] { 2, 1 }));
                Assert.That(healthChanges.Last(), Is.EqualTo(new Vector2Int(600, 600)));
            }
            finally
            {
                if (boss != null)
                {
                    healthEvent.RemoveEventHandler(boss, healthChanged);
                    phaseEvent.RemoveEventHandler(boss, phaseChanged);
                    UnityEngine.Object.DestroyImmediate(boss.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator BossHpBarConsumesEventsAndUnbindsAfterRemoval()
        {
            yield return LoadBattleScene();
            var spawner = FindActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            SetField(spawner, "_currentWave", 2);
            var boss = SpawnBossThroughSpawner(spawner);
            var bars = FindLoadedComponents("BossHPBar");
            Assert.That(
                bars,
                Has.Count.EqualTo(1),
                "B2_TASK6_RED_BOSS_BAR_LIFECYCLE: expected one event-owned BossHPBar.");
            var bar = bars.Single();
            var slider = (UnityEngine.UI.Slider)GetField(bar, "bossSlider");
            var phaseText = (UnityEngine.UI.Text)GetField(bar, "phaseText");

            Assert.That(bar.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                Is.Null,
                "BossHPBar must not poll Boss state in Update.");
            Assert.That(slider.maxValue, Is.EqualTo((int)GetField(boss, "maxHp")));
            Assert.That(slider.value, Is.EqualTo((int)GetField(boss, "hp")));
            Assert.That(phaseText.text, Is.EqualTo("\u9636\u6bb5 1"));

            Invoke(boss, "TakeDamage", 10, 0f, 0f);
            Assert.That(slider.value, Is.EqualTo((int)GetField(boss, "hp")),
                "Boss health changes must render synchronously from the event.");
            Invoke(boss, "EnterEnrage");
            Assert.That(phaseText.text, Is.EqualTo("\u9636\u6bb5 2"));

            Invoke(boss, "TakeDamage", 100000, 0f, 0f);
            Assert.That(bar.gameObject.activeInHierarchy, Is.False);
            Assert.That(GetInstanceEventHandlers(boss, "OnHealthChanged")
                .Any(handler => ReferenceEquals(handler.Target, bar)), Is.False);
            Assert.That(GetInstanceEventHandlers(boss, "OnPhaseChanged")
                .Any(handler => ReferenceEquals(handler.Target, bar)), Is.False);
        }

        private static Component SpawnBossThroughSpawner(Component spawner)
        {
            var runtimeAssembly = spawner.GetType().Assembly;
            var entryType = runtimeAssembly.GetType("EnemySpawnEntry");
            Assert.That(entryType, Is.Not.Null);
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("enemyType").SetValue(entry, "boss");
            entryType.GetField("preferredSide").SetValue(entry, ArenaSpawnSide.Right);

            Invoke(spawner, "SpawnEnemy", entry);
            var bossObject = ((IEnumerable)GetField(spawner, "_aliveEnemies"))
                .Cast<GameObject>()
                .Last(item => item.GetComponents<Component>()
                    .Any(component => component.GetType().Name == "Boss"));
            return bossObject.GetComponents<Component>()
                .Single(component => component.GetType().Name == "Boss");
        }

        private EventInfo RequireEvent(object publisher, string eventName, string marker)
        {
            var eventInfo = publisher.GetType().GetEvent(
                eventName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(eventInfo, Is.Not.Null, $"{marker}: missing {eventName}.");
            return eventInfo;
        }

        private Delegate CreateTask6BossDelegate(EventInfo eventInfo, string methodName, Type bossType)
        {
            var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(bossType);
            return Delegate.CreateDelegate(eventInfo.EventHandlerType, this, method);
        }

        private void CaptureTask6BossSpawned<T>(T boss) where T : Component
        {
            _task6EventOrder.Add("boss+");
            _task6SpawnedBosses.Add(boss);
        }

        private void CaptureTask6BossRemoved<T>(T boss) where T : Component
        {
            _task6EventOrder.Add("boss-");
            _task6RemovedBosses.Add(boss);
        }

        private static List<Delegate> GetInstanceEventHandlers(object publisher, string eventName)
        {
            for (var type = publisher.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    eventName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field?.GetValue(publisher) is Delegate handlers)
                {
                    return handlers.GetInvocationList().ToList();
                }
            }

            return new List<Delegate>();
        }

        private static List<Component> FindLoadedComponents(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>()
                .Where(component =>
                    component != null &&
                    component.GetType().Name == typeName &&
                    component.gameObject.scene == SceneManager.GetActiveScene())
                .ToList();
        }

        private static void AssertPreparedBoss(
            Component boss,
            GameObject enemyObject,
            EnemyWaveStats expected,
            EnemyStatBaseline baseline,
            Color baselineColor,
            bool beforeFirstPhysicsStep)
        {
            Assert.That((int)GetField(boss, "hp"), Is.EqualTo(expected.MaxHp));
            Assert.That((int)GetField(boss, "maxHp"), Is.EqualTo(expected.MaxHp));
            Assert.That((int)GetField(boss, "damage"), Is.EqualTo(expected.Damage));
            Assert.That((float)GetField(boss, "moveSpeed"), Is.EqualTo(expected.MoveSpeed).Within(0.0001f));
            Assert.That((float)GetField(boss, "damageReduction"),
                Is.EqualTo(baseline.DamageReduction).Within(0.0001f));
            Assert.That((float)GetField(boss, "telegraphDuration"),
                Is.EqualTo(baseline.TelegraphDuration).Within(0.0001f));
            Assert.That((float)GetField(boss, "attackDuration"),
                Is.EqualTo(baseline.AttackDuration).Within(0.0001f));
            Assert.That((bool)GetField(boss, "_isEnraged"), Is.False);
            Assert.That((int)GetField(boss, "_attackPattern"), Is.Zero);
            Assert.That(GetProperty(boss, "IsDead"), Is.False);

            var state = GetProperty(boss, "CurrentState").ToString();
            if (beforeFirstPhysicsStep)
            {
                Assert.That(state, Is.EqualTo("Idle"));
            }
            else
            {
                Assert.That(state, Is.Not.EqualTo("Telegraph").And.Not.EqualTo("Attack").And.Not.EqualTo("Die"),
                    "A fresh Boss may start chasing, but must not resume an old attack lease.");
            }

            var sprite = enemyObject.GetComponent<SpriteRenderer>();
            Assert.That(sprite.color, Is.EqualTo(baselineColor));
            Assert.That(enemyObject.GetComponent<Collider2D>().enabled, Is.True);
            if (beforeFirstPhysicsStep)
            {
                Assert.That(sprite.flipX, Is.False);
                Assert.That(enemyObject.GetComponent<Rigidbody2D>().velocity, Is.EqualTo(Vector2.zero));
                Assert.That(enemyObject.GetComponent<Rigidbody2D>().angularVelocity, Is.Zero);
            }
        }

        private static IEnumerator LoadBattleScene()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return WaitForApplicationReady();
        }

        private static void DisableActiveEnemyAutomation()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component == null ||
                    component.gameObject.scene != activeScene ||
                    !component.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (component is MonoBehaviour spawner && component.GetType().Name == "WaveSpawner")
                {
                    spawner.StopAllCoroutines();
                    spawner.enabled = false;
                    continue;
                }

                if (!(component is Behaviour enemyBehaviour))
                {
                    continue;
                }

                for (var type = component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.Name != "EnemyBase")
                    {
                        continue;
                    }

                    enemyBehaviour.enabled = false;
                    break;
                }
            }
        }

        private static IEnumerator WaitForAttackPhase(Component enemy, string phaseName, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (GetProperty(enemy, "CurrentAttackPhase").ToString() == phaseName)
                {
                    yield break;
                }

                yield return null;
            }

            var timeController = Resources.FindObjectsOfTypeAll<Component>()
                .FirstOrDefault(component =>
                    component != null &&
                    component.GetType().Name == "BattleTimeController" &&
                    component.gameObject.scene == SceneManager.GetActiveScene());
            var requestCount = timeController == null
                ? -1
                : (int)GetProperty(timeController, "ActiveRequestCount");
            Assert.Fail(
                $"{enemy.GetType().Name} did not enter attack phase {phaseName} within {maxFrames} frames. " +
                $"phase={GetProperty(enemy, "CurrentAttackPhase")}, state={GetProperty(enemy, "CurrentState")}, " +
                $"timeScale={Time.timeScale}, deltaTime={Time.deltaTime}, requests={requestCount}, " +
                $"routineNull={GetField(enemy, "_attackRoutine") == null}, " +
                $"telegraph={((EnemyAttackPlan)GetProperty(enemy, "CurrentAttackPlan")).TelegraphDuration}.");
        }

        private static IEnumerator WaitForAttackPhaseWithinRealtime(
            Component enemy,
            string phaseName,
            float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (GetProperty(enemy, "CurrentAttackPhase").ToString() == phaseName)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"{enemy.GetType().Name} did not enter attack phase {phaseName} within " +
                $"{timeoutSeconds} realtime seconds; final phase={GetProperty(enemy, "CurrentAttackPhase")}.");
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

        private static IEnumerator WaitForFreshActiveSceneComponent(
            string typeName,
            int oldInstanceId,
            Action<Component> found)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var activeScene = SceneManager.GetActiveScene();
                var component = Resources.FindObjectsOfTypeAll<Component>()
                    .FirstOrDefault(item =>
                        item != null &&
                        item.GetType().Name == typeName &&
                        item.gameObject.scene == activeScene &&
                        item.gameObject.activeInHierarchy &&
                        item.GetInstanceID() != oldInstanceId);
                if (component != null)
                {
                    found(component);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Restart did not create a fresh {typeName} within 10 realtime seconds.");
        }

        private static IEnumerator WaitForSceneTransitionComplete()
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var transitionManager = Resources.FindObjectsOfTypeAll<Component>()
                    .SingleOrDefault(component =>
                        component != null && component.GetType().Name == "SceneTransitionManager");
                if (transitionManager != null && !(bool)GetField(transitionManager, "_isTransitioning"))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("SceneTransitionManager did not finish restart within 10 realtime seconds.");
        }

        private static Component FindActiveSceneComponent(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>().Single(component =>
                component != null &&
                component.GetType().Name == typeName &&
                component.gameObject.scene == SceneManager.GetActiveScene() &&
                component.gameObject.activeInHierarchy);
        }

        private static Component FindFirstActiveEnemy(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>().First(component =>
                component != null &&
                component.GetType().Name == typeName &&
                component.gameObject.scene == SceneManager.GetActiveScene() &&
                component.gameObject.activeInHierarchy);
        }

        private static int _resolvedHitCount;
        private static object _lastResolvedHit;

        private static void CaptureResolvedHit<T>(T context)
        {
            _resolvedHitCount++;
            _lastResolvedHit = context;
        }

        private static void ThrowResolvedHit<T>(T context)
        {
            throw new InvalidOperationException("B2_TASK5_FEEDBACK_SUBSCRIBER_FAILURE");
        }

        private static object ReadRuntimeProperty(object instance, string propertyName)
        {
            return instance.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(instance);
        }

        private static Delegate[] GetStaticEventHandlers(FieldInfo backingField)
        {
            var handlers = backingField.GetValue(null) as Delegate;
            return handlers?.GetInvocationList() ?? Array.Empty<Delegate>();
        }

        private static Component CreateEnemyProbe(string typeName, string objectName)
        {
            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null, "BattleScene must expose the real Player for enemy probes.");

            var enemyObject = new GameObject(objectName);
            enemyObject.SetActive(false);
            enemyObject.tag = "Enemy";
            enemyObject.transform.position = player.transform.position + new Vector3(0.5f, 0f, 0f);
            enemyObject.AddComponent<SpriteRenderer>();
            var body = enemyObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            enemyObject.AddComponent<BoxCollider2D>();

            var enemyType = FindActiveSceneComponent("WaveSpawner")
                .GetType()
                .Assembly
                .GetType(typeName);
            Assert.That(enemyType, Is.Not.Null, $"Expected runtime enemy type {typeName}.");
            var enemy = enemyObject.AddComponent(enemyType);
            enemyObject.SetActive(true);
            Invoke(enemy, "InitializeCombatBaseline");
            return enemy;
        }

        private static object GetField(object instance, string name)
        {
            return FindField(instance.GetType(), name).GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            FindField(instance.GetType(), name).SetValue(instance, value);
        }

        private static object GetProperty(object instance, string name)
        {
            return instance.GetType()
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(instance);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            throw new MissingFieldException(type.FullName, name);
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            for (var current = instance.GetType(); current != null; current = current.BaseType)
            {
                var method = current.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SingleOrDefault(candidate =>
                        candidate.Name == name && candidate.GetParameters().Length == args.Length);
                if (method != null)
                {
                    return method.Invoke(instance, args);
                }
            }

            throw new MissingMethodException(instance.GetType().FullName, name);
        }
    }
}
