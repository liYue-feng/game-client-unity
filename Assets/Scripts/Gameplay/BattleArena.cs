namespace Game.Gameplay
{
    /// <summary>
    /// 战场横向出生侧别。显式数值让纯规划器无需依赖 Unity 朝向状态。
    /// </summary>
    public enum ArenaSpawnSide
    {
        Left = -1,
        Right = 1
    }

    /// <summary>
    /// 归一化后的战场横向边界，避免调用方传入端点顺序影响规划结果。
    /// </summary>
    public readonly struct BattleArenaBounds
    {
        /// <summary>
        /// 创建有序边界；非有限端点会让后续夹紧失去确定性，因此立即拒绝。
        /// </summary>
        public BattleArenaBounds(float minX, float maxX)
        {
            GameplayNumericGuard.RequireFinite(minX, nameof(minX));
            GameplayNumericGuard.RequireFinite(maxX, nameof(maxX));
            MinX = System.Math.Min(minX, maxX);
            MaxX = System.Math.Max(minX, maxX);
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float CenterX => MinX * 0.5f + MaxX * 0.5f;
        public float Width
        {
            get
            {
                var width = (double)MaxX - MinX;
                return width >= float.MaxValue ? float.MaxValue : (float)width;
            }
        }
    }

    /// <summary>
    /// 按战场边界和接敌距离生成确定性的世界坐标出生点。
    /// </summary>
    public static class ArenaSpawnPlanner
    {
        /// <summary>
        /// 优先选择指定侧别；空间不足时先保证边界内且可接敌，再选择更远的合法点。
        /// 非有限参数和未知侧别无法形成稳定世界坐标，因此立即拒绝。
        /// </summary>
        public static float PlanX(
            BattleArenaBounds bounds,
            float playerX,
            ArenaSpawnSide preferredSide,
            float cameraHalfWidth,
            float spawnMargin,
            float chaseRange)
        {
            GameplayNumericGuard.RequireFinite(playerX, nameof(playerX));
            GameplayNumericGuard.RequireFinite(cameraHalfWidth, nameof(cameraHalfWidth));
            GameplayNumericGuard.RequireFinite(spawnMargin, nameof(spawnMargin));
            GameplayNumericGuard.RequireFinite(chaseRange, nameof(chaseRange));
            if (preferredSide != ArenaSpawnSide.Left && preferredSide != ArenaSpawnSide.Right)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(preferredSide),
                    preferredSide,
                    "Spawn side must be Left or Right.");
            }

            // double 中间值避免有限 float 极值相加时先溢出成 Infinity。
            var margin = System.Math.Max(0d, (double)spawnMargin);
            var safeMin = (double)bounds.MinX + margin;
            var safeMax = (double)bounds.MaxX - margin;
            if (safeMin > safeMax)
            {
                return bounds.CenterX;
            }

            var clampedPlayerX = System.Math.Max(
                safeMin,
                System.Math.Min(safeMax, (double)playerX));
            var reachable = System.Math.Max(0d, (double)chaseRange);
            var desired = System.Math.Min(
                reachable,
                System.Math.Max(0d, (double)cameraHalfWidth) + margin);
            var requiredSeparation = System.Math.Min(
                reachable,
                System.Math.Max(0d, (double)cameraHalfWidth));

            double Candidate(ArenaSpawnSide side) => System.Math.Max(
                safeMin,
                System.Math.Min(safeMax, clampedPlayerX + (int)side * desired));
            bool Satisfies(double candidate, ArenaSpawnSide side)
            {
                var delta = candidate - clampedPlayerX;
                return System.Math.Sign(delta) == (int)side &&
                    System.Math.Abs(delta) >= requiredSeparation - 0.001f &&
                    System.Math.Abs(delta) <= reachable + 0.001f;
            }

            var preferred = Candidate(preferredSide);
            var oppositeSide = preferredSide == ArenaSpawnSide.Left
                ? ArenaSpawnSide.Right
                : ArenaSpawnSide.Left;
            var opposite = Candidate(oppositeSide);
            if (Satisfies(preferred, preferredSide))
            {
                return (float)preferred;
            }

            if (Satisfies(opposite, oppositeSide))
            {
                return (float)opposite;
            }

            // 狭窄战场无法完全避开视区时，可接敌和远离玩家优先于屏外出生。
            return System.Math.Abs(opposite - clampedPlayerX) >
                System.Math.Abs(preferred - clampedPlayerX)
                ? (float)opposite
                : (float)preferred;
        }
    }

    internal static class GameplayNumericGuard
    {
        internal static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new System.ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite.");
            }
        }
    }
}
