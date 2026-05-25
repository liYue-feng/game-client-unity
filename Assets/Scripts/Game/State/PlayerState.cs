using UnityEngine;

/// <summary>
/// 玩家战斗状态枚举。
/// 设计为有限状态机，每个状态有明确的转换规则，
/// 避免动画混合导致的逻辑混乱。
/// </summary>
public enum PlayerState
{
    /// <summary>站立不动</summary>
    Idle,
    /// <summary>水平移动</summary>
    Run,
    /// <summary>轻攻击第一段</summary>
    Attack1,
    /// <summary>轻攻击第二段（连击）</summary>
    Attack2,
    /// <summary>轻攻击第三段（连击终结）</summary>
    Attack3,
    /// <summary>蓄力重击</summary>
    HeavyAttack,
    /// <summary>弹反姿态（窗口期）</summary>
    Parry,
    /// <summary>弹反成功，进入反击窗口</summary>
    ParrySuccess,
    /// <summary>冲刺闪避</summary>
    Dash,
    /// <summary>受击硬直</summary>
    Hurt,
    /// <summary>死亡</summary>
    Die
}
