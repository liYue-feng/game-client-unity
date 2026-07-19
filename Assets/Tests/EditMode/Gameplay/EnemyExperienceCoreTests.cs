using System;
using Game.Gameplay;
using NUnit.Framework;

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

        private static void AssertOutOfRange(string parameterName, TestDelegate action)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                action,
                "Non-finite and unsupported inputs must fail fast.");
            Assert.That(exception.ParamName, Is.EqualTo(parameterName));
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
