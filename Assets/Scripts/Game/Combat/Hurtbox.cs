using UnityEngine;
using Game.Gameplay;

/// <summary>
/// 受击判定框：挂在角色 GameObject 上，接收来自 Hitbox 的伤害。
/// 将伤害传递给 CharacterStats，并通知状态机进入受击状态。
///
/// 弹反逻辑：如果玩家处于弹反窗口（PlayerStateMachine.IsInParryWindow），
/// 且攻击 hitbox 标记为可弹反，则触发弹反成功而非受伤。
/// </summary>
public class Hurtbox : MonoBehaviour
{
    [Tooltip("所属角色的 CharacterStats")]
    public CharacterStats stats;

    [Tooltip("所属角色的状态机（玩家专用，敌人留空）")]
    public PlayerStateMachine stateMachine;

    /// <summary>
    /// 接收命中。
    /// 由 Hitbox.OnTriggerEnter2D 调用。
    /// </summary>
    public CombatHitResult ReceiveHit(CombatHit hit)
    {
        EnemyBase enemy = null;
        if (stats != null)
        {
            if (stats.IsDead)
            {
                return CombatHitResult.Ignored;
            }
        }
        else
        {
            enemy = GetComponent<EnemyBase>();
            if (enemy == null || enemy.IsDead)
            {
                return CombatHitResult.Ignored;
            }
        }

        if (stateMachine != null && stateMachine.IsInParryWindow && hit.IsParryable)
        {
            stateMachine.OnParrySuccess();
            hit.Source?.OnParried();
            return CombatHitResult.Parried;
        }

        if (stats != null)
        {
            stats.TakeDamage(hit.Damage);
            CombatEvents.InvokeDamageTaken(transform.position, hit.Damage);
        }
        else
        {
            enemy.TakeDamage(hit.Damage, hit.KnockbackDirectionX, hit.KnockbackForce);
            return CombatHitResult.Damaged;
        }

        // 通知状态机进入受击
        if (stateMachine != null && (stats == null || !stats.IsDead))
        {
            stateMachine.ForceHurt();
        }

        // 击退（仅玩家，敌人由 EnemyBase.TakeDamage 处理）
        if (stats != null && !stats.IsDead)
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.AddForce(
                    new Vector2(hit.KnockbackDirectionX * hit.KnockbackForce, 2f),
                    ForceMode2D.Impulse);
            }
        }

        return CombatHitResult.Damaged;
    }
}
