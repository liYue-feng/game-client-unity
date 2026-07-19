using UnityEngine;
using System;

/// <summary>
/// 战斗全局事件：跨组件通信的静态事件中心。
/// 用于解耦 Hitbox/Hurtbox、特效系统、UI、连击计数等。
/// 位置参数用于在命中点生成特效。
/// </summary>
public static class CombatEvents
{
    public static event Action<CombatFeedbackContext> OnHitResolved;

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
    public static void InvokeHitResolved(CombatFeedbackContext context)
    {
        var resolvedHandlers = OnHitResolved;
        if (resolvedHandlers != null)
        {
            foreach (Action<CombatFeedbackContext> handler in resolvedHandlers.GetInvocationList())
            {
                try
                {
                    handler(context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        if (context.Result != Game.Gameplay.CombatHitResult.Damaged)
        {
            return;
        }

        if (context.TargetKind == CombatFeedbackTargetKind.Player)
        {
            InvokeDamageHandlersSafely(
                OnDamageTaken,
                context.Position,
                context.AppliedDamage);
        }
        else
        {
            InvokeDamageHandlersSafely(
                OnHitLanded,
                context.Position,
                context.AppliedDamage);
        }
    }

    private static void InvokeDamageHandlersSafely(
        Action<Vector3, int> handlers,
        Vector3 position,
        int appliedDamage)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action<Vector3, int> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(position, appliedDamage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    public static void InvokeHitLanded(Vector3 pos, int dmg) => OnHitLanded?.Invoke(pos, dmg);
    public static void InvokeDamageTaken(Vector3 pos, int dmg) => OnDamageTaken?.Invoke(pos, dmg);
    public static void InvokeParrySuccess(Vector3 pos) => OnParrySuccess?.Invoke(pos);
    public static void InvokePlayerDeath() => OnPlayerDeath?.Invoke();
    public static void InvokeEnemyDeath(GameObject enemy) => OnEnemyDeath?.Invoke(enemy);
    public static void InvokeStaminaBreak(Vector3 pos) => OnStaminaBreak?.Invoke(pos);
}
