using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Game.Gameplay;
using Game.Online;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public sealed class BattleSettlementUiTests
    {
        private static int _kill100ProgressUpdates;
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator SettlementStatesGateActionsAndRenderWithoutOverlappingButtons()
        {
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return WaitForGameOverUi();

            var gameOver = FindGameOverUi();
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var talentManager = Resources.FindObjectsOfTypeAll<Component>()
                .Single(item => item != null && item.GetType().Name == "TalentManager");
            var achievementManager = Resources.FindObjectsOfTypeAll<Component>()
                .Single(item => item != null && item.GetType().Name == "AchievementManager");
            Invoke(achievementManager, "ResetAll");
            var progressUpdated = achievementManager.GetType().GetEvent("OnProgressUpdated", InstanceFlags);
            Assert.That(progressUpdated, Is.Not.Null);
            _kill100ProgressUpdates = 0;
            var achievementHandler = CreateKill100Handler(progressUpdated.EventHandlerType);
            progressUpdated.AddEventHandler(achievementManager, achievementHandler);
            var talentBefore = (int)GetProperty(talentManager, "AvailablePoints");
            var battleSetup = Resources.FindObjectsOfTypeAll<Component>()
                .Single(item => item != null && item.GetType().Name == "BattleSceneSetup" &&
                                item.gameObject.scene == SceneManager.GetActiveScene());
            SetField(battleSetup, "_killCount", 1);
            var hurtbox = player.GetComponents<Component>()
                .Single(item => item != null && item.GetType().Name == "Hurtbox");
            var receiveHit = hurtbox.GetType().GetMethod(
                "ReceiveHit",
                InstanceFlags,
                null,
                new[] { typeof(CombatHit) },
                null);
            Assert.That(receiveHit, Is.Not.Null);
            receiveHit.Invoke(hurtbox, new object[] { new CombatHit(100000, 1f, 3f, false, null) });
            yield return null;
            Assert.That((int)GetProperty(talentManager, "AvailablePoints"), Is.EqualTo(talentBefore + 1),
                "The terminal BattleRunController must award talent points exactly once on defeat.");
            Assert.That(_kill100ProgressUpdates, Is.EqualTo(1),
                "The terminal BattleRunController must report defeat achievements exactly once.");
            progressUpdated.RemoveEventHandler(achievementManager, achievementHandler);
            Invoke(achievementManager, "ResetAll");

            Invoke(gameOver, "DisplayGameOver", true, new CombatResultData
            {
                killCount = 3,
                expGained = 8,
                maxCombo = 2,
                survivalTime = 12
            });
            yield return null;
            var canvas = gameOver.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            AssertState(gameOver, BattleSettlementState.Pending, false, false, false);
            AssertText(gameOver, "SettlementStatus", "\u7ed3\u7b97\u4e2d");
            AssertTextIsDark(gameOver, "SettlementStatus");
            AssertText(gameOver, "Reward", string.Empty);
            AssertActiveButtonsDoNotOverlap(gameOver);
            yield return Capture(canvas, camera, "task-6-ui-pending.png");

            Invoke(gameOver, "SetSettlementResult", CreateResult(BattleSettlementState.Saved, 13, 21));
            yield return null;
            AssertState(gameOver, BattleSettlementState.Saved, true, true, false);
            AssertText(gameOver, "SettlementStatus", "\u7ed3\u7b97\u5b8c\u6210");
            AssertTextIsDark(gameOver, "SettlementStatus");
            AssertText(gameOver, "Reward", "\u91d1\u5e01 13  \u7ecf\u9a8c 21");
            AssertTextIsDark(gameOver, "Reward");
            AssertActiveButtonsDoNotOverlap(gameOver);
            yield return Capture(canvas, camera, "task-6-ui-saved.png");

            Invoke(gameOver, "SetSettlementResult", CreateResult(BattleSettlementState.Failed, 0, 0));
            yield return null;
            AssertState(gameOver, BattleSettlementState.Failed, false, false, true);
            AssertText(gameOver, "SettlementStatus", "\u7ed3\u7b97\u5931\u8d25");
            AssertTextIsDark(gameOver, "SettlementStatus");
            AssertText(gameOver, "BtnRetry/Label", "\u91cd\u8bd5");
            AssertActiveButtonsDoNotOverlap(gameOver);
            yield return Capture(canvas, camera, "task-6-ui-failed.png");
        }

        [UnityTest]
        public IEnumerator WaveCompletionAwardsTalentAndReportsKillAchievementExactlyOnce()
        {
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return WaitForGameOverUi();

            var talent = Resources.FindObjectsOfTypeAll<Component>()
                .Single(item => item != null && item.GetType().Name == "TalentManager");
            var achievements = Resources.FindObjectsOfTypeAll<Component>()
                .Single(item => item != null && item.GetType().Name == "AchievementManager");
            Invoke(achievements, "ResetAll");
            var progressUpdated = achievements.GetType().GetEvent("OnProgressUpdated", InstanceFlags);
            Assert.That(progressUpdated, Is.Not.Null);
            _kill100ProgressUpdates = 0;
            var handler = CreateKill100Handler(progressUpdated.EventHandlerType);
            progressUpdated.AddEventHandler(achievements, handler);
            try
            {
                var talentBefore = (int)GetProperty(talent, "AvailablePoints");
                var setup = Resources.FindObjectsOfTypeAll<Component>()
                    .Single(item => item != null && item.GetType().Name == "BattleSceneSetup" &&
                                    item.gameObject.scene == SceneManager.GetActiveScene());
                SetField(setup, "_killCount", 1);
                var spawner = Resources.FindObjectsOfTypeAll<Component>()
                    .Single(item => item != null && item.GetType().Name == "WaveSpawner" &&
                                    item.gameObject.scene == SceneManager.GetActiveScene());

                // Drive the controller through WaveSpawner's actual completion coroutine.
                ((MonoBehaviour)spawner).StopAllCoroutines();
                var waves = spawner.GetType().GetField("waves", InstanceFlags);
                Assert.That(waves, Is.Not.Null);
                waves.SetValue(spawner, Array.CreateInstance(waves.FieldType.GetElementType(), 0));
                Invoke(spawner, "StartWaves");
                yield return null;

                Assert.That((int)GetProperty(talent, "AvailablePoints"), Is.EqualTo(talentBefore + 1));
                Assert.That(_kill100ProgressUpdates, Is.EqualTo(1));

                Invoke(spawner, "StartWaves");
                yield return null;

                Assert.That((int)GetProperty(talent, "AvailablePoints"), Is.EqualTo(talentBefore + 1));
                Assert.That(_kill100ProgressUpdates, Is.EqualTo(1));
            }
            finally
            {
                progressUpdated.RemoveEventHandler(achievements, handler);
                Invoke(achievements, "ResetAll");
            }
        }

        private static IEnumerator WaitForGameOverUi()
        {
            for (var frame = 0; frame < 180; frame++)
            {
                if (FindGameOverUi(false) != null &&
                    ApplicationIsReady() &&
                    Camera.main != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("BattleScene did not create its scene-owned GameOverUI.");
        }

        private static bool ApplicationIsReady()
        {
            var application = GameObject.Find("[GameApplication]");
            var component = application == null
                ? null
                : application.GetComponents<Component>()
                    .FirstOrDefault(item => item != null && item.GetType().Name == "GameApplication");
            return component != null && GetProperty(component, "State").ToString() == "Ready";
        }

        private static BattleSettlementResult CreateResult(BattleSettlementState state, int gold, int experience)
        {
            var result = new BattleSettlementResult();
            SetResultProperty(result, "State", state);
            SetResultProperty(result, "RewardGold", gold);
            SetResultProperty(result, "RewardExp", experience);
            return result;
        }

        private static void SetResultProperty(BattleSettlementResult result, string name, object value)
        {
            var property = typeof(BattleSettlementResult).GetProperty(name, InstanceFlags);
            Assert.That(property, Is.Not.Null);
            property.SetValue(result, value);
        }

        private static void AssertState(
            Component gameOver,
            BattleSettlementState expectedState,
            bool restartEnabled,
            bool menuEnabled,
            bool retryVisible)
        {
            Assert.That(GetProperty(gameOver, "SettlementState").ToString(), Is.EqualTo(expectedState.ToString()));
            var restart = FindButton(gameOver, "BtnRestart");
            var menu = FindButton(gameOver, "BtnMainMenu");
            var retry = FindButton(gameOver, "BtnRetry");
            Assert.That(restart.interactable, Is.EqualTo(restartEnabled));
            Assert.That(menu.interactable, Is.EqualTo(menuEnabled));
            Assert.That(retry.gameObject.activeSelf, Is.EqualTo(retryVisible));
            Assert.That(retry.interactable, Is.EqualTo(retryVisible));
        }

        private static void AssertActiveButtonsDoNotOverlap(Component gameOver)
        {
            var panel = gameOver.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == "ResultPanel");
            var activeButtons = gameOver.GetComponentsInChildren<Button>(true)
                .Where(item => item.gameObject.activeInHierarchy)
                .Select(item => BoundsInParent(item.GetComponent<RectTransform>(), panel))
                .ToArray();

            for (var left = 0; left < activeButtons.Length; left++)
            {
                for (var right = left + 1; right < activeButtons.Length; right++)
                {
                    Assert.That(activeButtons[left].Overlaps(activeButtons[right]), Is.False,
                        $"Active result actions {left} and {right} overlap.");
                }
            }
        }

        private static Button FindButton(Component gameOver, string name)
        {
            return gameOver.GetComponentsInChildren<Button>(true)
                .Single(item => item.gameObject.name == name);
        }

        private static void AssertText(Component gameOver, string path, string expected)
        {
            Assert.That(FindText(gameOver, path).text, Is.EqualTo(expected));
        }

        private static void AssertTextIsDark(Component gameOver, string path)
        {
            Assert.That(FindText(gameOver, path).color.grayscale, Is.LessThan(0.5f),
                $"{path} must remain readable on the light result panel.");
        }

        private static Text FindText(Component gameOver, string path)
        {
            var transform = gameOver.transform;
            foreach (var segment in path.Split('/'))
            {
                transform = transform.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == segment);
            }

            return transform.GetComponent<Text>();
        }

        private static Rect BoundsInParent(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var first = parent.InverseTransformPoint(corners[0]);
            var min = new Vector2(first.x, first.y);
            var max = min;
            for (var index = 1; index < corners.Length; index++)
            {
                var point = parent.InverseTransformPoint(corners[index]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Component FindGameOverUi(bool required = true)
        {
            var match = Resources.FindObjectsOfTypeAll<Component>()
                .SingleOrDefault(item => item != null && item.GetType().Name == "GameOverUI" &&
                                         item.gameObject.scene == SceneManager.GetActiveScene());
            if (required)
            {
                Assert.That(match, Is.Not.Null);
            }

            return match;
        }

        private static object GetProperty(Component component, string name)
        {
            var property = component.GetType().GetProperty(name, InstanceFlags);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(component);
        }

        private static void SetField(Component component, string name, object value)
        {
            var field = component.GetType().GetField(name, InstanceFlags);
            Assert.That(field, Is.Not.Null);
            field.SetValue(component, value);
        }

        private static void Invoke(Component component, string methodName, params object[] arguments)
        {
            var method = component.GetType().GetMethod(methodName, InstanceFlags);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, arguments);
        }

        private static Delegate CreateKill100Handler(Type handlerType)
        {
            var parameter = Expression.Parameter(handlerType.GetMethod("Invoke").GetParameters()[0].ParameterType);
            var method = typeof(BattleSettlementUiTests).GetMethod(
                nameof(CountKill100Progress), BindingFlags.Static | BindingFlags.NonPublic);
            return Expression.Lambda(handlerType,
                Expression.Call(method, Expression.Convert(parameter, typeof(object))), parameter).Compile();
        }

        private static void CountKill100Progress(object achievement)
        {
            if ((string)achievement.GetType().GetField("id").GetValue(achievement) == "kill_100")
            {
                _kill100ProgressUpdates++;
            }
        }

        private static IEnumerator Capture(Canvas canvas, Camera camera, string fileName)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, fileName);
            var originalRenderMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalPlaneDistance = canvas.planeDistance;
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + 0.1f);
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                target.Create();
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(path, EncodeToPng(texture));
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(1024));
                var pixels = texture.GetPixels32();
                var darkPixels = 0;
                var colors = new HashSet<int>();
                foreach (var pixel in pixels)
                {
                    var luminance = (pixel.r * 299 + pixel.g * 587 + pixel.b * 114) / 1000;
                    if (pixel.a >= 200 && luminance <= 90)
                    {
                        darkPixels++;
                    }

                    colors.Add(((pixel.r >> 4) << 8) | ((pixel.g >> 4) << 4) | (pixel.b >> 4));
                }

                Assert.That(darkPixels, Is.GreaterThan(pixels.Length / 100),
                    $"{fileName} did not render the result panel.");
                Assert.That(colors.Count, Is.GreaterThan(16), $"{fileName} has no rendered UI detail.");
            }
            finally
            {
                camera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                Canvas.ForceUpdateCanvases();
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static byte[] EncodeToPng(Texture2D texture)
        {
            var imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
            Assert.That(imageConversion, Is.Not.Null);
            var method = imageConversion.GetMethod("EncodeToPNG", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (byte[])method.Invoke(null, new object[] { texture });
        }
    }
}
