using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed class BattleVisualEvidenceTests
    {
        private const int CaptureWidth = 960;
        private const int CaptureHeight = 540;
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly struct PixelMetrics
        {
            public PixelMetrics(
                int totalPixels,
                int opaquePixels,
                int darkPixels,
                int lightPixels,
                int chromaticPixels,
                int quantizedColorCount,
                double luminanceVariance)
            {
                TotalPixels = totalPixels;
                OpaquePixels = opaquePixels;
                DarkPixels = darkPixels;
                LightPixels = lightPixels;
                ChromaticPixels = chromaticPixels;
                QuantizedColorCount = quantizedColorCount;
                LuminanceVariance = luminanceVariance;
            }

            public int TotalPixels { get; }
            public int OpaquePixels { get; }
            public int DarkPixels { get; }
            public int LightPixels { get; }
            public int ChromaticPixels { get; }
            public int QuantizedColorCount { get; }
            public double LuminanceVariance { get; }

            public override string ToString()
            {
                return $"total={TotalPixels}, opaque={OpaquePixels}, dark={DarkPixels}, " +
                       $"light={LightPixels}, chromatic={ChromaticPixels}, " +
                       $"colors={QuantizedColorCount}, variance={LuminanceVariance:F2}";
            }
        }

        [Test]
        public void InkPanelConfigureValidatesDimensionsAndRebuildsSingleImage()
        {
            var panelObject = new GameObject("InkPanelConfigureProbe");
            try
            {
                panelObject.AddComponent<RectTransform>();
                var inkPanelType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("InkPanel"))
                    .Single(type => type != null);
                var panel = panelObject.AddComponent(inkPanelType);
                var configure = inkPanelType.GetMethod(
                    "Configure",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(int) },
                    null);
                Assert.That(configure, Is.Not.Null);

                var originalImage = panelObject.GetComponents<RawImage>().Single();
                var originalTexture = originalImage.texture;
                configure.Invoke(panel, new object[] { 600, 500 });

                var configuredImage = panelObject.GetComponents<RawImage>().Single();
                Assert.That(configuredImage, Is.SameAs(originalImage));
                Assert.That(configuredImage.texture, Is.Not.SameAs(originalTexture));
                Assert.That(configuredImage.texture.width, Is.EqualTo(600));
                Assert.That(configuredImage.texture.height, Is.EqualTo(500));
                Assert.That(panelObject.GetComponent<RectTransform>().rect.size,
                    Is.EqualTo(new Vector2(600f, 500f)));

                foreach (var invalidSize in new[] { new Vector2Int(0, 500), new Vector2Int(600, 0) })
                {
                    var exception = Assert.Throws<TargetInvocationException>(
                        () => configure.Invoke(panel, new object[] { invalidSize.x, invalidSize.y }));
                    Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
                    Assert.That(configuredImage.texture.width, Is.EqualTo(600));
                    Assert.That(configuredImage.texture.height, Is.EqualTo(500));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [UnityTest]
        public IEnumerator InkPanelPreservesExternalTexturesAndDestroysOnlyOwnedTextures()
        {
            GameObject panelObject = null;
            Texture2D externalTexture = null;
            try
            {
                panelObject = new GameObject("InkPanelOwnershipProbe");
                panelObject.AddComponent<RectTransform>();
                var image = panelObject.AddComponent<RawImage>();
                externalTexture = new Texture2D(8, 8);
                image.texture = externalTexture;

                var inkPanelType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("InkPanel"))
                    .Single(type => type != null);
                var panel = panelObject.AddComponent(inkPanelType);
                var configure = inkPanelType.GetMethod(
                    "Configure",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(int) },
                    null);
                Assert.That(configure, Is.Not.Null);

                var firstOwnedTexture = image.texture;
                Assert.That(firstOwnedTexture, Is.Not.SameAs(externalTexture));
                configure.Invoke(panel, new object[] { 600, 500 });
                var secondOwnedTexture = image.texture;
                configure.Invoke(panel, new object[] { 320, 240 });
                var finalOwnedTexture = image.texture;
                Assert.That(panelObject.GetComponents<RawImage>(), Has.Length.EqualTo(1));

                yield return null;

                Assert.That(externalTexture == null, Is.False,
                    "InkPanel must not destroy a texture supplied by another owner.");
                Assert.That(firstOwnedTexture == null, Is.True);
                Assert.That(secondOwnedTexture == null, Is.True);
                Assert.That(finalOwnedTexture == null, Is.False);

                image.texture = externalTexture;
                UnityEngine.Object.Destroy(panelObject);
                yield return null;

                Assert.That(panelObject == null, Is.True);
                Assert.That(finalOwnedTexture == null, Is.True);
                Assert.That(externalTexture == null, Is.False,
                    "Destroying InkPanel must preserve an externally replaced texture.");
            }
            finally
            {
                if (panelObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(panelObject);
                }

                if (externalTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(externalTexture);
                }
            }
        }

        [UnityTest]
        public IEnumerator CombatWorldCaptureShowsRealAttackFraming()
        {
            var outputPath = PrepareOutput("phase-b1-combat.png");
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            var attackHitbox = FindComponent(GameObject.Find("AttackHitbox"), "Hitbox");
            Component grunt = null;
            yield return WaitForActiveSceneComponent("Grunt", found => grunt = found);

            StopWaveAndEnemyMotion(grunt);
            var playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.velocity = Vector2.zero;
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            var gruntBody = grunt.GetComponent<Rigidbody2D>();
            gruntBody.velocity = Vector2.zero;
            gruntBody.bodyType = RigidbodyType2D.Kinematic;
            grunt.transform.position = GameObject.Find("AttackHitbox").transform.position;
            SetIntField(grunt, "maxHp", 500);
            SetIntField(grunt, "hp", 500);
            Physics2D.SyncTransforms();

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var originalOrthographic = camera.orthographic;
            var originalOrthographicSize = camera.orthographicSize;
            var originalPosition = camera.transform.position;
            try
            {
                FrameWorld(camera, player, grunt.gameObject);
                var playerProjectedHeight = AssertProjectedSpriteHeight(
                    camera,
                    player.GetComponent<SpriteRenderer>(),
                    "Player");
                var gruntProjectedHeight = AssertProjectedSpriteHeight(
                    camera,
                    grunt.GetComponent<SpriteRenderer>(),
                    "Grunt");
                Invoke(stateMachine, "RequestAttack");
                Assert.That(GetPropertyValue(stateMachine, "CurrentState")?.ToString(), Is.EqualTo("Attack1"));

                PixelMetrics? metrics = null;
                for (var frame = 0; frame < 180; frame++)
                {
                    var phase = GetFieldValue(stateMachine, "_attackPhase")?.ToString();
                    var hitboxActive = (bool)GetPropertyValue(attackHitbox, "IsActive");
                    if (phase == "Active" && hitboxActive)
                    {
                        metrics = Capture(camera, outputPath);
                        break;
                    }

                    yield return new WaitForSeconds(0.01f);
                }

                Assert.That(metrics.HasValue, Is.True,
                    "The combat evidence must be captured during the real Attack1 active window.");
                yield return new WaitForFixedUpdate();
                Assert.That(GetIntField(grunt, "hp"), Is.LessThan(500),
                    "The framed Grunt must receive the real Attack1 hit.");
                Assert.That(GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(camera),
                    player.GetComponent<SpriteRenderer>().bounds), Is.True);
                Assert.That(GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(camera),
                    grunt.GetComponent<SpriteRenderer>().bounds), Is.True);

                var value = metrics.Value;
                AssertCaptureFile(outputPath, value);
                Assert.That(value.LuminanceVariance, Is.GreaterThan(80d), value.ToString());
                Assert.That(value.DarkPixels, Is.GreaterThan(value.TotalPixels / 200), value.ToString());
                Assert.That(value.LightPixels, Is.GreaterThan(value.TotalPixels / 5), value.ToString());
                Assert.That(value.ChromaticPixels, Is.GreaterThan(value.TotalPixels / 50), value.ToString());
                Assert.That(value.QuantizedColorCount, Is.GreaterThan(16), value.ToString());
                Debug.Log(
                    $"[BattleVisualEvidence] combat {value}; " +
                    $"playerHeight={playerProjectedHeight:F2}px, gruntHeight={gruntProjectedHeight:F2}px; " +
                    $"path={outputPath}");
            }
            finally
            {
                camera.orthographic = originalOrthographic;
                camera.orthographicSize = originalOrthographicSize;
                camera.transform.position = originalPosition;
            }
        }

        [UnityTest]
        public IEnumerator ResultCaptureShowsRealDefeatOverlayAndRestartButton()
        {
            var outputPath = PrepareOutput("phase-b1-result.png");
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var hurtbox = FindComponent(player, "Hurtbox");
            var result = ReceiveCombatHit(hurtbox, new CombatHit(100000, 1f, 3f, false, null));
            Assert.That(result, Is.EqualTo(CombatHitResult.Damaged));
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

            var gameOver = FindUniqueActiveSceneComponent("GameOverUI");
            var canvas = gameOver.GetComponentsInChildren<Canvas>(true).SingleOrDefault();
            Assert.That(canvas, Is.Not.Null);
            var resultPanel = FindDescendant(gameOver.transform, "ResultPanel");
            Assert.That(resultPanel, Is.Not.Null);
            Assert.That(FindDescendant(gameOver.transform, "Title"), Is.Not.Null);
            var buttons = gameOver.GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(2));
            Assert.That(buttons.Select(button => button.gameObject.name),
                Is.EquivalentTo(new[] { "BtnRestart", "BtnMainMenu" }));
            var panelRect = resultPanel.GetComponent<RectTransform>();
            Assert.That(panelRect.rect.size, Is.EqualTo(new Vector2(600f, 500f)));
            var panelImage = resultPanel.GetComponents<RawImage>().Single();
            Assert.That(panelImage.texture, Is.Not.Null);
            Assert.That(panelImage.texture.width, Is.EqualTo((int)panelRect.rect.width));
            Assert.That(panelImage.texture.height, Is.EqualTo((int)panelRect.rect.height));
            var restartRect = FindDescendant(gameOver.transform, "BtnRestart").GetComponent<RectTransform>();
            var menuRect = FindDescendant(gameOver.transform, "BtnMainMenu").GetComponent<RectTransform>();
            var restartBounds = GetBoundsInParent(restartRect, panelRect);
            var menuBounds = GetBoundsInParent(menuRect, panelRect);
            Assert.That(panelRect.rect.Contains(restartBounds.min), Is.True);
            Assert.That(panelRect.rect.Contains(restartBounds.max), Is.True);
            Assert.That(panelRect.rect.Contains(menuBounds.min), Is.True);
            Assert.That(panelRect.rect.Contains(menuBounds.max), Is.True);
            Assert.That(restartBounds.Overlaps(menuBounds), Is.False);
            var upperBounds = restartBounds.center.y > menuBounds.center.y ? restartBounds : menuBounds;
            var lowerBounds = restartBounds.center.y > menuBounds.center.y ? menuBounds : restartBounds;
            Assert.That(upperBounds.yMin - lowerBounds.yMax, Is.GreaterThan(0f));

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var originalRenderMode = canvas.renderMode;
            var originalWorldCamera = canvas.worldCamera;
            var originalPlaneDistance = canvas.planeDistance;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + 0.1f);
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;

                var metrics = Capture(camera, outputPath);
                AssertCaptureFile(outputPath, metrics);
                Assert.That(metrics.LuminanceVariance, Is.GreaterThan(100d), metrics.ToString());
                Assert.That(metrics.DarkPixels, Is.GreaterThan(metrics.TotalPixels / 4), metrics.ToString());
                Assert.That(metrics.LightPixels, Is.GreaterThan(metrics.TotalPixels / 12), metrics.ToString());
                Assert.That(metrics.QuantizedColorCount, Is.GreaterThan(16), metrics.ToString());
                Debug.Log($"[BattleVisualEvidence] result {metrics}; path={outputPath}");
            }
            finally
            {
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalWorldCamera;
                canvas.planeDistance = originalPlaneDistance;
                Canvas.ForceUpdateCanvases();
            }
        }

        private static IEnumerator LoadBattleScene()
        {
            yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            for (var frame = 0; frame < 120; frame++)
            {
                var applicationObject = GameObject.Find("[GameApplication]");
                var application = applicationObject == null
                    ? null
                    : applicationObject.GetComponents<Component>()
                        .FirstOrDefault(component => component != null && component.GetType().Name == "GameApplication");
                if (application?.GetType().GetProperty("State")?.GetValue(application)?.ToString() == "Ready")
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("GameApplication did not reach Ready within 120 frames.");
        }

        private static IEnumerator WaitForActiveSceneComponent(string typeName, Action<Component> found)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var activeScene = SceneManager.GetActiveScene();
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

            Assert.Fail($"No active-scene {typeName} appeared within 5 realtime seconds.");
        }

        private static void StopWaveAndEnemyMotion(Component grunt)
        {
            var spawner = FindUniqueActiveSceneComponent("WaveSpawner") as MonoBehaviour;
            spawner.StopAllCoroutines();
            spawner.enabled = false;
            if (grunt is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }
        }

        private static void FrameWorld(Camera camera, params GameObject[] objects)
        {
            var renderers = objects
                .Where(item => item != null)
                .Select(item => item.GetComponent<SpriteRenderer>())
                .Where(renderer => renderer != null)
                .ToArray();
            Assert.That(renderers, Is.Not.Empty);

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            camera.orthographic = true;
            var aspect = (float)CaptureWidth / CaptureHeight;
            camera.orthographicSize = Mathf.Max(
                2.25f,
                bounds.extents.y * 1.15f,
                bounds.extents.x / aspect * 1.15f);
            camera.transform.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                camera.transform.position.z);
        }

        private static PixelMetrics Capture(Camera camera, string outputPath)
        {
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
                target.Create();
                texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(outputPath, EncodePng(texture));
                return Analyze(texture.GetPixels32());
            }
            finally
            {
                camera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static PixelMetrics Analyze(IReadOnlyList<Color32> pixels)
        {
            long luminanceSum = 0;
            long luminanceSquaredSum = 0;
            var opaque = 0;
            var dark = 0;
            var light = 0;
            var chromatic = 0;
            var colors = new HashSet<int>();
            foreach (var pixel in pixels)
            {
                var luminance = (pixel.r * 299 + pixel.g * 587 + pixel.b * 114) / 1000;
                luminanceSum += luminance;
                luminanceSquaredSum += luminance * luminance;
                if (pixel.a >= 240) opaque++;
                if (pixel.a >= 200 && luminance <= 90) dark++;
                if (pixel.a >= 200 && luminance >= 190) light++;
                var max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                var min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                if (pixel.a >= 200 && max - min >= 24) chromatic++;
                colors.Add(((pixel.r >> 4) << 8) | ((pixel.g >> 4) << 4) | (pixel.b >> 4));
            }

            var mean = (double)luminanceSum / pixels.Count;
            var variance = (double)luminanceSquaredSum / pixels.Count - mean * mean;
            return new PixelMetrics(
                pixels.Count,
                opaque,
                dark,
                light,
                chromatic,
                colors.Count,
                variance);
        }

        private static byte[] EncodePng(Texture2D texture)
        {
            var imageConversionType = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                false);
            if (imageConversionType == null)
            {
                throw new InvalidOperationException("UnityEngine.ImageConversionModule is not loaded.");
            }

            var encodeMethod = imageConversionType.GetMethod(
                "EncodeToPNG",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Texture2D) },
                null);
            if (encodeMethod == null)
            {
                throw new MissingMethodException(imageConversionType.FullName, "EncodeToPNG(Texture2D)");
            }

            var bytes = encodeMethod.Invoke(null, new object[] { texture }) as byte[];
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException("Unity ImageConversion returned an empty PNG.");
            }

            return bytes;
        }

        private static void AssertCaptureFile(string outputPath, PixelMetrics metrics)
        {
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(1024));
            Assert.That(metrics.TotalPixels, Is.EqualTo(CaptureWidth * CaptureHeight));
            Assert.That(metrics.OpaquePixels, Is.GreaterThan(metrics.TotalPixels * 95 / 100), metrics.ToString());
        }

        private static float AssertProjectedSpriteHeight(
            Camera camera,
            SpriteRenderer renderer,
            string label)
        {
            Assert.That(renderer, Is.Not.Null);
            var projectedHeight = renderer.bounds.size.y / (camera.orthographicSize * 2f) * CaptureHeight;
            Assert.That(projectedHeight, Is.GreaterThanOrEqualTo(24f),
                $"{label} must remain inspectable in the 960x540 evidence frame; projected={projectedHeight:F2}px.");
            return projectedHeight;
        }

        private static string PrepareOutput(string fileName)
        {
            var logsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDirectory);
            var outputPath = Path.Combine(logsDirectory, fileName);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            return outputPath;
        }

        private static CombatHitResult ReceiveCombatHit(Component hurtbox, CombatHit hit)
        {
            var method = hurtbox.GetType().GetMethod(
                "ReceiveHit",
                InstanceFlags,
                null,
                new[] { typeof(CombatHit) },
                null);
            Assert.That(method, Is.Not.Null);
            return (CombatHitResult)method.Invoke(hurtbox, new object[] { hit });
        }

        private static Component FindComponent(GameObject gameObject, string typeName)
        {
            Assert.That(gameObject, Is.Not.Null, $"Expected GameObject for {typeName}.");
            var component = gameObject.GetComponents<Component>()
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, $"Expected {gameObject.name} to contain {typeName}.");
            return component;
        }

        private static Component FindUniqueActiveSceneComponent(string typeName)
        {
            var activeScene = SceneManager.GetActiveScene();
            var matches = Resources.FindObjectsOfTypeAll<Component>()
                .Where(item => item != null
                               && item.GetType().Name == typeName
                               && item.gameObject.scene == activeScene)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), $"Expected one active-scene {typeName}.");
            return matches.Single();
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == objectName)
                ?.gameObject;
        }

        private static Rect GetBoundsInParent(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var first = parent.InverseTransformPoint(corners[0]);
            var min = new Vector2(first.x, first.y);
            var max = min;
            for (var index = 1; index < corners.Length; index++)
            {
                var local = parent.InverseTransformPoint(corners[index]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static object GetFieldValue(Component component, string fieldName)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            return field.GetValue(component);
        }

        private static object GetPropertyValue(Component component, string propertyName)
        {
            var property = component.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null, $"Expected {component.GetType().Name}.{propertyName}.");
            return property.GetValue(component);
        }

        private static int GetIntField(Component component, string fieldName)
        {
            return (int)GetFieldValue(component, fieldName);
        }

        private static void SetIntField(Component component, string fieldName, int value)
        {
            var field = component.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {component.GetType().Name}.{fieldName}.");
            field.SetValue(component, value);
        }

        private static void Invoke(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(methodName, InstanceFlags);
            Assert.That(method, Is.Not.Null, $"Expected {component.GetType().Name}.{methodName}().");
            method.Invoke(component, null);
        }
    }
}
