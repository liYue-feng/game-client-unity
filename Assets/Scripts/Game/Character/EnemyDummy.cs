using UnityEngine;

/// <summary>
/// 木桩敌人：最简单的可攻击目标，用于测试战斗系统。
/// 有 HP、可受击闪烁、死亡后消失。没有 AI，不会攻击。
/// </summary>
public class EnemyDummy : MonoBehaviour
{
    [Tooltip("生命值")]
    public int hp = 50;
    [Tooltip("最大生命值"]
    public int maxHp = 50;

    private SpriteRenderer _sprite;
    private HitEffectPlayer _hitEffect;
    private Hurtbox _hurtbox;
    private bool _isDead;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _hitEffect = GetComponent<HitEffectPlayer>();
        _hurtbox = GetComponent<Hurtbox>();

        // 如果没有 Hurtbox，自动添加
        if (_hurtbox == null)
        {
            _hurtbox = gameObject.AddComponent<Hurtbox>();
        }
    }

    /// <summary>
    /// 受到伤害。由 Hurtbox 调用。
    /// 因为木桩没有 CharacterStats，需要自己的 HP 管理。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (_isDead) return;

        hp = Mathf.Max(0, hp - amount);

        // 受击闪烁
        if (_hitEffect != null) _hitEffect.PlayHitEffect();

        if (hp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        _isDead = true;
        CombatEvents.InvokeEnemyDeath(gameObject);

        // 简单消失（后续加死亡动画）
        gameObject.SetActive(false);
    }
}
