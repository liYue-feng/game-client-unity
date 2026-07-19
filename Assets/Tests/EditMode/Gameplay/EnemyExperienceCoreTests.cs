using System;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    public sealed class EnemyExperienceCoreTests
    {
        [Test]
        public void WaveScalingDependsOnBaselineAndWaveOnly()
        {
            var baseline = new EnemyStatBaseline(100, 10, 2f, 0f, 0.5f, 0.3f);
            var multipliers = new EnemyWaveMultipliers(1.15f, 1.1f, 1.05f);

            var first = EnemyWaveScaling.Calculate(baseline, 3, multipliers);
            var afterArbitraryReuse = EnemyWaveScaling.Calculate(baseline, 3, multipliers);

            Assert.That(afterArbitraryReuse.MaxHp, Is.EqualTo(first.MaxHp));
            Assert.That(afterArbitraryReuse.Damage, Is.EqualTo(first.Damage));
            Assert.That(afterArbitraryReuse.MoveSpeed, Is.EqualTo(first.MoveSpeed).Within(0.0001f));
        }

        [TestCase(0, 100, 10, 2f)]
        [TestCase(1, 115, 11, 2.1f)]
        [TestCase(3, 152, 13, 2.31525f)]
        public void WaveScalingUsesBaselinePowerAndRoundsIntegralStats(
            int waveIndex,
            int expectedMaxHp,
            int expectedDamage,
            float expectedMoveSpeed)
        {
            var baseline = new EnemyStatBaseline(100, 10, 2f, 0f, 0.5f, 0.3f);
            var multipliers = new EnemyWaveMultipliers(1.15f, 1.1f, 1.05f);

            var result = EnemyWaveScaling.Calculate(baseline, waveIndex, multipliers);

            Assert.That(result.MaxHp, Is.EqualTo(expectedMaxHp));
            Assert.That(result.Damage, Is.EqualTo(expectedDamage));
            Assert.That(result.MoveSpeed, Is.EqualTo(expectedMoveSpeed).Within(0.0001f));
        }

        [Test]
        public void WaveScalingUsesMidpointToEvenRounding()
        {
            var baseline = new EnemyStatBaseline(5, 3, 2f, 0f, 0.5f, 0.3f);
            var multipliers = new EnemyWaveMultipliers(0.5f, 0.5f, 1f);

            var result = EnemyWaveScaling.Calculate(baseline, 1, multipliers);

            Assert.That(result.MaxHp, Is.EqualTo(2));
            Assert.That(result.Damage, Is.EqualTo(2));
        }

        [Test]
        public void WaveScalingSaturatesHugeFiniteInputs()
        {
            var maximumBaseline = new EnemyStatBaseline(
                int.MaxValue,
                int.MaxValue,
                float.MaxValue,
                1f,
                0f,
                0f);
            var doubled = EnemyWaveScaling.Calculate(
                maximumBaseline,
                1,
                new EnemyWaveMultipliers(2f, 2f, 2f));
            var hugeWave = EnemyWaveScaling.Calculate(
                new EnemyStatBaseline(100, 10, 2f, 0f, 0f, 0f),
                int.MaxValue,
                new EnemyWaveMultipliers(float.MaxValue, float.MaxValue, float.MaxValue));

            AssertSaturatedAndFinite(doubled);
            AssertSaturatedAndFinite(hugeWave);
        }

        [Test]
        public void NegativeWaveIndexUsesTheUnscaledBaseline()
        {
            var baseline = new EnemyStatBaseline(100, 10, 2f, 0f, 0.5f, 0.3f);
            var multipliers = new EnemyWaveMultipliers(1.15f, 1.1f, 1.05f);

            var result = EnemyWaveScaling.Calculate(baseline, -3, multipliers);

            Assert.That(result.MaxHp, Is.EqualTo(100));
            Assert.That(result.Damage, Is.EqualTo(10));
            Assert.That(result.MoveSpeed, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void EnemyStatBaselineClampsInvalidValuesAndCopiesByValue()
        {
            var baseline = new EnemyStatBaseline(-10, -5, -2f, 2f, -0.5f, -0.3f);
            var copy = baseline;

            Assert.That(copy.MaxHp, Is.EqualTo(1));
            Assert.That(copy.Damage, Is.Zero);
            Assert.That(copy.MoveSpeed, Is.Zero);
            Assert.That(copy.DamageReduction, Is.EqualTo(1f));
            Assert.That(copy.TelegraphDuration, Is.Zero);
            Assert.That(copy.AttackDuration, Is.Zero);
        }

        [Test]
        public void EnemyWaveMultipliersClampNegativeValuesToZero()
        {
            var multipliers = new EnemyWaveMultipliers(-1f, -2f, -3f);

            Assert.That(multipliers.Hp, Is.Zero);
            Assert.That(multipliers.Damage, Is.Zero);
            Assert.That(multipliers.Speed, Is.Zero);
        }

        [Test]
        public void PublicValueConstructorsRejectNonFiniteInputsWithParameterNames()
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                AssertOutOfRange(
                    "minX",
                    () => { _ = new BattleArenaBounds(invalid, 1f); });
                AssertOutOfRange(
                    "maxX",
                    () => { _ = new BattleArenaBounds(-1f, invalid); });
                AssertOutOfRange(
                    "moveSpeed",
                    () => { _ = new EnemyStatBaseline(1, 1, invalid, 0f, 0f, 0f); });
                AssertOutOfRange(
                    "damageReduction",
                    () => { _ = new EnemyStatBaseline(1, 1, 1f, invalid, 0f, 0f); });
                AssertOutOfRange(
                    "telegraphDuration",
                    () => { _ = new EnemyStatBaseline(1, 1, 1f, 0f, invalid, 0f); });
                AssertOutOfRange(
                    "attackDuration",
                    () => { _ = new EnemyStatBaseline(1, 1, 1f, 0f, 0f, invalid); });
                AssertOutOfRange(
                    "hp",
                    () => { _ = new EnemyWaveMultipliers(invalid, 1f, 1f); });
                AssertOutOfRange(
                    "damage",
                    () => { _ = new EnemyWaveMultipliers(1f, invalid, 1f); });
                AssertOutOfRange(
                    "speed",
                    () => { _ = new EnemyWaveMultipliers(1f, 1f, invalid); });
                AssertOutOfRange(
                    "moveSpeed",
                    () => { _ = new EnemyWaveStats(1, 1, invalid); });
            }
        }

        [Test]
        public void EnemyWaveStatsNormalizesFiniteNegativeValues()
        {
            var stats = new EnemyWaveStats(-1, -2, -3f);

            Assert.That(stats.MaxHp, Is.EqualTo(1));
            Assert.That(stats.Damage, Is.Zero);
            Assert.That(stats.MoveSpeed, Is.Zero);
        }

        [Test]
        public void BattleArenaBoundsNormalizesReversedEndpoints()
        {
            var bounds = new BattleArenaBounds(15f, -15f);

            Assert.That(bounds.MinX, Is.EqualTo(-15f));
            Assert.That(bounds.MaxX, Is.EqualTo(15f));
            Assert.That(bounds.CenterX, Is.EqualTo(0f));
            Assert.That(bounds.Width, Is.EqualTo(30f));
        }

        [Test]
        public void BattleArenaBoundsKeepsDerivedValuesFiniteAtFloatLimits()
        {
            var bounds = new BattleArenaBounds(-float.MaxValue, float.MaxValue);

            Assert.That(bounds.CenterX, Is.Zero);
            Assert.That(bounds.Width, Is.EqualTo(float.MaxValue));
            Assert.That(float.IsNaN(bounds.Width), Is.False);
            Assert.That(float.IsInfinity(bounds.Width), Is.False);
        }

        [Test]
        public void SpawnPlannerReturnsTheSamePointForTheSameInput()
        {
            var bounds = new BattleArenaBounds(-15f, 15f);

            var first = ArenaSpawnPlanner.PlanX(
                bounds,
                1f,
                ArenaSpawnSide.Right,
                5f,
                0.5f,
                8f);
            var second = ArenaSpawnPlanner.PlanX(
                bounds,
                1f,
                ArenaSpawnSide.Right,
                5f,
                0.5f,
                8f);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SpawnPlannerRejectsNonFiniteInputsWithParameterNames()
        {
            var bounds = new BattleArenaBounds(-15f, 15f);
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                AssertOutOfRange(
                    "playerX",
                    () => ArenaSpawnPlanner.PlanX(
                        bounds, invalid, ArenaSpawnSide.Right, 5f, 0.5f, 8f));
                AssertOutOfRange(
                    "cameraHalfWidth",
                    () => ArenaSpawnPlanner.PlanX(
                        bounds, 0f, ArenaSpawnSide.Right, invalid, 0.5f, 8f));
                AssertOutOfRange(
                    "spawnMargin",
                    () => ArenaSpawnPlanner.PlanX(
                        bounds, 0f, ArenaSpawnSide.Right, 5f, invalid, 8f));
                AssertOutOfRange(
                    "chaseRange",
                    () => ArenaSpawnPlanner.PlanX(
                        bounds, 0f, ArenaSpawnSide.Right, 5f, 0.5f, invalid));
            }
        }

        [TestCase((ArenaSpawnSide)0)]
        [TestCase((ArenaSpawnSide)2)]
        public void SpawnPlannerRejectsUnknownSpawnSides(ArenaSpawnSide invalidSide)
        {
            AssertOutOfRange(
                "preferredSide",
                () => ArenaSpawnPlanner.PlanX(
                    new BattleArenaBounds(-15f, 15f),
                    0f,
                    invalidSide,
                    5f,
                    0.5f,
                    8f));
        }

        [Test]
        public void SpawnPlannerKeepsPreferredSideWhenItIsReachable()
        {
            var spawnX = ArenaSpawnPlanner.PlanX(
                new BattleArenaBounds(-15f, 15f),
                0f,
                ArenaSpawnSide.Right,
                5f,
                0.5f,
                8f);

            Assert.That(spawnX, Is.GreaterThan(0f));
            Assert.That(spawnX, Is.EqualTo(5.5f).Within(0.0001f));
        }

        [TestCase(14f, ArenaSpawnSide.Right, 8f)]
        [TestCase(-14f, ArenaSpawnSide.Left, -8f)]
        public void SpawnPlannerUsesTheOppositeSideWhenThePreferredEdgeIsBlocked(
            float playerX,
            ArenaSpawnSide preferredSide,
            float expectedSpawnX)
        {
            var spawnX = ArenaSpawnPlanner.PlanX(
                new BattleArenaBounds(-15f, 15f),
                playerX,
                preferredSide,
                5.5f,
                0.5f,
                8f);

            Assert.That(spawnX, Is.EqualTo(expectedSpawnX).Within(0.0001f));
        }

        [TestCase(1f, ArenaSpawnSide.Right, -2.5f)]
        [TestCase(-1f, ArenaSpawnSide.Left, 2.5f)]
        public void SpawnPlannerChoosesTheFarthestPointWhenNeitherSideCanLeaveTheView(
            float playerX,
            ArenaSpawnSide preferredSide,
            float expectedSpawnX)
        {
            var spawnX = ArenaSpawnPlanner.PlanX(
                new BattleArenaBounds(-3f, 3f),
                playerX,
                preferredSide,
                10f,
                0.5f,
                8f);

            Assert.That(spawnX, Is.EqualTo(expectedSpawnX).Within(0.0001f));
        }

        [Test]
        public void SpawnPlannerUsesDeterministicFallbackInANarrowArena()
        {
            var spawnX = ArenaSpawnPlanner.PlanX(
                new BattleArenaBounds(-3f, 3f),
                0f,
                ArenaSpawnSide.Left,
                10f,
                0.5f,
                8f);

            Assert.That(spawnX, Is.EqualTo(-2.5f).Within(0.0001f));
        }

        [TestCase(-15f, 15f, 0f, ArenaSpawnSide.Right, 8.5f)]
        [TestCase(-15f, 15f, 14f, ArenaSpawnSide.Right, 5.5f)]
        [TestCase(-3f, 3f, 0f, ArenaSpawnSide.Left, 10f)]
        public void SpawnPlannerReturnsAnInBoundsReachablePoint(
            float min,
            float max,
            float playerX,
            ArenaSpawnSide side,
            float cameraHalfWidth)
        {
            var bounds = new BattleArenaBounds(min, max);

            var spawnX = ArenaSpawnPlanner.PlanX(
                bounds,
                playerX,
                side,
                cameraHalfWidth,
                0.5f,
                8f);

            Assert.That(spawnX, Is.InRange(bounds.MinX + 0.5f, bounds.MaxX - 0.5f));
            Assert.That(System.Math.Abs(spawnX - playerX), Is.LessThanOrEqualTo(8.001f));
        }

        [Test]
        public void AttackPlanNormalizesGeometryTimingAndComboWindow()
        {
            var plan = EnemyAttackPlan.Box(
                "elite_combo", -1f, 0.1f, -2f, true,
                new Vector2(-0.7f, 0.2f), new Vector2(-1f, -0.8f),
                -1, new Vector2(-4f, 0f), 3, 0.4f, 20, 5f);

            Assert.That(
                plan.TelegraphDuration,
                Is.Zero,
                "B2_TASK3_RED_NORMALIZATION");
            Assert.That(plan.AttackId, Is.EqualTo("elite_combo"));
            Assert.That(plan.CommitDuration, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(plan.RecoveryDuration, Is.Zero);
            Assert.That(plan.Shape, Is.EqualTo(EnemyTelegraphShape.Box));
            Assert.That(plan.IsParryable, Is.True);
            Assert.That(plan.LocalOffset, Is.EqualTo(new Vector2(-0.7f, 0.2f)));
            Assert.That(plan.Size, Is.EqualTo(new Vector2(1f, 0.8f)));
            Assert.That(plan.FacingDirection, Is.EqualTo(-1));
            Assert.That(plan.AimDirection, Is.EqualTo(Vector2.left));
            Assert.That(plan.HitCount, Is.EqualTo(3));
            Assert.That(plan.HitInterval, Is.EqualTo(0.4f));
            Assert.That(plan.Damage, Is.EqualTo(20));
            Assert.That(plan.Knockback, Is.EqualTo(5f));
            Assert.That(plan.TotalDuration, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(plan.IsValid, Is.True);
        }

        [Test]
        public void AttackTimelineTraversesPreparedDurationsExactly()
        {
            var plan = EnemyAttackPlan.Circle(
                "boss_aoe", 0.6f, 0.2f, 0.3f, false,
                Vector2.zero, 4f, 1, Vector2.right, 1, 0f, 20, 8f);
            var timeline = new EnemyAttackTimeline(plan);

            Assert.That(plan.Shape, Is.EqualTo(EnemyTelegraphShape.Circle));
            Assert.That(plan.Radius, Is.EqualTo(4f));
            Assert.That(plan.IsParryable, Is.False);
            Assert.That(timeline.Evaluate(0.599f), Is.EqualTo(EnemyAttackPhase.Telegraph));
            Assert.That(timeline.Evaluate(0.6f), Is.EqualTo(EnemyAttackPhase.Commit));
            Assert.That(timeline.Evaluate(0.8f), Is.EqualTo(EnemyAttackPhase.Recovery));
            Assert.That(timeline.Evaluate(1.1f), Is.EqualTo(EnemyAttackPhase.Complete));
        }

        [Test]
        public void AttackPlanNormalizesSingleHitAndZeroAimFallback()
        {
            var plan = EnemyAttackPlan.Box(
                "grunt", 0f, -1f, 0.2f, true,
                Vector2.zero, Vector2.one,
                0, Vector2.zero, 0, -1f, -10, -2f);

            Assert.That(plan.FacingDirection, Is.EqualTo(1));
            Assert.That(plan.AimDirection, Is.EqualTo(Vector2.right));
            Assert.That(plan.HitCount, Is.EqualTo(1));
            Assert.That(plan.HitInterval, Is.Zero);
            Assert.That(plan.CommitDuration, Is.Zero);
            Assert.That(plan.Damage, Is.Zero);
            Assert.That(plan.Knockback, Is.Zero);
            Assert.That(plan.TotalDuration, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void AttackPlanRejectsInvalidIdentifiersAndGeometry()
        {
            var emptyId = EnemyAttackPlan.Box(
                string.Empty, 0f, 0f, 0f, true,
                Vector2.zero, Vector2.one, 1, Vector2.right, 1, 0f, 1, 0f);
            var nullId = EnemyAttackPlan.Box(
                null, 0f, 0f, 0f, true,
                Vector2.zero, Vector2.one, 1, Vector2.right, 1, 0f, 1, 0f);
            var zeroWidth = EnemyAttackPlan.Box(
                "box", 0f, 0f, 0f, true,
                Vector2.zero, new Vector2(0f, 1f), 1, Vector2.right, 1, 0f, 1, 0f);
            var zeroHeight = EnemyAttackPlan.Box(
                "box", 0f, 0f, 0f, true,
                Vector2.zero, new Vector2(1f, 0f), 1, Vector2.right, 1, 0f, 1, 0f);
            var zeroRadius = EnemyAttackPlan.Circle(
                "circle", 0f, 0f, 0f, false,
                Vector2.zero, 0f, 1, Vector2.right, 1, 0f, 1, 0f);
            var negativeRadius = EnemyAttackPlan.Circle(
                "circle", 0f, 0f, 0f, false,
                Vector2.zero, -2f, 1, Vector2.right, 1, 0f, 1, 0f);

            Assert.That(emptyId.IsValid, Is.False);
            Assert.That(nullId.AttackId, Is.Empty);
            Assert.That(nullId.IsValid, Is.False);
            Assert.That(zeroWidth.IsValid, Is.False);
            Assert.That(zeroHeight.IsValid, Is.False);
            Assert.That(zeroRadius.IsValid, Is.False);
            Assert.That(negativeRadius.Radius, Is.Zero);
            Assert.That(negativeRadius.IsValid, Is.False);
        }

        [Test]
        public void AttackPlanRejectsEveryNonFiniteNumericInput()
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                AssertAttackPlanOutOfRange("telegraphDuration", invalid, AttackPlanFloatSlot.Telegraph);
                AssertAttackPlanOutOfRange("commitDuration", invalid, AttackPlanFloatSlot.Commit);
                AssertAttackPlanOutOfRange("recoveryDuration", invalid, AttackPlanFloatSlot.Recovery);
                AssertAttackPlanOutOfRange("localOffset", invalid, AttackPlanFloatSlot.OffsetX);
                AssertAttackPlanOutOfRange("localOffset", invalid, AttackPlanFloatSlot.OffsetY);
                AssertAttackPlanOutOfRange("size", invalid, AttackPlanFloatSlot.SizeX);
                AssertAttackPlanOutOfRange("size", invalid, AttackPlanFloatSlot.SizeY);
                AssertAttackPlanOutOfRange("aimDirection", invalid, AttackPlanFloatSlot.AimX);
                AssertAttackPlanOutOfRange("aimDirection", invalid, AttackPlanFloatSlot.AimY);
                AssertAttackPlanOutOfRange("hitInterval", invalid, AttackPlanFloatSlot.HitInterval);
                AssertAttackPlanOutOfRange("knockback", invalid, AttackPlanFloatSlot.Knockback);
                AssertOutOfRange(
                    "radius",
                    () => EnemyAttackPlan.Circle(
                        "circle", 0f, 0f, 0f, false,
                        Vector2.zero, invalid, 1, Vector2.right, 1, 0f, 1, 0f));
            }
        }

        [Test]
        public void AttackPlanKeepsLargeFiniteNonOverflowValuesUsable()
        {
            var phaseDuration = float.MaxValue / 4f;
            var plan = EnemyAttackPlan.Box(
                "finite_extreme",
                phaseDuration,
                phaseDuration,
                phaseDuration,
                true,
                new Vector2(float.MaxValue, -float.MaxValue),
                new Vector2(-float.MaxValue, float.MaxValue),
                -1,
                new Vector2(float.MaxValue, float.MaxValue),
                2,
                phaseDuration,
                int.MaxValue,
                float.MaxValue);
            var timeline = new EnemyAttackTimeline(plan);
            var recoveryStart = (float)((double)phaseDuration * 2d);
            var expectedTotal = (float)((double)phaseDuration * 3d);

            Assert.That(plan.CommitDuration, Is.EqualTo(phaseDuration));
            Assert.That(plan.HitInterval, Is.EqualTo(phaseDuration));
            Assert.That(plan.TotalDuration, Is.EqualTo(expectedTotal));
            Assert.That(float.IsNaN(plan.TotalDuration), Is.False);
            Assert.That(float.IsInfinity(plan.TotalDuration), Is.False);
            Assert.That(plan.TotalDuration, Is.LessThan(float.MaxValue));
            Assert.That(plan.AimDirection.x, Is.EqualTo(0.70710677f).Within(0.0001f));
            Assert.That(plan.AimDirection.y, Is.EqualTo(0.70710677f).Within(0.0001f));
            Assert.That(plan.Size, Is.EqualTo(new Vector2(float.MaxValue, float.MaxValue)));
            Assert.That(plan.IsValid, Is.True);
            Assert.That(timeline.Evaluate(0f), Is.EqualTo(EnemyAttackPhase.Telegraph));
            Assert.That(timeline.Evaluate(phaseDuration), Is.EqualTo(EnemyAttackPhase.Commit));
            Assert.That(timeline.Evaluate(recoveryStart), Is.EqualTo(EnemyAttackPhase.Recovery));
            Assert.That(timeline.Evaluate(plan.TotalDuration), Is.EqualTo(EnemyAttackPhase.Complete));
        }

        [Test]
        public void AttackPlanRejectsPositiveCommitThatDoesNotAdvanceTheFloatBoundary()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => EnemyAttackPlan.Box(
                    "commit_precision",
                    16777216f,
                    1f,
                    1f,
                    true,
                    Vector2.zero,
                    Vector2.one,
                    1,
                    Vector2.right,
                    1,
                    0f,
                    1,
                    0f),
                "B2_TASK3_RED_COMMIT_PRECISION");

            Assert.That(exception.ParamName, Is.EqualTo("commitDuration"));
        }

        [Test]
        public void AttackPlanRejectsPositiveRecoveryThatDoesNotAdvanceTheFloatBoundary()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => EnemyAttackPlan.Circle(
                    "recovery_precision",
                    0f,
                    16777216f,
                    1f,
                    false,
                    Vector2.zero,
                    1f,
                    1,
                    Vector2.right,
                    1,
                    0f,
                    1,
                    0f),
                "B2_TASK3_RED_RECOVERY_PRECISION");

            Assert.That(exception.ParamName, Is.EqualTo("recoveryDuration"));
        }

        [Test]
        public void AttackPlanRejectsComboWindowOverflow()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => EnemyAttackPlan.Box(
                    "combo_overflow",
                    0f,
                    0f,
                    0f,
                    true,
                    Vector2.zero,
                    Vector2.one,
                    1,
                    Vector2.right,
                    int.MaxValue,
                    float.MaxValue,
                    1,
                    0f),
                "B2_TASK3_RED_COMBO_OVERFLOW");

            Assert.That(exception.ParamName, Is.EqualTo("hitInterval"));
        }

        [Test]
        public void AttackTimelineCannotReceiveAPlanWhosePhaseTotalOverflows()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    _ = new EnemyAttackTimeline(EnemyAttackPlan.Circle(
                        "timeline_overflow",
                        float.MaxValue,
                        float.MaxValue,
                        0f,
                        false,
                        Vector2.zero,
                        1f,
                        1,
                        Vector2.right,
                        1,
                        0f,
                        1,
                        0f));
                },
                "B2_TASK3_RED_TOTAL_OVERFLOW");

            Assert.That(exception.ParamName, Is.EqualTo("commitDuration"));
        }

        [Test]
        public void AttackPlanRejectsUnknownTelegraphShape()
        {
            var constructors = typeof(EnemyAttackPlan).GetConstructors(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(constructors, Has.Length.EqualTo(1));

            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => constructors[0].Invoke(new object[]
                {
                    (EnemyTelegraphShape)99,
                    "invalid_shape",
                    0f,
                    0f,
                    0f,
                    true,
                    Vector2.zero,
                    Vector2.one,
                    0f,
                    1,
                    Vector2.right,
                    1,
                    0f,
                    1,
                    0f
                }));

            Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                ((ArgumentOutOfRangeException)exception.InnerException).ParamName,
                Is.EqualTo("shape"));
        }

        [Test]
        public void AttackTimelineDefinesNonFiniteAndZeroDurationBoundaries()
        {
            var telegraphOnly = new EnemyAttackTimeline(EnemyAttackPlan.Box(
                "telegraph", 1f, 0f, 0f, true,
                Vector2.zero, Vector2.one, 1, Vector2.right, 1, 0f, 1, 0f));
            var commitOnly = new EnemyAttackTimeline(EnemyAttackPlan.Box(
                "commit", 0f, 1f, 0f, true,
                Vector2.zero, Vector2.one, 1, Vector2.right, 1, 0f, 1, 0f));
            var recoveryOnly = new EnemyAttackTimeline(EnemyAttackPlan.Box(
                "recovery", 0f, 0f, 1f, true,
                Vector2.zero, Vector2.one, 1, Vector2.right, 1, 0f, 1, 0f));
            var noDuration = new EnemyAttackTimeline(EnemyAttackPlan.Box(
                "instant", 0f, 0f, 0f, true,
                Vector2.zero, Vector2.one, 1, Vector2.right, 1, 0f, 1, 0f));

            Assert.That(telegraphOnly.Evaluate(float.NaN), Is.EqualTo(EnemyAttackPhase.Telegraph));
            Assert.That(telegraphOnly.Evaluate(float.NegativeInfinity), Is.EqualTo(EnemyAttackPhase.Telegraph));
            Assert.That(telegraphOnly.Evaluate(float.PositiveInfinity), Is.EqualTo(EnemyAttackPhase.Complete));
            Assert.That(telegraphOnly.Evaluate(1f), Is.EqualTo(EnemyAttackPhase.Complete));
            Assert.That(commitOnly.Evaluate(0f), Is.EqualTo(EnemyAttackPhase.Commit));
            Assert.That(recoveryOnly.Evaluate(0f), Is.EqualTo(EnemyAttackPhase.Recovery));
            Assert.That(noDuration.Evaluate(0f), Is.EqualTo(EnemyAttackPhase.Complete));
        }

        [Test]
        public void AttackPlanAndTimelineExposeReadOnlyValueContracts()
        {
            Assert.That(typeof(EnemyAttackPlan).IsValueType, Is.True);
            Assert.That(typeof(EnemyAttackTimeline).IsValueType, Is.True);

            foreach (var property in typeof(EnemyAttackPlan).GetProperties())
            {
                Assert.That(property.CanWrite, Is.False, property.Name);
            }
        }

        private static void AssertOutOfRange(string parameterName, TestDelegate action)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                action,
                "Non-finite and unsupported inputs must fail fast.");
            Assert.That(exception.ParamName, Is.EqualTo(parameterName));
        }

        private static void AssertAttackPlanOutOfRange(
            string parameterName,
            float invalid,
            AttackPlanFloatSlot slot)
        {
            var telegraph = slot == AttackPlanFloatSlot.Telegraph ? invalid : 0f;
            var commit = slot == AttackPlanFloatSlot.Commit ? invalid : 0f;
            var recovery = slot == AttackPlanFloatSlot.Recovery ? invalid : 0f;
            var offset = new Vector2(
                slot == AttackPlanFloatSlot.OffsetX ? invalid : 0f,
                slot == AttackPlanFloatSlot.OffsetY ? invalid : 0f);
            var size = new Vector2(
                slot == AttackPlanFloatSlot.SizeX ? invalid : 1f,
                slot == AttackPlanFloatSlot.SizeY ? invalid : 1f);
            var aim = new Vector2(
                slot == AttackPlanFloatSlot.AimX ? invalid : 1f,
                slot == AttackPlanFloatSlot.AimY ? invalid : 0f);
            var hitInterval = slot == AttackPlanFloatSlot.HitInterval ? invalid : 0f;
            var knockback = slot == AttackPlanFloatSlot.Knockback ? invalid : 0f;

            AssertOutOfRange(
                parameterName,
                () => EnemyAttackPlan.Box(
                    "box",
                    telegraph,
                    commit,
                    recovery,
                    true,
                    offset,
                    size,
                    1,
                    aim,
                    1,
                    hitInterval,
                    1,
                    knockback));
        }

        private enum AttackPlanFloatSlot
        {
            Telegraph,
            Commit,
            Recovery,
            OffsetX,
            OffsetY,
            SizeX,
            SizeY,
            AimX,
            AimY,
            HitInterval,
            Knockback
        }

        private static void AssertSaturatedAndFinite(EnemyWaveStats stats)
        {
            Assert.That(stats.MaxHp, Is.EqualTo(int.MaxValue));
            Assert.That(stats.Damage, Is.EqualTo(int.MaxValue));
            Assert.That(stats.MoveSpeed, Is.EqualTo(float.MaxValue));
            Assert.That(float.IsNaN(stats.MoveSpeed), Is.False);
            Assert.That(float.IsInfinity(stats.MoveSpeed), Is.False);
        }
    }
}
