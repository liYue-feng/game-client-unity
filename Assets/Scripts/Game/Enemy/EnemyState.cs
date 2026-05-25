using UnityEngine;

/// <summary>
/// 敌人战斗状态枚举。
/// 与 PlayerState 类似但增加了巡逻和前摇状态，
/// 敌人的攻击必须有可见的前摇（Telegraph），给玩家弹反的反应时间。
/// </summary>
public enum EnemyState
{
    /// <summary>待机不动</summary>
    Idle,
    /// <summary>巡逻（随机走动）</summary>
    Patrol,
    /// <summary>追击玩家</summary>
    Chase,
    /// <summary>出招前摇（警示玩家）</summary>
    Telegraph,
    /// <summary>攻击命中帧</summary>
    Attack,
    /// <summary>受击硬直</summary>
    Hurt,
    /// <summary>死亡</summary>
    Die,
    /// <summary>眩晕（被弹反后）</summary>
    Stunned
}
