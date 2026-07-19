using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// B2 敌人预警只允许使用与真实命中共享数据的矩形或圆形。
    /// </summary>
    public enum EnemyTelegraphShape
    {
        Box,
        Circle
    }

    /// <summary>
    /// 纯时间线阶段让场景协程只负责消费计划，不再自行推导攻击时序。
    /// </summary>
    public enum EnemyAttackPhase
    {
        Telegraph,
        Commit,
        Recovery,
        Complete
    }

    /// <summary>
    /// 攻击开始前冻结的只读快照，确保预警与真实判定消费同一份几何和方向。
    /// </summary>
    public readonly struct EnemyAttackPlan
    {
        private EnemyAttackPlan(
            EnemyTelegraphShape shape,
            string attackId,
            float telegraphDuration,
            float commitDuration,
            float recoveryDuration,
            bool isParryable,
            Vector2 localOffset,
            Vector2 size,
            float radius,
            int facingDirection,
            Vector2 aimDirection,
            int hitCount,
            float hitInterval,
            int damage,
            float knockback)
        {
            if (shape != EnemyTelegraphShape.Box && shape != EnemyTelegraphShape.Circle)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(shape),
                    shape,
                    "Telegraph shape must be Box or Circle.");
            }

            GameplayNumericGuard.RequireFinite(telegraphDuration, nameof(telegraphDuration));
            GameplayNumericGuard.RequireFinite(commitDuration, nameof(commitDuration));
            GameplayNumericGuard.RequireFinite(recoveryDuration, nameof(recoveryDuration));
            RequireFinite(localOffset, nameof(localOffset));
            RequireFinite(size, nameof(size));
            GameplayNumericGuard.RequireFinite(radius, nameof(radius));
            RequireFinite(aimDirection, nameof(aimDirection));
            GameplayNumericGuard.RequireFinite(hitInterval, nameof(hitInterval));
            GameplayNumericGuard.RequireFinite(knockback, nameof(knockback));

            Shape = shape;
            AttackId = attackId ?? string.Empty;
            TelegraphDuration = System.Math.Max(0f, telegraphDuration);
            HitCount = System.Math.Max(1, hitCount);
            HitInterval = System.Math.Max(0f, hitInterval);
            var comboWindow = CalculateComboWindow(HitCount, HitInterval);
            var normalizedCommitDuration = System.Math.Max(0f, commitDuration);
            CommitDuration = System.Math.Max(
                normalizedCommitDuration,
                comboWindow);
            var commitDurationParameterName = comboWindow > normalizedCommitDuration
                ? nameof(hitInterval)
                : nameof(commitDuration);
            RecoveryDuration = System.Math.Max(0f, recoveryDuration);
            IsParryable = isParryable;
            LocalOffset = localOffset;
            Size = new Vector2(System.Math.Abs(size.x), System.Math.Abs(size.y));
            Radius = System.Math.Max(0f, radius);
            FacingDirection = facingDirection < 0 ? -1 : 1;
            AimDirection = NormalizeDirection(aimDirection, FacingDirection);
            Damage = System.Math.Max(0, damage);
            Knockback = System.Math.Max(0f, knockback);
            TotalDuration = CalculateTotalDuration(
                TelegraphDuration,
                CommitDuration,
                RecoveryDuration,
                commitDurationParameterName);
            IsValid = AttackId.Length > 0 &&
                (Shape == EnemyTelegraphShape.Box
                    ? Size.x > 0f && Size.y > 0f
                    : Radius > 0f);
        }

        public string AttackId { get; }
        public float TelegraphDuration { get; }
        public float CommitDuration { get; }
        public float RecoveryDuration { get; }
        public bool IsParryable { get; }
        public EnemyTelegraphShape Shape { get; }
        public Vector2 LocalOffset { get; }
        public Vector2 Size { get; }
        public float Radius { get; }
        public int FacingDirection { get; }
        public Vector2 AimDirection { get; }
        public int HitCount { get; }
        public float HitInterval { get; }
        public int Damage { get; }
        public float Knockback { get; }
        public float TotalDuration { get; }
        public bool IsValid { get; }

        /// <summary>
        /// 创建矩形攻击快照；负值按战斗下限归一，无法表示的时长因不可调度而拒绝。
        /// </summary>
        public static EnemyAttackPlan Box(
            string attackId,
            float telegraphDuration,
            float commitDuration,
            float recoveryDuration,
            bool isParryable,
            Vector2 localOffset,
            Vector2 size,
            int facingDirection,
            Vector2 aimDirection,
            int hitCount,
            float hitInterval,
            int damage,
            float knockback)
        {
            return new EnemyAttackPlan(
                EnemyTelegraphShape.Box,
                attackId,
                telegraphDuration,
                commitDuration,
                recoveryDuration,
                isParryable,
                localOffset,
                size,
                0f,
                facingDirection,
                aimDirection,
                hitCount,
                hitInterval,
                damage,
                knockback);
        }

        /// <summary>
        /// 创建圆形攻击快照；半径与时序在进入场景协程前完成归一化。
        /// </summary>
        public static EnemyAttackPlan Circle(
            string attackId,
            float telegraphDuration,
            float commitDuration,
            float recoveryDuration,
            bool isParryable,
            Vector2 localOffset,
            float radius,
            int facingDirection,
            Vector2 aimDirection,
            int hitCount,
            float hitInterval,
            int damage,
            float knockback)
        {
            return new EnemyAttackPlan(
                EnemyTelegraphShape.Circle,
                attackId,
                telegraphDuration,
                commitDuration,
                recoveryDuration,
                isParryable,
                localOffset,
                Vector2.zero,
                radius,
                facingDirection,
                aimDirection,
                hitCount,
                hitInterval,
                damage,
                knockback);
        }

        private static void RequireFinite(Vector2 value, string parameterName)
        {
            if (float.IsNaN(value.x) ||
                float.IsInfinity(value.x) ||
                float.IsNaN(value.y) ||
                float.IsInfinity(value.y))
            {
                throw new System.ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Vector components must be finite.");
            }
        }

        private static Vector2 NormalizeDirection(Vector2 direction, int facingDirection)
        {
            // double 中间值避免有限 float 极值平方后溢出，导致归一化退化为零向量。
            var x = (double)direction.x;
            var y = (double)direction.y;
            var magnitude = System.Math.Sqrt(x * x + y * y);
            return magnitude > 0d
                ? new Vector2((float)(x / magnitude), (float)(y / magnitude))
                : new Vector2(facingDirection, 0f);
        }

        private static float CalculateComboWindow(int hitCount, float hitInterval)
        {
            var result = (double)(hitCount - 1) * hitInterval;
            if (result > float.MaxValue)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(hitInterval),
                    hitInterval,
                    "Hit interval and count exceed the representable combo window.");
            }

            return (float)result;
        }

        private static float CalculateTotalDuration(
            float telegraphDuration,
            float commitDuration,
            float recoveryDuration,
            string commitDurationParameterName)
        {
            var remaining = (double)float.MaxValue - telegraphDuration;
            if (commitDuration > remaining)
            {
                throw new System.ArgumentOutOfRangeException(
                    commitDurationParameterName,
                    commitDuration,
                    "Telegraph and commit durations exceed the representable timeline.");
            }

            var commitEnd = (double)telegraphDuration + commitDuration;
            var commitBoundary = (float)commitEnd;
            if (commitDuration > 0f && commitBoundary <= telegraphDuration)
            {
                throw new System.ArgumentOutOfRangeException(
                    commitDurationParameterName,
                    commitDuration,
                    "Positive commit duration must advance the float timeline boundary.");
            }

            remaining = (double)float.MaxValue - commitEnd;
            if (recoveryDuration > remaining)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(recoveryDuration),
                    recoveryDuration,
                    "Attack phase durations exceed the representable timeline.");
            }

            var totalDuration = (float)(commitEnd + recoveryDuration);
            if (recoveryDuration > 0f && totalDuration <= commitBoundary)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(recoveryDuration),
                    recoveryDuration,
                    "Positive recovery duration must advance the float timeline boundary.");
            }

            return totalDuration;
        }
    }

    /// <summary>
    /// 无可变游标的阶段求值器，允许协程取消后不留下跨攻击状态。
    /// </summary>
    public readonly struct EnemyAttackTimeline
    {
        private readonly EnemyAttackPlan _plan;

        public EnemyAttackTimeline(EnemyAttackPlan plan)
        {
            _plan = plan;
        }

        /// <summary>
        /// 按冻结时长纯求值；NaN/负时间视为起点，正无穷明确表示流程完成。
        /// </summary>
        public EnemyAttackPhase Evaluate(float elapsed)
        {
            if (float.IsPositiveInfinity(elapsed))
            {
                return EnemyAttackPhase.Complete;
            }

            var time = float.IsNaN(elapsed) || elapsed < 0f
                ? 0f
                : elapsed;
            if (time < _plan.TelegraphDuration)
            {
                return EnemyAttackPhase.Telegraph;
            }

            var commitEnd = (float)((double)_plan.TelegraphDuration + _plan.CommitDuration);
            if (time < commitEnd)
            {
                return EnemyAttackPhase.Commit;
            }

            if (time < _plan.TotalDuration)
            {
                return EnemyAttackPhase.Recovery;
            }

            return EnemyAttackPhase.Complete;
        }
    }
}
