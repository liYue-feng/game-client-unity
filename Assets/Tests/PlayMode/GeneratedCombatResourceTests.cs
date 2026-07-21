using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class GeneratedCombatResourceTests
    {
        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ArcherAndEliteLoadThroughAiSpriteLoaderAndRenderInsideViewport()
        {
            var loaderType = FindType("AiSpriteLoader");
            ResetSpriteLoader(loaderType);

            var archerResource = Resources.Load<Sprite>("Sprites/Enemies/Archer");
            var eliteResource = Resources.Load<Sprite>("Sprites/Enemies/Elite");
            Assert.That(archerResource, Is.Not.Null,
                "Missing generated Archer resource at Resources/Sprites/Enemies/Archer.");
            Assert.That(eliteResource, Is.Not.Null,
                "Missing generated Elite resource at Resources/Sprites/Enemies/Elite.");

            var archer = InvokeSprite(loaderType, "ArcherSprite");
            var elite = InvokeSprite(loaderType, "EliteSprite");
            Assert.That(archer, Is.SameAs(archerResource));
            Assert.That(elite, Is.SameAs(eliteResource));
            Assert.That(archer, Is.Not.SameAs(elite));
            AssertSpriteContract(archer, 64, 64, "Archer");
            AssertSpriteContract(elite, 96, 96, "Elite");

            AssertRenderedInsideViewport(archer, "Archer");
            AssertRenderedInsideViewport(elite, "Elite");
            yield return null;
        }

        [Test]
        public void EverySoundCatalogEntryLoadsThroughAudioManagerFromResources()
        {
            var audioType = FindType("AudioManager");
            var catalogType = FindType("SoundCatalog");
            var host = new GameObject("[GeneratedCombatResourceTests.AudioManager]");
            try
            {
                var audio = host.AddComponent(audioType);
                audioType.GetMethod("LoadAllSounds", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(audio, null);
                var isLoaded = audioType.GetMethod(
                    "IsLoadedFromResources",
                    BindingFlags.Instance | BindingFlags.Public);
                var catalog = (IEnumerable)catalogType
                    .GetField("Catalog", StaticFlags)
                    .GetValue(null);

                foreach (var pair in catalog)
                {
                    var pairType = pair.GetType();
                    var key = (string)pairType.GetProperty("Key").GetValue(pair);
                    var entry = pairType.GetProperty("Value").GetValue(pair);
                    var suggestedFile = (string)entry.GetType().GetField("suggestedFile").GetValue(entry);
                    var resourcePath = $"Sounds/{System.IO.Path.GetFileNameWithoutExtension(suggestedFile)}";

                    Assert.That((bool)isLoaded.Invoke(audio, new object[] { key }), Is.True,
                        $"AudioManager did not load '{key}' from {resourcePath}.");
                    var clip = Resources.Load<AudioClip>(resourcePath);
                    Assert.That(clip, Is.Not.Null, $"Missing generated audio resource {resourcePath}.");
                    Assert.That(clip.samples, Is.GreaterThan(0), $"Audio resource {resourcePath} is empty.");
                    Assert.That(clip.length, Is.GreaterThan(0f), $"Audio resource {resourcePath} has no duration.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Type FindType(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Runtime type '{name}' was not found.");
            return type;
        }

        private static void ResetSpriteLoader(Type loaderType)
        {
            foreach (var field in loaderType.GetFields(StaticFlags))
            {
                if (field.FieldType == typeof(bool) && field.Name == "_resourcesLoaded")
                {
                    field.SetValue(null, false);
                }
                else if (field.FieldType == typeof(Sprite))
                {
                    field.SetValue(null, null);
                }
            }
        }

        private static Sprite InvokeSprite(Type loaderType, string methodName)
        {
            return (Sprite)loaderType.GetMethod(methodName, StaticFlags).Invoke(null, null);
        }

        private static void AssertSpriteContract(Sprite sprite, int width, int height, string label)
        {
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(64f).Within(0.001f),
                $"{label} must preserve the imported 64 PPU Sprite metadata.");
            Assert.That(sprite.rect.width, Is.EqualTo(width).Within(0.001f));
            Assert.That(sprite.rect.height, Is.EqualTo(height).Within(0.001f));
            Assert.That(sprite.texture.width, Is.EqualTo(width));
            Assert.That(sprite.texture.height, Is.EqualTo(height));
        }

        private static void AssertRenderedInsideViewport(Sprite sprite, string label)
        {
            const int size = 128;
            var cameraObject = new GameObject($"{label}.Camera");
            var spriteObject = new GameObject($"{label}.Sprite");
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var originalActive = RenderTexture.active;

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y) * 1.35f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.targetTexture = target;
                camera.cullingMask = 1 << 31;

                spriteObject.layer = 31;
                spriteObject.AddComponent<SpriteRenderer>().sprite = sprite;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, false);
                texture.Apply(false, false);

                var pixels = texture.GetPixels32();
                var minX = size;
                var minY = size;
                var maxX = -1;
                var maxY = -1;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        if (pixels[y * size + x].a == 0)
                        {
                            continue;
                        }

                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                Assert.That(maxX, Is.GreaterThanOrEqualTo(minX), $"{label} rendered an empty alpha bounding box.");
                Assert.That(maxY, Is.GreaterThanOrEqualTo(minY), $"{label} rendered an empty alpha bounding box.");
                Assert.That(minX, Is.GreaterThanOrEqualTo(1), $"{label} touches the left viewport edge.");
                Assert.That(minY, Is.GreaterThanOrEqualTo(1), $"{label} touches the bottom viewport edge.");
                Assert.That(maxX, Is.LessThanOrEqualTo(size - 2), $"{label} touches the right viewport edge.");
                Assert.That(maxY, Is.LessThanOrEqualTo(size - 2), $"{label} touches the top viewport edge.");
            }
            finally
            {
                RenderTexture.active = originalActive;
                var camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.targetTexture = null;
                }
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(spriteObject);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
