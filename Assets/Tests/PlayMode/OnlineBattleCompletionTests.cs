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
    public sealed class OnlineBattleCompletionTests
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator TwoRealWavesDieThroughHealthAndCompleteVictoryOnce()
        {
            yield return LoadBattleScene();

            var setup = FindActiveSceneComponent("BattleSceneSetup");
            var spawner = FindActiveSceneComponent("WaveSpawner");
            var run = FindActiveSceneComponent("BattleRunController");
            var gameOver = FindActiveSceneComponent("GameOverUI");
            yield return ResetRunningWaves(spawner, setup);

            SetField(spawner, "waves", CreateTwoWaveConfiguration(spawner.GetType().Assembly));
            SetField(spawner, "waveDelay", 0f);

            var waveStarts = new List<int>();
            var aliveCounts = new List<int>();
            var completionCount = 0;
            AddEventHandler(spawner, "OnWaveStart", (Action<int>)waveStarts.Add);
            AddEventHandler(spawner, "OnAliveEnemyCountChanged", (Action<int>)aliveCounts.Add);
            AddEventHandler(spawner, "OnAllWavesComplete", (Action)(() => completionCount++));

            try
            {
                Invoke(spawner, "StartWaves");

                yield return WaitForWaveAndAliveCount(spawner, 0, 1, 120);
                var grunt = GetOnlyAliveEnemy(spawner);
                Assert.That(grunt.GetType().Name, Is.EqualTo("Grunt"));
                DisableEnemyAi(grunt);
                DamageToDeath(grunt);
                Assert.That(GetBoolProperty(grunt, "IsDead"), Is.True);
                Assert.That(GetIntProperty(spawner, "AliveEnemyCount"), Is.Zero,
                    "A real enemy death must unregister it from the active wave.");

                yield return WaitForWaveAndAliveCount(spawner, 1, 1, 120);
                var archer = GetOnlyAliveEnemy(spawner);
                Assert.That(archer.GetType().Name, Is.EqualTo("Archer"));
                DisableEnemyAi(archer);
                DamageToDeath(archer);
                Assert.That(GetBoolProperty(archer, "IsDead"), Is.True);
                Assert.That(GetIntProperty(spawner, "AliveEnemyCount"), Is.Zero,
                    "The second real enemy death must unregister it before victory.");

                yield return WaitForVictory(run, 120);

                Assert.That(waveStarts, Is.EqualTo(new[] { 0, 1 }));
                Assert.That(aliveCounts, Is.EqualTo(new[] { 1, 0, 1, 0 }),
                    "Each real wave must publish its live enemy transition from one to zero.");
                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(GetProperty(run, "State").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetProperty(run, "Outcome").ToString(), Is.EqualTo("Victory"));
                Assert.That(GetPrivateInt(setup, "_killCount"), Is.EqualTo(2));
                Assert.That(GetProperty(gameOver, "SettlementState").ToString(), Is.EqualTo("Saved"));
            }
            finally
            {
                Invoke(run, "Dispose");
                Time.timeScale = 1f;
            }
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

        private static IEnumerator LoadBattleScene()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            for (var frame = 0; frame < 120; frame++)
            {
                var application = GameObject.Find("[GameApplication]");
                var component = application == null
                    ? null
                    : application.GetComponents<Component>()
                        .FirstOrDefault(candidate => candidate != null && candidate.GetType().Name == "GameApplication");
                if (component?.GetType().GetProperty("State")?.GetValue(component)?.ToString() == "Ready")
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("GameApplication did not become Ready within 120 frames.");
        }

        private static IEnumerator ResetRunningWaves(Component spawner, Component setup)
        {
            ((MonoBehaviour)spawner).StopAllCoroutines();
            foreach (var enemy in GetAliveEnemies(spawner))
            {
                DisableEnemyAi(enemy);
                DamageToDeath(enemy);
            }

            Assert.That(GetIntProperty(spawner, "AliveEnemyCount"), Is.Zero,
                "The scene-started wave must be retired before the acceptance wave begins.");
            yield return new WaitForSecondsRealtime(0.7f);
            ((MonoBehaviour)spawner).StopAllCoroutines();
            SetField(setup, "_killCount", 0);
            SetField(setup, "_bossKills", 0);
            SetField(setup, "_startTime", Time.time);
        }

        private static IEnumerator WaitForWaveAndAliveCount(
            Component spawner,
            int expectedWave,
            int expectedAlive,
            int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (GetIntProperty(spawner, "CurrentWaveIndex") == expectedWave &&
                    GetIntProperty(spawner, "AliveEnemyCount") == expectedAlive)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Wave {expectedWave} did not reach alive count {expectedAlive} within {maxFrames} frames. " +
                $"Current wave: {GetIntProperty(spawner, "CurrentWaveIndex")}; " +
                $"alive: {GetIntProperty(spawner, "AliveEnemyCount")}.");
        }

        private static IEnumerator WaitForVictory(Component run, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (GetProperty(run, "State").ToString() == "Victory")
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Battle did not reach Victory within {maxFrames} frames; state was {GetProperty(run, "State")}.");
        }

        private static Component GetOnlyAliveEnemy(Component spawner)
        {
            var alive = GetAliveEnemies(spawner);
            Assert.That(alive, Has.Count.EqualTo(1));
            return alive.Single();
        }

        private static List<Component> GetAliveEnemies(Component spawner)
        {
            return ((IEnumerable)GetField(spawner, "_aliveEnemies"))
                .Cast<GameObject>()
                .Where(enemy => enemy != null)
                .Select(enemy => enemy.GetComponents<Component>().FirstOrDefault(component => IsEnemyType(component?.GetType())))
                .Where(enemy => enemy != null && !GetBoolProperty(enemy, "IsDead"))
                .ToList();
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

        private static void DisableEnemyAi(Component enemy)
        {
            ((Behaviour)enemy).enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private static void DamageToDeath(Component enemy)
        {
            var takeDamage = enemy.GetType().GetMethod(
                "TakeDamage",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(float), typeof(float) },
                null);
            Assert.That(takeDamage, Is.Not.Null, "EnemyBase must expose public TakeDamage(int, float, float).");
            takeDamage.Invoke(enemy, new object[] { int.MaxValue, 0f, 0f });
        }

        private static Component FindActiveSceneComponent(string typeName)
        {
            var activeScene = SceneManager.GetActiveScene();
            var matches = Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null &&
                                    component.GetType().Name == typeName &&
                                    component.gameObject.scene == activeScene)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), $"Expected exactly one active-scene {typeName}.");
            return matches.Single();
        }

        private static void AddEventHandler(Component component, string eventName, Delegate handler)
        {
            var eventInfo = component.GetType().GetEvent(eventName, InstanceFlags);
            Assert.That(eventInfo, Is.Not.Null);
            eventInfo.AddEventHandler(component, handler);
        }

        private static void Invoke(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(methodName, InstanceFlags, null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, null);
        }

        private static object GetField(Component component, string fieldName)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(component);
        }

        private static object GetProperty(Component component, string propertyName)
        {
            var property = component.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(component);
        }

        private static int GetIntProperty(Component component, string propertyName)
        {
            return (int)GetProperty(component, propertyName);
        }

        private static bool GetBoolProperty(Component component, string propertyName)
        {
            return (bool)GetProperty(component, propertyName);
        }

        private static int GetPrivateInt(Component component, string fieldName)
        {
            return (int)GetField(component, fieldName);
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null);
            field.SetValue(component, value);
        }
    }
}
