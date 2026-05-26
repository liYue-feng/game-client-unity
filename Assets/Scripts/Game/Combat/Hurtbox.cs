using UnityEngine;

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
    /// <param name="damage">伤害值</param>
    /// <param name="knockbackDirX">击退方向X</param>
    /// <param name="knockbackForce">击退力度</param>
    /// <param name="sourceHitbox">来源 hitbox（用于弹反判定）</param>
    public void ReceiveHit(int damage, float knockbackDirX, float knockbackForce, Hitbox sourceHitbox)
    {
        // 弹反判定：玩家在弹反窗口内 + 攻击可弹反
        if (stateMachine != null && stateMachine.IsInParryWindow && sourceHitbox.isParryable)
        {
            stateMachine.OnParrySuccess();
            return;
        }

        // 实际受伤：玩家走 CharacterStats，敌人走 EnemyBase
        if (stats != null)
        {
            stats.TakeDamage(damage);
            CombatEvents.InvokeDamageTaken(transform.position, damage);
        }
        else
        {
            EnemyBase enemy = GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, knockbackDirX, knockbackForce);
                return; // EnemyBase 内部处理击退，不重复施加
            }
        }

        // 通知状态机进入受击
        if (stateMachine != null)
        {
            if (stats != null && stats.IsDead)
            {
                stateMachine.ForceDie();
                CombatEvents.InvokePlayerDeath();
            }
            else
            {
                stateMachine.ForceHurt();
            }
        }

        // 击退（仅玩家，敌人由 EnemyBase.TakeDamage 处理）
        if (stats != null)
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.AddForce(new Vector2(knockbackDirX * knockbackForce, 2f), ForceMode2D.Impulse);
            }
        }
    }
}
