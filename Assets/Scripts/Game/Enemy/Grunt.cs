using UnityEngine;

/// <summary>
/// 杂兵：最简单的近战敌人。
/// 追击 → 前摇(0.5s黄闪) → 单次挥砍 → 回到追击。
/// 所有攻击都可弹反。
/// </summary>
public class Grunt : EnemyBase
{
    protected override void Awake()
    {
        // 覆盖基础属性
        hp = 30;
        maxHp = 30;
        moveSpeed = 2f;
        damage = 10;
        attackRange = 1.2f;
        telegraphDuration = 0.5f;
        attackDuration = 0.3f;
        isCurrentAttackParryable = true;

        base.Awake();
    }

    protected override void OnAttackStart()
    {
        // 在攻击方向创建命中判定
        Vector2 hitPos = (Vector2)transform.position + new Vector2(_facingDirection * 0.6f, 0.2f);

        // 简单的距离判定攻击（无需 hitbox 子物体）
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitPos, new Vector2(0.8f, 0.6f), 0f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    // 杂兵攻击是可弹反的，所以传入一个虚拟hitbox信息
                    // 实际弹反判定在 Hurtbox.ReceiveHit 中处理
                    hurtbox.ReceiveHit(damage, _facingDirection, 5f, null);
                }
            }
        }
    }
}
