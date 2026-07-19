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
    public sealed class BattleEnemyVisualEvidenceTests
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

        private readonly struct CapturedFrame
        {
            public CapturedFrame(PixelMetrics metrics, Color32[] pixels)
            {
                Metrics = metrics;
                Pixels = pixels;
            }

            public PixelMetrics Metrics { get; }
            public Color32[] Pixels { get; }
        }

        private sealed class FeedbackSnapshot
        {
            public int AppliedDamage { get; set; }
            public Component DamageNumber { get; set; }
            public List<GameObject> InkParticles { get; set; }
        }

        private readonly struct BattleHudCanvasState
        {
            public BattleHudCanvasState(Canvas canvas, Camera camera)
            {
                Canvas = canvas;
                Camera = camera;
                RenderMode = canvas.renderMode;
                WorldCamera = canvas.worldCamera;
                PlaneDistance = canvas.planeDistance;
                CameraTarget = camera.targetTexture;
                ActiveRenderTexture = RenderTexture.active;
            }

            public Canvas Canvas { get; }
            public Camera Camera { get; }
            public RenderMode RenderMode { get; }
            public Camera WorldCamera { get; }
            public float PlaneDistance { get; }
            public RenderTexture CameraTarget { get; }
            public RenderTexture ActiveRenderTexture { get; }
        }

        [UnityTest]
        public IEnumerator RealWaveEngagementShowsReachableEnemyFeedbackAndObjectiveHud()
        {
            var startedAt = DateTime.UtcNow;
            var output = PrepareOutput("phase-b2-wave-combat.png");
            Assert.That(File.Exists(output), Is.False, "Wave evidence must not reuse stale output.");
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var spawner = FindUniqueActiveSceneComponent("WaveSpawner") as MonoBehaviour;
            Component grunt = null;
            yield return WaitForActiveEnemy("Grunt", found => grunt = found);
            Assert.That(grunt, Is.Not.Null);

            spawner.StopAllCoroutines();
            spawner.enabled = false;
            DisableOtherActiveEnemies(grunt);
            SetFieldValue(grunt, "telegraphDuration", 999f);
            FacePlayerToward(player, grunt.gameObject);
            AssertNoActiveHitFeedback();

            var initialDistance = Mathf.Abs(grunt.transform.position.x - player.transform.position.x);
            yield return WaitUntilPlayerAndEnemyShareCamera(player, grunt.gameObject, camera, 300);
            var finalDistance = Mathf.Abs(grunt.transform.position.x - player.transform.position.x);
            Assert.That(finalDistance, Is.LessThan(initialDistance),
                "The selected Grunt must approach through its real AI rather than test repositioning.");
            FreezeEnemyWithoutRepositioning(grunt);

            var hpBefore = GetIntField(grunt, "hp");
            TriggerRealPlayerAttackWhenInRange(player, grunt.gameObject);
            FeedbackSnapshot feedback = null;
            yield return WaitForResolvedHitFeedback(grunt, hpBefore, found => feedback = found, 180);
            Assert.That(feedback, Is.Not.Null);

            CapturedFrame frame = default;
            CapturedFrame withoutNumber = default;
            CapturedFrame withoutInk = default;
            var captureState = ConfigureBattleHudCanvasForCapture(camera);
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                frame = CaptureWorldAndBattleHudSynchronous(
                    camera,
                    output,
                    CaptureWidth,
                    CaptureHeight);
                var numberRenderer = feedback.DamageNumber.GetComponentInChildren<Renderer>(true);
                var inkRenderers = feedback.InkParticles
                    .Select(particle => particle.GetComponent<SpriteRenderer>())
                    .Cast<Renderer>()
                    .ToArray();
                Assert.That(numberRenderer.isVisible, Is.True,
                    "Damage number renderer must be visible immediately after the evidence capture.");
                Assert.That(inkRenderers.All(renderer => renderer.isVisible), Is.True,
                    "Ink renderers must be visible immediately after the evidence capture.");
                withoutNumber = CaptureWithRenderersHiddenSynchronous(camera, numberRenderer);
                withoutInk = CaptureWithRenderersHiddenSynchronous(camera, inkRenderers);
                AssertWaveFrame(
                    frame,
                    withoutNumber,
                    withoutInk,
                    output,
                    startedAt,
                    player,
                    grunt,
                    feedback,
                    camera,
                    captureState.Canvas);
            }
            finally
            {
                RestoreBattleHudCanvas(captureState);
            }

            Debug.Log(
                $"[BattleEnemyVisualEvidence] wave {frame.Metrics}; " +
                $"playerHeight={ProjectedSpriteHeight(camera, player.GetComponent<SpriteRenderer>()):F2}px, " +
                $"gruntHeight={ProjectedSpriteHeight(camera, grunt.GetComponent<SpriteRenderer>()):F2}px, " +
                $"appliedDamage={feedback.AppliedDamage}, ink={feedback.InkParticles.Count}, path={output}");
        }

        [UnityTest]
        public IEnumerator RealBossCircleTelegraphShowsBossHudAndReadableDangerArea()
        {
            var startedAt = DateTime.UtcNow;
            var output = PrepareOutput("phase-b2-boss-telegraph.png");
            Assert.That(File.Exists(output), Is.False, "Boss evidence must not reuse stale output.");
            yield return LoadBattleScene();

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var spawner = FindUniqueActiveSceneComponent("WaveSpawner");
            ((MonoBehaviour)spawner).StopAllCoroutines();
            DisableOtherActiveEnemies(null);

            var boss = SpawnRealBossThroughCurrentSpawner(spawner);
            SetFieldValue(boss, "attackRange", 0f);
            var initialDistance = Mathf.Abs(boss.transform.position.x - player.transform.position.x);
            yield return WaitForNaturalBossFraming(player, boss, camera, 300);
            var finalDistance = Mathf.Abs(boss.transform.position.x - player.transform.position.x);
            Assert.That(finalDistance, Is.LessThan(initialDistance),
                "The pooled Boss must enter the evidence frame through runtime movement.");
            FreezeBodyWithoutDisabling(boss);

            var randomStateBeforeSeed = UnityEngine.Random.state;
            var expectedNextRandomValue = UnityEngine.Random.value;
            UnityEngine.Random.state = randomStateBeforeSeed;
            try
            {
                SetDeterministicBossAoeSeedAndAssertPreparedAttackId(boss, "boss_aoe");
                var actualNextRandomValue = UnityEngine.Random.value;
                Assert.That(
                    actualNextRandomValue,
                    Is.EqualTo(expectedNextRandomValue),
                    "B2_TASK7_RED_RANDOM_STATE: deterministic Boss selection must restore UnityEngine.Random.state.");
            }
            finally
            {
                UnityEngine.Random.state = randomStateBeforeSeed;
            }
            Component telegraph = null;
            yield return WaitForVisibleTelegraph(boss, "Circle", found => telegraph = found, 180);

            CapturedFrame frame = default;
            CapturedFrame withoutTelegraph = default;
            var captureState = ConfigureBattleHudCanvasForCapture(camera);
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                frame = CaptureWorldAndBattleHudSynchronous(
                    camera,
                    output,
                    CaptureWidth,
                    CaptureHeight);
                var line = telegraph.GetComponentInChildren<LineRenderer>(true);
                Assert.That(line.isVisible, Is.True,
                    "Boss Circle renderer must be visible immediately after the evidence capture.");
                withoutTelegraph = CaptureWithRenderersHiddenSynchronous(camera, line);
                AssertBossFrame(
                    frame,
                    withoutTelegraph,
                    output,
                    startedAt,
                    player,
                    boss,
                    telegraph,
                    camera,
                    captureState.Canvas);
            }
            finally
            {
                RestoreBattleHudCanvas(captureState);
            }

            var plan = (EnemyAttackPlan)GetPropertyValue(boss, "CurrentAttackPlan");
            Debug.Log(
                $"[BattleEnemyVisualEvidence] boss {frame.Metrics}; " +
                $"bossHeight={ProjectedSpriteHeight(camera, boss.GetComponent<SpriteRenderer>()):F2}px, " +
                $"telegraphDiameter={ProjectedWorldHeight(camera, plan.Radius * 2f):F2}px, " +
                $"radius={plan.Radius:F2}, path={output}");
        }

        private static IEnumerator LoadBattleScene()
        {
            Time.timeScale = 1f;
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

        private static IEnumerator WaitForActiveEnemy(string typeName, Action<Component> found)
        {
            for (var frame = 0; frame < 300; frame++)
            {
                var activeScene = SceneManager.GetActiveScene();
                var component = Resources.FindObjectsOfTypeAll<Component>()
                    .FirstOrDefault(item => item != null &&
                                            item.GetType().Name == typeName &&
                                            item.gameObject.scene == activeScene &&
                                            item.gameObject.activeInHierarchy);
                if (component != null)
                {
                    found(component);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"No active {typeName} appeared within 300 frames.");
        }

        private static IEnumerator WaitUntilPlayerAndEnemyShareCamera(
            GameObject player,
            GameObject enemy,
            Camera camera,
            int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (IsFullyInsideViewport(camera, player.GetComponent<SpriteRenderer>().bounds) &&
                    IsFullyInsideViewport(camera, enemy.GetComponent<SpriteRenderer>().bounds) &&
                    RealAttackBoundsOverlapEnemy(enemy))
                {
                    StopPlayerMovement(player);
                    yield return new WaitForFixedUpdate();
                    yield break;
                }

                DrivePlayerToward(player, enemy);
                yield return new WaitForFixedUpdate();
            }

            StopPlayerMovement(player);
            var hitboxObject = GameObject.Find("AttackHitbox");
            var hitbox = hitboxObject?.GetComponent<BoxCollider2D>();
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            var enemyComponent = enemy.GetComponents<Component>()
                .FirstOrDefault(component => component != null && IsEnemyType(component.GetType()));
            Assert.Fail(
                "Player and the non-repositioned Grunt did not share the camera within real attack range. " +
                $"player={player.transform.position}, enemy={enemy.transform.position}, " +
                $"state={GetPropertyValue(enemyComponent, "CurrentState")}, " +
                $"enabled={(enemyComponent as Behaviour)?.enabled}, velocity={enemyBody?.velocity}, " +
                $"playerVisible={IsFullyInsideViewport(camera, player.GetComponent<SpriteRenderer>().bounds)}, " +
                $"enemyVisible={IsFullyInsideViewport(camera, enemy.GetComponent<SpriteRenderer>().bounds)}, " +
                $"overlap={RealAttackBoundsOverlapEnemy(enemy)}, hitboxPosition={hitboxObject?.transform.position}, " +
                $"hitboxSize={hitbox?.size}, enemyBounds={enemy.GetComponent<Collider2D>()?.bounds}.");
        }

        private static IEnumerator WaitForNaturalBossFraming(
            GameObject player,
            Component boss,
            Camera camera,
            int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var horizontalDistance = Mathf.Abs(boss.transform.position.x - player.transform.position.x);
                if (horizontalDistance <= 1.25f &&
                    IsFullyInsideViewport(camera, boss.GetComponent<SpriteRenderer>().bounds))
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }

            var body = boss.GetComponent<Rigidbody2D>();
            Assert.Fail(
                "The real pooled Boss did not naturally enter the centered camera frame. " +
                $"player={player.transform.position}, boss={boss.transform.position}, " +
                $"distance={Mathf.Abs(boss.transform.position.x - player.transform.position.x):F3}, " +
                $"state={GetPropertyValue(boss, "CurrentState")}, enabled={(boss as Behaviour)?.enabled}, " +
                $"velocity={body?.velocity}, visible={IsFullyInsideViewport(camera, boss.GetComponent<SpriteRenderer>().bounds)}.");
        }

        private static void DisableOtherActiveEnemies(Component keep)
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component == null ||
                    ReferenceEquals(component, keep) ||
                    component.gameObject.scene != activeScene ||
                    !component.gameObject.activeInHierarchy ||
                    !IsEnemyType(component.GetType()))
                {
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }

                var collider = component.GetComponent<Collider2D>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                var body = component.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.simulated = false;
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

        private static void FacePlayerToward(GameObject player, GameObject enemy)
        {
            var controller = FindComponent(player, "PlayerController");
            var input = FindComponent(player, "InputMediator");
            if (input is Behaviour inputBehaviour)
            {
                inputBehaviour.enabled = false;
            }

            var direction = enemy.transform.position.x >= player.transform.position.x ? 1f : -1f;
            SetFieldValue(input, "<MoveInput>k__BackingField", direction);
            Invoke(controller, "Update");
            SetFieldValue(input, "<MoveInput>k__BackingField", 0f);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }
        }

        private static void DrivePlayerToward(GameObject player, GameObject enemy)
        {
            var controller = FindComponent(player, "PlayerController");
            var input = FindComponent(player, "InputMediator");
            if (input is Behaviour inputBehaviour)
            {
                inputBehaviour.enabled = false;
            }

            var direction = enemy.transform.position.x >= player.transform.position.x ? 1f : -1f;
            SetFieldValue(input, "<MoveInput>k__BackingField", direction);
            Invoke(controller, "Update");
        }

        private static void StopPlayerMovement(GameObject player)
        {
            var input = FindComponent(player, "InputMediator");
            SetFieldValue(input, "<MoveInput>k__BackingField", 0f);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }
        }

        private static bool RealAttackBoundsOverlapEnemy(GameObject enemy)
        {
            var hitboxObject = GameObject.Find("AttackHitbox");
            var box = hitboxObject?.GetComponent<BoxCollider2D>();
            var enemyCollider = enemy.GetComponent<Collider2D>();
            if (box == null || enemyCollider == null)
            {
                return false;
            }

            var scale = hitboxObject.transform.lossyScale;
            var center = hitboxObject.transform.TransformPoint(box.offset);
            var attackBounds = new Bounds(
                center,
                new Vector3(
                    Mathf.Abs(box.size.x * scale.x),
                    Mathf.Abs(box.size.y * scale.y),
                    0.1f));
            return attackBounds.Intersects(enemyCollider.bounds);
        }

        private static void FreezeEnemyWithoutRepositioning(Component enemy)
        {
            if (enemy is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }

            FreezeBodyWithoutDisabling(enemy);
            Physics2D.SyncTransforms();
        }

        private static void FreezeBodyWithoutDisabling(Component enemy)
        {
            var body = enemy.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        private static void TriggerRealPlayerAttackWhenInRange(GameObject player, GameObject enemy)
        {
            Assert.That(RealAttackBoundsOverlapEnemy(enemy), Is.True,
                "The real AttackHitbox bounds must overlap the naturally positioned Grunt.");
            var stateMachine = FindComponent(player, "PlayerStateMachine");
            Assert.That(GetPropertyValue(stateMachine, "CurrentState")?.ToString(), Is.EqualTo("Idle"));
            Invoke(stateMachine, "RequestAttack");
            Assert.That(GetPropertyValue(stateMachine, "CurrentState")?.ToString(), Is.EqualTo("Attack1"));
        }

        private static IEnumerator WaitForResolvedHitFeedback(
            Component grunt,
            int hpBefore,
            Action<FeedbackSnapshot> found,
            int maxFrames)
        {
            var player = GameObject.Find("Player");
            var inkEffect = FindComponent(player, "InkHitEffect");
            var expectedInkCount = GetIntField(inkEffect, "particleCount");
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var hpAfter = GetIntField(grunt, "hp");
                var damageNumbers = ActiveSceneComponents("DamageNumber");
                var particles = ActiveInkParticles();
                if (hpAfter < hpBefore && damageNumbers.Count == 1 && particles.Count == expectedInkCount)
                {
                    found(new FeedbackSnapshot
                    {
                        AppliedDamage = hpBefore - hpAfter,
                        DamageNumber = damageNumbers.Single(),
                        InkParticles = particles
                    });
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }

            Assert.Fail("The real player attack did not produce one resolved number and one current ink splash.");
        }

        private static void AssertNoActiveHitFeedback()
        {
            Assert.That(ActiveSceneComponents("DamageNumber"), Is.Empty,
                "A fresh BattleScene must not contain a prior-run damage number.");
            Assert.That(ActiveInkParticles(), Is.Empty,
                "A fresh BattleScene must not contain a prior-run ink particle.");
        }

        private static List<GameObject> ActiveInkParticles()
        {
            var pool = FindUniqueActiveSceneComponent("InkParticlePool");
            return ((IEnumerable)GetFieldValue(pool, "_allParticles"))
                .Cast<GameObject>()
                .Where(particle => particle != null && particle.activeSelf)
                .ToList();
        }

        private static Component SpawnRealBossThroughCurrentSpawner(Component spawner)
        {
            var entryType = spawner.GetType().Assembly.GetType("EnemySpawnEntry");
            Assert.That(entryType, Is.Not.Null);
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("enemyType", InstanceFlags)?.SetValue(entry, "boss");
            var sideField = entryType.GetField("preferredSide", InstanceFlags);
            Assert.That(sideField, Is.Not.Null);
            sideField.SetValue(entry, Enum.Parse(sideField.FieldType, "Right"));

            Invoke(spawner, "SpawnEnemy", entry);
            var bossObject = ((IEnumerable)GetFieldValue(spawner, "_aliveEnemies"))
                .Cast<GameObject>()
                .LastOrDefault(item => item != null && item.GetComponents<Component>()
                    .Any(component => component != null && component.GetType().Name == "Boss"));
            Assert.That(bossObject, Is.Not.Null);
            return bossObject.GetComponents<Component>()
                .Single(component => component != null && component.GetType().Name == "Boss");
        }

        private static void SetDeterministicBossAoeSeedAndAssertPreparedAttackId(
            Component boss,
            string expectedAttackId)
        {
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(7319);
                Invoke(boss, "CancelCombatActions");
                Invoke(boss, "FacePlayer");
                SetFieldValue(boss, "_attackPattern", 3);
                Assert.That((bool)Invoke(boss, "TryStartPreparedAttack"), Is.True);
                var plan = (EnemyAttackPlan)GetPropertyValue(boss, "CurrentAttackPlan");
                Assert.That(plan.AttackId, Is.EqualTo(expectedAttackId));
                Assert.That(plan.Shape, Is.EqualTo(EnemyTelegraphShape.Circle));
                Assert.That(plan.IsParryable, Is.False);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
            }
        }

        private static IEnumerator WaitForVisibleTelegraph(
            Component boss,
            string expectedShape,
            Action<Component> found,
            int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var telegraph = boss.GetComponents<Component>()
                    .FirstOrDefault(component => component != null &&
                                                 component.GetType().Name == "AttackTelegraphView");
                if (telegraph != null &&
                    (bool)GetPropertyValue(telegraph, "IsVisible") &&
                    GetPropertyValue(telegraph, "CurrentShape")?.ToString() == expectedShape)
                {
                    found(telegraph);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Boss did not expose a visible {expectedShape} telegraph within {maxFrames} frames.");
        }

        private static BattleHudCanvasState ConfigureBattleHudCanvasForCapture(Camera camera)
        {
            var hud = FindUniqueActiveSceneComponent("BattleHUD");
            var canvas = hud.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            var state = new BattleHudCanvasState(canvas, camera);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + 0.1f);
            return state;
        }

        private static void RestoreBattleHudCanvas(BattleHudCanvasState state)
        {
            if (state.Canvas != null)
            {
                state.Canvas.renderMode = state.RenderMode;
                state.Canvas.worldCamera = state.WorldCamera;
                state.Canvas.planeDistance = state.PlaneDistance;
            }

            if (state.Camera != null)
            {
                state.Camera.targetTexture = state.CameraTarget;
            }

            RenderTexture.active = state.ActiveRenderTexture;
            Canvas.ForceUpdateCanvases();
        }

        private static CapturedFrame CaptureWorldAndBattleHudSynchronous(
            Camera camera,
            string outputPath,
            int width,
            int height)
        {
            Assert.That(width, Is.EqualTo(CaptureWidth));
            Assert.That(height, Is.EqualTo(CaptureHeight));
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllBytes(outputPath, EncodePng(texture));
                }

                var pixels = texture.GetPixels32();
                return new CapturedFrame(Analyze(pixels), pixels);
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

        private static CapturedFrame CaptureWithRenderersHiddenSynchronous(
            Camera camera,
            params Renderer[] renderers)
        {
            Assert.That(renderers, Is.Not.Null.And.Not.Empty);
            Assert.That(renderers.All(renderer => renderer != null), Is.True);
            var enabledStates = renderers.Select(renderer => renderer.enabled).ToArray();
            var forceRenderingOffStates = renderers.Select(renderer => renderer.forceRenderingOff).ToArray();
            try
            {
                foreach (var renderer in renderers)
                {
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                }

                return CaptureWorldAndBattleHudSynchronous(camera, null, CaptureWidth, CaptureHeight);
            }
            finally
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index] != null)
                    {
                        renderers[index].enabled = enabledStates[index];
                        renderers[index].forceRenderingOff = forceRenderingOffStates[index];
                    }
                }
            }
        }

        private static void AssertWaveFrame(
            CapturedFrame frame,
            CapturedFrame withoutNumber,
            CapturedFrame withoutInk,
            string output,
            DateTime startedAt,
            GameObject player,
            Component grunt,
            FeedbackSnapshot feedback,
            Camera camera,
            Canvas canvas)
        {
            AssertCaptureFile(output, startedAt, frame.Metrics);
            AssertRendererInsideViewport(camera, player.GetComponent<SpriteRenderer>(), "Player");
            AssertRendererInsideViewport(camera, grunt.GetComponent<SpriteRenderer>(), "Grunt");
            Assert.That(ProjectedSpriteHeight(camera, player.GetComponent<SpriteRenderer>()),
                Is.GreaterThanOrEqualTo(24f));
            var gruntHeight = ProjectedSpriteHeight(camera, grunt.GetComponent<SpriteRenderer>());
            Assert.That(
                gruntHeight,
                Is.GreaterThanOrEqualTo(24f),
                $"B2_TASK7_RED_REAL_CAMERA_SCALE: real BattleScene Grunt projects to " +
                $"{gruntHeight:F2}px at orthographicSize={camera.orthographicSize:F2}; expected >=24px.");

            var numberText = feedback.DamageNumber.GetComponentInChildren<TextMesh>(true);
            Assert.That(numberText, Is.Not.Null);
            Assert.That(numberText.text, Is.EqualTo(feedback.AppliedDamage.ToString()));
            Assert.That(feedback.DamageNumber.gameObject.activeInHierarchy, Is.True);
            var numberRenderer = numberText.GetComponent<Renderer>();
            AssertRenderableInsideViewport(camera, numberRenderer, "Damage number");
            Assert.That(Vector3.Distance(feedback.DamageNumber.transform.position, grunt.transform.position),
                Is.LessThan(3f));
            Assert.That(feedback.InkParticles, Is.Not.Empty);
            Assert.That(feedback.InkParticles.All(particle =>
                    Vector3.Distance(particle.transform.position, grunt.transform.position) < 3f),
                Is.True,
                "Captured ink particles must belong to the just-resolved Grunt hit.");
            var inkRenderers = feedback.InkParticles
                .Select(particle => particle.GetComponent<SpriteRenderer>())
                .Cast<Renderer>()
                .ToArray();
            Assert.That(feedback.InkParticles.All(particle => particle.activeInHierarchy), Is.True);
            foreach (var inkRenderer in inkRenderers)
            {
                AssertRenderableInsideViewport(camera, inkRenderer, "Ink particle");
            }
            AssertVisiblePixelDelta(
                frame.Pixels,
                withoutNumber.Pixels,
                ProjectedPixelRect(camera, numberRenderer.bounds, 2),
                4,
                "B2_TASK7_RED_FEEDBACK_PIXELS: the captured damage number must change pixels in its projected ROI.");
            AssertVisiblePixelDelta(
                frame.Pixels,
                withoutInk.Pixels,
                ProjectedPixelRect(
                    camera,
                    CombinedBounds(inkRenderers
                        .Concat(new Renderer[] { grunt.GetComponent<SpriteRenderer>() })
                        .ToArray()),
                    32),
                8,
                "B2_TASK7_RED_FEEDBACK_PIXELS: captured ink particles must change pixels in the target-and-particle ROI.");

            var objective = FindUniqueActiveSceneComponent("WaveObjectiveView");
            var waveText = (Text)GetFieldValue(objective, "waveText");
            var aliveText = (Text)GetFieldValue(objective, "aliveText");
            AssertNonEmptyFittingText(waveText, "Wave text");
            AssertNonEmptyFittingText(aliveText, "Alive text");
            var statusPanel = FindDescendant(canvas.transform, "StatusPanel");
            Assert.That(statusPanel, Is.Not.Null);
            var expPanel = FindDescendant(canvas.transform, "ExpPanel");
            Assert.That(expPanel, Is.Not.Null);
            var canvasViewport = canvas.GetComponent<RectTransform>().rect;
            var statusTransform = statusPanel.GetComponent<RectTransform>();
            var statusRect = CanvasRect(canvas, statusTransform);
            var expRect = CanvasRect(canvas, expPanel.GetComponent<RectTransform>());
            var layoutFailures = new List<string>();
            if (statusTransform.anchorMin != new Vector2(0f, 1f) ||
                statusTransform.anchorMax != new Vector2(0f, 1f) ||
                statusTransform.pivot != new Vector2(0f, 1f) ||
                !ContainsRect(canvasViewport, statusRect))
            {
                layoutFailures.Add(
                    $"B2_TASK7_RED_STATUS_PANEL_ANCHOR: StatusPanel must be fully inside the top-left; " +
                    $"anchorMin={statusTransform.anchorMin}, anchorMax={statusTransform.anchorMax}, " +
                    $"pivot={statusTransform.pivot}, rect={statusRect}, canvas={canvasViewport}.");
            }
            if (!ContainsRect(canvasViewport, expRect))
            {
                layoutFailures.Add(
                    $"B2_TASK7_RED_EXP_PANEL_CLIP: ExpPanel must be fully inside the bottom viewport; " +
                    $"rect={expRect}, canvas={canvasViewport}.");
            }
            Assert.That(layoutFailures, Is.Empty, string.Join("\n", layoutFailures));
            AssertRectsDoNotOverlap(
                CanvasRect(canvas, objective.GetComponent<RectTransform>()),
                statusRect,
                "Wave objective and player status HUD");
            AssertPixelDiversity(frame.Metrics);
        }

        private static void AssertBossFrame(
            CapturedFrame frame,
            CapturedFrame withoutTelegraph,
            string output,
            DateTime startedAt,
            GameObject player,
            Component boss,
            Component telegraph,
            Camera camera,
            Canvas canvas)
        {
            AssertCaptureFile(output, startedAt, frame.Metrics);
            AssertRendererInsideViewport(camera, player.GetComponent<SpriteRenderer>(), "Player");
            AssertRendererInsideViewport(camera, boss.GetComponent<SpriteRenderer>(), "Boss");
            Assert.That(ProjectedSpriteHeight(camera, boss.GetComponent<SpriteRenderer>()),
                Is.GreaterThanOrEqualTo(24f));

            var ground = GameObject.Find("Ground")?.GetComponent<SpriteRenderer>();
            Assert.That(ground, Is.Not.Null);
            Assert.That(GeometryUtility.TestPlanesAABB(
                GeometryUtility.CalculateFrustumPlanes(camera), ground.bounds), Is.True);

            var plan = (EnemyAttackPlan)GetPropertyValue(boss, "CurrentAttackPlan");
            Assert.That(plan.AttackId, Is.EqualTo("boss_aoe"));
            Assert.That(plan.Shape, Is.EqualTo(EnemyTelegraphShape.Circle));
            Assert.That((bool)GetPropertyValue(telegraph, "IsVisible"), Is.True);
            var renderedMin = (Vector2)GetPropertyValue(telegraph, "RenderedLocalMin");
            var renderedMax = (Vector2)GetPropertyValue(telegraph, "RenderedLocalMax");
            Assert.That(renderedMin.x, Is.EqualTo(-plan.Radius).Within(0.02f));
            Assert.That(renderedMin.y, Is.EqualTo(-plan.Radius).Within(0.02f));
            Assert.That(renderedMax.x, Is.EqualTo(plan.Radius).Within(0.02f));
            Assert.That(renderedMax.y, Is.EqualTo(plan.Radius).Within(0.02f));

            var line = telegraph.GetComponentInChildren<LineRenderer>(true);
            Assert.That(line, Is.Not.Null);
            AssertRenderableInsideViewport(camera, line, "Boss Circle telegraph");
            Assert.That(line.startColor.r, Is.EqualTo(0.75f).Within(0.03f));
            Assert.That(line.startColor.g, Is.EqualTo(0.15f).Within(0.03f));
            Assert.That(line.startColor.b, Is.EqualTo(0.15f).Within(0.03f));
            Assert.That(line.startColor.a, Is.GreaterThanOrEqualTo(0.2f));
            Assert.That(GeometryUtility.TestPlanesAABB(
                GeometryUtility.CalculateFrustumPlanes(camera), line.bounds), Is.True);
            Assert.That(ProjectedWorldHeight(camera, plan.Radius * 2f), Is.GreaterThanOrEqualTo(120f));
            AssertVisiblePixelDelta(
                frame.Pixels,
                withoutTelegraph.Pixels,
                ProjectedPixelRect(camera, line.bounds, 3),
                64,
                "B2_TASK7_RED_TELEGRAPH_PIXELS: the captured Boss Circle must change pixels in its projected ROI.");

            var bossBar = FindUniqueActiveSceneComponent("BossHPBar");
            Assert.That(GetPropertyValue(bossBar, "BoundBoss"), Is.SameAs(boss));
            var slider = (Slider)GetFieldValue(bossBar, "bossSlider");
            Assert.That(slider.maxValue, Is.EqualTo(GetIntField(boss, "maxHp")));
            Assert.That(slider.value, Is.EqualTo(GetIntField(boss, "hp")));
            var bossName = (Text)GetFieldValue(bossBar, "bossNameText");
            var phaseText = (Text)GetFieldValue(bossBar, "phaseText");
            AssertNonEmptyFittingText(bossName, "Boss name");
            AssertNonEmptyFittingText(phaseText, "Boss phase");
            var bossPanelRect = CanvasSelfRect(canvas, bossBar.GetComponent<RectTransform>());
            var bossNameRect = CanvasSelfRect(canvas, bossName.rectTransform);
            var phaseTextRect = CanvasSelfRect(canvas, phaseText.rectTransform);
            var bossLayoutFailures = new List<string>();
            if (!ContainsRect(bossPanelRect, bossNameRect) || !ContainsRect(bossPanelRect, phaseTextRect))
            {
                bossLayoutFailures.Add(
                    $"B2_TASK7_RED_BOSS_LABEL_BOUNDS: Boss name and phase labels must remain inside the Boss panel; " +
                    $"panel={bossPanelRect}, name={bossNameRect}, phase={phaseTextRect}.");
            }
            if (!IsFullyInsideViewport(camera, line.bounds))
            {
                var viewportMin = camera.WorldToViewportPoint(line.bounds.min);
                var viewportMax = camera.WorldToViewportPoint(line.bounds.max);
                bossLayoutFailures.Add(
                    $"B2_TASK7_RED_BOSS_CIRCLE_CLIP: the complete radius-{plan.Radius:F2} Circle must fit the viewport; " +
                    $"viewportMin={viewportMin}, viewportMax={viewportMax}.");
            }
            Assert.That(bossLayoutFailures, Is.Empty, string.Join("\n", bossLayoutFailures));

            var objective = FindUniqueActiveSceneComponent("WaveObjectiveView");
            var waveText = (Text)GetFieldValue(objective, "waveText");
            var aliveText = (Text)GetFieldValue(objective, "aliveText");
            AssertNonEmptyFittingText(waveText, "Wave text");
            AssertNonEmptyFittingText(aliveText, "Alive text");
            var statusPanel = FindDescendant(canvas.transform, "StatusPanel");
            Assert.That(statusPanel, Is.Not.Null);
            var objectiveRect = CanvasRect(canvas, objective.GetComponent<RectTransform>());
            var bossRect = CanvasRect(canvas, bossBar.GetComponent<RectTransform>());
            var statusRect = CanvasRect(canvas, statusPanel.GetComponent<RectTransform>());
            AssertRectsDoNotOverlap(objectiveRect, bossRect, "Wave objective and Boss HP bar");
            AssertRectsDoNotOverlap(objectiveRect, statusRect, "Wave objective and player status HUD");
            AssertRectsDoNotOverlap(bossRect, statusRect, "Boss HP bar and player status HUD");
            AssertPixelDiversity(frame.Metrics);
        }

        private static void AssertCaptureFile(string output, DateTime startedAt, PixelMetrics metrics)
        {
            Assert.That(File.Exists(output), Is.True);
            var info = new FileInfo(output);
            Assert.That(info.Length, Is.GreaterThan(1024));
            Assert.That(info.LastWriteTimeUtc, Is.GreaterThanOrEqualTo(startedAt.AddSeconds(-1)));
            Assert.That(metrics.TotalPixels, Is.EqualTo(CaptureWidth * CaptureHeight));
            Assert.That(metrics.OpaquePixels, Is.GreaterThan(metrics.TotalPixels * 95 / 100), metrics.ToString());
        }

        private static void AssertPixelDiversity(PixelMetrics metrics)
        {
            Assert.That(metrics.LuminanceVariance, Is.GreaterThan(80d), metrics.ToString());
            Assert.That(metrics.DarkPixels, Is.GreaterThan(metrics.TotalPixels / 200), metrics.ToString());
            Assert.That(metrics.LightPixels, Is.GreaterThan(metrics.TotalPixels / 5), metrics.ToString());
            Assert.That(metrics.ChromaticPixels, Is.GreaterThan(metrics.TotalPixels / 50), metrics.ToString());
            Assert.That(metrics.QuantizedColorCount, Is.GreaterThan(16), metrics.ToString());
        }

        private static void AssertRenderableInsideViewport(Camera camera, Renderer renderer, string label)
        {
            Assert.That(renderer, Is.Not.Null, $"{label} renderer is required.");
            Assert.That(renderer.gameObject.activeInHierarchy, Is.True, $"{label} must be active after capture.");
            Assert.That(renderer.enabled, Is.True, $"{label} renderer must remain enabled after capture.");
            Assert.That(renderer.forceRenderingOff, Is.False, $"{label} renderer must not be force-hidden.");
            Assert.That(GeometryUtility.TestPlanesAABB(
                GeometryUtility.CalculateFrustumPlanes(camera), renderer.bounds), Is.True,
                $"{label} renderer bounds must intersect the evidence camera.");
            Assert.That(IsFullyInsideViewport(camera, renderer.bounds), Is.True,
                $"{label} renderer bounds must remain fully inside the evidence viewport.");
        }

        private static Bounds CombinedBounds(IReadOnlyList<Renderer> renderers)
        {
            Assert.That(renderers, Is.Not.Null.And.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static RectInt ProjectedPixelRect(Camera camera, Bounds bounds, int padding)
        {
            var viewportMin = camera.WorldToViewportPoint(
                new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
            var viewportMax = camera.WorldToViewportPoint(
                new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
            var xMin = Mathf.Clamp(Mathf.FloorToInt(viewportMin.x * CaptureWidth) - padding, 0, CaptureWidth);
            var yMin = Mathf.Clamp(Mathf.FloorToInt(viewportMin.y * CaptureHeight) - padding, 0, CaptureHeight);
            var xMax = Mathf.Clamp(Mathf.CeilToInt(viewportMax.x * CaptureWidth) + padding, 0, CaptureWidth);
            var yMax = Mathf.Clamp(Mathf.CeilToInt(viewportMax.y * CaptureHeight) + padding, 0, CaptureHeight);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        private static void AssertVisiblePixelDelta(
            IReadOnlyList<Color32> visiblePixels,
            IReadOnlyList<Color32> hiddenPixels,
            RectInt roi,
            int minimumChangedPixels,
            string failureMessage)
        {
            Assert.That(visiblePixels.Count, Is.EqualTo(CaptureWidth * CaptureHeight));
            Assert.That(hiddenPixels.Count, Is.EqualTo(CaptureWidth * CaptureHeight));
            Assert.That(roi.width, Is.GreaterThan(0));
            Assert.That(roi.height, Is.GreaterThan(0));

            var changedPixels = 0;
            for (var y = roi.yMin; y < roi.yMax; y++)
            {
                for (var x = roi.xMin; x < roi.xMax; x++)
                {
                    var index = y * CaptureWidth + x;
                    var visible = visiblePixels[index];
                    var hidden = hiddenPixels[index];
                    if (PixelsDiffer(visible, hidden))
                    {
                        changedPixels++;
                    }
                }
            }

            Assert.That(
                changedPixels,
                Is.GreaterThanOrEqualTo(minimumChangedPixels),
                $"{failureMessage} changed={changedPixels}, expected>={minimumChangedPixels}, roi={roi}.");
        }

        private static bool PixelsDiffer(Color32 visible, Color32 hidden)
        {
            return Mathf.Abs(visible.r - hidden.r) >= 4 ||
                   Mathf.Abs(visible.g - hidden.g) >= 4 ||
                   Mathf.Abs(visible.b - hidden.b) >= 4 ||
                   Mathf.Abs(visible.a - hidden.a) >= 4;
        }

        private static void AssertRendererInsideViewport(Camera camera, SpriteRenderer renderer, string label)
        {
            Assert.That(renderer, Is.Not.Null);
            Assert.That(IsFullyInsideViewport(camera, renderer.bounds), Is.True,
                $"{label} bounds must remain fully inside the 960x540 evidence viewport.");
        }

        private static bool IsFullyInsideViewport(Camera camera, Bounds bounds)
        {
            var min = camera.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
            var max = camera.WorldToViewportPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
            return min.z > 0f && max.z > 0f &&
                   min.x >= 0f && min.y >= 0f &&
                   max.x <= 1f && max.y <= 1f;
        }

        private static float ProjectedSpriteHeight(Camera camera, SpriteRenderer renderer)
        {
            return ProjectedWorldHeight(camera, renderer.bounds.size.y);
        }

        private static float ProjectedWorldHeight(Camera camera, float worldHeight)
        {
            return worldHeight / (camera.orthographicSize * 2f) * CaptureHeight;
        }

        private static void AssertNonEmptyFittingText(Text text, string label)
        {
            Assert.That(text, Is.Not.Null, $"{label} component is required.");
            Assert.That(text.text, Is.Not.Empty, $"{label} must be non-empty.");
            Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1f),
                $"{label} must fit its width.");
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f),
                $"{label} must fit its height.");
        }

        private static Rect CanvasRect(Canvas canvas, RectTransform target)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, target);
            return new Rect(
                bounds.min.x,
                bounds.min.y,
                bounds.size.x,
                bounds.size.y);
        }

        private static Rect CanvasSelfRect(Canvas canvas, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var minimum = (Vector2)canvas.transform.InverseTransformPoint(corners[0]);
            var maximum = minimum;
            for (var index = 1; index < corners.Length; index++)
            {
                var point = (Vector2)canvas.transform.InverseTransformPoint(corners[index]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            return new Rect(minimum, maximum - minimum);
        }

        private static void AssertRectsDoNotOverlap(Rect first, Rect second, string label)
        {
            Assert.That(first.Overlaps(second), Is.False,
                $"{label} must not overlap. first={first}, second={second}");
        }

        private static bool ContainsRect(Rect outer, Rect inner, float tolerance = 0.5f)
        {
            return inner.xMin >= outer.xMin - tolerance &&
                   inner.yMin >= outer.yMin - tolerance &&
                   inner.xMax <= outer.xMax + tolerance &&
                   inner.yMax <= outer.yMax + tolerance;
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

        private static string PrepareOutput(string fileName)
        {
            var logsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDirectory);
            var output = Path.Combine(logsDirectory, fileName);
            if (File.Exists(output))
            {
                File.Delete(output);
            }

            return output;
        }

        private static List<Component> ActiveSceneComponents(string typeName)
        {
            var activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null &&
                                    component.GetType().Name == typeName &&
                                    component.gameObject.scene == activeScene &&
                                    component.gameObject.activeInHierarchy)
                .ToList();
        }

        private static Component FindUniqueActiveSceneComponent(string typeName)
        {
            var matches = ActiveSceneComponents(typeName);
            Assert.That(matches, Has.Count.EqualTo(1), $"Expected exactly one active-scene {typeName}.");
            return matches.Single();
        }

        private static Component FindComponent(GameObject gameObject, string typeName)
        {
            Assert.That(gameObject, Is.Not.Null, $"Expected GameObject for {typeName}.");
            var component = gameObject.GetComponents<Component>()
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, $"Expected {gameObject.name} to contain {typeName}.");
            return component;
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == objectName)
                ?.gameObject;
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            var field = FindInstanceField(instance.GetType(), fieldName);
            Assert.That(field, Is.Not.Null, $"Expected {instance.GetType().Name}.{fieldName}.");
            return field.GetValue(instance);
        }

        private static int GetIntField(object instance, string fieldName)
        {
            return (int)GetFieldValue(instance, fieldName);
        }

        private static void SetFieldValue(object instance, string fieldName, object value)
        {
            var field = FindInstanceField(instance.GetType(), fieldName);
            Assert.That(field, Is.Not.Null, $"Expected {instance.GetType().Name}.{fieldName}.");
            field.SetValue(instance, value);
        }

        private static FieldInfo FindInstanceField(Type type, string fieldName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(fieldName, InstanceFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null, $"Expected {instance.GetType().Name}.{propertyName}.");
            return property.GetValue(instance);
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            var method = FindInstanceMethod(instance.GetType(), methodName, arguments.Length);
            Assert.That(method, Is.Not.Null, $"Expected {instance.GetType().Name}.{methodName}().");
            return method.Invoke(instance, arguments);
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName, int parameterCount)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var method = current.GetMethods(InstanceFlags | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(candidate => candidate.Name == methodName &&
                                                 candidate.GetParameters().Length == parameterCount);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
