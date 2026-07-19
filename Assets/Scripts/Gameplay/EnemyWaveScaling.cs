namespace Game.Gameplay
{
    /// <summary>
    /// 敌人实例首次初始化后捕获的只读战斗基线，避免池复用状态成为后续波次输入。
    /// </summary>
    public readonly struct EnemyStatBaseline
    {
        /// <summary>
        /// 创建可复用基线；有限负值按战斗下限归一，非有限值立即拒绝。
        /// </summary>
        public EnemyStatBaseline(
            int maxHp,
            int damage,
            float moveSpeed,
            float damageReduction,
            float telegraphDuration,
            float attackDuration)
        {
            GameplayNumericGuard.RequireFinite(moveSpeed, nameof(moveSpeed));
            GameplayNumericGuard.RequireFinite(damageReduction, nameof(damageReduction));
            GameplayNumericGuard.RequireFinite(telegraphDuration, nameof(telegraphDuration));
            GameplayNumericGuard.RequireFinite(attackDuration, nameof(attackDuration));
            MaxHp = System.Math.Max(1, maxHp);
            Damage = System.Math.Max(0, damage);
            MoveSpeed = System.Math.Max(0f, moveSpeed);
            DamageReduction = System.Math.Max(0f, System.Math.Min(1f, damageReduction));
            TelegraphDuration = System.Math.Max(0f, telegraphDuration);
            AttackDuration = System.Math.Max(0f, attackDuration);
        }

        public int MaxHp { get; }
        public int Damage { get; }
        public float MoveSpeed { get; }
        public float DamageReduction { get; }
        public float TelegraphDuration { get; }
        public float AttackDuration { get; }
    }

    /// <summary>
    /// 波次成长倍率；负倍率归零，防止产生反向生命、伤害或速度。
    /// </summary>
    public readonly struct EnemyWaveMultipliers
    {
        /// <summary>
        /// 创建有限成长倍率；有限负值归零，非有限值立即拒绝。
        /// </summary>
        public EnemyWaveMultipliers(float hp, float damage, float speed)
        {
            GameplayNumericGuard.RequireFinite(hp, nameof(hp));
            GameplayNumericGuard.RequireFinite(damage, nameof(damage));
            GameplayNumericGuard.RequireFinite(speed, nameof(speed));
            Hp = System.Math.Max(0f, hp);
            Damage = System.Math.Max(0f, damage);
            Speed = System.Math.Max(0f, speed);
        }

        public float Hp { get; }
        public float Damage { get; }
        public float Speed { get; }
    }

    /// <summary>
    /// 单次出生使用的派生属性快照，不作为下一次缩放的输入。
    /// </summary>
    public readonly struct EnemyWaveStats
    {
        /// <summary>
        /// 创建可直接应用的有效属性快照；速度必须有限，数值不得低于战斗下限。
        /// </summary>
        public EnemyWaveStats(int maxHp, int damage, float moveSpeed)
        {
            GameplayNumericGuard.RequireFinite(moveSpeed, nameof(moveSpeed));
            MaxHp = System.Math.Max(1, maxHp);
            Damage = System.Math.Max(0, damage);
            MoveSpeed = System.Math.Max(0f, moveSpeed);
        }

        public int MaxHp { get; }
        public int Damage { get; }
        public float MoveSpeed { get; }
    }

    /// <summary>
    /// 从不可变基线纯计算波次属性，确保结果不受实例复用顺序影响。
    /// </summary>
    public static class EnemyWaveScaling
    {
        /// <summary>
        /// 计算指定波次的属性；零或负索引统一视为未缩放的第零波。
        /// </summary>
        public static EnemyWaveStats Calculate(
            EnemyStatBaseline baseline,
            int waveIndex,
            EnemyWaveMultipliers multipliers)
        {
            // 每次都从只读基线推导，避免池对象复用次数改变同一波次结果。
            var wave = System.Math.Max(0, waveIndex);
            return new EnemyWaveStats(
                CalculateIntegralStat(baseline.MaxHp, multipliers.Hp, wave, 1),
                CalculateIntegralStat(baseline.Damage, multipliers.Damage, wave, 0),
                CalculateSpeed(baseline.MoveSpeed, multipliers.Speed, wave));
        }

        private static int CalculateIntegralStat(
            int baseline,
            float multiplier,
            int wave,
            int minimum)
        {
            var normalizedBaseline = System.Math.Max(minimum, baseline);
            if (normalizedBaseline == 0)
            {
                return 0;
            }

            var power = System.Math.Pow(multiplier, wave);
            var scaled = normalizedBaseline * power;
            if (double.IsPositiveInfinity(scaled) || scaled >= int.MaxValue)
            {
                return int.MaxValue;
            }

            var rounded = System.Math.Round(
                scaled,
                System.MidpointRounding.ToEven);
            return System.Math.Max(minimum, (int)rounded);
        }

        private static float CalculateSpeed(float baseline, float multiplier, int wave)
        {
            var normalizedBaseline = System.Math.Max(0f, baseline);
            if (normalizedBaseline == 0f)
            {
                return 0f;
            }

            var scaled = normalizedBaseline * System.Math.Pow(multiplier, wave);
            if (double.IsPositiveInfinity(scaled) || scaled >= float.MaxValue)
            {
                return float.MaxValue;
            }

            return (float)scaled;
        }
    }
}
