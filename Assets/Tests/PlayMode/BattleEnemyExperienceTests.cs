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
