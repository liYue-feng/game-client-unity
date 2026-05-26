using UnityEngine;
using System;

/// <summary>
/// 战斗全局事件：跨组件通信的静态事件中心。
/// 用于解耦 Hitbox/Hurtbox、特效系统、UI、连击计数等。
/// 位置参数用于在命中点生成特效。
/// </summary>
public static class CombatEvents
{
    /// <summary>玩家命中敌人：参数=(命中位置, 伤害值)</summary>
    public static event Action<Vector3, int> OnHitLanded;

    /// <summary>玩家受到伤害：参数=(受伤位置, 伤害值)</summary>
    public static event Action<Vector3, int> OnDamageTaken;

    /// <summary>弹反成功：参数=(弹反位置)</summary>
    public static event Action<Vector3> OnParrySuccess;

    /// <summary>玩家死亡</summary>
    public static event Action OnPlayerDeath;

    /// <summary>敌人死亡：参数=(敌人GameObject)</summary>
    public static event Action<GameObject> OnEnemyDeath;

    /// <summary>耐力归零破防：参数=(位置)</summary>
    public static event Action<Vector3> OnStaminaBreak;

    // 触发方法——由各系统调用
    public static void InvokeHitLanded(Vector3 pos, int dmg) => OnHitLanded?.Invoke(pos, dmg);
    public static void InvokeDamageTaken(Vector3 pos, int dmg) => OnDamageTaken?.Invoke(pos, dmg);
    public static void InvokeParrySuccess(Vector3 pos) => OnParrySuccess?.Invoke(pos);
    public static void InvokePlayerDeath() => OnPlayerDeath?.Invoke();
    public static void InvokeEnemyDeath(GameObject enemy) => OnEnemyDeath?.Invoke(enemy);
    public static void InvokeStaminaBreak(Vector3 pos) => OnStaminaBreak?.Invoke(pos);
}
