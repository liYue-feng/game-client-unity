using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class CombatAssetGenerator
    {
        private const int SampleRate = 44100;
        private const float MaximumAmplitude = 0.949f;
        private const string ArcherAssetPath = "Assets/Resources/Sprites/Enemies/Archer.png";
        private const string EliteAssetPath = "Assets/Resources/Sprites/Enemies/Elite.png";
        private const string SoundsAssetPath = "Assets/Resources/Sounds";

        private static readonly Color32[] ArcherPalette =
        {
            new Color32(13, 20, 18, 255),
            new Color32(31, 64, 47, 255),
            new Color32(75, 124, 79, 255),
            new Color32(179, 160, 105, 255)
        };

        private static readonly Color32[] ElitePalette =
        {
            new Color32(25, 16, 18, 255),
            new Color32(101, 27, 34, 255),
            new Color32(174, 48, 49, 255),
            new Color32(218, 174, 74, 255)
        };

        private enum AudioRecipeKind
        {
            Tone,
            Noise,
            Sweep
        }

        private readonly struct AudioRecipe
        {
            public AudioRecipe(
                AudioRecipeKind kind,
                float duration,
                float startFrequency,
                float endFrequency,
                float amplitude)
            {
                Kind = kind;
                Duration = duration;
                StartFrequency = startFrequency;
                EndFrequency = endFrequency;
                Amplitude = amplitude;
            }

            public AudioRecipeKind Kind { get; }
            public float Duration { get; }
            public float StartFrequency { get; }
            public float EndFrequency { get; }
            public float Amplitude { get; }
        }

        private sealed class DeterministicNoise
        {
            private uint _state;

            public DeterministicNoise(int seed)
            {
                _state = unchecked((uint)seed);
                if (_state == 0)
                {
                    _state = 0x6D2B79F5u;
                }
            }

            public float NextSigned()
            {
                _state = unchecked(_state * 1664525u + 1013904223u);
                return ((_state >> 8) / 8388607.5f) - 1f;
            }
        }

        [MenuItem("Tools/Game/Generate Combat Assets")]
        public static void GenerateAll()
        {
            var createdSpritePaths = new List<string>();
            if (WriteIfMissing(ArcherAssetPath, GenerateEnemyPng("Archer", 64, 64, ArcherPalette, 4101)))
            {
                createdSpritePaths.Add(ArcherAssetPath);
            }

            if (WriteIfMissing(EliteAssetPath, GenerateEnemyPng("Elite", 96, 96, ElitePalette, 4201)))
            {
                createdSpritePaths.Add(EliteAssetPath);
            }

            GenerateSoundCatalogWavsIfMissing();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in createdSpritePaths)
            {
                ConfigureSpriteImporter(path);
            }

            AssetDatabase.SaveAssets();
            AssertLoadedSprite("Sprites/Enemies/Archer", ArcherAssetPath);
            AssertLoadedSprite("Sprites/Enemies/Elite", EliteAssetPath);
        }

        public static bool WriteIfMissing(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[CombatAssetGenerator] Skipped existing {path}");
                return false;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, bytes);
            Debug.Log($"[CombatAssetGenerator] Created {path}");
            return true;
        }

        private static byte[] GenerateEnemyPng(
            string enemyName,
            int width,
            int height,
            IReadOnlyList<Color32> palette,
            int seed)
        {
            var pixels = new Color32[width * height];
            if (enemyName == "Archer")
            {
                DrawArcher(pixels, width, height, palette, seed);
            }
            else if (enemyName == "Elite")
            {
                DrawElite(pixels, width, height, palette, seed);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(enemyName), enemyName, "Unknown generated enemy.");
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                return ImageConversion.EncodeToPNG(texture);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void DrawArcher(
            Color32[] pixels,
            int width,
            int height,
            IReadOnlyList<Color32> palette,
            int seed)
        {
            FillCircle(pixels, width, height, 29, 48, 7, palette[0]);
            FillCircle(pixels, width, height, 29, 49, 5, palette[3]);
            FillRect(pixels, width, height, 23, 24, 36, 43, palette[1]);
            FillTriangle(pixels, width, height, 18, 12, 39, 12, 30, 38, palette[2]);
            FillRect(pixels, width, height, 21, 9, 26, 25, palette[0]);
            FillRect(pixels, width, height, 33, 9, 38, 25, palette[0]);
            DrawLine(pixels, width, height, 22, 32, 45, 35, 3, palette[0]);
            DrawBow(pixels, width, height, 47, 34, 18, palette[3], palette[0]);
            ApplySeededFleck(pixels, width, height, seed, palette[0], 14, 20, 41, 43);
        }

        private static void DrawElite(
            Color32[] pixels,
            int width,
            int height,
            IReadOnlyList<Color32> palette,
            int seed)
        {
            FillCircle(pixels, width, height, 48, 70, 12, palette[0]);
            FillRect(pixels, width, height, 38, 65, 58, 76, palette[2]);
            FillTriangle(pixels, width, height, 26, 22, 70, 22, 48, 65, palette[1]);
            FillRect(pixels, width, height, 19, 39, 29, 64, palette[2]);
            FillRect(pixels, width, height, 67, 39, 77, 64, palette[2]);
            FillRect(pixels, width, height, 31, 10, 42, 30, palette[0]);
            FillRect(pixels, width, height, 54, 10, 65, 30, palette[0]);
            DrawLine(pixels, width, height, 27, 56, 16, 30, 5, palette[0]);
            DrawLine(pixels, width, height, 69, 56, 80, 30, 5, palette[0]);
            DrawLine(pixels, width, height, 29, 62, 67, 62, 4, palette[3]);
            DrawLine(pixels, width, height, 48, 79, 48, 88, 4, palette[3]);
            DrawLine(pixels, width, height, 40, 82, 48, 90, 3, palette[3]);
            DrawLine(pixels, width, height, 56, 82, 48, 90, 3, palette[3]);
            ApplySeededFleck(pixels, width, height, seed, palette[3], 25, 25, 71, 67);
        }

        private static void FillRect(
            Color32[] pixels,
            int width,
            int height,
            int minX,
            int minY,
            int maxX,
            int maxY,
            Color32 color)
        {
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    SetPixel(pixels, width, height, x, y, color);
                }
            }
        }

        private static void FillCircle(
            Color32[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            Color32 color)
        {
            var radiusSquared = radius * radius;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    var deltaX = x - centerX;
                    var deltaY = y - centerY;
                    if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                    {
                        SetPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static void FillTriangle(
            Color32[] pixels,
            int width,
            int height,
            int x1,
            int y1,
            int x2,
            int y2,
            int x3,
            int y3,
            Color32 color)
        {
            var minX = Math.Min(x1, Math.Min(x2, x3));
            var maxX = Math.Max(x1, Math.Max(x2, x3));
            var minY = Math.Min(y1, Math.Min(y2, y3));
            var maxY = Math.Max(y1, Math.Max(y2, y3));
            var area = Edge(x1, y1, x2, y2, x3, y3);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var edge1 = Edge(x1, y1, x2, y2, x, y);
                    var edge2 = Edge(x2, y2, x3, y3, x, y);
                    var edge3 = Edge(x3, y3, x1, y1, x, y);
                    if ((area >= 0 && edge1 >= 0 && edge2 >= 0 && edge3 >= 0) ||
                        (area < 0 && edge1 <= 0 && edge2 <= 0 && edge3 <= 0))
                    {
                        SetPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static int Edge(int x1, int y1, int x2, int y2, int x, int y)
        {
            return (x - x1) * (y2 - y1) - (y - y1) * (x2 - x1);
        }

        private static void DrawLine(
            Color32[] pixels,
            int width,
            int height,
            int x1,
            int y1,
            int x2,
            int y2,
            int thickness,
            Color32 color)
        {
            var deltaX = Math.Abs(x2 - x1);
            var stepX = x1 < x2 ? 1 : -1;
            var deltaY = -Math.Abs(y2 - y1);
            var stepY = y1 < y2 ? 1 : -1;
            var error = deltaX + deltaY;
            var radius = Math.Max(0, thickness / 2);

            while (true)
            {
                FillCircle(pixels, width, height, x1, y1, radius, color);
                if (x1 == x2 && y1 == y2)
                {
                    break;
                }

                var doubleError = error * 2;
                if (doubleError >= deltaY)
                {
                    error += deltaY;
                    x1 += stepX;
                }

                if (doubleError <= deltaX)
                {
                    error += deltaX;
                    y1 += stepY;
                }
            }
        }

        private static void DrawBow(
            Color32[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            Color32 bowColor,
            Color32 stringColor)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var squared = radius * radius - y * y;
                var x = centerX + (int)Math.Round(Math.Sqrt(Math.Max(0, squared)) * 0.42);
                SetPixel(pixels, width, height, x, centerY + y, bowColor);
                SetPixel(pixels, width, height, x + 1, centerY + y, bowColor);
            }

            DrawLine(pixels, width, height, centerX, centerY - radius, centerX, centerY + radius, 1, stringColor);
        }

        private static void ApplySeededFleck(
            Color32[] pixels,
            int width,
            int height,
            int seed,
            Color32 color,
            int minX,
            int minY,
            int maxX,
            int maxY)
        {
            var state = unchecked((uint)seed);
            for (var index = 0; index < 24; index++)
            {
                state = unchecked(state * 1103515245u + 12345u);
                var x = minX + (int)((state >> 16) % (uint)(maxX - minX + 1));
                state = unchecked(state * 1103515245u + 12345u);
                var y = minY + (int)((state >> 16) % (uint)(maxY - minY + 1));
                if (pixels[y * width + x].a != 0)
                {
                    SetPixel(pixels, width, height, x, y, color);
                }
            }
        }

        private static void SetPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            Color32 color)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                pixels[y * width + x] = color;
            }
        }

        private static void GenerateSoundCatalogWavsIfMissing()
        {
            foreach (var pair in SoundCatalog.Catalog)
            {
                var recipe = GetAudioRecipe(pair.Key);
                var seed = StableSeed(pair.Value.suggestedFile);
                var bytes = GenerateWav(recipe, seed);
                WriteIfMissing($"{SoundsAssetPath}/{pair.Value.suggestedFile}", bytes);
            }
        }

        private static AudioRecipe GetAudioRecipe(string key)
        {
            switch (key)
            {
                case "hit": return new AudioRecipe(AudioRecipeKind.Noise, 0.08f, 0f, 0f, 0.45f);
                case "slash": return new AudioRecipe(AudioRecipeKind.Sweep, 0.30f, 220f, 1100f, 0.42f);
                case "parry": return new AudioRecipe(AudioRecipeKind.Tone, 0.12f, 1320f, 0f, 0.50f);
                case "heavy_hit": return new AudioRecipe(AudioRecipeKind.Noise, 0.18f, 0f, 0f, 0.62f);
                case "punch": return new AudioRecipe(AudioRecipeKind.Noise, 0.06f, 0f, 0f, 0.40f);
                case "damage_taken": return new AudioRecipe(AudioRecipeKind.Tone, 0.15f, 280f, 0f, 0.35f);
                case "death": return new AudioRecipe(AudioRecipeKind.Sweep, 0.30f, 240f, 90f, 0.43f);
                case "boss_death": return new AudioRecipe(AudioRecipeKind.Noise, 0.50f, 0f, 0f, 0.70f);
                case "dash": return new AudioRecipe(AudioRecipeKind.Sweep, 0.15f, 180f, 900f, 0.38f);
                case "footstep": return new AudioRecipe(AudioRecipeKind.Noise, 0.04f, 0f, 0f, 0.24f);
                case "ui_click": return new AudioRecipe(AudioRecipeKind.Tone, 0.04f, 520f, 0f, 0.18f);
                case "ui_confirm": return new AudioRecipe(AudioRecipeKind.Sweep, 0.08f, 600f, 880f, 0.24f);
                case "ui_cancel": return new AudioRecipe(AudioRecipeKind.Tone, 0.08f, 180f, 0f, 0.22f);
                case "ui_coin": return new AudioRecipe(AudioRecipeKind.Tone, 0.08f, 1240f, 0f, 0.22f);
                case "levelup": return new AudioRecipe(AudioRecipeKind.Sweep, 0.30f, 360f, 1240f, 0.34f);
                case "exp_pickup": return new AudioRecipe(AudioRecipeKind.Tone, 0.08f, 720f, 0f, 0.22f);
                case "special_skill": return new AudioRecipe(AudioRecipeKind.Sweep, 0.40f, 100f, 1000f, 0.48f);
                case "buff_activate": return new AudioRecipe(AudioRecipeKind.Sweep, 0.25f, 480f, 1440f, 0.30f);
                case "ambient_wind": return new AudioRecipe(AudioRecipeKind.Noise, 2f, 0f, 0f, 0.12f);
                case "ambient_rain": return new AudioRecipe(AudioRecipeKind.Noise, 2f, 0f, 0f, 0.10f);
                case "bgm_menu": return new AudioRecipe(AudioRecipeKind.Tone, 3f, 196f, 0f, 0.18f);
                case "bgm_battle": return new AudioRecipe(AudioRecipeKind.Tone, 3f, 130f, 0f, 0.24f);
                case "bgm_boss": return new AudioRecipe(AudioRecipeKind.Tone, 3f, 65f, 0f, 0.30f);
                case "game_over": return new AudioRecipe(AudioRecipeKind.Sweep, 0.50f, 220f, 90f, 0.40f);
                case "victory": return new AudioRecipe(AudioRecipeKind.Sweep, 0.50f, 420f, 1320f, 0.40f);
                default:
                    throw new InvalidOperationException($"SoundCatalog key has no deterministic recipe: {key}");
            }
        }

        private static int StableSeed(string value)
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash = unchecked(hash * 16777619u);
            }

            return unchecked((int)hash);
        }

        private static byte[] GenerateWav(AudioRecipe recipe, int seed)
        {
            var sampleCount = (int)Math.Round(recipe.Duration * SampleRate, MidpointRounding.AwayFromZero);
            var dataLength = sampleCount * sizeof(short);
            using (var output = new MemoryStream(44 + dataLength))
            using (var writer = new BinaryWriter(output, Encoding.ASCII))
            {
                var noise = new DeterministicNoise(seed);
                var smoothedNoise = 0f;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataLength);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * sizeof(short));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataLength);

                for (var index = 0; index < sampleCount; index++)
                {
                    var time = index / (double)SampleRate;
                    var progress = sampleCount <= 1 ? 0d : index / (double)(sampleCount - 1);
                    var envelope = GetEnvelope(recipe.Duration, progress);
                    double sample;
                    switch (recipe.Kind)
                    {
                        case AudioRecipeKind.Noise:
                            smoothedNoise = smoothedNoise * 0.82f + noise.NextSigned() * 0.18f;
                            sample = smoothedNoise * recipe.Amplitude * envelope;
                            break;
                        case AudioRecipeKind.Sweep:
                            var frequencyRange = recipe.EndFrequency - recipe.StartFrequency;
                            var phase = 2d * Math.PI *
                                        (recipe.StartFrequency * time +
                                         0.5d * frequencyRange * time * time / recipe.Duration);
                            sample = Math.Sin(phase) * recipe.Amplitude * envelope;
                            break;
                        default:
                            sample = (Math.Sin(2d * Math.PI * recipe.StartFrequency * time) +
                                      0.25d * Math.Sin(4d * Math.PI * recipe.StartFrequency * time)) *
                                     recipe.Amplitude * envelope;
                            break;
                    }

                    sample = Math.Max(-MaximumAmplitude, Math.Min(MaximumAmplitude, sample));
                    writer.Write((short)Math.Round(sample * short.MaxValue, MidpointRounding.AwayFromZero));
                }

                writer.Flush();
                return output.ToArray();
            }
        }

        private static double GetEnvelope(float duration, double progress)
        {
            if (duration >= 2f)
            {
                var fadeIn = Math.Min(1d, progress / 0.04d);
                var fadeOut = Math.Min(1d, (1d - progress) / 0.04d);
                return Math.Max(0d, Math.Min(fadeIn, fadeOut));
            }

            return Math.Max(0d, 1d - progress);
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter not found for generated sprite: {assetPath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void AssertLoadedSprite(string resourcePath, string assetPath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"Sprite failed Resources.Load<Sprite>: {resourcePath} ({assetPath})");
            }

            if (!Mathf.Approximately(sprite.pixelsPerUnit, 64f))
            {
                throw new InvalidOperationException(
                    $"Sprite must use 64 PPU: {resourcePath} ({assetPath}), actual={sprite.pixelsPerUnit}");
            }
        }
    }
}
